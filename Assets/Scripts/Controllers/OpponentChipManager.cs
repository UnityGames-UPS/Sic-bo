using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class OpponentChipManager : MonoBehaviour
{
    #region Serialized Fields
    [Header("Dealer Areas")]
    [SerializeField] private RectTransform opponentDealerArea;

    [Tooltip("RectTransform on the player-count UI element. Non-leaderboard opponent winning chips fly here.")]
    [SerializeField] private RectTransform playerCountArea;

    [Header("Chip Spawning")]
    [SerializeField] private GameObject chipPrefab;
    [SerializeField] private Sprite grayChipSprite;

    [Header("Animation Settings")]
    [SerializeField] private float dealerToBetDuration = 0.65f;
    [SerializeField] private float cashoutDuration = 0.70f;
    [SerializeField] private float dealerScatterX = 20f;
    [SerializeField] private float dealerScatterY = 15f;
    [SerializeField] private float betAreaScatterX = 12f;
    [SerializeField] private float betAreaScatterY = 10f;
    [SerializeField] private float chipScale = 0.8f;

    [Tooltip("Arc height (canvas pixels) for the cashout sweep. Mirrors the player arc for visual consistency.")]
    [SerializeField] private float cashoutArcHeight = 90f;

    [Header("References")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private RectTransform chipContainer;
    [SerializeField] private LeaderboardController leaderboardController;

    [Tooltip("Must be assigned — used to read PreFlightDelay, DealerToBetDuration and PostLandWait so player and opponent chips stay in sync.")]
    [SerializeField] private ChipWinAnimationController chipWinAnimationController;
    #endregion

    #region Private Fields
    private Dictionary<string, RectTransform> opponentContainers = new Dictionary<string, RectTransform>();
    private List<RectTransform> activeOpponentChips = new List<RectTransform>();
    private Dictionary<string, List<RectTransform>> chipsByBetArea = new Dictionary<string, List<RectTransform>>();
    private Dictionary<RectTransform, string> chipToUsername = new Dictionary<RectTransform, string>();
    private Dictionary<RectTransform, RectTransform> chipToSpawnPosition = new Dictionary<RectTransform, RectTransform>();
    private Dictionary<RectTransform, BadgeState> chipToOriginalBadge = new Dictionary<RectTransform, BadgeState>();

    private bool isCashoutRunning = false;
    private Coroutine cashoutCoroutine = null;
    private Leaderboards currentLeaderboards = null;
    private Leaderboards lockedLeaderboards = null;
    private List<Payout> currentPayouts = null;
    private string localPlayerUsername = null;

    private HashSet<string> winningBetAreas = new HashSet<string>();
    private List<RectTransform> activeWinChips = new List<RectTransform>();
    private Coroutine winAnimationCoroutine = null;

    private readonly Dictionary<string, double> _betAmountsCache = new Dictionary<string, double>();
    private readonly HashSet<string> _processedPlayersCache = new HashSet<string>();
    private readonly Dictionary<string, HashSet<string>> _winnersByBetAreaCache = new Dictionary<string, HashSet<string>>();

    private readonly Queue<RectTransform> _chipPool = new Queue<RectTransform>();
    private const int CHIP_POOL_SIZE = 100;
    #endregion

    #region Helper Structs
    private struct BadgeState
    {
        public bool isRichest;
        public bool isWinner;
    }
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
    }

    private void Start()
    {
        if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
        InitializeChipPool();
    }

    private void OnDestroy()
    {
        foreach (var chip in activeOpponentChips) if (chip != null) chip.DOKill();
        foreach (var chip in activeWinChips) if (chip != null) chip.DOKill();

        while (_chipPool.Count > 0)
        {
            var chip = _chipPool.Dequeue();
            if (chip != null) Destroy(chip.gameObject);
        }
    }
    #endregion

    #region Chip Pool
    private void InitializeChipPool()
    {
        if (chipPrefab == null || opponentDealerArea == null) return;
        Transform poolParent = chipContainer != null ? chipContainer : opponentDealerArea;

        for (int i = 0; i < CHIP_POOL_SIZE; i++)
        {
            GameObject chipObj = Instantiate(chipPrefab, poolParent);
            RectTransform chipRT = chipObj.GetComponent<RectTransform>();
            chipObj.SetActive(false);
            _chipPool.Enqueue(chipRT);
        }
    }

    private RectTransform GetPooledChip()
    {
        if (_chipPool.Count > 0)
        {
            RectTransform chipRT = _chipPool.Dequeue();
            chipRT.gameObject.SetActive(true);
            return chipRT;
        }
        Transform parent = chipContainer != null ? chipContainer : opponentDealerArea;
        GameObject chipObj = Instantiate(chipPrefab, parent);
        return chipObj.GetComponent<RectTransform>();
    }

    private void ReturnChipToPool(RectTransform chipRT)
    {
        if (chipRT == null) return;
        chipRT.DOKill();

        Chip chipComponent = chipRT.GetComponent<Chip>();
        if (chipComponent != null) chipComponent.ClearLeaderboardBadge();

        chipRT.gameObject.SetActive(false);
        chipRT.localScale = Vector3.one;

        Transform parent = chipContainer != null ? chipContainer : opponentDealerArea;
        chipRT.SetParent(parent, false);

        if (_chipPool.Count < CHIP_POOL_SIZE * 2)
            _chipPool.Enqueue(chipRT);
        else
            Destroy(chipRT.gameObject);
    }
    #endregion

    #region Internal API
    internal void InitializeContainers(Dictionary<string, Transform> betAreaMap)
    {
        opponentContainers.Clear();

        foreach (var kvp in betAreaMap)
        {
            string betOption = kvp.Key;
            Transform betAreaTransform = kvp.Value;
            if (betAreaTransform == null) continue;

            GameObject containerObj = new GameObject($"OpponentChipContainer_{betOption}");
            RectTransform container = containerObj.AddComponent<RectTransform>();

            container.SetParent(betAreaTransform, false);
            container.anchorMin = Vector2.zero;
            container.anchorMax = Vector2.one;
            container.offsetMin = Vector2.zero;
            container.offsetMax = Vector2.zero;
            container.localScale = Vector3.one;
            container.pivot = new Vector2(0.5f, 0.5f);
            containerObj.SetActive(false);

            opponentContainers[betOption] = container;
            chipsByBetArea[betOption] = new List<RectTransform>();
        }
    }

    internal void SetLeaderboardData(Leaderboards leaderboards) => currentLeaderboards = leaderboards;
    internal void LockLeaderboardsForRound() => lockedLeaderboards = currentLeaderboards;
    internal void SetCashoutData(List<Payout> payouts) => currentPayouts = payouts;
    internal void SetLocalPlayerUsername(string username) => localPlayerUsername = username;

    internal void SetWinningBetAreas(List<string> winningAreas)
    {
        winningBetAreas.Clear();
        if (winningAreas != null)
            foreach (string area in winningAreas)
                winningBetAreas.Add(area);
    }

    internal void AddOpponentBet(string betOption, double amount, string username = "")
    {
        if (!opponentContainers.ContainsKey(betOption)) return;
        if (opponentDealerArea == null || chipPrefab == null || grayChipSprite == null) return;

        AudioManager.Instance?.PlayChipAdd();
        StartCoroutine(CR_SpawnAndAnimateChip(betOption, amount, username));
    }

    internal void AddJoinTimeBets(List<BetInfo> bets)
    {
        if (bets == null || bets.Count == 0) return;
        if (opponentDealerArea == null || chipPrefab == null || grayChipSprite == null) return;

        var betsByOptionAndUser = new Dictionary<string, Dictionary<string, double>>();

        foreach (var bet in bets)
        {
            if (string.IsNullOrEmpty(bet.betOption) || bet.amount <= 0 || string.IsNullOrEmpty(bet.username)) continue;
            if (bet.username == localPlayerUsername) continue;

            if (!betsByOptionAndUser.ContainsKey(bet.betOption))
                betsByOptionAndUser[bet.betOption] = new Dictionary<string, double>();

            if (!betsByOptionAndUser[bet.betOption].ContainsKey(bet.username))
                betsByOptionAndUser[bet.betOption][bet.username] = 0;

            betsByOptionAndUser[bet.betOption][bet.username] += bet.amount;
        }

        foreach (var betAreaKvp in betsByOptionAndUser)
            foreach (var userBet in betAreaKvp.Value)
                StartCoroutine(CR_SpawnJoinTimeChip(betAreaKvp.Key, userBet.Value, userBet.Key));
    }

    internal void ClearAllOpponentBets()
    {
        if (cashoutCoroutine != null)
        {
            StopCoroutine(cashoutCoroutine);
            cashoutCoroutine = null;
            isCashoutRunning = false;
        }
        if (winAnimationCoroutine != null)
        {
            StopCoroutine(winAnimationCoroutine);
            winAnimationCoroutine = null;
        }

        StopAllCoroutines();

        foreach (var chip in activeOpponentChips) if (chip != null) ReturnChipToPool(chip);
        foreach (var chip in activeWinChips) if (chip != null) ReturnChipToPool(chip);

        activeOpponentChips.Clear();
        activeWinChips.Clear();
        chipToUsername.Clear();
        chipToSpawnPosition.Clear();
        chipToOriginalBadge.Clear();
        winningBetAreas.Clear();
        lockedLeaderboards = null;

        foreach (var container in opponentContainers.Values)
            if (container != null) container.gameObject.SetActive(false);

        foreach (var list in chipsByBetArea.Values) list.Clear();

        isCashoutRunning = false;
    }

    /// <summary>
    /// Called by GameManager.ShowResultEffects() — starts the win chip
    /// animation sequence.  Timing is derived entirely from
    /// ChipWinAnimationController so player and opponent chips are in lockstep.
    /// </summary>
    internal void PlayOpponentWinAnimations()
    {
        if (winAnimationCoroutine != null) StopCoroutine(winAnimationCoroutine);
        winAnimationCoroutine = StartCoroutine(CR_OpponentWinAnimation());
    }

    internal void PlayCashoutAnimation()
    {
        if (isCashoutRunning || activeOpponentChips.Count == 0) return;
        if (winAnimationCoroutine != null)
            StartCoroutine(WaitForWinAnimationThenCashout());
        else
        {
            AudioManager.Instance?.PlayChipAdd();
            cashoutCoroutine = StartCoroutine(CR_Cashout());
        }
    }

    internal bool IsCashoutRunning() => isCashoutRunning;
    internal bool HasActiveChips() => activeOpponentChips.Count > 0 || activeWinChips.Count > 0;
    #endregion

    #region Opponent Win Animation
    // =========================================================================
    //  PHASE SEQUENCE  (mirrors ChipWinAnimationController exactly)
    //
    //  PHASE 1  (0 s)       – Non-winning opponent chips fade/scale out.
    //                         Win-images are already shown by BetController.
    //  PHASE 2  (wait PreFlightDelay)
    //                       – Dealer win-chips spawn and fly toward winning bet
    //                         areas at the SAME moment player chips do.
    //  PHASE 3  (wait DealerToBetDuration) – chips landed.
    //  PHASE 4  (wait PostLandWait) – brief pause.
    //  Cashout is then triggered externally via PlayCashoutAnimation().
    // =========================================================================
    private IEnumerator CR_OpponentWinAnimation()
    {
        // ── PHASE 1: fade non-winning opponent chips immediately ──
        FadeOutNonWinningChips();

        if (winningBetAreas.Count == 0)
        {
            winAnimationCoroutine = null;
            yield break;
        }

        // Read timing values from ChipWinAnimationController for exact sync
        float preFlightDelay = chipWinAnimationController != null ? chipWinAnimationController.PreFlightDelay : 0.25f;
        float dealerDuration = chipWinAnimationController != null ? chipWinAnimationController.DealerToBetDuration : dealerToBetDuration;

        // ── PHASE 2: wait same pre-flight delay as player chips ──
        yield return new WaitForSeconds(preFlightDelay);

        if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
        Transform canvasRoot = targetCanvas != null ? targetCanvas.transform : transform.root;

        // Aggregate bet amounts per player per area (for sizing win chips)
        _betAmountsCache.Clear();
        foreach (string betArea in winningBetAreas)
        {
            if (!chipsByBetArea.ContainsKey(betArea)) continue;
            foreach (var chip in chipsByBetArea[betArea])
            {
                if (chip == null) continue;
                string chipOwner = chipToUsername.ContainsKey(chip) ? chipToUsername[chip] : "";
                if (string.IsNullOrEmpty(chipOwner)) continue;

                Chip chipComponent = chip.GetComponent<Chip>();
                if (chipComponent?.chipText == null) continue;

                double betAmount = ParseFormattedCurrency(chipComponent.chipText.text);
                if (betAmount <= 0) continue;

                string key = $"{chipOwner}_{betArea}";
                if (!_betAmountsCache.ContainsKey(key)) _betAmountsCache[key] = 0;
                _betAmountsCache[key] += betAmount;
            }
        }

        // Spawn all win chips and launch them simultaneously
        foreach (string betArea in winningBetAreas)
        {
            if (!opponentContainers.ContainsKey(betArea)) continue;
            if (!chipsByBetArea.ContainsKey(betArea)) continue;

            RectTransform targetContainer = opponentContainers[betArea];
            if (chipsByBetArea[betArea].Count == 0) continue;

            _processedPlayersCache.Clear();

            foreach (var chip in chipsByBetArea[betArea])
            {
                if (chip == null) continue;
                string chipOwner = chipToUsername.ContainsKey(chip) ? chipToUsername[chip] : "";
                if (chipOwner == localPlayerUsername) continue;
                if (_processedPlayersCache.Contains(chipOwner)) continue;
                _processedPlayersCache.Add(chipOwner);

                string key = $"{chipOwner}_{betArea}";
                double totalBetAmount = _betAmountsCache.ContainsKey(key) ? _betAmountsCache[key] : 0;
                int winChipCount = Random.Range(2, 4);
                double estimatedWinPerChip = totalBetAmount > 0 ? totalBetAmount * 2.0 / winChipCount : 0;

                for (int i = 0; i < winChipCount; i++)
                {
                    RectTransform winChipRT = GetPooledChip();
                    winChipRT.SetParent(opponentDealerArea, false);

                    Chip winChip = winChipRT.GetComponent<Chip>();
                    if (winChip == null || winChipRT == null)
                    {
                        ReturnChipToPool(winChipRT);
                        continue;
                    }

                    winChip.SetSprite(grayChipSprite);
                    winChip.SetActive(true);
                    winChip.ClearLeaderboardBadge();
                    winChip.SetAmount(estimatedWinPerChip > 0 ? GameUtilities.FormatCurrency(estimatedWinPerChip) : "");

                    winChipRT.localPosition = new Vector3(
                        Random.Range(-dealerScatterX, dealerScatterX),
                        Random.Range(-dealerScatterY, dealerScatterY), 0f);
                    winChipRT.localScale = Vector3.zero;

                    // Pop in — same 0.18 s window as player chips
                    winChipRT.DOScale(chipScale * 0.9f, 0.18f).SetEase(Ease.OutBack);

                    // Move to bet area immediately (same animation as player chips)
                    StartCoroutine(CR_AnimateWinChipToTarget(winChipRT, targetContainer, canvasRoot, dealerDuration));

                    activeWinChips.Add(winChipRT);
                }
            }
        }

        // ── PHASE 3: wait for flight to complete ──
        yield return new WaitForSeconds(dealerDuration);

        winAnimationCoroutine = null;
    }

    /// <summary>
    /// Fades out chips sitting on non-winning bet areas.  Called at the very
    /// start of the result sequence (Phase 1), simultaneously with win images
    /// appearing and player bet components clearing.
    /// </summary>
    private void FadeOutNonWinningChips()
    {
        foreach (var kvp in chipsByBetArea)
        {
            string betArea = kvp.Key;
            bool isWinArea = winningBetAreas.Contains(betArea);
            if (isWinArea) continue;

            foreach (var chip in kvp.Value)
            {
                if (chip == null) continue;
                RectTransform capturedChip = chip;
                chip.DOScale(0f, 0.25f)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => { if (capturedChip != null) ReturnChipToPool(capturedChip); });
            }
            kvp.Value.Clear();
        }
    }

    private IEnumerator CR_AnimateWinChipToTarget(
        RectTransform winChipRT,
        RectTransform targetContainer,
        Transform canvasRoot,
        float duration)
    {
        if (winChipRT == null || targetContainer == null) yield break;

        winChipRT.SetParent(canvasRoot, worldPositionStays: true);

        float winCanvasScale = targetCanvas != null ? targetCanvas.transform.lossyScale.x : 1f;
        Vector3 winTargetWorldPos = targetContainer.position + new Vector3(
            Random.Range(-betAreaScatterX, betAreaScatterX) * winCanvasScale,
            Random.Range(-betAreaScatterY, betAreaScatterY) * winCanvasScale,
            0f);

        winChipRT.DOMove(winTargetWorldPos, duration * 0.8f).SetEase(Ease.OutQuad);
    }

    private IEnumerator WaitForWinAnimationThenCashout()
    {
        yield return winAnimationCoroutine;
        AudioManager.Instance?.PlayChipAdd();
        cashoutCoroutine = StartCoroutine(CR_Cashout());
    }
    #endregion

    #region Join-time chip spawn (no animation)
    private IEnumerator CR_SpawnJoinTimeChip(string betOption, double amount, string username)
    {
        if (!opponentContainers.ContainsKey(betOption)) yield break;

        RectTransform targetContainer = opponentContainers[betOption];
        if (targetContainer == null) yield break;

        RectTransform chipRT = GetPooledChip();
        if (chipRT == null) yield break;

        Chip chipComponent = chipRT.GetComponent<Chip>();
        if (chipComponent != null)
        {
            chipComponent.SetData(grayChipSprite, GameUtilities.FormatCurrency(amount), 0);

            if (lockedLeaderboards != null && !string.IsNullOrEmpty(username))
            {
                bool isRichest = IsPlayerInFirst(username, lockedLeaderboards.richest);
                bool isWinner = IsPlayerInFirst(username, lockedLeaderboards.winners);
                chipComponent.SetLeaderboardBadge(isRichest, isWinner);
            }
        }

        chipRT.SetParent(targetContainer, false);
        chipRT.localScale = Vector3.one * chipScale;
        chipRT.anchoredPosition = new Vector2(
            Random.Range(-betAreaScatterX, betAreaScatterX),
            Random.Range(-betAreaScatterY, betAreaScatterY));

        activeOpponentChips.Add(chipRT);
        chipToUsername[chipRT] = username;

        if (chipsByBetArea.ContainsKey(betOption))
            chipsByBetArea[betOption].Add(chipRT);

        // Look up the actual leaderboard slot so cashout can fly this chip back
        // to the correct player position. opponentDealerArea is never stored here
        // intentionally — that would make the chip fly to dealer on win.
        RectTransform joinSpawnPos = ResolveLeaderboardPosition(username);
        chipToSpawnPosition[chipRT] = joinSpawnPos != null ? joinSpawnPos : null;

        if (chipComponent != null)
        {
            chipToOriginalBadge[chipRT] = new BadgeState
            {
                isRichest = chipComponent.HasRichestBadge(),
                isWinner = chipComponent.HasWinnerBadge()
            };
        }

        targetContainer.gameObject.SetActive(true);
        yield return null;
    }
    #endregion

    #region Live bet spawn (animated fly-in)
    private IEnumerator CR_SpawnAndAnimateChip(string betOption, double amount, string username = "")
    {
        RectTransform container = opponentContainers[betOption];

        if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
        Transform canvasRoot = targetCanvas != null ? targetCanvas.transform : transform.root;

        bool spawnedFromWinners = false;
        bool spawnedFromRichest = false;
        RectTransform spawnPosition = GetSpawnPositionForPlayer(username, out spawnedFromWinners, out spawnedFromRichest);
        if (spawnPosition == null) spawnPosition = opponentDealerArea;

        RectTransform chipRT = GetPooledChip();
        chipRT.SetParent(spawnPosition, false);
        Chip chip = chipRT.GetComponent<Chip>();

        if (chip == null || chipRT == null) { ReturnChipToPool(chipRT); yield break; }

        chip.SetSprite(grayChipSprite);
        chip.SetAmount(GameUtilities.FormatCurrency(amount));
        chip.SetActive(true);

        bool showRichestBadge = false;
        bool showWinnerBadge = false;

        if (lockedLeaderboards != null && !string.IsNullOrEmpty(username))
        {
            bool opponentIsFirstRichest = IsPlayerInFirst(username, lockedLeaderboards.richest);
            bool opponentIsFirstWinner = IsPlayerInFirst(username, lockedLeaderboards.winners);

            showRichestBadge = spawnedFromRichest && opponentIsFirstRichest;
            showWinnerBadge = spawnedFromWinners && opponentIsFirstWinner;

            if (showRichestBadge && showWinnerBadge) showWinnerBadge = false;
            if (username == localPlayerUsername) { showRichestBadge = false; showWinnerBadge = false; }
        }

        chip.SetLeaderboardBadge(showRichestBadge, showWinnerBadge);
        chipToOriginalBadge[chipRT] = new BadgeState { isRichest = showRichestBadge, isWinner = showWinnerBadge };

        float scatterX = dealerScatterX, scatterY = dealerScatterY;
        if (spawnPosition != opponentDealerArea && spawnPosition != null)
        {
            Rect spawnRect = spawnPosition.rect;
            scatterX = Mathf.Min(spawnRect.width * 0.4f, 15f);
            scatterY = Mathf.Min(spawnRect.height * 0.4f, 10f);
        }

        chipRT.localPosition = new Vector3(
            Random.Range(-scatterX, scatterX),
            Random.Range(-scatterY, scatterY), 0f);
        chipRT.localScale = Vector3.zero;
        container.gameObject.SetActive(true);

        chipRT.DOScale(chipScale, 0.2f).SetEase(Ease.OutBack);
        yield return new WaitForSeconds(0.22f);

        chipRT.SetParent(canvasRoot, worldPositionStays: true);

        float canvasScale = targetCanvas != null ? targetCanvas.transform.lossyScale.x : 1f;
        Vector3 targetWorldPos = container.position + new Vector3(
            Random.Range(-betAreaScatterX, betAreaScatterX) * canvasScale,
            Random.Range(-betAreaScatterY, betAreaScatterY) * canvasScale,
            0f);

        chipRT.DOMove(targetWorldPos, dealerToBetDuration).SetEase(Ease.OutQuad);
        yield return new WaitForSeconds(dealerToBetDuration);
        chipRT.SetParent(container, worldPositionStays: true);

        activeOpponentChips.Add(chipRT);
        chipsByBetArea[betOption].Add(chipRT);
        if (!string.IsNullOrEmpty(username)) chipToUsername[chipRT] = username;
        chipToSpawnPosition[chipRT] = spawnPosition;
    }
    #endregion

    #region Cashout
    private IEnumerator CR_Cashout()
    {
        isCashoutRunning = true;

        if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
        Transform canvasRoot = targetCanvas != null ? targetCanvas.transform : transform.root;

        // Build winner set (chips on winning bet areas whose owner received a payout)
        _winnersByBetAreaCache.Clear();
        if (currentPayouts != null)
        {
            foreach (var payout in currentPayouts)
            {
                if (payout.win <= 0 || string.IsNullOrEmpty(payout.username)) continue;
                foreach (var kvp in chipsByBetArea)
                {
                    string betArea = kvp.Key;
                    if (!winningBetAreas.Contains(betArea)) continue;
                    foreach (var chip in kvp.Value)
                    {
                        if (chipToUsername.ContainsKey(chip) && chipToUsername[chip] == payout.username)
                        {
                            if (!_winnersByBetAreaCache.ContainsKey(betArea))
                                _winnersByBetAreaCache[betArea] = new HashSet<string>();
                            _winnersByBetAreaCache[betArea].Add(payout.username);
                            break;
                        }
                    }
                }
            }
        }

        var losingChips = new List<RectTransform>();
        var winningChips = new List<RectTransform>();
        // Chips on a winning area but whose owner has no payout entry (or no username)
        // still need to fly somewhere visible — playerCountArea, not scale-out.
        var unknownWinningChips = new List<RectTransform>();

        foreach (var kvp in chipsByBetArea)
        {
            string betArea = kvp.Key;
            bool isWinningArea = winningBetAreas.Contains(betArea);

            foreach (var chip in kvp.Value)
            {
                if (chip == null) continue;
                chip.SetParent(canvasRoot, worldPositionStays: true);

                string chipOwner = chipToUsername.ContainsKey(chip) ? chipToUsername[chip] : "";

                if (!isWinningArea)
                {
                    // Definitely a losing chip — scale out
                    losingChips.Add(chip);
                    continue;
                }

                // Chip is on a winning area.
                bool hasUsername = !string.IsNullOrEmpty(chipOwner);
                bool ownerInPayout = hasUsername &&
                                     _winnersByBetAreaCache.ContainsKey(betArea) &&
                                     _winnersByBetAreaCache[betArea].Contains(chipOwner);

                if (ownerInPayout)
                    winningChips.Add(chip);          // known winner → leaderboard slot
                else
                    unknownWinningChips.Add(chip);   // winning area, but no payout confirmed
                                                     // (no payout data, empty username, etc.)
                                                     // → playerCountArea
            }
        }

        // Clear badges so chips look anonymous during flight
        foreach (var chip in losingChips) chip?.GetComponent<Chip>()?.ClearLeaderboardBadge();
        foreach (var chip in activeWinChips) chip?.GetComponent<Chip>()?.ClearLeaderboardBadge();
        foreach (var chip in unknownWinningChips) chip?.GetComponent<Chip>()?.ClearLeaderboardBadge();

        // ── Losing chips: scale out ──
        float fadeDuration = cashoutDuration * 0.4f;
        foreach (var chip in losingChips)
        {
            if (chip == null) continue;
            RectTransform capturedChip = chip;
            chip.DOScale(0f, fadeDuration)
                .SetEase(Ease.InBack)
                .OnComplete(() => { if (capturedChip != null) ReturnChipToPool(capturedChip); });
        }

        // Small pause so losing chips vanish before the sweep starts
        yield return new WaitForSeconds(fadeDuration + 0.1f);

        // ── Winning chips (original bet chips on winning areas): arc to leaderboard/playerCount ──
        // ── Win chips (dealer-spawned): arc to opponentDealerArea ──
        // Both groups start flying simultaneously for a classic casino sweep feel.

        float halfDur = cashoutDuration * 0.45f;
        float landDur = cashoutDuration * 0.55f;

        // Winning bet-area chips → their leaderboard/player-count slot
        foreach (var chip in winningChips)
        {
            if (chip == null) continue;

            RectTransform targetPosition = GetCashoutDestinationForWinner(chip);

            // Tighten scatter for small leaderboard slots; keep default for playerCountArea
            float scatterX = dealerScatterX, scatterY = dealerScatterY;
            if (targetPosition != playerCountArea)
            {
                Rect targetRect = targetPosition.rect;
                scatterX = Mathf.Min(targetRect.width * 0.4f, 15f);
                scatterY = Mathf.Min(targetRect.height * 0.4f, 10f);
            }

            RectTransform capturedChip = chip;
            Vector2 startPos = GetCanvasPosition(chip);
            Vector2 targetPos = GetCanvasPosition(targetPosition);
            Vector2 midPos = Vector2.Lerp(startPos, targetPos, 0.5f)
                                  + new Vector2(Random.Range(-12f, 12f), cashoutArcHeight);

            // Use anchoredPosition-based arc so it respects canvas space
            DOTween.Sequence()
                .Append(capturedChip.DOAnchorPos(midPos, halfDur).SetEase(Ease.OutQuad))
                .Append(capturedChip.DOAnchorPos(targetPos, landDur).SetEase(Ease.InQuad))
                .Join(capturedChip.DOScale(0f, landDur).SetEase(Ease.InBack))
                .OnComplete(() => { if (capturedChip != null) ReturnChipToPool(capturedChip); });
        }

        // Unknown-winner chips (winning area, but no confirmed payout / no username)
        // → playerCountArea. These must never scale out or go to dealer.
        RectTransform fallbackTarget = playerCountArea;
        foreach (var chip in unknownWinningChips)
        {
            if (chip == null) continue;
            RectTransform capturedChip = chip;
            Vector2 startPos = GetCanvasPosition(chip);
            Vector2 targetPos = GetCanvasPosition(fallbackTarget);
            Vector2 midPos = Vector2.Lerp(startPos, targetPos, 0.5f)
                                + new Vector2(Random.Range(-12f, 12f), cashoutArcHeight);

            DOTween.Sequence()
                .Append(capturedChip.DOAnchorPos(midPos, halfDur).SetEase(Ease.OutQuad))
                .Append(capturedChip.DOAnchorPos(targetPos, landDur).SetEase(Ease.InQuad))
                .Join(capturedChip.DOScale(0f, landDur).SetEase(Ease.InBack))
                .OnComplete(() => { if (capturedChip != null) ReturnChipToPool(capturedChip); });
        }

        // Dealer win chips (anonymous, no username) → playerCountArea
        // They must never fly back to opponentDealerArea — always playerCountArea.
        RectTransform winChipTarget = playerCountArea;
        float dealerHalfDur = cashoutDuration * 0.35f;
        float dealerLandDur = cashoutDuration * 0.35f;

        foreach (var winChip in activeWinChips)
        {
            if (winChip == null) continue;
            winChip.SetParent(canvasRoot, worldPositionStays: true);

            RectTransform capturedWinChip = winChip;
            Vector2 startPos = GetCanvasPosition(winChip);
            Vector2 targetPos = GetCanvasPosition(winChipTarget);
            Vector2 midPos = Vector2.Lerp(startPos, targetPos, 0.5f)
                                + new Vector2(Random.Range(-18f, 18f), cashoutArcHeight * 1.1f);

            DOTween.Sequence()
                .Append(capturedWinChip.DOAnchorPos(midPos, dealerHalfDur).SetEase(Ease.OutQuad))
                .Append(capturedWinChip.DOAnchorPos(targetPos, dealerLandDur).SetEase(Ease.InQuad))
                .Join(capturedWinChip.DOScale(0f, dealerLandDur).SetEase(Ease.InBack))
                .OnComplete(() => { if (capturedWinChip != null) ReturnChipToPool(capturedWinChip); });
        }

        yield return new WaitForSeconds(cashoutDuration);

        // Cleanup
        activeOpponentChips.Clear();
        activeWinChips.Clear();
        chipToSpawnPosition.Clear();
        chipToOriginalBadge.Clear();
        chipToUsername.Clear();
        winningBetAreas.Clear();
        currentPayouts = null;
        lockedLeaderboards = null;

        foreach (var list in chipsByBetArea.Values) list.Clear();
        foreach (var container in opponentContainers.Values)
            if (container != null) container.gameObject.SetActive(false);

        isCashoutRunning = false;
        cashoutCoroutine = null;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Returns where a winning opponent chip should fly on cashout.
    /// Priority:
    ///   1. If the chip's owner is currently on the leaderboard → their live slot.
    ///   2. If we stored a spawn position at bet-time and it is a real leaderboard
    ///      slot (not null / not dealer) → use that (covers the case where the
    ///      player dropped off the board between bet and cashout but their slot
    ///      RectTransform is still valid).
    ///   3. Everything else (no payout data, empty username, off-leaderboard) →
    ///      playerCountArea.
    /// opponentDealerArea is NEVER returned — chips always go to a player target.
    /// </summary>
    private RectTransform GetCashoutDestinationForWinner(RectTransform chipRT)
    {
        // Resolve the username for this chip
        string username = chipRT != null && chipToUsername.ContainsKey(chipRT)
            ? chipToUsername[chipRT] : "";

        // 1. Live leaderboard lookup by username
        if (!string.IsNullOrEmpty(username))
        {
            RectTransform livePos = ResolveLeaderboardPosition(username);
            if (livePos != null) return livePos;
        }

        // 2. Stored spawn position from bet-time (join-time chips store this too now)
        if (chipRT != null && chipToSpawnPosition.ContainsKey(chipRT))
        {
            RectTransform stored = chipToSpawnPosition[chipRT];
            if (stored != null && stored != opponentDealerArea)
                return stored;
        }

        // 3. Final fallback — always playerCountArea, never the dealer
        return playerCountArea;
    }

    /// <summary>
    /// Looks up the live leaderboard UI slot for <paramref name="username"/>.
    /// Checks both richest and winners boards; returns the first valid slot found.
    /// Returns null if the player is not on any board or leaderboardController is missing.
    /// </summary>
    private RectTransform ResolveLeaderboardPosition(string username)
    {
        if (string.IsNullOrEmpty(username) || leaderboardController == null) return null;

        Leaderboards boards = lockedLeaderboards ?? currentLeaderboards;
        if (boards == null) return null;

        bool isInRichest = IsPlayerInLeaderboard(username, boards.richest);
        bool isInWinners = IsPlayerInLeaderboard(username, boards.winners);
        bool isTop3Richest = IsPlayerInFirst(username, boards.richest);
        bool isTop3Winner = IsPlayerInFirst(username, boards.winners);

        // Prefer richest board when the player is top-3 richest but NOT top-3 winner
        bool checkRichestFirst = isTop3Richest && !isTop3Winner;

        if (checkRichestFirst)
        {
            if (isInRichest)
            {
                RectTransform pos = leaderboardController.GetPlayerPosition(username, checkWinners: false);
                if (pos != null) return pos;
            }
            if (isInWinners)
            {
                RectTransform pos = leaderboardController.GetPlayerPosition(username, checkWinners: true);
                if (pos != null) return pos;
            }
        }
        else
        {
            if (isInWinners)
            {
                RectTransform pos = leaderboardController.GetPlayerPosition(username, checkWinners: true);
                if (pos != null) return pos;
            }
            if (isInRichest)
            {
                RectTransform pos = leaderboardController.GetPlayerPosition(username, checkWinners: false);
                if (pos != null) return pos;
            }
        }

        return null;
    }

    private RectTransform GetSpawnPositionForPlayer(string username, out bool spawnedFromWinners, out bool spawnedFromRichest)
    {
        spawnedFromWinners = false;
        spawnedFromRichest = false;
        if (string.IsNullOrEmpty(username) || leaderboardController == null) return null;

        Leaderboards boards = lockedLeaderboards ?? currentLeaderboards;
        if (boards == null) return null;

        bool isInRichest = IsPlayerInLeaderboard(username, boards.richest);
        bool isInWinners = IsPlayerInLeaderboard(username, boards.winners);
        bool isTop3Richest = IsPlayerInFirst(username, boards.richest);
        bool isTop3Winner = IsPlayerInFirst(username, boards.winners);

        bool checkRichestFirst = isTop3Richest && !isTop3Winner;

        if (checkRichestFirst)
        {
            if (isInRichest)
            {
                RectTransform pos = leaderboardController.GetPlayerPosition(username, checkWinners: false);
                if (pos != null) { spawnedFromRichest = true; return pos; }
            }
            if (isInWinners)
            {
                RectTransform pos = leaderboardController.GetPlayerPosition(username, checkWinners: true);
                if (pos != null) { spawnedFromWinners = true; return pos; }
            }
        }
        else
        {
            if (isInWinners)
            {
                RectTransform pos = leaderboardController.GetPlayerPosition(username, checkWinners: true);
                if (pos != null) { spawnedFromWinners = true; return pos; }
            }
            if (isInRichest)
            {
                RectTransform pos = leaderboardController.GetPlayerPosition(username, checkWinners: false);
                if (pos != null) { spawnedFromRichest = true; return pos; }
            }
        }
        return null;
    }

    private Vector2 GetCanvasPosition(RectTransform rt)
    {
        if (rt == null || targetCanvas == null) return Vector2.zero;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(targetCanvas.worldCamera, rt.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetCanvas.GetComponent<RectTransform>(), screenPoint, targetCanvas.worldCamera, out Vector2 localPoint);
        return localPoint;
    }

    private bool IsPlayerInFirst(string username, List<LeaderboardEntry> entries)
    {
        if (string.IsNullOrEmpty(username) || entries == null || entries.Count == 0) return false;
        return entries[0] != null && entries[0].username == username;
    }

    private bool IsPlayerInLeaderboard(string username, List<LeaderboardEntry> entries)
    {
        if (string.IsNullOrEmpty(username) || entries == null) return false;
        foreach (var entry in entries)
            if (entry != null && entry.username == username) return true;
        return false;
    }

    private double ParseFormattedCurrency(string formatted)
    {
        if (string.IsNullOrEmpty(formatted)) return 0;
        formatted = formatted.Trim();
        if (formatted.EndsWith("K", System.StringComparison.OrdinalIgnoreCase))
        {
            string numberPart = formatted.Substring(0, formatted.Length - 1);
            if (double.TryParse(numberPart, out double value)) return value * 1000;
        }
        if (double.TryParse(formatted, out double result)) return result;
        return 0;
    }
    #endregion
}