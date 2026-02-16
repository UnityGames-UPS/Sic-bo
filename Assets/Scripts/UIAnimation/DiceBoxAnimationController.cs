using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dice box animation controller with SERVER TIME SYNCHRONIZATION - AUDIO INTEGRATED
/// Now handles mid-round joins by calculating elapsed time and jumping to correct animation phase
/// </summary>
public class DiceBoxAnimationController : MonoBehaviour
{
    #region Serialized Fields - Animation Sequences
    [Header("Animation Sequences")]
    [SerializeField] private List<Sprite> shakeSequence;
    [SerializeField] private List<Sprite> idleSequence;
    [SerializeField] private List<Sprite> zoomInSequence;
    [SerializeField] private List<Sprite> openingSequence;
    [SerializeField] private List<Sprite> closingSequence;

    [Header("UI References")]
    [SerializeField] private Image animationImage;
    [SerializeField] private GameObject diceBoxContainer;

    [Header("Timing Configuration")]
    [SerializeField] private float shakeDuration = 2.5f;
    [SerializeField] private float idleDuration = 4f;
    [SerializeField] private float zoomInDuration = 0.8f;
    [SerializeField] private float openingDuration = 2.3f;
    [SerializeField] private float holdOpenDuration = 0.5f;
    [SerializeField] private float closingDuration = 1.5f;
    [SerializeField] private float zoomOutDuration = 0.9f;

    [Header("Dice Visibility Control")]
    [SerializeField] private int diceVisibleAtOpeningFrame = 51;
    [SerializeField] private int diceHiddenAtClosingFrame = 28;
    [SerializeField] private GameObject diceContainer;
    #endregion

    #region Private Fields
    private DiceBoxState currentState = DiceBoxState.Hidden;
    private Coroutine animationCoroutine;
    private bool isAnimating = false;

    // Server sync fields
    private long roundStartTime = 0;
    private long bettingEndTime = 0;
    private long serverTimeOffset = 0; // Difference between server time and local time

    // Callbacks
    private Action onDiceShouldShow;
    private Action onDiceShouldHide;
    private Action onAnimationCycleComplete;

    // Audio tracking
    private bool hasPlayedShakeSound = false;
    private bool hasPlayedBoxOpenSound = false;
    private bool hasPlayedBoxCloseSound = false;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        ValidateSetup();

