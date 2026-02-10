using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    #region Serialized References
    [Header("Controllers")]
    [SerializeField] private UIController uiController;
    [SerializeField] private BetController betController;
    [SerializeField] private RoundController roundController;
    [SerializeField] private HistoryController historyController;

    [Header("Socket")]
    [SerializeField] private SocketIOManager socketManager;
    #endregion

    #region Public Properties
    internal string CurrentRoom { get; private set; }
    internal double CurrentBalance { get; private set; }
    internal string CurrentRoundId { get; private set; }
    #endregion

    #region Private Fields
    // NEW: Track last attempted bet for error handling
    private string lastAttemptedBetOption = "";
    #endregion

    #region Socket Callbacks - Init
    internal void OnInitDataReceived()
    {
        if (socketManager.InitialData == null || socketManager.PlayerData == null) return;

        CurrentBalance = socketManager.PlayerData.balance;

        uiController.SetupInitialData(
            socketManager.PlayerData.username,
            CurrentBalance,
            socketManager.InitialData.leaderboards,
            socketManager.InitialData.wagers,
            socketManager.InitialData.bets
        );

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

        if (payload.leaderboards != null)
        {
            uiController.UpdateLeaderboards(payload.leaderboards);
        }

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

        int timeRemaining = CalculateTimeRemaining(data.bettingEndTime, data.serverTime);

        uiController.UpdatePlayerCount(data.playerCount);
        uiController.ShowBettingPhase(timeRemaining);
        uiController.UpdateRoundPhase("BETTING");

        roundController.StartRound(data);
        betController.OnRoundStart();
        betController.EnableBetting();
    }

    internal void OnBettingTimer(TimerData data)
    {
        if (data == null) return;

        int timeRemaining = CalculateTimeRemaining(data.bettingEndTime, data.serverTime);

        roundController.UpdateTimer(timeRemaining);
        uiController.UpdateTimer(timeRemaining);
    }

    internal void OnBonus(BonusData data)
    {
        if (data == null) return;

        uiController.ShowBonusNotification(data.bonusPlayer, data.bonusMultiplier);
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
                    }
                }
            }
        }

        betController.ClearAllBets();
    }

    internal void OnRoundEnd(RoundEndPayload data)
    {
        if (data == null) return;

        int secondsUntilNextRound = CalculateTimeRemaining(data.nextRoundStartTime, data.serverTime);

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
            print("Historyy...................");
        }
    }

    /// <summary>
    /// UPDATED: Handle bet action response from server
    /// Shows "Limit reached" error with max bet info when server rejects bet
    /// </summary>
    internal void OnBetActionResponse(BetAckResponse response)
    {
        if (response == null)
        {
            betController.OnBetActionResponse(null);
            return;
        }

        if (response.success)
        {
            // Successful bet placement
            if (response.payload != null)
            {
                OnBalanceUpdated(response.payload.balance);
            }

            betController.OnBetActionResponse(response);

            Debug.Log($"[GameManager] Bet placed successfully: {response.payload?.amount}");
        }
        else
        {
            // Bet failed - handle different error types
            string errorMsg = response.payload?.message ?? "Bet action failed";

            // Check if this is a "Limit reached" error
            if (errorMsg.Contains("Limit reached") || errorMsg.Contains("limit"))
            {
                // Get max limit for this bet option
                double maxLimit = GetMaxBetLimitForArea(lastAttemptedBetOption);

                // Show detailed error message
                if (maxLimit > 0)
                {
                    string detailedMessage = $"Maximum bet limit for this area is {FormatAmount(maxLimit)}";
                    uiController.ShowErrorPopup(detailedMessage);
                    Debug.LogWarning($"[GameManager] Bet limit reached for {lastAttemptedBetOption}: max = {maxLimit}");
                }
                else
                {
                    // Fallback if we can't determine max limit
                    uiController.ShowErrorPopup("Bet limit reached for this area");
                    Debug.LogWarning($"[GameManager] Bet limit reached (max unknown)");
                }
            }
            else
            {
                // Other errors - show server message
                uiController.ShowErrorPopup(errorMsg);
                Debug.LogWarning($"[GameManager] Bet failed: {errorMsg}");
            }

            // Rollback optimistic bet on client
            betController.OnBetActionResponse(null);
        }
    }
    #endregion

    #region Public API - Called by UI
    internal void JoinRoom(string roomName)
    {
        CurrentRoom = roomName;

        List<double> chipValues = GetChipValuesForRoom(roomName);
        Wagers wagers = socketManager.InitialData?.wagers;

        betController.SetupChips(chipValues, wagers, roomName);
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

    /// <summary>
    /// UPDATED: Place bet and track the bet option
    /// </summary>
    internal void PlaceBet(string betOption, int chipIndex)
    {
        if (string.IsNullOrEmpty(CurrentRoom)) return;

        // Track last attempted bet for error handling
        lastAttemptedBetOption = betOption;

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

    internal void RequestHistory(int page)
    {
        socketManager.RequestHistory(page);
    }

    internal void ExitGame()
    {
        StartCoroutine(socketManager.CloseSocket());
    }
    #endregion

    #region Private Helpers
    private int CalculateTimeRemaining(long endTime, long serverTime)
    {
        long remainingMs = endTime - serverTime;
        float remainingSeconds = remainingMs / 1000f;
        return Mathf.Max(0, Mathf.RoundToInt(remainingSeconds));
    }

    private List<double> GetChipValuesForRoom(string roomName)
    {
        if (socketManager.InitialData?.bets == null)
        {
            return new List<double>();
        }

        return roomName switch
        {
            "casual" => socketManager.InitialData.bets.casual,
            "novice" => ConvertToDoubleList(socketManager.InitialData.bets.novice),
            "expert" => ConvertToDoubleList(socketManager.InitialData.bets.expert),
            "high_roller" => ConvertToDoubleList(socketManager.InitialData.bets.high_roller),
            _ => new List<double>()
        };
    }

    private List<double> ConvertToDoubleList(List<int> intList)
    {
        List<double> result = new List<double>();
        if (intList != null)
        {
            foreach (int value in intList)
            {
                result.Add((double)value);
            }
        }
        return result;
    }

    private string GetBetType(string betOption)
    {
        if (betOption == "small" || betOption == "big" || betOption == "odd" || betOption == "even")
        {
            return "main_bets";
        }

        // Handle specific_3_X (specific_3_1, specific_3_2, etc.)
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

    /// <summary>
    /// NEW: Get maximum bet limit for a specific bet area
    /// Returns the max limit from wager data
    /// </summary>
    private double GetMaxBetLimitForArea(string betOption)
    {
        if (socketManager?.InitialData?.wagers == null || string.IsNullOrEmpty(CurrentRoom))
        {
            return 0;
        }

        Wagers wagers = socketManager.InitialData.wagers;
        BetWager wager = null;

        // Main bets
        if (betOption == "small")
            wager = wagers.main_bets?.small;
        else if (betOption == "big")
            wager = wagers.main_bets?.big;
        else if (betOption == "odd")
            wager = wagers.main_bets?.odd;
        else if (betOption == "even")
            wager = wagers.main_bets?.even;

        // Side bets - single dice
        else if (betOption.StartsWith("single_"))
            wager = wagers.side_bets?.single_match_1;

        // Side bets - triple dice
        else if (betOption.StartsWith("specific_3_"))
            wager = wagers.side_bets?.specific_3;

        // Side bets - double dice
        else if (betOption == "specific_2")
            wager = wagers.side_bets?.specific_2;

        // Op bets - sum
        else if (betOption.StartsWith("sum_"))
        {
            wager = GetSumWager(betOption, wagers);
        }

        if (wager != null)
        {
            return wager.GetMaxBet(CurrentRoom);
        }

        return 0;
    }

    /// <summary>
    /// NEW: Get wager for sum bets
    /// </summary>
    private BetWager GetSumWager(string betOption, Wagers wagers)
    {
        if (!int.TryParse(betOption.Replace("sum_", ""), out int sum))
        {
            return null;
        }

        return sum switch
        {
            4 => wagers.op_bets?.sum_4,
            5 => wagers.op_bets?.sum_5,
            6 => wagers.op_bets?.sum_6,
            7 => wagers.op_bets?.sum_7,
            8 => wagers.op_bets?.sum_8,
            9 => wagers.op_bets?.sum_9,
            10 => wagers.op_bets?.sum_10,
            11 => wagers.op_bets?.sum_11,
            12 => wagers.op_bets?.sum_12,
            13 => wagers.op_bets?.sum_13,
            14 => wagers.op_bets?.sum_14,
            15 => wagers.op_bets?.sum_15,
            16 => wagers.op_bets?.sum_16,
            17 => wagers.op_bets?.sum_17,
            _ => null
        };
    }

    /// <summary>
    /// NEW: Format amount for display
    /// </summary>
    private string FormatAmount(double amount)
    {
        if (amount >= 1000)
        {
            return $"{(amount / 1000):F1}K";
        }

        if (amount < 1)
        {
            return amount.ToString("F2");
        }

        if (amount % 1 != 0)
        {
            return amount.ToString("F1");
        }

        return amount.ToString("F0");
    }
    #endregion
}