using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    #region Serialized Fields
    [Header("Controllers")]
    [SerializeField] private UIController uiController;
    [SerializeField] private BetController betController;
    [SerializeField] private RoundController roundController;
    [SerializeField] private HistoryController historyController;
    [SerializeField] private BetLimitManager betLimitManager;
    [SerializeField] private ChipWinAnimationController chipWinAnimationController;
    [SerializeField] private BonusIndicatorController bonusIndicatorController;
    [SerializeField] private OpponentChipManager opponentChipManager;
    [SerializeField] private ResultPlaneController resultPlaneController;

    [Header("Socket")]
    [SerializeField] private SocketIOManager socketManager;



    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    #endregion

    #region Internal Properties
    internal string CurrentRoom { get; private set; }
    internal string PlayerUsername { get; private set; }
    internal double CurrentBalance { get; private set; }
    internal string CurrentRoundId { get; private set; }
    internal Wagers CurrentWagers { get; private set; }
    #endregion

    #region Private Fields
    private string pendingRoomSwitch = null;
    #endregion

    #region Socket Callbacks - Initialization
    internal void OnInitDataReceived()
    {
        if (socketManager.InitialData == null || socketManager.PlayerData == null) return;

        CurrentBalance = socketManager.PlayerData.balance;
        CurrentWagers = socketManager.InitialData.wagers;
        PlayerUsername = socketManager.PlayerData.username;

        betController.SetCurrentPlayerUsername(PlayerUsername);
        betController.SetLeaderboardData(socketManager.InitialData.leaderboards);

        uiController.SetupInitialData(
            PlayerUsername,
            CurrentBalance,
            socketManager.InitialData.leaderboards,
            socketManager.InitialData.wagers,
            socketManager.InitialData.bets
        );

        uiController.SetupWinRatios(socketManager.InitialData.wagers);

        if (socketManager.InitialData.lobby != null)
        {
            uiController.UpdateLobbyPlayerCounts(
                socketManager.InitialData.lobby.casual,
                socketManager.InitialData.lobby.novice,
                socketManager.InitialData.lobby.expert,
                socketManager.InitialData.lobby.high_roller
            );
        }

        uiController.ShowHomeScreen();
    }
    #endregion

    #region Socket Callbacks - Room
    internal void OnRoomJoinedWithData(RoomPayload payload)
    {
        if (payload == null) return;

        uiController.HideLoadingScreen();
        uiController.UpdateTotalPlayerCount(payload.playerCount);
        uiController.UpdateLeaderboards(payload.leaderboards);
        betController.SetLeaderboardData(payload.leaderboards);

        if (payload.stats != null && payload.stats.Count > 0)
            uiController.UpdateStats(GameUtilities.CalculateStats(payload.stats));

        if (payload.roundState != null && !string.IsNullOrEmpty(payload.roundState.roundId))
        {
            CurrentRoundId = payload.roundState.roundId;
            uiController.UpdateRoundId(payload.roundState.roundId);

            string phase = (payload.roundState.phase ?? "").ToLower();

            switch (phase)
            {
                case "betting":
                    {
                        int timeRemaining = GameUtilities.CalculateTimeRemaining(
                            payload.roundState.bettingEndTime,
                            payload.roundState.serverTime);

                        uiController.ShowBettingPhase(timeRemaining);
                        //uiController.UpdateRoundPhase("BETTING");
                        betController.EnableBetting();

                        roundController.StartRound(new RoundStartData
                        {
                            roundId = payload.roundState.roundId,
                            startedAt = payload.roundState.startedAt,
                            bettingEndTime = payload.roundState.bettingEndTime,
                            serverTime = payload.roundState.serverTime,
                            playerCount = payload.playerCount
                        });
                        break;
                    }
                case "rolling":
                case "result":
                    uiController.ShowBetLocked();
                    //uiController.UpdateRoundPhase("ROLLING");
                    betController.DisableBetting();
                    break;
                case "nextround":
                    {
                        int secondsUntilNext = GameUtilities.CalculateTimeRemaining(
                            payload.roundState.bettingEndTime,
                            payload.roundState.serverTime);
                        uiController.ShowNextRound(secondsUntilNext);
                        //uiController.UpdateRoundPhase("NEXTROUND");
                        betController.DisableBetting();
                        break;
                    }
                default:
                    uiController.ShowBetLocked();
                    //uiController.UpdateRoundPhase("WAITING");
                    betController.DisableBetting();
                    break;
            }
        }
        else
        {
            CurrentRoundId = null;
            uiController.UpdateRoundId(null);
            //uiController.UpdateRoundPhase("WAITING");
            uiController.HideAllTimers();
        }
    }
    #endregion

    #region Socket Callbacks - Round Events
    internal void OnRoundStart(RoundStartData data)
    {
        if (data == null) return;

        CurrentRoundId = data.roundId;

        int timeRemaining = GameUtilities.CalculateTimeRemaining(data.bettingEndTime, data.serverTime);

        uiController.UpdatePlayerCountInLevel(data.playerCount);
        uiController.UpdateRoundId(data.roundId);
        uiController.ShowBettingPhase(timeRemaining);
        //uiController.UpdateRoundPhase("BETTING");

        roundController.StartRound(data);
        betController.OnRoundStart();
        betController.EnableBetting();

        chipWinAnimationController?.ResetAll();
        bonusIndicatorController?.ClearAllIndicators();
    }

    internal void OnBettingTimer(TimerData data)
    {
        if (!ValidateTimerData(data)) return;

        int timeRemaining = GameUtilities.CalculateTimeRemaining(data.bettingEndTime, data.serverTime);
        roundController.UpdateTimer(timeRemaining);
        uiController.UpdateTimer(timeRemaining);
    }

    internal void OnBonus(BonusData data)
    {
        if (data == null || !data.HasBonusDictionary()) return;
        bonusIndicatorController?.ShowBonusAnnouncements(data.bonus);
    }

    internal void OnDiceResult(DiceResultData data)
    {
        if (!ValidateDiceResult(data)) return;

        resultPlaneController?.AddNewResult(data);

        betController.DisableBetting();
        uiController.ShowBetLocked();
        //uiController.UpdateRoundPhase("RESULT");

        roundController.ShowDiceResult(data);
        betController.HighlightWinningAreas(data.matchSide, data.sum);
        betController.HighlightTripleDiceResult(data.dice1, data.dice2, data.dice3);

        List<string> winningBetOptions = betController.GetWinningBetOptions(); // keep for chip animation
        bonusIndicatorController?.HandleDiceResult(GetAllWinningAreasFromDice(data)); // use dice-based list

        if (chipWinAnimationController != null)
        {
            List<WinAreaData> winAreas = betController.GetWinningAreasData();
            if (winAreas != null && winAreas.Count > 0)
                chipWinAnimationController.PlayDiceResultAnimation(winAreas, data);
        }
    }
    private List<string> GetAllWinningAreasFromDice(DiceResultData data)
    {
        var winners = new List<string>();

        // Main bets - from matchSide
        string side = (data.matchSide ?? "").ToLower();
        if (side == "small") winners.Add("small");
        if (side == "big") winners.Add("big");
        if (side == "odd") winners.Add("odd");
        if (side == "even") winners.Add("even");

        // Sum areas
        winners.Add($"sum_{data.sum}");

        // Single dice (1-6)
        var diceValues = new[] { data.dice1, data.dice2, data.dice3 };
        var seen = new HashSet<int>();
        foreach (int d in diceValues)
        {
            if (seen.Add(d))
                winners.Add($"single_{d}");
        }

        // Triple same dice → specific_3_X
        if (data.dice1 == data.dice2 && data.dice2 == data.dice3)
            winners.Add($"specific_3_{data.dice1}");

        return winners;
    }
    internal void OnBetPlaced(BetPlacedData data)
    {
        if (data == null) return;
        betController.OnBetPlacedBroadcast(data);
    }

    internal void OnCashout(CashoutData data)
    {
        if (data == null) return;

        if (data.leaderboards != null)
        {
            uiController.UpdateLeaderboards(data.leaderboards);
            betController.SetLeaderboardData(data.leaderboards);
        }

        if (data.stats != null && data.stats.Count > 0)
            uiController.UpdateStats(GameUtilities.CalculateStats(data.stats));

        if (data.payouts != null)
        {
            foreach (var payout in data.payouts)
            {
                if (payout.username == PlayerUsername)
                {
                    ApplyBalanceUpdate(payout.balance);

                    if (payout.win > 0)
                    {
                        uiController.ShowWinAnimation(payout.win);
                        chipWinAnimationController?.PlayCashoutAnimation();
                    }
                }
            }
        }

        opponentChipManager?.PlayCashoutAnimation();
        betController.ClearAllBets(false);
    }

    internal void OnRoundEnd(RoundEndPayload data)
    {
        if (data == null) return;

        int secondsUntilNextRound = GameUtilities.CalculateTimeRemaining(data.nextRoundStartTime, data.serverTime);
        uiController.ShowNextRound(secondsUntilNextRound);
        //uiController.UpdateRoundPhase("NEXTROUND");
        betController.OnRoundEnd();
    }

    internal void OnLobbyCount(LobbyCountData data)
    {
        if (data?.lobby == null) return;

        uiController.UpdateLobbyPlayerCounts(
            data.lobby.casual,
            data.lobby.novice,
            data.lobby.expert,
            data.lobby.high_roller
        );

        int total = data.lobby.casual + data.lobby.novice + data.lobby.expert + data.lobby.high_roller;
        uiController.UpdateTotalPlayerCount(total);
    }

    internal void OnHistoryReceived(List<HistoryEntry> history, HistoryMeta meta)
    {
        historyController?.UpdateHistoryData(history, meta);
    }

    internal void OnBetActionResponse(BetAckResponse response)
    {
        if (response == null)
        {
            betController.OnBetActionResponse(null);
            return;
        }

        if (response.success)
        {
            if (response.payload != null)
                ApplyBalanceUpdate(response.payload.balance);

            betController.OnBetActionResponse(response);
        }
        else
        {
            string errorMsg = response.payload?.message ?? "Bet action failed";

            if (errorMsg == "Limit reached")
                betController.OnBetLimitReached();
            else if (errorMsg.Contains("Insufficient"))
                uiController.ShowInGamePopup("Insufficient balance");
            else if (errorMsg.Contains("not active") || errorMsg.Contains("locked"))
                uiController.ShowInGamePopup("Betting is locked");
            else
                uiController.ShowInGamePopup(errorMsg);

            betController.OnBetActionResponse(null);
        }
    }
    #endregion

    #region Internal API - Room Management
    internal void JoinRoom(string roomName)
    {
        uiController.ShowLoadingScreen("Joining Room...");

        CurrentRoom = roomName;

        List<double> chipValues = GetChipValuesForRoom(roomName);
        Wagers wagers = socketManager.InitialData?.wagers;

        betController.SetupChips(chipValues, wagers, roomName);

        if (betLimitManager != null && socketManager.InitialData != null)
        {
            betLimitManager.Initialize(
                socketManager.InitialData.wagers,
                socketManager.InitialData.bets,
                roomName,
                socketManager.InitialData.betOptions
            );
        }

        socketManager.JoinLevel(roomName);
        uiController.ShowGameScreen();
        uiController.UpdateRoundId(null);
    }

    internal void LeaveRoom()
    {
        uiController.ShowLoadingScreen("Returning to Lobby...");

        betController.DisableBetting();
        betController.ClearAllBets(true);
        betController.ClearAllWinHighlights();
        roundController.ClearRoundDisplay();
        resultPlaneController?.ClearAllResults();
        socketManager.ReturnHome();
        uiController.ShowHomeScreen();
        uiController.HideAllTimers();

        CurrentRoom = null;
        CurrentRoundId = null;
        uiController.ClearRoundId();
    }

    /// <summary>
    /// Switches to a different room without going back to the home screen.
    /// Cleans up the current room, emits HOME to the server, then waits for
    /// OnLeaveAcknowledged (fired from OnHomeAck) before joining the new room.
    /// </summary>
    internal void SwitchRoom(string targetRoom)
    {
        if (targetRoom == CurrentRoom) return;

        pendingRoomSwitch = targetRoom;

        uiController.ShowLoadingScreen("Joining Room...");

        betController.DisableBetting();
        betController.ClearAllBets(true);
        betController.ClearAllWinHighlights();
        roundController.ClearRoundDisplay();
        resultPlaneController?.ClearAllResults();
        bonusIndicatorController?.ClearAllIndicators();
        uiController.HideAllTimers();

        CurrentRoom = null;
        CurrentRoundId = null;
        uiController.ClearRoundId();

        // Emit HOME — OnLeaveAcknowledged is called when server confirms leave
        socketManager.ReturnHome();
    }

    /// <summary>
    /// Called by SocketIOManager.OnHomeAck when the server confirms the player has left.
    /// If a room switch is pending, joins that room; otherwise hides the loading screen
    /// (normal leave flow — home screen was already shown by LeaveRoom).
    /// </summary>
    internal void OnLeaveAcknowledged()
    {
        if (!string.IsNullOrEmpty(pendingRoomSwitch))
        {
            string target = pendingRoomSwitch;
            pendingRoomSwitch = null;
            JoinRoom(target);
        }
        else
        {
            // Normal leave — home screen is already visible, just hide loading
            uiController.HideLoadingScreen();
        }
    }
    #endregion

    #region Internal API - Betting Actions
    internal void PlaceBet(string betOption, int chipIndex)
    {
        if (string.IsNullOrEmpty(CurrentRoom)) return;

        AudioManager.Instance?.PlayPlayerBetPlace();

        socketManager.PlaceBet(GetBetType(betOption), betOption, chipIndex, CurrentRoom);
    }

    internal void UndoBet() => socketManager.UndoBet();
    internal void CancelAllBets() => socketManager.CancelBet();
    internal void DoubleBet() => socketManager.DoubleBet(CurrentRoom);
    internal void RepeatBet() => socketManager.RepeatBet();
    #endregion

    #region Internal API - History and Exit
    internal void RequestHistory(int page) => socketManager.RequestHistory(page);

    internal void ExitGame()
    {
        uiController.ShowLoadingScreen("Exiting Game...");
      

        betController?.DisableBetting();
        betController?.ClearAllBets(true);
        betController?.ClearAllWinHighlights();
        roundController?.ClearRoundDisplay();

        CurrentRoom = null;
        StartCoroutine(socketManager.CloseSocket());
    }
    #endregion

    #region Internal API - Wager Queries
    internal double GetMaxBetForBetOption(string betOption)
    {
        if (string.IsNullOrEmpty(CurrentRoom) || CurrentWagers == null) return 0;
        return GetWagerForBetOption(betOption)?.GetMaxBet(CurrentRoom) ?? 0;
    }

    internal BetWager GetWagerForBetOption(string betOption)
    {
        if (CurrentWagers == null) return null;

        if (betOption == "small") return CurrentWagers.main_bets?.small;
        if (betOption == "big") return CurrentWagers.main_bets?.big;
        if (betOption == "odd") return CurrentWagers.main_bets?.odd;
        if (betOption == "even") return CurrentWagers.main_bets?.even;

        if (betOption.StartsWith("single_")) return CurrentWagers.side_bets?.single_match_1;
        if (betOption.StartsWith("specific_3_")) return CurrentWagers.side_bets?.specific_3;
        if (betOption == "specific_2") return CurrentWagers.side_bets?.specific_2;

        if (betOption.StartsWith("sum_") && int.TryParse(betOption.Substring(4), out int sumVal))
        {
            return sumVal switch
            {
                4 => CurrentWagers.op_bets?.sum_4,
                5 => CurrentWagers.op_bets?.sum_5,
                6 => CurrentWagers.op_bets?.sum_6,
                7 => CurrentWagers.op_bets?.sum_7,
                8 => CurrentWagers.op_bets?.sum_8,
                9 => CurrentWagers.op_bets?.sum_9,
                10 => CurrentWagers.op_bets?.sum_10,
                11 => CurrentWagers.op_bets?.sum_11,
                12 => CurrentWagers.op_bets?.sum_12,
                13 => CurrentWagers.op_bets?.sum_13,
                14 => CurrentWagers.op_bets?.sum_14,
                15 => CurrentWagers.op_bets?.sum_15,
                16 => CurrentWagers.op_bets?.sum_16,
                17 => CurrentWagers.op_bets?.sum_17,
                _ => null
            };
        }

        return null;
    }
    #endregion

    #region Private Helpers
    private void ApplyBalanceUpdate(double newBalance)
    {
        if (newBalance < 0)
        {
            Debug.LogError($"[GameManager] Negative balance received: {newBalance}");
            return;
        }
        CurrentBalance = newBalance;
        uiController.UpdateBalance(newBalance);
    }

    private List<double> GetChipValuesForRoom(string roomName)
    {
        if (socketManager.InitialData?.bets == null) return new List<double>();

        return roomName switch
        {
            "casual" => socketManager.InitialData.bets.casual ?? new List<double>(),
            "novice" => socketManager.InitialData.bets.novice ?? new List<double>(),
            "expert" => socketManager.InitialData.bets.expert ?? new List<double>(),
            "high_roller" => socketManager.InitialData.bets.high_roller ?? new List<double>(),
            _ => new List<double>()
        };
    }

    private string GetBetType(string betOption)
    {
        if (betOption == "small" || betOption == "big" || betOption == "odd" || betOption == "even")
            return "main_bets";

        if (betOption.StartsWith("single_") || betOption == "specific_2" || betOption.StartsWith("specific_3_"))
            return "side_bets";

        if (betOption.StartsWith("sum_"))
            return "op_bets";

        return "main_bets";
    }

    private bool ValidateDiceResult(DiceResultData data)
    {
        if (data == null) return false;

        if (data.dice1 < 1 || data.dice1 > 6 ||
            data.dice2 < 1 || data.dice2 > 6 ||
            data.dice3 < 1 || data.dice3 > 6)
        {
            Debug.LogError($"[GameManager] Invalid dice values: {data.dice1},{data.dice2},{data.dice3}");
            return false;
        }

        if (data.sum != data.dice1 + data.dice2 + data.dice3)
        {
            Debug.LogError($"[GameManager] Dice sum mismatch. Expected {data.dice1 + data.dice2 + data.dice3}, got {data.sum}");
            return false;
        }

        return true;
    }

    private bool ValidateTimerData(TimerData data)
    {
        if (data == null) return false;
        if (data.timeRemaining < 0 || data.timeRemaining > 60000) return false;
        return true;
    }
    #endregion
}