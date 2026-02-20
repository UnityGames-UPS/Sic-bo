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
    [SerializeField] private Sprite[] chipSprites;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private RectTransform playerNameTarget;
    [SerializeField] private BetController betController;
    [SerializeField] private GameManager gameManager;

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

    [Header("Win Animation Settings")]
    [SerializeField] private float animationStartPercent = 0.6f;
    [SerializeField] private bool enableWinAnimations = true;
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
        if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
        PreSpawnDealerPool();
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        foreach (var c in dealerPool) { if (c) c.DOKill(); }
        foreach (var c in activeWinChips) { if (c) c.DOKill(); }
    }
    #endregion

    #region Internal API
    internal void PlayDiceResultAnimation(List<WinAreaData> winAreas)
    {
        if (isAnimating || winAreas == null || winAreas.Count == 0) return;
        if (winCoroutine != null) StopCoroutine(winCoroutine);
        winCoroutine = StartCoroutine(CR_DealerToBetAreas(winAreas));
    }

    internal void PlayCashoutAnimation()
    {
        if (cashoutCoroutine != null) StopCoroutine(cashoutCoroutine);
        cashoutCoroutine = StartCoroutine(CR_Cashout());
    }

    internal void ResetAll()
    {
        if (winCoroutine != null) StopCoroutine(winCoroutine);
        if (cashoutCoroutine != null) StopCoroutine(cashoutCoroutine);
        winCoroutine = cashoutCoroutine = null;

        foreach (var chip in dealerPool)
        {
            if (chip == null) continue;
            chip.DOKill();
            if (chip.parent != dealerSpawnPoint) chip.SetParent(dealerSpawnPoint, worldPositionStays: false);
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

    #region Pool
    private void PreSpawnDealerPool()
    {
        if (chipPrefab == null || dealerSpawnPoint == null) return;

        for (int i = 0; i < dealerPoolSize; i++)
        {
            GameObject go = Instantiate(chipPrefab, dealerSpawnPoint);
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) { Destroy(go); continue; }

            rt.localPosition = new Vector3(
                Random.Range(-dealerScatterX, dealerScatterX),
                Random.Range(-dealerScatterY, dealerScatterY), 0f);
            rt.localScale = Vector3.zero;

            if (chipSprites != null && chipSprites.Length > 0)
                SetSprite(rt, Random.Range(2, chipSprites.Length));

            go.SetActive(false);
            dealerPool.Add(rt);
        }
    }
    #endregion

    #region Dealer → Bet Areas
    private IEnumerator CR_DealerToBetAreas(List<WinAreaData> winAreas)
    {
        isAnimating = true;

        Transform canvasRoot = targetCanvas != null ? targetCanvas.transform : dealerSpawnPoint.parent;

        double totalWin = 0;
        foreach (var a in winAreas) totalWin += a.winAmount;

        var assignments = new List<(RectTransform chip, Vector2 dest, WinAreaData area)>();
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

            AudioManager.Instance?.PlayChipAdd();

            for (int i = 0; i < count && poolIdx < dealerPool.Count; i++, poolIdx++)
            {
                RectTransform chip = dealerPool[poolIdx];
                if (chip == null) continue;

                SetSprite(chip, SpriteIndex(area.winAmount));
                chip.gameObject.SetActive(true);
                chip.localPosition = new Vector3(
                    Random.Range(-dealerScatterX, dealerScatterX),
                    Random.Range(-dealerScatterY, dealerScatterY), 0f);
                chip.localScale = Vector3.zero;
                chip.DOScale(chipWorkingScale, 0.18f).SetEase(Ease.OutBack);

                Vector2 dest = GetCanvasPosition(targetRT) + new Vector2(
                    Random.Range(-betAreaScatterX, betAreaScatterX),
                    Random.Range(-betAreaScatterY, betAreaScatterY));

                assignments.Add((chip, dest, area));
                activeWinChips.Add(chip);
            }
        }

        yield return new WaitForSeconds(0.22f);

        foreach (var (chip, _, _) in assignments)
        {
            chip.SetParent(canvasRoot, worldPositionStays: true);
            chip.SetSiblingIndex(0); // render below bet areas so text stays visible
        }

        float chipFlightTime = 0f;
        float animationTriggerTime = dealerToBetDuration * animationStartPercent;
        bool animationsTriggered = false;

        foreach (var (chip, dest, area) in assignments)
        {
            chip.DOAnchorPos(dest, dealerToBetDuration).SetEase(Ease.OutQuad);
            yield return new WaitForSeconds(chipStaggerDelay);
            chipFlightTime += chipStaggerDelay;

            if (enableWinAnimations && !animationsTriggered && chipFlightTime >= animationTriggerTime)
            {
                animationsTriggered = true;
                TriggerAllWinCountingAnimations(winAreas);
            }
        }

        yield return new WaitForSeconds(dealerToBetDuration);

        isAnimating = false;
        winCoroutine = null;
    }

    private void TriggerAllWinCountingAnimations(List<WinAreaData> winAreas)
    {
        if (betController == null) return;

        foreach (var winArea in winAreas)
        {
            PlayerBetComponent playerBetComp = betController.GetPlayerBetComponent(winArea.betOption);
            if (playerBetComp == null) continue;
            if (winArea.betAmount <= 0) continue;

            // Use actual win/bet ratio from WinAreaData directly.
            // Fixes small/big (1:1), single dice (always returned wrong single_match_1 wager),
            // and specific_3 (mismatched second lookup). WinAreaData already has correct computed values.
            double ratio = winArea.winAmount / winArea.betAmount;
            playerBetComp.AnimateWinWithRatio(ratio);
        }
    }
    #endregion

    #region Bet Areas → Player (Cashout)
    private IEnumerator CR_Cashout()
    {
        if (playerNameTarget == null) yield break;

        Transform canvasRoot = targetCanvas != null ? targetCanvas.transform : dealerSpawnPoint.parent;
        var toSweep = new List<RectTransform>(activeWinChips);

        int extraNeeded = Mathf.Min(3, dealerPool.Count);
        var extraChips = new List<RectTransform>();
        foreach (var chip in dealerPool)
        {
            if (extraNeeded <= 0) break;
            if (activeWinChips.Contains(chip)) continue;

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

        foreach (var chip in extraChips)
        {
            chip.SetParent(canvasRoot, worldPositionStays: true);
            chip.SetSiblingIndex(0); // render below bet areas so text stays visible
            toSweep.Add(chip);
        }

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
                .Join(chip.DOScale(Vector3.zero, landDur).SetDelay(halfDur).SetEase(Ease.InBack))
                .OnComplete(() =>
                {
                    if (chip == null) return;
                    AudioManager.Instance?.PlayChipAdd();
                    chip.gameObject.SetActive(false);
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
    private Vector2 GetCanvasPosition(RectTransform rt)
    {
        if (rt == null || targetCanvas == null) return Vector2.zero;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(targetCanvas.worldCamera, rt.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetCanvas.GetComponent<RectTransform>(), screenPoint, targetCanvas.worldCamera, out Vector2 localPoint);
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
    #endregion
}

[System.Serializable]
internal class WinAreaData
{
    internal string betOption;
    internal Transform betAreaTarget;
    internal double betAmount;
    internal double winAmount;
}