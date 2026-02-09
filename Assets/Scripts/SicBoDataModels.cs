using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Unity.Burst.CompilerServices;

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
    public int diceLimit;
    public int statsLimit;
    public Bets bets;
    public List<string> levels;
    public Wagers wagers;
    public Lobby lobby;
    public Leaderboards leaderboards;
    public List<object> stats;
    public BonusMultipliers bonusMultipliers;
}

[Serializable]
public class Bets
{
    public List<double> casual;
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
    public BetWager specific_3;
    public BetWager specific_2;
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
    public List<double> payout;
    public MaxBetLimit max_bet_limit;

    public string GetPayoutRatioString()
    {
        if (payout != null && payout.Count >= 2)
        {
            return $"1 : {payout[1]}";
        }
        return "1 : 1";
    }


    public double CalculateWin(double betAmount)
    {
        if (payout != null && payout.Count >= 2)
        {
            return betAmount * payout[1];
        }
        return betAmount;
    }

    public double GetMaxBet(string level)
    {
        if (max_bet_limit == null) return 0;

        return level switch
        {
            "casual" => max_bet_limit.casual,
            "novice" => max_bet_limit.novice,
            "expert" => max_bet_limit.expert,
            "high_roller" => max_bet_limit.high_roller,
            _ => 0
        };
    }

    /// <summary>
    /// Get combined display for specific_2 and specific_3 in single text field
    /// Shows both payouts dynamically from server data
    /// </summary>
    public static string GetCombinedSpecificPayoutString(BetWager specific2, BetWager specific3)
    {
        string specific2Payout = "1";
        string specific3Payout = "1";

        if (specific2?.payout != null && specific2.payout.Count >= 2)
        {
            specific2Payout = specific2.payout[1].ToString();
        }

        if (specific3?.payout != null && specific3.payout.Count >= 2)
        {
            specific3Payout = specific3.payout[1].ToString();
        }

        return $"2 HIT PAYS 1 : {specific2Payout}   3 HIT PAYS 1 : {specific3Payout}";

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
    public string username;
    public string betId;
    public double totalBet;
    public string betOption;
    public double amount;
    public double balance;
    public string message;
    public List<HistoryEntry> history;
    public HistoryMeta meta;
    public Lobby lobby;
    public List<Payout> payouts;
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
    public string phase;
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
    public string matchSide;
}

[Serializable]
public class BetPlacedData
{
    public string username;
    public string betId;
    public string betType;
    public string betOption;
    public double amount;
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
public class RoundEndPayload
{
    public string roundId;
    public int cashoutInterval;
    public long nextRoundStartTime;
    public long serverTime;
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
    public double delta;
    public int diceNumber; // For triple dice bets (specific_3) - which dice (1-6)
}

public class BetLimitInfo
{
    public double MinBet { get; set; }
    public double MaxBet { get; set; }
    public double CurrentBetOnArea { get; set; }
    public double TotalBet { get; set; }

    public bool CanPlaceBet(double betAmount)
    {
        return (CurrentBetOnArea + betAmount) <= MaxBet;
    }

    public bool ExceedsMax(double betAmount)
    {
        return (CurrentBetOnArea + betAmount) > MaxBet;
    }
}
#endregion