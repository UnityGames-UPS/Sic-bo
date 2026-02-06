using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// IMPROVED: Manages betting timer UI with correct phase flow
/// - Shows betting time REMAINING during betting phase
/// - Shows "BET LOCKED" while dice roll and result display
/// - Shows "Next Round in X" countdown after round ends
/// - Last 5 seconds indicator for betting urgency
/// </summary>
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
    /// <summary>
    /// Show betting phase with REMAINING time (not total round time)
    /// This shows how much time is LEFT to place bets
    /// </summary>
    internal void ShowBettingPhase(int secondsRemaining)
    {
        StopCountdown();

        currentState = BetTimerState.Betting;
        currentSeconds = secondsRemaining;

        // Activate betting panel
        if (placeBetPanel) placeBetPanel.SetActive(true);
        if (betLockedPanel) betLockedPanel.SetActive(false);
        if (nextRoundPanel) nextRoundPanel.SetActive(false);

        // Update timer text
        UpdateBettingTimer(secondsRemaining);

        Debug.Log($"<color=cyan>[TIMER]</color>Betting Phase - {secondsRemaining}s remaining");
    }

    /// <summary>
    /// Update betting timer - called every second from server sync
    /// </summary>
    internal void UpdateBettingTimer(int secondsRemaining)
    {
        currentSeconds = secondsRemaining;

        if (bettingTimer_Text)
        {
            bettingTimer_Text.text = secondsRemaining.ToString();
        }

        // Show last 5 second indicator
        if (last5SecIndicator)
        {
            bool showIndicator = secondsRemaining <= 5 && secondsRemaining > 0;
            last5SecIndicator.SetActive(showIndicator);

            if (showIndicator && secondsRemaining <= 5)
            {
                Debug.Log($"<color=yellow>[TIMER]</color> LAST {secondsRemaining} SECONDS!");
            }
        }
    }

    /// <summary>
    /// Show "BET LOCKED" state - displayed during dice roll and result
    /// Stays visible until next round countdown starts
    /// </summary>
    internal void ShowBetLocked()
    {
        StopCountdown();

        currentState = BetTimerState.Locked;

        if (placeBetPanel) placeBetPanel.SetActive(false);
        if (betLockedPanel) betLockedPanel.SetActive(true);
        if (nextRoundPanel) nextRoundPanel.SetActive(false);
        if (last5SecIndicator) last5SecIndicator.SetActive(false);

        Debug.Log($"<color=red>[TIMER]</color>BET LOCKED - Dice rolling");
    }

    /// <summary>
    /// Show "Next Round in X" countdown after round ends
    /// </summary>
    internal void ShowNextRound(int secondsUntilNextRound)
    {
        StopCountdown();

        currentState = BetTimerState.NextRound;
        currentSeconds = secondsUntilNextRound;

        if (placeBetPanel) placeBetPanel.SetActive(false);
        if (betLockedPanel) betLockedPanel.SetActive(false);
        if (nextRoundPanel) nextRoundPanel.SetActive(true);
        if (last5SecIndicator) last5SecIndicator.SetActive(false);

        UpdateNextRoundTimer(secondsUntilNextRound);

        // Start countdown coroutine
        countdownCoroutine = StartCoroutine(NextRoundCountdown());

        Debug.Log($"<color=orange>[TIMER]</color>Next Round in {secondsUntilNextRound}s");
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

        Debug.Log($"<color=grey>[TIMER]</color> All timers hidden");
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
    /// <summary>
    /// Client-side countdown for "Next Round" timer
    /// This provides smooth visual countdown while waiting for next round to start
    /// </summary>
    private IEnumerator NextRoundCountdown()
    {
        while (currentSeconds > 0 && currentState == BetTimerState.NextRound)
        {
            yield return new WaitForSeconds(1f);
            currentSeconds--;
            UpdateNextRoundTimer(currentSeconds);
        }

        if (currentSeconds <= 0)
        {
            Debug.Log($"<color=green>[TIMER]</color> Next round countdown complete");
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
    Betting,    // Show "Place Bet" with time remaining
    Locked,     // Show "Bet Locked" during dice roll
    NextRound   // Show "Next Round in X" countdown
}
#endregion