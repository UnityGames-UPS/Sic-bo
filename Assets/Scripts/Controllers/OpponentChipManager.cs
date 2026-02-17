using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class OpponentChipManager : MonoBehaviour
{
    #region Serialized Fields
    [Header("Dealer Areas")]
    [SerializeField] private RectTransform opponentDealerArea;  // Where opponent chips spawn from
    [SerializeField] private RectTransform playerDealerArea;    // Half chips go here at cashout

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

    // FIX: Track whether a cashout animation is currently running so we don't double-clear.
    private bool isCashoutRunning = false;
    private Coroutine cashoutCoroutine = null;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>();
    }

    private void Start()
    {
        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>();
    }

    private void OnDestroy()
    {
        foreach (var chip in activeOpponentChips)
            if (chip != null) chip.DOKill();
    }
    #endregion

    #region Public API - Setup
    public void InitializeContainers(Dictionary<string, Transform> betAreaMap)
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

        Debug.Log($"[OpponentChipManager] Initialized {opponentContainers.Count} containers");
    }
    #endregion

    #region Public API - Betting Phase
    public void AddOpponentBet(string betOption, double amount)
    {
        if (!opponentContainers.ContainsKey(betOption))
        {
            Debug.LogWarning($"[OpponentChipManager] No container for bet option: {betOption}");
            return;
        }

        if (opponentDealerArea == null || chipPrefab == null || grayChipSprite == null)
        {
            Debug.LogError("[OpponentChipManager] Missing references!");
            return;
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayChipAdd();

        StartCoroutine(CR_SpawnAndAnimateChip(betOption, amount));
    }

    /// <summary>
    /// FIX: This now ONLY clears chips immediately (no animation).
    /// Call this at round START (before new bets arrive), NOT at cashout time.
    /// At cashout time, call PlayCashoutAnimation() and let it clean up on its own.
    /// </summary>
    internal void ClearAllOpponentBets()

    {
        // If a cashout animation is already running, abort it first cleanly.
        if (cashoutCoroutine != null)
        {
            StopCoroutine(cashoutCoroutine);
            cashoutCoroutine = null;
            isCashoutRunning = false;
        }

        StopAllCoroutines();

        foreach (var chip in activeOpponentChips)
        {
            if (chip != null)
            {
                chip.DOKill();
                Destroy(chip.gameObject);
            }
        }

        activeOpponentChips.Clear();

        foreach (var container in opponentContainers.Values)
            if (container != null) container.gameObject.SetActive(false);

        foreach (var list in chipsByBetArea.Values)
            list.Clear();

        isCashoutRunning = false;
    }
    #endregion

    #region Public API - Cashout Phase
    /// <summary>
    /// FIX: Plays the full cashout animation — chips fly back to both dealer areas,
    /// half each, randomly distributed. Chips are destroyed after they arrive.
    /// Containers are hidden and lists cleared only AFTER the animation finishes.
    /// Do NOT call ClearAllOpponentBets() immediately after this; let it self-clean.
    /// </summary>
    public void PlayCashoutAnimation()
    {
        if (isCashoutRunning)
        {
            Debug.LogWarning("[OpponentChipManager] Cashout already running, ignoring duplicate call.");
            return;
        }

        if (activeOpponentChips.Count == 0)
        {
            Debug.Log("[OpponentChipManager] No opponent chips to cash out.");
            return;
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayChipAdd();

        cashoutCoroutine = StartCoroutine(CR_Cashout());
    }

    public bool IsCashoutRunning() => isCashoutRunning;
    #endregion

    #region Private Methods - Chip Animation
    private IEnumerator CR_SpawnAndAnimateChip(string betOption, double amount)
    {
        RectTransform container = opponentContainers[betOption];

        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>();

        Transform canvasRoot = targetCanvas != null ? targetCanvas.transform : transform.root;

        // 1. Spawn chip at dealer area
        GameObject chipObj = Instantiate(chipPrefab, opponentDealerArea);
        RectTransform chipRT = chipObj.GetComponent<RectTransform>();
        Chip chip = chipObj.GetComponent<Chip>();

        if (chip == null || chipRT == null)
        {
            Debug.LogError("[OpponentChipManager] chipPrefab missing RectTransform or Chip component!");
            Destroy(chipObj);
            yield break;
        }

        chip.SetSprite(grayChipSprite);
        chip.SetAmount(GameUtilities.FormatCurrency(amount));
        chip.SetActive(true);

        chipRT.localPosition = new Vector3(
            Random.Range(-dealerScatterX, dealerScatterX),
            Random.Range(-dealerScatterY, dealerScatterY),
            0f
        );
        chipRT.localScale = Vector3.zero;

        container.gameObject.SetActive(true);

        chipRT.DOScale(chipScale, 0.2f).SetEase(Ease.OutBack);
        yield return new WaitForSeconds(0.22f);

        // Re-parent to canvas root to move across full canvas
        chipRT.SetParent(canvasRoot, worldPositionStays: true);

        // Calculate destination inside the bet area container
        Vector2 containerWorldPos = GetCanvasPosition(container);
        Vector2 destination = containerWorldPos + new Vector2(
            Random.Range(-betAreaScatterX, betAreaScatterX),
            Random.Range(-betAreaScatterY, betAreaScatterY)
        );

        chipRT.DOAnchorPos(destination, dealerToBetDuration).SetEase(Ease.OutQuad);
        yield return new WaitForSeconds(dealerToBetDuration);

        chipRT.SetParent(container, worldPositionStays: true);

        activeOpponentChips.Add(chipRT);
        chipsByBetArea[betOption].Add(chipRT);

        Debug.Log($"[OpponentChipManager] Chip placed for {betOption}. Active chips: {activeOpponentChips.Count}");
    }

    private IEnumerator CR_Cashout()
    {
        isCashoutRunning = true;

        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>();

        Transform canvasRoot = targetCanvas != null ? targetCanvas.transform : transform.root;

        // FIX: Snapshot the list BEFORE any cleanup so we have all chips to animate.
        List<RectTransform> chipsToAnimate = new List<RectTransform>(activeOpponentChips);

        // Re-parent all chips to canvas root so they fly freely.
        foreach (var chip in chipsToAnimate)
        {
            if (chip != null)
                chip.SetParent(canvasRoot, worldPositionStays: true);
        }

        // Shuffle chips for random dealer assignment.
        for (int i = chipsToAnimate.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var temp = chipsToAnimate[i];
            chipsToAnimate[i] = chipsToAnimate[j];
            chipsToAnimate[j] = temp;
        }

        // FIX: Half go to opponentDealerArea, half go to playerDealerArea.
        // If odd count, the extra chip goes to opponentDealerArea.
        int halfCount = chipsToAnimate.Count / 2;

        for (int i = 0; i < chipsToAnimate.Count; i++)
        {
            RectTransform chip = chipsToAnimate[i];
            if (chip == null) continue;

            // First half → opponentDealerArea, second half → playerDealerArea
            RectTransform targetDealer = (i < halfCount) ? playerDealerArea : opponentDealerArea;
            if (targetDealer == null) targetDealer = opponentDealerArea;

            Vector2 targetPos = GetCanvasPosition(targetDealer) + new Vector2(
                Random.Range(-dealerScatterX, dealerScatterX),
                Random.Range(-dealerScatterY, dealerScatterY)
            );

            // Fly chip to dealer, then scale it out.
            chip.DOAnchorPos(targetPos, cashoutDuration).SetEase(Ease.InQuad);
            chip.DOScale(0f, cashoutDuration * 0.6f)
                .SetDelay(cashoutDuration * 0.4f)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    if (chip != null) Destroy(chip.gameObject);
                });

            yield return new WaitForSeconds(cashoutStagger);
        }

        // Wait for the last chip's full animation to finish.
        yield return new WaitForSeconds(cashoutDuration);

        // FIX: Clean up containers and tracking lists AFTER animation is done.
        activeOpponentChips.Clear();

        foreach (var list in chipsByBetArea.Values)
            list.Clear();

        foreach (var container in opponentContainers.Values)
            if (container != null) container.gameObject.SetActive(false);

        isCashoutRunning = false;
        cashoutCoroutine = null;

        Debug.Log("[OpponentChipManager] Cashout animation complete.");
    }
    #endregion

    #region Helpers
    private Vector2 GetCanvasPosition(RectTransform rt)
    {
        if (rt == null || targetCanvas == null) return Vector2.zero;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            targetCanvas.worldCamera, rt.position);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetCanvas.GetComponent<RectTransform>(),
            screenPoint,
            targetCanvas.worldCamera,
            out Vector2 localPoint);

        return localPoint;
    }
    #endregion
}