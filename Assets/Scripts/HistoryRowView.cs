using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a single history entry row in the history table
/// Shows: Number, Round ID, Date/Time, Stake, Win, P/L, and Result (3 dice images)
/// </summary>
[System.Serializable]
public class HistoryRowView : MonoBehaviour
{
    [Header("Row Elements")]
    public TMP_Text Index_Text;          // Row number (1, 2, 3, etc.)
    public TMP_Text RoundId_Text;        // Round ID
    public TMP_Text DateTime_Text;       // Date and time of bet
    public TMP_Text BetAmount_Text;      // Total stake amount
    public TMP_Text WinAmount_Text;      // Win amount
    public TMP_Text ProfitLoss_Text;     // P/L (Profit/Loss)
    public Image Dice1_Image;            // First dice result
    public Image Dice2_Image;            // Second dice result
    public Image Dice3_Image;            // Third dice result
  

    [Header("Dice Sprites")]
    public Sprite[] DiceSprites; // Array of 6 sprites for dice faces 1-6 (index 0-5)

    /// <summary>
    /// Set data for this history row
    /// </summary>
    /// <param name="entry">History entry data from server</param>
    /// <param name="rowNumber">Display row number (1-based index)</param>
    public void SetData(HistoryEntry entry, int rowNumber)
    {
        if (entry == null) return;

        // Row number
        if (Index_Text) Index_Text.text = rowNumber.ToString();

        // Round ID - show last 8 characters for readability
        if (RoundId_Text)
        {
            RoundId_Text.text = entry.round_id;
        }
        // Date and Time - format from ISO 8601
        if (DateTime_Text)
        {
            DateTime_Text.text = entry.GetFormattedDateTime();
        }

        // Bet Amount (Stake)
        if (BetAmount_Text)
        {
            BetAmount_Text.text = FormatCurrency(entry.bet_amount);
        }

        // Win Amount
        if (WinAmount_Text)
        {
            WinAmount_Text.text = FormatCurrency(entry.win_amount);

            // Color green if won, white if lost
            WinAmount_Text.color = entry.win_amount > 0 ? Color.green : Color.white;
        }

        // Profit/Loss calculation
        if (ProfitLoss_Text)
        {
            double profitLoss = entry.GetProfitLoss();

            // Format with + or - sign
            string plText = profitLoss >= 0 ?
                $"+{FormatCurrency(profitLoss)}" :
                FormatCurrency(profitLoss);

            ProfitLoss_Text.text = plText;

            // Color: Green for profit, Red for loss, White for break-even
            if (profitLoss > 0)
                ProfitLoss_Text.color = Color.green;
            else if (profitLoss < 0)
                ProfitLoss_Text.color = Color.red;
            else
                ProfitLoss_Text.color = Color.white;
        }

        // Set dice images
        if (DiceSprites != null && DiceSprites.Length >= 6)
        {
            if (Dice1_Image && entry.dice_1 >= 1 && entry.dice_1 <= 6)
                Dice1_Image.sprite = DiceSprites[entry.dice_1 - 1];

            if (Dice2_Image && entry.dice_2 >= 1 && entry.dice_2 <= 6)
                Dice2_Image.sprite = DiceSprites[entry.dice_2 - 1];

            if (Dice3_Image && entry.dice_3 >= 1 && entry.dice_3 <= 6)
                Dice3_Image.sprite = DiceSprites[entry.dice_3 - 1];
        }

    }

    /// <summary>
    /// Format currency value with proper decimal places
    /// Shows 2 decimal places for values under 1000
    /// Shows K suffix for thousands
    /// </summary>
    private string FormatCurrency(double amount)
    {
        if (amount >= 1000)
        {
            return $"{(amount / 1000):F1}K";
        }

        return amount.ToString("F2");
    }
}