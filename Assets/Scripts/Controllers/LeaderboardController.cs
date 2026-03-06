using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LeaderboardController : MonoBehaviour
{
    private const int MaxSlots = 3;

    [Header("Richest Blocks (Left Side)")]
    [SerializeField] private List<LeaderboardPlayerBlock> richestBlocks = new List<LeaderboardPlayerBlock>(MaxSlots);

    [Header("Winners Blocks (Right Side)")]
    [SerializeField] private List<LeaderboardPlayerBlock> winnersBlocks = new List<LeaderboardPlayerBlock>(MaxSlots);

    [Header("Avatar Images (Random Selection)")]
    [SerializeField] private Sprite[] playerAvatars;

    [Header("Position Badges")]
    [SerializeField] private Sprite[] richestPositionBadges = new Sprite[MaxSlots];
    [SerializeField] private Sprite[] winnersPositionBadges = new Sprite[MaxSlots];

    [Header("Crown (For 1st Place Only)")]
    [SerializeField] private bool useSeparateCrown = true;

    [Header("Animation Settings")]
    [SerializeField] private float nameDuration = 2f;
    [SerializeField] private float balanceDuration = 2f;
    [SerializeField] private float fadeSpeed = 0.3f;
    [SerializeField] private float loopInterval = 0f;
    [SerializeField] private float valueSwitchDelay = 0.12f;
    [SerializeField] private float slideDuration = 0.35f;
    [SerializeField] private float hiddenOffsetX = 150f;
    [SerializeField] private float textMoveDistance = 24f;
    [SerializeField] private float crownPulseDuration = 0.2f;
    [SerializeField] private float crownPulseInterval = 1.6f;

    [Header("Parent Container (Optional)")]
    [SerializeField] private GameObject leaderboardParent;

    [Header("Debug (No Socket)")]
    [SerializeField] private bool enableDebugKeybinds = false;

    private readonly Dictionary<int, LeaderboardEntry> currentRichest = new Dictionary<int, LeaderboardEntry>();
    private readonly Dictionary<int, LeaderboardEntry> currentWinners = new Dictionary<int, LeaderboardEntry>();
    private readonly Dictionary<int, LeaderboardPlayerBlock> richestSlotToBlock = new Dictionary<int, LeaderboardPlayerBlock>();
    private readonly Dictionary<int, LeaderboardPlayerBlock> winnersSlotToBlock = new Dictionary<int, LeaderboardPlayerBlock>();
    private readonly Dictionary<int, Vector2> richestRestPositions = new Dictionary<int, Vector2>();
    private readonly Dictionary<int, Vector2> winnersRestPositions = new Dictionary<int, Vector2>();
    private readonly Dictionary<int, List<Coroutine>> blockCoroutines = new Dictionary<int, List<Coroutine>>();
    private readonly Dictionary<string, Sprite> cachedAvatars = new Dictionary<string, Sprite>();

    private string localPlayerUsername;
    private Sprite localPlayerAvatar;
    private bool isAnimating;
    private bool isInitialized;
    private Coroutine syncedTextLoopCoroutine;
    private Coroutine syncedCrownLoopCoroutine;
    private Coroutine delayedLoopStartCoroutine;

    internal void SetLocalPlayer(string username, Sprite avatar)
    {
        localPlayerUsername = username;
        localPlayerAvatar = avatar;
    }

    internal bool IsAnimating() => isAnimating;

    internal IEnumerator WaitForAnimationComplete()
    {
        while (isAnimating)
        {
            yield return null;
        }
    }

    internal RectTransform GetPlayerPosition(string username, bool checkWinners)
    {
        if (string.IsNullOrEmpty(username)) return null;

        var currentData = checkWinners ? currentWinners : currentRichest;
        var slotToBlock = checkWinners ? winnersSlotToBlock : richestSlotToBlock;

        foreach (var kv in currentData)
        {
            if (kv.Value.username != username) continue;
            if (slotToBlock.TryGetValue(kv.Key, out var block) && block != null)
            {
                return block.GetComponent<RectTransform>();
            }
        }

        return null;
    }

    private void OnDestroy() => StopAllAnimations();

    private void Start()
    {
        if (!enableDebugKeybinds) return;
        Initialize();
    }

    private void Update()
    {
        if (!enableDebugKeybinds) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) ApplyDebugScenario1();
        if (Input.GetKeyDown(KeyCode.Alpha2)) ApplyDebugScenario2();
        if (Input.GetKeyDown(KeyCode.Alpha3)) ApplyDebugScenario3();
        if (Input.GetKeyDown(KeyCode.Alpha4)) ApplyDebugScenario4();
        if (Input.GetKeyDown(KeyCode.Alpha5)) ApplyDebugScenario5();
        if (Input.GetKeyDown(KeyCode.Alpha6)) ApplyDebugScenario6();
        if (Input.GetKeyDown(KeyCode.Alpha7)) ApplyDebugScenario7();
        if (Input.GetKeyDown(KeyCode.Alpha8)) ApplyDebugScenario8();
        if (Input.GetKeyDown(KeyCode.Alpha9)) ApplyDebugScenario9();
        if (Input.GetKeyDown(KeyCode.Alpha0)) Hide();
    }

    internal void Initialize()
    {
        StopAllAnimations();

        currentRichest.Clear();
        currentWinners.Clear();
        if (!isInitialized)
        {
            richestSlotToBlock.Clear();
            winnersSlotToBlock.Clear();
            richestRestPositions.Clear();
            winnersRestPositions.Clear();

            CachePositionsAndResetSide(richestBlocks, richestRestPositions, richestSlotToBlock, false);
            CachePositionsAndResetSide(winnersBlocks, winnersRestPositions, winnersSlotToBlock, true);
        }
        else
        {
            ResetSideToHidden(richestBlocks, richestRestPositions, richestSlotToBlock, false);
            ResetSideToHidden(winnersBlocks, winnersRestPositions, winnersSlotToBlock, true);
        }

        if (leaderboardParent != null)
        {
            leaderboardParent.SetActive(false);
        }

        isAnimating = false;
        isInitialized = true;
    }

    internal void Hide()
    {
        StopAllAnimations();

        foreach (var b in richestBlocks) b?.HideAll();
        foreach (var b in winnersBlocks) b?.HideAll();

        currentRichest.Clear();
        currentWinners.Clear();
        richestSlotToBlock.Clear();
        winnersSlotToBlock.Clear();

        if (leaderboardParent != null)
        {
            leaderboardParent.SetActive(false);
        }

        isAnimating = false;
    }

    internal void UpdateLeaderboard(Leaderboards leaderboards)
    {
        if (!isInitialized)
        {
            Initialize();
        }
        isAnimating = true;

        var richestTarget = BuildTopThreeByRank(leaderboards?.richest, false);
        var winnersTarget = BuildTopThreeByRank(leaderboards?.winners, true);

        bool hasAnyData = richestTarget.Count > 0 || winnersTarget.Count > 0;
        bool hadAnyData = currentRichest.Count > 0 || currentWinners.Count > 0;

        if (!hasAnyData)
        {
            if (!hadAnyData)
            {
                Hide();
                return;
            }

            if (leaderboardParent != null)
            {
                leaderboardParent.SetActive(true);
            }

            float clearRichestTime = ApplySideUpdate(richestBlocks, currentRichest, richestSlotToBlock, richestRestPositions, richestTarget, false);
            float clearWinnersTime = ApplySideUpdate(winnersBlocks, currentWinners, winnersSlotToBlock, winnersRestPositions, winnersTarget, true);
            float clearDelay = Mathf.Max(clearRichestTime, clearWinnersTime) + 0.05f;

            StartCoroutine(HideParentAfterDelay(clearDelay));
            StartCoroutine(MarkAnimationComplete(clearDelay));
            return;
        }

        if (leaderboardParent != null)
        {
            leaderboardParent.SetActive(true);
        }

        float richestTime = ApplySideUpdate(richestBlocks, currentRichest, richestSlotToBlock, richestRestPositions, richestTarget, false);
        float winnersTime = ApplySideUpdate(winnersBlocks, currentWinners, winnersSlotToBlock, winnersRestPositions, winnersTarget, true);
        float settleDelay = Mathf.Max(richestTime, winnersTime) + 0.03f;
        RestartSynchronizedLoops(settleDelay);
        StartCoroutine(MarkAnimationComplete(settleDelay));
    }

    private IEnumerator HideParentAfterDelay(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (leaderboardParent != null)
        {
            leaderboardParent.SetActive(false);
        }
    }

    private IEnumerator MarkAnimationComplete(float waitSeconds)
    {
        if (waitSeconds > 0f)
        {
            yield return new WaitForSeconds(waitSeconds);
        }

        isAnimating = false;
    }

    private void CachePositionsAndResetSide(
        List<LeaderboardPlayerBlock> blocks,
        Dictionary<int, Vector2> restPositions,
        Dictionary<int, LeaderboardPlayerBlock> slotToBlock,
        bool isWinners)
    {
        for (int i = 0; i < MaxSlots && i < blocks.Count; i++)
        {
            var block = blocks[i];
            if (block == null) continue;

            var rect = block.GetComponent<RectTransform>();
            if (rect == null) continue;

            restPositions[i] = rect.anchoredPosition;
            slotToBlock[i] = block;
            rect.anchoredPosition = GetHiddenPosition(restPositions[i], isWinners);
            block.HideAll();
            StopBlockAnimation(block);
        }
    }

    private void ResetSideToHidden(
        List<LeaderboardPlayerBlock> blocks,
        Dictionary<int, Vector2> restPositions,
        Dictionary<int, LeaderboardPlayerBlock> slotToBlock,
        bool isWinners)
    {
        slotToBlock.Clear();

        for (int i = 0; i < MaxSlots && i < blocks.Count; i++)
        {
            var block = blocks[i];
            if (block == null) continue;

            var rect = block.GetComponent<RectTransform>();
            if (rect == null) continue;

            if (!restPositions.TryGetValue(i, out var restPos))
            {
                restPos = rect.anchoredPosition;
                restPositions[i] = restPos;
            }

            slotToBlock[i] = block;
            rect.anchoredPosition = GetHiddenPosition(restPos, isWinners);
            block.HideAll();
            StopBlockAnimation(block);
        }
    }

    private float ApplySideUpdate(
        List<LeaderboardPlayerBlock> blocks,
        Dictionary<int, LeaderboardEntry> currentData,
        Dictionary<int, LeaderboardPlayerBlock> slotToBlock,
        Dictionary<int, Vector2> restPositions,
        Dictionary<int, LeaderboardEntry> targetData,
        bool isWinners)
    {
        var currentUsernameToSlot = new Dictionary<string, int>();
        foreach (var kv in currentData)
        {
            currentUsernameToSlot[kv.Value.username] = kv.Key;
        }

        var targetUsernameToSlot = new Dictionary<string, int>();
        foreach (var kv in targetData)
        {
            targetUsernameToSlot[kv.Value.username] = kv.Key;
        }

        var assignedByTargetSlot = new Dictionary<int, LeaderboardPlayerBlock>();
        var reservedBlocks = new HashSet<LeaderboardPlayerBlock>();
        var outgoingBlocks = new List<LeaderboardPlayerBlock>();
        var outgoingSlotsByBlock = new Dictionary<LeaderboardPlayerBlock, int>();

        foreach (var kv in currentData)
        {
            int currentSlot = kv.Key;
            string username = kv.Value.username;
            if (targetUsernameToSlot.ContainsKey(username)) continue;

            if (!slotToBlock.TryGetValue(currentSlot, out var block) || block == null) continue;
            StopBlockAnimation(block);
            outgoingBlocks.Add(block);
            outgoingSlotsByBlock[block] = currentSlot;
        }

        foreach (var kv in targetData)
        {
            string username = kv.Value.username;
            int targetSlot = kv.Key;
            if (!currentUsernameToSlot.TryGetValue(username, out int oldSlot)) continue;
            if (!slotToBlock.TryGetValue(oldSlot, out var block) || block == null) continue;
            if (!currentData.TryGetValue(oldSlot, out var oldEntry) || oldEntry == null) continue;

            assignedByTargetSlot[targetSlot] = block;
            reservedBlocks.Add(block);

            UpdateExistingBlock(block, oldEntry, kv.Value, oldSlot, targetSlot, isWinners);
        }

        var freeBlocks = new List<LeaderboardPlayerBlock>();
        var outgoingSet = new HashSet<LeaderboardPlayerBlock>(outgoingBlocks);
        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            if (block == null || reservedBlocks.Contains(block) || outgoingSet.Contains(block)) continue;
            freeBlocks.Add(block);
        }

        var incomingSlots = new List<int>();
        foreach (var kv in targetData)
        {
            int targetSlot = kv.Key;
            if (!assignedByTargetSlot.ContainsKey(targetSlot))
            {
                incomingSlots.Add(targetSlot);
            }
        }

        bool hasOutgoingAndIncoming = outgoingBlocks.Count > 0 && incomingSlots.Count > 0;
        float totalTime = 0f;

        foreach (var kv in targetData)
        {
            int targetSlot = kv.Key;
            if (!assignedByTargetSlot.TryGetValue(targetSlot, out var block) || block == null) continue;

            var rect = block.GetComponent<RectTransform>();
            if (rect == null || !restPositions.TryGetValue(targetSlot, out var targetPos)) continue;

            rect.DOKill(complete: false);
            rect.DOAnchorPos(targetPos, slideDuration).SetEase(Ease.InOutQuad).SetDelay(valueSwitchDelay);
            totalTime = Mathf.Max(totalTime, valueSwitchDelay + slideDuration);
        }

        foreach (var outgoingBlock in outgoingBlocks)
        {
            if (outgoingBlock == null || reservedBlocks.Contains(outgoingBlock)) continue;
            if (!outgoingSlotsByBlock.TryGetValue(outgoingBlock, out int slot)) continue;
            if (!restPositions.TryGetValue(slot, out var restPos)) continue;

            var rect = outgoingBlock.GetComponent<RectTransform>();
            if (rect == null) continue;

            rect.DOKill(complete: false);
            rect.DOAnchorPos(GetHiddenPosition(restPos, isWinners), slideDuration)
                .SetEase(Ease.InOutQuad)
                .OnComplete(outgoingBlock.HideAll);
            totalTime = Mathf.Max(totalTime, slideDuration);
        }

        if (hasOutgoingAndIncoming)
        {
            totalTime += slideDuration;
        }

        var outgoingPool = new Queue<LeaderboardPlayerBlock>(outgoingBlocks);
        slotToBlock.Clear();
        foreach (var kv in assignedByTargetSlot)
        {
            slotToBlock[kv.Key] = kv.Value;
        }

        for (int i = 0; i < incomingSlots.Count; i++)
        {
            int targetSlot = incomingSlots[i];
            if (!targetData.TryGetValue(targetSlot, out var targetEntry)) continue;

            LeaderboardPlayerBlock block = null;
            while (outgoingPool.Count > 0 && block == null)
            {
                block = outgoingPool.Dequeue();
            }

            if (block == null && freeBlocks.Count > 0)
            {
                block = freeBlocks[0];
                freeBlocks.RemoveAt(0);
            }

            if (block == null) continue;

            StopBlockAnimation(block);
            PrepareBlock(block, targetEntry, targetSlot, isWinners);

            if (!restPositions.TryGetValue(targetSlot, out var targetPos))
            {
                slotToBlock[targetSlot] = block;
                continue;
            }

            var rect = block.GetComponent<RectTransform>();
            if (rect == null)
            {
                slotToBlock[targetSlot] = block;
                continue;
            }

            rect.DOKill(complete: false);
            rect.anchoredPosition = GetHiddenPosition(targetPos, isWinners);
            float enterDelay = hasOutgoingAndIncoming ? slideDuration + valueSwitchDelay : valueSwitchDelay;

            if (enterDelay > 0f)
            {
                AddBlockCoroutine(block, StartCoroutine(AnimateEnterAfterDelay(block, rect, targetPos, enterDelay)));
                totalTime = Mathf.Max(totalTime, enterDelay + slideDuration);
            }
            else
            {
                rect.DOAnchorPos(targetPos, slideDuration).SetEase(Ease.InOutQuad);
                totalTime = Mathf.Max(totalTime, slideDuration);
            }

            slotToBlock[targetSlot] = block;
        }

        currentData.Clear();
        foreach (var kv in targetData)
        {
            currentData[kv.Key] = kv.Value;
        }

        return totalTime;
    }

    private IEnumerator AnimateEnterAfterDelay(LeaderboardPlayerBlock block, RectTransform rect, Vector2 targetPos, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (block == null || rect == null) yield break;

        rect.DOKill(complete: false);
        rect.DOAnchorPos(targetPos, slideDuration).SetEase(Ease.InOutQuad);
    }

    private void PrepareBlock(LeaderboardPlayerBlock block, LeaderboardEntry entry, int targetSlot, bool isWinners)
    {
        double value = isWinners ? entry.totalWins : entry.balance;
        block.SetPlayerData(entry.username, value, PickAvatar(entry.username));
        SetPositionBadge(block, targetSlot, isWinners);
        ResetTextState(block.NameText);
        ResetTextState(block.BalanceText);
        block.ShowName();
    }

    private void UpdateExistingBlock(
        LeaderboardPlayerBlock block,
        LeaderboardEntry oldEntry,
        LeaderboardEntry newEntry,
        int oldSlot,
        int targetSlot,
        bool isWinners)
    {
        bool movedSlot = oldSlot != targetSlot;
        double oldValue = isWinners ? oldEntry.totalWins : oldEntry.balance;
        double newValue = isWinners ? newEntry.totalWins : newEntry.balance;

        if (newEntry.username != oldEntry.username)
        {
            PrepareBlock(block, newEntry, targetSlot, isWinners);
            return;
        }

        if (oldValue != newValue)
        {
            block.UpdateBalance(newValue);
        }

        if (movedSlot)
        {
            SetPositionBadge(block, targetSlot, isWinners);
            block.ShowName();
        }
        else
        {
            if (targetSlot == 0 && useSeparateCrown)
            {
                block.SetCrownVisible(true);
            }
            else if (useSeparateCrown)
            {
                block.SetCrownVisible(false);
            }
        }
    }

    private Dictionary<int, LeaderboardEntry> BuildTopThreeByRank(List<LeaderboardEntry> source, bool isWinners)
    {
        var ranked = new List<LeaderboardEntry>();

        if (source != null)
        {
            for (int i = 0; i < source.Count; i++)
            {
                var item = source[i];
                if (item == null || string.IsNullOrEmpty(item.username)) continue;

                ranked.Add(new LeaderboardEntry
                {
                    username = item.username,
                    balance = item.balance,
                    totalWins = item.totalWins,
                    rank = item.rank
                });
            }
        }

        ranked.Sort((a, b) => a.rank.CompareTo(b.rank));

        var result = new Dictionary<int, LeaderboardEntry>();
        var addedUsers = new HashSet<string>();

        for (int i = 0; i < ranked.Count; i++)
        {
            var entry = ranked[i];
            if (addedUsers.Contains(entry.username)) continue;

            int slot = Mathf.Clamp(entry.rank - 1, 0, MaxSlots - 1);
            if (result.ContainsKey(slot))
            {
                for (int fallback = 0; fallback < MaxSlots; fallback++)
                {
                    if (!result.ContainsKey(fallback))
                    {
                        slot = fallback;
                        break;
                    }
                }
            }

            if (result.ContainsKey(slot)) continue;

            if (isWinners && entry.totalWins <= 0d)
            {
                // winners entries may sometimes be sent with balance only; keep value if username is valid
            }

            addedUsers.Add(entry.username);
            result[slot] = entry;

            if (result.Count >= MaxSlots) break;
        }

        return result;
    }

    private void SetPositionBadge(LeaderboardPlayerBlock block, int position, bool isWinners)
    {
        var badges = isWinners ? winnersPositionBadges : richestPositionBadges;
        if (position >= 0 && position < badges.Length)
        {
            block.SetPositionBadge(badges[position]);
        }
        else
        {
            block.SetPositionBadge(null);
        }

        if (useSeparateCrown)
        {
            block.SetCrownVisible(position == 0);
        }
    }

    private void RestartSynchronizedLoops(float delay)
    {
        StopSynchronizedLoops();
        delayedLoopStartCoroutine = StartCoroutine(StartSynchronizedLoopsAfterDelay(delay));
    }

    private IEnumerator StartSynchronizedLoopsAfterDelay(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        var visibleBlocks = GetVisibleBlocks();
        if (visibleBlocks.Count == 0) yield break;

        for (int i = 0; i < visibleBlocks.Count; i++)
        {
            visibleBlocks[i].ShowName();
        }

        syncedTextLoopCoroutine = StartCoroutine(RunSynchronizedTextLoop());
        syncedCrownLoopCoroutine = StartCoroutine(RunSynchronizedCrownLoop());
    }

    private void StopSynchronizedLoops()
    {
        if (delayedLoopStartCoroutine != null)
        {
            StopCoroutine(delayedLoopStartCoroutine);
            delayedLoopStartCoroutine = null;
        }

        if (syncedTextLoopCoroutine != null)
        {
            StopCoroutine(syncedTextLoopCoroutine);
            syncedTextLoopCoroutine = null;
        }

        if (syncedCrownLoopCoroutine != null)
        {
            StopCoroutine(syncedCrownLoopCoroutine);
            syncedCrownLoopCoroutine = null;
        }
    }

    private IEnumerator RunSynchronizedTextLoop()
    {
        while (true)
        {
            var visibleBlocks = GetVisibleBlocks();
            if (visibleBlocks.Count == 0) yield break;

            yield return new WaitForSeconds(nameDuration);
            yield return StartCoroutine(TransitionTextSet(visibleBlocks, showBalance: true));

            yield return new WaitForSeconds(balanceDuration);
            yield return StartCoroutine(TransitionTextSet(visibleBlocks, showBalance: false));

            if (loopInterval > 0f)
            {
                yield return new WaitForSeconds(loopInterval);
            }
        }
    }

    private IEnumerator TransitionTextSet(List<LeaderboardPlayerBlock> blocks, bool showBalance)
    {
        var outRoutines = new List<Coroutine>();
        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            if (block == null) continue;
            TMP_Text outText = showBalance ? block.NameText : block.BalanceText;
            outRoutines.Add(StartCoroutine(FadeOutDown(outText)));
        }

        yield return new WaitForSeconds(fadeSpeed);

        var inRoutines = new List<Coroutine>();
        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            if (block == null) continue;
            TMP_Text inText = showBalance ? block.BalanceText : block.NameText;
            inRoutines.Add(StartCoroutine(FadeInUp(inText)));
        }

        yield return new WaitForSeconds(fadeSpeed);
    }

    private IEnumerator RunSynchronizedCrownLoop()
    {
        while (true)
        {
            var firstBlocks = GetCurrentFirstBlocks();
            for (int i = 0; i < firstBlocks.Count; i++)
            {
                var block = firstBlocks[i];
                if (block == null) continue;
                block.PlayCrownPulse(crownPulseDuration);
            }

            yield return new WaitForSeconds(crownPulseInterval);
        }
    }

    private List<LeaderboardPlayerBlock> GetVisibleBlocks()
    {
        var result = new List<LeaderboardPlayerBlock>(6);
        var seen = new HashSet<int>();

        foreach (var kv in richestSlotToBlock)
        {
            if (!currentRichest.ContainsKey(kv.Key)) continue;
            var block = kv.Value;
            if (block == null) continue;
            int id = block.GetInstanceID();
            if (seen.Add(id)) result.Add(block);
        }

        foreach (var kv in winnersSlotToBlock)
        {
            if (!currentWinners.ContainsKey(kv.Key)) continue;
            var block = kv.Value;
            if (block == null) continue;
            int id = block.GetInstanceID();
            if (seen.Add(id)) result.Add(block);
        }

        return result;
    }

    private List<LeaderboardPlayerBlock> GetCurrentFirstBlocks()
    {
        var result = new List<LeaderboardPlayerBlock>(2);
        if (richestSlotToBlock.TryGetValue(0, out var richestFirst) && currentRichest.ContainsKey(0) && richestFirst != null)
        {
            result.Add(richestFirst);
        }

        if (winnersSlotToBlock.TryGetValue(0, out var winnersFirst) && currentWinners.ContainsKey(0) && winnersFirst != null)
        {
            result.Add(winnersFirst);
        }

        return result;
    }

    private IEnumerator FadeOutDown(TMP_Text textComponent)
    {
        if (textComponent == null) yield break;

        RectTransform textRect = textComponent.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = GetOrAddCanvasGroup(textComponent.gameObject);
        Vector2 startPos = textRect.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(0f, -textMoveDistance);
        float elapsed = 0f;

        while (elapsed < fadeSpeed)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeSpeed);
            float eased = DOVirtual.EasedValue(0f, 1f, t, Ease.InBack);
            textRect.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
            canvasGroup.alpha = 1f - eased;
            yield return null;
        }

        textRect.anchoredPosition = Vector2.zero;
        canvasGroup.alpha = 0f;
        textComponent.gameObject.SetActive(false);
    }

    private IEnumerator FadeInUp(TMP_Text textComponent)
    {
        if (textComponent == null) yield break;

        var cg = GetOrAddCanvasGroup(textComponent.gameObject);
        var textRect = textComponent.GetComponent<RectTransform>();
        if (textRect != null)
        {
            textRect.anchoredPosition = new Vector2(0f, -textMoveDistance);
        }

        cg.alpha = 0f;
        textComponent.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < fadeSpeed)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeSpeed);
            float eased = DOVirtual.EasedValue(0f, 1f, t, Ease.OutBack);
            cg.alpha = eased;
            if (textRect != null)
            {
                textRect.anchoredPosition = Vector2.Lerp(new Vector2(0f, -textMoveDistance), Vector2.zero, eased);
            }
            yield return null;
        }

        if (textRect != null) textRect.anchoredPosition = Vector2.zero;
        cg.alpha = 1f;
    }

    private void ResetTextState(TMP_Text textComponent)
    {
        if (textComponent == null) return;

        RectTransform textRect = textComponent.GetComponent<RectTransform>();
        if (textRect != null)
        {
            textRect.DOKill(complete: false);
            textRect.anchoredPosition = Vector2.zero;
        }

        var cg = GetOrAddCanvasGroup(textComponent.gameObject);
        cg.alpha = 1f;
        textComponent.gameObject.SetActive(true);
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        return cg != null ? cg : go.AddComponent<CanvasGroup>();
    }

    private Vector2 GetHiddenPosition(Vector2 restPos, bool isWinners)
    {
        float offset = isWinners ? hiddenOffsetX : -hiddenOffsetX;
        return new Vector2(restPos.x + offset, restPos.y);
    }

    private void AddBlockCoroutine(LeaderboardPlayerBlock block, Coroutine coroutine)
    {
        if (block == null || coroutine == null) return;

        int id = block.GetInstanceID();
        if (!blockCoroutines.ContainsKey(id))
        {
            blockCoroutines[id] = new List<Coroutine>();
        }

        blockCoroutines[id].Add(coroutine);
    }

    private void StopBlockAnimation(LeaderboardPlayerBlock block)
    {
        if (block == null) return;
        block.StopCrownPulse();

        int id = block.GetInstanceID();
        if (blockCoroutines.TryGetValue(id, out var coroutines))
        {
            foreach (var c in coroutines)
            {
                if (c == null) continue;
                try
                {
                    StopCoroutine(c);
                }
                catch
                {
                }
            }

            coroutines.Clear();
        }

        var rect = block.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.DOKill(complete: false);
        }

        if (block.NameText != null)
        {
            block.NameText.GetComponent<RectTransform>()?.DOKill(complete: false);
        }

        if (block.BalanceText != null)
        {
            block.BalanceText.GetComponent<RectTransform>()?.DOKill(complete: false);
        }
    }

    private void StopAllAnimations()
    {
        foreach (var kvp in blockCoroutines)
        {
            foreach (var c in kvp.Value)
            {
                if (c == null) continue;
                try
                {
                    StopCoroutine(c);
                }
                catch
                {
                }
            }
        }

        blockCoroutines.Clear();
        StopSynchronizedLoops();

        foreach (var block in richestBlocks)
        {
            StopBlockAnimation(block);
        }

        foreach (var block in winnersBlocks)
        {
            StopBlockAnimation(block);
        }
    }

    private Sprite PickAvatar(string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            return GetRandomAvatar();
        }

        if (!string.IsNullOrEmpty(localPlayerUsername) && localPlayerAvatar != null && username == localPlayerUsername)
        {
            return localPlayerAvatar;
        }

        if (cachedAvatars.TryGetValue(username, out var cached) && cached != null)
        {
            return cached;
        }

        var picked = GetRandomAvatar();
        if (picked != null)
        {
            cachedAvatars[username] = picked;
        }

        return picked;
    }

    private Sprite GetRandomAvatar()
    {
        if (playerAvatars == null || playerAvatars.Length == 0) return null;
        return playerAvatars[Random.Range(0, playerAvatars.Length)];
    }

    private LeaderboardEntry MakeRich(string username, double balance, int rank)
    {
        return new LeaderboardEntry
        {
            username = username,
            balance = balance,
            totalWins = 0d,
            rank = rank
        };
    }

    private LeaderboardEntry MakeWin(string username, double wins, int rank)
    {
        return new LeaderboardEntry
        {
            username = username,
            balance = 0d,
            totalWins = wins,
            rank = rank
        };
    }

    private void ApplyDebugScenario1()
    {
        UpdateLeaderboard(new Leaderboards
        {
            richest = new List<LeaderboardEntry>
      {
        MakeRich("alpha_player", 1850, 1)
      },
            winners = new List<LeaderboardEntry>
      {
        MakeWin("winner_one", 14, 1)
      }
        });
    }

    private void ApplyDebugScenario2()
    {
        UpdateLeaderboard(new Leaderboards
        {
            richest = new List<LeaderboardEntry>
      {
        MakeRich("new_top_user", 2600, 1),
        MakeRich("alpha_player", 1900, 2)
      },
            winners = new List<LeaderboardEntry>
      {
        MakeWin("big_winner", 22, 1),
        MakeWin("winner_one", 15, 2)
      }
        });
    }

    private void ApplyDebugScenario3()
    {
        UpdateLeaderboard(new Leaderboards
        {
            richest = new List<LeaderboardEntry>
      {
        MakeRich("new_top_user", 2800, 1),
        MakeRich("mid_entry", 2100, 2),
        MakeRich("alpha_player", 1950, 3)
      },
            winners = new List<LeaderboardEntry>
      {
        MakeWin("big_winner", 24, 1),
        MakeWin("new_winner_two", 19, 2),
        MakeWin("winner_one", 16, 3)
      }
        });
    }

    private void ApplyDebugScenario4()
    {
        UpdateLeaderboard(new Leaderboards
        {
            richest = new List<LeaderboardEntry>
      {
        MakeRich("new_top_user", 2850, 1),
        MakeRich("mid_entry", 2200, 2),
        MakeRich("fresh_third", 1750, 3)
      },
            winners = new List<LeaderboardEntry>
      {
        MakeWin("big_winner", 25, 1),
        MakeWin("new_winner_two", 20, 2),
        MakeWin("fresh_winner_three", 12, 3)
      }
        });
    }

    private void ApplyDebugScenario5()
    {
        UpdateLeaderboard(new Leaderboards
        {
            richest = new List<LeaderboardEntry>
      {
        MakeRich("alpha_player", 3200, 1),
        MakeRich("fresh_third", 2100, 2),
        MakeRich("new_top_user", 2000, 3)
      },
            winners = new List<LeaderboardEntry>
      {
        MakeWin("winner_one", 30, 1),
        MakeWin("fresh_winner_three", 18, 2),
        MakeWin("big_winner", 17, 3)
      }
        });
    }

    private void ApplyDebugScenario6()
    {
        UpdateLeaderboard(new Leaderboards
        {
            richest = new List<LeaderboardEntry>(),
            winners = new List<LeaderboardEntry>()
        });
    }

    private void ApplyDebugScenario7()
    {
        UpdateLeaderboard(new Leaderboards
        {
            richest = BuildRandomEntries(false),
            winners = BuildRandomEntries(true)
        });
    }

    private void ApplyDebugScenario8()
    {
        UpdateLeaderboard(new Leaderboards
        {
            richest = ShuffleExisting(currentRichest, false),
            winners = ShuffleExisting(currentWinners, true)
        });
    }

    private void ApplyDebugScenario9()
    {
        var richest = ShuffleExisting(currentRichest, false);
        var winners = ShuffleExisting(currentWinners, true);

        if (richest.Count == 0) richest = BuildRandomEntries(false);
        if (winners.Count == 0) winners = BuildRandomEntries(true);

        if (richest.Count > 0)
        {
            int idx = Random.Range(0, richest.Count);
            richest[idx].username = "r_new_" + Random.Range(10, 999);
            richest[idx].balance = Random.Range(1200f, 5800f);
        }

        if (winners.Count > 0)
        {
            int idx = Random.Range(0, winners.Count);
            winners[idx].username = "w_new_" + Random.Range(10, 999);
            winners[idx].totalWins = Random.Range(6f, 44f);
        }

        UpdateLeaderboard(new Leaderboards
        {
            richest = richest,
            winners = winners
        });
    }

    private List<LeaderboardEntry> BuildRandomEntries(bool isWinners)
    {
        int count = Random.Range(1, MaxSlots + 1);
        var entries = new List<LeaderboardEntry>(count);
        for (int i = 0; i < count; i++)
        {
            int rank = i + 1;
            if (isWinners)
            {
                entries.Add(MakeWin("w_" + Random.Range(100, 999), Random.Range(5f, 40f), rank));
            }
            else
            {
                entries.Add(MakeRich("r_" + Random.Range(100, 999), Random.Range(1000f, 6000f), rank));
            }
        }

        return entries;
    }

    private List<LeaderboardEntry> ShuffleExisting(Dictionary<int, LeaderboardEntry> current, bool isWinners)
    {
        var existing = new List<LeaderboardEntry>();
        foreach (var kv in current)
        {
            if (kv.Value == null || string.IsNullOrEmpty(kv.Value.username)) continue;
            existing.Add(new LeaderboardEntry
            {
                username = kv.Value.username,
                balance = kv.Value.balance,
                totalWins = kv.Value.totalWins,
                rank = kv.Value.rank
            });
        }

        if (existing.Count == 0) return existing;

        for (int i = existing.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var temp = existing[i];
            existing[i] = existing[j];
            existing[j] = temp;
        }

        for (int i = 0; i < existing.Count; i++)
        {
            existing[i].rank = i + 1;
            if (isWinners)
            {
                existing[i].totalWins = Mathf.Max(1f, (float)existing[i].totalWins + Random.Range(-2f, 3f));
            }
            else
            {
                existing[i].balance = Mathf.Max(100f, (float)existing[i].balance + Random.Range(-300f, 350f));
            }
        }

        return existing;
    }
}
