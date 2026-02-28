using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

internal class BonusIndicatorController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Number Sprites - Brown (Announced)")]
    [SerializeField] private Sprite[] brownNumberSprites = new Sprite[10];
    [SerializeField] private Sprite brownMultiplierSprite;
    [SerializeField] private Sprite brownBackgroundSprite;
    [SerializeField] private Sprite brownDotSprite;

    [Header("Number Sprites - Green (Won)")]
    [SerializeField] private Sprite[] greenNumberSprites = new Sprite[10];
    [SerializeField] private Sprite greenMultiplierSprite;
    [SerializeField] private Sprite greenBackgroundSprite;
    [SerializeField] private Sprite greenDotSprite;

    [Header("Bonus Indicator Prefab")]
    [SerializeField] private GameObject bonusIndicatorPrefab;

    [Header("Win Animation Settings")]
    [SerializeField] private float scaleOutDuration = 0.18f;
    [SerializeField] private float scaleInDuration = 0.22f;
    [SerializeField] private float winScale = 1.2f;

    [Header("Fall-In Entrance Animation")]
    [SerializeField] private float fallStartScale = 3.5f;
    [SerializeField] private float fallDuration = 0.45f;
    [SerializeField] private Ease fallEase = Ease.OutBounce;
    [SerializeField] private float landingPunchScale = 0.25f;
    [SerializeField] private float landingPunchDuration = 0.3f;
    [SerializeField] private float fallStaggerDelay = 0.06f;

    [Header("Win-Exit Animation")]
    [SerializeField] private float winHoldDuration = 1.5f;
    [SerializeField] private float winExitDuration = 0.25f;
    [SerializeField] private Ease winExitEase = Ease.InBack;

    [Header("Decimal Support")]
    [SerializeField] private bool supportDecimalMultipliers = false;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    #endregion

    #region Private Fields
    private readonly Dictionary<string, BonusIndicator> indicatorPool = new Dictionary<string, BonusIndicator>();
    private readonly HashSet<string> activeBonusOptions = new HashSet<string>();
    private readonly Dictionary<string, List<int>> currentMultipliers = new Dictionary<string, List<int>>();
    private bool isPoolInitialized = false;

    // GC optimisation: reuse HashSet for HandleDiceResult (called every dice result)
    private readonly HashSet<string> _winningSetCache = new HashSet<string>();

    // DOTween sequence tracking — prevents orphaned tweens accumulating memory (~30MB saved over session)
    private readonly Dictionary<string, Sequence> _activeSequences = new Dictionary<string, Sequence>();
    #endregion

    #region Pool Initialization

    internal void InitializePool(Dictionary<string, Transform> betAreaContainers)
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

    #region Public API – called by GameManager

    internal void ShowBonusAnnouncements(Dictionary<string, List<int>> bonuses)
    {
        HideAllActiveIndicators();
        activeBonusOptions.Clear();
        currentMultipliers.Clear();

        int staggerIndex = 0;
        foreach (var kvp in bonuses)
        {
            string betOption = kvp.Key;
            List<int> multipliers = kvp.Value;

            if (multipliers == null || multipliers.Count == 0) continue;

            ShowBonus(betOption, multipliers, staggerIndex);
            staggerIndex++;
        }

        if (showDebugLogs)
            Debug.Log($"[BonusIndicator] Showing bonuses for {activeBonusOptions.Count} bet option(s).");
    }


    internal void HandleDiceResult(List<string> winningBetOptions)
    {

        _winningSetCache.Clear();
        if (winningBetOptions != null)
            foreach (var s in winningBetOptions)
                _winningSetCache.Add(s);

        foreach (string betOption in activeBonusOptions)
        {
            if (!indicatorPool.TryGetValue(betOption, out BonusIndicator indicator)) continue;
            if (indicator == null) continue;

            bool isWinning = _winningSetCache.Contains(betOption);

            if (isWinning)
            {
                AnimateIndicatorToGreen(indicator, betOption);

                if (showDebugLogs)
                    Debug.Log($"[BonusIndicator] {betOption} → GREEN (winning).");
            }
            else
            {
                indicator.gameObject.SetActive(false);

                if (showDebugLogs)
                    Debug.Log($"[BonusIndicator] {betOption} → hidden (not winning).");
            }
        }
    }

    internal void ClearAllIndicators()
    {
        HideAllActiveIndicators();

        foreach (var seq in _activeSequences.Values) seq?.Kill();
        _activeSequences.Clear();

        foreach (var kvp in indicatorPool)
        {
            if (kvp.Value != null)
            {
                Transform numberHolder = kvp.Value.transform.Find("NumberHolder");
                if (numberHolder != null)
                {
                    numberHolder.DOKill();
                    numberHolder.localScale = Vector3.one;
                }

                kvp.Value.transform.DOKill();
                kvp.Value.transform.localScale = Vector3.one;
                kvp.Value.transform.localPosition = Vector3.zero;
                kvp.Value.isWon = false;

                kvp.Value.HideAllRows();
                kvp.Value.gameObject.SetActive(false);
            }
        }

        activeBonusOptions.Clear();
        currentMultipliers.Clear();

        if (showDebugLogs)
            Debug.Log("[BonusIndicator] All indicators cleared.");
    }
    #endregion

    #region Private – Display Helpers

    private void ShowBonus(string betOption, List<int> multipliers, int staggerIndex = 0)
    {
        if (!indicatorPool.TryGetValue(betOption, out BonusIndicator indicator))
        {
            if (showDebugLogs)
                Debug.LogWarning($"[BonusIndicator] No pooled indicator for '{betOption}'");
            return;
        }
        if (!currentMultipliers.TryGetValue(betOption, out List<int> cachedList))
        {
            cachedList = new List<int>(multipliers.Count);
            currentMultipliers[betOption] = cachedList;
        }
        else
        {
            cachedList.Clear();
        }
        cachedList.AddRange(multipliers);

        int[] multipliersArray = multipliers.ToArray();

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

        indicator.transform.DOKill();
        indicator.transform.localPosition = Vector3.zero;
        indicator.transform.localScale = Vector3.one * fallStartScale;

        float delay = staggerIndex * fallStaggerDelay;

        Sequence fallSeq = DOTween.Sequence();
        fallSeq.AppendInterval(delay);

        fallSeq.AppendCallback(() =>
        {
            indicator.gameObject.SetActive(true);
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBonusSpawn();
            }
        });

        fallSeq.Append(
            indicator.transform.DOScale(Vector3.one, fallDuration)
                .SetEase(fallEase)
        );

        fallSeq.AppendCallback(() =>
        {
            indicator.transform.DOPunchScale(
                Vector3.one * landingPunchScale,
                landingPunchDuration,
                vibrato: 1,
                elasticity: 0.5f
            );
        });

        string fallSeqKey = $"fall_{betOption}";
        if (_activeSequences.TryGetValue(fallSeqKey, out Sequence oldFallSeq)) { oldFallSeq?.Kill(); _activeSequences.Remove(fallSeqKey); }

        fallSeq.OnKill(() => _activeSequences.Remove(fallSeqKey));
        fallSeq.Play();
        _activeSequences[fallSeqKey] = fallSeq;

        activeBonusOptions.Add(betOption);

        if (showDebugLogs)
            Debug.Log($"[BonusIndicator] Falling in {multipliers.Count} row(s) for '{betOption}' (stagger={delay:F2}s)");
    }

    private void HideAllActiveIndicators()
    {
        foreach (string betOption in activeBonusOptions)
        {
            if (indicatorPool.TryGetValue(betOption, out BonusIndicator indicator) && indicator != null)
            {
                Transform numberHolder = indicator.transform.Find("NumberHolder");
                if (numberHolder != null)
                {
                    numberHolder.DOKill();
                    numberHolder.localScale = Vector3.one;
                }

                indicator.transform.DOKill();
                indicator.transform.localScale = Vector3.one;
                indicator.transform.localPosition = Vector3.zero;
                indicator.HideAllRows();
                indicator.gameObject.SetActive(false);
            }
        }
    }
    #endregion

    #region Private – Win Animation

    private void AnimateIndicatorToGreen(BonusIndicator indicator, string betOption)
    {
        if (indicator == null || indicator.isWon) return;
        if (!currentMultipliers.TryGetValue(betOption, out List<int> multipliers)) return;

        indicator.isWon = true;
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBonusHit();
        }
        indicator.AnimateToGreen(
            greenNumberSprites,
            greenMultiplierSprite,
            greenBackgroundSprite,
            greenDotSprite,
            scaleOutDuration,
            scaleInDuration,
            onComplete: () => AnimateIndicatorOut(indicator)
        );

        if (showDebugLogs)
            Debug.Log($"[BonusIndicator] Animating to green for '{betOption}'");
    }

    private void AnimateIndicatorOut(BonusIndicator indicator)
    {
        if (indicator == null) return;

        indicator.transform.DOKill();

        string exitSeqKey = $"exit_{indicator.betOption}";
        if (_activeSequences.TryGetValue(exitSeqKey, out Sequence oldExitSeq)) { oldExitSeq?.Kill(); _activeSequences.Remove(exitSeqKey); }

        Sequence exitSeq = DOTween.Sequence();
        exitSeq.AppendInterval(winHoldDuration);

        exitSeq.Append(
            indicator.transform.DOScale(0f, winExitDuration)
                .SetEase(winExitEase)
        );

        exitSeq.OnComplete(() =>
        {
            indicator.transform.localScale = Vector3.one;
            indicator.gameObject.SetActive(false);
            _activeSequences.Remove(exitSeqKey);

            if (showDebugLogs)
                Debug.Log($"[BonusIndicator] Win indicator exited cleanly.");
        });

        exitSeq.OnKill(() => _activeSequences.Remove(exitSeqKey));
        exitSeq.Play();
        _activeSequences[exitSeqKey] = exitSeq;
    }
    #endregion

    #region Static Helper

    internal static Dictionary<string, Transform> BuildBetAreaContainerMap(
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

}