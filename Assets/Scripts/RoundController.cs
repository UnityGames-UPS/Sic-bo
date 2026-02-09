using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class RoundController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Dice Objects")]
    [SerializeField] private Image Dice1_Image;
    [SerializeField] private Image Dice2_Image;
    [SerializeField] private Image Dice3_Image;
    [SerializeField] private GameObject DiceContainer;

    [Header("Dice Sprites")]
    [SerializeField] private Sprite[] DiceSprites; // 0-5 for dice faces 1-6

    [Header("Result Display")]
    [SerializeField] private TMPro.TMP_Text Sum_Text;
    [SerializeField] private TMPro.TMP_Text MatchSide_Text;
    [SerializeField] private GameObject ResultPanel;

    [Header("Animation Settings")]
    [SerializeField] private float rollDuration = 2f;
    [SerializeField] private int rollIterations = 10;

    [Header("References")]
    [SerializeField] private UIController uiController;
    [SerializeField] private BetController betController;
    [SerializeField] private GameManager gameManager;
    #endregion

    #region Private Fields
    private string currentRoundId;
    private bool isRoundActive = false;
    private Coroutine finalCountdownCoroutine;
    #endregion

    #region Public API
    /// <summary>
    /// Start new round and clear previous round's results and highlights
    /// </summary>
    internal void StartRound(RoundStartData data)
    {
        if (data == null) return;

        currentRoundId = data.roundId;
        isRoundActive = true;

        // Stop any existing countdown
        if (finalCountdownCoroutine != null)
        {
            StopCoroutine(finalCountdownCoroutine);
            finalCountdownCoroutine = null;
        }

        // Clear previous round results and highlights NOW
        HideDiceImmediate();
        HideResultImmediate();
        betController?.ClearAllWinHighlights();

        // Update UI to betting phase
        uiController.UpdateRoundPhase("BETTING");

        // Calculate initial time remaining from server data
        int timeRemaining = CalculateTimeRemaining(data.bettingEndTime, data.serverTime);


        // Update timer display immediately
        uiController.UpdateTimer(timeRemaining);
    }

    /// <summary>
    /// FIXED: Update timer - when server sends 1, start client countdown to 0 then lock
    /// Shows: 3-2-1-0-BET LOCKED (no delay)
    /// </summary>
    internal void UpdateTimer(int secondsRemaining)
    {
        if (!isRoundActive) return;

        // Update the display
        uiController.UpdateTimer(secondsRemaining);


        // When server sends 1, start client-side countdown to 0 then lock
        // Only start if not already running
        if (secondsRemaining == 1 && finalCountdownCoroutine == null)
        {
            finalCountdownCoroutine = StartCoroutine(FinalCountdownToZero());
        }
    }

    /// <summary>
    /// Client-side countdown from 1 to 0, then immediately lock betting
    /// </summary>
    private IEnumerator FinalCountdownToZero()
    {
        // Wait 1 second
        yield return new WaitForSeconds(1f);

        // Show 0 on timer
        uiController.UpdateTimer(0);
    

        // Immediately lock betting and show bet locked
        betController.DisableBetting();
        uiController.ShowBetLocked();

    

        finalCountdownCoroutine = null;
    }

    /// <summary>
    /// Show dice result - betting is already locked at timer 0
    /// </summary>
    internal void ShowDiceResult(DiceResultData data)
    {
        if (data == null) return;

        

        // Ensure betting is disabled (should already be from timer 0)
        betController.DisableBetting();
        uiController.UpdateRoundPhase("RESULT");

        // Start dice animation
        StartCoroutine(AnimateDiceRoll(data));
    }



    /// <summary>
    /// Clear all round displays (dice and results)
    /// Called when leaving room
    /// </summary>
    internal void ClearRoundDisplay()
    {
        HideDiceImmediate();
        HideResultImmediate();
     
    }
    #endregion

    #region Private Methods
    private int CalculateTimeRemaining(long bettingEndTime, long serverTime)
    {
        long remainingMs = bettingEndTime - serverTime;
        int remainingSeconds = Mathf.Max(0, (int)(remainingMs / 1000));
        return remainingSeconds;
    }

    private IEnumerator AnimateDiceRoll(DiceResultData data)
    {
        // Show dice container immediately
        if (DiceContainer) DiceContainer.SetActive(true);

    

        // Animate rolling
        float elapsed = 0f;
        float intervalTime = rollDuration / rollIterations;

        while (elapsed < rollDuration)
        {
            // Random dice faces during roll
            SetDiceFace(Dice1_Image, Random.Range(0, 6));
            SetDiceFace(Dice2_Image, Random.Range(0, 6));
            SetDiceFace(Dice3_Image, Random.Range(0, 6));

            yield return new WaitForSeconds(intervalTime);
            elapsed += intervalTime;
        }

        // Show final result
        SetDiceFace(Dice1_Image, data.dice1 - 1);
        SetDiceFace(Dice2_Image, data.dice2 - 1);
        SetDiceFace(Dice3_Image, data.dice3 - 1);

  

        // Bounce animation
        if (DiceContainer)
        {
            DiceContainer.transform.DOScale(1.2f, 0.2f)
                .OnComplete(() => DiceContainer.transform.DOScale(1f, 0.2f));
        }

        // Show result text - STAYS VISIBLE until next round starts
        ShowResult(data.sum, data.matchSide);
    }

    private void SetDiceFace(Image diceImage, int faceIndex)
    {
        if (diceImage == null || DiceSprites == null) return;
        if (faceIndex < 0 || faceIndex >= DiceSprites.Length) return;

        diceImage.sprite = DiceSprites[faceIndex];
    }

    private void ShowResult(int sum, string matchSide)
    {
        if (Sum_Text) Sum_Text.text = sum.ToString();
        if (MatchSide_Text) MatchSide_Text.text = matchSide.ToUpper();
        if (ResultPanel) ResultPanel.SetActive(true);


    }

    private void HideResultImmediate()
    {
        if (ResultPanel) ResultPanel.SetActive(false);
    }

    private void HideDiceImmediate()
    {
        if (DiceContainer) DiceContainer.SetActive(false);
    }
    #endregion
}