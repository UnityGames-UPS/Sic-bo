using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Pure display component — holds UI references and exposes data setters.
/// All name/balance toggle animation is driven by LeaderboardController.
/// </summary>
public class LeaderboardPlayerBlock : MonoBehaviour
{
    [Header("UI Elements")]
    public Image AvatarImage;
    public TMP_Text NameText;
    public TMP_Text BalanceText;
    public GameObject Container;
    public Image PositionImage;

    // ── Public API ───────────────────────────────────────────────────────────

    internal void SetPlayerData(string username, double balance, Sprite avatar)
    {
        if (NameText != null) NameText.text = MaskUsername(username);
        if (BalanceText != null) BalanceText.text = GameUtilities.FormatBalance(balance);
        if (AvatarImage != null && avatar != null) AvatarImage.sprite = avatar;
        if (Container != null) Container.SetActive(true);

        // Start with name visible, balance hidden.
        // Do NOT call ShowName() here — that would double-show name at the start
        // of AlternateNameBalance which also begins by showing name.
        // Instead we set state directly so the controller loop begins on "show balance" first.
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

    internal void UpdateBalance(double balance)
    {
        if (BalanceText != null) BalanceText.text = GameUtilities.FormatBalance(balance);
    }

    /// <summary>Instantly show name, hide balance — no tween.</summary>
    internal void ShowName()
    {
        SetNameVisible(true);
        SetBalanceVisible(false);
    }

    /// <summary>Instantly show balance, hide name — no tween.</summary>
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