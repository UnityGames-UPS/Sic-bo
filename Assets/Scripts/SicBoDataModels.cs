using System;
using System.Collections.Generic;
using Newtonsoft.Json;

#region Root Response
[Serializable]
public class SicBoRoot
{
    public string id;
    public SicBoGameData gameData;
    public Player player;
    public bool success;
    public RoomPayload payload;
}

[Serializable]
public class AuthTokenData
{
    public string cookie;
    public string socketURL;
    public string nameSpace;
}
#endregion

#region Game Init Data
[Serializable]
public class SicBoGameData
{
    public List<string> betOptions;
    public int roundInterval;
    public int diceInterval;
    public Bets bets;
    public List<string> levels;
    public Leaderboards leaderboards;
    public BonusMultipliers bonusMultipliers;
    public Wagers wagers;
}

[Serializable]
public class Bets
{
    public List<double> casual; // Contains 0.5, so needs double
    public List<int> novice;
    public List<int> expert;
    public List<int> high_roller;
}

[Serializable]
public class BonusMultipliers
{
    public int small;
    public int big;
    public int odd;
    public int even;
}

[Serializable]
public class Leaderboards
{
    public List<LeaderboardEntry> richest;
    public List<LeaderboardEntry> winners;
}

[Serializable]
public class LeaderboardEntry
{
    public string username;
    public double balance;
    public double totalWins;
    public int rank;
}

[Serializable]
public class Player
{
    public double balance;
    public string username;
}

#region Wager Data
[Serializable]
public class Wagers
{
    public MainBets main_bets;
    public SideBets side_bets;
    public OpBets op_bets;
}

[Serializable]
public class MainBets
{
    public BetWager small;
    public BetWager big;
    public BetWager odd;
    public BetWager even;
}

[Serializable]
public class SideBets
{
    public BetWager single_match_1;
    public BetWager single_match_2;
    public BetWager single_match_3;
    public BetWager specific_2;
    public BetWager specific_3;
}

[Serializable]
public class OpBets
{
    public BetWager sum_4;
    public BetWager sum_5;
    public BetWager sum_6;
    public BetWager sum_7;
    public BetWager sum_8;
    public BetWager sum_9;
    public BetWager sum_10;
    public BetWager sum_11;
    public BetWager sum_12;
    public BetWager sum_13;
    public BetWager sum_14;
    public BetWager sum_15;
    public BetWager sum_16;
    public BetWager sum_17;
}

[Serializable]
public class BetWager
{
    public List<double> payout; // [1, 0.95] format - multiply bet, win ratio
    public MaxBetLimit max_bet_limit;

    /// <summary>
    /// Get win ratio as formatted string "1 : 0.95"
    /// </summary>
    public string GetPayoutRatioString()
    {
        if (payout != null && payout.Count >= 2)
        {
            return $"1 : {payout[1]}";
        }
        return "1 : 1";
    }

    /// <summary>
    /// Calculate win amount based on bet
    /// </summary>
    public double CalculateWin(double betAmount)
    {
        if (payout != null && payout.Count >= 2)
        {
            return betAmount * payout[1];
        }
        return betAmount;
    }
}

[Serializable]
public class MaxBetLimit
{
    public int casual;
    public int novice;
    public int expert;
    public int high_roller;
}
#endregion

#endregion

#region Room Join Response
[Serializable]
public class RoomPayload
{
    public string roomId;
    public string oldRoomId;
    public int playerCount;
    public string level;
    public Leaderboards leaderboards;
    public RoundState roundState;

    // Bet Response
    public string username;
    public string betId;
    public double totalBet;
    public string betOption;
    public double amount;
    public double balance;
    public string message;

    // History Response
    public List<HistoryEntry> history;
    public HistoryMeta meta;

    // Home Response
    public Lobby lobby;

    // Cashout Response
    public List<Payout> payouts;

    // Bet list for double/repeat
    public List<BetInfo> bets;
}

[Serializable]
public class RoundState
{
    public string roundId;
    public long startedAt;
    public long bettingEndTime;
    public long serverTime;
    public int timeRemaining;
    public string phase; // "betting" or "dealing"
}
#endregion

#region Round Events
[Serializable]
public class RoundStartData
{
    public string roundId;
    public long startedAt;
    public long bettingEndTime;
    public long serverTime;
    public int playerCount;
}

[Serializable]
public class TimerData
{
    public string roundId;
    public long serverTime;
    public long bettingEndTime;
    public int timeRemaining;
}

[Serializable]
public class BonusData
{
    public string roundId;
    public int bonusPlayer;
    public int bonusMultiplier;
}

[Serializable]
public class DiceResultData
{
    public string roundId;
    public int dice1;
    public int dice2;
    public int dice3;
    public int sum;
    public string matchSide; // "small", "big", "odd", "even"
}

[Serializable]
public class BetPlacedData
{
    public string username;
    public string betId;
    public string betType;
    public string betOption;
    public double amount; // Negative = cancellation
}

[Serializable]
public class CashoutData
{
    public Leaderboards leaderboards;
    public List<Payout> payouts;
}

[Serializable]
public class Payout
{
    public string userId;
    public string username;
    public double win;
    public double balance;
}

[Serializable]
public class LobbyCountData
{
    public Lobby lobby;
}

[Serializable]
public class Lobby
{
    public int casual;
    public int novice;
    public int expert;
    public int high_roller;
}

[Serializable]
public class RoundEndData
{
    public string roundId;
}
#endregion

#region History
[Serializable]
public class HistoryEntry
{
    public string round_id;
    public double bet_amount;
    public double win_amount;
    public int dice_1;
    public int dice_2;
    public int dice_3;
    public string match_side;
}

[Serializable]
public class HistoryMeta
{
    public int total;
    public int page;
    public int limit;
    public int pages;
}
#endregion

#region Betting
[Serializable]
public class BetInfo
{
    public string betId;
    public string betType;
    public string betOption;
    public double amount;
    public double delta; // For double bet
}
#endregion