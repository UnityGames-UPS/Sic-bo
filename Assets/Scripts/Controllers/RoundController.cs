using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

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
    [SerializeField] private GameObject ResultPanel;

    [Header("Result Indicators")]
    [SerializeField] private GameObject SmallImage;
    [SerializeField] private GameObject BigImage;
    [SerializeField] private GameObject OddImage;
    [SerializeField] private GameObject EvenImage;

    [Header("Sum Text Colors")]
    [SerializeField] private Color oddSumColor = Color.red;
    [SerializeField] private Color evenSumColor = Color.black;

    [Header("References")]
    [SerializeField] private UIController uiController;
    [SerializeField] private BetController betController;

    [Header("Dice Box Animation")]
    [SerializeField] private DiceBoxAnimationController diceBoxAnimController;

    [Header("Audio")]
    [SerializeField] private float diceResultSoundDelay = 1.0f;
    #endregion

    #region Private Fields
    private string currentRoundId;
    private bool isRoundActive = false;
    private Coroutine finalCountdownCoroutine;
    private DiceResultData currentDiceResult;
    private bool diceResultReceived = false;
    private long currentBettingEndTime = 0;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (diceBoxAnimController != null)
        {
            diceBoxAnimController.SetDiceShowCallback(OnAnimationShowDice);
            diceBoxAnimController.SetDiceHideCallback(OnAnimationHideDice);
            diceBoxAnimController.SetAnimationCycleCompleteCallback(OnAnimationCycleComplete);
        }

        if (DiceContainer) DiceContainer.SetActive(false);
        if (ResultPanel) ResultPanel.SetActive(false);
    }
    #endregion

    #region Internal API
    internal void StartRound(RoundStartData data)
    {
        if (data == null) return;

        currentRoundId = data.roundId;
        isRoundActive = true;
        diceResultReceived = false;
        currentDiceResult = null;
        currentBettingEndTime = data.bettingEndTime;

        if (finalCountdownCoroutine != null)
        {
            StopCoroutine(finalCountdownCoroutine);
            finalCountdownCoroutine = null;
        }

        ClearRoundDisplay();
        betController?.ClearAllWinHighlights();
        AudioManager.Instance?.PlayRoundStart();

        diceBoxAnimController?.StartAnimationCycleWithServerSync(
            data.startedAt,
            data.bettingEndTime,
            data.serverTime
        );

        uiController.UpdateRoundPhase("BETTING");

        int timeRemaining = GameUtilities.CalculateTimeRemaining(data.bettingEndTime, data.serverTime);
        uiController.UpdateTimer(timeRemaining);
    }

    internal void UpdateTimer(int secondsRemaining)
    {
        if (!isRoundActive) return;

        uiController.UpdateTimer(secondsRemaining);

        if (secondsRemaining == 1 && finalCountdownCoroutine == null)
            finalCountdownCoroutine = StartCoroutine(FinalCountdownToZero());
    }

    internal void ShowDiceResult(DiceResultData data)
    {
        if (data == null) return;

        currentDiceResult = data;
        diceResultReceived = true;

        betController.DisableBetting();
        uiController.UpdateRoundPhase("RESULT");

        if (diceBoxAnimController != null)
            diceBoxAnimController.RevealDiceResult();
        else
        {
            SetDiceValues(data);
            ShowResult(data.sum, data.matchSide);
            PlayDiceResultSounds(data);
        }
    }

    internal void ClearRoundDisplay()
    {
        if (DiceContainer) DiceContainer.SetActive(false);
        if (ResultPanel) ResultPanel.SetActive(false);
        if (SmallImage) SmallImage.SetActive(false);
        if (BigImage) BigImage.SetActive(false);
        if (OddImage) OddImage.SetActive(false);
        if (EvenImage) EvenImage.SetActive(false);

        if (finalCountdownCoroutine != null)
        {
            StopCoroutine(finalCountdownCoroutine);
            finalCountdownCoroutine = null;
        }

        currentDiceResult = null;
        diceResultReceived = false;
    }

    internal string GetCurrentRoundId() => currentRoundId;
    internal bool IsRoundActive() => isRoundActive;
    #endregion

    #region Countdown
    private IEnumerator FinalCountdownToZero()
    {
        yield return new WaitForSeconds(1f);
        uiController.UpdateTimer(0);
        betController.DisableBetting();
        uiController.ShowBetLocked();
        diceBoxAnimController?.OnBettingLocked();
        finalCountdownCoroutine = null;
    }
    #endregion

    #region Animation Callbacks
    private void OnAnimationShowDice()
    {
        if (currentDiceResult == null) return;

        SetDiceValues(currentDiceResult);
        if (DiceContainer) DiceContainer.SetActive(true);
        ShowResult(currentDiceResult.sum, currentDiceResult.matchSide);
        AudioManager.Instance?.PlayDiceShow();
        PlayDiceResultSounds(currentDiceResult);

        if (DiceContainer)
        {
            DiceContainer.transform.localScale = Vector3.zero;
            DiceContainer.transform.DOScale(1.2f, 0.3f)
                .SetEase(Ease.OutBack)
                .OnComplete(() => DiceContainer.transform.DOScale(1f, 0.2f));
        }
    }

    private void OnAnimationHideDice()
    {
        if (DiceContainer) DiceContainer.SetActive(false);
        if (ResultPanel) ResultPanel.SetActive(false);
    }

    private void OnAnimationCycleComplete() => isRoundActive = false;
    #endregion

    #region Dice Display
    private void SetDiceValues(DiceResultData data)
    {
        if (DiceSprites == null || DiceSprites.Length < 6) return;
        SetDiceFace(Dice1_Image, data.dice1 - 1);
        SetDiceFace(Dice2_Image, data.dice2 - 1);
        SetDiceFace(Dice3_Image, data.dice3 - 1);
    }

    private void SetDiceFace(Image diceImage, int faceIndex)
    {
        if (diceImage == null || DiceSprites == null) return;
        if (faceIndex < 0 || faceIndex >= DiceSprites.Length) return;
        diceImage.sprite = DiceSprites[faceIndex];
    }

    private void ShowResult(int sum, string matchSide)
    {
        if (Sum_Text)
        {
            Sum_Text.text = sum.ToString();
            Sum_Text.color = (sum % 2 != 0) ? oddSumColor : evenSumColor;
        }
        if (SmallImage) SmallImage.SetActive(false);
        if (BigImage) BigImage.SetActive(false);
        if (OddImage) OddImage.SetActive(false);
        if (EvenImage) EvenImage.SetActive(false);

        if (sum >= 4 && sum <= 10 && SmallImage) SmallImage.SetActive(true);
        if (sum >= 11 && sum <= 17 && BigImage) BigImage.SetActive(true);
        if (sum % 2 != 0 && OddImage) OddImage.SetActive(true);
        else if (sum % 2 == 0 && EvenImage) EvenImage.SetActive(true);

        if (ResultPanel) ResultPanel.SetActive(true);
    }
    #endregion

    #region Audio
    private void PlayDiceResultSounds(DiceResultData data)
    {
        AudioManager.Instance?.PlayDiceResultSequence(data.dice1, data.dice2, data.dice3, diceResultSoundDelay);
    }
    #endregion
}