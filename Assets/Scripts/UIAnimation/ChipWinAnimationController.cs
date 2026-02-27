using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

internal class ChipWinAnimationController : MonoBehaviour
{
    #region Serialized Fields
    [Header("References")]
    [SerializeField] private RectTransform dealerSpawnPoint;
    [SerializeField] private GameObject chipPrefab;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private RectTransform playerNameTarget;
    [SerializeField] private BetController betController;
    [SerializeField] private GameManager gameManager;

    [Header("Pool")]
    [SerializeField] private int dealerPoolSize = 25;
    [SerializeField] private float dealerScatterX = 28f;
    [SerializeField] private float dealerScatterY = 18f;

    [Header("Dealer to Bet Area")]
    [SerializeField] private float dealerToBetDuration = 0.50f;
    [SerializeField] private float chipStaggerDelay = 0.055f;
    [SerializeField] private float betAreaScatterX = 11f;
    [SerializeField] private float betAreaScatterY = 9f;

    [Header("Bet Area to Player")]
    [SerializeField] private float betToPlayerDuration = 0.60f;
    [SerializeField] private float cashoutStagger = 0.04f;
    [SerializeField] private float arcHeight = 110f;

    [Header("Chip Visual")]
    [SerializeField] private float chipWorkingScale = 1.0f;

    [Header("Win Animation Settings")]
    [SerializeField] private float animationStartPercent = 0.6f;
    [SerializeField] private bool enableWinAnimations = true;

    [Header("Chip Count Settings")]
    [SerializeField] private int minChipsPerWin = 1;
    [SerializeField] private int maxChipsPerWin = 8;
    [SerializeField] private double minWinForExtraChips = 1.0;
    #endregion

    #region Private Fields
    private readonly List<(RectTransform rt, Chip chip)> dealerPool = new List<(RectTransform, Chip)>();
    private readonly List<RectTransform> activeWinChips = new List<RectTransform>();
    private readonly List<RectTransform> stakeReturnChips = new List<RectTransform>();
    private bool isAnimating;
    private Coroutine winCoroutine;
    private Coroutine cashoutCoroutine;

