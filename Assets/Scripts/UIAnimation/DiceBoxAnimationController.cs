using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DiceBoxAnimationController - Reworked for FPS independence
/// Uses time-based sampling instead of frame delays for smooth, consistent playback on any device
/// </summary>
public class DiceBoxAnimationController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Base Layer")]
    [SerializeField] private Image baseImage;

    [Header("Top Layer")]
    [SerializeField] private Image topImage;
    [SerializeField] private GameObject topLayerContainer;

    [Header("Containers")]
    [SerializeField] private GameObject diceContainer;

    [Header("Base Layer Sequences")]
    [SerializeField] private List<Sprite> shakeSequence;
    [SerializeField] private List<Sprite> idleSequence;
    [SerializeField] private List<Sprite> zoomInSequence;

    [Header("Open Close Sequences")]
    [SerializeField] private List<Sprite> openCloseBaseSequence;
    [SerializeField] private List<Sprite> openCloseTopSequence;

    [Header("Timing - Animation completes in exact duration regardless of FPS")]
    [SerializeField] private float shakeDuration = 2.5f;
    [SerializeField] private float idleDuration = 4.0f;
    [SerializeField] private float zoomInDuration = 0.8f;
    [SerializeField] private float openDuration = 2.3f;
    [SerializeField] private float holdOpenDuration = 2.0f;
    [SerializeField] private float closeDuration = 1.5f;
    [SerializeField] private float zoomOutDuration = 0.9f;

    [Header("Open Close Frame Triggers")]
    [SerializeField] private int holdOnFrame = 51;
    [SerializeField] private int diceShowFrame = 40;
    [SerializeField] private int diceHideFrame = 65;
    [SerializeField] private int boxOpenSoundFrame = 0;
    [SerializeField] private int boxCloseSoundFrame = 0;
    [SerializeField] private int diceScaleStartFrame = 40;
    [SerializeField] private int diceScaleEndFrame = 51;
    [SerializeField] private float diceScaleTarget = 1.3f;
    [SerializeField] private AnimationCurve diceScaleCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 3f), 
        new Keyframe(0.5f, 0.88f, 1.2f, 0.4f), 
        new Keyframe(1f, 1f, 0.1f, 0f));
    [SerializeField] private int diceScaleResetFrameOffset = 5;

    [Header("Speed")]
    [SerializeField] private float fastForwardSpeed = 3f;

    #endregion

    #region Private Fields

    private DiceBoxState currentState = DiceBoxState.Hidden;
    private Coroutine animationCoroutine;
    private bool isAnimating = false;
    private float playbackSpeed = 1f;

    private long serverTimeOffset = 0;

    private Action onDiceShouldShow;
    private Action onDiceShouldHide;
    private Action onAnimationCycleComplete;

    private bool hasPlayedShakeSound = false;
    private bool hasPlayedBoxOpenSound = false;
    private bool hasPlayedBoxCloseSound = false;

    private bool hasPendingRound = false;
    private long pendingRoundStartTimestamp;
    private long pendingBettingEndTimestamp;
    private long pendingServerTime;

    private bool hasPendingReveal = false;

    // Frame trigger tracking to prevent duplicate calls
    private HashSet<int> triggeredFrames = new HashSet<int>();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (diceContainer) diceContainer.SetActive(false);
        SetTopLayerActive(false);
    }

    private void OnDestroy() => StopAllAnimations();

    #endregion

    #region Internal API

    internal void StartAnimationCycleWithServerSync(long roundStartTimestamp, long bettingEndTimestamp, long currentServerTime)
    {
        // Save the current state BEFORE resetting
        DiceBoxState previousState = currentState;
        
        ForceResetToCleanState();
     
        // Check the PREVIOUS state (before reset), not the current state (after reset)
        if (previousState == DiceBoxState.Opening ||
            previousState == DiceBoxState.Open ||
            previousState == DiceBoxState.Closing ||
            previousState == DiceBoxState.ZoomingOut)
        {
            // New round started while previous animation still playing - use fast-forward
            playbackSpeed = fastForwardSpeed;
            hasPendingRound = true;
            pendingRoundStartTimestamp = roundStartTimestamp;
            pendingBettingEndTimestamp = bettingEndTimestamp;
            pendingServerTime = currentServerTime;
            return;
        }
     
        playbackSpeed = 1f;
        hasPendingRound = false;
        hasPendingReveal = false;
     
        StopAllAnimations();
        ResetSoundFlags();
     
        serverTimeOffset = currentServerTime - (long)(Time.realtimeSinceStartup * 1000);
     
        float elapsedSeconds = (currentServerTime - roundStartTimestamp) / 1000f;
     
        if (diceContainer) diceContainer.SetActive(false);
        SetTopLayerActive(false);
     
        JumpToCorrectPhase(elapsedSeconds);
    }

    internal void SyncToPhaseOnJoin(string phase, long timeUntilNextRound, long serverTime)
    {
        ForceResetToCleanState();

        StopAllAnimations();
        ResetSoundFlags();

        if (diceContainer) diceContainer.SetActive(false);
        SetTopLayerActive(false);

        float secondsUntilNext = timeUntilNextRound / 1000f;

        switch (phase.ToLower())
        {
            case "betting":
                PlayIdleAnimation();
                currentState = DiceBoxState.Idle;
                break;

            case "rolling":
            case "dealing":
                PlayIdleAnimation();
                currentState = DiceBoxState.Idle;
                break;

            case "result":
                if (secondsUntilNext > 2f)
                {
                    float adjustedIdleDuration = Mathf.Max(0.5f, secondsUntilNext - 1f);
                    animationCoroutine = StartCoroutine(PlayTimedSequence(
                        idleSequence,
                        adjustedIdleDuration,
                        loop: true,
                        reverse: false,
                        startTime: 0f,
                        onComplete: null));
                    currentState = DiceBoxState.Idle;
                }
                else
                {
                    PlayIdleAnimation();
                    currentState = DiceBoxState.Idle;
                }
                break;

            case "nextround":
                if (secondsUntilNext > 2f)
                {
                    float adjustedIdleDuration = Mathf.Max(0.5f, secondsUntilNext - 1f);
                    animationCoroutine = StartCoroutine(PlayTimedSequence(
                        idleSequence,
                        adjustedIdleDuration,
                        loop: true,
                        reverse: false,
                        startTime: 0f,
                        onComplete: null));
                    currentState = DiceBoxState.Idle;
                }
                else
                {
                    PlayIdleAnimation();
                    currentState = DiceBoxState.Idle;
                }
                break;

            case "waiting":
                PlayIdleAnimation();
                currentState = DiceBoxState.Idle;
                break;

            default:
                PlayIdleAnimation();
                currentState = DiceBoxState.Idle;
                break;
        }
    }

    internal void StartAnimationCycle()
    {
        ForceResetToCleanState();

        StopAllAnimations();
        ResetSoundFlags();

        if (diceContainer) diceContainer.SetActive(false);
        SetTopLayerActive(false);

        PlayShakeAnimation();
    }

    internal void OnBettingLocked()
    {
        if (currentState == DiceBoxState.Idle || currentState == DiceBoxState.Shaking)
        {
            StopAllAnimations();
            PlayZoomInAnimation();
        }
        else if (currentState == DiceBoxState.Waiting || currentState == DiceBoxState.Hidden)
        {
            StopAllAnimations();
            PlayZoomInAnimation();
        }
    }

    internal void RevealDiceResult()
    {
        if (currentState == DiceBoxState.ZoomingIn || currentState == DiceBoxState.ZoomedIn)
        {
            hasPendingReveal = false;
            StopAllAnimations();
            PlayOpenCloseAnimation();
        }
        else if (currentState == DiceBoxState.Idle || currentState == DiceBoxState.Shaking ||
                 currentState == DiceBoxState.Waiting)
        {
            hasPendingReveal = true;
            StopAllAnimations();
            PlayZoomInAnimation();
        }
        else if (currentState == DiceBoxState.Hidden)
        {
            playbackSpeed = fastForwardSpeed;
            hasPendingReveal = true;
            StopAllAnimations();
            PlayZoomInAnimation();
        }
    }

    internal void ForceHide()
    {
        StopAllAnimations();
        if (diceContainer) diceContainer.SetActive(false);
        SetTopLayerActive(false);
        currentState = DiceBoxState.Hidden;
    }

    internal void SetDiceShowCallback(Action cb) => onDiceShouldShow = cb;
    internal void SetDiceHideCallback(Action cb) => onDiceShouldHide = cb;
    internal void SetAnimationCycleCompleteCallback(Action cb) => onAnimationCycleComplete = cb;

    internal DiceBoxState GetCurrentState() => currentState;
    internal bool IsAnimating() => isAnimating;
    internal float GetTotalCycleTime() =>
        shakeDuration + idleDuration + zoomInDuration + openDuration + holdOpenDuration + closeDuration + zoomOutDuration;

    #endregion

    #region Phase Jump

    private void JumpToCorrectPhase(float elapsedSeconds)
    {
        float shakeEnd = shakeDuration;
        float idleEnd = shakeEnd + idleDuration;
        float zoomInEnd = idleEnd + zoomInDuration;
        float openEnd = zoomInEnd + openDuration;
        float holdEnd = openEnd + holdOpenDuration;
        float closeEnd = holdEnd + closeDuration;
        float zoomOutEnd = closeEnd + zoomOutDuration;

        if (elapsedSeconds < shakeEnd)
        {
            hasPlayedShakeSound = true;
            PlayShakeAnimation(elapsedSeconds);
        }
        else if (elapsedSeconds < idleEnd)
        {
            hasPlayedShakeSound = true;
            PlayIdleAnimation(elapsedSeconds - shakeEnd);
        }
        else if (elapsedSeconds < zoomInEnd)
        {
            hasPlayedShakeSound = true;
            PlayZoomInAnimation(elapsedSeconds - idleEnd);
        }
        else if (elapsedSeconds < openEnd)
        {
            hasPlayedShakeSound = true;
            hasPlayedBoxOpenSound = true;
            hasPendingReveal = false;
            PlayOpenCloseAnimation(elapsedSeconds - zoomInEnd, OpenClosePhase.Opening);
        }
        else if (elapsedSeconds < holdEnd)
        {
            hasPlayedShakeSound = true;
            hasPlayedBoxOpenSound = true;
            hasPendingReveal = false;
            SnapOpenCloseToFrame(holdOnFrame);
            if (diceContainer) diceContainer.SetActive(true);
            onDiceShouldShow?.Invoke();
            currentState = DiceBoxState.Open;
            animationCoroutine = StartCoroutine(HoldThenClose(holdEnd - elapsedSeconds));
        }
        else if (elapsedSeconds < closeEnd)
        {
            hasPlayedShakeSound = true;
            hasPlayedBoxOpenSound = true;
            hasPlayedBoxCloseSound = true;
            hasPendingReveal = false;
            if (diceContainer) diceContainer.SetActive(true);
            onDiceShouldShow?.Invoke();
            PlayOpenCloseAnimation(elapsedSeconds - holdEnd, OpenClosePhase.Closing);
        }
        else if (elapsedSeconds < zoomOutEnd)
        {
            hasPlayedShakeSound = true;
            hasPlayedBoxOpenSound = true;
            hasPlayedBoxCloseSound = true;
            hasPendingReveal = false;
            PlayZoomOutAnimation(elapsedSeconds - closeEnd);
        }
        else
        {
            hasPendingReveal = false;
            currentState = DiceBoxState.Waiting;
            onAnimationCycleComplete?.Invoke();
        }
    }

    private enum OpenClosePhase { Opening, Closing }

    #endregion

    #region Animation Phases

    private void PlayShakeAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.Shaking;
        playbackSpeed = 1f;

        AudioManager.Instance?.PlayShake();
        hasPlayedShakeSound = true;

        animationCoroutine = StartCoroutine(PlayTimedSequence(
            shakeSequence, shakeDuration, loop: false, reverse: false,
            startTime: startTime, onComplete: OnShakeComplete));
    }

    private void OnShakeComplete() => PlayIdleAnimation();

    private void PlayIdleAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.Idle;
        animationCoroutine = StartCoroutine(PlayTimedSequence(
            idleSequence, idleDuration, loop: true, reverse: false,
            startTime: startTime, onComplete: null));
    }

    private void PlayZoomInAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.ZoomingIn;
        animationCoroutine = StartCoroutine(PlayTimedSequence(
            zoomInSequence, zoomInDuration, loop: false, reverse: false,
            startTime: startTime, onComplete: OnZoomInComplete));
    }

    private void OnZoomInComplete()
    {
        currentState = DiceBoxState.ZoomedIn;
        if (hasPendingReveal)
        {
            hasPendingReveal = false;
            playbackSpeed = 1f;
            PlayOpenCloseAnimation();
        }
    }

    private void PlayOpenCloseAnimation(float startTime = 0f, OpenClosePhase phase = OpenClosePhase.Opening)
    {
        SetTopLayerActive(true);

        if (phase == OpenClosePhase.Opening)
        {
            currentState = DiceBoxState.Opening;

            animationCoroutine = StartCoroutine(PlayOpenCloseRange(
                startFrame: 0,
                endFrame: holdOnFrame,
                duration: openDuration,
                startTime: startTime,
                onComplete: OnOpeningComplete));
        }
        else
        {
            currentState = DiceBoxState.Closing;

            int totalFrames = TotalOpenCloseFrames();
            int closeStart = Mathf.Min(holdOnFrame + 1, totalFrames - 1);

            animationCoroutine = StartCoroutine(PlayOpenCloseRange(
                startFrame: closeStart,
                endFrame: totalFrames - 1,
                duration: closeDuration,
                startTime: startTime,
                onComplete: OnClosingComplete));
        }
    }

    private void OnOpeningComplete()
    {
        currentState = DiceBoxState.Open;
        animationCoroutine = StartCoroutine(HoldThenClose(holdOpenDuration));
    }

    private IEnumerator HoldThenClose(float holdDuration)
    {
        float elapsed = 0f;
        float scaledDuration = holdDuration / Mathf.Max(playbackSpeed, 0.01f);
        
        while (elapsed < scaledDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        currentState = DiceBoxState.Closing;

        int totalFrames = TotalOpenCloseFrames();
        int closeStart = Mathf.Min(holdOnFrame + 1, totalFrames - 1);

        animationCoroutine = StartCoroutine(PlayOpenCloseRange(
            startFrame: closeStart,
            endFrame: totalFrames - 1,
            duration: closeDuration,
            startTime: 0f,
            onComplete: OnClosingComplete));
    }

    private void OnClosingComplete()
    {
        SetTopLayerActive(false);
        PlayZoomOutAnimation();
    }

    private void PlayZoomOutAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.ZoomingOut;
        animationCoroutine = StartCoroutine(PlayTimedSequence(
            zoomInSequence, zoomOutDuration, loop: false, reverse: true,
            startTime: startTime, onComplete: OnZoomOutComplete));
    }

    private void OnZoomOutComplete()
    {
        currentState = DiceBoxState.Waiting;
        playbackSpeed = 1f;
        onAnimationCycleComplete?.Invoke();

        if (hasPendingRound)
        {
            hasPendingRound = false;
            long corrected = (long)(Time.realtimeSinceStartup * 1000) + serverTimeOffset;
            StartAnimationCycleWithServerSync(pendingRoundStartTimestamp, pendingBettingEndTimestamp, corrected);
        }
    }

    #endregion

    #region Core Animation System - Time-Based Sampling (FPS Independent)

    /// <summary>
    /// Time-based animation player - samples sprite sequence based on elapsed time
    /// Guarantees animation completes in exact duration regardless of FPS
    /// </summary>
    private IEnumerator PlayTimedSequence(
        List<Sprite> sequence,
        float duration,
        bool loop,
        bool reverse,
        float startTime,
        Action onComplete)
    {
        if (sequence == null || sequence.Count == 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        isAnimating = true;
        float elapsedTime = startTime;
        float scaledDuration = duration / Mathf.Max(playbackSpeed, 0.01f);
        int frameCount = sequence.Count;
        int lastDisplayedFrame = -1;

        do
        {
            // Time-based frame calculation - FPS independent
            float normalizedTime = elapsedTime / scaledDuration;
            
            if (loop)
            {
                // Loop: wrap time to [0, 1] range
                normalizedTime = normalizedTime % 1f;
            }
            else
            {
                // Non-loop: clamp to [0, 1] range
                normalizedTime = Mathf.Clamp01(normalizedTime);
            }

            // Calculate current frame index based on time
            int frameIndex = Mathf.FloorToInt(normalizedTime * frameCount);
            frameIndex = Mathf.Clamp(frameIndex, 0, frameCount - 1);

            // Apply reverse if needed
            int displayIndex = reverse ? (frameCount - 1 - frameIndex) : frameIndex;

            // Only update sprite if frame changed (optimization)
            if (displayIndex != lastDisplayedFrame)
            {
                SetBaseFrame(sequence, displayIndex);
                lastDisplayedFrame = displayIndex;
            }

            // Check if animation completed (non-looping only)
            if (!loop && elapsedTime >= scaledDuration)
            {
                break;
            }

            // Advance time
            elapsedTime += Time.unscaledDeltaTime;
            yield return null;
        }
        while (loop || elapsedTime < scaledDuration);

        isAnimating = false;
        animationCoroutine = null;

        if (!loop) onComplete?.Invoke();
    }

    /// <summary>
    /// Time-based open/close animation - samples frame range based on elapsed time
    /// Fires frame triggers at exact frame indices
    /// </summary>
    private IEnumerator PlayOpenCloseRange(
        int startFrame,
        int endFrame,
        float duration,
        float startTime,
        Action onComplete)
    {
        int totalFrames = TotalOpenCloseFrames();
        startFrame = Mathf.Clamp(startFrame, 0, totalFrames - 1);
        endFrame = Mathf.Clamp(endFrame, 0, totalFrames - 1);

        int frameCount = Mathf.Abs(endFrame - startFrame) + 1;
        if (frameCount == 0) { onComplete?.Invoke(); yield break; }

        isAnimating = true;
        triggeredFrames.Clear(); // Reset trigger tracking
        
        float elapsedTime = startTime;
        float scaledDuration = duration / Mathf.Max(playbackSpeed, 0.01f);
        int lastDisplayedFrame = -1;

        while (elapsedTime < scaledDuration)
        {
            // Time-based frame calculation
            float normalizedTime = Mathf.Clamp01(elapsedTime / scaledDuration);
            int frameOffset = Mathf.FloorToInt(normalizedTime * frameCount);
            int currentFrame = Mathf.Clamp(startFrame + frameOffset, startFrame, endFrame);

            // Only update if frame changed
            if (currentFrame != lastDisplayedFrame)
            {
                SetOpenCloseFrameBothLayers(currentFrame);
                FireOpenCloseFrameTriggers(currentFrame);
                lastDisplayedFrame = currentFrame;
            }

            elapsedTime += Time.unscaledDeltaTime;
            yield return null;
        }

        // Ensure final frame is displayed
        SetOpenCloseFrameBothLayers(endFrame);
        FireOpenCloseFrameTriggers(endFrame);

        isAnimating = false;
        animationCoroutine = null;
        onComplete?.Invoke();
    }

    #endregion

    #region Frame Helpers

    private void SetBaseFrame(List<Sprite> sequence, int index)
    {
        if (baseImage == null || sequence == null) return;
        if (index < 0 || index >= sequence.Count) return;
        baseImage.sprite = sequence[index];
    }

    private void SetOpenCloseFrameBothLayers(int frameIndex)
    {
        if (baseImage != null && openCloseBaseSequence != null && frameIndex < openCloseBaseSequence.Count)
            baseImage.sprite = openCloseBaseSequence[frameIndex];

        if (topImage != null && openCloseTopSequence != null && frameIndex < openCloseTopSequence.Count)
            topImage.sprite = openCloseTopSequence[frameIndex];
    }

    private void SnapOpenCloseToFrame(int frameIndex)
    {
        SetTopLayerActive(true);
        SetOpenCloseFrameBothLayers(frameIndex);
        if (diceContainer) diceContainer.transform.localScale = Vector3.one;
    }

    private void FireOpenCloseFrameTriggers(int frame)
    {
        // Prevent duplicate triggers for the same frame
        if (triggeredFrames.Contains(frame)) return;
        triggeredFrames.Add(frame);

        if (frame == boxOpenSoundFrame && !hasPlayedBoxOpenSound)
        {
            AudioManager.Instance?.PlayBoxOpen();
            hasPlayedBoxOpenSound = true;
        }

        if (frame == boxCloseSoundFrame && !hasPlayedBoxCloseSound)
        {
            AudioManager.Instance?.PlayBoxClose();
            hasPlayedBoxCloseSound = true;
        }

        if (frame == diceShowFrame)
        {
            if (diceContainer)
            {
                diceContainer.SetActive(true);
                diceContainer.transform.localScale = Vector3.one;
            }
            onDiceShouldShow?.Invoke();
            AudioManager.Instance?.PlayDiceShow();
        }

        // Dice scaling logic
        if (frame >= diceScaleStartFrame && frame <= diceScaleEndFrame && diceContainer != null)
        {
            int range = diceScaleEndFrame - diceScaleStartFrame;
            float t = range > 0 ? (float)(frame - diceScaleStartFrame) / range : 1f;
            float eased = diceScaleCurve.Evaluate(t);
            float scale = Mathf.Lerp(1f, diceScaleTarget, eased);
            diceContainer.transform.localScale = new Vector3(scale, scale, scale);
        }
        else if (frame > diceScaleEndFrame && frame < diceHideFrame + diceScaleResetFrameOffset && diceContainer != null)
        {
            diceContainer.transform.localScale = new Vector3(diceScaleTarget, diceScaleTarget, diceScaleTarget);
        }
        else if (frame == diceHideFrame + diceScaleResetFrameOffset && diceContainer != null)
        {
            diceContainer.transform.localScale = Vector3.one;
        }

        if (frame == diceHideFrame)
        {
            StartCoroutine(HideDiceNextFrame());
        }
    }

    private IEnumerator HideDiceNextFrame()
    {
        yield return null;
        if (diceContainer) diceContainer.SetActive(false);
        onDiceShouldHide?.Invoke();
    }

    private int TotalOpenCloseFrames() =>
        openCloseBaseSequence != null ? openCloseBaseSequence.Count : 0;

    private void SetTopLayerActive(bool active)
    {
        if (topLayerContainer) topLayerContainer.SetActive(active);
        else if (topImage) topImage.gameObject.SetActive(active);
    }

    #endregion

    #region Utility

    private void StopAllAnimations()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }
        isAnimating = false;
    }

    private void ResetSoundFlags()
    {
        hasPlayedShakeSound = false;
        hasPlayedBoxOpenSound = false;
        hasPlayedBoxCloseSound = false;
    }

    private void ForceResetToCleanState()
    {
        StopAllAnimations();

        ResetSoundFlags();
        hasPendingRound = false;
        hasPendingReveal = false;
        playbackSpeed = 1f;
        currentState = DiceBoxState.Waiting;
        triggeredFrames.Clear();
        
        if (diceContainer)
        {
            diceContainer.SetActive(false);
            diceContainer.transform.localScale = Vector3.one;
        }
        SetTopLayerActive(false);
        if (baseImage != null && idleSequence != null && idleSequence.Count > 0)
        {
            baseImage.sprite = idleSequence[0];
        }
    }

    #endregion
}