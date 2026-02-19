using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] private int boxOpeningStartFrame = 10;
    [SerializeField] private int boxFullyOpenFrame = 51;

    [Header("Mask — Closing Frames")]
    [SerializeField] private int boxStartClosingFrame = 5;
    [SerializeField] private int boxClosedFrame = 28;

    [Header("Mask — Scale-Up Frames")]
    [SerializeField] private int boxScaleUpStartFrame = 10;
    [SerializeField] private int boxScaleUpEndFrame = 51;

    [Header("Mask Controller")]
    [SerializeField] private DiceMaskFollowPath diceMaskFollowPath;

    [Header("Speed Control")]
    [SerializeField] private float fastForwardSpeed = 3f;
    #endregion

    #region Private Fields
    private DiceBoxState currentState = DiceBoxState.Hidden;
    private Coroutine animationCoroutine;
    private bool isAnimating = false;

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

    private bool hasPendingReveal = false;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (diceBoxContainer) diceBoxContainer.SetActive(false);
        if (diceContainer) diceContainer.SetActive(false);
    }

    private void OnDestroy() => StopAllAnimations();
    #endregion

    #region Internal API
    internal void StartAnimationCycleWithServerSync(long roundStartTimestamp, long bettingEndTimestamp, long currentServerTime)
    {
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

        hasPlayedShakeSound = false;
        hasPlayedBoxOpenSound = false;
        hasPlayedBoxCloseSound = false;

        serverTimeOffset = currentServerTime - (long)(Time.realtimeSinceStartup * 1000);

        long elapsedMs = currentServerTime - roundStartTimestamp;
        float elapsedSeconds = elapsedMs / 1000f;

        if (diceBoxContainer) diceBoxContainer.SetActive(true);
        if (diceContainer) diceContainer.SetActive(false);

        diceMaskFollowPath?.ResetToStart();
        JumpToCorrectPhase(elapsedSeconds);
    }

    internal void StartAnimationCycle()
    {
        StopAllAnimations();

        hasPlayedShakeSound = false;
        hasPlayedBoxOpenSound = false;
        hasPlayedBoxCloseSound = false;

        if (diceBoxContainer) diceBoxContainer.SetActive(true);
        if (diceContainer) diceContainer.SetActive(false);

        diceMaskFollowPath?.ResetToStart();
        PlayShakeAnimation();
    }

    internal void OnBettingLocked()
    {
        if (currentState == DiceBoxState.Idle || currentState == DiceBoxState.Shaking)
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
            PlayOpeningAnimation();
        }
        else if (currentState == DiceBoxState.Idle || currentState == DiceBoxState.Shaking)
        {
            hasPendingReveal = true;
            StopAllAnimations();
            PlayZoomInAnimation();
        }
    }

    internal void CloseAndFinish()
    {
        if (currentState == DiceBoxState.Open)
        {
            StopAllAnimations();
            PlayClosingAnimation();
        }
    }

    internal void ForceHide()
    {
        StopAllAnimations();
        if (diceBoxContainer) diceBoxContainer.SetActive(false);
        if (diceContainer) diceContainer.SetActive(false);
        diceMaskFollowPath?.ResetToStart();
        currentState = DiceBoxState.Hidden;
    }

    internal void SetDiceShowCallback(Action callback) => onDiceShouldShow = callback;
    internal void SetDiceHideCallback(Action callback) => onDiceShouldHide = callback;
    internal void SetAnimationCycleCompleteCallback(Action callback) => onAnimationCycleComplete = callback;

    internal DiceBoxState GetCurrentState() => currentState;
    internal bool IsAnimating() => isAnimating;
    internal float GetTotalCycleTime() =>
        shakeDuration + zoomInDuration + openingDuration + holdOpenDuration + closingDuration + zoomOutDuration;
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
        else if (elapsedSeconds < openingEnd)
        {
            hasPlayedShakeSound = true;
            hasPlayedBoxOpenSound = true;
            hasPendingReveal = false;
            PlayOpeningAnimation(elapsedSeconds - zoomInEnd);
        }
        else if (elapsedSeconds < holdOpenEnd)
        {
            hasPlayedShakeSound = true;
            hasPlayedBoxOpenSound = true;
            hasPendingReveal = false;

            if (openingSequence != null && openingSequence.Count > 0)
                SetDisplayToFrame(openingSequence, openingSequence.Count - 1);

            diceMaskFollowPath?.SetOpenProgress(1f);
            if (diceContainer) diceContainer.SetActive(true);
            onDiceShouldShow?.Invoke();

            currentState = DiceBoxState.Open;
            animationCoroutine = StartCoroutine(HoldOpenThenClose(holdOpenEnd - elapsedSeconds));
        }
        else if (elapsedSeconds < closingEnd)
        {
            hasPlayedShakeSound = true;
            hasPlayedBoxOpenSound = true;
            hasPlayedBoxCloseSound = true;
            hasPendingReveal = false;
            PlayClosingAnimation(elapsedSeconds - holdOpenEnd);
        }
        else if (elapsedSeconds < zoomOutEnd)
        {
            hasPlayedShakeSound = true;
            hasPlayedBoxOpenSound = true;
            hasPlayedBoxCloseSound = true;
            hasPendingReveal = false;
            PlayZoomOutAnimation(elapsedSeconds - closingEnd);
        }
        else
        {
            hasPendingReveal = false;
            currentState = DiceBoxState.Waiting;
            if (diceBoxContainer) diceBoxContainer.SetActive(false);
            onAnimationCycleComplete?.Invoke();
        }
    }
    #endregion

    #region Animation Phases
    private void PlayShakeAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.Shaking;
        playbackSpeed = 1f;

        AudioManager.Instance?.PlayShake();
        hasPlayedShakeSound = true;

        animationCoroutine = StartCoroutine(PlaySequenceCoroutine(
            shakeSequence, shakeDuration, false, false, startTime, OnShakeComplete));
    }

    private void OnShakeComplete() => PlayIdleAnimation();

    private void PlayIdleAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.Idle;
        animationCoroutine = StartCoroutine(PlaySequenceCoroutine(
            idleSequence, idleDuration, false, true, startTime, null));
    }

    private void PlayZoomInAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.ZoomingIn;
        animationCoroutine = StartCoroutine(PlaySequenceCoroutine(
            zoomInSequence, zoomInDuration, false, false, startTime, OnZoomInComplete));
    }

    private void OnZoomInComplete()
    {
        currentState = DiceBoxState.ZoomedIn;
        if (hasPendingReveal)
        {
            hasPendingReveal = false;
            PlayOpeningAnimation();
        }
    }

    private void PlayOpeningAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.Opening;

        if (diceContainer) diceContainer.SetActive(true);
        onDiceShouldShow?.Invoke();

        if (!hasPlayedBoxOpenSound)
        {
            AudioManager.Instance?.PlayBoxOpen();
            hasPlayedBoxOpenSound = true;
        }

        animationCoroutine = StartCoroutine(PlaySequenceCoroutine(
            openingSequence, openingDuration, false, false, startTime, OnOpeningComplete));
    }

    private void OnOpeningComplete()
    {
        currentState = DiceBoxState.Open;
        animationCoroutine = StartCoroutine(HoldOpenThenClose(holdOpenDuration));
    }

    private IEnumerator HoldOpenThenClose(float holdDuration)
    {
        yield return new WaitForSeconds(holdDuration / Mathf.Max(playbackSpeed, 0.01f));
        PlayClosingAnimation();
    }

    private void PlayClosingAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.Closing;

        if (!hasPlayedBoxCloseSound)
        {
            AudioManager.Instance?.PlayBoxClose();
            hasPlayedBoxCloseSound = true;
        }

        animationCoroutine = StartCoroutine(PlaySequenceCoroutine(
            closingSequence, closingDuration, false, false, startTime, OnClosingComplete));
    }

    private void OnClosingComplete()
    {
        diceMaskFollowPath?.ResetToStart();
        PlayZoomOutAnimation();
    }

    private void PlayZoomOutAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.ZoomingOut;
        animationCoroutine = StartCoroutine(PlaySequenceCoroutine(
            zoomInSequence, zoomOutDuration, true, false, startTime, OnZoomOutComplete));
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
                    if (animationImage && sequence[i]) animationImage.sprite = sequence[i];
                    yield return new WaitForSeconds(baseFrameDelay / Mathf.Max(playbackSpeed, 0.01f));
                }
            }
            else
            {
                for (int i = startFrame; i < sequence.Count; i++)
                {
                    if (animationImage && sequence[i]) animationImage.sprite = sequence[i];
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

    private void HandleFrameTriggers(int frame, int totalFrames)
    {
        if (currentState == DiceBoxState.Opening)
        {
            if (frame >= boxOpeningStartFrame && frame <= boxFullyOpenFrame)
            {
                int range = boxFullyOpenFrame - boxOpeningStartFrame;
                float progress = range > 0 ? (float)(frame - boxOpeningStartFrame) / range : 1f;
                diceMaskFollowPath?.SetOpenProgress(progress);
            }
            if (frame > boxFullyOpenFrame)
                diceMaskFollowPath?.SetOpenProgress(1f);

            if (frame >= boxScaleUpStartFrame && frame <= boxScaleUpEndFrame)
            {
                int scaleRange = boxScaleUpEndFrame - boxScaleUpStartFrame;
                float scaleProgress = scaleRange > 0 ? (float)(frame - boxScaleUpStartFrame) / scaleRange : 1f;
                diceMaskFollowPath?.SetScaleProgress(scaleProgress);
            }
            else if (frame > boxScaleUpEndFrame)
            {
                diceMaskFollowPath?.SetScaleProgress(1f);
            }
        }

        if (currentState == DiceBoxState.Closing)
        {
            if (frame >= boxStartClosingFrame && frame <= boxClosedFrame)
            {
                int range = boxClosedFrame - boxStartClosingFrame;
                float progress = range > 0 ? (float)(frame - boxStartClosingFrame) / range : 1f;
                diceMaskFollowPath?.SetCloseProgress(progress);
            }

            if (frame == boxClosedFrame)
            {
                if (diceContainer) diceContainer.SetActive(false);
                onDiceShouldHide?.Invoke();
            }

            if (frame > boxClosedFrame)
                diceMaskFollowPath?.SetCloseProgress(1f);
        }
    }

    private void SetDisplayToFrame(List<Sprite> sequence, int index)
    {
        if (animationImage && sequence != null && index >= 0 && index < sequence.Count)
            animationImage.sprite = sequence[index];
    }

    private void StopAllAnimations()
    {
        if (animationCoroutine != null) { StopCoroutine(animationCoroutine); animationCoroutine = null; }
        isAnimating = false;
    }
    #endregion
}