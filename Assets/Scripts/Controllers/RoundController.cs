using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("Preset Dice Positions")]
    [SerializeField] private List<DicePositionSet> dicePositionSets = new List<DicePositionSet>();

    [Header("Random Variation Settings")]
    [SerializeField] private float maxRotationOffset = 12f;
    [SerializeField] private float maxPositionJitter = 5f;
    #endregion

    #region Private Fields
    private string currentRoundId;
    private bool isRoundActive = false;
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

        ClearRoundDisplay();
        betController?.ClearAllWinHighlights();
        AudioManager.Instance?.PlayRoundStart();

        diceBoxAnimController?.StartAnimationCycleWithServerSync(
            data.startedAt,
            data.bettingEndTime,
            data.serverTime
        );

        int timeRemaining = GameUtilities.CalculateTimeRemaining(data.bettingEndTime, data.serverTime);
        uiController.UpdateTimer(timeRemaining);
    }

    internal void JoinActiveRound(string roundId, long bettingEndTime)
    {
        currentRoundId = roundId;
        isRoundActive = true;
        diceResultReceived = false;
        currentDiceResult = null;
        currentBettingEndTime = bettingEndTime;

        ClearRoundDisplay();
        betController?.ClearAllWinHighlights();
    }

    internal void SyncAnimationToPhase(string phase, long timeUntilNext, long serverTime)
    {
        diceBoxAnimController?.SyncToPhaseOnJoin(phase, timeUntilNext, serverTime);
    }

    internal void UpdateTimer(int secondsRemaining)
    {
        if (!isRoundActive) return;
        uiController.UpdateTimer(secondsRemaining);
    }

    internal void ShowDiceResult(DiceResultData data)
    {
        if (data == null) return;

        currentDiceResult = data;
        diceResultReceived = true;

        betController.DisableBetting();
        ////uiController.UpdateRoundPhase("RESULT");

        if (diceBoxAnimController != null)
            diceBoxAnimController.RevealDiceResult();
        else
        {
            bool isTriple = data.dice1 == data.dice2 && data.dice2 == data.dice3;
            SetDiceValues(data);
            ShowResult(data.sum, data.matchSide, isTriple);
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

        currentDiceResult = null;
        diceResultReceived = false;
    }

    internal void OnBettingLockedByServer()
    {
        betController.DisableBetting();
        diceBoxAnimController?.OnBettingLocked();
    }

    internal string GetCurrentRoundId() => currentRoundId;
    internal bool IsRoundActive() => isRoundActive;
    #endregion

    #region Animation Callbacks
    private void OnAnimationShowDice()
    {
        if (currentDiceResult == null) return;

        bool isTriple = currentDiceResult.dice1 == currentDiceResult.dice2 && currentDiceResult.dice2 == currentDiceResult.dice3;

        SetDiceValues(currentDiceResult);
        ApplyRandomPresetPosition();
        ShowResult(currentDiceResult.sum, currentDiceResult.matchSide, isTriple);
        PlayDiceResultSounds(currentDiceResult);
    }

    private void OnAnimationHideDice()
    {
        if (DiceContainer) DiceContainer.SetActive(false);
        if (ResultPanel) ResultPanel.SetActive(false);
    }

    private void OnAnimationCycleComplete() => isRoundActive = false;
    #endregion

    #region Dice Display
    private void ApplyRandomPresetPosition()
    {
        if (dicePositionSets == null || dicePositionSets.Count == 0)
        {
            Debug.LogWarning("No Dice Position Sets Assigned!");
            return;
        }

        int randomIndex = Random.Range(0, dicePositionSets.Count);
        DicePositionSet selectedSet = dicePositionSets[randomIndex];

        ApplyToDice(Dice1_Image, selectedSet.GetDice1Pos());
        ApplyToDice(Dice2_Image, selectedSet.GetDice2Pos());
        ApplyToDice(Dice3_Image, selectedSet.GetDice3Pos());
    }
    private void ApplyToDice(Image diceImage, Vector2 basePosition)
    {
        if (diceImage == null) return;

        RectTransform rect = diceImage.rectTransform;
        Vector2 jitter = new Vector2(
            Random.Range(-maxPositionJitter, maxPositionJitter),
            Random.Range(-maxPositionJitter, maxPositionJitter)
        );

        rect.anchoredPosition = basePosition + jitter;
        float randomZ = Random.Range(-maxRotationOffset, maxRotationOffset);
        rect.localRotation = Quaternion.Euler(0f, 0f, randomZ);
    }
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

    private void ShowResult(int sum, string matchSide, bool isTriple)
    {
        if (Sum_Text)
        {
            Sum_Text.text = sum.ToString();
            Sum_Text.color = (sum % 2 != 0) ? oddSumColor : evenSumColor;
        }

        // FIXED: Always clear all indicators first
        if (SmallImage) SmallImage.SetActive(false);
        if (BigImage) BigImage.SetActive(false);
        if (OddImage) OddImage.SetActive(false);
        if (EvenImage) EvenImage.SetActive(false);

        // FIXED: Never show big/small for triples
        if (isTriple)
        {
            // For triples, only show odd/even (though this is rare)
            if (sum % 2 != 0 && OddImage) OddImage.SetActive(true);
            else if (sum % 2 == 0 && EvenImage) EvenImage.SetActive(true);
            return;
        }

        // For non-triples, determine big/small
        bool showBig = false;
        bool showSmall = false;

        if (!string.IsNullOrEmpty(matchSide))
        {
            // FIXED: Use server's matchSide if available (most reliable)
            string side = matchSide.ToLower();
            showBig = side == "big";
            showSmall = side == "small";
        }
        else
        {
            // Fallback to calculation if matchSide not provided
            showSmall = sum >= 4 && sum <= 10;
            showBig = sum >= 11 && sum <= 17;
        }

        // FIXED: Only activate the correct indicator, never both
        if (showSmall && SmallImage)
        {
            SmallImage.SetActive(true);
        }
        else if (showBig && BigImage)
        {
            BigImage.SetActive(true);
        }

        // Set odd/even
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

[System.Serializable]
public class DicePositionSet
{
    public RectTransform dice1Ref;
    public RectTransform dice2Ref;
    public RectTransform dice3Ref;

    public Vector2 GetDice1Pos() => dice1Ref != null ? dice1Ref.anchoredPosition : Vector2.zero;
    public Vector2 GetDice2Pos() => dice2Ref != null ? dice2Ref.anchoredPosition : Vector2.zero;
    public Vector2 GetDice3Pos() => dice3Ref != null ? dice3Ref.anchoredPosition : Vector2.zero;
}