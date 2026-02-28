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

    [Header("Crown (Separate from Badge)")]
    public GameObject CrownObject; // Assign the crown GameObject/Image here


    internal void SetPlayerData(string username, double balance, Sprite avatar)
    {
        if (NameText != null) NameText.text = MaskUsername(username);
        if (BalanceText != null) BalanceText.text = GameUtilities.FormatBalance(balance);
        if (AvatarImage != null && avatar != null) AvatarImage.sprite = avatar;
        if (Container != null) Container.SetActive(true);
        SetNameVisible(true);
        SetBalanceVisible(false);
    }

    internal void SetPositionBadge(Sprite positionSprite)
    {
        if (PositionImage != null)
        {
            PositionImage.sprite = positionSprite;
            PositionImage.gameObject.SetActive(positionSprite != null);
        }
    }

    /// <summary>
    /// Show or hide the crown. Crown should only be visible for 1st place (index 0)
    /// </summary>
    internal void SetCrownVisible(bool visible)
    {
        if (CrownObject != null)
        {
            CrownObject.SetActive(visible);
        }
    }

    /// <summary>
    /// Hide crown immediately (used during swaps)
    /// </summary>
    internal void HideCrown()
    {
        SetCrownVisible(false);
    }

    internal void UpdateBalance(double balance)
    {
        if (BalanceText != null) BalanceText.text = GameUtilities.FormatBalance(balance);
    }
    internal void ShowName()
    {
        SetNameVisible(true);
        SetBalanceVisible(false);
    }

    internal void ShowBalance()
    {
        SetNameVisible(false);
        SetBalanceVisible(true);
    }
    internal void HideAll()
    {
        if (Container != null)
        {
            Container.SetActive(false);
        }
        else
        {
            SetNameVisible(false);
            SetBalanceVisible(false);
            AvatarImage?.gameObject.SetActive(false);
        }

        // Also hide crown when hiding the block
        HideCrown();
    }
    internal CanvasGroup GetNameCanvasGroup() => NameText != null ? GetOrAddCanvasGroup(NameText.gameObject) : null;
    internal CanvasGroup GetBalanceCanvasGroup() => BalanceText != null ? GetOrAddCanvasGroup(BalanceText.gameObject) : null;
    private void SetNameVisible(bool visible)
    {
        if (NameText == null) return;
        var cg = GetOrAddCanvasGroup(NameText.gameObject);
        if (cg != null) cg.alpha = visible ? 1f : 0f;
        NameText.gameObject.SetActive(visible);
    }

    private void SetBalanceVisible(bool visible)
    {
        if (BalanceText == null) return;
        var cg = GetOrAddCanvasGroup(BalanceText.gameObject);
        if (cg != null) cg.alpha = visible ? 1f : 0f;
        BalanceText.gameObject.SetActive(visible);
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        if (go == null) return null;
        var cg = go.GetComponent<CanvasGroup>();
        return cg != null ? cg : go.AddComponent<CanvasGroup>();
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