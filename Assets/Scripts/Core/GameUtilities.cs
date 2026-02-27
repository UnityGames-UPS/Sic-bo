using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public static class GameUtilities
{
    private const int MaxChipCombinationCount = 20;

    // GC optimisation: reuse static lists across calls to FindChipCombination
    private static readonly List<double> _sortedValuesCache = new List<double>(16);
    private static readonly List<ChipCombinationItem> _chipCombResultCache = new List<ChipCombinationItem>(MaxChipCombinationCount);

    // String result caching — FormatCurrency is called per chip, per frame during animations
    private static readonly Dictionary<double, string> _currencyCache = new Dictionary<double, string>(300);
    private static readonly Dictionary<double, string> _betValueCache = new Dictionary<double, string>(100);
    private static readonly Dictionary<double, string> _balanceCache = new Dictionary<double, string>(50);
    private static readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(32);

    /// <summary>Call when leaving or switching rooms to release cached string memory.</summary>
    internal static void ClearCaches()
    {
        if (_currencyCache.Count > 200) _currencyCache.Clear();
        if (_betValueCache.Count > 80) _betValueCache.Clear();
        if (_balanceCache.Count > 40) _balanceCache.Clear();
    }

    #region Currency Formatting
    internal static string FormatCurrency(double amount)
    {
        if (_currencyCache.TryGetValue(amount, out string cached)) return cached;

        string result;
        if (amount >= 1000)
        {
            _sb.Clear();
            _sb.Append((amount / 1000).ToString("F1"));
            _sb.Append("K");
            result = _sb.ToString();
        }
        else if (amount < 1) result = amount.ToString("F1");
        else if (amount % 1 != 0) result = amount.ToString("F1");
        else result = amount.ToString("F0");

        if (_currencyCache.Count < 300) _currencyCache[amount] = result;
        return result;
    }

    internal static string FormatBetValue(double value)
    {
        if (_betValueCache.TryGetValue(value, out string cached)) return cached;
        string result = value % 1 == 0 ? value.ToString("F0") : value.ToString("F2");
        if (_betValueCache.Count < 100) _betValueCache[value] = result;
        return result;
    }

    internal static string FormatBalance(double balance)
    {
        if (_balanceCache.TryGetValue(balance, out string cached)) return cached;
        string result = balance.ToString("F2");
        if (_balanceCache.Count < 50) _balanceCache[balance] = result;
        return result;
    }
    #endregion

    #region Time Calculations
    internal static int CalculateTimeRemaining(long endTime, long serverTime)
    {
        long remainingMs = endTime - serverTime;
        return Mathf.Max(0, Mathf.RoundToInt(remainingMs / 1000f));
    }

    internal static string FormatDateTime(DateTime dateTime) =>
        dateTime == DateTime.MinValue ? "Unknown" : dateTime.ToString("dd/MM/yyyy hh:mm tt");

    internal static DateTime ParseTimestamp(string timestamp)
    {
        try { return DateTime.Parse(timestamp); }
        catch { return DateTime.MinValue; }
    }
    #endregion

    #region List Conversions
    internal static List<double> ConvertToDoubleList(List<double> list) => list ?? new List<double>();

    internal static List<double> ConvertToDoubleList(List<int> intList)
    {
        List<double> result = new List<double>();
        if (intList != null)
            foreach (int value in intList) result.Add(value);
        return result;
    }
    #endregion

    #region Chip Combination
    internal static List<ChipCombinationItem> FindChipCombination(double targetAmount, List<double> availableChipValues)
    {
        _chipCombResultCache.Clear();
        if (availableChipValues == null || availableChipValues.Count == 0) return _chipCombResultCache;

        _sortedValuesCache.Clear();
        _sortedValuesCache.AddRange(availableChipValues);
        _sortedValuesCache.Sort((a, b) => b.CompareTo(a));

        double remaining = targetAmount;
        const double tolerance = 0.01;

        while (remaining > tolerance)
        {
            bool foundChip = false;
            for (int i = 0; i < _sortedValuesCache.Count; i++)
            {
                if (_sortedValuesCache[i] <= remaining + tolerance)
                {
                    _chipCombResultCache.Add(new ChipCombinationItem
                    {
                        amount = _sortedValuesCache[i],
                        chipIndex = availableChipValues.IndexOf(_sortedValuesCache[i])
                    });
                    remaining -= _sortedValuesCache[i];
                    foundChip = true;
                    break;
                }
            }

            if (!foundChip) break;
            if (_chipCombResultCache.Count >= MaxChipCombinationCount) break;
        }

        return _chipCombResultCache;
    }
    #endregion

    #region Validation
    internal static T GetFromList<T>(List<T> list, int index) where T : class
    {
        if (list == null || index < 0 || index >= list.Count) return null;
        return list[index];
    }
    #endregion

    #region Stats
    internal static StatsResult CalculateStats(List<string> rawStats)
    {
        StatsResult result = new StatsResult();

        if (rawStats == null || rawStats.Count == 0) return result;

        int validRounds = 0;
        int[] diceCounts = new int[6];
        int totalDiceRolls = 0;
        int smallCount = 0, bigCount = 0, oddCount = 0;

        foreach (string entry in rawStats)
        {
            ResultData data = null;
            try { data = Newtonsoft.Json.JsonConvert.DeserializeObject<ResultData>(entry); }
            catch { continue; }

            if (data == null) continue;
            if (data.dice1 < 1 || data.dice1 > 6 || data.dice2 < 1 || data.dice2 > 6 || data.dice3 < 1 || data.dice3 > 6) continue;

            validRounds++;

            int computedSum = data.dice1 + data.dice2 + data.dice3;

            int[] diceVals = { data.dice1, data.dice2, data.dice3 };
            foreach (int d in diceVals)
            {
                diceCounts[d - 1]++;
                totalDiceRolls++;
            }

            if (computedSum >= 4 && computedSum <= 10) smallCount++;
            else if (computedSum >= 11 && computedSum <= 17) bigCount++;

            if (computedSum % 2 != 0) oddCount++;
        }

        result.totalRounds = validRounds;
        if (validRounds == 0) return result;

        int evenCount = validRounds - oddCount;

        int[] diceInts = LargestRemainder(diceCounts, totalDiceRolls > 0 ? totalDiceRolls : 1, 100);
        for (int i = 0; i < 6; i++) result.dicePct[i] = diceInts[i];

        int smallBigTotal = smallCount + bigCount;
        int[] smallBigInts = LargestRemainder(new[] { smallCount, bigCount }, smallBigTotal > 0 ? smallBigTotal : 1, 100);
        result.smallPct = smallBigInts[0];
        result.bigPct = smallBigInts[1];

        int[] oddEvenInts = LargestRemainder(new[] { oddCount, evenCount }, validRounds, 100);
        result.oddPct = oddEvenInts[0];
        result.evenPct = oddEvenInts[1];

        return result;
    }

    private static int[] LargestRemainder(int[] counts, int total, int target)
    {
        int n = counts.Length;
        double[] exact = new double[n];
        int[] floored = new int[n];
        double[] remainders = new double[n];
        int[] result = new int[n];
        int flooredSum = 0;

        for (int i = 0; i < n; i++)
        {
            exact[i] = (double)counts[i] / total * target;
            floored[i] = (int)exact[i];
            remainders[i] = exact[i] - floored[i];
            flooredSum += floored[i];
        }

        int leftover = target - flooredSum;
        int[] indices = new int[n];
        for (int i = 0; i < n; i++) indices[i] = i;
        System.Array.Sort(indices, (a, b) => remainders[b].CompareTo(remainders[a]));

        for (int i = 0; i < n; i++) result[i] = floored[i];
        for (int i = 0; i < leftover && i < n; i++) result[indices[i]]++;

        return result;
    }
    #endregion
}