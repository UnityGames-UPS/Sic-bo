using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Chip : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] internal Image chipImage;
    [SerializeField] internal TMP_Text chipText;

    [Header("Leaderboard Badges")]
    [Tooltip("Enable this ONLY on chips spawned inside bet areas (not selector/decoration chips)")]
    [SerializeField] private bool chipUsedForBetting = false;
    [Tooltip("Badge shown when the chip owner is in the Richest top-3")]
    [SerializeField] private GameObject richestBadgeObject;
    [Tooltip("Badge shown when the chip owner is in the Winners top-3")]
    [SerializeField] private GameObject winnersBadgeObject;
    #endregion

    #region Public Properties
    internal int chipIndex { get; private set; }
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Always start with badges hidden so decorative chips are never affected
        HideBadgesInternal();
    }
    #endregion

    #region Public API - Core
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

    internal void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }

    internal bool IsActive() => gameObject.activeSelf;
    #endregion

    #region Public API - Leaderboard Badges
    /// <summary>
    /// Shows the richest or winners badge on this chip.
    /// Has NO effect if chipUsedForBetting is false (selector / decoration chips stay clean).
    /// Both badges are mutually exclusive: richest takes priority if both flags are true.
    /// Call this right after a chip is placed into a bet area.
    /// </summary>
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

    /// <summary>
    /// Hides both badges. Call when the chip is cleared / returned to pool.
    /// Safe to call on non-betting chips (no-op because badges are already hidden).
    /// </summary>
    internal void ClearLeaderboardBadge()
    {
        HideBadgesInternal();
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