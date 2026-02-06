using System.Collections;
using UnityEngine;
using TMPro;

public class BetTimerController : MonoBehaviour
{
    #region Serialized Fields
    [Header("During Betting State")]
    [SerializeField] private GameObject placeBetPanel;
    [SerializeField] private GameObject last5SecIndicator;
    [SerializeField] private TMP_Text bettingTimer_Text;

    [Header("Bet Locked State")]
    [SerializeField] private GameObject betLockedPanel;

    [Header("Next Round State")]
    [SerializeField] private GameObject nextRoundPanel;
    [SerializeField] private TMP_Text nextRoundTimer_Text;
    #endregion

    #region Private Fields
    private BetTimerState currentState = BetTimerState.Hidden;
    private int currentSeconds = 0;
    private Coroutine countdownCoroutine;
    #endregion

    #region Public API
    internal void ShowBettingPhase(int seconds)
    {
        StopCountdown();

        currentState = BetTimerState.Betting;
        currentSeconds = seconds;

        if (placeBetPanel) placeBetPanel.SetActive(true);
        if (betLockedPanel) betLockedPanel.SetActive(false);
        if (nextRoundPanel) nextRoundPanel.SetActive(false);

        UpdateBettingTimer(seconds);
    }

    internal void UpdateBettingTimer(int seconds)
    {
        currentSeconds = seconds;

        if (bettingTimer_Text)
        {
            bettingTimer_Text.text = seconds.ToString();
        }

        if (last5SecIndicator)
        {
            last5SecIndicator.SetActive(seconds <= 5 && seconds > 0);
        }
    }

    internal void ShowBetLocked()
    {
        StopCountdown();

        currentState = BetTimerState.Locked;

        if (placeBetPanel) placeBetPanel.SetActive(false);
        if (betLockedPanel) betLockedPanel.SetActive(true);
        if (nextRoundPanel) nextRoundPanel.SetActive(false);
        if (last5SecIndicator) last5SecIndicator.SetActive(false);
    }

    internal void ShowNextRound(int seconds)
    {
        StopCountdown();

        currentState = BetTimerState.NextRound;
        currentSeconds = seconds;

        if (placeBetPanel) placeBetPanel.SetActive(false);
        if (betLockedPanel) betLockedPanel.SetActive(false);
        if (nextRoundPanel) nextRoundPanel.SetActive(true);
        if (last5SecIndicator) last5SecIndicator.SetActive(false);

        UpdateNextRoundTimer(seconds);

        countdownCoroutine = StartCoroutine(NextRoundCountdown());
    }

    internal void UpdateNextRoundTimer(int seconds)
    {
        currentSeconds = seconds;

        if (nextRoundTimer_Text)
        {
            nextRoundTimer_Text.text = seconds.ToString();
        }
    }

    internal void HideAll()
    {
        StopCountdown();

        currentState = BetTimerState.Hidden;

        if (placeBetPanel) placeBetPanel.SetActive(false);
        if (betLockedPanel) betLockedPanel.SetActive(false);
        if (nextRoundPanel) nextRoundPanel.SetActive(false);
        if (last5SecIndicator) last5SecIndicator.SetActive(false);
    }

    internal BetTimerState GetState()
    {
        return currentState;
    }

    internal int GetCurrentSeconds()
    {
        return currentSeconds;
    }
    #endregion

    #region Private Methods
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
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }
    }

    private void OnDestroy()
    {
        StopCountdown();
    }
    #endregion
}

#region Enums
public enum BetTimerState
{
    Hidden,
    Betting,
    Locked,
    NextRound
}
#endregion