using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LeaderboardController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Richest Blocks (Left Side)")]
    [SerializeField] private List<LeaderboardPlayerBlock> richestBlocks = new List<LeaderboardPlayerBlock>(3);

    [Header("Winners Blocks (Right Side)")]
    [SerializeField] private List<LeaderboardPlayerBlock> winnersBlocks = new List<LeaderboardPlayerBlock>(3);

    [Header("Avatar Images (Random Selection)")]
    [SerializeField] private Sprite[] playerAvatars;

    [Header("Animation Settings")]
    [SerializeField] private float nameDuration = 2f;
    [SerializeField] private float balanceDuration = 2f;
    [SerializeField] private float fadeSpeed = 0.3f;
    [SerializeField] private float loopInterval = 0f;

    [Tooltip("Cards slide exactly this many units. Richest: -300 <-> 0   Winners: 0 <-> +300")]
    [SerializeField] private float slideDistance = 300f;
    [SerializeField] private float slideDuration = 0.5f;

    [SerializeField] private float minRandomOffset = 0f;
    [SerializeField] private float maxRandomOffset = 2f;

    [Header("Dummy / Placeholder Data")]
    [SerializeField] private string dummyUsername = "---";
    [SerializeField] private double dummyBalance = 0.00;
    [SerializeField] private double dummyWins = 0.00;

    [Header("Parent Container (Optional)")]
    [SerializeField] private GameObject leaderboardParent;
    #endregion

    #region Private Fields
    private Dictionary<int, LeaderboardEntry> currentRichest = new Dictionary<int, LeaderboardEntry>();
    private Dictionary<int, LeaderboardEntry> currentWinners = new Dictionary<int, LeaderboardEntry>();
    private Dictionary<int, List<Coroutine>> blockCoroutines = new Dictionary<int, List<Coroutine>>();
    #endregion

    #region Dummy Data Helpers
    private LeaderboardEntry MakeDummy(int rank) => new LeaderboardEntry
    {
        username = dummyUsername,
        balance = dummyBalance,
        totalWins = dummyWins,
        rank = rank
    };

    private List<LeaderboardEntry> PadToThree(List<LeaderboardEntry> source)
    {
        var result = source != null
            ? new List<LeaderboardEntry>(source)
            : new List<LeaderboardEntry>();

        while (result.Count < 3)
            result.Add(MakeDummy(result.Count + 1));

        return result;
    }
    #endregion

    #region Unity Lifecycle
    private void OnDestroy() => StopAllAnimations();
    #endregion

    #region Public API
    public void Initialize()
    {
        Debug.Log("[LeaderboardController] Initialize called");

        foreach (var b in richestBlocks) if (b != null) b.HideAll();
        foreach (var b in winnersBlocks) if (b != null) b.HideAll();

        currentRichest.Clear();
        currentWinners.Clear();
        StopAllAnimations();

        if (leaderboardParent != null) leaderboardParent.SetActive(false);
    }

    public void UpdateLeaderboard(Leaderboards leaderboards)
    {
        List<LeaderboardEntry> richestData = PadToThree(leaderboards?.richest);
        List<LeaderboardEntry> winnersData = PadToThree(leaderboards?.winners);

        if (leaderboardParent != null && !leaderboardParent.activeSelf)
            leaderboardParent.SetActive(true);

        // Richest (left panel)  — slideDir = -1  →  off-screen position is at x - 300
        for (int i = 0; i < 3; i++)
            UpdatePlayerBlock(richestBlocks, currentRichest, i, richestData[i],
                              slideDir: -1f, isWinners: false);

        // Winners (right panel) — slideDir = +1  →  off-screen position is at x + 300
        for (int i = 0; i < 3; i++)
            UpdatePlayerBlock(winnersBlocks, currentWinners, i, winnersData[i],
                              slideDir: 1f, isWinners: true);
    }

    public void Hide()
    {
        if (leaderboardParent != null) leaderboardParent.SetActive(false);

        foreach (var b in richestBlocks) if (b != null) b.HideAll();
        foreach (var b in winnersBlocks) if (b != null) b.HideAll();

        currentRichest.Clear();
        currentWinners.Clear();
        StopAllAnimations();
    }
    #endregion

    #region Block Management
    /// <param name="slideDir">
    ///   -1 = Richest (left)   entry: x-300 → x    exit: x → x-300
    ///   +1 = Winners (right)  entry: x+300 → x    exit: x → x+300
    /// </param>
    private void UpdatePlayerBlock(
        List<LeaderboardPlayerBlock> blocks,
        Dictionary<int, LeaderboardEntry> currentData,
        int index,
        LeaderboardEntry newEntry,
        float slideDir,
        bool isWinners)
    {
        if (index >= blocks.Count || blocks[index] == null) return;

        LeaderboardPlayerBlock block = blocks[index];
        bool isFirstTime = !currentData.ContainsKey(index);
        bool playerChanged = !isFirstTime && currentData[index].username != newEntry.username;

        double displayValue = isWinners ? newEntry.totalWins : newEntry.balance;

        if (isFirstTime)
        {
            // No animation on first appearance — just show immediately
            currentData[index] = newEntry;
            block.SetPlayerData(newEntry.username, displayValue, GetRandomAvatar());

            float offset = Random.Range(minRandomOffset, maxRandomOffset);
            AddBlockCoroutine(block, StartCoroutine(DelayedAlternateStart(block, offset)));
        }
        else if (playerChanged)
        {
            currentData[index] = newEntry;
            StopBlockAnimation(block);
            AddBlockCoroutine(block,
                StartCoroutine(SlideOutAndUpdate(block, newEntry, slideDir, displayValue)));
        }
        else
        {
            double prev = isWinners ? currentData[index].totalWins : currentData[index].balance;
            if (System.Math.Abs(prev - displayValue) > 0.001)
            {
                currentData[index] = newEntry;
                block.UpdateBalance(displayValue);
            }
        }
    }

    private IEnumerator DelayedAlternateStart(LeaderboardPlayerBlock block, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        AddBlockCoroutine(block, StartCoroutine(AlternateNameBalance(block)));
    }
    #endregion

    #region Animations

    /// <summary>
    /// Slide behaviour (no easing overshoot — pure linear in, quad out):
    ///
    ///   Richest (slideDir = -1):
    ///     EXIT  → tween from x  to  x - 300        (slides LEFT off screen)
    ///     ENTRY → jump to x - 300, tween to x       (slides in from LEFT)
    ///
    ///   Winners (slideDir = +1):
    ///     EXIT  → tween from x  to  x + 300        (slides RIGHT off screen)
    ///     ENTRY → jump to x + 300, tween to x       (slides in from RIGHT)
    /// </summary>
    private IEnumerator SlideOutAndUpdate(
        LeaderboardPlayerBlock block,
        LeaderboardEntry entry,
        float slideDir,
        double displayValue)
    {
        RectTransform blockRect = block.GetComponent<RectTransform>();

        if (blockRect != null)
        {
            blockRect.DOKill(complete: true);
            Vector2 restPos = blockRect.anchoredPosition;

            // ── EXIT: slide out in the SAME direction as entry origin ──────────
            // richest: restPos → restPos - 300
            // winners: restPos → restPos + 300
            Vector2 offScreenPos = restPos + new Vector2(slideDir * slideDistance, 0f);

            yield return blockRect
                .DOAnchorPos(offScreenPos, slideDuration)
                .SetEase(Ease.InQuad)          // accelerate out, no bounce
                .WaitForCompletion();

            // ── SWAP content while card is fully off-screen ────────────────────
            ResetTextState(block.NameText);
            ResetTextState(block.BalanceText);
            block.SetPlayerData(entry.username, displayValue, GetRandomAvatar());

            // ── ENTRY: already at offScreenPos, tween back to restPos ──────────
            // richest: restPos - 300 → restPos
            // winners: restPos + 300 → restPos
            // (no need to teleport — we never moved restPos itself)

            yield return blockRect
                .DOAnchorPos(restPos, slideDuration)
                .SetEase(Ease.OutQuad)         // decelerate in, no bounce
                .WaitForCompletion();
        }
        else
        {
            ResetTextState(block.NameText);
            ResetTextState(block.BalanceText);
            block.SetPlayerData(entry.username, displayValue, GetRandomAvatar());
        }

        float randomOffset = Random.Range(minRandomOffset, maxRandomOffset);
        yield return new WaitForSeconds(randomOffset);
        AddBlockCoroutine(block, StartCoroutine(AlternateNameBalance(block)));
    }

    private IEnumerator AlternateNameBalance(LeaderboardPlayerBlock block)
    {
        while (true)
        {
            block.ShowName();
            yield return new WaitForSeconds(nameDuration);

            StartCoroutine(FadeOutUp(block.NameText));
            yield return StartCoroutine(FadeInAtPosition(block.BalanceText));

            yield return new WaitForSeconds(balanceDuration);

            StartCoroutine(FadeOutUp(block.BalanceText));
            yield return StartCoroutine(FadeInAtPosition(block.NameText));

            if (loopInterval > 0f) yield return new WaitForSeconds(loopInterval);
        }
    }

    private IEnumerator FadeOutUp(TMP_Text textComponent)
    {
        if (textComponent == null) yield break;

        RectTransform textRect = textComponent.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = GetOrAddCanvasGroup(textComponent.gameObject);
        Vector2 startPos = textRect.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(0, 30f);
        float elapsed = 0f;

        while (elapsed < fadeSpeed)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeSpeed);
            textRect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            canvasGroup.alpha = 1f - t;
            yield return null;
        }

        textRect.anchoredPosition = startPos;
        canvasGroup.alpha = 0f;
        textComponent.gameObject.SetActive(false);
    }

    private IEnumerator FadeInAtPosition(TMP_Text textComponent)
    {
        if (textComponent == null) yield break;

        CanvasGroup canvasGroup = GetOrAddCanvasGroup(textComponent.gameObject);
        canvasGroup.alpha = 0f;
        textComponent.gameObject.SetActive(true);
        float elapsed = 0f;

        while (elapsed < fadeSpeed)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeSpeed);
            yield return null;
        }

        canvasGroup.alpha = 1f;
    }

    private void ResetTextState(TMP_Text textComponent)
    {
        if (textComponent == null) return;
        CanvasGroup cg = textComponent.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 1f;
        RectTransform rt = textComponent.GetComponent<RectTransform>();
        if (rt != null) rt.DOKill(complete: false);
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }
    #endregion

    #region Coroutine Tracking (Per-Block)
    private void AddBlockCoroutine(LeaderboardPlayerBlock block, Coroutine coroutine)
    {
        if (block == null || coroutine == null) return;
        int id = block.GetInstanceID();
        if (!blockCoroutines.ContainsKey(id))
            blockCoroutines[id] = new List<Coroutine>();
        blockCoroutines[id].Add(coroutine);
    }

    private void StopBlockAnimation(LeaderboardPlayerBlock block)
    {
        if (block == null) return;
        int id = block.GetInstanceID();

        if (blockCoroutines.TryGetValue(id, out List<Coroutine> coroutines))
        {
            foreach (var c in coroutines) if (c != null) StopCoroutine(c);
            coroutines.Clear();
        }

        RectTransform blockRect = block.GetComponent<RectTransform>();
        if (blockRect != null) blockRect.DOKill(complete: false);
    }

    private void StopAllAnimations()
    {
        foreach (var kvp in blockCoroutines)
            foreach (var c in kvp.Value)
                if (c != null) StopCoroutine(c);

        blockCoroutines.Clear();
        DOTween.Kill(this);
    }
    #endregion

    #region Helpers
    private Sprite GetRandomAvatar()
    {
        if (playerAvatars == null || playerAvatars.Length == 0) return null;
        return playerAvatars[Random.Range(0, playerAvatars.Length)];
    }
    #endregion
}