using UnityEngine;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;
public class PlayerBetComponent : MonoBehaviour
{
    [Header("Total Bet Display")]
    [SerializeField] private TMP_Text totalBetAmountText;

    [Header("Chip Pool - Initial Chips")]
    [SerializeField] private List<Chip> initialChips = new List<Chip>(6);

    [Header("Chip Spawning")]
    [SerializeField] private GameObject chipPrefab; // Prefab for spawning additional chips

    [Header("Animation Settings")]
    [SerializeField] private float dropStartY = 200f; // How far above to start drop
    [SerializeField] private float dropDuration = 0.4f;
    [SerializeField] private float popScale = 1.15f;
    [SerializeField] private float popDuration = 0.15f;
    [SerializeField] private Vector2 randomOffsetRange = new Vector2(10f, 13f); // Min/Max offset for spawned chips

    [Header("Debug Info")]
    [SerializeField] private bool showDebugLogs = false;

    private Sprite[] chipSprites;
    private List<Chip> allChips = new List<Chip>();
    private List<Vector3> initialChipFinalPositions = new List<Vector3>();
    private List<BetData> bets = new List<BetData>();
    private double totalBetAmount = 0;
    private List<double> availableChipValues = new List<double>();

    #region Public API
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
            for (int i = 0; i < initialChipFinalPositions.Count; i++)
            {
                Debug.Log($"[PlayerBetComponent] Chip {i} final position: {initialChipFinalPositions[i]}");
            }
        }
    }

    public void UpdateChipValues(List<double> chipValues)
    {
        if (chipValues != null)
        {
            availableChipValues = new List<double>(chipValues);
        }
    }

    public void AddBetFromServer(double serverAmount)
    {
        if (serverAmount <= 0) return;

        List<ChipCombinationItem> combination = FindChipCombination(serverAmount);

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
        if (bets.Count == 0)
        {
            if (showDebugLogs)
            {
                Debug.Log("[PlayerBetComponent] RemoveLastBet: No bets to remove");
            }
            return;
        }

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

        if (showDebugLogs)
        {
            Debug.Log($"[PlayerBetComponent] Removed last bet: {lastBet.amount}, Remaining: {bets.Count}");
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

        if (showDebugLogs)
        {
            Debug.Log("[PlayerBetComponent] Cleared all bets");
        }
    }

    public double GetTotalBet()
    {
        return totalBetAmount;
    }
    public int GetBetCount()
    {
        return bets.Count;
    }

    public bool HasBets()
    {
        return bets.Count > 0;
    }

    public List<BetData> GetBetData()
    {
        return new List<BetData>(bets); // Return copy
    }

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
            chip.SetAmount(FormatAmount(amount));
            chip.SetActive(true);

            Vector3 startPosition = new Vector3(finalPosition.x, dropStartY, finalPosition.z);
            chip.transform.localPosition = startPosition;

            AnimateChipDrop(chip, finalPosition);

            if (showDebugLogs)
            {
                Debug.Log($"[PlayerBetComponent] Added chip {chipSlot}: {amount} from {startPosition} to {finalPosition}");
            }
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
            Debug.LogError("[PlayerBetComponent] chipPrefab is null, cannot spawn additional chips!");
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

        if (showDebugLogs)
        {
            Debug.Log($"[PlayerBetComponent] Spawned new chip, total count: {allChips.Count}");
        }
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

    #region Private Methods - Chip Combination Algorithm

    private List<ChipCombinationItem> FindChipCombination(double targetAmount)
    {
        List<ChipCombinationItem> result = new List<ChipCombinationItem>();

        if (availableChipValues.Count == 0)
        {
            Debug.LogWarning("[PlayerBetComponent] No chip values available for combination");
            return result;
        }

        List<double> sortedValues = new List<double>(availableChipValues);
        sortedValues.Sort((a, b) => b.CompareTo(a));

        double remaining = targetAmount;
        const double tolerance = 0.01; 

        while (remaining > tolerance)
        {
            bool foundChip = false;

            for (int i = 0; i < sortedValues.Count; i++)
            {
                if (sortedValues[i] <= remaining + tolerance)
                {
                    int chipIndex = availableChipValues.IndexOf(sortedValues[i]);

                    result.Add(new ChipCombinationItem
                    {
                        amount = sortedValues[i],
                        chipIndex = chipIndex
                    });

                    remaining -= sortedValues[i];
                    foundChip = true;
                    break;
                }
            }

            if (!foundChip)
            {
                Debug.LogWarning($"[PlayerBetComponent] Cannot find chip combination for remaining: {remaining}");
                break;
            }

            if (result.Count >= 20)
            {
                Debug.LogWarning($"[PlayerBetComponent] Chip combination exceeded 20 chips");
                break;
            }
        }

        return result;
    }

    #endregion

    #region Private Methods - Display
    private void UpdateTotalDisplay()
    {
        if (totalBetAmountText != null)
        {
            if (bets.Count > 0)
            {
                totalBetAmountText.text = FormatAmount(totalBetAmount);
                totalBetAmountText.gameObject.SetActive(true);
            }
            else
            {
                totalBetAmountText.gameObject.SetActive(false);
            }
        }
    }

    private string FormatAmount(double amount)
    {
        if (amount >= 1000)
        {
            return $"{(amount / 1000):F1}K";
        }

        if (amount < 1)
        {
            return amount.ToString("F1");
        }

        if (amount % 1 != 0)
        {
            return amount.ToString("F1");
        }

        return amount.ToString("F0");
    }

    #endregion

    #region Validation
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
}

[System.Serializable]
public class BetData
{
    public double amount;
    public int chipIndex; 
}

[System.Serializable]
public class ChipCombinationItem
{
    public double amount;
    public int chipIndex;
}