using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a single history entry row
/// </summary>
[System.Serializable]
public class HistoryRowView : MonoBehaviour
{
    #region Serialized Fields
    [Header("Row Elements")]
    public TMP_Text Index_Text;
    public TMP_Text RoundId_Text;
    public TMP_Text DateTime_Text;
    public TMP_Text BetAmount_Text;
    public TMP_Text WinAmount_Text;
    public TMP_Text ProfitLoss_Text;
    public Image Dice1_Image;
    public Image Dice2_Image;
    public Image Dice3_Image;

    [Header("Dice Sprites")]
    public Sprite[] DiceSprites;
    #endregion

    #region Public API
    public void SetData(HistoryEntry entry, int rowNumber)
    {
        if (entry == null) return;

        if (Index_Text) Index_Text.text = rowNumber.ToString();

        if (RoundId_Text)
        {
            RoundId_Text.text = entry.round_id;
        }

        if (DateTime_Text)
        {
            DateTime_Text.text = entry.GetFormattedDateTime();
        }

        if (BetAmount_Text)
        {
            BetAmount_Text.text = GameUtilities.FormatCurrency(entry.bet_amount);
        }

        if (WinAmount_Text)
        {
            WinAmount_Text.text = GameUtilities.FormatCurrency(entry.win_amount);

            WinAmount_Text.color = entry.win_amount > 0 ? Color.green : Color.white;
        }

        if (ProfitLoss_Text)
        {
            double profitLoss = entry.GetProfitLoss();

            string plText = profitLoss >= 0 ?
                $"+{GameUtilities.FormatCurrency(profitLoss)}" :
                GameUtilities.FormatCurrency(profitLoss);

            ProfitLoss_Text.text = plText;

            if (profitLoss > 0)
                ProfitLoss_Text.color = Color.green;
            else if (profitLoss < 0)
                ProfitLoss_Text.color = Color.red;
            else
                ProfitLoss_Text.color = Color.white;
        }

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
    #endregion
}
