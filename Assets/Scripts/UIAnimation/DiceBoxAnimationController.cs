using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


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

    [Header("Timing")]
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
    [SerializeField] private AnimationCurve diceScaleCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 3f), new Keyframe(0.5f, 0.88f, 1.2f, 0.4f), new Keyframe(1f, 1f, 0.1f, 0f));
    [SerializeField] private int diceScaleResetFrameOffset = 5;

    [Header("Speed")]
    [SerializeField] private float fastForwardSpeed = 3f;

    [Header("FPS Adaptation - NEW")]
    [SerializeField] private float targetFPS = 60f;
    [SerializeField] private bool enableFPSAdaptation = true;

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

    // FPS tracking - NEW
    private float averageFPS = 60f;
    private const int FPS_SAMPLE_SIZE = 30;
    private Queue<float> fpsSamples = new Queue<float>();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {

        if (diceContainer) diceContainer.SetActive(false);
        SetTopLayerActive(false);
    }

    // NEW: FPS tracking
    private void Update()
    {
        if (enableFPSAdaptation)
        {
            UpdateFPSTracking();
        }
    }

    private void OnDestroy() => StopAllAnimations();

    #endregion

    #region Internal API

    internal void StartAnimationCycleWithServerSync(long roundStartTimestamp, long bettingEndTimestamp, long currentServerTime)
    {
        ForceResetToCleanState();

        if (currentState == DiceBoxState.Opening ||
            currentState == DiceBoxState.Open ||
            currentState == DiceBoxState.Closing ||
            currentState == DiceBoxState.ZoomingOut)
        {
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
                    animationCoroutine = StartCoroutine(PlayBaseSequence(
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
                    animationCoroutine = StartCoroutine(PlayBaseSequence(
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

        animationCoroutine = StartCoroutine(PlayBaseSequence(
            shakeSequence, shakeDuration, loop: false, reverse: false,
            startTime: startTime, onComplete: OnShakeComplete));
    }

    private void OnShakeComplete() => PlayIdleAnimation();

    private void PlayIdleAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.Idle;
        animationCoroutine = StartCoroutine(PlayBaseSequence(
            idleSequence, idleDuration, loop: true, reverse: false,
            startTime: startTime, onComplete: null));
    }

    private void PlayZoomInAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.ZoomingIn;
        animationCoroutine = StartCoroutine(PlayBaseSequence(
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
        yield return new WaitForSecondsRealtime(holdDuration / Mathf.Max(playbackSpeed, 0.01f));

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

    private IEnumerator PlayTimedSequenceForRolling(float idleTime, float openCloseTime)
    {
        isAnimating = true;
        currentState = DiceBoxState.Idle;

        yield return StartCoroutine(PlayBaseSequence(
            idleSequence,
            idleTime,
            loop: true,
            reverse: false,
            startTime: 0f,
            onComplete: null));

        currentState = DiceBoxState.ZoomingIn;
        SetTopLayerActive(true);

        float adjustedOpenDuration = openCloseTime * 0.4f;
        float adjustedHoldDuration = openCloseTime * 0.3f;
        float adjustedCloseDuration = openCloseTime * 0.3f;

        int totalFrames = TotalOpenCloseFrames();
        int openEndFrame = Mathf.Min(holdOnFrame, totalFrames - 1);

        yield return StartCoroutine(PlayOpenCloseRange(
            startFrame: 0,
            endFrame: openEndFrame,
            duration: adjustedOpenDuration,
            startTime: 0f,
            onComplete: null));

        currentState = DiceBoxState.Open;
        yield return new WaitForSecondsRealtime(adjustedHoldDuration);

        currentState = DiceBoxState.Closing;
        yield return StartCoroutine(PlayOpenCloseRange(
            startFrame: openEndFrame,
            endFrame: totalFrames - 1,
            duration: adjustedCloseDuration,
            startTime: 0f,
            onComplete: null));

        SetTopLayerActive(false);
        if (diceContainer) diceContainer.SetActive(false);
        currentState = DiceBoxState.Waiting;
        isAnimating = false;
    }

    private void OnClosingComplete()
    {
        SetTopLayerActive(false);
        PlayZoomOutAnimation();
    }

    private void PlayZoomOutAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.ZoomingOut;
        animationCoroutine = StartCoroutine(PlayBaseSequence(
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

    #region Core Coroutines

    private IEnumerator PlayBaseSequence(
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
        float frameDelay = duration / sequence.Count;

        int startFrame = Mathf.FloorToInt(startTime / frameDelay);
        float timeIntoStartFrame = startTime - startFrame * frameDelay;

        if (timeIntoStartFrame > 0f && startFrame < sequence.Count)
        {
            int displayIdx = reverse ? (sequence.Count - 1 - startFrame) : startFrame;
            SetBaseFrame(sequence, displayIdx);

            // MODIFIED: Apply FPS multiplier
            float fpsMultiplier = GetFPSMultiplier();
            yield return WaitForSecondsAccurate((frameDelay - timeIntoStartFrame) * fpsMultiplier / Mathf.Max(playbackSpeed, 0.01f));
            startFrame++;
        }

        do
        {
            int iFrom = reverse ? (sequence.Count - 1 - startFrame) : startFrame;
            int iTo = reverse ? 0 : (sequence.Count - 1);
            int step = reverse ? -1 : 1;

            for (int i = iFrom; reverse ? (i >= iTo) : (i <= iTo); i += step)
            {
                SetBaseFrame(sequence, i);

                // MODIFIED: Apply FPS multiplier
                float fpsMultiplier = GetFPSMultiplier();
                yield return WaitForSecondsAccurate(frameDelay * fpsMultiplier / Mathf.Max(playbackSpeed, 0.01f));
            }

            startFrame = 0;
        }
        while (loop && isAnimating);

        isAnimating = false;
        animationCoroutine = null;

        if (!loop) onComplete?.Invoke();
    }

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
        float frameDelay = duration / frameCount;

        int skipFrames = Mathf.FloorToInt(startTime / frameDelay);
        float timeIntoSkipFrame = startTime - skipFrames * frameDelay;
        int currentFrame = Mathf.Min(startFrame + skipFrames, endFrame);

        if (timeIntoSkipFrame > 0f && currentFrame <= endFrame)
        {
            SetOpenCloseFrameBothLayers(currentFrame);
            FireOpenCloseFrameTriggers(currentFrame);

            float fpsMultiplier = GetFPSMultiplier();
            yield return WaitForSecondsAccurate((frameDelay - timeIntoSkipFrame) * fpsMultiplier / Mathf.Max(playbackSpeed, 0.01f));
            currentFrame++;
        }

        for (int frame = currentFrame; frame <= endFrame; frame++)
        {
            SetOpenCloseFrameBothLayers(frame);
            FireOpenCloseFrameTriggers(frame);
            float fpsMultiplier = GetFPSMultiplier();
            yield return WaitForSecondsAccurate(frameDelay * fpsMultiplier / Mathf.Max(playbackSpeed, 0.01f));
        }

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
        yield return null;  // skip to end of current frame
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

    private IEnumerator WaitForSecondsAccurate(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // NEW: FPS tracking methods
    private void UpdateFPSTracking()
    {
        float currentFPS = 1f / Time.unscaledDeltaTime;

        fpsSamples.Enqueue(currentFPS);
        if (fpsSamples.Count > FPS_SAMPLE_SIZE)
        {
            fpsSamples.Dequeue();
        }

        float sum = 0f;
        foreach (float fps in fpsSamples)
        {
            sum += fps;
        }
        averageFPS = sum / fpsSamples.Count;
    }

    private float GetFPSMultiplier()
    {
        if (!enableFPSAdaptation) return 1f;

        // Adjust timing to ensure smooth animation completion at any FPS
        // At lower FPS (30): multiply delays to stretch animation, ensuring all sprites show
        // At higher FPS (120): reduce delays to maintain visual flow
        // Formula: targetFPS / averageFPS  
        // - At 30 FPS: 60/30 = 2.0 (each sprite shown longer)
        // - At 60 FPS: 60/60 = 1.0 (normal timing)
        // - At 120 FPS: 60/120 = 0.5 (each sprite shown shorter)
        float ratio = targetFPS / averageFPS;

        return Mathf.Clamp(ratio, 0.6f, 1.8f);
    }

    #endregion


}