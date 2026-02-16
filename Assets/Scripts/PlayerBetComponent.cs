using UnityEngine;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;

/// <summary>
/// Manages bet display for a single betting area with win animations
/// FIXED: Counting animation is protected from being overridden
/// </summary>
public class PlayerBetComponent : MonoBehaviour
{
    #region Serialized Fields
    [Header("Total Bet Display")]
    [SerializeField] private TMP_Text totalBetAmountText;
    [SerializeField] private Image betAmountBackground;

    [Header("Chip Pool")]
    [SerializeField] private List<Chip> initialChips = new List<Chip>(6);

    [Header("Chip Spawning")]
    [SerializeField] private GameObject chipPrefab;

    [Header("Animation Settings")]
    [SerializeField] private float dropStartY = 200f;
    [SerializeField] private float dropDuration = 0.4f;
    [SerializeField] private float popScale = 1.15f;
    [SerializeField] private float popDuration = 0.15f;
    [SerializeField] private Vector2 randomOffsetRange = new Vector2(10f, 13f);

    [Header("Win Animation Settings")]
    [SerializeField] private float countingDuration = 1.5f;
    [SerializeField] private float maxBackgroundScale = 1.15f;
    [SerializeField] private Ease countingEase = Ease.OutQuad;

    [Header("Pop Animation Settings")]
    [SerializeField] private float popInScale = 1.2f;
    [SerializeField] private float popInDuration = 0.3f;
    [SerializeField] private Ease popEase = Ease.OutBack;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    #endregion

    #region Private Fields
    private Sprite[] chipSprites;
    private List<Chip> allChips = new List<Chip>();
    private List<Vector3> initialChipFinalPositions = new List<Vector3>();
    private List<BetData> bets = new List<BetData>();
    private double totalBetAmount = 0;
    private List<double> availableChipValues = new List<double>();

    // Animation fields
    private Vector3 originalBackgroundScale;
    private Tween countingTween;
    private Tween scaleTween;
    private Sequence popSequence;
    private bool hasStoredOriginalScale = false;
    private bool isAnimatingWin = false; // NEW: Prevents UpdateTotalDisplay from interfering
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (betAmountBackground != null)
        {
            originalBackgroundScale = betAmountBackground.transform.localScale;
            hasStoredOriginalScale = true;

            if (showDebugLogs)
            {
                Debug.Log($"[PlayerBetComponent] Awake - Stored original scale: {originalBackgroundScale}");
            }
        }

