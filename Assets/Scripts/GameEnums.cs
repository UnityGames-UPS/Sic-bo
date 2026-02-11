/// <summary>
/// All enums used in Sic Bo game
/// </summary>

#region Bet Timer States
public enum BetTimerState
{
    Hidden,
    Betting,    // Show "Place Bet" with time remaining
    Locked,     // Show "Bet Locked" during dice roll
    NextRound   // Show "Next Round in X" countdown
}
#endregion
