using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class OpponentChipManager : MonoBehaviour
{
    #region Serialized Fields
    [Header("Dealer Areas")]
    [SerializeField] private RectTransform opponentDealerArea;
    [SerializeField] private RectTransform playerDealerArea;

    [Header("Chip Spawning")]
    [SerializeField] private GameObject chipPrefab;
    [SerializeField] private Sprite grayChipSprite;

    [Header("Animation Settings")]
    [SerializeField] private float dealerToBetDuration = 0.45f;
    [SerializeField] private float chipStaggerDelay = 0.06f;
    [SerializeField] private float cashoutDuration = 0.55f;
    [SerializeField] private float cashoutStagger = 0.05f;
    [SerializeField] private float dealerScatterX = 20f;
    [SerializeField] private float dealerScatterY = 15f;
    [SerializeField] private float betAreaScatterX = 12f;
    [SerializeField] private float betAreaScatterY = 10f;
    [SerializeField] private float chipScale = 0.8f;

    [Header("References")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private LeaderboardController leaderboardController;
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
    private readonly List<RectTransform> _toSweepCache = new List<RectTransform>();
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
        foreach (var chip in activeOpponentChips)
            if (chip != null) chip.DOKill();
        foreach (var chip in activeWinChips)
            if (chip != null) chip.DOKill();

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

        for (int i = 0; i < CHIP_POOL_SIZE; i++)
        {
            GameObject chipObj = Instantiate(chipPrefab, opponentDealerArea);
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
        GameObject chipObj = Instantiate(chipPrefab, opponentDealerArea);
        return chipObj.GetComponent<RectTransform>();
    }

    private void ReturnChipToPool(RectTransform chipRT)
    {
        if (chipRT == null) return;
        chipRT.DOKill();
        chipRT.gameObject.SetActive(false);
        chipRT.localScale = Vector3.one;
        chipRT.SetParent(opponentDealerArea, false);

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

    internal void SetLeaderboardData(Leaderboards leaderboards)
    {
        currentLeaderboards = leaderboards;

        if (activeOpponentChips.Count == 0 && activeWinChips.Count == 0)
        {
            lockedLeaderboards = leaderboards;
        }
    }

    internal void LockLeaderboardsForRound()
    {
        lockedLeaderboards = currentLeaderboards;
    }

    internal void SetCashoutData(List<Payout> payouts) => currentPayouts = payouts;

    internal void SetLocalPlayerUsername(string username) => localPlayerUsername = username;

    internal void SetWinningBetAreas(List<string> winningAreas)
    {
        winningBetAreas.Clear();
        if (winningAreas != null)
        {
            foreach (string area in winningAreas)
            {
                winningBetAreas.Add(area);
            }
        }
    }

    internal void AddOpponentBet(string betOption, double amount, string username = "")
    {
        if (!opponentContainers.ContainsKey(betOption)) return;
        if (opponentDealerArea == null || chipPrefab == null || grayChipSprite == null) return;

        AudioManager.Instance?.PlayChipAdd();
        StartCoroutine(CR_SpawnAndAnimateChip(betOption, amount, username));
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

        foreach (var chip in activeOpponentChips)
        {
            if (chip != null) ReturnChipToPool(chip);
        }

        foreach (var chip in activeWinChips)
        {
            if (chip != null) ReturnChipToPool(chip);
        }

        activeOpponentChips.Clear();
        activeWinChips.Clear();
        chipToUsername.Clear();
        chipToSpawnPosition.Clear();
        chipToOriginalBadge.Clear();
        winningBetAreas.Clear();
        lockedLeaderboards = null;

        foreach (var container in opponentContainers.Values)
            if (container != null) container.gameObject.SetActive(false);

        foreach (var list in chipsByBetArea.Values)
            list.Clear();

        isCashoutRunning = false;
    }

    internal void PlayCashoutAnimation()
    {
        if (isCashoutRunning || activeOpponentChips.Count == 0) return;

        if (leaderboardController != null && leaderboardController.IsAnimating())
        {
            StartCoroutine(WaitForLeaderboardThenCashout());
        }
        else if (winAnimationCoroutine != null)
        {

            StartCoroutine(WaitForWinAnimationThenCashout());
        }
        else
        {
            AudioManager.Instance?.PlayChipAdd();
            cashoutCoroutine = StartCoroutine(CR_Cashout());
        }
    }

    internal void PlayOpponentWinAnimations()
    {
        if (winAnimationCoroutine != null) StopCoroutine(winAnimationCoroutine);
        winAnimationCoroutine = StartCoroutine(CR_OpponentWinAnimation());
    }

    private IEnumerator WaitForLeaderboardThenCashout()
    {
        yield return leaderboardController.WaitForAnimationComplete();
        if (winAnimationCoroutine != null)
        {
            yield return winAnimationCoroutine;
        }

        AudioManager.Instance?.PlayChipAdd();
        cashoutCoroutine = StartCoroutine(CR_Cashout());
    }

    private IEnumerator WaitForWinAnimationThenCashout()
    {
        yield return winAnimationCoroutine;
        AudioManager.Instance?.PlayChipAdd();
        cashoutCoroutine = StartCoroutine(CR_Cashout());
    }

    internal bool IsCashoutRunning() => isCashoutRunning;

    internal bool HasActiveChips()
    {
        return activeOpponentChips.Count > 0 || activeWinChips.Count > 0;
    }
    #endregion

    #region Opponent Win Animation
    private IEnumerator CR_OpponentWinAnimation()
    {
        if (winningBetAreas.Count == 0)
        {
            winAnimationCoroutine = null;
            yield break;
        }


        if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
        Transform canvasRoot = targetCanvas != null ? targetCanvas.transform : transform.root;

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
                if (chipComponent != null && chipComponent.chipText != null)
                {
                    string chipAmountText = chipComponent.chipText.text;

                    double betAmount = ParseFormattedCurrency(chipAmountText);

                    if (betAmount > 0)
                    {
                        string key = $"{chipOwner}_{betArea}";
                        if (!_betAmountsCache.ContainsKey(key))
                        {
                            _betAmountsCache[key] = 0;
                        }
                        _betAmountsCache[key] += betAmount;
                    }
                }
            }
        }

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
                int winChipCount = UnityEngine.Random.Range(2, 4);
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
                    if (chipToOriginalBadge.ContainsKey(chip))
                    {
                        BadgeState badge = chipToOriginalBadge[chip];
                        winChip.SetLeaderboardBadge(badge.isRichest, badge.isWinner);
                    }
                    if (estimatedWinPerChip > 0)
                    {
                        string formattedAmount = GameUtilities.FormatCurrency(estimatedWinPerChip);
                        winChip.SetAmount(formattedAmount);

                    }
                    else
                    {
                        winChip.SetAmount("");
                    }
                    winChipRT.localPosition = new Vector3(
                        Random.Range(-dealerScatterX, dealerScatterX),
                        Random.Range(-dealerScatterY, dealerScatterY), 0f);
                    winChipRT.localScale = Vector3.zero;
                    winChipRT.DOScale(chipScale * 0.9f, 0.18f).SetEase(Ease.OutBack);
                    yield return new WaitForSeconds(0.04f);
                    winChipRT.SetParent(canvasRoot, worldPositionStays: true);

                    // Use world-space position – reliable across all resolutions/ratios.
                    float winCanvasScale = targetCanvas != null ? targetCanvas.transform.lossyScale.x : 1f;
                    Vector3 winTargetWorldPos = targetContainer.position + new Vector3(
                        Random.Range(-betAreaScatterX, betAreaScatterX) * winCanvasScale,
                        Random.Range(-betAreaScatterY, betAreaScatterY) * winCanvasScale,
                        0f);

                    winChipRT.DOMove(winTargetWorldPos, dealerToBetDuration * 0.8f).SetEase(Ease.OutQuad);

                    activeWinChips.Add(winChipRT);

                    yield return new WaitForSeconds(chipStaggerDelay);
                }
            }
        }

        yield return new WaitForSeconds(dealerToBetDuration * 0.8f);

        winAnimationCoroutine = null;
    }
    private double ParseFormattedCurrency(string formatted)
    {
        if (string.IsNullOrEmpty(formatted)) return 0;

        formatted = formatted.Trim();
        if (formatted.EndsWith("K", System.StringComparison.OrdinalIgnoreCase))
        {
            string numberPart = formatted.Substring(0, formatted.Length - 1);
            if (double.TryParse(numberPart, out double value))
            {
                return value * 1000;
            }
        }
        if (double.TryParse(formatted, out double result))
        {
            return result;
        }

        return 0;
    }
    #endregion

    #region Chip Animation
    private IEnumerator CR_SpawnAndAnimateChip(string betOption, double amount, string username = "")
    {
        RectTransform container = opponentContainers[betOption];

        if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
        Transform canvasRoot = targetCanvas != null ? targetCanvas.transform : transform.root;

        bool spawnedFromWinners = false;
        bool spawnedFromRichest = false;
        RectTransform spawnPosition = GetSpawnPositionForPlayer(username, out spawnedFromWinners, out spawnedFromRichest);
        if (spawnPosition == null) spawnPosition = opponentDealerArea;

        // Use pooled chip instead of Instantiate
        RectTransform chipRT = GetPooledChip();
        chipRT.SetParent(spawnPosition, false);
        Chip chip = chipRT.GetComponent<Chip>();

        if (chip == null || chipRT == null) { ReturnChipToPool(chipRT); yield break; }

        chip.SetSprite(grayChipSprite);
        chip.SetAmount(GameUtilities.FormatCurrency(amount));
        chip.SetActive(true);
        Leaderboards leaderboardsToUse = lockedLeaderboards ?? currentLeaderboards;
        bool showRichestBadge = spawnedFromRichest && IsPlayerInTop3(username, leaderboardsToUse?.richest);
        bool showWinnerBadge = spawnedFromWinners && IsPlayerInTop3(username, leaderboardsToUse?.winners);

        if (showRichestBadge && showWinnerBadge)
        {
            showRichestBadge = false;
        }

        chip.SetLeaderboardBadge(showRichestBadge, showWinnerBadge);

        chipToOriginalBadge[chipRT] = new BadgeState
        {
            isRichest = showRichestBadge,
            isWinner = showWinnerBadge
        };

        if (spawnPosition == opponentDealerArea)
        {
        }
        else
        {
        }

        float scatterX = dealerScatterX;
        float scatterY = dealerScatterY;

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

        if (!string.IsNullOrEmpty(username))
        {
            chipToUsername[chipRT] = username;
        }

        chipToSpawnPosition[chipRT] = spawnPosition;
    }

    private RectTransform GetSpawnPositionForPlayer(string username, out bool spawnedFromWinners, out bool spawnedFromRichest)
    {
        spawnedFromWinners = false;
        spawnedFromRichest = false;

        if (string.IsNullOrEmpty(username) || leaderboardController == null) return null;

        Leaderboards leaderboardsToUse = lockedLeaderboards ?? currentLeaderboards;
        if (leaderboardsToUse == null) return null;

        bool isInWinners = IsPlayerInLeaderboard(username, leaderboardsToUse.winners);
        bool isInRichest = IsPlayerInLeaderboard(username, leaderboardsToUse.richest);

        bool checkWinnersFirst = true;

        bool isTop3Richest = IsPlayerInTop3(username, leaderboardsToUse.richest);
        bool isTop3Winner = IsPlayerInTop3(username, leaderboardsToUse.winners);

        if (isTop3Richest && !isTop3Winner)
        {
            checkWinnersFirst = false;
        }
        if (checkWinnersFirst)
        {
            if (isInWinners)
            {
                RectTransform position = leaderboardController.GetPlayerPosition(username, checkWinners: true);
                if (position != null)
                {
                    spawnedFromWinners = true;
                    return position;
                }
            }

            if (isInRichest)
            {
                RectTransform position = leaderboardController.GetPlayerPosition(username, checkWinners: false);
                if (position != null)
                {
                    spawnedFromRichest = true;
                    return position;
                }
            }
        }
        else
        {
            if (isInRichest)
            {
                RectTransform position = leaderboardController.GetPlayerPosition(username, checkWinners: false);
                if (position != null)
                {
                    spawnedFromRichest = true;
                    return position;
                }
            }

            if (isInWinners)
            {
                RectTransform position = leaderboardController.GetPlayerPosition(username, checkWinners: true);
                if (position != null)
                {
                    spawnedFromWinners = true;
                    return position;
                }
            }
        }
        return null;
    }

    private IEnumerator CR_Cashout()
    {
        isCashoutRunning = true;

        if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
        Transform canvasRoot = targetCanvas != null ? targetCanvas.transform : transform.root;

        foreach (var kvp in chipToOriginalBadge)
        {
            RectTransform chipRT = kvp.Key;
            BadgeState originalBadge = kvp.Value;

            if (chipRT != null)
            {
                Chip chipComponent = chipRT.GetComponent<Chip>();
                if (chipComponent != null)
                {
                    chipComponent.SetLeaderboardBadge(originalBadge.isRichest, originalBadge.isWinner);
                }
            }
        }

        _winnersByBetAreaCache.Clear();

        if (currentPayouts != null && currentPayouts.Count > 0)
        {

            foreach (var payout in currentPayouts)
            {
                if (payout.win > 0 && !string.IsNullOrEmpty(payout.username))
                {
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
                                else
                                    _winnersByBetAreaCache[betArea].Clear();

                                _winnersByBetAreaCache[betArea].Add(payout.username);
                                break;
                            }
                        }
                    }
                }
            }
        }
        int chipAnimationCount = 0;

        foreach (var kvp in chipsByBetArea)
        {
            string betArea = kvp.Key;
            List<RectTransform> chipsInArea = kvp.Value;

            bool isWinningArea = winningBetAreas.Contains(betArea);

            foreach (var chip in chipsInArea)
            {
                if (chip == null) continue;

                chip.SetParent(canvasRoot, worldPositionStays: true);

                string chipOwner = chipToUsername.ContainsKey(chip) ? chipToUsername[chip] : "";
                bool chipOwnerWon = !string.IsNullOrEmpty(chipOwner) &&
                                   _winnersByBetAreaCache.ContainsKey(betArea) &&
                                   _winnersByBetAreaCache[betArea].Contains(chipOwner);

                bool isLosingChip = !isWinningArea || !chipOwnerWon;

                if (isLosingChip)
                {
                    Chip chipComponent = chip.GetComponent<Chip>();
                    if (chipComponent != null)
                    {
                        chipComponent.ClearLeaderboardBadge();
                    }

                    RectTransform targetDealer = Random.value > 0.5f ? playerDealerArea : opponentDealerArea;

                    float coScale = targetCanvas != null ? targetCanvas.transform.lossyScale.x : 1f;
                    Vector3 cashoutLoseTarget = targetDealer.position + new Vector3(
                        Random.Range(-dealerScatterX, dealerScatterX) * coScale,
                        Random.Range(-dealerScatterY, dealerScatterY) * coScale,
                        0f);
                    float staggerDelay = chipAnimationCount * cashoutStagger * 0.1f;

                    chip.DOMove(cashoutLoseTarget, cashoutDuration).SetEase(Ease.InQuad).SetDelay(staggerDelay);
                    chip.DOScale(0f, cashoutDuration * 0.6f)
                        .SetDelay(cashoutDuration * 0.4f + staggerDelay)
                        .SetEase(Ease.InBack)
                        .OnComplete(() => { if (chip != null) ReturnChipToPool(chip); });

                    chipAnimationCount++;
                }
                else
                {

                    Chip chipComponent = chip.GetComponent<Chip>();
                    if (chipToOriginalBadge.ContainsKey(chip))
                    {
                        BadgeState originalBadge = chipToOriginalBadge[chip];

                        if (chipComponent != null)
                        {
                            chipComponent.SetLeaderboardBadge(originalBadge.isRichest, originalBadge.isWinner);
                        }

                    }

                    RectTransform targetPosition = GetCashoutDestinationForWinner(chip);

                    float scatterX = 15f;
                    float scatterY = 10f;

                    if (targetPosition == playerDealerArea || targetPosition == opponentDealerArea)
                    {
                        scatterX = dealerScatterX;
                        scatterY = dealerScatterY;
                    }

                    float coWinScale = targetCanvas != null ? targetCanvas.transform.lossyScale.x : 1f;
                    Vector3 cashoutWinTarget = targetPosition.position + new Vector3(
                        Random.Range(-scatterX, scatterX) * coWinScale,
                        Random.Range(-scatterY, scatterY) * coWinScale,
                        0f);

                    float staggerDelay = chipAnimationCount * cashoutStagger * 0.1f;

                    chip.DOMove(cashoutWinTarget, cashoutDuration).SetEase(Ease.InQuad).SetDelay(staggerDelay);
                    chip.DOScale(0f, cashoutDuration * 0.6f)
                        .SetDelay(cashoutDuration * 0.4f + staggerDelay)
                        .SetEase(Ease.InBack)
                        .OnComplete(() => { if (chip != null) ReturnChipToPool(chip); });

                    chipAnimationCount++;
                }
            }
        }
        int winChipIndex = 0;
        foreach (var winChip in activeWinChips)
        {
            if (winChip == null) continue;

            winChip.SetParent(canvasRoot, worldPositionStays: true);
            RectTransform targetDealer = Random.value > 0.5f ? playerDealerArea : opponentDealerArea;

            float winCashoutScale = targetCanvas != null ? targetCanvas.transform.lossyScale.x : 1f;
            Vector3 winCashoutTarget = targetDealer.position + new Vector3(
                Random.Range(-dealerScatterX, dealerScatterX) * winCashoutScale,
                Random.Range(-dealerScatterY, dealerScatterY) * winCashoutScale,
                0f);
            float staggerDelay = winChipIndex * cashoutStagger * 0.1f;

            winChip.DOMove(winCashoutTarget, cashoutDuration * 0.7f).SetEase(Ease.InQuad).SetDelay(staggerDelay);
            winChip.DOScale(0f, cashoutDuration * 0.5f)
                .SetDelay(cashoutDuration * 0.3f + staggerDelay)
                .SetEase(Ease.InBack)
                .OnComplete(() => { if (winChip != null) ReturnChipToPool(winChip); });

            winChipIndex++;
        }

        yield return new WaitForSeconds(cashoutDuration);

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

    private RectTransform GetCashoutDestinationForWinner(RectTransform chipRT)
    {
        if (chipRT == null) return opponentDealerArea;

        string chipOwner = chipToUsername.ContainsKey(chipRT) ? chipToUsername[chipRT] : "";

        if (chipOwner == localPlayerUsername)
        {
            return playerDealerArea;
        }

        if (chipToSpawnPosition.ContainsKey(chipRT))
        {
            RectTransform originalSpawnPosition = chipToSpawnPosition[chipRT];

            if (originalSpawnPosition != null &&
                originalSpawnPosition != opponentDealerArea &&
                originalSpawnPosition != playerDealerArea)
            {
                return originalSpawnPosition;
            }

            if (originalSpawnPosition == opponentDealerArea)
            {
                return opponentDealerArea;
            }
        }
        return opponentDealerArea;
    }
    #endregion

    #region Helpers
    private Vector2 GetCanvasPosition(RectTransform rt)
    {
        if (rt == null || targetCanvas == null) return Vector2.zero;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(targetCanvas.worldCamera, rt.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetCanvas.GetComponent<RectTransform>(), screenPoint, targetCanvas.worldCamera, out Vector2 localPoint);
        return localPoint;
    }

    private bool IsPlayerInTop3(string username, List<LeaderboardEntry> entries)
    {
        if (string.IsNullOrEmpty(username) || entries == null) return false;

        int checkCount = Mathf.Min(3, entries.Count);
        for (int i = 0; i < checkCount; i++)
        {
            if (entries[i] != null && entries[i].username == username)
                return true;
        }

        return false;
    }

    private bool IsPlayerInLeaderboard(string username, List<LeaderboardEntry> entries)
    {
        if (string.IsNullOrEmpty(username) || entries == null || entries.Count == 0) return false;

        foreach (var entry in entries)
        {
            if (entry != null && entry.username == username)
            {
                return true;
            }
        }

        return false;
    }
    #endregion
}