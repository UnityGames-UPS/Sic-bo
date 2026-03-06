using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public static class GameUtilities
{
    private const int MaxChipCombinationCount = 20;

    private static readonly List<double> _sortedValuesCache = new List<double>(16);
    private static readonly List<ChipCombinationItem> _chipCombResultCache = new List<ChipCombinationItem>(MaxChipCombinationCount);

    private static readonly Dictionary<double, string> _currencyCache = new Dictionary<double, string>(300);
    private static readonly Dictionary<double, string> _betValueCache = new Dictionary<double, string>(100);
    private static readonly Dictionary<double, string> _balanceCache = new Dictionary<double, string>(50);
    private static readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(32);

    private static readonly int[] _statsDiceCounts = new int[6];
    private static readonly int[] _lrFloored = new int[8];
    private static readonly int[] _lrResult = new int[8];
    private static readonly int[] _lrIndices = new int[8];
    private static readonly double[] _lrExact = new double[8];
    private static readonly double[] _lrRemainders = new double[8];

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
        if (amount >= 10000)
        {
            double kValue = amount / 1000;

            if (kValue % 1 == 0)
            {
                _sb.Clear();
                _sb.Append(kValue.ToString("F0"));
                _sb.Append("k");
                result = _sb.ToString();
            }
            else
            {
                _sb.Clear();
                _sb.Append(kValue.ToString("0.##"));
                _sb.Append("k");
                result = _sb.ToString();
            }
        }
        else
        {
            result = amount % 1 == 0 ? amount.ToString("F0") : amount.ToString("0.##");
        }

        if (_currencyCache.Count < 300) _currencyCache[amount] = result;
        return result;
    }

    internal static string FormatBetValue(double value)
    {
        if (_betValueCache.TryGetValue(value, out string cached)) return cached;
        string result = value % 1 == 0 ? value.ToString("F0") : value.ToString("0.##");
        if (_betValueCache.Count < 100) _betValueCache[value] = result;
        return result;
    }

    internal static string FormatBalance(double balance)
    {
        if (_balanceCache.TryGetValue(balance, out string cached)) return cached;

        string result;
        if (balance >= 10000)
        {
            double kValue = balance / 1000;

            if (kValue % 1 == 0)
            {
                result = kValue.ToString("F0") + "k";
            }
            else
            {
                result = kValue.ToString("0.##") + "k";
            }
        }
        else
        {
            result = balance % 1 == 0 ? balance.ToString("F0") : balance.ToString("0.##");
        }

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

        for (int i = 0; i < 6; i++) _statsDiceCounts[i] = 0;

        int validRounds = 0;
        int totalDiceRolls = 0;
        int smallCount = 0, bigCount = 0, oddCount = 0;

        foreach (string entry in rawStats)
        {
            ResultData data = null;
            try { data = JsonConvert.DeserializeObject<ResultData>(entry); }
            catch { continue; }

            if (data == null) continue;
            if (data.dice1 < 1 || data.dice1 > 6 ||
                data.dice2 < 1 || data.dice2 > 6 ||
                data.dice3 < 1 || data.dice3 > 6) continue;

            validRounds++;
            int computedSum = data.dice1 + data.dice2 + data.dice3;

            _statsDiceCounts[data.dice1 - 1]++;
            _statsDiceCounts[data.dice2 - 1]++;
            _statsDiceCounts[data.dice3 - 1]++;
            totalDiceRolls += 3;

            if (computedSum >= 4 && computedSum <= 10) smallCount++;
            else if (computedSum >= 11 && computedSum <= 17) bigCount++;

            if (computedSum % 2 != 0) oddCount++;
        }

        result.totalRounds = validRounds;
        if (validRounds == 0) return result;

        int evenCount = validRounds - oddCount;

        int[] diceInts = LargestRemainder(_statsDiceCounts, 6, totalDiceRolls > 0 ? totalDiceRolls : 1, 100);
        for (int i = 0; i < 6; i++) result.dicePct[i] = diceInts[i];

        int smallBigTotal = smallCount + bigCount;
        int[] smallBigPair = { smallCount, bigCount };
        int[] smallBigInts = LargestRemainder(smallBigPair, 2, smallBigTotal > 0 ? smallBigTotal : 1, 100);
        result.smallPct = smallBigInts[0];
        result.bigPct = smallBigInts[1];

        int[] oddEvenPair = { oddCount, evenCount };
        int[] oddEvenInts = LargestRemainder(oddEvenPair, 2, validRounds, 100);
        result.oddPct = oddEvenInts[0];
        result.evenPct = oddEvenInts[1];

        return result;
    }

    private static int[] LargestRemainder(int[] counts, int n, int total, int target)
    {
        int flooredSum = 0;

        for (int i = 0; i < n; i++)
        {
            _lrExact[i] = (double)counts[i] / total * target;
            _lrFloored[i] = (int)_lrExact[i];
            _lrRemainders[i] = _lrExact[i] - _lrFloored[i];
            _lrResult[i] = _lrFloored[i];
            _lrIndices[i] = i;
            flooredSum += _lrFloored[i];
        }

        int leftover = target - flooredSum;
        Array.Sort(_lrIndices, 0, n, Comparer<int>.Create((a, b) => _lrRemainders[b].CompareTo(_lrRemainders[a])));

        for (int i = 0; i < leftover && i < n; i++)
            _lrResult[_lrIndices[i]]++;

        return _lrResult;
    }
    #endregion
}