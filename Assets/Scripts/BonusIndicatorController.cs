using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Manages bonus indicators on bet areas.
///
/// LIFECYCLE (mirrors PlayerBetComponent pattern):
///   1. BetController calls InitializePool() at startup → one BonusIndicator pre-spawned
///      per bet area as a child, all disabled.
///   2. Bonus broadcast → ShowBonusAnnouncements() enables + configures relevant indicators
///      IMMEDIATELY (no spawn delay, no pop-in animation).
///   3. Dice result → HandleDiceResult():
///        • Player has bet on that area AND it's a winning option → animate to green.
///        • Anything else (no player bet, or not winning) → disable.
///   4. OnRoundStart → ClearAllIndicators() disables everything.
/// </summary>
public class BonusIndicatorController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Number Sprites - Brown (Announced)")]
    [Tooltip("Sprites for digits 0-9 in brown color")]
    [SerializeField] private Sprite[] brownNumberSprites = new Sprite[10];

    [Tooltip("Brown multiplier 'X' symbol")]
    [SerializeField] private Sprite brownMultiplierSprite;

    [Tooltip("Brown background sprite")]
    [SerializeField] private Sprite brownBackgroundSprite;

    [Tooltip("Brown decimal point '.' sprite")]
    [SerializeField] private Sprite brownDotSprite;

    [Header("Number Sprites - Green (Won)")]
    [Tooltip("Sprites for digits 0-9 in green color")]
    [SerializeField] private Sprite[] greenNumberSprites = new Sprite[10];

    [Tooltip("Green multiplier 'X' symbol")]
    [SerializeField] private Sprite greenMultiplierSprite;

    [Tooltip("Green background sprite")]
    [SerializeField] private Sprite greenBackgroundSprite;

    [Tooltip("Green decimal point '.' sprite")]
    [SerializeField] private Sprite greenDotSprite;

    [Header("Bonus Indicator Prefab")]
    [Tooltip("Prefab with BonusIndicator component attached")]
    [SerializeField] private GameObject bonusIndicatorPrefab;

    [Header("Win Animation Settings")]
    [SerializeField] private float scaleOutDuration = 0.18f;
    [SerializeField] private float scaleInDuration = 0.22f;
    [SerializeField] private float winScale = 1.2f;

    [Header("Decimal Support")]
    [Tooltip("Enable if bonuses can be decimal values (e.g. 2.5x)")]
    [SerializeField] private bool supportDecimalMultipliers = false;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    #endregion

    #region Private Fields
    // betOption → pre-spawned BonusIndicator (always exists once initialized)
    private readonly Dictionary<string, BonusIndicator> indicatorPool =
        new Dictionary<string, BonusIndicator>();

    // bet options that are currently visible (active bonus this round)
    private readonly HashSet<string> activeBonusOptions = new HashSet<string>();

    // bet options the player has chips on this round
    private readonly HashSet<string> playerBetAreas = new HashSet<string>();

    private bool isPoolInitialized = false;
    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Pool Initialization  (called by BetController after its own pool is built)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pre-spawns one disabled BonusIndicator as a child of each bet area container.
    /// Must be called once, after all PlayerBetContainers are valid.
    /// </summary>
    public void InitializePool(Dictionary<string, Transform> betAreaContainers)
    {
        if (isPoolInitialized)
        {
            if (showDebugLogs)
                Debug.Log("[BonusIndicator] Pool already initialized – skipping.");
            return;
        }

        if (bonusIndicatorPrefab == null)
        {
            Debug.LogError("[BonusIndicatorController] bonusIndicatorPrefab is null!");
            return;
        }

        foreach (var kvp in betAreaContainers)
        {
            string betOption = kvp.Key;
            Transform container = kvp.Value;

            if (container == null) continue;

            GameObject go = Instantiate(bonusIndicatorPrefab, container);
            BonusIndicator indicator = go.GetComponent<BonusIndicator>();

            if (indicator == null)
            {
                Debug.LogError("[BonusIndicatorController] Prefab is missing BonusIndicator component!");
                Destroy(go);
                continue;
            }

            indicator.betOption = betOption;
            go.name = $"BonusIndicator_{betOption}";
            indicator.transform.localScale = Vector3.one;
            go.SetActive(false);                    // disabled until a bonus is broadcast

            indicatorPool[betOption] = indicator;
        }

        isPoolInitialized = true;

        if (showDebugLogs)
            Debug.Log($"[BonusIndicator] Pool initialized – {indicatorPool.Count} indicators pre-spawned.");
    }
    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Public API – called by GameManager
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Bonus broadcast received (int multiplier version).
    /// Enables and configures matching indicators IMMEDIATELY – no delay.
    /// </summary>
    public void ShowBonusAnnouncements(Dictionary<string, int> bonuses)
    {
        HideAllActiveIndicators();
        activeBonusOptions.Clear();

        foreach (var kvp in bonuses)
        {
            ShowSingleBonus(kvp.Key, kvp.Value);
        }

        if (showDebugLogs)
            Debug.Log($"[BonusIndicator] Showing {activeBonusOptions.Count} bonus announcement(s) (brown).");
    }

    /// <summary>
    /// Bonus broadcast received (float/decimal multiplier version).
    /// </summary>
    public void ShowBonusAnnouncements(Dictionary<string, float> bonuses)
    {
        HideAllActiveIndicators();
        activeBonusOptions.Clear();

        foreach (var kvp in bonuses)
        {
            ShowSingleBonus(kvp.Key, kvp.Value);
        }

        if (showDebugLogs)
            Debug.Log($"[BonusIndicator] Showing {activeBonusOptions.Count} bonus announcement(s) (brown, float).");
    }

    /// <summary>
    /// Called once the dice result is known.
    /// - Areas where player has a bet AND is a winning option → animate green.
    /// - All other active bonus areas → disable.
    /// </summary>
    public void HandleDiceResult(List<string> winningBetOptions)
    {
        var winningSet = new HashSet<string>(winningBetOptions);

        foreach (string betOption in activeBonusOptions)
        {
            if (!indicatorPool.TryGetValue(betOption, out BonusIndicator indicator)) continue;
            if (indicator == null) continue;

            bool playerHasBet = playerBetAreas.Contains(betOption);
            bool isWinning = winningSet.Contains(betOption);

            if (playerHasBet && isWinning)
            {
                AnimateIndicatorToGreen(indicator);

                if (showDebugLogs)
                    Debug.Log($"[BonusIndicator] {betOption} → GREEN (player won with bonus).");
            }
            else
            {
                indicator.gameObject.SetActive(false);

                if (showDebugLogs)
                    Debug.Log($"[BonusIndicator] {betOption} → hidden " +
                              $"(playerBet={playerHasBet}, winning={isWinning}).");
            }
        }
    }

    /// <summary>
    /// Disable all indicators and reset round state.  Call on round start.
    /// Does NOT destroy GameObjects – they stay in the pool.
    /// </summary>
    public void ClearAllIndicators()
    {
        HideAllActiveIndicators();

        // Also make sure every indicator in the pool is off (safety net)
        foreach (var kvp in indicatorPool)
        {
            if (kvp.Value != null)
            {
                kvp.Value.transform.DOKill();
                kvp.Value.transform.localScale = Vector3.one;
                kvp.Value.gameObject.SetActive(false);
            }
        }

        activeBonusOptions.Clear();
        playerBetAreas.Clear();

        if (showDebugLogs)
            Debug.Log("[BonusIndicator] All indicators cleared (pool kept).");
    }

    // ── Player bet tracking ──────────────────────────────────────────────────

    /// <summary>Replace the full set of areas the player has chips on.</summary>
    public void UpdatePlayerBetAreas(List<string> betOptions)
    {
        playerBetAreas.Clear();
        if (betOptions != null)
            foreach (string opt in betOptions)
                playerBetAreas.Add(opt);
    }

    public void AddPlayerBetArea(string betOption) => playerBetAreas.Add(betOption);
    public void RemovePlayerBetArea(string betOption) => playerBetAreas.Remove(betOption);
    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Private – Display Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowSingleBonus(string betOption, float multiplier)
    {
        if (!indicatorPool.TryGetValue(betOption, out BonusIndicator indicator))
        {
            // Pool not ready yet (edge case) – skip silently
            if (showDebugLogs)
                Debug.LogWarning($"[BonusIndicator] No pooled indicator for '{betOption}' – was InitializePool() called?");
            return;
        }

        // Configure sprite content
        SetupIndicator(indicator, multiplier, isWon: false);

        // Enable immediately – no pop-in tween so there is zero delay
        indicator.transform.localScale = Vector3.one;
        indicator.gameObject.SetActive(true);

        activeBonusOptions.Add(betOption);
    }

    private void SetupIndicator(BonusIndicator indicator, float multiplier, bool isWon)
    {
        indicator.multiplier = multiplier;
        indicator.isWon = isWon;

        Sprite[] numberSprites = isWon ? greenNumberSprites : brownNumberSprites;
        Sprite multiplierSprite = isWon ? greenMultiplierSprite : brownMultiplierSprite;
        Sprite bgSprite = isWon ? greenBackgroundSprite : brownBackgroundSprite;

        bool isDecimal = supportDecimalMultipliers && (multiplier % 1f != 0f);

        if (isDecimal)
        {
            indicator.SetupDecimal(multiplier, numberSprites, multiplierSprite,
                brownDotSprite, greenDotSprite, isWon, bgSprite);
        }
        else
        {
            int intMultiplier = Mathf.RoundToInt(multiplier);
            indicator.SetupInteger(intMultiplier, numberSprites, multiplierSprite, bgSprite);
        }
    }

    private void HideAllActiveIndicators()
    {
        foreach (string betOption in activeBonusOptions)
        {
            if (indicatorPool.TryGetValue(betOption, out BonusIndicator indicator) && indicator != null)
            {
                indicator.transform.DOKill();
                indicator.transform.localScale = Vector3.one;
                indicator.gameObject.SetActive(false);
            }
        }
    }
    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Private – Win Animation
    // ─────────────────────────────────────────────────────────────────────────

    private void AnimateIndicatorToGreen(BonusIndicator indicator)
    {
        if (indicator == null || indicator.isWon) return;

        indicator.transform.DOKill();

        DOTween.Sequence()
            // 1. Scale down (brown)
            .Append(indicator.transform.DOScale(0f, scaleOutDuration).SetEase(Ease.InBack))
            // 2. Swap to green sprites
            .AppendCallback(() =>
            {
                indicator.ChangeToWonState(
                    greenNumberSprites, greenMultiplierSprite,
                    greenBackgroundSprite, greenDotSprite);
            })
            // 3. Pop in (green)
            .Append(indicator.transform.DOScale(winScale, scaleInDuration).SetEase(Ease.OutBack))
            // 4. Settle
            .Append(indicator.transform.DOScale(1f, 0.12f).SetEase(Ease.InOutQuad))
            .Play();
    }
    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Static Helper – kept for backwards compat with BetController
    // ─────────────────────────────────────────────────────────────────────────

    public static Dictionary<string, Transform> BuildBetAreaContainerMap(
        SimpleBetArea smallArea, SimpleBetArea bigArea,
        SimpleBetArea oddArea, SimpleBetArea evenArea,
        List<TripleSameDiceArea> tripleDiceAreas,
        List<SingleDiceArea> singleDiceAreas,
        List<SumArea> sumAreas)
    {
        var map = new Dictionary<string, Transform>();

        if (smallArea?.PlayerBetContainer != null) map["small"] = smallArea.PlayerBetContainer;
        if (bigArea?.PlayerBetContainer != null) map["big"] = bigArea.PlayerBetContainer;
        if (oddArea?.PlayerBetContainer != null) map["odd"] = oddArea.PlayerBetContainer;
        if (evenArea?.PlayerBetContainer != null) map["even"] = evenArea.PlayerBetContainer;

        for (int i = 0; i < tripleDiceAreas?.Count; i++)
            if (tripleDiceAreas[i]?.PlayerBetContainer != null)
                map[$"specific_3_{i + 1}"] = tripleDiceAreas[i].PlayerBetContainer;

        for (int i = 0; i < singleDiceAreas?.Count; i++)
            if (singleDiceAreas[i]?.PlayerBetContainer != null)
                map[$"single_{i + 1}"] = singleDiceAreas[i].PlayerBetContainer;

        for (int i = 0; i < sumAreas?.Count; i++)
            if (sumAreas[i]?.PlayerBetContainer != null)
                map[$"sum_{i + 4}"] = sumAreas[i].PlayerBetContainer;

        return map;
    }
    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Validation
    // ─────────────────────────────────────────────────────────────────────────

    private void OnValidate()
    {
        if (brownNumberSprites.Length != 10)
            Debug.LogWarning("[BonusIndicatorController] brownNumberSprites needs exactly 10 entries (0-9).");

        if (greenNumberSprites.Length != 10)
            Debug.LogWarning("[BonusIndicatorController] greenNumberSprites needs exactly 10 entries (0-9).");

        if (supportDecimalMultipliers)
        {
            if (brownDotSprite == null)
                Debug.LogWarning("[BonusIndicatorController] brownDotSprite not assigned (required for decimals).");
            if (greenDotSprite == null)
                Debug.LogWarning("[BonusIndicatorController] greenDotSprite not assigned (required for decimals).");
        }
    }
    #endregion
}