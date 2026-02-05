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
        Debug.Log("[GAME] Data refreshed");
    }
    #endregion

    #region Socket Callbacks - Room
    internal void OnRoomJoinedWithData(RoomPayload payload)
    {
        if (payload == null)
        {
            Debug.LogWarning("[GAME] OnRoomJoinedWithData: null payload");
            return;
        }

        Debug.Log($"[GAME] Room joined: {payload.level}, players: {payload.playerCount}");

        uiController.UpdatePlayerCount(payload.playerCount);

        if (payload.leaderboards != null)
        {
            uiController.UpdateLeaderboards(payload.leaderboards);
        }

        if (payload.roundState == null)
        {
            Debug.Log("[GAME] Waiting for round to start...");
            uiController.UpdateRoundPhase("WAITING");
        }
    }
    #endregion

    #region Socket Callbacks - Round Events
    internal void OnRoundStart(RoundStartData data)
    {
        if (data == null)
        {
            Debug.LogWarning("[GAME] OnRoundStart: null data");
            return;
        }

        Debug.Log($"[GAME] Round started: {data.roundId}");

        CurrentRoundId = data.roundId;

        uiController.UpdatePlayerCount(data.playerCount);

        int timeRemaining = Mathf.Max(0, (int)((data.bettingEndTime - data.serverTime) / 1000));

        Debug.Log($"[GAME] Time remaining: {timeRemaining}s");

        uiController.ShowBettingPhase(timeRemaining);
        roundController.StartRound(data);
        betController.EnableBetting();

        Debug.Log("[GAME] Betting enabled");
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

        Debug.Log($"[GAME] Bonus: {data.bonusPlayer} x{data.bonusMultiplier}");
        uiController.ShowBonusNotification(data.bonusPlayer, data.bonusMultiplier);
    }

    internal void OnDiceResult(DiceResultData data)
    {
        if (data == null) return;

        Debug.Log($"[GAME] Dice: {data.dice1}, {data.dice2}, {data.dice3} = {data.sum} ({data.matchSide})");

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
            Debug.Log($"[GAME] Other player bet: {data.username} on {data.betOption} = {data.amount}");
            betController.ShowOtherPlayerBet(data);
        }
    }

    internal void OnCashout(CashoutData data)
    {
        if (data == null) return;

        Debug.Log("[GAME] Cashout received");

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

        betController.ClearAllBets();
        roundController.EndRound();
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

        betController.ClearAllBets();
        roundController.EndRound();
        uiController.ShowNextRound(5);
    }

    internal void OnBalanceUpdated(double newBalance)
    {
        Debug.Log($"[GAME] Balance updated: {CurrentBalance} -> {newBalance}");
        CurrentBalance = newBalance;
        uiController.UpdateBalance(CurrentBalance);
    }

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

        List<double> chipValues = GetChipValuesForRoom(roomName);
        Wagers wagers = socketManager.InitialData?.wagers;

        betController.SetupChips(chipValues, wagers, roomName);

        socketManager.JoinLevel(roomName);

        uiController.ShowGameScreen();
    }

    internal void LeaveRoom()
    {
        Debug.Log("[GAME] Leaving room");

        betController.DisableBetting();
        betController.ClearAllBets();

        socketManager.ReturnHome();

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
        socketManager.DoubleBet(CurrentRoom);
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
        if (betOption == "small" || betOption == "big" || betOption == "odd" || betOption == "even")
        {
            return "main_bets";
        }

        if (betOption.StartsWith("single_"))
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