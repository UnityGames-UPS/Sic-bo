using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;

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
    #endregion

    #region Private Fields
    private Sprite[] chipSprites;
    private List<Chip> allChips = new List<Chip>();
    private List<Vector3> initialChipFinalPositions = new List<Vector3>();
    private List<BetData> bets = new List<BetData>();
    private double totalBetAmount = 0;
    private List<double> availableChipValues = new List<double>();
    private Vector3 originalBackgroundScale;
    private Tween countingTween;
    private Tween scaleTween;
    private Sequence popSequence;
    private bool hasStoredOriginalScale = false;
    private bool isAnimatingWin = false;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (betAmountBackground != null)
        {
            originalBackgroundScale = betAmountBackground.transform.localScale;
            hasStoredOriginalScale = true;
        }
    }

    private void OnEnable()
    {
        if (betAmountBackground != null)
            StartCoroutine(PlayPopAfterFrame());
    }

    private IEnumerator PlayPopAfterFrame()
    {
        yield return null;
        PlayPopAnimation();
    }

    private void OnDestroy()
    {
        countingTween?.Kill();
        scaleTween?.Kill();
        popSequence?.Kill();
        foreach (var chip in allChips)
            chip?.transform.DOKill();
    }
    #endregion

    #region Internal API - Initialization
    internal void Initialize(Sprite[] sprites, List<double> chipValues = null)
    {
        chipSprites = sprites;

        if (chipValues != null)
            availableChipValues = new List<double>(chipValues);

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
    }

    internal void UpdateChipValues(List<double> chipValues)
    {
        if (chipValues != null)
            availableChipValues = new List<double>(chipValues);
    }
    #endregion

    #region Internal API - Betting
    internal void AddBetFromServer(double serverAmount)
    {
        if (serverAmount <= 0) return;

        var combination = GameUtilities.FindChipCombination(serverAmount, availableChipValues);
        if (combination.Count == 0) return;

        foreach (var item in combination)
            AddSingleChip(item.amount, item.chipIndex);
    }

    internal void AddBet(double amount, int chipIndex) => AddSingleChip(amount, chipIndex);

    internal void AnimateWinWithRatio(double winRatio)
    {
        if (totalBetAmountText == null || totalBetAmount <= 0 || winRatio <= 0) return;

        PlayCountingAnimation(totalBetAmount, totalBetAmount * winRatio);
        PlayBackgroundScaleAnimation();
    }

    internal void RemoveLastBet()
    {
        if (bets.Count == 0) return;

        int lastIndex = bets.Count - 1;
        totalBetAmount -= bets[lastIndex].amount;
        bets.RemoveAt(lastIndex);

        if (lastIndex < allChips.Count && allChips[lastIndex] != null)
        {
            allChips[lastIndex].transform.DOKill();
            allChips[lastIndex].SetActive(false);
        }

        UpdateTotalDisplay();
        if (bets.Count == 0) gameObject.SetActive(false);
    }

    internal void Clear()
    {
        countingTween?.Kill();
        scaleTween?.Kill();
        popSequence?.Kill();

        isAnimatingWin = false;
        bets.Clear();
        totalBetAmount = 0;

        foreach (var chip in allChips) chip?.transform.DOKill();

        for (int i = allChips.Count - 1; i >= initialChips.Count; i--)
        {
            if (allChips[i] != null) Destroy(allChips[i].gameObject);
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
            betAmountBackground.transform.localScale = originalBackgroundScale;

        UpdateTotalDisplay();
        gameObject.SetActive(false);
    }

    internal double GetTotalBet() => totalBetAmount;
    internal int GetBetCount() => bets.Count;
    internal bool HasBets() => bets.Count > 0;
    internal List<BetData> GetBetData() => new List<BetData>(bets);

    internal void TriggerPopAnimation() => PlayPopAnimation();
    #endregion

    #region Chip Management
    private void AddSingleChip(double amount, int chipIndex, bool skipDisplay = false)
    {
        if (chipIndex < 0 || chipIndex >= chipSprites.Length) return;

        bets.Add(new BetData { amount = amount, chipIndex = chipIndex });
        totalBetAmount += amount;

        int chipSlot = bets.Count - 1;
        Chip chip = GetOrSpawnChip(chipSlot);

        if (chip != null)
        {
            Vector3 finalPosition = chipSlot < initialChips.Count
                ? initialChipFinalPositions[chipSlot]
                : CalculateSpawnedChipPosition();

            chip.SetSprite(chipSprites[chipIndex]);
            chip.SetAmount(GameUtilities.FormatCurrency(amount));
            chip.SetActive(true);
            chip.transform.localPosition = new Vector3(finalPosition.x, dropStartY, finalPosition.z);
            AnimateChipDrop(chip, finalPosition);
        }

        if (!skipDisplay) UpdateTotalDisplay();
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    private Chip GetOrSpawnChip(int index)
    {
        if (index < allChips.Count && allChips[index] != null) return allChips[index];
        if (chipPrefab == null) return null;

        GameObject chipObj = Instantiate(chipPrefab, transform);
        Chip newChip = chipObj.GetComponent<Chip>();

        if (newChip == null) { Destroy(chipObj); return null; }

        allChips.Add(newChip);
        return newChip;
    }

    private Vector3 CalculateSpawnedChipPosition()
    {
        float offsetX = Random.Range(-randomOffsetRange.x, randomOffsetRange.x);
        float offsetY = Random.Range(-randomOffsetRange.y, randomOffsetRange.y);

        if (initialChipFinalPositions.Count == 0) return new Vector3(offsetX, offsetY, 0);

        Vector3 basePosition = initialChipFinalPositions[Random.Range(0, initialChipFinalPositions.Count)];
        return basePosition + new Vector3(offsetX, offsetY, 0);
    }
    #endregion

    #region Animations
    private void AnimateChipDrop(Chip chip, Vector3 targetPosition)
    {
        if (chip == null) return;
        chip.transform.DOKill();

        DOTween.Sequence()
            .Append(chip.transform.DOLocalMove(targetPosition, dropDuration).SetEase(Ease.OutBounce))
            .Join(chip.transform.DOScale(popScale, popDuration).SetEase(Ease.OutBack))
            .Append(chip.transform.DOScale(1f, popDuration).SetEase(Ease.InBack))
            .Play();
    }

    private void PlayCountingAnimation(double fromAmount, double toAmount)
    {
        if (totalBetAmountText == null) return;

        countingTween?.Kill();
        isAnimatingWin = true;

        totalBetAmountText.text = GameUtilities.FormatCurrency(fromAmount);

        if (Mathf.Approximately((float)fromAmount, (float)toAmount))
        {
            totalBetAmountText.text = GameUtilities.FormatCurrency(toAmount);
            PlayBackgroundScaleAnimation(); // still give visual feedback for 1:1 bets (small/big/single)
            isAnimatingWin = false;
            return;
        }

        countingTween = DOVirtual.Float(
            (float)fromAmount, (float)toAmount, countingDuration,
            value => { if (totalBetAmountText != null) totalBetAmountText.text = GameUtilities.FormatCurrency(value); })
            .SetEase(countingEase)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (totalBetAmountText != null)
                    totalBetAmountText.text = GameUtilities.FormatCurrency(toAmount);
                isAnimatingWin = false;
            })
            .SetAutoKill(true)
            .Play();
    }

    private void PlayBackgroundScaleAnimation()
    {
        if (betAmountBackground == null || !hasStoredOriginalScale) return;

        scaleTween?.Kill();

        Transform bgTransform = betAmountBackground.transform;
        Vector3 targetScale = originalBackgroundScale * maxBackgroundScale;
        bgTransform.localScale = originalBackgroundScale;

        scaleTween = DOTween.Sequence()
            .Append(bgTransform.DOScale(targetScale, countingDuration * 0.6f).SetEase(Ease.OutQuad))
            .AppendInterval(countingDuration * 0.1f)
            .Append(bgTransform.DOScale(originalBackgroundScale, countingDuration * 0.3f).SetEase(Ease.InQuad));
    }

    private void PlayPopAnimation()
    {
        if (betAmountBackground == null) return;

        popSequence?.Kill();

        Transform bgTransform = betAmountBackground.transform;
        Vector3 targetScale = hasStoredOriginalScale ? originalBackgroundScale : Vector3.one;
        bgTransform.localScale = targetScale;

        popSequence = DOTween.Sequence()
            .Append(bgTransform.DOScale(targetScale * popInScale, popInDuration).SetEase(popEase))
            .Append(bgTransform.DOScale(targetScale, popInDuration * 0.7f).SetEase(Ease.InBack));
    }
    #endregion

    #region Display
    private void UpdateTotalDisplay()
    {
        if (isAnimatingWin || totalBetAmountText == null) return;

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
    #endregion
}