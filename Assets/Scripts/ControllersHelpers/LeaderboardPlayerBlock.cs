using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LeaderboardPlayerBlock : MonoBehaviour
{
    [Header("UI Elements")]
    public Image AvatarImage;
    public TMP_Text NameText;
    public TMP_Text BalanceText;
    public GameObject Container;
    public Image PositionImage;

    internal void SetPlayerData(string username, double balance, Sprite avatar)
    {
        if (NameText != null) NameText.text = MaskUsername(username);
        if (BalanceText != null) BalanceText.text = GameUtilities.FormatBalance(balance);
        if (AvatarImage != null && avatar != null) AvatarImage.sprite = avatar;
        if (Container != null) Container.SetActive(true);
        ShowName();
    }

    internal void SetPositionBadge(Sprite positionSprite)
    {
        if (PositionImage != null)
        {
            PositionImage.sprite = positionSprite;
            PositionImage.gameObject.SetActive(positionSprite != null);
        }
    }

    internal void UpdateBalance(double balance)
    {
        if (BalanceText != null) BalanceText.text = GameUtilities.FormatBalance(balance);
    }

    internal void ShowName()
    {
        NameText?.gameObject.SetActive(true);
        BalanceText?.gameObject.SetActive(false);
    }

    internal void ShowBalance()
    {
        NameText?.gameObject.SetActive(false);
        BalanceText?.gameObject.SetActive(true);
    }

    internal void HideAll()
    {
        if (Container != null)
        {
            Container.SetActive(false);
            return;
        }

        NameText?.gameObject.SetActive(false);
        BalanceText?.gameObject.SetActive(false);
        AvatarImage?.gameObject.SetActive(false);
    }

    private string MaskUsername(string username)
    {
        if (string.IsNullOrEmpty(username) || username.Length <= 4) return username;

        int firstChars = 3;
        int lastChars = 2;
        int maskedLength = username.Length - firstChars - lastChars;

        if (maskedLength <= 0) return username;

        return username.Substring(0, firstChars)
             + new string('*', maskedLength)
             + username.Substring(username.Length - lastChars);
    }
}