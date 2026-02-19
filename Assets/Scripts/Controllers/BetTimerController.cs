using System.Collections;
using UnityEngine;
using TMPro;
using DG.Tweening;

public class BetTimerController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Betting Phase")]
    [SerializeField] private GameObject placeBetPanel;
    [SerializeField] private GameObject last5SecIndicator;
    [SerializeField] private TMP_Text bettingTimer_Text;

    [Header("Locked Phase")]
    [SerializeField] private GameObject betLockedPanel;

    [Header("Next Round Phase")]
    [SerializeField] private GameObject nextRoundPanel;
    [SerializeField] private TMP_Text nextRoundTimer_Text;

    [Header("Animation Settings")]
    [SerializeField] private float heartbeatScale = 1.3f;
    [SerializeField] private float heartbeatDuration = 0.2f;
    #endregion

    #region Private Fields
    private BetTimerState currentState = BetTimerState.Hidden;
    private int currentSeconds = 0;
    private Coroutine countdownCoroutine;
    private bool isClockTickActive = false;
    #endregion

    #region Unity Lifecycle
    private void OnDestroy()
    {
        StopCountdown();
        StopClockTick();
        bettingTimer_Text?.transform.DOKill();
    }
    #endregion

    #region Internal API
    internal void ShowBettingPhase(int secondsRemaining)
    {
        StopCountdown();

        currentState = BetTimerState.Betting;
        currentSeconds = secondsRemaining;

        if (placeBetPanel) placeBetPanel.SetActive(true);
        if (betLockedPanel) betLockedPanel.SetActive(false);
        if (nextRoundPanel) nextRoundPanel.SetActive(false);
        if (bettingTimer_Text) bettingTimer_Text.transform.localScale = Vector3.one;

        UpdateBettingTimer(secondsRemaining);
    }

    internal void UpdateBettingTimer(int secondsRemaining)
    {
        currentSeconds = secondsRemaining;

        if (bettingTimer_Text)
        {
            bettingTimer_Text.text = secondsRemaining.ToString();

            if (secondsRemaining <= 5 && secondsRemaining > 0)
            {
                PopTimerText();

                if (!isClockTickActive)
                    StartClockTick();
            }
            else if (secondsRemaining > 5 && isClockTickActive)
            {
                StopClockTick();
            }
        }

        if (last5SecIndicator)
            last5SecIndicator.SetActive(secondsRemaining <= 5 && secondsRemaining > 0);

        if (secondsRemaining == 0)
            StopClockTick();
    }

    internal void ShowBetLocked()
    {
        StopCountdown();
        StopClockTick();

        currentState = BetTimerState.Locked;

        if (placeBetPanel) placeBetPanel.SetActive(false);
        if (betLockedPanel) betLockedPanel.SetActive(true);
        if (nextRoundPanel) nextRoundPanel.SetActive(false);
        if (last5SecIndicator) last5SecIndicator.SetActive(false);
    }

    internal void ShowNextRound(int secondsUntilNextRound)
    {
        StopCountdown();
        StopClockTick();

        currentState = BetTimerState.NextRound;
        currentSeconds = secondsUntilNextRound;

        if (placeBetPanel) placeBetPanel.SetActive(false);
        if (betLockedPanel) betLockedPanel.SetActive(false);
        if (nextRoundPanel) nextRoundPanel.SetActive(true);
        if (last5SecIndicator) last5SecIndicator.SetActive(false);

        UpdateNextRoundTimer(secondsUntilNextRound);
        countdownCoroutine = StartCoroutine(NextRoundCountdown());
    }

    internal void UpdateNextRoundTimer(int seconds)
    {
        currentSeconds = seconds;
        if (nextRoundTimer_Text) nextRoundTimer_Text.text = seconds.ToString();
    }

    internal void HideAll()
    {
        StopCountdown();
        StopClockTick();

        currentState = BetTimerState.Hidden;

        if (placeBetPanel) placeBetPanel.SetActive(false);
        if (betLockedPanel) betLockedPanel.SetActive(false);
        if (nextRoundPanel) nextRoundPanel.SetActive(false);
        if (last5SecIndicator) last5SecIndicator.SetActive(false);
    }

    internal BetTimerState GetState() => currentState;
    internal int GetCurrentSeconds() => currentSeconds;
    #endregion

    #region Countdown
    private IEnumerator NextRoundCountdown()
    {
        while (currentSeconds > 0 && currentState == BetTimerState.NextRound)
        {
            yield return new WaitForSeconds(1f);
            currentSeconds--;
            UpdateNextRoundTimer(currentSeconds);
        }
        countdownCoroutine = null;
    }

    private void StopCountdown()
    {
        if (countdownCoroutine != null) { StopCoroutine(countdownCoroutine); countdownCoroutine = null; }
    }
    #endregion

    #region Animation
    private void PopTimerText()
    {
        if (bettingTimer_Text == null) return;
        bettingTimer_Text.transform.DOKill();
        bettingTimer_Text.transform.localScale = Vector3.one;
        bettingTimer_Text.transform.DOScale(heartbeatScale, heartbeatDuration)
            .SetEase(Ease.OutBack)
            .OnComplete(() => bettingTimer_Text.transform.DOScale(1f, heartbeatDuration).SetEase(Ease.InBack));
    }
    #endregion

    #region Clock Tick
    private void StartClockTick()
    {
        if (AudioManager.Instance == null) return;
        AudioManager.Instance.StartClockTick();
        isClockTickActive = true;
    }

    private void StopClockTick()
    {
        if (AudioManager.Instance != null && isClockTickActive)
            AudioManager.Instance.StopClockTick();
        isClockTickActive = false;
    }
    #endregion
}