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
    [SerializeField] private Sprite[] playerAvatars; // 4-5 avatar images

    [Header("Animation Settings")]
    [SerializeField] private float nameDuration = 2f; // How long to HOLD name visible before fading out
    [SerializeField] private float balanceDuration = 2f; // How long to HOLD balance visible before fading out
    [SerializeField] private float fadeSpeed = 0.3f; // Speed of fade in/out animations (lower = faster)
    [SerializeField] private float loopInterval = 0f; // Pause between complete cycles (0 = no pause)
    [SerializeField] private float slideDistance = 300f; // How far to slide out
    [SerializeField] private float slideDuration = 0.5f; // Slide animation time
    [SerializeField] private float minRandomOffset = 0f; // Min random timing offset
    [SerializeField] private float maxRandomOffset = 2f; // Max random timing offset

    [Header("Parent Container (Optional)")]
    [SerializeField] private GameObject leaderboardParent; // Optional parent GameObject to show/hide entire leaderboard
    #endregion

    #region Private Fields
    private Dictionary<int, LeaderboardEntry> currentRichest = new Dictionary<int, LeaderboardEntry>();
    private Dictionary<int, LeaderboardEntry> currentWinners = new Dictionary<int, LeaderboardEntry>();
    private List<Coroutine> animationCoroutines = new List<Coroutine>();
    private bool isInitialized = false;
    #endregion

    #region Unity Lifecycle
    private void OnDestroy()
    {
        StopAllAnimations();
    }
    #endregion

    #region Public API
    /// <summary>
    /// Initialize all blocks to empty state - called once at start
    /// </summary>
    public void Initialize()
    {
        Debug.Log("[LeaderboardController] Initialize called");

        // Hide all blocks initially
        foreach (var block in richestBlocks)
        {
            if (block != null)
            {
                block.HideAll();
            }
        }

        foreach (var block in winnersBlocks)
        {
            if (block != null)
            {
                block.HideAll();
            }
        }

        currentRichest.Clear();
        currentWinners.Clear();

        // Hide parent if it exists
        if (leaderboardParent != null)
        {
            leaderboardParent.SetActive(false);
        }

        isInitialized = true;
    }

    /// <summary>
    /// Update leaderboard data from server
    /// </summary>
    public void UpdateLeaderboard(Leaderboards leaderboards)
    {
        if (leaderboards == null)
        {
            Debug.LogWarning("[LeaderboardController] UpdateLeaderboard called with null leaderboards");
            return;
        }

        Debug.Log($"[LeaderboardController] UpdateLeaderboard called - richest count: {leaderboards.richest?.Count ?? 0}, winners count: {leaderboards.winners?.Count ?? 0}");

        // Check if we have any data to show
        bool hasRichestData = leaderboards.richest != null && leaderboards.richest.Count > 0;
        bool hasWinnersData = leaderboards.winners != null && leaderboards.winners.Count > 0;
        bool hasData = hasRichestData || hasWinnersData;

        if (!hasData)
        {
            Debug.Log("[LeaderboardController] No leaderboard data to display");
            // Hide parent if no data
            if (leaderboardParent != null)
            {
                leaderboardParent.SetActive(false);
            }
            return;
        }

        // Show parent if we have data
        if (leaderboardParent != null && !leaderboardParent.activeSelf)
        {
            Debug.Log("[LeaderboardController] Showing leaderboard parent");
            leaderboardParent.SetActive(true);
        }

        // Update richest (left side)
        // If richest is empty but winners has data, duplicate winners to richest
        List<LeaderboardEntry> richestData = leaderboards.richest;
        if (!hasRichestData && hasWinnersData)
        {
            Debug.Log("[LeaderboardController] Richest is empty, duplicating winners data");
            richestData = leaderboards.winners;
        }

        if (richestData != null)
        {
            for (int i = 0; i < 3; i++)
            {
                if (i < richestData.Count)
                {
                    UpdatePlayerBlock(
                        richestBlocks,
                        currentRichest,
                        i,
                        richestData[i],
                        -slideDistance, // Slide LEFT (negative X to go left)
                        true // Is left side
                    );
                }
                else
                {
                    ClearPlayerBlock(richestBlocks, currentRichest, i);
                }
            }
        }

        // Update winners (right side)
        // If winners is empty but richest has data, duplicate richest to winners
        List<LeaderboardEntry> winnersData = leaderboards.winners;
        if (!hasWinnersData && hasRichestData)
        {
            Debug.Log("[LeaderboardController] Winners is empty, duplicating richest data");
            winnersData = leaderboards.richest;
        }

        if (winnersData != null)
        {
            for (int i = 0; i < 3; i++)
            {
                if (i < winnersData.Count)
                {
                    UpdatePlayerBlock(
                        winnersBlocks,
                        currentWinners,
                        i,
                        winnersData[i],
                        slideDistance, // Slide RIGHT (positive X to go right)
                        false // Is right side
                    );
                }
                else
                {
                    ClearPlayerBlock(winnersBlocks, currentWinners, i);
                }
            }
        }
    }

    /// <summary>
    /// Hide leaderboard (useful when leaving room or resetting)
    /// </summary>
    public void Hide()
    {
        Debug.Log("[LeaderboardController] Hide called");

        if (leaderboardParent != null)
        {
            leaderboardParent.SetActive(false);
        }

        // Also hide all individual blocks
        foreach (var block in richestBlocks)
        {
            if (block != null)
            {
                block.HideAll();
            }
        }

        foreach (var block in winnersBlocks)
        {
            if (block != null)
            {
                block.HideAll();
            }
        }

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
        float slideDirection,
        bool isLeftSide)
    {
        if (index >= blocks.Count || blocks[index] == null) return;

        LeaderboardPlayerBlock block = blocks[index];

        // Check if this is the first time setting data (no previous entry)
        bool isFirstTime = !currentData.ContainsKey(index);

        // Check if player changed
        bool playerChanged = !isFirstTime && currentData[index].username != newEntry.username;

        Debug.Log($"[LeaderboardController] UpdatePlayerBlock index={index}, isFirstTime={isFirstTime}, playerChanged={playerChanged}, username={newEntry.username}");

        if (isFirstTime)
        {
            // First time - just set data directly without animation
            currentData[index] = newEntry;
            block.SetPlayerData(
                newEntry.username,
                newEntry.balance,
                GetRandomAvatar()
            );

            Debug.Log($"[LeaderboardController] First time display for {newEntry.username} at index {index}");

            // Start alternating animation after a random delay
            float randomOffset = Random.Range(minRandomOffset, maxRandomOffset);
            Coroutine alternatingCoroutine = StartCoroutine(DelayedAlternateStart(block, randomOffset));
            animationCoroutines.Add(alternatingCoroutine);
        }
        else if (playerChanged)
        {
            // Player changed - slide out, update, slide in
            currentData[index] = newEntry;

            // Stop any existing animation for this block
            StopBlockAnimation(block);

            // Start slide out/in animation
            Coroutine slideCoroutine = StartCoroutine(SlideOutAndUpdate(
                block,
                newEntry,
                slideDirection
            ));

            animationCoroutines.Add(slideCoroutine);
        }
        else
        {
            // Same player - just update balance if changed
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
        Coroutine alternatingCoroutine = StartCoroutine(AlternateNameBalance(block));
        animationCoroutines.Add(alternatingCoroutine);
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
        // Slide out
        RectTransform blockRect = block.GetComponent<RectTransform>();
        if (blockRect != null)
        {
            Vector2 originalPos = blockRect.anchoredPosition;
            Vector2 slideOutPos = originalPos + new Vector2(slideDirection, 0);

            yield return blockRect.DOAnchorPos(slideOutPos, slideDuration)
                .SetEase(Ease.InBack)
                .WaitForCompletion();

            // Update data while off-screen
            block.SetPlayerData(
                entry.username,
                entry.balance,
                GetRandomAvatar()
            );

            // Slide back in
            yield return blockRect.DOAnchorPos(originalPos, slideDuration)
                .SetEase(Ease.OutBack)
                .WaitForCompletion();
        }
        else
        {
            // No rect transform - just update directly
            block.SetPlayerData(
                entry.username,
                entry.balance,
                GetRandomAvatar()
            );
        }

        // Start name/balance alternating animation with random offset
        float randomOffset = Random.Range(minRandomOffset, maxRandomOffset);
        yield return new WaitForSeconds(randomOffset);

        Coroutine alternatingCoroutine = StartCoroutine(AlternateNameBalance(block));
        animationCoroutines.Add(alternatingCoroutine);
    }

    private IEnumerator AlternateNameBalance(LeaderboardPlayerBlock block)
    {
        while (true)
        {
            // === SHOW NAME, HIDE BALANCE ===
            block.ShowName();  // Instantly show name, hide balance

            // Hold for specified duration
            yield return new WaitForSeconds(nameDuration);

            // === TRANSITION: Name fades out UP, Balance fades in from SAME position ===
            StartCoroutine(FadeOutUp(block.NameText));  // Start name fade out
            yield return StartCoroutine(FadeInAtPosition(block.BalanceText));  // Balance fades in at current position

            // Hold for specified duration
            yield return new WaitForSeconds(balanceDuration);

            // === TRANSITION: Balance fades out UP, Name fades in from SAME position ===
            StartCoroutine(FadeOutUp(block.BalanceText));  // Start balance fade out
            yield return StartCoroutine(FadeInAtPosition(block.NameText));  // Name fades in at current position

            // Optional pause between complete cycles (loop interval)
            if (loopInterval > 0)
            {
                yield return new WaitForSeconds(loopInterval);
            }
        }
    }

    /// <summary>
    /// Fade text OUT by moving up and reducing opacity
    /// </summary>
    private IEnumerator FadeOutUp(TMP_Text textComponent)
    {
        if (textComponent == null) yield break;

        RectTransform textRect = textComponent.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = textComponent.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = textComponent.gameObject.AddComponent<CanvasGroup>();
        }

        Vector2 startPos = textRect.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(0, 30f); // Move up

        float elapsed = 0f;
        float duration = fadeSpeed; // Use inspector value

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            textRect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            canvasGroup.alpha = 1f - t;

            yield return null;
        }

        textRect.anchoredPosition = startPos; // Reset position for next cycle
        canvasGroup.alpha = 0f;
        textComponent.gameObject.SetActive(false);
    }

    /// <summary>
    /// Fade text IN at its current position (no movement from below)
    /// </summary>
    private IEnumerator FadeInAtPosition(TMP_Text textComponent)
    {
        if (textComponent == null) yield break;

        RectTransform textRect = textComponent.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = textComponent.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = textComponent.gameObject.AddComponent<CanvasGroup>();
        }

        // Activate and start at current position (no offset)
        textComponent.gameObject.SetActive(true);
        canvasGroup.alpha = 0f;

        float elapsed = 0f;
        float duration = fadeSpeed; // Use inspector value

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            canvasGroup.alpha = t;

            yield return null;
        }

        canvasGroup.alpha = 1f;
    }

    private void StopBlockAnimation(LeaderboardPlayerBlock block)
    {
        // Stop any coroutines running on this block
        for (int i = animationCoroutines.Count - 1; i >= 0; i--)
        {
            if (animationCoroutines[i] != null)
            {
                StopCoroutine(animationCoroutines[i]);
                animationCoroutines.RemoveAt(i);
            }
        }

        // Kill any DOTween animations on this block
        if (block != null)
        {
            RectTransform blockRect = block.GetComponent<RectTransform>();
            if (blockRect != null)
                blockRect.DOKill();
        }
    }

    private void StopAllAnimations()
    {
        foreach (var coroutine in animationCoroutines)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }
        animationCoroutines.Clear();

        DOTween.Kill(this);
    }

    private Sprite GetRandomAvatar()
    {
        if (playerAvatars == null || playerAvatars.Length == 0)
        {
            return null;
        }

        return playerAvatars[Random.Range(0, playerAvatars.Length)];
    }
    #endregion
}