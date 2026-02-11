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
    [SerializeField] private GameObject chipPrefab;            // Regular chip prefab with Chip.cs
    [SerializeField] private Sprite grayChipSprite;            // Gray sprite for opponent chips

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
    // betOption -> Container for opponent chips in that bet area
    private Dictionary<string, RectTransform> opponentContainers = new Dictionary<string, RectTransform>();

    // All active opponent chips (for cleanup and cashout)
    private List<RectTransform> activeOpponentChips = new List<RectTransform>();

    // Track chips per bet area for cashout
    private Dictionary<string, List<RectTransform>> chipsByBetArea = new Dictionary<string, List<RectTransform>>();
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>();
    }

    private void OnDestroy()
    {
        foreach (var chip in activeOpponentChips)
        {
            if (chip != null) chip.DOKill();
        }
    }
    #endregion

    #region Public API - Setup
    /// <summary>
    /// Call this at initialization to create empty containers in each bet area
    /// Pass in the bet area transforms where opponent chips should appear
    /// </summary>
    public void InitializeContainers(Dictionary<string, Transform> betAreaMap)
    {
        opponentContainers.Clear();

        foreach (var kvp in betAreaMap)
        {
            string betOption = kvp.Key;
            Transform betAreaTransform = kvp.Value;

            if (betAreaTransform == null) continue;

            // Create empty stretched container
            GameObject containerObj = new GameObject($"OpponentChipContainer_{betOption}");
            RectTransform container = containerObj.AddComponent<RectTransform>();

            // Parent to bet area
            container.SetParent(betAreaTransform, false);

            // Stretch to fill (anchors at corners, offsets zero)
            container.anchorMin = Vector2.zero;
            container.anchorMax = Vector2.one;
            container.offsetMin = Vector2.zero;
            container.offsetMax = Vector2.zero;
            container.localScale = Vector3.one;

            containerObj.SetActive(false); // Hidden until chips spawn

            opponentContainers[betOption] = container;
            chipsByBetArea[betOption] = new List<RectTransform>();
        }

        Debug.Log($"[OpponentChipManager] Initialized {opponentContainers.Count} containers");
    }
    #endregion

    #region Public API - Betting Phase
    /// <summary>
    /// Called when opponent bet broadcast is received
    /// Spawns chip at opponent dealer, animates to bet area container
    /// </summary>
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

        StartCoroutine(CR_SpawnAndAnimateChip(betOption, amount));
    }

    /// <summary>
    /// Clear all opponent bets (called at round start)
    /// </summary>
    public void ClearAllOpponentBets()
    {
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
        {
            if (container != null)
                container.gameObject.SetActive(false);
        }

        foreach (var list in chipsByBetArea.Values)
        {
            list.Clear();
        }
    }
    #endregion

    #region Public API - Cashout Phase
    /// <summary>
    /// Called at cashout - moves all opponent chips randomly to dealer areas
    /// Half go to opponent dealer, half to player dealer
    /// </summary>
    public void PlayCashoutAnimation()
    {
        StartCoroutine(CR_Cashout());
    }
    #endregion

    #region Private Methods - Chip Animation
    private IEnumerator CR_SpawnAndAnimateChip(string betOption, double amount)
    {
        RectTransform container = opponentContainers[betOption];
        Transform canvasRoot = targetCanvas != null ? targetCanvas.transform : opponentDealerArea.parent;

        // 1. Spawn chip at dealer area
        GameObject chipObj = Instantiate(chipPrefab, opponentDealerArea);
        RectTransform chipRT = chipObj.GetComponent<RectTransform>();
        Chip chip = chipObj.GetComponent<Chip>();

        if (chip == null || chipRT == null)
        {
            Destroy(chipObj);
            yield break;
        }

        // Configure chip
        chip.SetSprite(grayChipSprite);
        chip.SetAmount(GameUtilities.FormatCurrency(amount));
        chip.SetActive(true);

        // Random scatter at dealer
        chipRT.localPosition = new Vector3(
            Random.Range(-dealerScatterX, dealerScatterX),
            Random.Range(-dealerScatterY, dealerScatterY),
            0f
        );
        chipRT.localScale = Vector3.zero;

        // Pop in
        chipRT.DOScale(chipScale, 0.2f).SetEase(Ease.OutBack);
        yield return new WaitForSeconds(0.22f);

        // 2. Re-parent to canvas root to move freely
        chipRT.SetParent(canvasRoot, worldPositionStays: true);

        // 3. Calculate destination (inside container, with scatter)
        Vector2 containerWorldPos = GetCanvasPosition(container);
        Vector2 destination = containerWorldPos + new Vector2(
            Random.Range(-betAreaScatterX, betAreaScatterX),
            Random.Range(-betAreaScatterY, betAreaScatterY)
        );

        // 4. Animate to bet area
        chipRT.DOAnchorPos(destination, dealerToBetDuration).SetEase(Ease.OutQuad);
        yield return new WaitForSeconds(dealerToBetDuration);

        // 5. Re-parent to container (keeps visual position)
        chipRT.SetParent(container, worldPositionStays: true);

        // Activate container
        container.gameObject.SetActive(true);

        // Track chip
        activeOpponentChips.Add(chipRT);
        chipsByBetArea[betOption].Add(chipRT);
    }

    private IEnumerator CR_Cashout()
    {
        if (activeOpponentChips.Count == 0) yield break;

        Transform canvasRoot = targetCanvas != null ? targetCanvas.transform : opponentDealerArea.parent;

        // Re-parent all chips to canvas root so they can fly freely
        foreach (var chip in activeOpponentChips)
        {
            if (chip != null)
                chip.SetParent(canvasRoot, worldPositionStays: true);
        }

        // Shuffle chips
        List<RectTransform> shuffledChips = new List<RectTransform>(activeOpponentChips);
        for (int i = shuffledChips.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var temp = shuffledChips[i];
            shuffledChips[i] = shuffledChips[j];
            shuffledChips[j] = temp;
        }

        // Half to opponent dealer, half to player dealer
        int halfCount = shuffledChips.Count / 2;

        for (int i = 0; i < shuffledChips.Count; i++)
        {
            RectTransform chip = shuffledChips[i];
            if (chip == null) continue;

            // Determine target dealer
            RectTransform targetDealer = (i < halfCount) ? opponentDealerArea : playerDealerArea;
            if (targetDealer == null) targetDealer = opponentDealerArea;

            Vector2 targetPos = GetCanvasPosition(targetDealer) + new Vector2(
                Random.Range(-dealerScatterX, dealerScatterX),
                Random.Range(-dealerScatterY, dealerScatterY)
            );

            // Animate to dealer with scale down
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

        yield return new WaitForSeconds(cashoutDuration);

        // Cleanup
        activeOpponentChips.Clear();
        foreach (var list in chipsByBetArea.Values)
        {
            list.Clear();
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Convert RectTransform world position to canvas-space anchoredPosition
    /// </summary>
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