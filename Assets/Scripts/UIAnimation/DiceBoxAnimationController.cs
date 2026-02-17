using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dice box animation controller with SERVER TIME SYNCHRONIZATION - AUDIO INTEGRATED
/// CHANGES vs original:
///   + DiceMaskFollowPath reference — mask driven per-frame to match sprite animation
///   + boxOpeningStartFrame / boxFullyOpenFrame — mask slides open between these frames
///   + boxStartClosingFrame / boxClosedFrame    — mask slides closed between these frames
///   - Removed diceVisibleAtOpeningFrame / diceHiddenAtClosingFrame (replaced by mask frames)
///   * Shake sound restored to original: always plays unconditionally in PlayShakeAnimation
/// </summary>
public class DiceBoxAnimationController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Animation Sequences")]
    [SerializeField] private List<Sprite> shakeSequence;
    [SerializeField] private List<Sprite> idleSequence;
    [SerializeField] private List<Sprite> zoomInSequence;
    [SerializeField] private List<Sprite> openingSequence;
    [SerializeField] private List<Sprite> closingSequence;

    [Header("UI References")]
    [SerializeField] private Image animationImage;
    [SerializeField] private GameObject diceBoxContainer;
    [SerializeField] private GameObject diceContainer;

    [Header("Timing Configuration")]
    [SerializeField] private float shakeDuration = 2.5f;
    [SerializeField] private float idleDuration = 4f;
    [SerializeField] private float zoomInDuration = 0.8f;
    [SerializeField] private float openingDuration = 2.3f;
    [SerializeField] private float holdOpenDuration = 0.5f;
    [SerializeField] private float closingDuration = 1.5f;
    [SerializeField] private float zoomOutDuration = 0.9f;

    [Header("Mask — Opening Frames")]
    [Tooltip("Frame in openingSequence where the box lid starts moving (mask starts sliding).")]
    [SerializeField] private int boxOpeningStartFrame = 10;
    [Tooltip("Frame in openingSequence where the box is fully open (mask reaches end position).")]
    [SerializeField] private int boxFullyOpenFrame = 51;

    [Header("Mask — Closing Frames")]
    [Tooltip("Frame in closingSequence where the box lid starts closing (mask starts sliding back).")]
    [SerializeField] private int boxStartClosingFrame = 5;
    [Tooltip("Frame in closingSequence where the box is fully closed (mask back at start position).")]
    [SerializeField] private int boxClosedFrame = 28;

    [Header("Mask — Scale-Up Frames")]
    [Tooltip("Frame in openingSequence where the mask starts scaling up (1,1,1 → targetScale).\n" +
             "Before this frame scale stays at 1,1,1.")]
    [SerializeField] private int boxScaleUpStartFrame = 10;
    [Tooltip("Frame in openingSequence where the mask reaches full scale.\n" +
             "After this frame scale is held at targetScale.")]
    [SerializeField] private int boxScaleUpEndFrame = 51;

    [Header("Mask Controller")]
    [SerializeField] private DiceMaskFollowPath diceMaskFollowPath;

    [Header("Speed Control")]
    [Tooltip("Speed multiplier applied to post-result animations when a new round starts early.")]
    [SerializeField] private float fastForwardSpeed = 3f;
    #endregion

    #region Private Fields
    private DiceBoxState currentState = DiceBoxState.Hidden;
    private Coroutine animationCoroutine;
    private bool isAnimating = false;

    private long roundStartTime = 0;
    private long bettingEndTime = 0;
    private long serverTimeOffset = 0;

    private Action onDiceShouldShow;
    private Action onDiceShouldHide;
    private Action onAnimationCycleComplete;

    private bool hasPlayedShakeSound = false;
    private bool hasPlayedBoxOpenSound = false;
    private bool hasPlayedBoxCloseSound = false;

    private float playbackSpeed = 1f;

    private bool hasPendingRound = false;
    private long pendingRoundStartTimestamp;
    private long pendingBettingEndTimestamp;
    private long pendingServerTime;

    // FIX Issue 2: store a pending reveal request so RevealDiceResult() can be
    // honoured even when it arrives before ZoomIn completes (lag scenario)
    private bool hasPendingReveal = false;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        ValidateSetup();
        if (diceBoxContainer) diceBoxContainer.SetActive(false);
        if (diceContainer) diceContainer.SetActive(false);
    }

    private void OnDestroy() => StopAllAnimations();
    #endregion

    #region Public API
    public void StartAnimationCycleWithServerSync(long roundStartTimestamp, long bettingEndTimestamp, long currentServerTime)
    {
        Debug.Log($"[DiceBoxAnim] Starting animation cycle with server sync");
        Debug.Log($"[DiceBoxAnim] Round started at: {roundStartTimestamp}");
        Debug.Log($"[DiceBoxAnim] Betting ends at: {bettingEndTimestamp}");
        Debug.Log($"[DiceBoxAnim] Current server time: {currentServerTime}");

        if (currentState == DiceBoxState.Opening ||
            currentState == DiceBoxState.Open ||
            currentState == DiceBoxState.Closing ||
            currentState == DiceBoxState.ZoomingOut)
        {
            Debug.Log($"[DiceBoxAnim] New round queued - fast-forwarding current {currentState} animation at {fastForwardSpeed}x speed");
            playbackSpeed = fastForwardSpeed;
            hasPendingRound = true;
            pendingRoundStartTimestamp = roundStartTimestamp;
            pendingBettingEndTimestamp = bettingEndTimestamp;
            pendingServerTime = currentServerTime;
            return;
        }

        playbackSpeed = 1f;
        hasPendingRound = false;
        hasPendingReveal = false;   // FIX: clear any stale reveal from previous round

        StopAllAnimations();

        hasPlayedShakeSound = false;
        hasPlayedBoxOpenSound = false;
        hasPlayedBoxCloseSound = false;

        roundStartTime = roundStartTimestamp;
        bettingEndTime = bettingEndTimestamp;
        serverTimeOffset = currentServerTime - (long)(Time.realtimeSinceStartup * 1000);

        long elapsedMs = currentServerTime - roundStartTimestamp;
        float elapsedSeconds = elapsedMs / 1000f;

        Debug.Log($"[DiceBoxAnim] Elapsed time since round start: {elapsedSeconds:F2}s");

        if (diceBoxContainer) diceBoxContainer.SetActive(true);
        if (diceContainer) diceContainer.SetActive(false);

        diceMaskFollowPath?.ResetToStart();   // resets position AND scale to 1,1,1

        JumpToCorrectPhase(elapsedSeconds);
    }

    public void StartAnimationCycle()
    {
        Debug.Log("[DiceBoxAnim] Starting new animation cycle (legacy)");
        StopAllAnimations();

        hasPlayedShakeSound = false;
        hasPlayedBoxOpenSound = false;
        hasPlayedBoxCloseSound = false;

        if (diceBoxContainer) diceBoxContainer.SetActive(true);
        if (diceContainer) diceContainer.SetActive(false);

        diceMaskFollowPath?.ResetToStart();

        PlayShakeAnimation();
    }

    public void OnBettingLocked()
    {
        Debug.Log($"[DiceBoxAnim] Betting locked - current state: {currentState}");

        if (currentState == DiceBoxState.Idle)
        {
            // Normal path: idle loop was running, kill it and zoom in
            StopAllAnimations();
            PlayZoomInAnimation();
        }
        else if (currentState == DiceBoxState.Shaking)
        {
            // FIX Issue 1: Edge case — mid-round join with very little time left,
            // betting ends while shake animation is still playing.
            // Skip straight to ZoomIn so the reveal path is unblocked.
            Debug.LogWarning("[DiceBoxAnim] Betting locked during Shake — skipping to ZoomIn");
            StopAllAnimations();
            PlayZoomInAnimation();
        }
        else if (currentState == DiceBoxState.ZoomingIn ||
                 currentState == DiceBoxState.ZoomedIn)
        {
            // Already past betting phase — do nothing
            Debug.Log("[DiceBoxAnim] Betting locked: already ZoomingIn/ZoomedIn, no action needed");
        }
        else
        {
            Debug.Log($"[DiceBoxAnim] Betting locked: unexpected state {currentState}, ignoring");
        }
    }

    public void RevealDiceResult()
    {
        Debug.Log($"[DiceBoxAnim] Revealing dice result (state={currentState})");

        if (currentState == DiceBoxState.ZoomingIn || currentState == DiceBoxState.ZoomedIn)
        {
            // Normal path: ZoomIn is complete or in progress, open immediately
            hasPendingReveal = false;
            StopAllAnimations();
            PlayOpeningAnimation();
        }
        else if (currentState == DiceBoxState.Idle ||
                 currentState == DiceBoxState.Shaking)
        {
            // FIX Issue 2: Result arrived early (lag) — store it and it will fire
            // as soon as OnZoomInComplete() or OnBettingLocked() is processed.
            Debug.LogWarning($"[DiceBoxAnim] RevealDiceResult called in {currentState} — storing as pending, forcing ZoomIn now");
            hasPendingReveal = true;
            StopAllAnimations();
            PlayZoomInAnimation();
        }
        else if (currentState == DiceBoxState.Opening ||
                 currentState == DiceBoxState.Open ||
                 currentState == DiceBoxState.Closing)
        {
            // Already revealing or finishing — ignore duplicate
            Debug.Log($"[DiceBoxAnim] RevealDiceResult: already in {currentState}, ignoring");
        }
        else
        {
            Debug.LogWarning($"[DiceBoxAnim] RevealDiceResult called in unexpected state: {currentState}");
        }
    }

    public void CloseAndFinish()
    {
        Debug.Log("[DiceBoxAnim] Starting close sequence");
        if (currentState == DiceBoxState.Open)
        {
            StopAllAnimations();
            PlayClosingAnimation();
        }
    }

    public void ForceHide()
    {
        Debug.Log("[DiceBoxAnim] Force hiding");
        StopAllAnimations();
        if (diceBoxContainer) diceBoxContainer.SetActive(false);
        if (diceContainer) diceContainer.SetActive(false);
        diceMaskFollowPath?.ResetToStart();
        currentState = DiceBoxState.Hidden;
    }

    public void SetDiceShowCallback(Action callback) => onDiceShouldShow = callback;
    public void SetDiceHideCallback(Action callback) => onDiceShouldHide = callback;
    public void SetAnimationCycleCompleteCallback(Action callback) => onAnimationCycleComplete = callback;
    #endregion

    #region Phase Jump Logic
    private void JumpToCorrectPhase(float elapsedSeconds)
    {
        float shakeEnd = shakeDuration;
        float idleEnd = shakeEnd + idleDuration;
        float zoomInEnd = idleEnd + zoomInDuration;
        float openingEnd = zoomInEnd + openingDuration;
        float holdOpenEnd = openingEnd + holdOpenDuration;
        float closingEnd = holdOpenEnd + closingDuration;
        float zoomOutEnd = closingEnd + zoomOutDuration;

        Debug.Log($"[DiceBoxAnim] Phase boundaries:");
        Debug.Log($"  Shake: 0 - {shakeEnd:F2}s");
        Debug.Log($"  Idle: {shakeEnd:F2} - {idleEnd:F2}s");
        Debug.Log($"  Zoom In: {idleEnd:F2} - {zoomInEnd:F2}s");
        Debug.Log($"  Opening: {zoomInEnd:F2} - {openingEnd:F2}s");
        Debug.Log($"  Hold Open: {openingEnd:F2} - {holdOpenEnd:F2}s");
        Debug.Log($"  Closing: {holdOpenEnd:F2} - {closingEnd:F2}s");
        Debug.Log($"  Zoom Out: {closingEnd:F2} - {zoomOutEnd:F2}s");

        if (elapsedSeconds < shakeEnd)
        {
            Debug.Log($"[DiceBoxAnim] Joining during SHAKE phase ({elapsedSeconds:F2}s into shake)");
            hasPlayedShakeSound = true;
            PlayShakeAnimation(elapsedSeconds);
        }
        else if (elapsedSeconds < idleEnd)
        {
            Debug.Log($"[DiceBoxAnim] Joining during IDLE phase ({(elapsedSeconds - shakeEnd):F2}s into idle)");
            hasPlayedShakeSound = true;
            PlayIdleAnimation(elapsedSeconds - shakeEnd);
        }
        else if (elapsedSeconds < zoomInEnd)
        {
            Debug.Log($"[DiceBoxAnim] Joining during ZOOM IN phase ({(elapsedSeconds - idleEnd):F2}s into zoom)");
            hasPlayedShakeSound = true;
            PlayZoomInAnimation(elapsedSeconds - idleEnd);
        }
        else if (elapsedSeconds < openingEnd)
        {
            Debug.Log($"[DiceBoxAnim] Joining during OPENING phase ({(elapsedSeconds - zoomInEnd):F2}s into opening)");
            hasPlayedShakeSound = true;
            hasPlayedBoxOpenSound = true;
            hasPendingReveal = false; // already opening — no need for pending
            PlayOpeningAnimation(elapsedSeconds - zoomInEnd);
        }
        else if (elapsedSeconds < holdOpenEnd)
        {
            Debug.Log($"[DiceBoxAnim] Joining during HOLD OPEN phase");
            hasPlayedShakeSound = true;
            hasPlayedBoxOpenSound = true;
            hasPendingReveal = false;

            if (openingSequence != null && openingSequence.Count > 0)
                SetDisplayToFrame(openingSequence, openingSequence.Count - 1);

            // Joining during hold — mask is fully open, dice visible
            diceMaskFollowPath?.SetOpenProgress(1f);
            if (diceContainer) diceContainer.SetActive(true);
            onDiceShouldShow?.Invoke();

            currentState = DiceBoxState.Open;
            animationCoroutine = StartCoroutine(HoldOpenThenClose(holdOpenEnd - elapsedSeconds));
        }
        else if (elapsedSeconds < closingEnd)
        {
            Debug.Log($"[DiceBoxAnim] Joining during CLOSING phase ({(elapsedSeconds - holdOpenEnd):F2}s into closing)");
            hasPlayedShakeSound = true;
            hasPlayedBoxOpenSound = true;
            hasPlayedBoxCloseSound = true;
            hasPendingReveal = false;
            PlayClosingAnimation(elapsedSeconds - holdOpenEnd);
        }
        else if (elapsedSeconds < zoomOutEnd)
        {
            Debug.Log($"[DiceBoxAnim] Joining during ZOOM OUT phase ({(elapsedSeconds - closingEnd):F2}s into zoom out)");
            hasPlayedShakeSound = true;
            hasPlayedBoxOpenSound = true;
            hasPlayedBoxCloseSound = true;
            hasPendingReveal = false;
            PlayZoomOutAnimation(elapsedSeconds - closingEnd);
        }
        else
        {
            Debug.Log("[DiceBoxAnim] Joining after cycle complete — waiting state");
            hasPendingReveal = false;
            currentState = DiceBoxState.Waiting;
            if (diceBoxContainer) diceBoxContainer.SetActive(false);
            onAnimationCycleComplete?.Invoke();
        }
    }
    #endregion

    #region Individual Animation Phases
    private void PlayShakeAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.Shaking;
        playbackSpeed = 1f;
        Debug.Log("[DiceBoxAnim] Playing shake animation");

        // ── ORIGINAL: always plays unconditionally ────────────────────────
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayShake();
            Debug.Log("[DiceBoxAnim] Shake sound played");
        }
        hasPlayedShakeSound = true;
        // ─────────────────────────────────────────────────────────────────

        animationCoroutine = StartCoroutine(PlaySequenceCoroutine(
            shakeSequence, shakeDuration, false, false, startTime, OnShakeComplete));
    }

    private void OnShakeComplete()
    {
        Debug.Log("[DiceBoxAnim] Shake complete");
        PlayIdleAnimation();
    }

    private void PlayIdleAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.Idle;
        Debug.Log("[DiceBoxAnim] Playing idle animation (loops until betting ends)");

        animationCoroutine = StartCoroutine(PlaySequenceCoroutine(
            idleSequence, idleDuration, false, true, startTime, null));
    }

    private void PlayZoomInAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.ZoomingIn;
        Debug.Log("[DiceBoxAnim] Playing zoom in animation");

        animationCoroutine = StartCoroutine(PlaySequenceCoroutine(
            zoomInSequence, zoomInDuration, false, false, startTime, OnZoomInComplete));
    }

    private void OnZoomInComplete()
    {
        Debug.Log("[DiceBoxAnim] Zoom in complete — waiting for dice result");
        currentState = DiceBoxState.ZoomedIn;

        // FIX Issue 2: if RevealDiceResult() already arrived while we were zooming
        // (lag scenario), play opening immediately instead of waiting forever
        if (hasPendingReveal)
        {
            Debug.Log("[DiceBoxAnim] Pending reveal found — opening immediately");
            hasPendingReveal = false;
            PlayOpeningAnimation();
        }
    }

    private void PlayOpeningAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.Opening;
        Debug.Log("[DiceBoxAnim] Playing opening animation");

        // Activate dice BEFORE the animation starts so they sit behind the closed mask.
        // The mask then reveals them naturally as it slides/scales — no pop.
        if (diceContainer) diceContainer.SetActive(true);
        onDiceShouldShow?.Invoke();

        if (!hasPlayedBoxOpenSound && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBoxOpen();
            hasPlayedBoxOpenSound = true;
        }

        animationCoroutine = StartCoroutine(PlaySequenceCoroutine(
            openingSequence, openingDuration, false, false, startTime, OnOpeningComplete));
    }

    private void OnOpeningComplete()
    {
        Debug.Log("[DiceBoxAnim] Opening complete — holding open");
        currentState = DiceBoxState.Open;
        animationCoroutine = StartCoroutine(HoldOpenThenClose(holdOpenDuration));
    }

    private IEnumerator HoldOpenThenClose(float holdDuration)
    {
        Debug.Log($"[DiceBoxAnim] Holding open for {holdDuration:F2}s");
        yield return new WaitForSeconds(holdDuration / Mathf.Max(playbackSpeed, 0.01f));
        PlayClosingAnimation();
    }

    private void PlayClosingAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.Closing;
        Debug.Log("[DiceBoxAnim] Playing closing animation");

        if (!hasPlayedBoxCloseSound && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBoxClose();
            hasPlayedBoxCloseSound = true;
        }

        animationCoroutine = StartCoroutine(PlaySequenceCoroutine(
            closingSequence, closingDuration, false, false, startTime, OnClosingComplete));
    }

    private void OnClosingComplete()
    {
        Debug.Log("[DiceBoxAnim] Closing complete");
        diceMaskFollowPath?.ResetToStart();
        PlayZoomOutAnimation();
    }

    private void PlayZoomOutAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.ZoomingOut;
        Debug.Log("[DiceBoxAnim] Playing zoom out animation");

        animationCoroutine = StartCoroutine(PlaySequenceCoroutine(
            zoomInSequence, zoomOutDuration, true, false, startTime, OnZoomOutComplete));
    }

    private void OnZoomOutComplete()
    {
        Debug.Log("[DiceBoxAnim] Zoom out complete — cycle finished");
        currentState = DiceBoxState.Waiting;
        playbackSpeed = 1f;

        onAnimationCycleComplete?.Invoke();

        if (hasPendingRound)
        {
            Debug.Log("[DiceBoxAnim] Starting queued pending round");
            hasPendingRound = false;
            long corrected = (long)(Time.realtimeSinceStartup * 1000) + serverTimeOffset;
            StartAnimationCycleWithServerSync(pendingRoundStartTimestamp, pendingBettingEndTimestamp, corrected);
        }
    }
    #endregion

    #region Core Animation Playback
    private IEnumerator PlaySequenceCoroutine(
        List<Sprite> sequence,
        float duration,
        bool reverse,
        bool loop,
        float startTime,
        Action onComplete)
    {
        if (sequence == null || sequence.Count == 0)
        {
            Debug.LogWarning($"[DiceBoxAnim] Empty sequence in state {currentState}");
            onComplete?.Invoke();
            yield break;
        }

        isAnimating = true;
        float baseFrameDelay = duration / sequence.Count;

        int startFrame = Mathf.FloorToInt(startTime / baseFrameDelay);
        float timeIntoStartFrame = startTime - (startFrame * baseFrameDelay);

        if (timeIntoStartFrame > 0 && startFrame < sequence.Count)
        {
            float remaining = (baseFrameDelay - timeIntoStartFrame) / Mathf.Max(playbackSpeed, 0.01f);

            if (reverse)
            {
                int ri = sequence.Count - 1 - startFrame;
                if (ri >= 0) SetDisplayToFrame(sequence, ri);
            }
            else
            {
                SetDisplayToFrame(sequence, startFrame);
                HandleFrameTriggers(startFrame, sequence.Count);
            }

            yield return new WaitForSeconds(remaining);
            startFrame++;
        }

        do
        {
            if (reverse)
            {
                for (int i = sequence.Count - 1 - startFrame; i >= 0; i--)
                {
                    if (animationImage && sequence[i])
                        animationImage.sprite = sequence[i];

                    yield return new WaitForSeconds(baseFrameDelay / Mathf.Max(playbackSpeed, 0.01f));
                }
            }
            else
            {
                for (int i = startFrame; i < sequence.Count; i++)
                {
                    if (animationImage && sequence[i])
                        animationImage.sprite = sequence[i];

                    HandleFrameTriggers(i, sequence.Count);

                    yield return new WaitForSeconds(baseFrameDelay / Mathf.Max(playbackSpeed, 0.01f));
                }
            }

            startFrame = 0;
        }
        while (loop && isAnimating);

        isAnimating = false;
        animationCoroutine = null;

        if (!loop) onComplete?.Invoke();
    }

    /// <summary>
    /// Called every frame during playback.
    /// During Opening: drives mask open  between boxOpeningStartFrame → boxFullyOpenFrame
    /// During Closing: drives mask close between boxStartClosingFrame → boxClosedFrame
    /// Also shows/hides diceContainer at the right moment.
    /// </summary>
    private void HandleFrameTriggers(int frame, int totalFrames)
    {
        // ── OPENING sequence ──────────────────────────────────────────────────
        if (currentState == DiceBoxState.Opening)
        {
            // Drive mask progress between start and fully-open frames
            if (frame >= boxOpeningStartFrame && frame <= boxFullyOpenFrame)
            {
                int range = boxFullyOpenFrame - boxOpeningStartFrame;
                float progress = range > 0
                                 ? (float)(frame - boxOpeningStartFrame) / range
                                 : 1f;
                diceMaskFollowPath?.SetOpenProgress(progress);
            }

            // Hold mask at end after fully open
            if (frame > boxFullyOpenFrame)
            {
                diceMaskFollowPath?.SetOpenProgress(1f);
            }

            // Scale mask up, frame-synced between boxScaleUpStartFrame → boxScaleUpEndFrame.
            // rawT is computed here (same pattern as SetOpenProgress above) and passed to the mask.
            if (frame >= boxScaleUpStartFrame && frame <= boxScaleUpEndFrame)
            {
                int scaleRange = boxScaleUpEndFrame - boxScaleUpStartFrame;
                float scaleProgress = scaleRange > 0
                                      ? (float)(frame - boxScaleUpStartFrame) / scaleRange
                                      : 1f;
                diceMaskFollowPath?.SetScaleProgress(scaleProgress);
            }
            else if (frame > boxScaleUpEndFrame)
            {
                diceMaskFollowPath?.SetScaleProgress(1f);
            }
        }

        // ── CLOSING sequence ──────────────────────────────────────────────────
        if (currentState == DiceBoxState.Closing)
        {
            // Drive mask progress between start-closing and fully-closed frames
            if (frame >= boxStartClosingFrame && frame <= boxClosedFrame)
            {
                int range = boxClosedFrame - boxStartClosingFrame;
                float progress = range > 0
                                 ? (float)(frame - boxStartClosingFrame) / range
                                 : 1f;
                diceMaskFollowPath?.SetCloseProgress(progress);
            }

            // Hide dice when box is fully closed
            if (frame == boxClosedFrame)
            {
                Debug.Log($"[DiceBoxAnim] Frame {frame}: hiding dice");
                if (diceContainer) diceContainer.SetActive(false);
                onDiceShouldHide?.Invoke();
            }

            // Hold mask at start after fully closed
            if (frame > boxClosedFrame)
            {
                diceMaskFollowPath?.SetCloseProgress(1f);
            }
        }
    }

    private void SetDisplayToFrame(List<Sprite> sequence, int index)
    {
        if (animationImage && sequence != null && index >= 0 && index < sequence.Count)
            animationImage.sprite = sequence[index];
    }

    private void StopAllAnimations()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }
        isAnimating = false;
    }
    #endregion

    #region Validation
    private void ValidateSetup()
    {
        if (animationImage == null) Debug.LogError("[DiceBoxAnim] animationImage not assigned!");
        if (diceBoxContainer == null) Debug.LogWarning("[DiceBoxAnim] diceBoxContainer not assigned!");
        if (diceContainer == null) Debug.LogWarning("[DiceBoxAnim] diceContainer not assigned!");
        if (diceMaskFollowPath == null) Debug.LogWarning("[DiceBoxAnim] diceMaskFollowPath not assigned — mask disabled.");

        if (shakeSequence == null || shakeSequence.Count == 0) Debug.LogWarning("[DiceBoxAnim] shakeSequence empty!");
        if (idleSequence == null || idleSequence.Count == 0) Debug.LogWarning("[DiceBoxAnim] idleSequence empty!");
        if (zoomInSequence == null || zoomInSequence.Count == 0) Debug.LogWarning("[DiceBoxAnim] zoomInSequence empty!");
        if (openingSequence == null || openingSequence.Count == 0) Debug.LogWarning("[DiceBoxAnim] openingSequence empty!");
        if (closingSequence == null || closingSequence.Count == 0) Debug.LogWarning("[DiceBoxAnim] closingSequence empty!");

        if (openingSequence != null)
        {
            if (boxOpeningStartFrame >= openingSequence.Count)
                Debug.LogWarning($"[DiceBoxAnim] boxOpeningStartFrame ({boxOpeningStartFrame}) >= openingSequence length ({openingSequence.Count})!");
            if (boxFullyOpenFrame >= openingSequence.Count)
                Debug.LogWarning($"[DiceBoxAnim] boxFullyOpenFrame ({boxFullyOpenFrame}) >= openingSequence length ({openingSequence.Count})!");
            if (boxOpeningStartFrame >= boxFullyOpenFrame)
                Debug.LogWarning("[DiceBoxAnim] boxOpeningStartFrame should be less than boxFullyOpenFrame!");

            if (boxScaleUpStartFrame >= openingSequence.Count)
                Debug.LogWarning($"[DiceBoxAnim] boxScaleUpStartFrame ({boxScaleUpStartFrame}) >= openingSequence length ({openingSequence.Count})!");
            if (boxScaleUpEndFrame >= openingSequence.Count)
                Debug.LogWarning($"[DiceBoxAnim] boxScaleUpEndFrame ({boxScaleUpEndFrame}) >= openingSequence length ({openingSequence.Count})!");
            if (boxScaleUpStartFrame >= boxScaleUpEndFrame)
                Debug.LogWarning("[DiceBoxAnim] boxScaleUpStartFrame should be less than boxScaleUpEndFrame!");
        }

        if (closingSequence != null)
        {
            if (boxStartClosingFrame >= closingSequence.Count)
                Debug.LogWarning($"[DiceBoxAnim] boxStartClosingFrame ({boxStartClosingFrame}) >= closingSequence length ({closingSequence.Count})!");
            if (boxClosedFrame >= closingSequence.Count)
                Debug.LogWarning($"[DiceBoxAnim] boxClosedFrame ({boxClosedFrame}) >= closingSequence length ({closingSequence.Count})!");
            if (boxStartClosingFrame >= boxClosedFrame)
                Debug.LogWarning("[DiceBoxAnim] boxStartClosingFrame should be less than boxClosedFrame!");
        }

        Debug.Log("[DiceBoxAnim] Validation complete");
    }
    #endregion

    #region Public Getters
    public DiceBoxState GetCurrentState() => currentState;
    public bool IsAnimating() => isAnimating;
    public float GetTotalCycleTime() =>
        shakeDuration + zoomInDuration + openingDuration + holdOpenDuration + closingDuration + zoomOutDuration;
    #endregion
}

public enum DiceBoxState
{
    Hidden, Shaking, Idle, ZoomingIn, ZoomedIn, Opening, Open, Closing, ZoomingOut, Waiting
}