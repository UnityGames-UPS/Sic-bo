using UnityEngine;
using TMPro;
using System.Collections.Generic;


public class PlayerBetComponent : MonoBehaviour
{
    [Header("Total Bet Display")]
    [SerializeField] private TMP_Text totalBetAmountText;

    [Header("Chip Pool - 6 chips max per area")]
    [SerializeField] private List<Chip> chips = new List<Chip>(6);

    [Header("Debug Info")]
    [SerializeField] private bool showDebugLogs = false;

    // Chip sprites reference (set during Initialize)
    private Sprite[] chipSprites;

    // Track individual bets for this area
    private List<BetData> bets = new List<BetData>(6);
    private double totalBetAmount = 0;

    // Area identification
    private string areaId = "";

    #region Public API

    /// <summary>
    /// Initialize with chip sprites from BetController
    /// Called once during game setup
    /// </summary>
    public void Initialize(Sprite[] sprites)
    {
        chipSprites = sprites;

        // Validate chip pool
        if (chips.Count != 6)
        {
            Debug.LogError($"[PLAYER BET] Expected 6 chips, found {chips.Count}!");
        }

        // Reset all chips to disabled state
        Clear();

        if (showDebugLogs)
        {
            Debug.Log($"[PLAYER BET] Initialized with {chips.Count} chips");
        }
    }

    /// <summary>
    /// Add a new bet to this area
    /// Enables next available chip with correct sprite and amount
    /// </summary>
    /// <param name="amount">Bet amount</param>
    /// <param name="chipIndex">Index in chip sprites array (determines which sprite to show)</param>
    public void AddBet(double amount, int chipIndex)
    {
        if (bets.Count >= 6)
        {
            Debug.LogWarning($"[PLAYER BET] {areaId} - Max 6 bets reached!");
            return;
        }

        // Validate chip index
        if (chipIndex < 0 || chipIndex >= chipSprites.Length)
        {
            Debug.LogError($"[PLAYER BET] {areaId} - Invalid chip index: {chipIndex}");
            return;
        }

        // Record bet
        bets.Add(new BetData { amount = amount, chipIndex = chipIndex });
        totalBetAmount += amount;

        // Enable and configure the next chip
        int chipSlot = bets.Count - 1;
        if (chipSlot < chips.Count && chips[chipSlot] != null)
        {
            Chip chip = chips[chipSlot];

            // Use Chip script to set sprite and amount
            chip.SetSprite(chipSprites[chipIndex]);
            chip.SetAmount(FormatAmount(amount));
            chip.SetActive(true);

            if (showDebugLogs)
            {
                Debug.Log($"[PLAYER BET] {areaId} - Chip {chipSlot}: ${amount} (index {chipIndex})");
            }
        }

        // Update total display
        UpdateTotalDisplay();

        // Show the parent object if hidden
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (showDebugLogs)
        {
            Debug.Log($"[PLAYER BET] {areaId} - Total bets: {bets.Count}, Amount: ${totalBetAmount}");
        }
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
                Debug.LogWarning($"[PLAYER BET] {areaId} - No bets to remove!");
            }
            return;
        }

        int lastIndex = bets.Count - 1;
        BetData lastBet = bets[lastIndex];

        // Update total
        totalBetAmount -= lastBet.amount;

        // Remove bet record
        bets.RemoveAt(lastIndex);

        // Disable the chip
        if (lastIndex < chips.Count && chips[lastIndex] != null)
        {
            chips[lastIndex].SetActive(false);
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
            Debug.Log($"[PLAYER BET] {areaId} - Removed bet. Remaining: {bets.Count}, Total: ${totalBetAmount}");
        }
    }

    /// <summary>
    /// Clear all bets and reset to pool
    /// Used for CANCEL ALL and round end
    /// Keeps component ready for reuse
    /// </summary>
    public void Clear()
    {
        bets.Clear();
        totalBetAmount = 0;

        // Disable all chips
        foreach (var chip in chips)
        {
            if (chip != null)
            {
                chip.SetActive(false);
            }
        }

        // Update total display
        UpdateTotalDisplay();

        // Hide parent object
        gameObject.SetActive(false);

        if (showDebugLogs)
        {
            Debug.Log($"[PLAYER BET] {areaId} - Reset complete");
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

    #region Private Methods

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
        // Auto-find chips in children if not assigned
        if (chips.Count == 0)
        {
            chips.AddRange(GetComponentsInChildren<Chip>(true));

            if (chips.Count > 6)
            {
                Debug.LogWarning($"[PLAYER BET] Found {chips.Count} chips, trimming to 6");
                chips.RemoveRange(6, chips.Count - 6);
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