using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Manages bonus indicators on bet areas with 3 pre-defined rows per indicator.
///
/// STRUCTURE:
///   - Each BonusIndicator prefab has 3 rows pre-built (Row1, Row2, Row3)
///   - Each row has 4 images: X, Number1, Number2, Number3
///   - Rows are shown/hidden based on how many multipliers are in the array
///
/// LIFECYCLE:
///   1. BetController calls InitializePool() → one BonusIndicator per bet area
///   2. Bonus broadcast → ShowBonusAnnouncements() shows rows based on array length
///   3. Dice result → HandleDiceResult() animates winning indicators to green
///   4. OnRoundStart → ClearAllIndicators() hides all rows
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
    [Tooltip("Prefab with BonusIndicator component (3 rows pre-built)")]
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
    // betOption → pre-spawned BonusIndicator
    private readonly Dictionary<string, BonusIndicator> indicatorPool =
        new Dictionary<string, BonusIndicator>();

    // bet options that are currently visible (active bonus this round)
    private readonly HashSet<string> activeBonusOptions = new HashSet<string>();

    // bet options the player has chips on this round
    private readonly HashSet<string> playerBetAreas = new HashSet<string>();

    // Store current multipliers for re-setup during color change
    private readonly Dictionary<string, List<int>> currentMultipliers =
        new Dictionary<string, List<int>>();

    private bool isPoolInitialized = false;
    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Pool Initialization
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pre-spawns one disabled BonusIndicator as a child of each bet area container.
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
            indicator.HideAllRows();
            go.SetActive(false);

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
    /// NEW: Bonus broadcast with array-based multipliers
    /// Example: {"single_1": [2, 3, 3], "specific_3_2": [3, 2], "sum_12": [1]}
    /// </summary>
    public void ShowBonusAnnouncements(Dictionary<string, List<int>> bonuses)
    {
        HideAllActiveIndicators();
        activeBonusOptions.Clear();
        currentMultipliers.Clear();

        foreach (var kvp in bonuses)
        {
            string betOption = kvp.Key;
            List<int> multipliers = kvp.Value;

            if (multipliers == null || multipliers.Count == 0) continue;

            ShowBonus(betOption, multipliers);
        }

        if (showDebugLogs)
            Debug.Log($"[BonusIndicator] Showing bonuses for {activeBonusOptions.Count} bet option(s).");
    }

    /// <summary>
    /// LEGACY: Backwards compatibility for old single-multiplier format
    /// </summary>
    public void ShowBonusAnnouncements(Dictionary<string, int> bonuses)
    {
        Dictionary<string, List<int>> newFormat = new Dictionary<string, List<int>>();
        foreach (var kvp in bonuses)
        {
            newFormat[kvp.Key] = new List<int> { kvp.Value };
        }
        ShowBonusAnnouncements(newFormat);
    }

    /// <summary>
    /// LEGACY: Float version for backwards compatibility
    /// </summary>
    public void ShowBonusAnnouncements(Dictionary<string, float> bonuses)
    {
        Dictionary<string, List<int>> newFormat = new Dictionary<string, List<int>>();
        foreach (var kvp in bonuses)
        {
            newFormat[kvp.Key] = new List<int> { Mathf.RoundToInt(kvp.Value) };
        }
        ShowBonusAnnouncements(newFormat);
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
                AnimateIndicatorToGreen(indicator, betOption);

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
    /// Disable all indicators and reset round state.
    /// </summary>
    public void ClearAllIndicators()
    {
        HideAllActiveIndicators();

        foreach (var kvp in indicatorPool)
        {
            if (kvp.Value != null)
            {
                // Kill animation on the single number holder
                Transform numberHolder = kvp.Value.transform.Find("NumberHolder");
                if (numberHolder != null)
                {
                    numberHolder.DOKill();
                    numberHolder.localScale = Vector3.one;
                }

                kvp.Value.transform.DOKill();
                kvp.Value.transform.localScale = Vector3.one;
                kvp.Value.HideAllRows();
                kvp.Value.gameObject.SetActive(false);
            }
        }

        activeBonusOptions.Clear();
        playerBetAreas.Clear();
        currentMultipliers.Clear();

        if (showDebugLogs)
            Debug.Log("[BonusIndicator] All indicators cleared.");
    }

    // ── Player bet tracking ──────────────────────────────────────────────────

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

    private void ShowBonus(string betOption, List<int> multipliers)
    {
        if (!indicatorPool.TryGetValue(betOption, out BonusIndicator indicator))
        {
            if (showDebugLogs)
                Debug.LogWarning($"[BonusIndicator] No pooled indicator for '{betOption}'");
            return;
        }

        // Store multipliers for later green conversion
        currentMultipliers[betOption] = new List<int>(multipliers);

        // Convert to array
        int[] multipliersArray = multipliers.ToArray();

        // Setup with brown sprites
        if (supportDecimalMultipliers)
        {
            float[] floatArray = System.Array.ConvertAll(multipliersArray, x => (float)x);
            indicator.Setup(floatArray, brownNumberSprites, brownMultiplierSprite,
                brownDotSprite, greenDotSprite, false, brownBackgroundSprite);
        }
        else
        {
            indicator.Setup(multipliersArray, brownNumberSprites, brownMultiplierSprite,
                brownBackgroundSprite, brownDotSprite, false);
        }

        // Enable immediately
        indicator.transform.localScale = Vector3.one;
        indicator.gameObject.SetActive(true);

        activeBonusOptions.Add(betOption);

        if (showDebugLogs)
            Debug.Log($"[BonusIndicator] Showing {multipliers.Count} row(s) for '{betOption}'");
    }

    private void HideAllActiveIndicators()
    {
        foreach (string betOption in activeBonusOptions)
        {
            if (indicatorPool.TryGetValue(betOption, out BonusIndicator indicator) && indicator != null)
            {
                // Kill animation on the single number holder
                Transform numberHolder = indicator.transform.Find("NumberHolder");
                if (numberHolder != null)
                {
                    numberHolder.DOKill();
                    numberHolder.localScale = Vector3.one;
                }

                indicator.transform.DOKill();
                indicator.transform.localScale = Vector3.one;
                indicator.HideAllRows();
                indicator.gameObject.SetActive(false);
            }
        }
    }
    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Private – Win Animation
    // ─────────────────────────────────────────────────────────────────────────

    private void AnimateIndicatorToGreen(BonusIndicator indicator, string betOption)
    {
        if (indicator == null || indicator.isWon) return;
        if (!currentMultipliers.TryGetValue(betOption, out List<int> multipliers)) return;

        indicator.isWon = true;

        // Animate all rows at once using single number holder
        indicator.AnimateToGreen(
            greenNumberSprites,
            greenMultiplierSprite,
            greenBackgroundSprite,
            greenDotSprite,
            scaleOutDuration,
            scaleInDuration
        );

        if (showDebugLogs)
            Debug.Log($"[BonusIndicator] Animating to green for '{betOption}'");
    }
    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Static Helper
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

    #region Validation


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