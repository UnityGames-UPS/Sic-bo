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

    [Header("Crown (For 1st Place Only)")]
    [SerializeField] private bool useSeparateCrown = true; // Enable if crown is separate from badges

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

    // GC optimisation: reuse lists in PadToThree — called twice per leaderboard update (once each for richest/winners)
    private readonly List<LeaderboardEntry> _padRichestCache = new List<LeaderboardEntry>(3);
    private readonly List<LeaderboardEntry> _padWinnersCache = new List<LeaderboardEntry>(3);
    #endregion

    #region Internal API — Local Player
    internal void SetLocalPlayer(string username, Sprite avatar)
    {
        localPlayerUsername = username;
        localPlayerAvatar = avatar;
    }

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
        foreach (var b in richestBlocks) b?.HideAll(); // HideAll() now also hides crown
        foreach (var b in winnersBlocks) b?.HideAll(); // HideAll() now also hides crown

        currentRichest.Clear();
        currentWinners.Clear();
        StopAllAnimations();

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

        var richestData = PadToThree(leaderboards?.richest, _padRichestCache);
        var winnersData = PadToThree(leaderboards?.winners, _padWinnersCache);

        if (leaderboardParent != null && !leaderboardParent.activeSelf)
            leaderboardParent.SetActive(true);
        bool richestCascade = DetectCascade(currentRichest, richestData);
        bool winnersCascade = DetectCascade(currentWinners, winnersData);

        for (int i = 0; i < 3; i++)
            UpdatePlayerBlock(richestBlocks, currentRichest, i, richestData[i], -1f, false, richestCascade);

        for (int i = 0; i < 3; i++)
            UpdatePlayerBlock(winnersBlocks, currentWinners, i, winnersData[i], 1f, true, winnersCascade);
        StartCoroutine(MarkAnimationComplete());
    }

    private IEnumerator MarkAnimationComplete()
    {
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

    private List<LeaderboardEntry> PadToThree(List<LeaderboardEntry> source, List<LeaderboardEntry> cache)
    {
        cache.Clear();
        if (source != null) cache.AddRange(source);
        while (cache.Count < 3) cache.Add(MakeDummy(cache.Count + 1));
        return cache;
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

        int oldPosition = FindPlayerPosition(currentData, newEntry.username);
        bool playerMovedUp = oldPosition > index;

        if (isFirstTime)
        {
            StopBlockAnimation(block);
            block.SetPlayerData(newEntry.username, displayValue, PickAvatar(newEntry.username));
            SetPositionBadge(block, index, isWinners);
            currentData[index] = newEntry;
            float randomOffset = Random.Range(minRandomOffset, maxRandomOffset);
            StartCoroutine(DelayedStartAnimation(block, randomOffset));
        }
        else if (playerChanged)
        {
            if (isCascade)
            {
                StopBlockAnimation(block);
                AddBlockCoroutine(block, StartCoroutine(
                    SlideOutAndUpdate(block, newEntry, slideDir, displayValue, index, isWinners)
                ));
            }
            else if (playerMovedUp && oldPosition != -1)
            {
                int newIndex = index;
                int oldIndex = oldPosition;
                LeaderboardPlayerBlock oldBlock = blocks[oldIndex];
                LeaderboardEntry oldEntry = currentData[oldIndex];

                StopBlockAnimation(block);
                StopBlockAnimation(oldBlock);

                currentData[newIndex] = newEntry;
                currentData[oldIndex] = oldEntry;

                double oldDisplayValue = isWinners ? oldEntry.totalWins : oldEntry.balance;

                AddBlockCoroutine(block, StartCoroutine(
                    InterchangeBlocks(
                        block, oldBlock, newEntry, oldEntry, displayValue,
                        oldDisplayValue, isWinners, newIndex, oldIndex
                    )
                ));
            }
            else
            {
                StopBlockAnimation(block);
                AddBlockCoroutine(block, StartCoroutine(
                    SlideOutAndUpdate(block, newEntry, slideDir, displayValue, index, isWinners)
                ));
            }
            currentData[index] = newEntry;
        }
        else
        {
            if (displayValue != (isWinners ? currentData[index].totalWins : currentData[index].balance))
            {
                block.UpdateBalance(displayValue);
                currentData[index].balance = newEntry.balance;
                currentData[index].totalWins = newEntry.totalWins;
            }
        }
    }

    private IEnumerator DelayedStartAnimation(LeaderboardPlayerBlock block, float delay)
    {
        yield return new WaitForSeconds(delay);
        AddBlockCoroutine(block, StartCoroutine(AlternateNameBalance(block)));
    }

    private int FindPlayerPosition(Dictionary<int, LeaderboardEntry> currentData, string username)
    {
        foreach (var kvp in currentData)
        {
            if (kvp.Value.username == username)
                return kvp.Key;
        }
        return -1;
    }

    private void SetPositionBadge(LeaderboardPlayerBlock block, int position, bool isWinners)
    {
        var badges = isWinners ? winnersPositionBadges : richestPositionBadges;
        if (position >= 0 && position < badges.Length)
        {
            block.SetPositionBadge(badges[position]);
        }

        // Manage crown visibility - only show for 1st place (position 0)
        if (useSeparateCrown)
        {
            block.SetCrownVisible(position == 0);
        }
    }

    private IEnumerator InterchangeBlocks(
        LeaderboardPlayerBlock block1,
        LeaderboardPlayerBlock block2,
        LeaderboardEntry entry1,
        LeaderboardEntry entry2,
        double displayValue1,
        double displayValue2,
        bool isWinners,
        int newIndex1,  // New position for block1 (where it's going)
        int newIndex2)  // New position for block2 (where it's going)
    {
        // STOP ALL ANIMATIONS ON BOTH BLOCKS FIRST
        StopBlockAnimation(block1);
        StopBlockAnimation(block2);

        RectTransform rect1 = block1.GetComponent<RectTransform>();
        RectTransform rect2 = block2.GetComponent<RectTransform>();

        if (rect1 == null || rect2 == null) yield break;

        // Kill any existing DOTween animations
        rect1.DOKill(complete: true);
        rect2.DOKill(complete: true);

        Vector2 pos1 = rect1.anchoredPosition;
        Vector2 pos2 = rect2.anchoredPosition;

        // HIDE BOTH POSITION BADGES AND CROWNS BEFORE SWAP
        block1.SetPositionBadge(null);
        block2.SetPositionBadge(null);
        if (useSeparateCrown)
        {
            block1.HideCrown();
            block2.HideCrown();
        }

        // Reset text states to ensure clean animation
        ResetTextState(block1.NameText);
        ResetTextState(block1.BalanceText);
        ResetTextState(block2.NameText);
        ResetTextState(block2.BalanceText);

        // Perform the position swap animation
        rect1.DOAnchorPos(pos2, interchangeDuration).SetEase(Ease.InOutQuad);
        rect2.DOAnchorPos(pos1, interchangeDuration).SetEase(Ease.InOutQuad);

        yield return new WaitForSeconds(interchangeDuration);

        // Swap sibling indices to maintain proper layer order
        int siblingIndex1 = rect1.GetSiblingIndex();
        int siblingIndex2 = rect2.GetSiblingIndex();
        rect1.SetSiblingIndex(siblingIndex2);
        rect2.SetSiblingIndex(siblingIndex1);

        // FORCE set positions to avoid stuck blocks
        rect1.anchoredPosition = pos2;
        rect2.anchoredPosition = pos1;

        // Update the original positions dictionary
        if (originalPositions.ContainsKey(rect1))
        {
            originalPositions[rect1] = pos2;
        }
        if (originalPositions.ContainsKey(rect2))
        {
            originalPositions[rect2] = pos1;
        }

        // Reset text states again before setting data
        ResetTextState(block1.NameText);
        ResetTextState(block1.BalanceText);
        ResetTextState(block2.NameText);
        ResetTextState(block2.BalanceText);

        // Update player data
        block1.SetPlayerData(entry1.username, displayValue1, PickAvatar(entry1.username));
        block2.SetPlayerData(entry2.username, displayValue2, PickAvatar(entry2.username));

        // SET POSITION BADGES AFTER SWAP IS COMPLETE
        // block1 is now at newIndex1 position, so it gets that position's badge
        // block2 is now at newIndex2 position, so it gets that position's badge
        SetPositionBadge(block1, newIndex1, isWinners);
        SetPositionBadge(block2, newIndex2, isWinners);

        // Wait a bit before starting the name/balance animation
        float randomOffset1 = Random.Range(minRandomOffset, maxRandomOffset);
        float randomOffset2 = Random.Range(minRandomOffset, maxRandomOffset);

        yield return new WaitForSeconds(Mathf.Max(randomOffset1, randomOffset2));

        // Start the alternating animation for both blocks
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
            // Kill any ongoing animations
            blockRect.DOKill(complete: true);

            Vector2 restPos = originalPositions.ContainsKey(blockRect)
                ? originalPositions[blockRect]
                : blockRect.anchoredPosition;

            Vector2 offScreenPos = restPos + new Vector2(slideDir * slideDistance, 0f);

            // Slide out
            yield return blockRect.DOAnchorPos(offScreenPos, slideDuration).SetEase(Ease.InQuad).WaitForCompletion();

            // Update data while off-screen
            ResetTextState(block.NameText);
            ResetTextState(block.BalanceText);
            block.SetPlayerData(entry.username, displayValue, PickAvatar(entry.username));
            SetPositionBadge(block, index, isWinners);

            // Slide back in
            yield return blockRect.DOAnchorPos(restPos, slideDuration).SetEase(Ease.OutQuad).WaitForCompletion();

            // Force set position to avoid stuck blocks
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

        bool firstIteration = true;

        while (true)
        {
            if (!firstIteration)
            {

                StartCoroutine(FadeOutUp(block.BalanceText));
                yield return StartCoroutine(FadeInAtPosition(block.NameText));
            }


            yield return new WaitForSeconds(nameDuration);

            StartCoroutine(FadeOutUp(block.NameText));
            yield return StartCoroutine(FadeInAtPosition(block.BalanceText));


            yield return new WaitForSeconds(balanceDuration);

            firstIteration = false;

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

        // Don't reset position here - let FadeInAtPosition handle it
        canvasGroup.alpha = 0f;
        textComponent.gameObject.SetActive(false);
    }

    private IEnumerator FadeInAtPosition(TMP_Text textComponent)
    {
        if (textComponent == null) yield break;

        CanvasGroup canvasGroup = GetOrAddCanvasGroup(textComponent.gameObject);
        RectTransform textRect = textComponent.GetComponent<RectTransform>();

        if (textRect != null)
        {
            // Get the original position from the block
            LeaderboardPlayerBlock block = textComponent.GetComponentInParent<LeaderboardPlayerBlock>();
            if (block != null)
            {
                Vector2 originalPos = (textComponent == block.NameText)
                    ? block.GetNameOriginalPosition()
                    : block.GetBalanceOriginalPosition();
                textRect.anchoredPosition = originalPos; // Reset to ORIGINAL inspector position
            }
            else
            {
                textRect.anchoredPosition = Vector2.zero; // Fallback
            }
        }

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

        RectTransform textRect = textComponent.GetComponent<RectTransform>();
        if (textRect != null)
        {
            textRect.DOKill(complete: true);

            // Reset to ORIGINAL inspector position
            LeaderboardPlayerBlock block = textComponent.GetComponentInParent<LeaderboardPlayerBlock>();
            if (block != null)
            {
                Vector2 originalPos = (textComponent == block.NameText)
                    ? block.GetNameOriginalPosition()
                    : block.GetBalanceOriginalPosition();
                textRect.anchoredPosition = originalPos;
            }
            else
            {
                textRect.anchoredPosition = Vector2.zero; // Fallback
            }
        }

        var cg = textComponent.GetComponent<CanvasGroup>();
        if (cg == null) cg = textComponent.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        textComponent.gameObject.SetActive(true);
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

        // Stop all tracked coroutines for this block
        if (blockCoroutines.TryGetValue(id, out var coroutines))
        {
            foreach (var c in coroutines)
            {
                if (c != null)
                {
                    try
                    {
                        StopCoroutine(c);
                    }
                    catch
                    {
                        // Coroutine might already be stopped
                    }
                }
            }
            coroutines.Clear();
        }

        // Kill all DOTween animations on the block
        RectTransform blockRect = block.GetComponent<RectTransform>();
        if (blockRect != null)
        {
            blockRect.DOKill(complete: true);
        }

        // Also kill animations on text components
        if (block.NameText != null)
        {
            RectTransform nameRect = block.NameText.GetComponent<RectTransform>();
            if (nameRect != null) nameRect.DOKill(complete: true);
        }
        if (block.BalanceText != null)
        {
            RectTransform balanceRect = block.BalanceText.GetComponent<RectTransform>();
            if (balanceRect != null) balanceRect.DOKill(complete: true);
        }
    }

    private void StopAllAnimations()
    {
        foreach (var kvp in blockCoroutines)
            foreach (var c in kvp.Value)
                if (c != null)
                {
                    try
                    {
                        StopCoroutine(c);
                    }
                    catch
                    {
                        // Coroutine might already be stopped
                    }
                }

        blockCoroutines.Clear();
        DOTween.Kill(this);

        // Also kill animations on all blocks
        foreach (var block in richestBlocks)
        {
            if (block != null)
            {
                block.GetComponent<RectTransform>()?.DOKill(complete: true);
            }
        }
        foreach (var block in winnersBlocks)
        {
            if (block != null)
            {
                block.GetComponent<RectTransform>()?.DOKill(complete: true);
            }
        }
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