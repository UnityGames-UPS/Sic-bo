using UnityEngine;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// CORRECTED: Proper chip animation logic
/// - First 6 chips: Drop from Y=200 to their ORIGINAL position (scene position)
/// - Additional chips (7+): Drop from Y=200 to random offset FROM the 6 original positions
/// - Chips only spawn after server confirmation
/// </summary>
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

    // Chip sprites reference (set during Initialize)
    private Sprite[] chipSprites;

    // ALL chips (initial + spawned)
    private List<Chip> allChips = new List<Chip>();

    // FINAL positions for the 6 initial chips (their original scene positions)
    private List<Vector3> initialChipFinalPositions = new List<Vector3>();

    // Track individual bets for this area
    private List<BetData> bets = new List<BetData>();
    private double totalBetAmount = 0;

    // Chip values available for combinations
    private List<double> availableChipValues = new List<double>();

    #region Public API

    /// <summary>
    /// Initialize with chip sprites and available chip values
    /// Called once during game setup
    /// </summary>
    public void Initialize(Sprite[] sprites, List<double> chipValues = null)
    {
        chipSprites = sprites;

        if (chipValues != null)
        {
            availableChipValues = new List<double>(chipValues);
        }

        // Add initial chips to allChips list and store their FINAL positions
        allChips.Clear();
        initialChipFinalPositions.Clear();

        foreach (var chip in initialChips)
        {
            if (chip != null)
            {
                allChips.Add(chip);
                // Store the ORIGINAL scene position as the FINAL position
                initialChipFinalPositions.Add(chip.transform.localPosition);
            }
        }

        // Reset all chips to disabled state
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

    /// <summary>
    /// Update chip values (called when changing rooms/levels)
    /// </summary>
    public void UpdateChipValues(List<double> chipValues)
    {
        if (chipValues != null)
        {
            availableChipValues = new List<double>(chipValues);
        }
    }

    /// <summary>
    /// Add bet based on SERVER RESPONSE amount
    /// Automatically calculates best chip combination and spawns chips as needed
    /// </summary>
    /// <param name="serverAmount">Actual amount placed by server (may differ from client request)</param>
    public void AddBetFromServer(double serverAmount)
    {
        if (serverAmount <= 0) return;

        // Find best chip combination for this amount
        List<ChipCombinationItem> combination = FindChipCombination(serverAmount);

        if (combination.Count == 0)
        {
            Debug.LogWarning($"[PlayerBetComponent] Could not find chip combination for amount: {serverAmount}");
            return;
        }

        // Add each chip in the combination
        foreach (var item in combination)
        {
            AddSingleChip(item.amount, item.chipIndex);
        }

        if (showDebugLogs)
        {
            Debug.Log($"[PlayerBetComponent] Added bet: {serverAmount} using {combination.Count} chips");
        }
    }

    /// <summary>
    /// LEGACY: Add a single bet chip (kept for backward compatibility)
    /// Use AddBetFromServer for server-based betting
    /// </summary>
    public void AddBet(double amount, int chipIndex)
    {
        AddSingleChip(amount, chipIndex);
    }

    /// <summary>
    /// Remove the last bet from this area
    /// Used for UNDO functionality
    /// </summary>
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

        // Update total
        totalBetAmount -= lastBet.amount;

        // Remove bet record
        bets.RemoveAt(lastIndex);

        // Disable the chip (could be initial or spawned)
        if (lastIndex < allChips.Count && allChips[lastIndex] != null)
        {
            Chip chip = allChips[lastIndex];

            // Kill any animations
            chip.transform.DOKill();

            chip.SetActive(false);
        }

        // Update total display
        UpdateTotalDisplay();

        // Hide parent if no bets left
        if (bets.Count == 0)
        {
            gameObject.SetActive(false);
        }

        if (showDebugLogs)
        {
            Debug.Log($"[PlayerBetComponent] Removed last bet: {lastBet.amount}, Remaining: {bets.Count}");
        }
    }

    /// <summary>
    /// Clear all bets and reset
    /// Used for CANCEL ALL and round end
    /// Destroys spawned chips, keeps initial chips ready for reuse
    /// </summary>
    public void Clear()
    {
        bets.Clear();
        totalBetAmount = 0;

        // Kill all animations
        foreach (var chip in allChips)
        {
            if (chip != null)
            {
                chip.transform.DOKill();
            }
        }

        // Disable and destroy spawned chips (beyond initial 6)
        for (int i = allChips.Count - 1; i >= initialChips.Count; i--)
        {
            if (allChips[i] != null)
            {
                Destroy(allChips[i].gameObject);
            }
            allChips.RemoveAt(i);
        }

        // Disable initial chips (keep for reuse)
        foreach (var chip in initialChips)
        {
            if (chip != null)
            {
                chip.transform.localScale = Vector3.one;
                chip.SetActive(false);
            }
        }

        // Update total display
        UpdateTotalDisplay();

        // Hide parent object
        gameObject.SetActive(false);

        if (showDebugLogs)
        {
            Debug.Log("[PlayerBetComponent] Cleared all bets");
        }
    }

    /// <summary>
    /// Get total bet amount for this area
    /// </summary>
    public double GetTotalBet()
    {
        return totalBetAmount;
    }

    /// <summary>
    /// Get number of bets in this area
    /// </summary>
    public int GetBetCount()
    {
        return bets.Count;
    }

    /// <summary>
    /// Check if this area has any bets
    /// </summary>
    public bool HasBets()
    {
        return bets.Count > 0;
    }

    /// <summary>
    /// Get all bet data for this area (for repeat/double functionality)
    /// </summary>
    public List<BetData> GetBetData()
    {
        return new List<BetData>(bets); // Return copy
    }

    #endregion

    #region Private Methods - Chip Management

    /// <summary>
    /// CORRECTED: Add a single chip to the area with proper animation
    /// - First 6 chips: Drop from Y=200 to their ORIGINAL position
    /// - Chips 7+: Drop from Y=200 to random offset from the 6 original positions
    /// </summary>
    private void AddSingleChip(double amount, int chipIndex)
    {
        // Validate chip index
        if (chipIndex < 0 || chipIndex >= chipSprites.Length)
        {
            Debug.LogWarning($"[PlayerBetComponent] Invalid chipIndex: {chipIndex}");
            return;
        }

        // Record bet
        bets.Add(new BetData { amount = amount, chipIndex = chipIndex });
        totalBetAmount += amount;

        int chipSlot = bets.Count - 1; // Index of this chip
        Chip chip = GetOrSpawnChip(chipSlot);

        if (chip != null)
        {
            // Calculate final position
            Vector3 finalPosition;

            if (chipSlot < initialChips.Count)
            {
                // First 6 chips: Use their ORIGINAL position
                finalPosition = initialChipFinalPositions[chipSlot];
            }
            else
            {
                // Spawned chips (7+): Random offset from one of the 6 original positions
                finalPosition = CalculateSpawnedChipPosition();
            }

            // Set chip data
            chip.SetSprite(chipSprites[chipIndex]);
            chip.SetAmount(FormatAmount(amount));
            chip.SetActive(true);

            // ALL chips drop from Y=200
            Vector3 startPosition = new Vector3(finalPosition.x, dropStartY, finalPosition.z);
            chip.transform.localPosition = startPosition;

            // Animate drop to final position
            AnimateChipDrop(chip, finalPosition);

            if (showDebugLogs)
            {
                Debug.Log($"[PlayerBetComponent] Added chip {chipSlot}: {amount} from {startPosition} to {finalPosition}");
            }
        }

        // Update total display
        UpdateTotalDisplay();

        // Show the parent object if hidden
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Get existing chip or spawn new one if needed
    /// </summary>
    private Chip GetOrSpawnChip(int index)
    {
        // Use existing chip if available
        if (index < allChips.Count && allChips[index] != null)
        {
            return allChips[index];
        }

        // Need to spawn new chip
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

    /// <summary>
    /// CORRECTED: Calculate position for spawned chips (7+)
    /// Returns a random offset from one of the 6 original chip positions
    /// </summary>
    private Vector3 CalculateSpawnedChipPosition()
    {  // Add random offset
        float offsetX = Random.Range(-randomOffsetRange.x, randomOffsetRange.x);
        float offsetY = Random.Range(-randomOffsetRange.y, randomOffsetRange.y);

        if (initialChipFinalPositions.Count == 0)
        {
            return new Vector3(offsetX, offsetY, 0);
        }

        // Pick a random position from the 6 original chip positions
        int randomIndex = Random.Range(0, initialChipFinalPositions.Count);
        Vector3 basePosition = initialChipFinalPositions[randomIndex];

      

        return basePosition + new Vector3(offsetX, offsetY, 0);
    }

    /// <summary>
    /// Animate chip dropping from above with bounce/pop effect
    /// Used for ALL chips (initial and spawned)
    /// </summary>
    private void AnimateChipDrop(Chip chip, Vector3 targetPosition)
    {
        if (chip == null) return;

        // Kill any existing animations
        chip.transform.DOKill();

        // Drop animation sequence
        Sequence dropSequence = DOTween.Sequence();

        // 1. Drop down with bounce
        dropSequence.Append(
            chip.transform.DOLocalMove(targetPosition, dropDuration)
                .SetEase(Ease.OutBounce)
        );

        // 2. Pop scale
        dropSequence.Join(
            chip.transform.DOScale(popScale, popDuration)
                .SetEase(Ease.OutBack)
        );

        // 3. Return to normal scale
        dropSequence.Append(
            chip.transform.DOScale(1f, popDuration)
                .SetEase(Ease.InBack)
        );

        dropSequence.Play();
    }

    #endregion

    #region Private Methods - Chip Combination Algorithm

    /// <summary>
    /// Find optimal combination of chips to represent the target amount
    /// Uses greedy algorithm: largest chips first
    /// Returns list of chips to display
    /// </summary>
    private List<ChipCombinationItem> FindChipCombination(double targetAmount)
    {
        List<ChipCombinationItem> result = new List<ChipCombinationItem>();

        if (availableChipValues.Count == 0)
        {
            Debug.LogWarning("[PlayerBetComponent] No chip values available for combination");
            return result;
        }

        // Sort chip values in descending order (largest first)
        List<double> sortedValues = new List<double>(availableChipValues);
        sortedValues.Sort((a, b) => b.CompareTo(a));

        double remaining = targetAmount;
        const double tolerance = 0.01; // For floating point comparison

        // Greedy algorithm: use largest chips first
        while (remaining > tolerance)
        {
            bool foundChip = false;

            for (int i = 0; i < sortedValues.Count; i++)
            {
                if (sortedValues[i] <= remaining + tolerance)
                {
                    // Find chip index in original array
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

            // Safety: prevent infinite loop
            if (!foundChip)
            {
                Debug.LogWarning($"[PlayerBetComponent] Cannot find chip combination for remaining: {remaining}");
                break;
            }

            // Safety: max 20 chips
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
        // Format large numbers with K suffix
        if (amount >= 1000)
        {
            return $"{(amount / 1000):F1}K";
        }

        // Format decimals
        if (amount < 1)
        {
            return amount.ToString("F2");
        }

        // Show one decimal if not whole number
        if (amount % 1 != 0)
        {
            return amount.ToString("F1");
        }

        // Whole numbers
        return amount.ToString("F0");
    }

    #endregion

    #region Validation
    private void OnValidate()
    {
        // Auto-find initial chips in children if not assigned
        if (initialChips.Count == 0)
        {
            initialChips.AddRange(GetComponentsInChildren<Chip>(true));

            // Keep only first 6
            if (initialChips.Count > 6)
            {
                initialChips.RemoveRange(6, initialChips.Count - 6);
            }
        }
    }

    private void OnDestroy()
    {
        // Kill all animations on destroy
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

/// <summary>
/// Record of individual bet in an area
/// Stores amount and which chip sprite to show
/// </summary>
[System.Serializable]
public class BetData
{
    public double amount;
    public int chipIndex; // Index in chip sprites array
}

/// <summary>
/// Helper class for chip combination calculation
/// </summary>
[System.Serializable]
public class ChipCombinationItem
{
    public double amount;
    public int chipIndex;
}