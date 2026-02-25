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

        foreach (var container in opponentContainers.Values)
            if (container != null) container.gameObject.SetActive(false);

        foreach (var list in chipsByBetArea.Values)
            list.Clear();

        isCashoutRunning = false;
    }

    internal void PlayCashoutAnimation()
    {
        if (isCashoutRunning || activeOpponentChips.Count == 0) return;
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

        // Determine spawn position - ONLY leaderboard TOP 3 players get special spawn
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
    }

    private RectTransform GetSpawnPositionForPlayer(string username)
    {
        if (string.IsNullOrEmpty(username) || leaderboardController == null) return null;
        if (currentLeaderboards == null) return null;

        // Only spawn from leaderboard if player is in TOP 3 of either list
        bool isInWinnersTop3 = IsPlayerInTop3(username, currentLeaderboards.winners);
        bool isInRichestTop3 = IsPlayerInTop3(username, currentLeaderboards.richest);

        if (!isInWinnersTop3 && !isInRichestTop3)
        {
            // Not in any leaderboard top 3 - use normal opponent dealer
            return null;
        }

        // Check winners list first (higher priority visual)
        if (isInWinnersTop3)
        {
            RectTransform position = leaderboardController.GetPlayerPosition(username, checkWinners: true);
            if (position != null) return position;
        }

        // Check richest list
        if (isInRichestTop3)
        {
            RectTransform position = leaderboardController.GetPlayerPosition(username, checkWinners: false);
            if (position != null) return position;
        }

        // Fallback to null (opponent dealer)
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
            // Distribute chips proportionally to winners
            int chipsPerWinner = Mathf.Max(1, chipsToAnimate.Count / currentPayouts.Count);
            int chipIndex = 0;

            foreach (var payout in currentPayouts)
            {
                if (payout.win > 0 && !string.IsNullOrEmpty(payout.username))
                {
                    if (!chipsByDestination.ContainsKey(payout.username))
                        chipsByDestination[payout.username] = new List<RectTransform>();

                    int chipsToAssign = Mathf.Min(chipsPerWinner, chipsToAnimate.Count - chipIndex);
                    for (int i = 0; i < chipsToAssign && chipIndex < chipsToAnimate.Count; i++, chipIndex++)
                    {
                        chipsByDestination[payout.username].Add(chipsToAnimate[chipIndex]);
                    }
                }
            }

            // Assign any remaining chips to player dealer
            while (chipIndex < chipsToAnimate.Count)
            {
                if (!chipsByDestination.ContainsKey("player_dealer"))
                    chipsByDestination["player_dealer"] = new List<RectTransform>();
                chipsByDestination["player_dealer"].Add(chipsToAnimate[chipIndex]);
                chipIndex++;
            }
        }
        else
        {
            // No payout data - use old behavior
            int halfCount = chipsToAnimate.Count / 2;
            chipsByDestination["player"] = chipsToAnimate.GetRange(0, halfCount);
            chipsByDestination["opponent"] = chipsToAnimate.GetRange(halfCount, chipsToAnimate.Count - halfCount);
        }

        // Animate chips to their destinations
        foreach (var kvp in chipsByDestination)
        {
            string destination = kvp.Key;
            List<RectTransform> chips = kvp.Value;

            foreach (var chip in chips)
            {
                if (chip == null) continue;

                RectTransform targetPosition = GetCashoutDestination(destination);

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

    private RectTransform GetCashoutDestination(string destination)
    {
        // Check if destination is a username
        if (destination == "player_dealer" || destination == "player")
        {
            return playerDealerArea;
        }
        else if (destination == "opponent")
        {
            return opponentDealerArea;
        }

        // Check if player is local player
        if (destination == localPlayerUsername)
        {
            return playerDealerArea;
        }

        // Check if player is in TOP 3 of either leaderboard
        if (currentLeaderboards != null)
        {
            bool isInWinnersTop3 = IsPlayerInTop3(destination, currentLeaderboards.winners);
            bool isInRichestTop3 = IsPlayerInTop3(destination, currentLeaderboards.richest);

            if (isInWinnersTop3 || isInRichestTop3)
            {
                // Player is in top 3 - route to their leaderboard position
                RectTransform leaderboardPos = GetSpawnPositionForPlayer(destination);
                if (leaderboardPos != null)
                {
                    return leaderboardPos;
                }
            }
        }

        // Not in top 3 or leaderboard position not found - default to opponent dealer
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
    #endregion
}