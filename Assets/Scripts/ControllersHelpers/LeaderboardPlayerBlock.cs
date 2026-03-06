using DG.Tweening;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardPlayerBlock : MonoBehaviour
{
    [Header("UI Elements")]
    public Image AvatarImage;
    public TMP_Text NameText;
    public TMP_Text BalanceText;
    public GameObject Container;
    public Image PositionImage;

    [Header("Crown (Separate from Badge)")]
    public GameObject CrownObject;

    internal void SetPlayerData(string username, double balance, Sprite avatar)
    {
        if (NameText != null) NameText.text = MaskUsername(username);
        if (BalanceText != null) BalanceText.text = FormatMetric(balance);
        if (AvatarImage != null && avatar != null) AvatarImage.sprite = avatar;

        if (Container != null)
        {
            Container.SetActive(true);
        }

        SetNameVisible(true);
        SetBalanceVisible(false);
    }

    internal void SetPositionBadge(Sprite positionSprite)
    {
        if (PositionImage == null) return;

        PositionImage.sprite = positionSprite;
        PositionImage.gameObject.SetActive(positionSprite != null);
    }

    internal void SetCrownVisible(bool visible)
    {
        StopCrownPulse();
        if (CrownObject == null) return;

        CrownObject.SetActive(true);
        CrownObject.transform.localScale = visible ? Vector3.one : Vector3.zero;
    }

    internal void HideCrown()
    {
        StopCrownPulse();
        SetCrownVisible(false);
    }

    internal void PlayCrownPulse(float showDuration = 0.2f)
    {
        if (CrownObject == null) return;

        StopCrownPulse();
        CrownObject.SetActive(true);
        CrownObject.transform.localScale = Vector3.zero;
        CrownObject.transform.DOScale(1f, showDuration).SetEase(Ease.OutBounce);
    }

    internal void StopCrownPulse()
    {
        if (CrownObject == null) return;
        CrownObject.transform.DOKill(complete: false);
    }

    private void OnDestroy()
    {
        if (CrownObject != null)
        {
            CrownObject.transform.DOKill(complete: false);
        }
    }

    internal void UpdateBalance(double balance)
    {
        if (BalanceText != null)
        {
            BalanceText.text = FormatMetric(balance);
        }
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

    private static string FormatMetric(double value)
    {
        return GameUtilities.FormatCurrency(value);
    }

    private string MaskUsername(string username)
    {
        if (string.IsNullOrEmpty(username) || username.Length <= 4) return username;

        int firstChars = 1;
        int lastChars = 3;
        int maskedLength = username.Length - firstChars - lastChars;
        if (maskedLength <= 0) return username;

        return username.Substring(0, firstChars)
             + new string('*', 3)
             + username.Substring(username.Length - lastChars);
    }
}
