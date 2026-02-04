using TMPro;
using UnityEngine;
using UnityEngine.UI;
[System.Serializable]
public class HistoryRowView : MonoBehaviour
{
    [Header("Row Elements")]
    public TMP_Text Index_Text;
    public TMP_Text RoundId_Text;
    public TMP_Text BetAmount_Text;
    public TMP_Text WinAmount_Text;
    public Image Dice1_Image;
    public Image Dice2_Image;
    public Image Dice3_Image;
    public TMP_Text MatchSide_Text;

    [Header("Dice Sprites")]
    public Sprite[] DiceSprites; // 0-5 for faces 1-6

    public void SetData(HistoryEntry entry, int index)
    {
        if (entry == null) return;

        if (Index_Text) Index_Text.text = index.ToString();
        if (RoundId_Text) RoundId_Text.text = entry.round_id;
        if (BetAmount_Text) BetAmount_Text.text = entry.bet_amount.ToString("F2");
        if (WinAmount_Text)
        {
            WinAmount_Text.text = entry.win_amount.ToString("F2");
            WinAmount_Text.color = entry.win_amount > 0 ? Color.green : Color.white;
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

        if (MatchSide_Text) MatchSide_Text.text = entry.match_side.ToUpper();
    }
}

