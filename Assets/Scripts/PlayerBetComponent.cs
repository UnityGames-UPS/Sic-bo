using UnityEngine;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// Manages bet display for a single betting area
/// </summary>
public class PlayerBetComponent : MonoBehaviour
{
    #region Serialized Fields
    [Header("Total Bet Display")]
    [SerializeField] private TMP_Text totalBetAmountText;

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
    #endregion

    #region Unity Lifecycle
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
            Debug.Log($"[PlayerBetComponent] Initialized with {chipSprites.Length} sprites, {allChips.Count} initial chips");
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

        UpdateTotalDisplay();

        gameObject.SetActive(false);
    }

    public double GetTotalBet() => totalBetAmount;

    public int GetBetCount() => bets.Count;

    public bool HasBets() => bets.Count > 0;

    public List<BetData> GetBetData() => new List<BetData>(bets);
    #endregion

    #region Private Methods - Chip Management
    private void AddSingleChip(double amount, int chipIndex)
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

        UpdateTotalDisplay();

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
    #endregion

    #region Private Methods - Display
    private void UpdateTotalDisplay()
    {
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
}
