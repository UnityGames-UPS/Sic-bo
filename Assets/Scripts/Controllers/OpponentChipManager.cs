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
    #endregion

    #region Private Fields
    private Dictionary<string, RectTransform> opponentContainers = new Dictionary<string, RectTransform>();
    private List<RectTransform> activeOpponentChips = new List<RectTransform>();
    private Dictionary<string, List<RectTransform>> chipsByBetArea = new Dictionary<string, List<RectTransform>>();
    private bool isCashoutRunning = false;
    private Coroutine cashoutCoroutine = null;
    private Leaderboards currentLeaderboards = null;
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

        GameObject chipObj = Instantiate(chipPrefab, opponentDealerArea);
        RectTransform chipRT = chipObj.GetComponent<RectTransform>();
        Chip chip = chipObj.GetComponent<Chip>();

        if (chip == null || chipRT == null) { Destroy(chipObj); yield break; }

        chip.SetSprite(grayChipSprite);
        chip.SetAmount(GameUtilities.FormatCurrency(amount));
        chip.SetActive(true);

        bool isRichest = IsUsernameInList(username, currentLeaderboards?.richest);
        bool isWinner = IsUsernameInList(username, currentLeaderboards?.winners);
        chip.SetLeaderboardBadge(isRichest, isWinner);

        chipRT.localPosition = new Vector3(
            Random.Range(-dealerScatterX, dealerScatterX),
            Random.Range(-dealerScatterY, dealerScatterY), 0f);
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

    private IEnumerator CR_Cashout()
    {
        isCashoutRunning = true;

        if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
        Transform canvasRoot = targetCanvas != null ? targetCanvas.transform : transform.root;

        var chipsToAnimate = new List<RectTransform>(activeOpponentChips);

        foreach (var chip in chipsToAnimate)
            if (chip != null) chip.SetParent(canvasRoot, worldPositionStays: true);

        for (int i = chipsToAnimate.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var temp = chipsToAnimate[i];
            chipsToAnimate[i] = chipsToAnimate[j];
            chipsToAnimate[j] = temp;
        }

        int halfCount = chipsToAnimate.Count / 2;

        for (int i = 0; i < chipsToAnimate.Count; i++)
        {
            RectTransform chip = chipsToAnimate[i];
            if (chip == null) continue;

            RectTransform targetDealer = (i < halfCount) ? playerDealerArea : opponentDealerArea;
            if (targetDealer == null) targetDealer = opponentDealerArea;

            Vector2 targetPos = GetCanvasPosition(targetDealer) + new Vector2(
                Random.Range(-dealerScatterX, dealerScatterX),
                Random.Range(-dealerScatterY, dealerScatterY));

            chip.DOAnchorPos(targetPos, cashoutDuration).SetEase(Ease.InQuad);
            chip.DOScale(0f, cashoutDuration * 0.6f)
                .SetDelay(cashoutDuration * 0.4f)
                .SetEase(Ease.InBack)
                .OnComplete(() => { if (chip != null) Destroy(chip.gameObject); });

            yield return new WaitForSeconds(cashoutStagger);
        }

        yield return new WaitForSeconds(cashoutDuration);

        activeOpponentChips.Clear();

        foreach (var list in chipsByBetArea.Values) list.Clear();
        foreach (var container in opponentContainers.Values)
            if (container != null) container.gameObject.SetActive(false);

        isCashoutRunning = false;
        cashoutCoroutine = null;
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

    private static bool IsUsernameInList(string username, List<LeaderboardEntry> entries)
    {
        if (string.IsNullOrEmpty(username) || entries == null) return false;
        int checkCount = Mathf.Min(3, entries.Count);
        for (int i = 0; i < checkCount; i++)
            if (entries[i] != null && entries[i].username == username) return true;
        return false;
    }
    #endregion
}