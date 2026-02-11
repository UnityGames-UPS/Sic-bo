using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Real-money casino style chip win animation.
///
/// FLOW:
///   PreSpawnDealerPool() → Called at Start: spawns 20–30 chips at dealer area, all hidden (scale 0).
///
///   PlayDiceResultAnimation(winAreas) → Called after dice result if player won:
///       Pops a proportional number of chips from the dealer pool, slides them to
///       each winning bet area with a stagger. These chips sit on top of the table chips.
///
///   PlayCashoutAnimation() → Called at cashout event:
///       All chips on bet areas (win chips + a few extra representing the player's placed bet)
///       arc gracefully to the player name / balance target, scale down and vanish.
///
///   ResetAll() → Called at round start to hide everything cleanly.
/// </summary>
public class ChipWinAnimationController : MonoBehaviour
{
    #region Serialized Fields
    [Header("References")]
    [SerializeField] private RectTransform dealerSpawnPoint;
    [SerializeField] private GameObject chipPrefab;
    [SerializeField] private Sprite[] chipSprites;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private RectTransform playerNameTarget;

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
    [SerializeField] private float chipWorkingScale = 0.85f;
    #endregion

    #region Private Fields
    private readonly List<RectTransform> dealerPool = new List<RectTransform>();
    private readonly List<RectTransform> activeWinChips = new List<RectTransform>();
    private bool isAnimating;
    private Coroutine winCoroutine;
    private Coroutine cashoutCoroutine;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>();
        ValidateReferences();
        PreSpawnDealerPool();
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        foreach (var c in dealerPool) { if (c) c.DOKill(); }
        foreach (var c in activeWinChips) { if (c) c.DOKill(); }
    }
    #endregion

    #region Public API
    /// <summary>Call after dice result is shown IF player has winning bets.</summary>
    public void PlayDiceResultAnimation(List<WinAreaData> winAreas)
    {
        if (isAnimating || winAreas == null || winAreas.Count == 0) return;
        if (winCoroutine != null) StopCoroutine(winCoroutine);
        winCoroutine = StartCoroutine(CR_DealerToBetAreas(winAreas));
    }

    /// <summary>Call at cashout event to sweep all chips to the player balance area.</summary>
    public void PlayCashoutAnimation()
    {
        if (cashoutCoroutine != null) StopCoroutine(cashoutCoroutine);
        cashoutCoroutine = StartCoroutine(CR_Cashout());
    }

    /// <summary>Hard reset. Call at round start.</summary>
    public void ResetAll()
    {
        if (winCoroutine != null) StopCoroutine(winCoroutine);
        if (cashoutCoroutine != null) StopCoroutine(cashoutCoroutine);
        winCoroutine = cashoutCoroutine = null;

        foreach (var chip in dealerPool)
        {
            if (chip == null) continue;
            chip.DOKill();

            // Return stray chips (moved to canvas root during animation) back to dealer
            if (chip.parent != dealerSpawnPoint)
                chip.SetParent(dealerSpawnPoint, worldPositionStays: false);

            chip.localPosition = new Vector3(
                Random.Range(-dealerScatterX, dealerScatterX),
                Random.Range(-dealerScatterY, dealerScatterY), 0f);
            chip.localScale = Vector3.zero;
            chip.gameObject.SetActive(false);
        }
        activeWinChips.Clear();
        isAnimating = false;
    }
    #endregion

    #region Phase 1 – Pre-Spawn Dealer Pool
    private void PreSpawnDealerPool()
    {
        if (chipPrefab == null || dealerSpawnPoint == null) return;

        for (int i = 0; i < dealerPoolSize; i++)
        {
            // Parent directly inside the dealer object – chips live at the dealer position,
            // not scattered across the root canvas.
            GameObject go = Instantiate(chipPrefab, dealerSpawnPoint);
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) { Destroy(go); continue; }

            // Small local scatter so they look like a natural stacked pile
            rt.localPosition = new Vector3(
                Random.Range(-dealerScatterX, dealerScatterX),
                Random.Range(-dealerScatterY, dealerScatterY),
                0f);
            rt.localScale = Vector3.zero; // invisible until animation starts

            if (chipSprites != null && chipSprites.Length > 0)
                SetSprite(rt, Random.Range(2, chipSprites.Length));

            go.SetActive(false);
            dealerPool.Add(rt);
        }
    }
    #endregion

    #region Phase 2 – Dealer → Bet Areas
    private IEnumerator CR_DealerToBetAreas(List<WinAreaData> winAreas)
    {
        isAnimating = true;

        // Canvas root needed so chips can travel anywhere on screen
        Transform canvasRoot = targetCanvas != null ? targetCanvas.transform : dealerSpawnPoint.parent;

        // How many chips per area (proportional to win, clamped 1–6)
        double totalWin = 0;
        foreach (var a in winAreas) totalWin += a.winAmount;

        var assignments = new List<(RectTransform chip, Vector2 dest)>();
        int poolIdx = 0;

        foreach (var area in winAreas)
        {
            if (area.betAreaTarget == null) continue;
            RectTransform targetRT = area.betAreaTarget as RectTransform
                                     ?? area.betAreaTarget.GetComponent<RectTransform>();
            if (targetRT == null) continue;

            int count = totalWin > 0
                ? Mathf.Clamp(Mathf.RoundToInt((float)(area.winAmount / totalWin) * 10f), 1, 6)
                : 1;

            for (int i = 0; i < count && poolIdx < dealerPool.Count; i++, poolIdx++)
            {
                RectTransform chip = dealerPool[poolIdx];
                if (chip == null) continue;

                SetSprite(chip, SpriteIndex(area.winAmount));

                // Activate while still a child of dealerSpawnPoint – pop in there
                chip.gameObject.SetActive(true);
                chip.localPosition = new Vector3(
                    Random.Range(-dealerScatterX, dealerScatterX),
                    Random.Range(-dealerScatterY, dealerScatterY), 0f);
                chip.localScale = Vector3.zero;
                chip.DOScale(chipWorkingScale, 0.18f).SetEase(Ease.OutBack);

                // Destination: canvas-space position of the target bet area + scatter
                Vector2 dest = GetCanvasPosition(targetRT) + new Vector2(
                    Random.Range(-betAreaScatterX, betAreaScatterX),
                    Random.Range(-betAreaScatterY, betAreaScatterY));

                assignments.Add((chip, dest));
                activeWinChips.Add(chip);
            }
        }

        // Let pop-in settle
        yield return new WaitForSeconds(0.22f);

        // Re-parent to canvas root BEFORE sliding (worldPositionStays preserves screen position)
        foreach (var (chip, _) in assignments)
            chip.SetParent(canvasRoot, worldPositionStays: true);

        // Slide to bet areas with stagger
        foreach (var (chip, dest) in assignments)
        {
            chip.DOAnchorPos(dest, dealerToBetDuration).SetEase(Ease.OutQuad);
            yield return new WaitForSeconds(chipStaggerDelay);
        }

        yield return new WaitForSeconds(dealerToBetDuration);
        isAnimating = false;
        winCoroutine = null;
    }
    #endregion

    #region Phase 3 – Bet Areas → Player (Cashout)
    private IEnumerator CR_Cashout()
    {
        if (playerNameTarget == null) yield break;

        Transform canvasRoot = targetCanvas != null ? targetCanvas.transform : dealerSpawnPoint.parent;

        // Chips already on the table (win chips, already parented to canvas root)
        var toSweep = new List<RectTransform>(activeWinChips);

        // A few extra chips from the dealer pool represent the player's placed bet returning
        int extraNeeded = Mathf.Min(3, dealerPool.Count);
        var extraChips = new List<RectTransform>();
        foreach (var chip in dealerPool)
        {
            if (extraNeeded <= 0) break;
            if (activeWinChips.Contains(chip)) continue;

            // Pop in while still a child of dealerSpawnPoint
            chip.gameObject.SetActive(true);
            chip.localPosition = new Vector3(
                Random.Range(-dealerScatterX, dealerScatterX),
                Random.Range(-dealerScatterY, dealerScatterY), 0f);
            chip.localScale = Vector3.zero;
            chip.DOScale(chipWorkingScale * 0.70f, 0.14f).SetEase(Ease.OutBack);

            extraChips.Add(chip);
            extraNeeded--;
        }

        yield return new WaitForSeconds(0.18f);

        // Re-parent extras to canvas root so they can fly freely
        foreach (var chip in extraChips)
        {
            chip.SetParent(canvasRoot, worldPositionStays: true);
            toSweep.Add(chip);
        }

        // Target is the player name / balance area in canvas space
        Vector2 playerPos = GetCanvasPosition(playerNameTarget);

        foreach (var chip in toSweep)
        {
            if (chip == null) continue;

            Vector2 start = chip.anchoredPosition;
            Vector2 mid = Vector2.Lerp(start, playerPos, 0.5f)
                            + new Vector2(Random.Range(-18f, 18f), arcHeight);

            float halfDur = betToPlayerDuration * 0.45f;
            float landDur = betToPlayerDuration * 0.55f;

            DOTween.Sequence()
                .Append(chip.DOAnchorPos(mid, halfDur).SetEase(Ease.OutQuad))
                .Append(chip.DOAnchorPos(playerPos, landDur).SetEase(Ease.InQuad))
                .Join(chip.DOScale(Vector3.zero, landDur)
                          .SetDelay(halfDur)
                          .SetEase(Ease.InBack))
                .OnComplete(() =>
                {
                    if (chip == null) return;
                    chip.gameObject.SetActive(false);
                    // Return chip to dealer pool parent so ResetAll works correctly
                    chip.SetParent(dealerSpawnPoint, worldPositionStays: false);
                    chip.localPosition = Vector3.zero;
                });

            yield return new WaitForSeconds(cashoutStagger);
        }

        yield return new WaitForSeconds(betToPlayerDuration + 0.25f);

        activeWinChips.Clear();
        cashoutCoroutine = null;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Converts a RectTransform's world position into an anchoredPosition relative to
    /// the root canvas, so DOAnchorPos works correctly regardless of parent hierarchy.
    /// </summary>
    private Vector2 GetCanvasPosition(RectTransform rt)
    {
        if (rt == null || targetCanvas == null) return Vector2.zero;

        // Convert to screen point then back to canvas local point
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            targetCanvas.worldCamera, rt.position);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetCanvas.GetComponent<RectTransform>(),
            screenPoint,
            targetCanvas.worldCamera,
            out Vector2 localPoint);

        return localPoint;
    }

    private void SetSprite(RectTransform rt, int index)
    {
        if (chipSprites == null || chipSprites.Length == 0) return;
        var img = rt.GetComponent<UnityEngine.UI.Image>();
        if (img != null) img.sprite = chipSprites[Mathf.Clamp(index, 0, chipSprites.Length - 1)];
    }

    private int SpriteIndex(double winAmount)
    {
        if (winAmount >= 500) return 0;
        if (winAmount >= 100) return 1;
        if (winAmount >= 50) return 2;
        if (winAmount >= 10) return 3;
        if (winAmount >= 5) return 4;
        return chipSprites != null ? Mathf.Min(5, chipSprites.Length - 1) : 0;
    }

    private void ValidateReferences()
    {
        if (dealerSpawnPoint == null) Debug.LogError("[ChipWinAnimation] dealerSpawnPoint not assigned!");
        if (chipPrefab == null) Debug.LogError("[ChipWinAnimation] chipPrefab not assigned!");
        if (playerNameTarget == null) Debug.LogError("[ChipWinAnimation] playerNameTarget not assigned!");
    }
    #endregion
}


/// <summary>Data for a single winning bet area, built in BetController.</summary>
[System.Serializable]
public class WinAreaData
{
    public string betOption;
    public Transform betAreaTarget;
    public double betAmount;
    public double winAmount;
}