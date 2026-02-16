using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Manages round flow and dice display with SERVER-SYNCED dice box animation - AUDIO INTEGRATED
/// Updated to handle mid-round joins properly
/// </summary>
public class RoundController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Dice Display")]
    [SerializeField] private Image Dice1_Image;
    [SerializeField] private Image Dice2_Image;
    [SerializeField] private Image Dice3_Image;
    [SerializeField] private GameObject DiceContainer;

    [Header("Dice Sprites")]
    [SerializeField] private Sprite[] DiceSprites;

    [Header("Result Display")]
    [SerializeField] private TMPro.TMP_Text Sum_Text;
    [SerializeField] private TMPro.TMP_Text MatchSide_Text;
    [SerializeField] private GameObject ResultPanel;

    [Header("References")]
    [SerializeField] private UIController uiController;
    [SerializeField] private BetController betController;
    [SerializeField] private GameManager gameManager;

    [Header("Dice Box Animation")]
    [SerializeField] private DiceBoxAnimationController diceBoxAnimController;

    [Header("Audio Settings")]
    [SerializeField] private float diceResultSoundDelay = 0.5f;
    #endregion

    #region Private Fields
    private string currentRoundId;
    private bool isRoundActive = false;
    private Coroutine finalCountdownCoroutine;
    private DiceResultData currentDiceResult;
    private bool diceResultReceived = false;

    // Server time tracking
    private long currentRoundStartTime = 0;
    private long currentBettingEndTime = 0;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Register callbacks with animation controller
        if (diceBoxAnimController != null)
        {
            diceBoxAnimController.SetDiceShowCallback(OnAnimationShowDice);
            diceBoxAnimController.SetDiceHideCallback(OnAnimationHideDice);
            diceBoxAnimController.SetAnimationCycleCompleteCallback(OnAnimationCycleComplete);
        }
        else
        {
            Debug.LogError("[RoundController] DiceBoxAnimationController is not assigned!");
        }

        // Initially hide everything
        if (DiceContainer) DiceContainer.SetActive(false);
        if (ResultPanel) ResultPanel.SetActive(false);
    }
    #endregion

    #region Public API - Round Management
    /// <summary>
    /// Called when a new round starts - NOW WITH SERVER SYNC
    /// </summary>
    internal void StartRound(RoundStartData data)
    {
        if (data == null) return;

        Debug.Log($"[RoundController] Starting round {data.roundId}");
        Debug.Log($"[RoundController] Round start time: {data.startedAt}");
        Debug.Log($"[RoundController] Betting end time: {data.bettingEndTime}");
        Debug.Log($"[RoundController] Server time: {data.serverTime}");

        currentRoundId = data.roundId;
        isRoundActive = true;
        diceResultReceived = false;
        currentDiceResult = null;

        // Store timing info
        currentRoundStartTime = data.startedAt;
        currentBettingEndTime = data.bettingEndTime;

        // Stop any existing countdown
        if (finalCountdownCoroutine != null)
        {
            StopCoroutine(finalCountdownCoroutine);
            finalCountdownCoroutine = null;
        }

        // Clear previous round display
        ClearRoundDisplay();

        // Clear win highlights from previous round
        betController?.ClearAllWinHighlights();

        // AUDIO: Play round start sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayRoundStart();
        }

        // Start the dice box animation cycle WITH SERVER SYNC
        if (diceBoxAnimController != null)
        {
            diceBoxAnimController.StartAnimationCycleWithServerSync(
                data.startedAt,
                data.bettingEndTime,
                data.serverTime
            );
        }

        // Update UI
        uiController.UpdateRoundPhase("BETTING");

        int timeRemaining = GameUtilities.CalculateTimeRemaining(data.bettingEndTime, data.serverTime);
        uiController.UpdateTimer(timeRemaining);
    }

    /// <summary>
    /// Called every second with updated timer
    /// </summary>
    internal void UpdateTimer(int secondsRemaining)
    {
        if (!isRoundActive) return;

        uiController.UpdateTimer(secondsRemaining);

        // When we hit 1 second, start the final countdown
        if (secondsRemaining == 1 && finalCountdownCoroutine == null)
        {
            finalCountdownCoroutine = StartCoroutine(FinalCountdownToZero());
        }
    }

    /// <summary>
    /// Called when dice result is received from server
    /// </summary>
    internal void ShowDiceResult(DiceResultData data)
    {
        if (data == null) return;

        Debug.Log($"[RoundController] Dice result received: {data.dice1}, {data.dice2}, {data.dice3} = {data.sum} ({data.matchSide})");

        // Store the result
        currentDiceResult = data;
        diceResultReceived = true;

        // Disable betting
        betController.DisableBetting();
        uiController.UpdateRoundPhase("RESULT");

        // The animation controller should be in ZoomedIn state by now
        // Trigger the reveal
        if (diceBoxAnimController != null)
        {
            diceBoxAnimController.RevealDiceResult();
        }
        else
        {
            // Fallback if no animation controller
            Debug.LogWarning("[RoundController] No animation controller - showing dice immediately");
            SetDiceValues(data);
            ShowResult(data.sum, data.matchSide);
            PlayDiceResultSounds(data);
        }
    }

    /// <summary>
    /// Clear all round displays
    /// </summary>
    internal void ClearRoundDisplay()
    {
        if (DiceContainer) DiceContainer.SetActive(false);
        if (ResultPanel) ResultPanel.SetActive(false);
        currentDiceResult = null;
        diceResultReceived = false;
    }
    #endregion

    #region Private Methods - Countdown
    private IEnumerator FinalCountdownToZero()
    {
        // Wait for the remaining second
        yield return new WaitForSeconds(1f);

        // Update UI to show 0
        uiController.UpdateTimer(0);

        // Disable betting
        betController.DisableBetting();
        uiController.ShowBetLocked();

        // Notify animation controller that betting is locked
        if (diceBoxAnimController != null)
        {
            diceBoxAnimController.OnBettingLocked();
        }

        finalCountdownCoroutine = null;
    }
    #endregion

    #region Private Methods - Animation Callbacks
    /// <summary>
    /// Called by animation controller when dice should be shown (during opening animation)
    /// </summary>
    private void OnAnimationShowDice()
    {
        Debug.Log("[RoundController] Animation triggered: Show dice");

        if (currentDiceResult != null)
        {
            // Set the dice values
            SetDiceValues(currentDiceResult);

            // Make sure container is active
            if (DiceContainer) DiceContainer.SetActive(true);

            // Show result text
            ShowResult(currentDiceResult.sum, currentDiceResult.matchSide);

            // AUDIO: Play dice show sound
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayDiceShow();
            }

            // AUDIO: Play dice result sounds in sequence
            PlayDiceResultSounds(currentDiceResult);

            // Optional: Add pop animation when dice appear
            if (DiceContainer)
            {
                DiceContainer.transform.localScale = Vector3.zero;
                DiceContainer.transform.DOScale(1.2f, 0.3f)
                    .SetEase(Ease.OutBack)
                    .OnComplete(() => DiceContainer.transform.DOScale(1f, 0.2f));
            }
        }
        else
        {
            Debug.LogWarning("[RoundController] Dice show triggered but no result data available!");
        }
    }

    /// <summary>
    /// Called by animation controller when dice should be hidden (during closing animation)
    /// </summary>
    private void OnAnimationHideDice()
    {
        Debug.Log("[RoundController] Animation triggered: Hide dice");

        if (DiceContainer) DiceContainer.SetActive(false);
        if (ResultPanel) ResultPanel.SetActive(false);
    }

    /// <summary>
    /// Called when full animation cycle completes
    /// </summary>
    private void OnAnimationCycleComplete()
    {
        Debug.Log("[RoundController] Animation cycle complete - ready for next round");
        isRoundActive = false;
    }
    #endregion

    #region Private Methods - Dice Display
    /// <summary>
    /// Set the dice face sprites to show the result
    /// </summary>
    private void SetDiceValues(DiceResultData data)
    {
        if (DiceSprites == null || DiceSprites.Length < 6)
        {
            Debug.LogError("[RoundController] Dice sprites not properly configured!");
            return;
        }

        SetDiceFace(Dice1_Image, data.dice1 - 1);
        SetDiceFace(Dice2_Image, data.dice2 - 1);
        SetDiceFace(Dice3_Image, data.dice3 - 1);

        Debug.Log($"[RoundController] Dice values set: {data.dice1}, {data.dice2}, {data.dice3}");
    }

    private void SetDiceFace(Image diceImage, int faceIndex)
    {
        if (diceImage == null || DiceSprites == null) return;
        if (faceIndex < 0 || faceIndex >= DiceSprites.Length)
        {
            Debug.LogError($"[RoundController] Invalid dice face index: {faceIndex}");
            return;
        }

        diceImage.sprite = DiceSprites[faceIndex];
    }

    private void ShowResult(int sum, string matchSide)
    {
        if (Sum_Text) Sum_Text.text = sum.ToString();
        if (MatchSide_Text) MatchSide_Text.text = matchSide.ToUpper();
        if (ResultPanel) ResultPanel.SetActive(true);

        Debug.Log($"[RoundController] Result displayed: Sum={sum}, Side={matchSide}");
    }
    #endregion

    #region Private Methods - Audio
    private void PlayDiceResultSounds(DiceResultData data)
    {
        if (AudioManager.Instance != null)
        {
            // Play dice sounds in sequence with delay
            AudioManager.Instance.PlayDiceResultSequence(
                data.dice1,
                data.dice2,
                data.dice3,
                diceResultSoundDelay
            );
        }
    }
    #endregion

    #region Public Getters
    public string GetCurrentRoundId() => currentRoundId;
    public bool IsRoundActive() => isRoundActive;
    #endregion
}