    private readonly List<WinAreaData> _recalcCache = new List<WinAreaData>();
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
        PreSpawnDealerPool();
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        foreach (var (rt, _) in dealerPool) { if (rt) rt.DOKill(); }
        foreach (var rt in activeWinChips) { if (rt) rt.DOKill(); }
    }
    #endregion

    #region Internal API
    internal void PlayDiceResultAnimation(List<WinAreaData> winAreas, DiceResultData diceResult)
    {
        if (isAnimating || winAreas == null || winAreas.Count == 0 || diceResult == null) return;
        if (winCoroutine != null) StopCoroutine(winCoroutine);
        var recalculated = RecalculateWinAmounts(winAreas, diceResult);
        winCoroutine = StartCoroutine(CR_DealerToBetAreas(recalculated));
    }

    internal void PlayCashoutAnimation()
    {
        if (cashoutCoroutine != null) StopCoroutine(cashoutCoroutine);
        cashoutCoroutine = StartCoroutine(CR_Cashout());
    }

    internal void ResetAll()
    {
        if (winCoroutine != null) StopCoroutine(winCoroutine);
        if (cashoutCoroutine != null) StopCoroutine(cashoutCoroutine);
        winCoroutine = cashoutCoroutine = null;

        foreach (var (rt, _) in dealerPool)
        {
            if (rt == null) continue;
            rt.DOKill();
            if (rt.parent != dealerSpawnPoint) rt.SetParent(dealerSpawnPoint, worldPositionStays: false);
            rt.localPosition = new Vector3(
                Random.Range(-dealerScatterX, dealerScatterX),
                Random.Range(-dealerScatterY, dealerScatterY), 0f);
            rt.localScale = Vector3.zero;
            rt.gameObject.SetActive(false);
        }

        activeWinChips.Clear();
        stakeReturnChips.Clear();
        isAnimating = false;
    }
    #endregion

    #region Win Calculation
    private List<WinAreaData> RecalculateWinAmounts(List<WinAreaData> winAreas, DiceResultData diceResult)
    {
        _recalcCache.Clear();
        foreach (var area in winAreas)
        {
            if (area.betAmount <= 0) continue;
            double actualWin = CalculateActualWin(area.betOption, area.betAmount, diceResult);
            if (actualWin > 0)
            {
                _recalcCache.Add(new WinAreaData
                {
                    betOption = area.betOption,
                    betAreaTarget = area.betAreaTarget,
                    betAmount = area.betAmount,
                    winAmount = actualWin,
                    winRatio = actualWin / area.betAmount
                });
            }
        }
        return _recalcCache;
    }

    private double CalculateActualWin(string betOption, double betAmount, DiceResultData diceResult)
    {
        if (gameManager == null) return 0;

        if (betOption.StartsWith("single_"))
        {
            int num = GetDiceNumberFromBetOption(betOption);
            if (num == -1) return 0;
            return CalculateSingleDiceWin(betAmount, CountDiceMatches(num, diceResult));
        }
        if (betOption.StartsWith("specific_3_"))
        {
            int num = GetDiceNumberFromBetOption(betOption);
            if (num == -1) return 0;
            return CalculateSpecificTripleWin(betAmount, CountDiceMatches(num, diceResult));
        }
        return GetWagerForBetOption(betOption)?.CalculateWin(betAmount) ?? 0;
    }

    private int GetDiceNumberFromBetOption(string betOption)
    {
        string[] parts = betOption.Split('_');
        if (parts.Length > 0 && int.TryParse(parts[parts.Length - 1], out int n) && n >= 1 && n <= 6)
            return n;
        return -1;
    }

    private int CountDiceMatches(int target, DiceResultData d)
    {
        int c = 0;
        if (d.dice1 == target) c++;
        if (d.dice2 == target) c++;
        if (d.dice3 == target) c++;
        return c;
    }

    private double CalculateSingleDiceWin(double betAmount, int matchCount)
    {
        var sb = gameManager?.CurrentWagers?.side_bets;
        if (sb == null) return 0;
        switch (matchCount)
        {
            case 3: return sb.single_match_3?.CalculateWin(betAmount) ?? 0;
            case 2: return sb.single_match_2?.CalculateWin(betAmount) ?? 0;
            case 1: return sb.single_match_1?.CalculateWin(betAmount) ?? 0;
        }
        return 0;
    }

    private double CalculateSpecificTripleWin(double betAmount, int matchCount)
    {
        var sb = gameManager?.CurrentWagers?.side_bets;
        if (sb == null) return 0;
        switch (matchCount)
        {
            case 3: return sb.specific_3?.CalculateWin(betAmount) ?? 0;
            case 2: return sb.specific_2?.CalculateWin(betAmount) ?? 0;
        }
        return 0;
    }

    private BetWager GetWagerForBetOption(string betOption)
    {
        if (gameManager?.CurrentWagers == null) return null;
        switch (betOption)
        {
            case "small": return gameManager.CurrentWagers.main_bets?.small;
            case "big": return gameManager.CurrentWagers.main_bets?.big;
            case "odd": return gameManager.CurrentWagers.main_bets?.odd;
            case "even": return gameManager.CurrentWagers.main_bets?.even;
        }
        if (betOption.StartsWith("sum_"))
        {
            switch (betOption)
            {
                case "sum_4": return gameManager.CurrentWagers.op_bets?.sum_4;
                case "sum_5": return gameManager.CurrentWagers.op_bets?.sum_5;
                case "sum_6": return gameManager.CurrentWagers.op_bets?.sum_6;
                case "sum_7": return gameManager.CurrentWagers.op_bets?.sum_7;
                case "sum_8": return gameManager.CurrentWagers.op_bets?.sum_8;
                case "sum_9": return gameManager.CurrentWagers.op_bets?.sum_9;
                case "sum_10": return gameManager.CurrentWagers.op_bets?.sum_10;
                case "sum_11": return gameManager.CurrentWagers.op_bets?.sum_11;
                case "sum_12": return gameManager.CurrentWagers.op_bets?.sum_12;
                case "sum_13": return gameManager.CurrentWagers.op_bets?.sum_13;
                case "sum_14": return gameManager.CurrentWagers.op_bets?.sum_14;
                case "sum_15": return gameManager.CurrentWagers.op_bets?.sum_15;
                case "sum_16": return gameManager.CurrentWagers.op_bets?.sum_16;
                case "sum_17": return gameManager.CurrentWagers.op_bets?.sum_17;
            }
        }
        return null;
    }
    #endregion

    #region Pool
    private void PreSpawnDealerPool()
    {
        if (chipPrefab == null || dealerSpawnPoint == null) return;

        for (int i = 0; i < dealerPoolSize; i++)
        {
            GameObject go = Instantiate(chipPrefab, dealerSpawnPoint);
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) { Destroy(go); continue; }

            Chip chipComp = go.GetComponent<Chip>();
            if (chipComp == null)
                Debug.LogWarning("[ChipWinAnimationController] chipPrefab has no Chip component — sprites won't update.");

            rt.localPosition = new Vector3(
                Random.Range(-dealerScatterX, dealerScatterX),
                Random.Range(-dealerScatterY, dealerScatterY), 0f);
            rt.localScale = Vector3.zero;
            go.SetActive(false);

            dealerPool.Add((rt, chipComp));
        }
    }
    #endregion

    #region Dealer → Bet Areas
    private IEnumerator CR_DealerToBetAreas(List<WinAreaData> winAreas)
    {
        isAnimating = true;
        List<double> chipValues = betController != null ? betController.GetChipValues() : new List<double>();
        Sprite[] chipSprites = betController != null ? betController.GetChipSprites() : null;

        var assignments = new List<(RectTransform rt, Transform parent, Vector3 localPos)>();
        int poolIdx = 0;

        foreach (var area in winAreas)
        {
            if (area.betAreaTarget == null) continue;

            PlayerBetComponent playerBetComp = betController?.GetPlayerBetComponent(area.betOption);
            if (playerBetComp == null) continue;

            Transform chipParent = playerBetComp.transform;
            AudioManager.Instance?.PlayChipAdd();

            bool spawnWinChips = area.winRatio > 1.0 && area.winRatio >= minWinForExtraChips;
            if (spawnWinChips)
            {
                var combination = BuildCombination(area.winAmount, chipValues, chipSprites);
                int count = Mathf.Clamp(combination.Count, minChipsPerWin, maxChipsPerWin);

                for (int i = 0; i < count && poolIdx < dealerPool.Count; i++, poolIdx++)
                {
                    var (rt, chip) = dealerPool[poolIdx];
                    if (rt == null) continue;

                    ApplyChipVisual(chip, combination, i, chipSprites);

                    rt.gameObject.SetActive(true);
                    rt.localPosition = new Vector3(
                        Random.Range(-dealerScatterX, dealerScatterX),
                        Random.Range(-dealerScatterY, dealerScatterY), 0f);
                    rt.localScale = Vector3.zero;
                    rt.DOScale(chipWorkingScale, 0.18f).SetEase(Ease.OutBack);

                    Vector3 localPos = new Vector3(
                        Random.Range(-betAreaScatterX, betAreaScatterX),
                        Random.Range(-betAreaScatterY, betAreaScatterY), 0f);

                    assignments.Add((rt, chipParent, localPos));
                    activeWinChips.Add(rt);
                }
            }

            int stakeCount = CalculateStakeReturnChipCount(area.winRatio, area.betAmount);
            var stakeCombination = BuildCombination(area.betAmount, chipValues, chipSprites);
            int actualStakeCount = Mathf.Min(stakeCount, Mathf.Max(1, stakeCombination.Count));

            for (int i = 0; i < actualStakeCount && poolIdx < dealerPool.Count; i++, poolIdx++)
            {
                var (rt, chip) = dealerPool[poolIdx];
                if (rt == null) continue;

                ApplyChipVisual(chip, stakeCombination, i, chipSprites);

                RectTransform parentRT = chipParent as RectTransform;
                if (parentRT == null) continue;

                rt.SetParent(chipParent, worldPositionStays: false);
                rt.SetAsFirstSibling();
                rt.localPosition = new Vector3(
                    Random.Range(-betAreaScatterX, betAreaScatterX),
                    Random.Range(-betAreaScatterY, betAreaScatterY), 0f);
                rt.localScale = Vector3.zero;
                rt.gameObject.SetActive(false);

                stakeReturnChips.Add(rt);
            }
        }

        yield return new WaitForSeconds(0.20f);

        var animData = new List<(RectTransform rt, Vector3 worldTarget)>();
        foreach (var (rt, parent, localPos) in assignments)
        {
            if (rt == null || parent == null) continue;
            RectTransform parentRT = parent as RectTransform;
            if (parentRT == null) continue;

            Vector3 worldTarget = parentRT.TransformPoint(localPos);
            rt.SetParent(parent, worldPositionStays: true);
            rt.SetAsFirstSibling();
            animData.Add((rt, worldTarget));
        }

        if (enableWinAnimations && assignments.Count > 0)
        {
            DOVirtual.DelayedCall(
                dealerToBetDuration * animationStartPercent,
                () => TriggerAllWinCountingAnimations(winAreas));
        }

        foreach (var (rt, worldTarget) in animData)
        {
            if (rt == null) continue;
            rt.DOMove(worldTarget, dealerToBetDuration)
              .SetEase(Ease.OutQuad)
              .OnComplete(() =>
              {
                  if (rt != null)
                      rt.localPosition = rt.parent.InverseTransformPoint(worldTarget);
              });

            yield return new WaitForSeconds(chipStaggerDelay);
        }

        yield return new WaitForSeconds(dealerToBetDuration);

        isAnimating = false;
        winCoroutine = null;
    }

    private int CalculateChipCount(WinAreaData area)
    {
        if (area.winAmount <= 0) return 0;
        double ratio = area.winRatio;
        if (ratio <= minWinForExtraChips) return 0;
        int count;
        if (ratio >= 50) count = maxChipsPerWin;
        else if (ratio >= 30) count = Mathf.Min(7, maxChipsPerWin);
        else if (ratio >= 15) count = Mathf.Min(6, maxChipsPerWin);
        else if (ratio >= 10) count = Mathf.Min(5, maxChipsPerWin);
        else if (ratio >= 5) count = Mathf.Min(4, maxChipsPerWin);
        else if (ratio >= 3) count = Mathf.Min(3, maxChipsPerWin);
        else count = Mathf.Min(2, maxChipsPerWin);
        return Mathf.Max(minChipsPerWin, count);
    }

    private int CalculateStakeReturnChipCount(double winRatio, double betAmount)
    {
        if (winRatio <= 0) return 0;
        double val = winRatio * betAmount;
        if (val >= 20) return 3;
        if (val >= 5) return 2;
        return 1;
    }

    private void TriggerAllWinCountingAnimations(List<WinAreaData> winAreas)
    {
        if (betController == null) return;
        foreach (var winArea in winAreas)
        {
            PlayerBetComponent comp = betController.GetPlayerBetComponent(winArea.betOption);
            if (comp == null || winArea.betAmount <= 0) continue;
            comp.AnimateWinWithRatio(winArea.winAmount / winArea.betAmount);
        }
    }
    #endregion

    #region Bet Areas → Player (Cashout)
    private IEnumerator CR_Cashout()
    {
        if (playerNameTarget == null) yield break;

        var toSweep = new List<RectTransform>(activeWinChips);

        foreach (var rt in stakeReturnChips)
        {
            if (rt == null) continue;
            rt.gameObject.SetActive(true);
            rt.DOScale(chipWorkingScale, 0.12f).SetEase(Ease.OutBack);
            betController?.RefreshBadgesForContainer(rt.parent);
            toSweep.Add(rt);
        }
        stakeReturnChips.Clear();
        int extraNeeded = Mathf.Min(3, dealerPool.Count);
        foreach (var (rt, _) in dealerPool)
        {
            if (extraNeeded <= 0) break;
            if (activeWinChips.Contains(rt)) continue;

            rt.gameObject.SetActive(true);
            rt.localPosition = new Vector3(
                Random.Range(-dealerScatterX, dealerScatterX),
                Random.Range(-dealerScatterY, dealerScatterY), 0f);
            rt.localScale = Vector3.zero;
            rt.DOScale(chipWorkingScale * 0.70f, 0.14f).SetEase(Ease.OutBack);

            toSweep.Add(rt);
            extraNeeded--;
        }

        yield return new WaitForSeconds(0.18f);

        var chipCanvasPositions = new Dictionary<RectTransform, Vector2>();
        foreach (var rt in toSweep)
        {
            if (rt == null) continue;
            chipCanvasPositions[rt] = GetCanvasPosition(rt);
        }

        foreach (var rt in toSweep)
        {
            if (rt == null) continue;
            if (rt.parent != targetCanvas.transform)
            {
                rt.SetParent(targetCanvas.transform, worldPositionStays: false);
                rt.SetAsLastSibling();
                if (chipCanvasPositions.ContainsKey(rt))
                    rt.anchoredPosition = chipCanvasPositions[rt];
            }
        }

        Vector2 playerCanvasPos = GetCanvasPosition(playerNameTarget);

        foreach (var rt in toSweep)
        {
            if (rt == null) continue;

            Vector2 startPos = rt.anchoredPosition;
            Vector2 midPos = Vector2.Lerp(startPos, playerCanvasPos, 0.5f)
                             + new Vector2(Random.Range(-18f, 18f), arcHeight);

            float halfDur = betToPlayerDuration * 0.45f;
            float landDur = betToPlayerDuration * 0.55f;

            DOTween.Sequence()
                .Append(rt.DOAnchorPos(midPos, halfDur).SetEase(Ease.OutQuad))
                .Append(rt.DOAnchorPos(playerCanvasPos, landDur).SetEase(Ease.InQuad))
                .Join(rt.DOScale(Vector3.zero, landDur).SetDelay(halfDur).SetEase(Ease.InBack))
                .OnComplete(() =>
                {
                    if (rt == null) return;
                    AudioManager.Instance?.PlayChipAdd();
                    rt.gameObject.SetActive(false);
                    rt.SetParent(dealerSpawnPoint, worldPositionStays: false);
                    rt.localPosition = Vector3.zero;
                });

            yield return new WaitForSeconds(cashoutStagger);
        }

        yield return new WaitForSeconds(betToPlayerDuration + 0.25f);

        activeWinChips.Clear();
        stakeReturnChips.Clear();
        cashoutCoroutine = null;
    }
    #endregion

    #region Helpers

    private List<ChipCombinationItem> BuildCombination(double amount, List<double> chipValues, Sprite[] sprites)
    {
        if (chipValues != null && chipValues.Count > 0)
        {
            var combo = GameUtilities.FindChipCombination(amount, chipValues);
            if (combo != null && combo.Count > 0)
                return combo;
        }

        return new List<ChipCombinationItem>
        {
            new ChipCombinationItem { amount = amount, chipIndex = FallbackSpriteIndex(amount, sprites) }
        };
    }
    private static void ApplyChipVisual(Chip chip, List<ChipCombinationItem> combination, int i, Sprite[] sprites)
    {
        if (chip == null || sprites == null || sprites.Length == 0 || combination.Count == 0) return;

        
        ChipCombinationItem item = combination[i % combination.Count];
        int safeIdx = Mathf.Clamp(item.chipIndex, 0, sprites.Length - 1);

        chip.SetData(
            sprites[safeIdx],
            GameUtilities.FormatCurrency(item.amount),
            safeIdx);
    }

    private static int FallbackSpriteIndex(double amount, Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0) return 0;
        if (amount >= 500) return 0;
        if (amount >= 100) return Mathf.Min(1, sprites.Length - 1);
        if (amount >= 50) return Mathf.Min(2, sprites.Length - 1);
        if (amount >= 10) return Mathf.Min(3, sprites.Length - 1);
        if (amount >= 5) return Mathf.Min(4, sprites.Length - 1);
        return Mathf.Min(5, sprites.Length - 1);
    }

    private Vector2 GetCanvasPosition(RectTransform rt)
    {
        if (rt == null || targetCanvas == null) return Vector2.zero;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(targetCanvas.worldCamera, rt.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetCanvas.GetComponent<RectTransform>(), screenPoint, targetCanvas.worldCamera, out Vector2 localPoint);
        return localPoint;
    }
    #endregion
}

[System.Serializable]
internal class WinAreaData
{
    internal string betOption;
    internal Transform betAreaTarget;
    internal double betAmount;
    internal double winAmount;
    internal double winRatio; // winAmount / betAmount
}