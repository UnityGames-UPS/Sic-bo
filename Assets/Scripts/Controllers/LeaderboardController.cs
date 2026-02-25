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

    [Header("Position Badges")]
    [SerializeField] private Sprite[] richestPositionBadges = new Sprite[3]; // 1st, 2nd, 3rd
    [SerializeField] private Sprite[] winnersPositionBadges = new Sprite[3]; // 1st, 2nd, 3rd

    [Header("Animation Settings")]
    [SerializeField] private float nameDuration = 2f;
    [SerializeField] private float balanceDuration = 2f;
    [SerializeField] private float fadeSpeed = 0.3f;
    [SerializeField] private float loopInterval = 0f;
    [SerializeField] private float slideDistance = 300f;
    [SerializeField] private float slideDuration = 0.3f; // Faster from 0.5f
    [SerializeField] private float interchangeDuration = 0.4f; // For position swaps
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
    private string localPlayerUsername = null;
    private Sprite localPlayerAvatar = null;
    private bool isAnimating = false;
    private Dictionary<RectTransform, Vector2> originalPositions = new Dictionary<RectTransform, Vector2>();
    #endregion

    #region Internal API — Local Player
    internal void SetLocalPlayer(string username, Sprite avatar)
    {
        localPlayerUsername = username;
        localPlayerAvatar = avatar;
    }

    /// <summary>
    /// Gets the RectTransform of a player in the leaderboard by username.
    /// Returns null if player not found.
    /// </summary>
    internal RectTransform GetPlayerPosition(string username, bool checkWinners)
    {
        if (string.IsNullOrEmpty(username)) return null;

        var blocks = checkWinners ? winnersBlocks : richestBlocks;
        var currentData = checkWinners ? currentWinners : currentRichest;

        for (int i = 0; i < blocks.Count; i++)
        {
            if (currentData.ContainsKey(i) && currentData[i].username == username)
            {
                return blocks[i]?.GetComponent<RectTransform>();
            }
        }

        return null;
    }

    internal bool IsAnimating() => isAnimating;

    internal IEnumerator WaitForAnimationComplete()
    {
        while (isAnimating)
        {
            yield return null;
        }
    }
    #endregion

    #region Unity Lifecycle
    private void OnDestroy() => StopAllAnimations();
    #endregion

    #region Internal API
    internal void Initialize()
    {
        foreach (var b in richestBlocks) b?.HideAll();
        foreach (var b in winnersBlocks) b?.HideAll();

        currentRichest.Clear();
        currentWinners.Clear();
        StopAllAnimations();

        // Store original positions
        originalPositions.Clear();
        foreach (var block in richestBlocks)
        {
            if (block != null)
            {
                RectTransform rt = block.GetComponent<RectTransform>();
                if (rt != null)
                {
                    originalPositions[rt] = rt.anchoredPosition;
                }
            }
        }
        foreach (var block in winnersBlocks)
        {
            if (block != null)
            {
                RectTransform rt = block.GetComponent<RectTransform>();
                if (rt != null)
                {
                    originalPositions[rt] = rt.anchoredPosition;
                }
            }
        }

        if (leaderboardParent != null) leaderboardParent.SetActive(false);
    }

    internal void UpdateLeaderboard(Leaderboards leaderboards)
    {
        isAnimating = true;

        var richestData = PadToThree(leaderboards?.richest);
        var winnersData = PadToThree(leaderboards?.winners);

        if (leaderboardParent != null && !leaderboardParent.activeSelf)
            leaderboardParent.SetActive(true);

        // Check for cascade scenarios (multiple position changes)
        bool richestCascade = DetectCascade(currentRichest, richestData);
        bool winnersCascade = DetectCascade(currentWinners, winnersData);

        for (int i = 0; i < 3; i++)
            UpdatePlayerBlock(richestBlocks, currentRichest, i, richestData[i], -1f, false, richestCascade);

        for (int i = 0; i < 3; i++)
            UpdatePlayerBlock(winnersBlocks, currentWinners, i, winnersData[i], 1f, true, winnersCascade);

        // Animation will complete after all blocks finish
        StartCoroutine(MarkAnimationComplete());
    }

    private IEnumerator MarkAnimationComplete()
    {
        // Wait for longest possible animation duration
        float maxDuration = Mathf.Max(interchangeDuration, slideDuration * 2) + 0.5f;
        yield return new WaitForSeconds(maxDuration);
        isAnimating = false;
    }

    private bool DetectCascade(Dictionary<int, LeaderboardEntry> currentData, List<LeaderboardEntry> newData)
    {
        if (currentData.Count == 0) return false;

        int positionChanges = 0;
        for (int i = 0; i < newData.Count; i++)
        {
            if (currentData.ContainsKey(i) && currentData[i].username != newData[i].username)
            {
                positionChanges++;
            }
        }

        // If more than one position is changing, it's a cascade
        return positionChanges > 1;
    }

    internal void Hide()
    {
        if (leaderboardParent != null) leaderboardParent.SetActive(false);

        foreach (var b in richestBlocks) b?.HideAll();
        foreach (var b in winnersBlocks) b?.HideAll();

        currentRichest.Clear();
        currentWinners.Clear();
        StopAllAnimations();
    }
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
        var result = source != null ? new List<LeaderboardEntry>(source) : new List<LeaderboardEntry>();
        while (result.Count < 3) result.Add(MakeDummy(result.Count + 1));
        return result;
    }
    #endregion

    #region Block Management
    private void UpdatePlayerBlock(
        List<LeaderboardPlayerBlock> blocks,
        Dictionary<int, LeaderboardEntry> currentData,
        int index,
        LeaderboardEntry newEntry,
        float slideDir,
        bool isWinners,
        bool isCascade)
    {
        if (index >= blocks.Count || blocks[index] == null) return;

        LeaderboardPlayerBlock block = blocks[index];
        bool isFirstTime = !currentData.ContainsKey(index);
        bool playerChanged = !isFirstTime && currentData[index].username != newEntry.username;
        double displayValue = isWinners ? newEntry.totalWins : newEntry.balance;

        // Check if this is a position interchange (player moved from another position)
        int oldPosition = FindPlayerPosition(currentData, newEntry.username);
        bool isPositionSwap = oldPosition != -1 && oldPosition != index;

        if (isFirstTime)
        {
            currentData[index] = newEntry;
            block.SetPlayerData(newEntry.username, displayValue, PickAvatar(newEntry.username));
            SetPositionBadge(block, index, isWinners);
            float offset = Random.Range(minRandomOffset, maxRandomOffset);
            AddBlockCoroutine(block, StartCoroutine(DelayedAlternateStart(block, offset)));
        }
        else if (isPositionSwap && !isCascade)
        {
            // Only use interchange for simple 1:1 swaps (not cascade scenarios)
            LeaderboardPlayerBlock oldBlock = blocks[oldPosition];
            LeaderboardEntry oldBlockEntry = currentData[index];

            currentData[oldPosition] = oldBlockEntry;
            currentData[index] = newEntry;

            StopBlockAnimation(block);
            StopBlockAnimation(oldBlock);

            AddBlockCoroutine(block, StartCoroutine(InterchangePositions(
                block, oldBlock, newEntry, oldBlockEntry, displayValue,
                isWinners ? currentWinners[oldPosition].totalWins : currentRichest[oldPosition].balance,
                isWinners)));
        }
        else if (playerChanged)
        {
            // Use fast slide for cascade scenarios or new players
            currentData[index] = newEntry;
            StopBlockAnimation(block);
            AddBlockCoroutine(block, StartCoroutine(SlideOutAndUpdate(block, newEntry, slideDir, displayValue, index, isWinners)));
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

    private int FindPlayerPosition(Dictionary<int, LeaderboardEntry> currentData, string username)
    {
        if (string.IsNullOrEmpty(username)) return -1;

        foreach (var kvp in currentData)
        {
            if (kvp.Value.username == username)
                return kvp.Key;
        }

        return -1;
    }

    private void SetPositionBadge(LeaderboardPlayerBlock block, int index, bool isWinners)
    {
        if (block == null) return;

        Sprite[] badges = isWinners ? winnersPositionBadges : richestPositionBadges;
        if (badges != null && index >= 0 && index < badges.Length)
        {
            block.SetPositionBadge(badges[index]);
        }
    }

    private IEnumerator DelayedAlternateStart(LeaderboardPlayerBlock block, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        AddBlockCoroutine(block, StartCoroutine(AlternateNameBalance(block)));
    }
    #endregion

    #region Animations
    private IEnumerator InterchangePositions(
        LeaderboardPlayerBlock block1,
        LeaderboardPlayerBlock block2,
        LeaderboardEntry entry1,
        LeaderboardEntry entry2,
        double displayValue1,
        double displayValue2,
        bool isWinners)
    {
        RectTransform rect1 = block1.GetComponent<RectTransform>();
        RectTransform rect2 = block2.GetComponent<RectTransform>();

        if (rect1 == null || rect2 == null) yield break;

        rect1.DOKill(complete: true);
        rect2.DOKill(complete: true);

        Vector2 pos1 = rect1.anchoredPosition;
        Vector2 pos2 = rect2.anchoredPosition;

        // Animate position swap
        rect1.DOAnchorPos(pos2, interchangeDuration).SetEase(Ease.InOutQuad);
        rect2.DOAnchorPos(pos1, interchangeDuration).SetEase(Ease.InOutQuad);

        yield return new WaitForSeconds(interchangeDuration);

        // Swap the actual positions in hierarchy
        int siblingIndex1 = rect1.GetSiblingIndex();
        int siblingIndex2 = rect2.GetSiblingIndex();
        rect1.SetSiblingIndex(siblingIndex2);
        rect2.SetSiblingIndex(siblingIndex1);

        // Reset positions after hierarchy swap
        rect1.anchoredPosition = pos1;
        rect2.anchoredPosition = pos2;

        // Update data
        ResetTextState(block1.NameText);
        ResetTextState(block1.BalanceText);
        ResetTextState(block2.NameText);
        ResetTextState(block2.BalanceText);

        block1.SetPlayerData(entry1.username, displayValue1, PickAvatar(entry1.username));
        block2.SetPlayerData(entry2.username, displayValue2, PickAvatar(entry2.username));

        // Update position badges
        int index1 = -1, index2 = -1;
        var blocks = isWinners ? winnersBlocks : richestBlocks;
        for (int i = 0; i < blocks.Count; i++)
        {
            if (blocks[i] == block1) index1 = i;
            if (blocks[i] == block2) index2 = i;
        }

        if (index1 != -1) SetPositionBadge(block1, index1, isWinners);
        if (index2 != -1) SetPositionBadge(block2, index2, isWinners);

        // Restart animations
        float randomOffset1 = Random.Range(minRandomOffset, maxRandomOffset);
        float randomOffset2 = Random.Range(minRandomOffset, maxRandomOffset);

        yield return new WaitForSeconds(Mathf.Max(randomOffset1, randomOffset2));

        AddBlockCoroutine(block1, StartCoroutine(AlternateNameBalance(block1)));
        AddBlockCoroutine(block2, StartCoroutine(AlternateNameBalance(block2)));
    }

    private IEnumerator SlideOutAndUpdate(
        LeaderboardPlayerBlock block,
        LeaderboardEntry entry,
        float slideDir,
        double displayValue,
        int index,
        bool isWinners)
    {
        RectTransform blockRect = block.GetComponent<RectTransform>();

        if (blockRect != null)
        {
            blockRect.DOKill(complete: true);

            // Use stored original position to ensure card returns correctly
            Vector2 restPos = originalPositions.ContainsKey(blockRect)
                ? originalPositions[blockRect]
                : blockRect.anchoredPosition;

            Vector2 offScreenPos = restPos + new Vector2(slideDir * slideDistance, 0f);

            yield return blockRect.DOAnchorPos(offScreenPos, slideDuration).SetEase(Ease.InQuad).WaitForCompletion();

            ResetTextState(block.NameText);
            ResetTextState(block.BalanceText);
            block.SetPlayerData(entry.username, displayValue, PickAvatar(entry.username));
            SetPositionBadge(block, index, isWinners);

            yield return blockRect.DOAnchorPos(restPos, slideDuration).SetEase(Ease.OutQuad).WaitForCompletion();

            // Force position to original (prevent drift)
            blockRect.anchoredPosition = restPos;
        }
        else
        {
            ResetTextState(block.NameText);
            ResetTextState(block.BalanceText);
            block.SetPlayerData(entry.username, displayValue, PickAvatar(entry.username));
            SetPositionBadge(block, index, isWinners);
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
        var cg = textComponent.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 1f;
        textComponent.GetComponent<RectTransform>()?.DOKill(complete: false);
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        return cg != null ? cg : go.AddComponent<CanvasGroup>();
    }
    #endregion

    #region Coroutine Tracking
    private void AddBlockCoroutine(LeaderboardPlayerBlock block, Coroutine coroutine)
    {
        if (block == null || coroutine == null) return;
        int id = block.GetInstanceID();
        if (!blockCoroutines.ContainsKey(id)) blockCoroutines[id] = new List<Coroutine>();
        blockCoroutines[id].Add(coroutine);
    }

    private void StopBlockAnimation(LeaderboardPlayerBlock block)
    {
        if (block == null) return;
        int id = block.GetInstanceID();
        if (blockCoroutines.TryGetValue(id, out var coroutines))
        {
            foreach (var c in coroutines) if (c != null) StopCoroutine(c);
            coroutines.Clear();
        }
        block.GetComponent<RectTransform>()?.DOKill(complete: false);
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
    private Sprite PickAvatar(string username)
    {
        if (!string.IsNullOrEmpty(localPlayerUsername) &&
            localPlayerAvatar != null &&
            username == localPlayerUsername)
            return localPlayerAvatar;

        return GetRandomAvatar();
    }

    private Sprite GetRandomAvatar()
    {
        if (playerAvatars == null || playerAvatars.Length == 0) return null;
        return playerAvatars[Random.Range(0, playerAvatars.Length)];
    }
    #endregion
}