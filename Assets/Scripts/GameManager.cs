using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FIXED GameManager - Properly handles bet_placed broadcasts for current player during bet actions
/// 
/// KEY FIX: OnBetPlaced() now forwards ALL broadcasts to BetController.OnBetPlacedBroadcast()
/// This allows REPEAT/UNDO/CANCEL/DOUBLE to process each broadcast individually
/// 
/// CHANGE LOG:
/// - Line 227-240: Added check for bet action broadcasts from current player
/// - Now calls betController.OnBetPlacedBroadcast() for own player during bet actions
/// - Still calls ShowOtherPlayerBet() only for other players with positive amounts
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

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showRequestData = true;
    [SerializeField] private bool showResponseData = true;
    [SerializeField] private bool showBroadcastCycle = true;
    [SerializeField] private bool showPingPong = false;
    #endregion

    #region Public Properties
    internal string CurrentRoom { get; private set; }
    internal double CurrentBalance { get; private set; }
    internal string CurrentRoundId { get; private set; }
    internal bool EnableDebugLogs => enableDebugLogs;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        LogInfo("🎮 Game Manager Initialized");
    }
    #endregion

    #region Debug Logging
    internal void LogInfo(string message)
    {
        if (!enableDebugLogs) return;
        Debug.Log($"<color=cyan>[GAME]</color> {message}");
    }

    internal void LogSuccess(string message)
    {
        if (!enableDebugLogs) return;
        Debug.Log($"<color=green>[GAME]</color> {message}");
    }

    internal void LogWarning(string message)
    {
        if (!enableDebugLogs) return;
        Debug.LogWarning($"<color=yellow>[GAME]</color> {message}");
    }

    internal void LogError(string message)
    {
        if (!enableDebugLogs) return;
        Debug.LogError($"<color=red>[GAME]</color> {message}");
    }

    internal void LogRequest(string action, object payload)
    {
        if (!enableDebugLogs || !showRequestData) return;
        string json = payload != null ? Newtonsoft.Json.JsonConvert.SerializeObject(payload, Newtonsoft.Json.Formatting.Indented) : "{}";
        Debug.Log($"<color=magenta>[REQUEST]</color> {action}\n{json}");
    }

    internal void LogResponse(string event_name, string data)
    {
        if (!enableDebugLogs || !showResponseData) return;
        string formatted = data;
        try
        {
            var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject(data);
            formatted = Newtonsoft.Json.JsonConvert.SerializeObject(parsed, Newtonsoft.Json.Formatting.Indented);
        }
        catch { }

        Debug.Log($"<color=lime>[RESPONSE]</color> {event_name}\n{formatted}");
    }

    internal void LogBroadcast(string phase, string details = "")
    {
        if (!enableDebugLogs || !showBroadcastCycle) return;
        string detailsStr = string.IsNullOrEmpty(details) ? "" : $" - {details}";
        Debug.Log($"<color=orange>[BROADCAST]</color> {phase}{detailsStr}");
    }

    internal void LogPingPong(string message)
    {
        if (!enableDebugLogs || !showPingPong) return;
        Debug.Log($"<color=grey>[PING-PONG]</color> {message}");
    }
    #endregion

    #region Socket Callbacks - Init
    internal void OnInitDataReceived()
    {
        if (socketManager.InitialData == null || socketManager.PlayerData == null)
        {
            LogError("Init data is null");
            return;
        }

        LogSuccess($"✅ Init received - Player: {socketManager.PlayerData.username}, Balance: {socketManager.PlayerData.balance:F2}");

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
        LogInfo("Data refreshed from server");
    }
    #endregion

    #region Socket Callbacks - Room
    internal void OnRoomJoinedWithData(RoomPayload payload)
    {
        if (payload == null) return;

        LogSuccess($"🚪 Room joined: {payload.level}, Players: {payload.playerCount}");

        uiController.UpdatePlayerCount(payload.playerCount);

        if (payload.leaderboards != null)
        {
            uiController.UpdateLeaderboards(payload.leaderboards);
        }

        if (payload.roundState == null)
        {
            LogBroadcast("WAITING", "Waiting for next round to start");
            uiController.UpdateRoundPhase("WAITING");
        }
        else
        {
            LogBroadcast("IN_PROGRESS", $"Round in progress: {payload.roundState.phase}");
        }
    }
    #endregion

    #region Socket Callbacks - Round Events
    internal void OnRoundStart(RoundStartData data)
    {
        if (data == null) return;

        CurrentRoundId = data.roundId;

        int timeRemaining = CalculateTimeRemaining(data.bettingEndTime, data.serverTime);

        LogBroadcast("ROUND_START", $"Round: {data.roundId}, Betting Time: {timeRemaining}s, Players: {data.playerCount}");
        LogInfo($"📊 Timestamps - Start: {data.startedAt}, End: {data.bettingEndTime}, Server: {data.serverTime}");

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

        LogBroadcast("TIMER_SYNC", $"Round: {data.roundId}, Time: {timeRemaining}s");

        roundController.UpdateTimer(timeRemaining);
        uiController.UpdateTimer(timeRemaining);
    }

    internal void OnBonus(BonusData data)
    {
        if (data == null) return;

        LogBroadcast("BONUS", $"Player {data.bonusPlayer} got x{data.bonusMultiplier} multiplier");
        uiController.ShowBonusNotification(data.bonusPlayer, data.bonusMultiplier);
    }

    internal void OnDiceResult(DiceResultData data)
    {
        if (data == null) return;

        LogBroadcast("DICE_RESULT", $"🎲 [{data.dice1}, {data.dice2}, {data.dice3}] = {data.sum} ({data.matchSide.ToUpper()})");

        betController.DisableBetting();

        uiController.ShowBetLocked();
        uiController.UpdateRoundPhase("RESULT");

        roundController.ShowDiceResult(data);

        betController.HighlightWinningAreas(data.matchSide, data.sum);
        betController.HighlightTripleDiceResult(data.dice1, data.dice2, data.dice3);
    }

    /// <summary>
    /// CRITICAL FIX: Handle ALL bet_placed broadcasts - both from current player and others
    /// 
    /// BEFORE: Only processed broadcasts from other players
    /// AFTER: Also processes own player's broadcasts during bet actions (REPEAT/UNDO/CANCEL/DOUBLE)
    /// </summary>
    internal void OnBetPlaced(BetPlacedData data)
    {
        if (data == null) return;

        bool isOwnPlayer = (data.username == socketManager.PlayerData.username);

        if (isOwnPlayer)
        {
            // CRITICAL: Forward OWN broadcasts to BetController for bet action processing
            // This allows REPEAT/UNDO/CANCEL/DOUBLE to handle each broadcast individually
            LogInfo($"🎯 Own bet broadcast: {data.betOption} amount={data.amount:F2} (action in progress)");
            betController.OnBetPlacedBroadcast(data);
        }
        else
        {
           /* // Show other player bets (only positive amounts)
            if (data.amount > 0)
            {
                LogInfo($"👤 Other player bet: {data.username} on {data.betOption} = {data.amount:F2}");
                betController.ShowOtherPlayerBet(data);
            }*/
        }
    }

    internal void OnCashout(CashoutData data)
    {
        if (data == null) return;

        LogBroadcast("CASHOUT", "💰 Processing payouts");

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
                        LogSuccess($"🎉 WON: +{payout.win:F2}, New Balance: {CurrentBalance:F2}");
                        uiController.ShowWinAnimation(payout.win);
                    }
                    else
                    {
                        LogInfo($"❌ LOST - Balance: {CurrentBalance:F2}");
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

        LogBroadcast("ROUND_END", $"⏱️ Next round in {secondsUntilNextRound}s");
        LogInfo($"Cashout interval: {data.cashoutInterval}ms");
            
        uiController.ShowNextRound(secondsUntilNextRound);
        uiController.UpdateRoundPhase("NEXTROUND");
        betController.OnRoundEnd();
    }

    internal void OnLobbyCount(LobbyCountData data)
    {
        if (data?.lobby == null) return;

        LogBroadcast("LOBBY_UPDATE", $"Casual: {data.lobby.casual}, Novice: {data.lobby.novice}, Expert: {data.lobby.expert}, High: {data.lobby.high_roller}");

        uiController.UpdateLobbyPlayerCounts(
            data.lobby.casual,
            data.lobby.novice,
            data.lobby.expert,
            data.lobby.high_roller
        );
    }

    internal void OnBalanceUpdated(double newBalance)
    {
        LogInfo($"💵 Balance updated: {CurrentBalance:F2} → {newBalance:F2}");
        CurrentBalance = newBalance;
        uiController.UpdateBalance(CurrentBalance);
    }

    internal void OnHistoryReceived(List<HistoryEntry> history, HistoryMeta meta)
    {
        if (historyController != null)
        {
            LogInfo($"📜 History received: Page {meta.page}/{meta.pages}, Entries: {history.Count}");
            historyController.UpdateHistoryData(history, meta);
        }
    }

    /// <summary>
    /// Handle bet action responses (undo, cancel, double, repeat)
    /// Forwards server response to BetController for UI sync
    /// </summary>
    internal void OnBetActionResponse(BetAckResponse response)
    {
        if (response == null)
        {
            // Null response means reset processing flag
            betController.OnBetActionResponse(null);
            return;
        }

        if (response.success)
        {
            LogSuccess($"✅ Bet action success: {response.payload?.message}");

            // Update balance
            if (response.payload != null)
            {
                OnBalanceUpdated(response.payload.balance);
            }

            // Update bet UI to match server state
            betController.OnBetActionResponse(response);
        }
        else
        {
            string errorMsg = response.payload?.message ?? "Bet action failed";
            LogError($"❌ Bet action failed: {errorMsg}");
            uiController.ShowErrorPopup(errorMsg);

            // Reset processing flag
            betController.OnBetActionResponse(null);
        }
    }
    #endregion

    #region Public API - Called by UI
    internal void JoinRoom(string roomName)
    {
        LogRequest("JOIN_ROOM", new { level = roomName });

        CurrentRoom = roomName;

        List<double> chipValues = GetChipValuesForRoom(roomName);
        Wagers wagers = socketManager.InitialData?.wagers;

        betController.SetupChips(chipValues, wagers, roomName);
        socketManager.JoinLevel(roomName);
        uiController.ShowGameScreen();
    }

    internal void LeaveRoom()
    {
        LogRequest("LEAVE_ROOM", null);

        betController.DisableBetting();
        betController.ClearAllBets();
        betController.ClearAllWinHighlights();
        roundController.ClearRoundDisplay();
        socketManager.ReturnHome();
        uiController.ShowHomeScreen();
        uiController.HideAllTimers();
        CurrentRoom = null;
    }

    internal void PlaceBet(string betOption, int chipIndex)
    {
        if (string.IsNullOrEmpty(CurrentRoom)) return;

        string betType = GetBetType(betOption);

        LogRequest("PLACE_BET", new { betType, betOption, chipIndex, room = CurrentRoom });

        socketManager.PlaceBet(betType, betOption, chipIndex, CurrentRoom);
    }

    internal void UndoBet()
    {
        LogRequest("UNDO_BET", null);
        socketManager.UndoBet();
    }

    internal void CancelAllBets()
    {
        LogRequest("CANCEL_BET", null);
        socketManager.CancelBet();
    }

    internal void DoubleBet()
    {
        LogRequest("DOUBLE_BET", null);
        socketManager.DoubleBet(CurrentRoom);
    }

    internal void RepeatBet()
    {
        LogRequest("REPEAT_BET", null);
        socketManager.RepeatBet();
    }

    internal void RequestHistory(int page)
    {
        LogRequest("BET_HISTORY", new { page });
        socketManager.RequestHistory(page);
    }

    internal void ExitGame()
    {
        LogInfo("Exiting game");
        StartCoroutine(socketManager.CloseSocket());
    }
    #endregion

    #region Private Helpers
    /// <summary>
    /// Calculate time remaining with proper rounding
    /// </summary>
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

        if (betOption.StartsWith("single_") || betOption == "specific_2" || betOption == "specific_3")
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