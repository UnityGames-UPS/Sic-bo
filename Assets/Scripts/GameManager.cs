using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Main game controller - coordinates all game systems
/// Updated with wager data support for win ratios
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

    #region Socket Callbacks
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

        // Setup lobby counts
        if (socketManager.InitialData.leaderboards != null)
        {
            uiController.UpdateLobbyPlayerCounts(
                socketManager.InitialData.leaderboards.richest?.Count ?? 0
            );
        }

        // Show home screen
        uiController.ShowHomeScreen();
    }

    internal void OnRoundStart(RoundStartData data)
    {
        if (data == null) return;

        Debug.Log($"[GAME] Round started: {data.roundId}");

        CurrentRoundId = data.roundId;

        // Update UI
        uiController.UpdatePlayerCount(data.playerCount);

        // Calculate initial time remaining
        int timeRemaining = Mathf.Max(0, (int)((data.bettingEndTime - data.serverTime) / 1000));

        // Show betting phase
        uiController.ShowBettingPhase(timeRemaining);

        // Start round controller
        roundController.StartRound(data);

        // Enable betting
        betController.EnableBetting();
    }

    internal void OnBettingTimer(TimerData data)
    {
        if (data == null) return;

        // Update timer display
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

        // Show bet locked state
        uiController.ShowBetLocked();

        // Show dice animation
        roundController.ShowDiceResult(data);

        // Highlight winning areas
        betController.HighlightWinningAreas(data.matchSide, data.sum);

        // Highlight triple dice results
        betController.HighlightTripleDiceResult(data.dice1, data.dice2, data.dice3);
    }

    internal void OnBetPlaced(BetPlacedData data)
    {
        if (data == null) return;

        // Show other players' chips
        if (data.username != socketManager.PlayerData.username)
        {
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
                        uiController.ShowWinAnimation(payout.win);
                    }
                }
            }
        }
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

    internal void OnRoundEnd(RoundEndData data)
    {
        if (data == null) return;

        Debug.Log($"[GAME] Round ended: {data.roundId}");

        // Clear bets and prepare for next round
        betController.ClearAllBets();
        roundController.EndRound();

        // Show next round countdown (example: 5 seconds between rounds)
        uiController.ShowNextRound(5);
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

        // Request join
        socketManager.JoinLevel(roomName);

        // Show game screen with loading
        uiController.ShowGameScreen();
    }

    internal void LeaveRoom()
    {
        Debug.Log("[GAME] Leaving room");

        socketManager.ReturnHome();
        uiController.ShowHomeScreen();
        uiController.HideAllTimers();

        CurrentRoom = null;
        betController.ClearAllBets();
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
        socketManager.DoubleBet("");
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
    private List<double> GetChipValuesForRoom(string roomName)
    {
        if (socketManager.InitialData?.bets == null) return new List<double>();

        return roomName switch
        {
            "casual" => socketManager.InitialData.bets.casual, // Already List<double>
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
        // Main bets: small, big, odd, even
        if (betOption == "small" || betOption == "big" || betOption == "odd" || betOption == "even")
            return "main_bets";

        // Side bets: single_1 to single_6, specific_2, specific_3
        if (betOption.StartsWith("single_") || betOption.StartsWith("specific_"))
            return "side_bets";

        // Sum bets: sum_4 to sum_17
        if (betOption.StartsWith("sum_"))
            return "op_bets";

        return "main_bets";
    }
    #endregion
}