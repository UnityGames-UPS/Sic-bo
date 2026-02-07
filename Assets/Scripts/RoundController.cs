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
    #endregion

    #region Public API
    /// <summary>
    /// FIXED: Start new round and clear previous round's results and highlights
    /// </summary>
    internal void StartRound(RoundStartData data)
    {
        if (data == null) return;

        gameManager?.LogInfo($"[ROUND] Starting: {data.roundId}");

        currentRoundId = data.roundId;
        isRoundActive = true;

        // Clear previous round results and highlights NOW
        HideDiceImmediate();
        HideResultImmediate();
        betController?.ClearAllWinHighlights(); // Clear win highlights from previous round

        // Update UI to betting phase
        uiController.UpdateRoundPhase("BETTING");

        // Calculate initial time remaining from server data
        int timeRemaining = CalculateTimeRemaining(data.bettingEndTime, data.serverTime);

        gameManager?.LogInfo($"[ROUND] Betting time: {timeRemaining}s (End: {data.bettingEndTime}, Now: {data.serverTime})");

        // Update timer display immediately
        uiController.UpdateTimer(timeRemaining);
    }

    /// <summary>
    /// FIXED: Update timer - skip showing 0 to prevent delay before "Bet Locked"
    /// Server sends updates every second via game:betting_timer
    /// </summary>
    internal void UpdateTimer(int secondsRemaining)
    {
        if (!isRoundActive) return;

        // FIXED: Don't show 0 - it causes a 1-2 second pause before dice result
        // When timer reaches 0, betting is already over, so skip the update
        if (secondsRemaining <= 0)
        {
            gameManager?.LogInfo("[ROUND] Betting time expired (0s) - skipping display");
            // Don't call UpdateTimer(0) - it just delays the "Bet Locked" message
            // The dice result will trigger ShowBetLocked directly
            return;
        }

        // Update the display - timer sync comes from server
        uiController.UpdateTimer(secondsRemaining);

        // Log last 5 seconds
        if (secondsRemaining <= 5)
        {
            gameManager?.LogBroadcast("TIMER", $" {secondsRemaining}s remaining");
        }
    }

    internal void ShowDiceResult(DiceResultData data)
    {
        if (data == null) return;

        gameManager?.LogInfo($"[ROUND] Result: [{data.dice1}, {data.dice2}, {data.dice3}] = {data.sum} ({data.matchSide})");

        // Disable betting if not already disabled
        betController.DisableBetting();
        uiController.UpdateRoundPhase("RESULT");

        // Start dice animation immediately
        StartCoroutine(AnimateDiceRoll(data));
    }

    /// <summary>
    /// FIXED: Round end no longer clears results - they stay until next round starts
    /// </summary>
    internal void EndRound()
    {
        gameManager?.LogInfo("[ROUND] Round ended - results will stay visible until next round");
        isRoundActive = false;

        // DON'T hide results - they stay visible during next round countdown
        // They will be hidden when StartRound() is called for the next round
    }

    /// <summary>
    /// Clear all round displays (dice and results)
    /// Called when leaving room
    /// </summary>
    internal void ClearRoundDisplay()
    {
        HideDiceImmediate();
        HideResultImmediate();
        gameManager?.LogInfo("[ROUND] Display cleared");
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

        gameManager?.LogInfo($"[ROUND] Starting dice animation ({rollDuration}s)");

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

        gameManager?.LogSuccess($"[ROUND] Animation complete - showing result");

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

        gameManager?.LogInfo("[ROUND] Result panel displayed - will stay visible until next round");
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