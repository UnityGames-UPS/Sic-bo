using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Manages round flow and dice display
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

    #region Public API - Round Management
    internal void StartRound(RoundStartData data)
    {
        if (data == null) return;

        currentRoundId = data.roundId;
        isRoundActive = true;

        if (finalCountdownCoroutine != null)
        {
            StopCoroutine(finalCountdownCoroutine);
            finalCountdownCoroutine = null;
        }

        HideDiceImmediate();
        HideResultImmediate();
        betController?.ClearAllWinHighlights();

        uiController.UpdateRoundPhase("BETTING");

        int timeRemaining = GameUtilities.CalculateTimeRemaining(data.bettingEndTime, data.serverTime);

        uiController.UpdateTimer(timeRemaining);
    }

    internal void UpdateTimer(int secondsRemaining)
    {
        if (!isRoundActive) return;

        uiController.UpdateTimer(secondsRemaining);

        if (secondsRemaining == 1 && finalCountdownCoroutine == null)
        {
            finalCountdownCoroutine = StartCoroutine(FinalCountdownToZero());
        }
    }

    internal void ShowDiceResult(DiceResultData data)
    {
        if (data == null) return;

        betController.DisableBetting();
        uiController.UpdateRoundPhase("RESULT");

        StartCoroutine(AnimateDiceRoll(data));
    }

    internal void ClearRoundDisplay()
    {
        HideDiceImmediate();
        HideResultImmediate();
    }
    #endregion

    #region Private Methods - Countdown
    private IEnumerator FinalCountdownToZero()
    {
        yield return new WaitForSeconds(1f);

        uiController.UpdateTimer(0);

        betController.DisableBetting();
        uiController.ShowBetLocked();

        finalCountdownCoroutine = null;
    }
    #endregion

    #region Private Methods - Dice Animation
    private IEnumerator AnimateDiceRoll(DiceResultData data)
    {
        if (DiceContainer) DiceContainer.SetActive(true);

        float elapsed = 0f;
        float intervalTime = rollDuration / rollIterations;

        while (elapsed < rollDuration)
        {
            SetDiceFace(Dice1_Image, Random.Range(0, 6));
            SetDiceFace(Dice2_Image, Random.Range(0, 6));
            SetDiceFace(Dice3_Image, Random.Range(0, 6));

            yield return new WaitForSeconds(intervalTime);
            elapsed += intervalTime;
        }

        SetDiceFace(Dice1_Image, data.dice1 - 1);
        SetDiceFace(Dice2_Image, data.dice2 - 1);
        SetDiceFace(Dice3_Image, data.dice3 - 1);

        if (DiceContainer)
        {
            DiceContainer.transform.DOScale(1.2f, 0.2f)
                .OnComplete(() => DiceContainer.transform.DOScale(1f, 0.2f));
        }

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
