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
    [SerializeField] private RectTransform chipContainer;

    [Header("Pool")]
    [SerializeField] private int dealerPoolSize = 25;
    [SerializeField] private float dealerScatterX = 28f;
    [SerializeField] private float dealerScatterY = 18f;

    [Header("Phase 1 — Win Image + Non-Win Fade")]
    [Tooltip("How long non-winning player bet components take to fade/clear.")]
    [SerializeField] private float losingAreaClearDuration = 0.25f;

    [Header("Phase 2 — Dealer to Bet Area (player win chips)")]
    [SerializeField] private float dealerToBetDuration = 0.65f;
    [SerializeField] private float betAreaScatterX = 11f;
    [SerializeField] private float betAreaScatterY = 9f;

    [Header("Phase 3 — Post-Land Wait")]
    [Tooltip("Pause after all win chips have landed before the cashout sweep begins.")]
    [SerializeField] private float postLandWait = 0.5f;

    [Header("Phase 4 — Bet Area to Player (cashout arc)")]
    [SerializeField] private float betToPlayerDuration = 0.75f;
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

    // -------------------------------------------------------------------------
    // Internal Sync Properties  (read by OpponentChipManager to stay in lockstep)
    // -------------------------------------------------------------------------
    #region Internal Sync Properties

    /// <summary>
    /// Total seconds from PlayDiceResultAnimation() call until dealer chips
    /// start flying toward bet areas.  OpponentChipManager waits this long
    /// before spawning its own win chips so both sets leave the dealer at the
    /// same frame.
    /// </summary>
    internal float PreFlightDelay => losingAreaClearDuration;

    /// <summary>
    /// How long the dealer→bet-area flight takes.  Opponent chips use the same
    /// value so they land at the same time as player chips.
    /// </summary>
    internal float DealerToBetDuration => dealerToBetDuration;

    /// <summary>
    /// Pause after landing before the cashout sweep.  Opponent chips use the
    /// same value so the sweeps start simultaneously.
    /// </summary>
    internal float PostLandWait => postLandWait;

    /// <summary>
    /// Duration of the bet-area → player arc sweep (cashout).
    /// </summary>
    internal float BetToPlayerDuration => betToPlayerDuration;
    #endregion

    #region Private Fields
    private readonly List<(RectTransform rt, Chip chip)> dealerPool = new List<(RectTransform, Chip)>();
    private readonly List<RectTransform> activeWinChips = new List<RectTransform>();
    private readonly List<RectTransform> stakeReturnChips = new List<RectTransform>();
    private bool isAnimating;
    private Coroutine winCoroutine;
    private Coroutine cashoutCoroutine;

    private readonly List<WinAreaData> _recalcCache = new List<WinAreaData>();
    private readonly HashSet<PlayerBetComponent> _winAreaComponents = new HashSet<PlayerBetComponent>();
    private Tween _clearWinCompsTween;
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
        if (isAnimating)
        {
            Debug.LogWarning("[ChipWinAnim] PlayDiceResultAnimation SKIPPED: Animation is already running (isAnimating=true).");
            return;
        }
        if (winAreas == null || winAreas.Count == 0)
        {
            Debug.Log($"[ChipWinAnim] PlayDiceResultAnimation SKIPPED: winAreas is null or empty (Count: {winAreas?.Count ?? 0}). Player has no winning bets on this round.");
            return;
        }
        if (diceResult == null)
        {
            Debug.LogWarning("[ChipWinAnim] PlayDiceResultAnimation SKIPPED: diceResult is null.");
            return;
        }

        Debug.Log($"[ChipWinAnim] PlayDiceResultAnimation START: Processing {winAreas.Count} winning area(s) for player.");
        if (winCoroutine != null) StopCoroutine(winCoroutine);
        var recalculated = RecalculateWinAmounts(winAreas, diceResult);
        Debug.Log($"[ChipWinAnim] RecalculateWinAmounts produced {recalculated.Count} area(s) with actual win > 0.");
        winCoroutine = StartCoroutine(CR_DealerToBetAreas(recalculated));
    }

    internal void PlayCashoutAnimation()
    {
        if (playerNameTarget == null)
        {
            Debug.LogError("[ChipWinAnim] PlayCashoutAnimation BLOCKED: playerNameTarget is NULL in Inspector! Assign Player Header RectTransform.");
            return;
        }
        if (activeWinChips.Count == 0 && stakeReturnChips.Count == 0)
        {
            Debug.Log($"[ChipWinAnim] PlayCashoutAnimation SKIPPED: No win chips ({activeWinChips.Count}) or stake return chips ({stakeReturnChips.Count}) to sweep.");
            return;
        }

        Debug.Log($"[ChipWinAnim] PlayCashoutAnimation START: Sweeping {activeWinChips.Count} win chip(s) and {stakeReturnChips.Count} stake return chip(s) to player target ({playerNameTarget.name}).");
        if (cashoutCoroutine != null) StopCoroutine(cashoutCoroutine);
        cashoutCoroutine = StartCoroutine(CR_Cashout());
    }

    internal void PlayRefundAnimation(Dictionary<string, double> refundBets, bool clearComponentsAfter = true)
    {
        if (refundBets == null || refundBets.Count == 0)
        {
            Debug.Log("[ChipWinAnim] PlayRefundAnimation SKIPPED: refundBets is null or empty.");
            return;
        }

        Debug.Log($"[ChipWinAnim] PlayRefundAnimation START: Refunding {refundBets.Count} bet area(s) (clearComponentsAfter={clearComponentsAfter}).");
        StartCoroutine(CR_RefundChips(refundBets, clearComponentsAfter));
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

        _clearWinCompsTween?.Kill();
        _clearWinCompsTween = null;

        activeWinChips.Clear();
        stakeReturnChips.Clear();
        _winAreaComponents.Clear();
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
        return new List<WinAreaData>(_recalcCache);
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

    // =========================================================================
    //  PHASE SEQUENCE
    //  ─────────────────────────────────────────────────────────────────────────
    //  PHASE 1  (instant)   – Win images appear on winning areas.
    //                         Non-winning player bet components clear/fade.
    //  PHASE 2  (wait losingAreaClearDuration)
    //                       – Dealer win-chips fly to winning bet areas.
    //                         OpponentChipManager starts its own dealer chips at
    //                         exactly the same moment (it reads PreFlightDelay).
    //  PHASE 3  (wait dealerToBetDuration) – chips have landed.
    //  PHASE 4  (wait postLandWait) – brief configurable pause.
    //  PHASE 5  (cashout)   – All chips sweep from bet areas → player in classic
    //                         casino arc.  PlayCashoutAnimation() is called
    //                         externally (by GameManager.CR_CashoutFlow) but the
    //                         timing lines up because OpponentChipManager reads
    //                         PostLandWait and DealerToBetDuration.
    // =========================================================================
    #region Dealer → Bet Areas
    private IEnumerator CR_DealerToBetAreas(List<WinAreaData> winAreas)
    {
        isAnimating = true;
        _winAreaComponents.Clear();

        List<double> chipValues = betController != null ? betController.GetChipValues() : new List<double>();
        Sprite[] chipSprites = betController != null ? betController.GetChipSprites() : null;

        // ── PHASE 1: show win images, clear losing player bet areas immediately ──
        FadeOutLosingAreaChips(winAreas);

        // Prepare chip assignments (dealer → bet area targets).
        // We do this during the Phase-1 window so chips are ready to fly the
        // moment the wait ends.
        var assignments = new List<(RectTransform rt, Transform parent, Vector3 localPos)>();
        int poolIdx = 0;

        foreach (var area in winAreas)
        {
            if (area.betAreaTarget == null) continue;

            PlayerBetComponent playerBetComp = betController?.GetPlayerBetComponent(area.betOption);
            if (playerBetComp == null) continue;
            _winAreaComponents.Add(playerBetComp);

            Transform chipParent = playerBetComp.transform;
            AudioManager.Instance?.PlayChipAdd();
            var winCombination = BuildCombination(area.winAmount, chipValues, chipSprites);
            int winChipCount = Mathf.Clamp(winCombination.Count, minChipsPerWin, maxChipsPerWin);

            // Win chips
            for (int i = 0; i < winChipCount && poolIdx < dealerPool.Count; i++, poolIdx++)
            {
                var (rt, chip) = dealerPool[poolIdx];
                if (rt == null) continue;

                ApplyChipVisual(chip, winCombination, i, chipSprites);

                rt.gameObject.SetActive(true);
                rt.localPosition = new Vector3(
                    Random.Range(-dealerScatterX, dealerScatterX),
                    Random.Range(-dealerScatterY, dealerScatterY), 0f);
                rt.localScale = Vector3.zero;
                // Pop in while we wait — chips are ready when flight begins
                rt.DOScale(chipWorkingScale, 0.18f).SetEase(Ease.OutBack);

                Vector3 localPos = new Vector3(
                    Random.Range(-betAreaScatterX, betAreaScatterX),
                    Random.Range(-betAreaScatterY, betAreaScatterY), 0f);

                assignments.Add((rt, chipParent, localPos));
                activeWinChips.Add(rt);
            }

            // Stake-return chips (sit hidden in the bet area, revealed on cashout)
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

        // ── PHASE 2: wait for the losing-area clear to finish, then launch chips ──
        yield return new WaitForSeconds(losingAreaClearDuration);

        // Resolve world-space targets now (parent transforms are settled)
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

        // Trigger win-amount counting animation partway through the flight
        if (enableWinAnimations && assignments.Count > 0)
        {
            DOVirtual.DelayedCall(
                dealerToBetDuration * animationStartPercent,
                () => TriggerAllWinCountingAnimations(winAreas));
        }

        // Launch ALL chips simultaneously
        int totalChips = animData.Count;
        int landedChipCount = 0;
        var capturedWinAreas = winAreas;

        foreach (var (rt, worldTarget) in animData)
        {
            if (rt == null) { totalChips--; continue; }

            RectTransform capturedRT = rt;
            Vector3 capturedTarget = worldTarget;

            capturedRT.DOMove(capturedTarget, dealerToBetDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    if (capturedRT != null)
                        capturedRT.localPosition = capturedRT.parent.InverseTransformPoint(capturedTarget);

                    landedChipCount++;
                    // Nothing extra needed on land — cashout is triggered externally
                    // via PlayCashoutAnimation() after the postLandWait.
                });
        }

        if (totalChips <= 0)
        {
            // No chips to animate — nothing to do
        }

        // ── PHASE 3: wait for flight to complete ──
        yield return new WaitForSeconds(dealerToBetDuration);

        isAnimating = false;
        winCoroutine = null;
    }
    #endregion

    #region Bet Areas → Player (Cashout arc)
    private IEnumerator CR_Cashout()
    {
        if (playerNameTarget == null) yield break;

        // Reveal stake-return chips with a little pop
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

        // One frame so stake chips are positioned before we read their canvas pos
        yield return null;

        // Snapshot canvas positions BEFORE re-parenting
        var chipCanvasPositions = new Dictionary<RectTransform, Vector2>();
        foreach (var rt in toSweep)
        {
            if (rt == null) continue;
            chipCanvasPositions[rt] = GetCanvasPosition(rt);
        }

        // Move all chips to the flight parent (canvas root or chipContainer)
        Transform flightParent = chipContainer != null
            ? (Transform)chipContainer
            : targetCanvas.transform;

        foreach (var rt in toSweep)
        {
            if (rt == null) continue;
            if (rt.parent != flightParent)
            {
                rt.SetParent(flightParent, worldPositionStays: false);
                if (chipCanvasPositions.ContainsKey(rt))
                    rt.anchoredPosition = chipCanvasPositions[rt];
            }
        }

        // Clear winning PlayerBetComponents mid-arc (halfway up) so it feels
        // like the chips "take" the displayed amount with them
        float halfDur = betToPlayerDuration * 0.45f;
        float landDur = betToPlayerDuration * 0.55f;
        float clearDelay = halfDur * 0.5f;

        var winCompsToClean = new List<PlayerBetComponent>(_winAreaComponents);
        _winAreaComponents.Clear();

        if (winCompsToClean.Count > 0)
        {
            _clearWinCompsTween?.Kill();
            _clearWinCompsTween = DOVirtual.DelayedCall(clearDelay, () =>
            {
                foreach (var comp in winCompsToClean)
                    if (comp != null) comp.Clear();
                _clearWinCompsTween = null;
            });
        }

        // ── Classic casino arc: ALL chips start flying at the same time ──
        Vector2 playerCanvasPos = GetCanvasPosition(playerNameTarget);

        foreach (var rt in toSweep)
        {
            if (rt == null) continue;

            RectTransform capturedRT = rt;
            Vector2 startPos = capturedRT.anchoredPosition;
            // Arc peak: halfway between start and target, lifted by arcHeight
            Vector2 midPos = Vector2.Lerp(startPos, playerCanvasPos, 0.5f)
                             + new Vector2(Random.Range(-18f, 18f), arcHeight);

            DOTween.Sequence()
                .Append(capturedRT.DOAnchorPos(midPos, halfDur).SetEase(Ease.OutQuad))
                .Append(capturedRT.DOAnchorPos(playerCanvasPos, landDur).SetEase(Ease.InQuad))
                .Join(capturedRT.DOScale(Vector3.zero, landDur).SetEase(Ease.InBack))
                .OnComplete(() =>
                {
                    if (capturedRT == null) return;
                    AudioManager.Instance?.PlayChipAdd();
                    capturedRT.gameObject.SetActive(false);
                    capturedRT.SetParent(dealerSpawnPoint, worldPositionStays: false);
                    capturedRT.localPosition = Vector3.zero;
                });
        }

        yield return new WaitForSeconds(betToPlayerDuration + 0.25f);

        activeWinChips.Clear();
        stakeReturnChips.Clear();
        cashoutCoroutine = null;
    }
    #endregion

    #region Refund
    private IEnumerator CR_RefundChips(Dictionary<string, double> refundBets, bool clearComponentsAfter = true)
    {
        if (playerNameTarget == null || betController == null)
        {
            Debug.LogError("[ChipWinAnim] playerNameTarget or betController is null!");
            yield break;
        }

        Sprite[] chipSprites = betController.GetChipSprites();
        List<double> chipValues = betController.GetChipValues();

        if (chipSprites == null || chipSprites.Length == 0)
        {
            Debug.LogError("[ChipWinAnim] chipSprites is null or empty!");
            yield break;
        }

        var refundData = new List<(PlayerBetComponent component, Transform betArea, List<ChipCombinationItem> chips)>();

        foreach (var kvp in refundBets)
        {
            string betOption = kvp.Key;
            double betAmount = kvp.Value;

            PlayerBetComponent comp = betController.GetPlayerBetComponent(betOption);
            if (comp == null) continue;

            List<ChipCombinationItem> chipsForArea;
            if (clearComponentsAfter)
            {
                List<BetData> betDataList = comp.GetBetData();
                chipsForArea = new List<ChipCombinationItem>(betDataList.Count);
                foreach (var bd in betDataList)
                    chipsForArea.Add(new ChipCombinationItem { amount = bd.amount, chipIndex = bd.chipIndex });
                if (chipsForArea.Count == 0)
                    chipsForArea = BuildCombination(betAmount > 0 ? betAmount : 1, chipValues, chipSprites);
            }
            else
            {
                chipsForArea = BuildCombination(betAmount > 0 ? betAmount : 1, chipValues, chipSprites);
            }

            refundData.Add((comp, comp.transform, chipsForArea));
        }

        if (refundData.Count == 0) yield break;

        List<RectTransform> chipsToAnimate = new List<RectTransform>();
        int poolIdx = 0;

        foreach (var (component, betArea, chipsForArea) in refundData)
        {
            if (betArea == null) continue;
            Vector2 betAreaCanvasPos = GetCanvasPosition(betArea as RectTransform);

            for (int i = 0; i < chipsForArea.Count; i++)
            {
                while (poolIdx < dealerPool.Count &&
                       dealerPool[poolIdx].rt != null &&
                       dealerPool[poolIdx].rt.gameObject.activeSelf)
                    poolIdx++;

                if (poolIdx >= dealerPool.Count) break;

                var (chipRT, chipComponent) = dealerPool[poolIdx];
                poolIdx++;

                if (chipRT == null || chipComponent == null) continue;

                ChipCombinationItem item = chipsForArea[i];
                int safeIdx = Mathf.Clamp(item.chipIndex, 0, chipSprites.Length - 1);
                chipComponent.SetData(chipSprites[safeIdx], GameUtilities.FormatCurrency(item.amount), safeIdx);

                chipRT.SetParent(targetCanvas.transform, worldPositionStays: false);
                chipRT.anchoredPosition = betAreaCanvasPos + new Vector2(
                    Random.Range(-betAreaScatterX, betAreaScatterX),
                    Random.Range(-betAreaScatterY, betAreaScatterY));
                chipRT.localScale = Vector3.one * chipWorkingScale;
                chipRT.gameObject.SetActive(true);

                chipsToAnimate.Add(chipRT);
            }
        }

        if (clearComponentsAfter)
            foreach (var (component, _, _) in refundData)
                if (component != null) component.Clear();

        yield return null; // one frame for clear to settle

        if (chipsToAnimate.Count == 0) yield break;

        Vector2 playerCanvasPos = GetCanvasPosition(playerNameTarget);
        float halfDur = betToPlayerDuration * 0.45f;
        float landDur = betToPlayerDuration * 0.55f;

        // All refund chips fly simultaneously in the same casino arc
        foreach (var chipRT in chipsToAnimate)
        {
            if (chipRT == null) continue;

            RectTransform capturedRT = chipRT;
            Vector2 startPos = capturedRT.anchoredPosition;
            Vector2 midPos = Vector2.Lerp(startPos, playerCanvasPos, 0.5f)
                               + new Vector2(Random.Range(-18f, 18f), arcHeight * 0.8f);

            DOTween.Sequence()
                .Append(capturedRT.DOAnchorPos(midPos, halfDur).SetEase(Ease.OutQuad))
                .Append(capturedRT.DOAnchorPos(playerCanvasPos, landDur).SetEase(Ease.InQuad))
                .Join(capturedRT.DOScale(Vector3.zero, landDur).SetEase(Ease.InBack))
                .OnComplete(() =>
                {
                    if (capturedRT == null) return;
                    AudioManager.Instance?.PlayChipAdd();
                    capturedRT.gameObject.SetActive(false);
                    capturedRT.SetParent(dealerSpawnPoint, worldPositionStays: false);
                    capturedRT.localPosition = new Vector3(
                        Random.Range(-dealerScatterX, dealerScatterX),
                        Random.Range(-dealerScatterY, dealerScatterY), 0f);
                    capturedRT.localScale = Vector3.zero;
                });
        }

        yield return new WaitForSeconds(betToPlayerDuration);
    }
    #endregion

    #region Helpers
    private void FadeOutLosingAreaChips(List<WinAreaData> winAreas)
    {
        if (betController == null) return;

        var winningOptions = new HashSet<string>();
        foreach (var w in winAreas) winningOptions.Add(w.betOption);

        var allOptions = betController.GetAllBetOptions();
        if (allOptions == null) return;

        foreach (var option in allOptions)
        {
            if (winningOptions.Contains(option)) continue;

            PlayerBetComponent comp = betController.GetPlayerBetComponent(option);
            if (comp == null || !comp.HasBets()) continue;

            comp.Clear();
        }
    }

    private void TriggerAllWinCountingAnimations(List<WinAreaData> winAreas)
    {
        if (betController == null) return;
        foreach (var winArea in winAreas)
        {
            PlayerBetComponent comp = betController.GetPlayerBetComponent(winArea.betOption);
            if (comp == null || winArea.betAmount <= 0) continue;
            comp.AnimateWinWithRatio(winArea.winRatio);
        }
    }

    private int CalculateStakeReturnChipCount(double winRatio, double betAmount)
    {
        if (winRatio <= 0) return 0;
        double val = winRatio * betAmount;
        if (val >= 20) return 3;
        if (val >= 5) return 2;
        return 1;
    }

    private List<ChipCombinationItem> BuildCombination(double amount, List<double> chipValues, Sprite[] sprites)
    {
        if (chipValues != null && chipValues.Count > 0)
        {
            var combo = GameUtilities.FindChipCombination(amount, chipValues);
            if (combo != null && combo.Count > 0) return combo;
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
        chip.SetData(sprites[safeIdx], GameUtilities.FormatCurrency(item.amount), safeIdx);
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
    internal double winRatio;
}