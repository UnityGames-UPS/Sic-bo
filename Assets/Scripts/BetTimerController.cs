using System.Collections;
using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// IMPROVED: Manages betting timer UI with correct phase flow
/// - Shows betting time REMAINING during betting phase
/// - Shows "BET LOCKED" while dice roll and result display
/// - Shows "Next Round in X" countdown after round ends
/// - Last 5 seconds: indicator + SINGLE POP animation per number change
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

    [Header("Pop Animation Settings")]
    [SerializeField] private float heartbeatScale = 1.3f;
    [SerializeField] private float heartbeatDuration = 0.2f;
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

        // Reset timer text scale
        if (bettingTimer_Text) bettingTimer_Text.transform.localScale = Vector3.one;

        // Update timer text
        UpdateBettingTimer(secondsRemaining);
    }

    /// <summary>
    /// Update betting timer - called every second from server sync
    /// Pops timer text during last 5 seconds (one pop per number change)
    /// </summary>
    internal void UpdateBettingTimer(int secondsRemaining)
    {
        currentSeconds = secondsRemaining;

        if (bettingTimer_Text)
        {
            bettingTimer_Text.text = secondsRemaining.ToString();

            // Pop animation on text during last 5 seconds
            if (secondsRemaining <= 5 && secondsRemaining > 0)
            {
                PopTimerText();
            }
        }

        // Show last 5 second indicator
        if (last5SecIndicator)
        {
            bool showIndicator = secondsRemaining <= 5 && secondsRemaining > 0;
            last5SecIndicator.SetActive(showIndicator);

            if (showIndicator)
            {
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

    #region Private Methods - Countdown
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
    #endregion

    #region Private Methods - Pop Animation
    /// <summary>
    /// Single pop animation on timer text - called once per second during last 5 seconds
    /// </summary>
    private void PopTimerText()
    {
        if (bettingTimer_Text == null) return;

        // Kill any existing animation on this text
        bettingTimer_Text.transform.DOKill();

        // Reset scale first
        bettingTimer_Text.transform.localScale = Vector3.one;

        // Pop OUT then back IN - single pulse per number
        bettingTimer_Text.transform.DOScale(heartbeatScale, heartbeatDuration)
            .SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                bettingTimer_Text.transform.DOScale(1f, heartbeatDuration)
                    .SetEase(Ease.InBack);
            });
    }
    #endregion

    #region Unity Lifecycle
    private void OnDestroy()
    {
        StopCountdown();

        // Kill any active tweens on timer text
        if (bettingTimer_Text) bettingTimer_Text.transform.DOKill();
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