        // Initially hide everything
        if (diceBoxContainer) diceBoxContainer.SetActive(false);
        if (diceContainer) diceContainer.SetActive(false);
    }

    private void OnDestroy()
    {
        StopAllAnimations();
    }
    #endregion

    #region Public API - Animation Control with Server Sync
    /// <summary>
    /// Start animation cycle synced with server time
    /// Calculates elapsed time and jumps to correct phase if joining mid-round
    /// </summary>
    public void StartAnimationCycleWithServerSync(long roundStartTimestamp, long bettingEndTimestamp, long currentServerTime)
    {
        Debug.Log($"[DiceBoxAnim] Starting animation cycle with server sync");
        Debug.Log($"[DiceBoxAnim] Round started at: {roundStartTimestamp}");
        Debug.Log($"[DiceBoxAnim] Betting ends at: {bettingEndTimestamp}");
        Debug.Log($"[DiceBoxAnim] Current server time: {currentServerTime}");

        StopAllAnimations();

        // Reset audio flags - CRITICAL for new round
        hasPlayedShakeSound = false;
        hasPlayedBoxOpenSound = false;
        hasPlayedBoxCloseSound = false;

        // Store server timing
        roundStartTime = roundStartTimestamp;
        bettingEndTime = bettingEndTimestamp;
        serverTimeOffset = currentServerTime - (long)(Time.realtimeSinceStartup * 1000);

        // Calculate elapsed time since round start
        long elapsedMs = currentServerTime - roundStartTimestamp;
        float elapsedSeconds = elapsedMs / 1000f;

        Debug.Log($"[DiceBoxAnim] Elapsed time since round start: {elapsedSeconds:F2} seconds");

        // Show container
        if (diceBoxContainer) diceBoxContainer.SetActive(true);
        if (diceContainer) diceContainer.SetActive(false);

        // Determine which phase to start in based on elapsed time
        JumpToCorrectPhase(elapsedSeconds);
    }

    /// <summary>
    /// Legacy method - starts from beginning (for backward compatibility)
    /// </summary>
    public void StartAnimationCycle()
    {
        Debug.Log("[DiceBoxAnim] Starting new animation cycle (legacy - from beginning)");
        StopAllAnimations();

        // Reset audio flags - CRITICAL for new round
        hasPlayedShakeSound = false;
        hasPlayedBoxOpenSound = false;
        hasPlayedBoxCloseSound = false;

        // Show container and ensure dice are hidden
        if (diceBoxContainer) diceBoxContainer.SetActive(true);
        if (diceContainer) diceContainer.SetActive(false);

        // Start with shake animation
        PlayShakeAnimation();
    }

    /// <summary>
    /// Call this when betting is locked to transition from idle to zoom in
    /// </summary>
    public void OnBettingLocked()
    {
        Debug.Log("[DiceBoxAnim] Betting locked - will transition to zoom in after current animation");

        // If we're in idle state, transition to zoom in
        if (currentState == DiceBoxState.Idle)
        {
            StopAllAnimations();
            PlayZoomInAnimation();
        }
    }

    /// <summary>
    /// Call this when dice result is ready to be revealed
    /// </summary>
    public void RevealDiceResult()
    {
        Debug.Log("[DiceBoxAnim] Revealing dice result");

        // Should be called after zoom in completes, but handle gracefully
        if (currentState == DiceBoxState.ZoomingIn || currentState == DiceBoxState.ZoomedIn)
        {
            StopAllAnimations();
            PlayOpeningAnimation();
        }
        else
        {
            Debug.LogWarning($"[DiceBoxAnim] RevealDiceResult called in unexpected state: {currentState}");
        }
    }

    /// <summary>
    /// Call this to start the closing sequence
    /// </summary>
    public void CloseAndFinish()
    {
        Debug.Log("[DiceBoxAnim] Starting close sequence");

        if (currentState == DiceBoxState.Open)
        {
            StopAllAnimations();
            PlayClosingAnimation();
        }
    }

    /// <summary>
    /// Force hide everything immediately
    /// </summary>
    public void ForceHide()
    {
        Debug.Log("[DiceBoxAnim] Force hiding");
        StopAllAnimations();

        if (diceBoxContainer) diceBoxContainer.SetActive(false);
        if (diceContainer) diceContainer.SetActive(false);

        currentState = DiceBoxState.Hidden;
    }

    /// <summary>
    /// Set callback for when dice should become visible during opening animation
    /// </summary>
    public void SetDiceShowCallback(Action callback)
    {
        onDiceShouldShow = callback;
    }

    /// <summary>
    /// Set callback for when dice should be hidden during closing animation
    /// </summary>
    public void SetDiceHideCallback(Action callback)
    {
        onDiceShouldHide = callback;
    }

    /// <summary>
    /// Set callback for when full animation cycle completes
    /// </summary>
    public void SetAnimationCycleCompleteCallback(Action callback)
    {
        onAnimationCycleComplete = callback;
    }
    #endregion

    #region Private Methods - Phase Jump Logic
    /// <summary>
    /// Jump to the correct animation phase based on elapsed time
    /// </summary>
    private void JumpToCorrectPhase(float elapsedSeconds)
    {
        // Calculate phase boundaries
        float shakeEnd = shakeDuration;
        float idleEnd = shakeEnd + idleDuration; // This is when betting should end
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
            // Start shake but skip ahead
            float timeIntoShake = elapsedSeconds;
            Debug.Log($"[DiceBoxAnim] Joining during SHAKE phase ({timeIntoShake:F2}s into shake)");
            
            // Mark shake sound as played since we're joining mid-shake
            hasPlayedShakeSound = true;
            PlayShakeAnimation(timeIntoShake);
        }
        else if (elapsedSeconds < idleEnd)
        {
            // Start idle but skip ahead
            float timeIntoIdle = elapsedSeconds - shakeEnd;
            Debug.Log($"[DiceBoxAnim] Joining during IDLE phase ({timeIntoIdle:F2}s into idle)");
            
            // Mark shake sound as played
            hasPlayedShakeSound = true;
            PlayIdleAnimation(timeIntoIdle);
        }
        else if (elapsedSeconds < zoomInEnd)
        {
            // Start zoom in but skip ahead
            float timeIntoZoomIn = elapsedSeconds - idleEnd;
            Debug.Log($"[DiceBoxAnim] Joining during ZOOM IN phase ({timeIntoZoomIn:F2}s into zoom in)");
            
            hasPlayedShakeSound = true;
            PlayZoomInAnimation(timeIntoZoomIn);
        }
        else if (elapsedSeconds < openingEnd)
        {
            // Start opening but skip ahead
            float timeIntoOpening = elapsedSeconds - zoomInEnd;
            Debug.Log($"[DiceBoxAnim] Joining during OPENING phase ({timeIntoOpening:F2}s into opening)");
            
            hasPlayedShakeSound = true;
            // Mark box open as played since we're joining mid-open
            hasPlayedBoxOpenSound = true;
            PlayOpeningAnimation(timeIntoOpening);
        }
        else if (elapsedSeconds < holdOpenEnd)
        {
            // Jump to hold open state
            Debug.Log($"[DiceBoxAnim] Joining during HOLD OPEN phase");
            
            hasPlayedShakeSound = true;
            hasPlayedBoxOpenSound = true;
            
            // Set to final frame of opening sequence
            if (openingSequence != null && openingSequence.Count > 0)
            {
                SetDisplayToFrame(openingSequence, openingSequence.Count - 1);
            }
            currentState = DiceBoxState.Open;
            
            // Start hold timer
            float remainingHoldTime = holdOpenEnd - elapsedSeconds;
            animationCoroutine = StartCoroutine(HoldOpenThenClose(remainingHoldTime));
        }
        else if (elapsedSeconds < closingEnd)
        {
            // Start closing but skip ahead
            float timeIntoClosing = elapsedSeconds - holdOpenEnd;
            Debug.Log($"[DiceBoxAnim] Joining during CLOSING phase ({timeIntoClosing:F2}s into closing)");
            
            hasPlayedShakeSound = true;
            hasPlayedBoxOpenSound = true;
            // Mark box close as played since we're joining mid-close
            hasPlayedBoxCloseSound = true;
            PlayClosingAnimation(timeIntoClosing);
        }
        else if (elapsedSeconds < zoomOutEnd)
        {
            // Start zoom out but skip ahead
            float timeIntoZoomOut = elapsedSeconds - closingEnd;
            Debug.Log($"[DiceBoxAnim] Joining during ZOOM OUT phase ({timeIntoZoomOut:F2}s into zoom out)");
            
            hasPlayedShakeSound = true;
            hasPlayedBoxOpenSound = true;
            hasPlayedBoxCloseSound = true;
            PlayZoomOutAnimation(timeIntoZoomOut);
        }
        else
        {
            // Round should be over, go to waiting state
            Debug.Log($"[DiceBoxAnim] Joining after cycle complete - waiting state");
            currentState = DiceBoxState.Waiting;
            if (diceBoxContainer) diceBoxContainer.SetActive(false);
            onAnimationCycleComplete?.Invoke();
        }
    }
    #endregion

    #region Private Methods - Individual Animation Phases
    private void PlayShakeAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.Shaking;
        Debug.Log("[DiceBoxAnim] Playing shake animation");

       
        if ( AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayShake();
            Debug.Log("[DiceBoxAnim] Shake sound played");
        }
        hasPlayedShakeSound = true;

        animationCoroutine = StartCoroutine(PlaySequenceCoroutine(
            shakeSequence,
            shakeDuration,
            reverse: false,
            loop: false,
            startTime,
            OnShakeComplete
        ));
    }

    private void OnShakeComplete()
    {
        Debug.Log("[DiceBoxAnim] Shake complete");
        PlayIdleAnimation();
    }

    private void PlayIdleAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.Idle;
        Debug.Log("[DiceBoxAnim] Playing idle animation (will loop until betting ends)");

        animationCoroutine = StartCoroutine(PlaySequenceCoroutine(
            idleSequence,
            idleDuration,
            reverse: false,
            loop: true,
            startTime,
            null
        ));
    }

    private void PlayZoomInAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.ZoomingIn;
        Debug.Log("[DiceBoxAnim] Playing zoom in animation");

        animationCoroutine = StartCoroutine(PlaySequenceCoroutine(
            zoomInSequence,
            zoomInDuration,
            reverse: false,
            loop: false,
            startTime,
            OnZoomInComplete
        ));
    }

    private void OnZoomInComplete()
    {
        Debug.Log("[DiceBoxAnim] Zoom in complete - waiting for dice result");
        currentState = DiceBoxState.ZoomedIn;
    }

    private void PlayOpeningAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.Opening;
        Debug.Log("[DiceBoxAnim] Playing opening animation");

        // AUDIO: Play box open sound only once at the start
        if (!hasPlayedBoxOpenSound && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBoxOpen();
            hasPlayedBoxOpenSound = true;
        }

        animationCoroutine = StartCoroutine(PlaySequenceCoroutine(
            openingSequence,
            openingDuration,
            reverse: false,
            loop: false,
            startTime,
            OnOpeningComplete
        ));
    }

    private void OnOpeningComplete()
    {
        Debug.Log("[DiceBoxAnim] Opening complete - holding open");
        currentState = DiceBoxState.Open;

        animationCoroutine = StartCoroutine(HoldOpenThenClose(holdOpenDuration));
    }

    private IEnumerator HoldOpenThenClose(float holdDuration)
    {
        Debug.Log($"[DiceBoxAnim] Holding open for {holdDuration}s");
        yield return new WaitForSeconds(holdDuration);

        PlayClosingAnimation();
    }

    private void PlayClosingAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.Closing;
        Debug.Log("[DiceBoxAnim] Playing closing animation");

        // AUDIO: Play box close sound only once at the start
        if (!hasPlayedBoxCloseSound && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBoxClose();
            hasPlayedBoxCloseSound = true;
        }

        animationCoroutine = StartCoroutine(PlaySequenceCoroutine(
            closingSequence,
            closingDuration,
            reverse: false,
            loop: false,
            startTime,
            OnClosingComplete
        ));
    }

    private void OnClosingComplete()
    {
        Debug.Log("[DiceBoxAnim] Closing complete");
        PlayZoomOutAnimation();
    }

    private void PlayZoomOutAnimation(float startTime = 0f)
    {
        currentState = DiceBoxState.ZoomingOut;
        Debug.Log("[DiceBoxAnim] Playing zoom out animation");

        animationCoroutine = StartCoroutine(PlaySequenceCoroutine(
            zoomInSequence,
            zoomOutDuration,
            reverse: true,
            loop: false,
            startTime,
            OnZoomOutComplete
        ));
    }

    private void OnZoomOutComplete()
    {
        Debug.Log("[DiceBoxAnim] Zoom out complete - cycle finished");
        currentState = DiceBoxState.Waiting;

        // Notify that full cycle is complete
        onAnimationCycleComplete?.Invoke();
    }
    #endregion

    #region Private Methods - Core Animation Playback
    /// <summary>
    /// Play animation sequence with optional time skip
    /// </summary>
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
        float frameDelay = duration / sequence.Count;

        // Calculate starting frame based on startTime
        int startFrame = Mathf.FloorToInt(startTime / frameDelay);
        float timeIntoStartFrame = startTime - (startFrame * frameDelay);

        // Wait for remaining time in start frame
        if (timeIntoStartFrame > 0 && startFrame < sequence.Count)
        {
            float remainingFrameTime = frameDelay - timeIntoStartFrame;

            // Display the start frame
            if (reverse)
            {
                int reverseIndex = sequence.Count - 1 - startFrame;
                if (reverseIndex >= 0 && reverseIndex < sequence.Count)
                {
                    SetDisplayToFrame(sequence, reverseIndex);
                }
            }
            else
            {
                SetDisplayToFrame(sequence, startFrame);
                // Check dice visibility for this frame
                CheckDiceVisibilityTriggers(startFrame);
            }

            yield return new WaitForSeconds(remainingFrameTime);
            startFrame++;
        }

        do
        {
            if (reverse)
            {
                // Play backwards
                for (int i = sequence.Count - 1 - startFrame; i >= 0; i--)
                {
                    if (animationImage && sequence[i])
                    {
                        animationImage.sprite = sequence[i];
                    }
                    yield return new WaitForSeconds(frameDelay);
                }
            }
            else
            {
                // Play forwards
                for (int i = startFrame; i < sequence.Count; i++)
                {
                    if (animationImage && sequence[i])
                    {
                        animationImage.sprite = sequence[i];
                    }

                    // Check for dice visibility triggers at specific frames
                    CheckDiceVisibilityTriggers(i);

                    yield return new WaitForSeconds(frameDelay);
                }
            }

            startFrame = 0; // Reset for loop iterations
        } while (loop && isAnimating);

        isAnimating = false;
        animationCoroutine = null;

        // Call completion callback if not looping
        if (!loop)
        {
            onComplete?.Invoke();
        }
    }

    private void CheckDiceVisibilityTriggers(int currentFrame)
    {
        // Show dice at specific frame during opening
        if (currentState == DiceBoxState.Opening && currentFrame == diceVisibleAtOpeningFrame)
        {
            Debug.Log($"[DiceBoxAnim] Frame {currentFrame}/{openingSequence.Count}: SHOWING dice");
            ShowDice();
        }

        // Hide dice at specific frame during closing
        if (currentState == DiceBoxState.Closing && currentFrame == diceHiddenAtClosingFrame)
        {
            Debug.Log($"[DiceBoxAnim] Frame {currentFrame}/{closingSequence.Count}: HIDING dice");
            HideDice();
        }
    }

    private void ShowDice()
    {
        if (diceContainer)
        {
            diceContainer.SetActive(true);
        }
        onDiceShouldShow?.Invoke();
    }

    private void HideDice()
    {
        if (diceContainer)
        {
            diceContainer.SetActive(false);
        }
        onDiceShouldHide?.Invoke();
    }

    private void SetDisplayToFrame(List<Sprite> sequence, int frameIndex)
    {
        if (animationImage && sequence != null && frameIndex >= 0 && frameIndex < sequence.Count)
        {
            animationImage.sprite = sequence[frameIndex];
        }
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
        bool hasErrors = false;

        if (animationImage == null)
        {
            Debug.LogError("[DiceBoxAnim] Animation Image is not assigned!");
            hasErrors = true;
        }

        if (diceContainer == null)
        {
            Debug.LogWarning("[DiceBoxAnim] Dice Container is not assigned! Dice visibility control will not work.");
        }

        if (shakeSequence == null || shakeSequence.Count == 0)
        {
            Debug.LogWarning("[DiceBoxAnim] Shake sequence is empty!");
        }

        if (idleSequence == null || idleSequence.Count == 0)
        {
            Debug.LogWarning("[DiceBoxAnim] Idle sequence is empty!");
        }

        if (zoomInSequence == null || zoomInSequence.Count == 0)
        {
            Debug.LogWarning("[DiceBoxAnim] Zoom in sequence is empty!");
        }

        if (openingSequence == null || openingSequence.Count == 0)
        {
            Debug.LogWarning("[DiceBoxAnim] Opening sequence is empty!");
        }
        else if (diceVisibleAtOpeningFrame >= openingSequence.Count)
        {
            Debug.LogWarning($"[DiceBoxAnim] Dice visible frame ({diceVisibleAtOpeningFrame}) is beyond opening sequence length ({openingSequence.Count})!");
        }

        if (closingSequence == null || closingSequence.Count == 0)
        {
            Debug.LogWarning("[DiceBoxAnim] Closing sequence is empty!");
        }
        else if (diceHiddenAtClosingFrame >= closingSequence.Count)
        {
            Debug.LogWarning($"[DiceBoxAnim] Dice hidden frame ({diceHiddenAtClosingFrame}) is beyond closing sequence length ({closingSequence.Count})!");
        }

        if (!hasErrors)
        {
            Debug.Log("[DiceBoxAnim] Setup validated successfully");
        }
    }
    #endregion

    #region Public Getters
    public DiceBoxState GetCurrentState() => currentState;
    public bool IsAnimating() => isAnimating;
    public float GetTotalCycleTime() => shakeDuration + zoomInDuration + openingDuration + holdOpenDuration + closingDuration + zoomOutDuration;
    #endregion
}

/// <summary>
/// Animation states for the dice box
/// </summary>
public enum DiceBoxState
{
    Hidden,      // Not visible
    Shaking,     // Girl shaking the dice box
    Idle,        // Idle loop while betting is active
    ZoomingIn,   // Zooming into the box
    ZoomedIn,    // Zoomed in, waiting for result
    Opening,     // Box opening to reveal dice
    Open,        // Box is open, dice visible, holding
    Closing,     // Box closing after result
    ZoomingOut,  // Zooming out from the box
    Waiting      // Waiting for next round (cycle complete)
}