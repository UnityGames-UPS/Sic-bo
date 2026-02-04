using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Manages the three betting timer states:
/// 1. During Betting (Place Bet panel, Timer, Last 5 Sec indicator)
/// 2. Bet Locked (Bet Locked object)
/// 3. Next Round (Next Round panel with countdown)
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
    #endregion

    #region Public API
    /// <summary>
    /// Show betting phase with countdown timer
    /// </summary>
    internal void ShowBettingPhase(int seconds)
    {
        currentState = BetTimerState.Betting;
        currentSeconds = seconds;

        if (placeBetPanel) placeBetPanel.SetActive(true);
        if (betLockedPanel) betLockedPanel.SetActive(false);
        if (nextRoundPanel) nextRoundPanel.SetActive(false);

        UpdateBettingTimer(seconds);
    }

    /// <summary>
    /// Update betting timer and handle last 5 seconds indicator
    /// </summary>
    internal void UpdateBettingTimer(int seconds)
    {
        currentSeconds = seconds;

        if (bettingTimer_Text)
        {
            bettingTimer_Text.text = seconds.ToString();
        }

        // Show last 5 seconds indicator
        if (last5SecIndicator)
        {
            last5SecIndicator.SetActive(seconds <= 5 && seconds > 0);
        }
    }

    /// <summary>
    /// Show bet locked state (betting is closed, waiting for dice result)
    /// </summary>
    internal void ShowBetLocked()
    {
        currentState = BetTimerState.Locked;

        if (placeBetPanel) placeBetPanel.SetActive(false);
        if (betLockedPanel) betLockedPanel.SetActive(true);
        if (nextRoundPanel) nextRoundPanel.SetActive(false);
        if (last5SecIndicator) last5SecIndicator.SetActive(false);
    }

    /// <summary>
    /// Show next round countdown (between rounds)
    /// </summary>
    internal void ShowNextRound(int seconds)
    {
        currentState = BetTimerState.NextRound;
        currentSeconds = seconds;

        if (placeBetPanel) placeBetPanel.SetActive(false);
        if (betLockedPanel) betLockedPanel.SetActive(false);
        if (nextRoundPanel) nextRoundPanel.SetActive(true);
        if (last5SecIndicator) last5SecIndicator.SetActive(false);

        UpdateNextRoundTimer(seconds);
    }

    /// <summary>
    /// Update next round timer countdown
    /// </summary>
    internal void UpdateNextRoundTimer(int seconds)
    {
        if (nextRoundTimer_Text)
        {
            nextRoundTimer_Text.text = seconds.ToString();
        }
    }

    /// <summary>
    /// Hide all timer panels
    /// </summary>
    internal void HideAll()
    {
        currentState = BetTimerState.Hidden;

        if (placeBetPanel) placeBetPanel.SetActive(false);
        if (betLockedPanel) betLockedPanel.SetActive(false);
        if (nextRoundPanel) nextRoundPanel.SetActive(false);
        if (last5SecIndicator) last5SecIndicator.SetActive(false);
    }

    /// <summary>
    /// Get current timer state
    /// </summary>
    internal BetTimerState GetState()
    {
        return currentState;
    }

    /// <summary>
    /// Get current countdown value
    /// </summary>
    internal int GetCurrentSeconds()
    {
        return currentSeconds;
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
