using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Manages round phases, timer, and dice animations
/// </summary>
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
    #endregion

    #region Private Fields
    private string currentRoundId;
    private Coroutine timerRoutine;
    private bool isRoundActive = false;
    #endregion

    #region Public API
    internal void StartRound(RoundStartData data)
    {
        if (data == null) return;

        Debug.Log($"[ROUND] Starting: {data.roundId}");

        currentRoundId = data.roundId;
        isRoundActive = true;

        // Clear previous round
        HideDice();
        HideResult();

        // Update UI
        uiController.UpdateRoundPhase("BETTING");

        // Calculate time remaining from timestamps
        // Fixed: Calculate timeRemaining from bettingEndTime and serverTime
        int timeRemaining = CalculateTimeRemaining(data.bettingEndTime, data.serverTime);

        // Start timer
        if (timerRoutine != null) StopCoroutine(timerRoutine);
        timerRoutine = StartCoroutine(TimerRoutine(timeRemaining));
    }

    internal void UpdateTimer(int secondsRemaining)
    {
        uiController.UpdateTimer(secondsRemaining);
    }

    internal void ShowDiceResult(DiceResultData data)
    {
        if (data == null) return;

        Debug.Log($"[ROUND] Showing dice result");

        // Stop betting
        betController.DisableBetting();
        uiController.UpdateRoundPhase("RESULT");

        // Animate dice roll
        StartCoroutine(AnimateDiceRoll(data));
    }

    internal void EndRound()
    {
        Debug.Log("[ROUND] Ending");

        isRoundActive = false;

        if (timerRoutine != null)
        {
            StopCoroutine(timerRoutine);
            timerRoutine = null;
        }

        // Hide dice after delay
        StartCoroutine(HideDiceAfterDelay(3f));
    }
    #endregion

    #region Private Methods
    private int CalculateTimeRemaining(long bettingEndTime, long serverTime)
    {
        // Both times are in milliseconds (Unix timestamp)
        long remainingMs = bettingEndTime - serverTime;

        // Convert to seconds and ensure it's not negative
        int remainingSeconds = Mathf.Max(0, (int)(remainingMs / 1000));

        Debug.Log($"[ROUND] Time remaining calculated: {remainingSeconds}s (End: {bettingEndTime}, Server: {serverTime})");

        return remainingSeconds;
    }

    private IEnumerator TimerRoutine(int initialTime)
    {
        int remaining = initialTime;

        while (remaining > 0 && isRoundActive)
        {
            uiController.UpdateTimer(remaining);
            yield return new WaitForSeconds(1f);
            remaining--;
        }

        if (isRoundActive)
        {
            betController.DisableBetting();
            uiController.UpdateRoundPhase("ROLLING");
        }

        timerRoutine = null;
    }

    private IEnumerator AnimateDiceRoll(DiceResultData data)
    {
        // Show dice container
        if (DiceContainer) DiceContainer.SetActive(true);

        // Animate rolling
        float elapsed = 0f;
        float intervalTime = rollDuration / rollIterations;

        while (elapsed < rollDuration)
        {
            // Random dice faces
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

        // Show result text
        ShowResult(data.sum, data.matchSide);

        // Highlight winning areas (including triple dice)
        betController.HighlightTripleDiceResult(data.dice1, data.dice2, data.dice3);
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

    private void HideResult()
    {
        if (ResultPanel) ResultPanel.SetActive(false);
    }

    private void HideDice()
    {
        if (DiceContainer) DiceContainer.SetActive(false);
    }

    private IEnumerator HideDiceAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideDice();
        HideResult();
    }
    #endregion
}