using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Chip : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] internal Image chipImage;
    [SerializeField] internal TMP_Text chipText;

    [Header("Leaderboard Badges")]
    [SerializeField] private bool chipUsedForBetting = false;
    [SerializeField] private GameObject richestBadgeObject;
    [SerializeField] private GameObject winnersBadgeObject;
    #endregion

    internal int chipIndex { get; private set; }

    #region Unity Lifecycle
    private void Awake() => HideBadgesInternal();
    #endregion

    #region Internal API
    internal void SetData(Sprite chip, string amount, int chipIndex)
    {
        if (chipImage != null) chipImage.sprite = chip;
        if (chipText != null) chipText.text = amount;
        this.chipIndex = chipIndex;
    }

    internal void SetAmount(string amount)
    {
        if (chipText != null) chipText.text = amount;
    }

    internal void SetSprite(Sprite chip)
    {
        if (chipImage != null) chipImage.sprite = chip;
    }

    internal void SetActive(bool active) => gameObject.SetActive(active);
    internal bool IsActive() => gameObject.activeSelf;

    internal void SetLeaderboardBadge(bool isRichest, bool isWinner)
    {
        if (!chipUsedForBetting) return;

        if (isRichest)
        {
            SetBadgeActive(richestBadgeObject, true);
            SetBadgeActive(winnersBadgeObject, false);
        }
        else if (isWinner)
        {
            SetBadgeActive(richestBadgeObject, false);
            SetBadgeActive(winnersBadgeObject, true);
        }
        else
        {
            HideBadgesInternal();
        }
    }

    internal void ClearLeaderboardBadge() => HideBadgesInternal();

    internal bool HasRichestBadge()
    {
        return richestBadgeObject != null && richestBadgeObject.activeSelf;
    }

    internal bool HasWinnerBadge()
    {
        return winnersBadgeObject != null && winnersBadgeObject.activeSelf;
    }
    #endregion

    #region Private Helpers
    private void HideBadgesInternal()
    {
        SetBadgeActive(richestBadgeObject, false);
        SetBadgeActive(winnersBadgeObject, false);
    }

    private static void SetBadgeActive(GameObject badge, bool active)
    {
        if (badge != null) badge.SetActive(active);
    }
    #endregion
}