        if (totalBetAmountText == null)
        {
            Debug.LogError("[PlayerBetComponent] totalBetAmountText is NULL! Counting animation won't work!");
        }
    }

    private void OnEnable()
    {
        if (betAmountBackground != null)
        {
            StartCoroutine(PlayPopAfterFrame());
        }
    }

    private System.Collections.IEnumerator PlayPopAfterFrame()
    {
        yield return null;
        PlayPopAnimation();
    }

    private void OnValidate()
    {
        if (initialChips.Count == 0)
        {
            initialChips.AddRange(GetComponentsInChildren<Chip>(true));
            if (initialChips.Count > 6)
            {
                initialChips.RemoveRange(6, initialChips.Count - 6);
            }
        }
    }

    private void OnDestroy()
    {
        countingTween?.Kill();
        scaleTween?.Kill();
        popSequence?.Kill();

        foreach (var chip in allChips)
        {
            if (chip != null)
            {
                chip.transform.DOKill();
            }
        }
    }
    #endregion

    #region Public API - Initialization
    public void Initialize(Sprite[] sprites, List<double> chipValues = null)
    {
        chipSprites = sprites;

        if (chipValues != null)
        {
            availableChipValues = new List<double>(chipValues);
        }

        allChips.Clear();
        initialChipFinalPositions.Clear();

        foreach (var chip in initialChips)
        {
            if (chip != null)
            {
                allChips.Add(chip);
                initialChipFinalPositions.Add(chip.transform.localPosition);
            }
        }

        Clear();

        if (showDebugLogs)
        {
            Debug.Log($"[PlayerBetComponent] Initialized - totalBetAmountText is {(totalBetAmountText != null ? "ASSIGNED" : "NULL")}");
        }
    }

    public void UpdateChipValues(List<double> chipValues)
    {
        if (chipValues != null)
        {
            availableChipValues = new List<double>(chipValues);
        }
    }
    #endregion

    #region Public API - Betting
    public void AddBetFromServer(double serverAmount)
    {
        if (serverAmount <= 0) return;

        List<ChipCombinationItem> combination = GameUtilities.FindChipCombination(serverAmount, availableChipValues);

        if (combination.Count == 0)
        {
            Debug.LogWarning($"[PlayerBetComponent] Could not find chip combination for amount: {serverAmount}");
            return;
        }

        foreach (var item in combination)
        {
            AddSingleChip(item.amount, item.chipIndex);
        }

        if (showDebugLogs)
        {
            Debug.Log($"[PlayerBetComponent] Added bet: {serverAmount} using {combination.Count} chips");
        }
    }

    public void AddBet(double amount, int chipIndex)
    {
        AddSingleChip(amount, chipIndex);
    }

    /// <summary>
    /// Animate win using winRatio - counts from bet amount to win amount
    /// </summary>
    public void AnimateWinWithRatio(double winRatio)
    {
        if (totalBetAmountText == null)
        {
            Debug.LogError("[PlayerBetComponent] Cannot animate - totalBetAmountText is NULL!");
            return;
        }

        if (totalBetAmount <= 0)
        {
            Debug.LogWarning($"[PlayerBetComponent] Cannot animate - no bet placed (amount = {totalBetAmount})");
            return;
        }

        if (winRatio <= 0)
        {
            Debug.LogWarning($"[PlayerBetComponent] Cannot animate - invalid winRatio: {winRatio}");
            return;
        }

        double startAmount = totalBetAmount;
        double endAmount = totalBetAmount * winRatio;

        Debug.Log($"[PlayerBetComponent] WIN ANIMATION: Bet={startAmount:F2}, Ratio=1:{winRatio}, Final={endAmount:F2}");

        // Play animations
        PlayCountingAnimation(startAmount, endAmount);
        PlayBackgroundScaleAnimation();
    }

    public void RemoveLastBet()
    {
        if (bets.Count == 0) return;

        int lastIndex = bets.Count - 1;
        BetData lastBet = bets[lastIndex];

        totalBetAmount -= lastBet.amount;
        bets.RemoveAt(lastIndex);

        if (lastIndex < allChips.Count && allChips[lastIndex] != null)
        {
            Chip chip = allChips[lastIndex];
            chip.transform.DOKill();
            chip.SetActive(false);
        }

        UpdateTotalDisplay();

        if (bets.Count == 0)
        {
            gameObject.SetActive(false);
        }
    }

    public void Clear()
    {
        countingTween?.Kill();
        scaleTween?.Kill();
        popSequence?.Kill();

        isAnimatingWin = false;
        bets.Clear();
        totalBetAmount = 0;

        foreach (var chip in allChips)
        {
            if (chip != null)
            {
                chip.transform.DOKill();
            }
        }

        for (int i = allChips.Count - 1; i >= initialChips.Count; i--)
        {
            if (allChips[i] != null)
            {
                Destroy(allChips[i].gameObject);
            }
            allChips.RemoveAt(i);
        }

        foreach (var chip in initialChips)
        {
            if (chip != null)
            {
                chip.transform.localScale = Vector3.one;
                chip.SetActive(false);
            }
        }

        if (betAmountBackground != null && hasStoredOriginalScale)
        {
            betAmountBackground.transform.localScale = originalBackgroundScale;
        }

        UpdateTotalDisplay();
        gameObject.SetActive(false);
    }

    public double GetTotalBet() => totalBetAmount;

    public int GetBetCount() => bets.Count;

    public bool HasBets() => bets.Count > 0;

    public List<BetData> GetBetData() => new List<BetData>(bets);
    #endregion

    #region Private Methods - Chip Management
    private void AddSingleChip(double amount, int chipIndex, bool skipDisplay = false)
    {
        if (chipIndex < 0 || chipIndex >= chipSprites.Length)
        {
            Debug.LogWarning($"[PlayerBetComponent] Invalid chipIndex: {chipIndex}");
            return;
        }

        bets.Add(new BetData { amount = amount, chipIndex = chipIndex });
        totalBetAmount += amount;

        int chipSlot = bets.Count - 1;
        Chip chip = GetOrSpawnChip(chipSlot);

        if (chip != null)
        {
            Vector3 finalPosition;

            if (chipSlot < initialChips.Count)
            {
                finalPosition = initialChipFinalPositions[chipSlot];
            }
            else
            {
                finalPosition = CalculateSpawnedChipPosition();
            }

            chip.SetSprite(chipSprites[chipIndex]);
            chip.SetAmount(GameUtilities.FormatCurrency(amount));
            chip.SetActive(true);

            Vector3 startPosition = new Vector3(finalPosition.x, dropStartY, finalPosition.z);
            chip.transform.localPosition = startPosition;

            AnimateChipDrop(chip, finalPosition);
        }

        if (!skipDisplay)
        {
            UpdateTotalDisplay();
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    private Chip GetOrSpawnChip(int index)
    {
        if (index < allChips.Count && allChips[index] != null)
        {
            return allChips[index];
        }

        if (chipPrefab == null)
        {
            Debug.LogError("[PlayerBetComponent] chipPrefab is null!");
            return null;
        }

        GameObject chipObj = Instantiate(chipPrefab, transform);
        Chip newChip = chipObj.GetComponent<Chip>();

        if (newChip == null)
        {
            Debug.LogError("[PlayerBetComponent] Spawned chip prefab doesn't have Chip component!");
            Destroy(chipObj);
            return null;
        }

        allChips.Add(newChip);
        return newChip;
    }

    private Vector3 CalculateSpawnedChipPosition()
    {
        float offsetX = Random.Range(-randomOffsetRange.x, randomOffsetRange.x);
        float offsetY = Random.Range(-randomOffsetRange.y, randomOffsetRange.y);

        if (initialChipFinalPositions.Count == 0)
        {
            return new Vector3(offsetX, offsetY, 0);
        }

        int randomIndex = Random.Range(0, initialChipFinalPositions.Count);
        Vector3 basePosition = initialChipFinalPositions[randomIndex];

        return basePosition + new Vector3(offsetX, offsetY, 0);
    }
    #endregion

    #region Private Methods - Animation
    private void AnimateChipDrop(Chip chip, Vector3 targetPosition)
    {
        if (chip == null) return;

        chip.transform.DOKill();

        Sequence dropSequence = DOTween.Sequence();

        dropSequence.Append(
            chip.transform.DOLocalMove(targetPosition, dropDuration)
                .SetEase(Ease.OutBounce)
        );

        dropSequence.Join(
            chip.transform.DOScale(popScale, popDuration)
                .SetEase(Ease.OutBack)
        );

        dropSequence.Append(
            chip.transform.DOScale(1f, popDuration)
                .SetEase(Ease.InBack)
        );

        dropSequence.Play();
    }

    /// <summary>
    /// Counting animation with value updates every frame
    /// </summary>
    private void PlayCountingAnimation(double fromAmount, double toAmount)
    {
        if (totalBetAmountText == null)
        {
            Debug.LogError("[PlayerBetComponent] COUNTING FAILED - totalBetAmountText is NULL!");
            return;
        }

        countingTween?.Kill();
        isAnimatingWin = true;

        Debug.Log($"[PlayerBetComponent] COUNTING START: {fromAmount:F2} → {toAmount:F2}");
        Debug.Log($"[PlayerBetComponent] Duration: {countingDuration}s, Ease: {countingEase}");

        // Set initial value immediately
        totalBetAmountText.text = GameUtilities.FormatCurrency(fromAmount);

        // Skip animation if amounts are same
        if (Mathf.Approximately((float)fromAmount, (float)toAmount))
        {
            Debug.Log("[PlayerBetComponent] Amounts are equal - skipping animation");
            totalBetAmountText.text = GameUtilities.FormatCurrency(toAmount);
            isAnimatingWin = false;
            return;
        }

        int updateCount = 0;

        countingTween = DOVirtual.Float(
            (float)fromAmount,
            (float)toAmount,
            countingDuration,
            value =>
            {
                if (totalBetAmountText != null)
                {
                    string formattedValue = GameUtilities.FormatCurrency(value);
                    totalBetAmountText.text = formattedValue;
                    updateCount++;

                    if (updateCount % 5 == 0)
                    {
                        Debug.Log($"[PlayerBetComponent] Update {updateCount}: {value:F2} → '{formattedValue}'");
                    }
                }
            })
        .SetEase(countingEase)
        .SetUpdate(true) // Use unscaled time
        .OnStart(() =>
        {
            Debug.Log($"[PlayerBetComponent] ✓ Counting animation STARTED");
        })
        .OnComplete(() =>
        {
            // Ensure final value is set correctly
            if (totalBetAmountText != null)
            {
                totalBetAmountText.text = GameUtilities.FormatCurrency(toAmount);
            }
            isAnimatingWin = false;
            Debug.Log($"[PlayerBetComponent] ✓ Counting animation COMPLETE");
            Debug.Log($"[PlayerBetComponent] Total updates: {updateCount}");
            Debug.Log($"[PlayerBetComponent] Final text: '{totalBetAmountText.text}'");
        })
        .SetAutoKill(true)
        .Play(); // Explicitly play the tween

        Debug.Log($"[PlayerBetComponent] DOVirtual.Float created and playing");
    }

    /// <summary>
    /// Background scale animation
    /// </summary>
    private void PlayBackgroundScaleAnimation()
    {
        if (betAmountBackground == null || !hasStoredOriginalScale)
        {
            return;
        }

        scaleTween?.Kill();

        Transform bgTransform = betAmountBackground.transform;
        Vector3 targetScale = originalBackgroundScale * maxBackgroundScale;

        bgTransform.localScale = originalBackgroundScale;

        Sequence scaleSequence = DOTween.Sequence();

        scaleSequence.Append(
            bgTransform.DOScale(targetScale, countingDuration * 0.6f)
                .SetEase(Ease.OutQuad)
        );

        scaleSequence.AppendInterval(countingDuration * 0.1f);

        scaleSequence.Append(
            bgTransform.DOScale(originalBackgroundScale, countingDuration * 0.3f)
                .SetEase(Ease.InQuad)
        );

        scaleTween = scaleSequence;
    }

    private void PlayPopAnimation()
    {
        if (betAmountBackground == null) return;

        popSequence?.Kill();

        Transform bgTransform = betAmountBackground.transform;
        Vector3 targetOriginalScale = hasStoredOriginalScale ? originalBackgroundScale : Vector3.one;

        bgTransform.localScale = targetOriginalScale;

        popSequence = DOTween.Sequence();

        popSequence.Append(
            bgTransform.DOScale(targetOriginalScale * popInScale, popInDuration)
                .SetEase(popEase)
        );

        popSequence.Append(
            bgTransform.DOScale(targetOriginalScale, popInDuration * 0.7f)
                .SetEase(Ease.InBack)
        );
    }
    #endregion

    #region Private Methods - Display
    private void UpdateTotalDisplay()
    {
        // Don't update display if we're animating a win
        if (isAnimatingWin)
        {
            if (showDebugLogs)
            {
                Debug.Log("[PlayerBetComponent] UpdateTotalDisplay skipped - win animation in progress");
            }
            return;
        }

        if (totalBetAmountText != null)
        {
            if (bets.Count > 0)
            {
                totalBetAmountText.text = GameUtilities.FormatCurrency(totalBetAmount);
                totalBetAmountText.gameObject.SetActive(true);
            }
            else
            {
                totalBetAmountText.gameObject.SetActive(false);
            }
        }
    }
    #endregion

    #region Public API - Manual Animation Trigger
    public void TriggerPopAnimation()
    {
        PlayPopAnimation();
    }
    #endregion
}