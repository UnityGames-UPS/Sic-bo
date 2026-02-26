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
    private Dictionary<RectTransform, RectTransform> chipToSpawnPosition = new Dictionary<RectTransform, RectTransform>(); // Track where each chip spawned from
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
        chipToSpawnPosition.Clear(); // Clear spawn position tracking

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

        // Determine spawn position and track which leaderboard it came from
        bool spawnedFromWinners = false;
        bool spawnedFromRichest = false;
        RectTransform spawnPosition = GetSpawnPositionForPlayer(username, out spawnedFromWinners, out spawnedFromRichest);
        if (spawnPosition == null) spawnPosition = opponentDealerArea;

        GameObject chipObj = Instantiate(chipPrefab, spawnPosition);
        RectTransform chipRT = chipObj.GetComponent<RectTransform>();
        Chip chip = chipObj.GetComponent<Chip>();

        if (chip == null || chipRT == null) { Destroy(chipObj); yield break; }

        chip.SetSprite(grayChipSprite);
        chip.SetAmount(GameUtilities.FormatCurrency(amount));
        chip.SetActive(true);

        // CRITICAL: Set badge to match the ACTUAL spawn position
        // If spawned from Winners → show winner badge ONLY
        // If spawned from Richest → show richest badge ONLY
        // This ensures badge ALWAYS matches the spawn location
        // GUARANTEE: Only ONE badge can be true (mutually exclusive)
        bool showRichestBadge = spawnedFromRichest && IsPlayerInTop3(username, currentLeaderboards?.richest);
        bool showWinnerBadge = spawnedFromWinners && IsPlayerInTop3(username, currentLeaderboards?.winners);

        // SAFETY CHECK: Ensure no conflict when player is in both leaderboards
        // This should never happen due to the spawn logic, but we verify here for safety
        if (showRichestBadge && showWinnerBadge)
        {
            Debug.LogError($"[OpponentChip] CONFLICT: {username} chip has BOTH badges! This should never happen. Defaulting to Winner badge.");
            showRichestBadge = false; // Winner badge takes priority
        }

        chip.SetLeaderboardBadge(showRichestBadge, showWinnerBadge);

        Debug.Log($"[OpponentChip] {username} chip badge set: Richest={showRichestBadge}, Winner={showWinnerBadge}, SpawnPos={spawnPosition.name}");

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

        // IMPORTANT: Store the original spawn position so chip returns to where it came from
        chipToSpawnPosition[chipRT] = spawnPosition;
    }

    private RectTransform GetSpawnPositionForPlayer(string username, out bool spawnedFromWinners, out bool spawnedFromRichest)
    {
        spawnedFromWinners = false;
        spawnedFromRichest = false;

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
            Debug.Log($"[OpponentChip] {username} has RICHEST badge only, prioritizing richest leaderboard");
        }
        else if (isTop3Winner)
        {
            // Player has winner badge (may also have richest), prioritize winner
            Debug.Log($"[OpponentChip] {username} has WINNER badge, prioritizing winners leaderboard");
        }

        // Check leaderboards based on priority and track which one we spawn from
        // IMPORTANT: We return immediately after finding a position, ensuring only ONE flag is set
        if (checkWinnersFirst)
        {
            if (isInWinners)
            {
                RectTransform position = leaderboardController.GetPlayerPosition(username, checkWinners: true);
                if (position != null)
                {
                    spawnedFromWinners = true;
                    Debug.Log($"[OpponentChip] {username} spawning from WINNERS leaderboard position");
                    return position; // ← RETURN: Only Winners flag is true
                }
            }

            if (isInRichest)
            {
                RectTransform position = leaderboardController.GetPlayerPosition(username, checkWinners: false);
                if (position != null)
                {
                    spawnedFromRichest = true;
                    Debug.Log($"[OpponentChip] {username} spawning from RICHEST leaderboard position (fallback)");
                    return position; // ← RETURN: Only Richest flag is true
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
                    Debug.Log($"[OpponentChip] {username} spawning from RICHEST leaderboard position");
                    return position; // ← RETURN: Only Richest flag is true
                }
            }

            if (isInWinners)
            {
                RectTransform position = leaderboardController.GetPlayerPosition(username, checkWinners: true);
                if (position != null)
                {
                    spawnedFromWinners = true;
                    Debug.Log($"[OpponentChip] {username} spawning from WINNERS leaderboard position (fallback)");
                    return position; // ← RETURN: Only Winners flag is true
                }
            }
        }

        // Not in any leaderboard - use opponent dealer (both flags remain false)
        Debug.Log($"[OpponentChip] {username} not in any leaderboard, will spawn from opponent dealer");
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
                // Create a set of winner usernames for quick lookup
                HashSet<string> winners = new HashSet<string>();
                foreach (var payout in currentPayouts)
                {
                    if (payout.win > 0 && !string.IsNullOrEmpty(payout.username))
                    {
                        winners.Add(payout.username);
                        Debug.Log($"[OpponentChip] Winner: {payout.username}, Win: {payout.win}");
                    }
                }

                // Route each chip based on whether its owner won
                foreach (var chip in chipsToAnimate)
                {
                    string chipOwner = chipToUsername.ContainsKey(chip) ? chipToUsername[chip] : "";

                    if (!string.IsNullOrEmpty(chipOwner) && winners.Contains(chipOwner))
                    {
                        // This chip belongs to a winner
                        // Check if it spawned from a leaderboard position
                        bool spawnedFromLeaderboard = chipToSpawnPosition.ContainsKey(chip) &&
                                                      chipToSpawnPosition[chip] != null &&
                                                      chipToSpawnPosition[chip] != opponentDealerArea &&
                                                      chipToSpawnPosition[chip] != playerDealerArea;

                        if (spawnedFromLeaderboard)
                        {
                            // Winner's chip from leaderboard → return to their leaderboard position
                            if (!chipsByDestination.ContainsKey(chipOwner))
                                chipsByDestination[chipOwner] = new List<RectTransform>();
                            chipsByDestination[chipOwner].Add(chip);
                            Debug.Log($"[OpponentChip] Winner {chipOwner}'s chip → returning to leaderboard position");
                        }
                        else
                        {
                            // Winner's chip but spawned from dealer → go to player dealer
                            if (!chipsByDestination.ContainsKey("player_dealer"))
                                chipsByDestination["player_dealer"] = new List<RectTransform>();
                            chipsByDestination["player_dealer"].Add(chip);
                            Debug.Log($"[OpponentChip] Winner {chipOwner}'s chip spawned from dealer → player dealer");
                        }
                    }
                    else
                    {
                        // This chip belongs to a non-winner OR has no owner → opponent dealer
                        if (!chipsByDestination.ContainsKey("opponent_dealer"))
                            chipsByDestination["opponent_dealer"] = new List<RectTransform>();
                        chipsByDestination["opponent_dealer"].Add(chip);
                        Debug.Log($"[OpponentChip] Loser chip (owner: {chipOwner}) → opponent dealer");
                    }
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
        chipToSpawnPosition.Clear(); // Clear spawn position tracking after cashout
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

        // CRITICAL FIX: Return chip to its ORIGINAL spawn position
        // This prevents chips from switching leaderboards mid-animation when badges change
        if (chipRT != null && chipToSpawnPosition.ContainsKey(chipRT))
        {
            RectTransform originalSpawnPosition = chipToSpawnPosition[chipRT];

            // If it spawned from opponent dealer, return there
            if (originalSpawnPosition == opponentDealerArea)
            {
                Debug.Log($"[OpponentChip] Chip spawned from opponent dealer, returning to opponent dealer");
                return opponentDealerArea;
            }

            // If it spawned from a leaderboard position, return to the SAME position
            if (originalSpawnPosition != null && originalSpawnPosition != opponentDealerArea && originalSpawnPosition != playerDealerArea)
            {
                Debug.Log($"[OpponentChip] Chip returning to original spawn leaderboard position");
                return originalSpawnPosition;
            }
        }

        // Fallback: No spawn position tracked, use opponent dealer
        Debug.Log($"[OpponentChip] No spawn position tracked for chip, defaulting to opponent dealer");
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

        // Backend only sends 3 entries per leaderboard, check all of them
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