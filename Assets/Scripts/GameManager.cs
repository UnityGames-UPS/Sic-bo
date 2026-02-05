using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FIXED: Main game controller with enhanced debugging
/// - Added detailed debug logs matching mock format
/// - Fixed timer calculation
/// - Proper betting enable/disable flow
/// - All handlers properly route data to controllers
/// </summary>
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

    #region Unity Lifecycle
    private void Start()
    {
        Debug.Log("[GAME] Manager started");
    }
    #endregion

    #region Socket Callbacks - Init
    internal void OnInitDataReceived()
    {
        if (socketManager.InitialData == null || socketManager.PlayerData == null)
        {
            Debug.LogError("[GAME] Init data is null");
            return;
        }

        Debug.Log("[GAME] Initializing with data");

        // Store player data
        CurrentBalance = socketManager.PlayerData.balance;

        // Initialize UI with home screen
        uiController.SetupInitialData(
            socketManager.PlayerData.username,
            CurrentBalance,
            socketManager.InitialData.leaderboards
        );

        // Setup lobby counts if available
        if (socketManager.InitialData.lobby != null)
        {
            uiController.UpdateLobbyPlayerCounts(
                socketManager.InitialData.lobby.casual,
                socketManager.InitialData.lobby.novice,
                socketManager.InitialData.lobby.expert,
                socketManager.InitialData.lobby.high_roller
            );
        }

        // Show home screen
        uiController.ShowHomeScreen();
    }

    internal void OnDataRefreshed()
    {
        // Handle data refresh if needed
        Debug.Log("[GAME] Data refreshed");
    }
    #endregion

    #region Socket Callbacks - Room
    /// <summary>
    /// ✅ CRITICAL FIX: Handle JOIN_LEVEL response with player count and round state
    /// Called by SocketManager after receiving "request" response to JOIN_LEVEL
    /// </summary>
    internal void OnRoomJoinedWithData(RoomPayload payload)
    {
        if (payload == null)
        {
            Debug.LogWarning("[GAME] OnRoomJoinedWithData: null payload");
            return;
        }

        Debug.Log($"[GAME] Room joined: {payload.level}, players: {payload.playerCount}");

        // ✅ FIX: Update player count immediately
        uiController.UpdatePlayerCount(payload.playerCount);

        // ✅ FIX: Update leaderboards if available
        if (payload.leaderboards != null)
        {
            uiController.UpdateLeaderboards(payload.leaderboards);
        }

        // ✅ FIX: Check if round state exists (joining mid-round)
        // Note: Round start will be triggered by SocketManager if roundState exists
        if (payload.roundState == null)
        {
            Debug.Log("[GAME] Waiting for round to start...");
            // Show waiting state
            uiController.UpdateRoundPhase("WAITING");
        }
    }
    #endregion

    #region Socket Callbacks - Round Events
    /// <summary>
    /// ✅ CRITICAL FIX: Enable betting when round starts
    /// </summary>
    internal void OnRoundStart(RoundStartData data)
    {
        if (data == null)
        {
            Debug.LogWarning("[GAME] OnRoundStart: null data");
            return;
        }

        Debug.Log($"[GAME] Round started: {data.roundId}");
        Debug.Log($"[GAME] StartedAt: {data.startedAt}, BettingEndTime: {data.bettingEndTime}, ServerTime: {data.serverTime}");

        CurrentRoundId = data.roundId;

        // Update UI
        uiController.UpdatePlayerCount(data.playerCount);

        // ✅ FIX: Calculate initial time remaining from server timestamps
        int timeRemaining = Mathf.Max(0, (int)((data.bettingEndTime - data.serverTime) / 1000));

        Debug.Log($"[GAME] Time remaining: {timeRemaining}s (calculated from {data.bettingEndTime} - {data.serverTime} = {data.bettingEndTime - data.serverTime}ms)");

        // Show betting phase
        uiController.ShowBettingPhase(timeRemaining);

        // Start round controller
        roundController.StartRound(data);

        // ✅ CRITICAL FIX: Enable betting here!
        betController.EnableBetting();

        Debug.Log("[GAME] Betting enabled");
    }

    /// <summary>
    /// ✅ FIX: Use timeRemaining from server directly
    /// </summary>
    internal void OnBettingTimer(TimerData data)
    {
        if (data == null) return;

        // ✅ FIX: Calculate time remaining from server timestamps
        int timeRemaining = Mathf.Max(0, (int)((data.bettingEndTime - data.serverTime) / 1000));

        Debug.Log($"[GAME] Timer update: {timeRemaining}s remaining (from {data.serverTime})");

        // Update controllers
        roundController.UpdateTimer(timeRemaining);
        uiController.UpdateTimer(timeRemaining);
    }

    internal void OnBonus(BonusData data)
    {
        if (data == null) return;

        Debug.Log($"[GAME] Bonus: {data.bonusPlayer} x{data.bonusMultiplier}");
        uiController.ShowBonusNotification(data.bonusPlayer, data.bonusMultiplier);
    }

    internal void OnDiceResult(DiceResultData data)
    {
        if (data == null) return;

        Debug.Log($"[GAME] Dice: {data.dice1}, {data.dice2}, {data.dice3} = {data.sum} ({data.matchSide})");

        // ✅ FIX: Disable betting when dice result comes
        betController.DisableBetting();

        // Show bet locked state
        uiController.ShowBetLocked();

        // Show dice animation
        roundController.ShowDiceResult(data);

        // Highlight winning areas
        betController.HighlightWinningAreas(data.matchSide, data.sum);

        // Highlight triple dice results if applicable
        betController.HighlightTripleDiceResult(data.dice1, data.dice2, data.dice3);
    }

    internal void OnBetPlaced(BetPlacedData data)
    {
        if (data == null) return;

        // Show other players' chips (not your own - that's handled in OnBalanceUpdated)
        if (data.username != socketManager.PlayerData.username)
        {
            Debug.Log($"[GAME] Other player bet: {data.username} on {data.betOption} = {data.amount}");
            betController.ShowOtherPlayerBet(data);
        }
    }

    internal void OnCashout(CashoutData data)
    {
        if (data == null) return;

        Debug.Log("[GAME] Cashout received");

        // Update leaderboards
        if (data.leaderboards != null)
        {
            uiController.UpdateLeaderboards(data.leaderboards);
        }

        // Process payouts
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
                        Debug.Log($"[GAME] Won: {payout.win}");
                        uiController.ShowWinAnimation(payout.win);
                    }
                    else
                    {
                        Debug.Log($"[GAME] Lost bet");
                    }
                }
            }
        }

        // ✅ CORRECTED: Clear bets after cashout
        // Server will send next round_start after ~5 seconds
        betController.ClearAllBets();
        roundController.EndRound();

        // ✅ NEW: Hide timer during gap between rounds
        uiController.HideAllTimers();

        Debug.Log("[GAME] Waiting for next round...");
    }
    internal void OnLobbyCount(LobbyCountData data)
    {
        if (data?.lobby == null) return;

        Debug.Log($"[GAME] Lobby update: Casual={data.lobby.casual}, Novice={data.lobby.novice}, Expert={data.lobby.expert}, HighRoller={data.lobby.high_roller}");

        uiController.UpdateLobbyPlayerCounts(
            data.lobby.casual,
            data.lobby.novice,
            data.lobby.expert,
            data.lobby.high_roller
        );
    }

    internal void OnRoundEnd(RoundEndData data)
    {
        if (data == null) return;

        Debug.Log($"[GAME] Round ended: {data.roundId}");

        // Clear bets and prepare for next round
        betController.ClearAllBets();
        roundController.EndRound();

        // Show next round countdown
        uiController.ShowNextRound(5);
    }

    /// <summary>
    /// ✅ NEW: Handle balance updates from bet confirmations
    /// </summary>
    internal void OnBalanceUpdated(double newBalance)
    {
        Debug.Log($"[GAME] Balance updated: {CurrentBalance} -> {newBalance}");
        CurrentBalance = newBalance;
        uiController.UpdateBalance(CurrentBalance);
    }

    /// <summary>
    /// ✅ NEW: Handle history responses
    /// </summary>
    internal void OnHistoryReceived(List<HistoryEntry> history, HistoryMeta meta)
    {
        if (historyController != null)
        {
            Debug.Log($"[GAME] History received: {history.Count} entries, page {meta.page}/{meta.pages}");
            historyController.UpdateHistoryData(history, meta);
        }
    }
    #endregion

    #region Public API - Called by UI
    internal void JoinRoom(string roomName)
    {
        Debug.Log($"[GAME] Joining room: {roomName}");

        CurrentRoom = roomName;

        // Get chip values for this room
        List<double> chipValues = GetChipValuesForRoom(roomName);

        // Get wager data for win ratios
        Wagers wagers = socketManager.InitialData?.wagers;

        // Setup chips with win ratio data
        betController.SetupChips(chipValues, wagers);

        // Request join from server
        socketManager.JoinLevel(roomName);

        // Show game screen
        uiController.ShowGameScreen();
    }

    internal void LeaveRoom()
    {
        Debug.Log("[GAME] Leaving room");

        // Disable betting
        betController.DisableBetting();

        // Clear all bets
        betController.ClearAllBets();

        // Return to home
        socketManager.ReturnHome();

        // Show home screen
        uiController.ShowHomeScreen();
        uiController.HideAllTimers();

        CurrentRoom = null;
    }

    internal void PlaceBet(string betOption, int chipIndex)
    {
        if (string.IsNullOrEmpty(CurrentRoom))
        {
            Debug.LogWarning("[GAME] No room joined");
            return;
        }

        string betType = GetBetType(betOption);
        socketManager.PlaceBet(betType, betOption, chipIndex, CurrentRoom);

        Debug.Log($"[GAME] Bet placed: {betOption} with chip {chipIndex} in room {CurrentRoom}");
    }

    internal void UndoBet()
    {
        Debug.Log("[GAME] Undo bet");
        socketManager.UndoBet();
    }

    internal void CancelAllBets()
    {
        Debug.Log("[GAME] Cancel all bets");
        socketManager.CancelBet();
    }

    internal void DoubleBet()
    {
        Debug.Log("[GAME] Double bet");
        socketManager.DoubleBet("");
    }

    internal void RepeatBet()
    {
        Debug.Log("[GAME] Repeat bet");
        socketManager.RepeatBet();
    }

    internal void RequestHistory(int page)
    {
        Debug.Log($"[GAME] Request history page {page}");
        socketManager.RequestHistory(page);
    }

    internal void ExitGame()
    {
        Debug.Log("[GAME] Exit game");
        StartCoroutine(socketManager.CloseSocket());
    }
    #endregion

    #region Private Helpers
    private List<double> GetChipValuesForRoom(string roomName)
    {
        if (socketManager.InitialData?.bets == null)
        {
            Debug.LogWarning("[GAME] No bet data available");
            return new List<double>();
        }

        List<double> chipValues = roomName switch
        {
            "casual" => socketManager.InitialData.bets.casual,
            "novice" => ConvertToDoubleList(socketManager.InitialData.bets.novice),
            "expert" => ConvertToDoubleList(socketManager.InitialData.bets.expert),
            "high_roller" => ConvertToDoubleList(socketManager.InitialData.bets.high_roller),
            _ => new List<double>()
        };

        Debug.Log($"[GAME] Chip values for {roomName}: {string.Join(", ", chipValues)}");
        return chipValues;
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
        // Main bets: small, big, odd, even
        if (betOption == "small" || betOption == "big" || betOption == "odd" || betOption == "even")
        {
            Debug.Log($"[GAME] Bet type: main_bets for {betOption}");
            return "main_bets";
        }

        // Side bets: single_1 to single_6, specific_2, specific_3
        if (betOption.StartsWith("single_") || betOption.StartsWith("specific_"))
        {
            Debug.Log($"[GAME] Bet type: side_bets for {betOption}");
            return "side_bets";
        }

        // Sum bets: sum_4 to sum_17
        if (betOption.StartsWith("sum_"))
        {
            Debug.Log($"[GAME] Bet type: op_bets for {betOption}");
            return "op_bets";
        }

        Debug.Log($"[GAME] Bet type: main_bets (default) for {betOption}");
        return "main_bets";
    }
    #endregion
}