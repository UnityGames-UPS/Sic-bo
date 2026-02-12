using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Main game coordinator handling all game flow and socket communication
/// UPDATED: Now supports array-based bonus multipliers
/// </summary>
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

    [Header("Socket")]
    [SerializeField] private SocketIOManager socketManager;


    #endregion

    #region Public Properties
    internal string CurrentRoom { get; private set; }
    internal string PlayerUsername { get; private set; }
    internal double CurrentBalance { get; private set; }
    internal string CurrentRoundId { get; private set; }
    internal Wagers CurrentWagers { get; private set; }
    #endregion

    #region Socket Callbacks - Initialization
    internal void OnInitDataReceived()
    {
        if (socketManager.InitialData == null || socketManager.PlayerData == null) return;

        CurrentBalance = socketManager.PlayerData.balance;
        CurrentWagers = socketManager.InitialData.wagers;

        betController.SetCurrentPlayerUsername(socketManager.PlayerData.username);
        uiController.SetupInitialData(
            socketManager.PlayerData.username,
            CurrentBalance,
            socketManager.InitialData.leaderboards,
            socketManager.InitialData.wagers,
            socketManager.InitialData.bets
        );
        betController.SetCurrentPlayerUsername(socketManager.PlayerData.username);

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

    internal void OnDataRefreshed()
    {
    }
    #endregion

    #region Socket Callbacks - Room
    internal void OnRoomJoinedWithData(RoomPayload payload)
    {
        if (payload == null) return;

        uiController.UpdatePlayerCount(payload.playerCount);

        // Always try to update leaderboards - LeaderboardController will handle null/empty cases
        Debug.Log($"[GameManager] OnRoomJoinedWithData - leaderboards is {(payload.leaderboards == null ? "null" : "not null")}");
        if (payload.leaderboards == null)
        {
            Debug.LogWarning("[GameManager] Leaderboards data is null in room join payload");
        }

        uiController.UpdateLeaderboards(payload.leaderboards);

        if (payload.roundState == null)
        {
            uiController.UpdateRoundPhase("WAITING");
        }
    }
    #endregion

    #region Socket Callbacks - Round Events
    internal void OnRoundStart(RoundStartData data)
    {
        if (data == null) return;

        CurrentRoundId = data.roundId;

        int timeRemaining = GameUtilities.CalculateTimeRemaining(data.bettingEndTime, data.serverTime);

        uiController.UpdatePlayerCount(data.playerCount);
        uiController.ShowBettingPhase(timeRemaining);
        uiController.UpdateRoundPhase("BETTING");

        roundController.StartRound(data);
        betController.OnRoundStart();
        betController.EnableBetting();

        // Reset chip animation pool ready for next round
        chipWinAnimationController?.ResetAll();

        // Clear bonus indicators from previous round
        bonusIndicatorController?.ClearAllIndicators();
    }

    internal void OnBettingTimer(TimerData data)
    {
        if (data == null) return;

        int timeRemaining = GameUtilities.CalculateTimeRemaining(data.bettingEndTime, data.serverTime);

        roundController.UpdateTimer(timeRemaining);
        uiController.UpdateTimer(timeRemaining);
    }

    internal void OnBonus(BonusData data)
    {
        if (data == null) return;

        if (data.HasBonusDictionary())
        {
            // NEW: Show array-based bonus announcements
            bonusIndicatorController?.ShowBonusAnnouncements(data.bonus);

            // Build debug message
            string bonusText = "BONUS: ";
            foreach (var kvp in data.bonus)
            {
                string betOption = FormatBetOptionName(kvp.Key);
                List<int> multipliers = kvp.Value;

                if (multipliers.Count == 1)
                {
                    bonusText += $"{betOption} x{multipliers[0]}, ";
                }
                else
                {
                    bonusText += $"{betOption} [";
                    for (int i = 0; i < multipliers.Count; i++)
                    {
                        bonusText += $"x{multipliers[i]}";
                        if (i < multipliers.Count - 1) bonusText += ", ";
                    }
                    bonusText += "], ";
                }
            }
            bonusText = bonusText.TrimEnd(',', ' ');

            if (showDebugLogs)
                Debug.Log($"[GameManager] {bonusText}");
        }
    }

    private bool showDebugLogs = true;  // Can be made a serialized field if needed

    private string FormatBetOptionName(string betOption)
    {
        // Convert bet option to readable name
        if (betOption == "small") return "SMALL";
        if (betOption == "big") return "BIG";
        if (betOption == "odd") return "ODD";
        if (betOption == "even") return "EVEN";

        if (betOption.StartsWith("single_"))
        {
            string num = betOption.Substring(7);
            return $"DICE {num}";
        }

        if (betOption.StartsWith("specific_3_"))
        {
            string num = betOption.Substring(11);
            return $"TRIPLE {num}";
        }

        if (betOption.StartsWith("sum_"))
        {
            string num = betOption.Substring(4);
            return $"SUM {num}";
        }

        return betOption.ToUpper();
    }

    internal void OnDiceResult(DiceResultData data)
    {
        if (data == null) return;

        betController.DisableBetting();

        uiController.ShowBetLocked();
        uiController.UpdateRoundPhase("RESULT");

        roundController.ShowDiceResult(data);

        betController.HighlightWinningAreas(data.matchSide, data.sum);
        betController.HighlightTripleDiceResult(data.dice1, data.dice2, data.dice3);

        // Get winning bet options
        List<string> winningBetOptions = betController.GetWinningBetOptions();

        bonusIndicatorController?.HandleDiceResult(winningBetOptions);

        // Trigger chip animation for winning areas (dealer → bet area)
        if (chipWinAnimationController != null)
        {
            List<WinAreaData> winAreas = betController.GetWinningAreasData();
            if (winAreas != null && winAreas.Count > 0)
                chipWinAnimationController.PlayDiceResultAnimation(winAreas);
        }
    }

    internal void OnBetPlaced(BetPlacedData data)
    {
        if (data == null) return;

        bool isOwnPlayer = (data.username == socketManager.PlayerData.username);

        if (isOwnPlayer)
        {
            betController.OnBetPlacedBroadcast(data);
        }
    }

    internal void OnCashout(CashoutData data)
    {
        if (data == null) return;

        if (data.leaderboards != null)
        {
            uiController.UpdateLeaderboards(data.leaderboards);
        }

        if (data.payouts != null)
        {
            foreach (var payout in data.payouts)
            {
                if (payout.username == socketManager.PlayerData.username)
                {
                    CurrentBalance = payout.balance;
                    uiController.UpdateBalance(CurrentBalance);

                    if (payout.win > 0)
                    {
                        uiController.ShowWinAnimation(payout.win);

                        // Trigger cashout chip sweep animation (bet area → player)
                        chipWinAnimationController?.PlayCashoutAnimation();
                    }
                }
            }
        }

        // Trigger opponent chip cashout animation (bet area → dealers)
        opponentChipManager?.PlayCashoutAnimation();

        betController.ClearAllBets();
    }

    internal void OnRoundEnd(RoundEndPayload data)
    {
        if (data == null) return;

        int secondsUntilNextRound = GameUtilities.CalculateTimeRemaining(data.nextRoundStartTime, data.serverTime);

        uiController.ShowNextRound(secondsUntilNextRound);
        uiController.UpdateRoundPhase("NEXTROUND");
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
    }

    internal void OnBalanceUpdated(double newBalance)
    {
        CurrentBalance = newBalance;
        uiController.UpdateBalance(CurrentBalance);
    }

    internal void OnHistoryReceived(List<HistoryEntry> history, HistoryMeta meta)
    {
        if (historyController != null)
        {
            historyController.UpdateHistoryData(history, meta);
        }
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
            {
                OnBalanceUpdated(response.payload.balance);
            }

            betController.OnBetActionResponse(response);
        }
        else
        {
            // IMPORTANT: All bet action failures are GAME NOTIFICATIONS, not errors
            // They should use ShowInGamePopup(), NOT ShowErrorPopup()
            //
            // ShowInGamePopup = Game notifications (auto-closes after 1 second)
            // ShowErrorPopup = Connection/system errors (requires user to click OK)

            string errorMsg = response.payload?.message ?? "Bet action failed";

            if (errorMsg == "Limit reached")
            {
                // Specific handling for limit - BetController shows detailed message
                betController.OnBetLimitReached();
            }
            else if (errorMsg.Contains("Insufficient"))
            {
                // Insufficient balance notification
                uiController.ShowInGamePopup("Insufficient balance");
            }
            else if (errorMsg.Contains("not active") || errorMsg.Contains("locked"))
            {
                // Betting phase ended notification
                uiController.ShowInGamePopup("Betting is locked");
            }
            else
            {
                // Any other bet-related message from server
                uiController.ShowInGamePopup(errorMsg);
            }

            betController.OnBetActionResponse(null);
        }
    }
    #endregion

    #region Public API - Room Management
    internal void JoinRoom(string roomName)
    {
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
    }

    internal void LeaveRoom()
    {
        betController.DisableBetting();
        betController.ClearAllBets();
        betController.ClearAllWinHighlights();
        roundController.ClearRoundDisplay();
        socketManager.ReturnHome();
        uiController.ShowHomeScreen();
        uiController.HideAllTimers();
        CurrentRoom = null;
    }
    #endregion

    #region Public API - Betting Actions
    internal void PlaceBet(string betOption, int chipIndex)
    {
        if (string.IsNullOrEmpty(CurrentRoom)) return;

        string betType = GetBetType(betOption);

        socketManager.PlaceBet(betType, betOption, chipIndex, CurrentRoom);
    }

    internal void UndoBet()
    {
        socketManager.UndoBet();
    }

    internal void CancelAllBets()
    {
        socketManager.CancelBet();
    }

    internal void DoubleBet()
    {
        socketManager.DoubleBet(CurrentRoom);
    }

    internal void RepeatBet()
    {
        socketManager.RepeatBet();
    }
    #endregion

    #region Public API - History and Exit
    internal void RequestHistory(int page)
    {
        socketManager.RequestHistory(page);
    }

    internal void ExitGame()
    {
        StartCoroutine(socketManager.CloseSocket());
    }
    #endregion

    #region Public API - NEW: Bet Limit Query
    internal double GetMaxBetForBetOption(string betOption)
    {
        if (string.IsNullOrEmpty(CurrentRoom) || CurrentWagers == null) return 0;

        BetWager wager = GetWagerForBetOption(betOption);
        if (wager != null)
        {
            return wager.GetMaxBet(CurrentRoom);
        }

        return 0;
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

        if (betOption.StartsWith("sum_"))
        {
            int sumValue = int.Parse(betOption.Substring(4));
            return sumValue switch
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
    private List<double> GetChipValuesForRoom(string roomName)
    {
        if (socketManager.InitialData?.bets == null)
        {
            return new List<double>();
        }

        return roomName switch
        {
            "casual" => socketManager.InitialData.bets.casual,
            "novice" => GameUtilities.ConvertToDoubleList(socketManager.InitialData.bets.novice),
            "expert" => GameUtilities.ConvertToDoubleList(socketManager.InitialData.bets.expert),
            "high_roller" => GameUtilities.ConvertToDoubleList(socketManager.InitialData.bets.high_roller),
            _ => new List<double>()
        };
    }

    private string GetBetType(string betOption)
    {
        if (betOption == "small" || betOption == "big" || betOption == "odd" || betOption == "even")
        {
            return "main_bets";
        }

        if (betOption.StartsWith("single_") ||
            betOption == "specific_2" ||
            betOption.StartsWith("specific_3_"))
        {
            return "side_bets";
        }

        if (betOption.StartsWith("sum_"))
        {
            return "op_bets";
        }

        return "main_bets";
    }


    #endregion
}