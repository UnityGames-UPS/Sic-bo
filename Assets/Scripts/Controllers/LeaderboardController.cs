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
    [SerializeField] private float slideDistance = 300f;
    [SerializeField] private float slideDuration = 0.5f;
    [SerializeField] private float minRandomOffset = 0f;
    [SerializeField] private float maxRandomOffset = 2f;

    [Header("Parent Container (Optional)")]
    [SerializeField] private GameObject leaderboardParent;
    #endregion

    #region Private Fields
    private Dictionary<int, LeaderboardEntry> currentRichest = new Dictionary<int, LeaderboardEntry>();
    private Dictionary<int, LeaderboardEntry> currentWinners = new Dictionary<int, LeaderboardEntry>();

    // FIX: Track coroutines per-block instead of one flat list.
    // Key = block instance ID, Value = list of running coroutines for that block.
    private Dictionary<int, List<Coroutine>> blockCoroutines = new Dictionary<int, List<Coroutine>>();

    private bool isInitialized = false;
    #endregion

    #region Unity Lifecycle
    private void OnDestroy()
    {
        StopAllAnimations();
    }
    #endregion

    #region Public API
    public void Initialize()
    {
        Debug.Log("[LeaderboardController] Initialize called");

        foreach (var block in richestBlocks)
            if (block != null) block.HideAll();

        foreach (var block in winnersBlocks)
            if (block != null) block.HideAll();

        currentRichest.Clear();
        currentWinners.Clear();

        if (leaderboardParent != null)
            leaderboardParent.SetActive(false);

        isInitialized = true;
    }

    public void UpdateLeaderboard(Leaderboards leaderboards)
    {
        if (leaderboards == null) return;

        bool hasRichestData = leaderboards.richest != null && leaderboards.richest.Count > 0;
        bool hasWinnersData = leaderboards.winners != null && leaderboards.winners.Count > 0;
        bool hasData = hasRichestData || hasWinnersData;

        if (!hasData)
        {
            Debug.Log("[LeaderboardController] No leaderboard data to display");
            if (leaderboardParent != null) leaderboardParent.SetActive(false);
            return;
        }

        if (leaderboardParent != null && !leaderboardParent.activeSelf)
            leaderboardParent.SetActive(true);

        List<LeaderboardEntry> richestData = leaderboards.richest;
        if (!hasRichestData && hasWinnersData)
            richestData = leaderboards.winners;

        if (richestData != null)
        {
            for (int i = 0; i < 3; i++)
            {
                if (i < richestData.Count)
                    UpdatePlayerBlock(richestBlocks, currentRichest, i, richestData[i], -slideDistance);
                else
                    ClearPlayerBlock(richestBlocks, currentRichest, i);
            }
        }

        List<LeaderboardEntry> winnersData = leaderboards.winners;
        if (!hasWinnersData && hasRichestData)
            winnersData = leaderboards.richest;

        if (winnersData != null)
        {
            for (int i = 0; i < 3; i++)
            {
                if (i < winnersData.Count)
                    UpdatePlayerBlock(winnersBlocks, currentWinners, i, winnersData[i], slideDistance);
                else
                    ClearPlayerBlock(winnersBlocks, currentWinners, i);
            }
        }
    }

    public void Hide()
    {
        if (leaderboardParent != null)
            leaderboardParent.SetActive(false);

        foreach (var block in richestBlocks)
            if (block != null) block.HideAll();

        foreach (var block in winnersBlocks)
            if (block != null) block.HideAll();

        currentRichest.Clear();
        currentWinners.Clear();
        StopAllAnimations();
    }
    #endregion

    #region Private Methods - Block Management
    private void UpdatePlayerBlock(
        List<LeaderboardPlayerBlock> blocks,
        Dictionary<int, LeaderboardEntry> currentData,
        int index,
        LeaderboardEntry newEntry,
        float slideDirection)
    {
        if (index >= blocks.Count || blocks[index] == null) return;

        LeaderboardPlayerBlock block = blocks[index];
        bool isFirstTime = !currentData.ContainsKey(index);
        bool playerChanged = !isFirstTime && currentData[index].username != newEntry.username;

        if (isFirstTime)
        {
            currentData[index] = newEntry;
            block.SetPlayerData(newEntry.username, newEntry.balance, GetRandomAvatar());

            float randomOffset = Random.Range(minRandomOffset, maxRandomOffset);
            AddBlockCoroutine(block, StartCoroutine(DelayedAlternateStart(block, randomOffset)));
        }
        else if (playerChanged)
        {
            currentData[index] = newEntry;

            // FIX: Stop only THIS block's coroutines, not everyone's.
            StopBlockAnimation(block);

            AddBlockCoroutine(block, StartCoroutine(SlideOutAndUpdate(block, newEntry, slideDirection)));
        }
        else
        {
            if (currentData[index].balance != newEntry.balance)
            {
                currentData[index] = newEntry;
                block.UpdateBalance(newEntry.balance);
            }
        }
    }

    private IEnumerator DelayedAlternateStart(LeaderboardPlayerBlock block, float delay)
    {
        yield return new WaitForSeconds(delay);
        AddBlockCoroutine(block, StartCoroutine(AlternateNameBalance(block)));
    }

    private void ClearPlayerBlock(
        List<LeaderboardPlayerBlock> blocks,
        Dictionary<int, LeaderboardEntry> currentData,
        int index)
    {
        if (index >= blocks.Count || blocks[index] == null) return;

        if (currentData.ContainsKey(index))
        {
            currentData.Remove(index);
            LeaderboardPlayerBlock block = blocks[index];
            StopBlockAnimation(block);
            block.HideAll();
        }
    }
    #endregion

    #region Private Methods - Animations
    private IEnumerator SlideOutAndUpdate(
        LeaderboardPlayerBlock block,
        LeaderboardEntry entry,
        float slideDirection)
    {
        RectTransform blockRect = block.GetComponent<RectTransform>();

        if (blockRect != null)
        {
            // FIX: Capture originalPos BEFORE any tween might have already moved it.
            // Kill existing tweens first so anchoredPosition is reliable.
            blockRect.DOKill(complete: true);
            Vector2 originalPos = blockRect.anchoredPosition;
            Vector2 slideOutPos = originalPos + new Vector2(slideDirection, 0);

            yield return blockRect.DOAnchorPos(slideOutPos, slideDuration)
                .SetEase(Ease.InBack)
                .WaitForCompletion();

            // FIX: Reset any stale CanvasGroup state on text elements before setting new data.
            ResetTextState(block.NameText);
            ResetTextState(block.BalanceText);

            block.SetPlayerData(entry.username, entry.balance, GetRandomAvatar());

            yield return blockRect.DOAnchorPos(originalPos, slideDuration)
                .SetEase(Ease.OutBack)
                .WaitForCompletion();
        }
        else
        {
            ResetTextState(block.NameText);
            ResetTextState(block.BalanceText);
            block.SetPlayerData(entry.username, entry.balance, GetRandomAvatar());
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

            if (loopInterval > 0)
                yield return new WaitForSeconds(loopInterval);
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
        float duration = fadeSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            textRect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            canvasGroup.alpha = 1f - t;
            yield return null;
        }

        // FIX: Always reset position back and leave alpha at 0 cleanly.
        textRect.anchoredPosition = startPos;
        canvasGroup.alpha = 0f;
        textComponent.gameObject.SetActive(false);
    }

    private IEnumerator FadeInAtPosition(TMP_Text textComponent)
    {
        if (textComponent == null) yield break;

        CanvasGroup canvasGroup = GetOrAddCanvasGroup(textComponent.gameObject);

        // FIX: Ensure we start from a known clean state.
        canvasGroup.alpha = 0f;
        textComponent.gameObject.SetActive(true);

        float elapsed = 0f;
        float duration = fadeSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = t;
            yield return null;
        }

        canvasGroup.alpha = 1f;
    }

    /// <summary>
    /// FIX: Reset a text element's CanvasGroup and position to a clean visible state
    /// so stale fade-out state from a previous cycle doesn't bleed into the new one.
    /// </summary>
    private void ResetTextState(TMP_Text textComponent)
    {
        if (textComponent == null) return;

        CanvasGroup cg = textComponent.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 1f;

        // Also kill any lingering DOTween on the rect
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

    #region Private Methods - Coroutine Tracking (Per-Block)
    // FIX: All coroutine tracking now keyed by block instance ID so stopping
    // one block's animations never affects any other block.

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
            foreach (var c in coroutines)
                if (c != null) StopCoroutine(c);
            coroutines.Clear();
        }

        // Kill DOTween on the block rect
        RectTransform blockRect = block.GetComponent<RectTransform>();
        if (blockRect != null) blockRect.DOKill(complete: false);
    }

    private void StopAllAnimations()
    {
        foreach (var kvp in blockCoroutines)
        {
            foreach (var c in kvp.Value)
                if (c != null) StopCoroutine(c);
        }
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