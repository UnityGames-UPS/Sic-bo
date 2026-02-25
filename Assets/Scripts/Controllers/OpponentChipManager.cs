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
    private Dictionary<RectTransform, string> chipToUsername = new Dictionary<RectTransform, string>(); // Track which chip belongs to which player
    private bool isCashoutRunning = false;
    private Coroutine cashoutCoroutine = null;
    private Leaderboards currentLeaderboards = null;
    private List<Payout> currentPayouts = null;
    private string localPlayerUsername = null;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
    }

    private void Start()
    {
        if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
    }

    private void OnDestroy()
    {
        foreach (var chip in activeOpponentChips)
            if (chip != null) chip.DOKill();
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
            containerObj.SetActive(false);

            opponentContainers[betOption] = container;
            chipsByBetArea[betOption] = new List<RectTransform>();
        }
    }

    internal void SetLeaderboardData(Leaderboards leaderboards) => currentLeaderboards = leaderboards;

    internal void SetCashoutData(List<Payout> payouts) => currentPayouts = payouts;

    internal void SetLocalPlayerUsername(string username) => localPlayerUsername = username;

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

        StopAllCoroutines();

        foreach (var chip in activeOpponentChips)
        {
            if (chip != null) { chip.DOKill(); Destroy(chip.gameObject); }
        }

        activeOpponentChips.Clear();
        chipToUsername.Clear();

        foreach (var container in opponentContainers.Values)
            if (container != null) container.gameObject.SetActive(false);

        foreach (var list in chipsByBetArea.Values)
            list.Clear();

        isCashoutRunning = false;
    }

    internal void PlayCashoutAnimation()
    {
        if (isCashoutRunning || activeOpponentChips.Count == 0) return;

        // Wait for leaderboard animation before starting cashout
        if (leaderboardController != null && leaderboardController.IsAnimating())
        {
            StartCoroutine(WaitForLeaderboardThenCashout());
        }
        else
        {
            // Play sound when starting cashout immediately
            AudioManager.Instance?.PlayChipAdd();
            cashoutCoroutine = StartCoroutine(CR_Cashout());
        }
    }

    private IEnumerator WaitForLeaderboardThenCashout()
    {
        Debug.Log("[OpponentChip] Waiting for leaderboard animation to complete...");
        yield return leaderboardController.WaitForAnimationComplete();
        Debug.Log("[OpponentChip] Leaderboard animation complete, starting cashout");

        // Play sound NOW, after leaderboard animation completes and right before chips move
        AudioManager.Instance?.PlayChipAdd();

        cashoutCoroutine = StartCoroutine(CR_Cashout());
    }

    internal bool IsCashoutRunning() => isCashoutRunning;
    #endregion

    #region Chip Animation
    private IEnumerator CR_SpawnAndAnimateChip(string betOption, double amount, string username = "")
    {
        RectTransform container = opponentContainers[betOption];

        if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
        Transform canvasRoot = targetCanvas != null ? targetCanvas.transform : transform.root;

        // Determine spawn position - ANY player in leaderboard spawns from their position
        RectTransform spawnPosition = GetSpawnPositionForPlayer(username);
        if (spawnPosition == null) spawnPosition = opponentDealerArea;

        GameObject chipObj = Instantiate(chipPrefab, spawnPosition);
        RectTransform chipRT = chipObj.GetComponent<RectTransform>();
        Chip chip = chipObj.GetComponent<Chip>();

        if (chip == null || chipRT == null) { Destroy(chipObj); yield break; }

        chip.SetSprite(grayChipSprite);
        chip.SetAmount(GameUtilities.FormatCurrency(amount));
        chip.SetActive(true);

        // Set badges only for TOP 3 leaderboard players
        bool isRichest = IsPlayerInTop3(username, currentLeaderboards?.richest);
        bool isWinner = IsPlayerInTop3(username, currentLeaderboards?.winners);
        chip.SetLeaderboardBadge(isRichest, isWinner);

        float scatterX = spawnPosition == opponentDealerArea ? dealerScatterX : 15f;
        float scatterY = spawnPosition == opponentDealerArea ? dealerScatterY : 10f;

        chipRT.localPosition = new Vector3(
            Random.Range(-scatterX, scatterX),
            Random.Range(-scatterY, scatterY), 0f);
        chipRT.localScale = Vector3.zero;
        container.gameObject.SetActive(true);

        chipRT.DOScale(chipScale, 0.2f).SetEase(Ease.OutBack);
        yield return new WaitForSeconds(0.22f);

        chipRT.SetParent(canvasRoot, worldPositionStays: true);

        Vector2 containerWorldPos = GetCanvasPosition(container);
        Vector2 destination = containerWorldPos + new Vector2(
            Random.Range(-betAreaScatterX, betAreaScatterX),
            Random.Range(-betAreaScatterY, betAreaScatterY));

        chipRT.DOAnchorPos(destination, dealerToBetDuration).SetEase(Ease.OutQuad);
        yield return new WaitForSeconds(dealerToBetDuration);

        chipRT.SetParent(container, worldPositionStays: true);

        activeOpponentChips.Add(chipRT);
        chipsByBetArea[betOption].Add(chipRT);

        // Store username for this chip so we can check badges during cashout
        if (!string.IsNullOrEmpty(username))
        {
            chipToUsername[chipRT] = username;
        }
    }

    private RectTransform GetSpawnPositionForPlayer(string username)
    {
        if (string.IsNullOrEmpty(username) || leaderboardController == null) return null;
        if (currentLeaderboards == null) return null;

        // Check if player is in both leaderboards
        bool isInWinners = IsPlayerInLeaderboard(username, currentLeaderboards.winners);
        bool isInRichest = IsPlayerInLeaderboard(username, currentLeaderboards.richest);

        // Determine priority based on badges (similar to cashout logic)
        bool checkWinnersFirst = true; // Default: winners has priority

        // Check TOP 3 badges to determine spawn priority
        bool isTop3Richest = IsPlayerInTop3(username, currentLeaderboards.richest);
        bool isTop3Winner = IsPlayerInTop3(username, currentLeaderboards.winners);

        if (isTop3Richest && !isTop3Winner)
        {
            // Player has richest badge only, prioritize richest leaderboard
            checkWinnersFirst = false;
            Debug.Log($"[OpponentChip] {username} has RICHEST badge, spawning from richest leaderboard");
        }
        else if (isTop3Winner)
        {
            Debug.Log($"[OpponentChip] {username} has WINNER badge, spawning from winners leaderboard");
        }

        // Check leaderboards based on priority
        if (checkWinnersFirst)
        {
            if (isInWinners)
            {
                RectTransform position = leaderboardController.GetPlayerPosition(username, checkWinners: true);
                if (position != null) return position;
            }

            if (isInRichest)
            {
                RectTransform position = leaderboardController.GetPlayerPosition(username, checkWinners: false);
                if (position != null) return position;
            }
        }
        else
        {
            if (isInRichest)
            {
                RectTransform position = leaderboardController.GetPlayerPosition(username, checkWinners: false);
                if (position != null) return position;
            }

            if (isInWinners)
            {
                RectTransform position = leaderboardController.GetPlayerPosition(username, checkWinners: true);
                if (position != null) return position;
            }
        }

        // Not in any leaderboard - use opponent dealer
        return null;
    }

    private IEnumerator CR_Cashout()
    {
        isCashoutRunning = true;

        if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
        Transform canvasRoot = targetCanvas != null ? targetCanvas.transform : transform.root;

        var chipsToAnimate = new List<RectTransform>(activeOpponentChips);

        foreach (var chip in chipsToAnimate)
            if (chip != null) chip.SetParent(canvasRoot, worldPositionStays: true);

        // Shuffle chips
        for (int i = chipsToAnimate.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var temp = chipsToAnimate[i];
            chipsToAnimate[i] = chipsToAnimate[j];
            chipsToAnimate[j] = temp;
        }

        // Group chips by destination based on payouts
        Dictionary<string, List<RectTransform>> chipsByDestination = new Dictionary<string, List<RectTransform>>();

        if (currentPayouts != null && currentPayouts.Count > 0)
        {
            Debug.Log($"[OpponentChip] Processing {currentPayouts.Count} payouts for cashout");

            // Calculate total win amount to distribute chips proportionally
            double totalWinAmount = 0;
            foreach (var payout in currentPayouts)
            {
                if (payout.win > 0)
                {
                    totalWinAmount += payout.win;
                    Debug.Log($"[OpponentChip] Winner: {payout.username}, Win: {payout.win}");
                }
            }

            if (totalWinAmount > 0 && chipsToAnimate.Count > 0)
            {
                int chipIndex = 0;

                // Distribute chips proportionally based on win amount
                foreach (var payout in currentPayouts)
                {
                    if (payout.win > 0 && !string.IsNullOrEmpty(payout.username))
                    {
                        // Calculate how many chips this winner should get
                        double winRatio = payout.win / totalWinAmount;
                        int chipsForWinner = Mathf.RoundToInt((float)(winRatio * chipsToAnimate.Count));
                        chipsForWinner = Mathf.Max(1, chipsForWinner); // At least 1 chip

                        if (!chipsByDestination.ContainsKey(payout.username))
                            chipsByDestination[payout.username] = new List<RectTransform>();

                        // Assign chips to this winner
                        int assigned = 0;
                        while (assigned < chipsForWinner && chipIndex < chipsToAnimate.Count)
                        {
                            chipsByDestination[payout.username].Add(chipsToAnimate[chipIndex]);
                            chipIndex++;
                            assigned++;
                        }

                        Debug.Log($"[OpponentChip] Assigned {assigned} chips to {payout.username}");
                    }
                }

                // Any remaining chips go randomly to dealers (losing chips)
                while (chipIndex < chipsToAnimate.Count)
                {
                    string dealerKey = Random.value > 0.5f ? "player_dealer" : "opponent_dealer";
                    if (!chipsByDestination.ContainsKey(dealerKey))
                        chipsByDestination[dealerKey] = new List<RectTransform>();
                    chipsByDestination[dealerKey].Add(chipsToAnimate[chipIndex]);
                    chipIndex++;
                }
            }
            else
            {
                // No wins - all chips go randomly to dealers
                foreach (var chip in chipsToAnimate)
                {
                    string dealerKey = Random.value > 0.5f ? "player_dealer" : "opponent_dealer";
                    if (!chipsByDestination.ContainsKey(dealerKey))
                        chipsByDestination[dealerKey] = new List<RectTransform>();
                    chipsByDestination[dealerKey].Add(chip);
                }
            }
        }
        else
        {
            // No payout data - split randomly between dealers
            Debug.Log("[OpponentChip] No payout data, splitting chips randomly");
            foreach (var chip in chipsToAnimate)
            {
                string dealerKey = Random.value > 0.5f ? "player_dealer" : "opponent_dealer";
                if (!chipsByDestination.ContainsKey(dealerKey))
                    chipsByDestination[dealerKey] = new List<RectTransform>();
                chipsByDestination[dealerKey].Add(chip);
            }
        }

        // Animate chips to their destinations
        foreach (var kvp in chipsByDestination)
        {
            string destination = kvp.Key;
            List<RectTransform> chips = kvp.Value;

            Debug.Log($"[OpponentChip] Routing {chips.Count} chips to destination: {destination}");

            foreach (var chip in chips)
            {
                if (chip == null) continue;

                RectTransform targetPosition = GetCashoutDestination(destination, chip);
                Debug.Log($"[OpponentChip] Chip destination for '{destination}' resolved to: {(targetPosition == playerDealerArea ? "PlayerDealer" : targetPosition == opponentDealerArea ? "OpponentDealer" : "Leaderboard")}");

                float scatterX = dealerScatterX;
                float scatterY = dealerScatterY;

                // Smaller scatter for leaderboard positions
                if (targetPosition != playerDealerArea && targetPosition != opponentDealerArea)
                {
                    scatterX = 15f;
                    scatterY = 10f;
                }

                Vector2 targetPos = GetCanvasPosition(targetPosition) + new Vector2(
                    Random.Range(-scatterX, scatterX),
                    Random.Range(-scatterY, scatterY));

                chip.DOAnchorPos(targetPos, cashoutDuration).SetEase(Ease.InQuad);
                chip.DOScale(0f, cashoutDuration * 0.6f)
                    .SetDelay(cashoutDuration * 0.4f)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => { if (chip != null) Destroy(chip.gameObject); });

                yield return new WaitForSeconds(cashoutStagger);
            }
        }

        yield return new WaitForSeconds(cashoutDuration);

        activeOpponentChips.Clear();
        currentPayouts = null;

        foreach (var list in chipsByBetArea.Values) list.Clear();
        foreach (var container in opponentContainers.Values)
            if (container != null) container.gameObject.SetActive(false);

        isCashoutRunning = false;
        cashoutCoroutine = null;
    }

    private RectTransform GetCashoutDestination(string destination, RectTransform chipRT = null)
    {
        // Handle dealer destinations
        if (destination == "player_dealer" || destination == "player")
        {
            return playerDealerArea;
        }
        else if (destination == "opponent_dealer" || destination == "opponent")
        {
            return opponentDealerArea;
        }

        // Check if player is local player
        if (destination == localPlayerUsername)
        {
            Debug.Log($"[OpponentChip] {destination} is local player, routing to player dealer");
            return playerDealerArea;
        }

        // Check chip badge to determine routing priority (if chip reference provided)
        bool checkWinnersFirst = true; // Default: winners has priority

        if (chipRT != null)
        {
            Chip chip = chipRT.GetComponent<Chip>();
            if (chip != null)
            {
                bool hasRichestBadge = chip.HasRichestBadge();
                bool hasWinnerBadge = chip.HasWinnerBadge();

                if (hasRichestBadge && !hasWinnerBadge)
                {
                    // Chip has richest badge, check richest first
                    checkWinnersFirst = false;
                    Debug.Log($"[OpponentChip] {destination} chip has RICHEST badge, prioritizing richest leaderboard");
                }
                else if (hasWinnerBadge)
                {
                    Debug.Log($"[OpponentChip] {destination} chip has WINNER badge, prioritizing winners leaderboard");
                }
            }
        }

        // Check if player is in leaderboards (only 3 in each list from backend)
        if (currentLeaderboards != null)
        {
            if (checkWinnersFirst)
            {
                // Check winners leaderboard first
                bool isInWinners = IsPlayerInLeaderboard(destination, currentLeaderboards.winners);
                if (isInWinners)
                {
                    RectTransform winnerPos = leaderboardController?.GetPlayerPosition(destination, checkWinners: true);
                    if (winnerPos != null)
                    {
                        Debug.Log($"[OpponentChip] {destination} found in WINNERS leaderboard, routing to leaderboard position");
                        return winnerPos;
                    }
                }

                // Check richest leaderboard as fallback
                bool isInRichest = IsPlayerInLeaderboard(destination, currentLeaderboards.richest);
                if (isInRichest)
                {
                    RectTransform richestPos = leaderboardController?.GetPlayerPosition(destination, checkWinners: false);
                    if (richestPos != null)
                    {
                        Debug.Log($"[OpponentChip] {destination} found in RICHEST leaderboard (fallback), routing to leaderboard position");
                        return richestPos;
                    }
                }
            }
            else
            {
                // Check richest leaderboard first (due to richest badge)
                bool isInRichest = IsPlayerInLeaderboard(destination, currentLeaderboards.richest);
                if (isInRichest)
                {
                    RectTransform richestPos = leaderboardController?.GetPlayerPosition(destination, checkWinners: false);
                    if (richestPos != null)
                    {
                        Debug.Log($"[OpponentChip] {destination} found in RICHEST leaderboard, routing to leaderboard position");
                        return richestPos;
                    }
                }

                // Check winners leaderboard as fallback
                bool isInWinners = IsPlayerInLeaderboard(destination, currentLeaderboards.winners);
                if (isInWinners)
                {
                    RectTransform winnerPos = leaderboardController?.GetPlayerPosition(destination, checkWinners: true);
                    if (winnerPos != null)
                    {
                        Debug.Log($"[OpponentChip] {destination} found in WINNERS leaderboard (fallback), routing to leaderboard position");
                        return winnerPos;
                    }
                }
            }

            Debug.Log($"[OpponentChip] {destination} NOT in any leaderboard, routing to opponent dealer");
        }
        else
        {
            Debug.Log($"[OpponentChip] No leaderboard data available, routing to opponent dealer");
        }

        // Not in any leaderboard or position not found - default to opponent dealer
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