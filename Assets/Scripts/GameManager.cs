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

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
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
        LogInfo("Manager started");
    }
    #endregion

    #region Debug Logging
    private void LogInfo(string message)
    {
        if (!enableDebugLogs) return;
        Debug.Log($"<color=cyan>[GAME]</color> {message}");
    }

    private void LogSuccess(string message)
    {
        if (!enableDebugLogs) return;
        Debug.Log($"<color=green>[GAME]</color> {message}");
    }

    private void LogWarning(string message)
    {
        if (!enableDebugLogs) return;
        Debug.LogWarning($"<color=yellow>[GAME]</color> {message}");
    }

    private void LogError(string message)
    {
        if (!enableDebugLogs) return;
        Debug.LogError($"<color=red>[GAME]</color> {message}");
    }

    private void LogRequest(string action, object payload)
    {
        if (!enableDebugLogs) return;
        string json = payload != null ? JsonUtility.ToJson(payload) : "{}";
        Debug.Log($"<color=magenta>[REQUEST]</color> {action}: {json}");
    }

    private void LogResponse(string event_name, string data)
    {
        if (!enableDebugLogs) return;
        Debug.Log($"<color=lime>[RESPONSE]</color> {event_name}: {data}");
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

        LogSuccess($"Init received - Player: {socketManager.PlayerData.username}, Balance: {socketManager.PlayerData.balance:F2}");

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
        LogInfo("Data refreshed");
    }
    #endregion

    #region Socket Callbacks - Room
    internal void OnRoomJoinedWithData(RoomPayload payload)
    {
        if (payload == null) return;

        LogResponse("room:joined", $"Room: {payload.level}, Players: {payload.playerCount}");

        uiController.UpdatePlayerCount(payload.playerCount);

        if (payload.leaderboards != null)
        {
            uiController.UpdateLeaderboards(payload.leaderboards);
        }

        if (payload.roundState == null)
        {
            LogInfo("Waiting for round to start");
            uiController.UpdateRoundPhase("WAITING");
        }
    }
    #endregion

    #region Socket Callbacks - Round Events
    internal void OnRoundStart(RoundStartData data)
    {
        if (data == null) return;

        CurrentRoundId = data.roundId;
        int timeRemaining = Mathf.Max(0, (int)((data.bettingEndTime - data.serverTime) / 1000));

        LogResponse("game:round_start", $"Round: {data.roundId}, Time: {timeRemaining}s, Players: {data.playerCount}");

        uiController.UpdatePlayerCount(data.playerCount);
        uiController.ShowBettingPhase(timeRemaining);
        roundController.StartRound(data);
        betController.EnableBetting();
    }

    internal void OnBettingTimer(TimerData data)
    {
        if (data == null) return;

        int timeRemaining = Mathf.Max(0, (int)((data.bettingEndTime - data.serverTime) / 1000));
        roundController.UpdateTimer(timeRemaining);
        uiController.UpdateTimer(timeRemaining);
    }

    internal void OnBonus(BonusData data)
    {
        if (data == null) return;
        LogResponse("game:bonus", $"Player: {data.bonusPlayer}, Multiplier: x{data.bonusMultiplier}");
        uiController.ShowBonusNotification(data.bonusPlayer, data.bonusMultiplier);
    }

    internal void OnDiceResult(DiceResultData data)
    {
        if (data == null) return;

        LogResponse("game:dice_result", $"Dice: [{data.dice1},{data.dice2},{data.dice3}] = {data.sum} ({data.matchSide})");

        betController.DisableBetting();
        uiController.ShowBetLocked();
        roundController.ShowDiceResult(data);
        betController.HighlightWinningAreas(data.matchSide, data.sum);
        betController.HighlightTripleDiceResult(data.dice1, data.dice2, data.dice3);
    }

    internal void OnBetPlaced(BetPlacedData data)
    {
        if (data == null) return;

        if (data.username != socketManager.PlayerData.username)
        {
            LogInfo($"Other player bet: {data.username} on {data.betOption} = {data.amount:F2}");
            betController.ShowOtherPlayerBet(data);
        }
    }

    internal void OnCashout(CashoutData data)
    {
        if (data == null) return;

        LogResponse("game:cashout", "Processing payouts");

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
                        LogSuccess($"Won: {payout.win:F2}, New Balance: {CurrentBalance:F2}");
                        uiController.ShowWinAnimation(payout.win);
                    }
                    else
                    {
                        LogInfo($"Lost, Balance: {CurrentBalance:F2}");
                    }
                }
            }
        }

        betController.ClearAllBets();
        roundController.EndRound();
    }

    internal void OnRoundEnd(RoundEndPayload data)
    {
        if (data == null) return;

        long serverTime = data.serverTime;
        long nextRoundStartTime = data.nextRoundStartTime;
        int secondsUntilNextRound = Mathf.Max(0, (int)((nextRoundStartTime - serverTime) / 1000));

        LogResponse("game:round_end", $"Next round in {secondsUntilNextRound}s");

        uiController.ShowNextRound(secondsUntilNextRound);
        uiController.UpdateRoundPhase("NEXTROUND");
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
        LogInfo($"Balance updated: {CurrentBalance:F2} → {newBalance:F2}");
        CurrentBalance = newBalance;
        uiController.UpdateBalance(CurrentBalance);
    }

    internal void OnHistoryReceived(List<HistoryEntry> history, HistoryMeta meta)
    {
        if (historyController != null)
        {
            LogResponse("history", $"Page {meta.page}/{meta.pages}, Entries: {history.Count}");
            historyController.UpdateHistoryData(history, meta);
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