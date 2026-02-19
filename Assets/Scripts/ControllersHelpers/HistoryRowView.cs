using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    internal void SetData(HistoryEntry entry, int rowNumber)
    {
        if (entry == null) return;

        if (Index_Text) Index_Text.text = rowNumber.ToString();
        if (RoundId_Text) RoundId_Text.text = entry.round_id;
        if (DateTime_Text) DateTime_Text.text = entry.GetFormattedDateTime();

        if (BetAmount_Text) BetAmount_Text.text = GameUtilities.FormatCurrency(entry.bet_amount);

        if (WinAmount_Text)
        {
            WinAmount_Text.text = GameUtilities.FormatCurrency(entry.win_amount);
            WinAmount_Text.color = entry.win_amount > 0 ? Color.green : Color.white;
        }

        if (ProfitLoss_Text)
        {
            double pl = entry.GetProfitLoss();
            ProfitLoss_Text.text = pl >= 0
                ? $"+{GameUtilities.FormatCurrency(pl)}"
                : GameUtilities.FormatCurrency(pl);
            ProfitLoss_Text.color = pl > 0 ? Color.green : pl < 0 ? Color.red : Color.white;
        }

        if (DiceSprites != null && DiceSprites.Length >= 6)
        {
            SetDiceSprite(Dice1_Image, entry.dice_1);
            SetDiceSprite(Dice2_Image, entry.dice_2);
            SetDiceSprite(Dice3_Image, entry.dice_3);
        }
    }

    private void SetDiceSprite(Image img, int value)
    {
        if (img != null && value >= 1 && value <= 6)
            img.sprite = DiceSprites[value - 1];
    }
}