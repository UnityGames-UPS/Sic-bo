using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameUtilities
{
    private const int MaxChipCombinationCount = 20;

    #region Currency Formatting
    internal static string FormatCurrency(double amount)
    {
        if (amount >= 1000) return $"{amount / 1000:F1}K";
        if (amount < 1) return amount.ToString("F1");
        if (amount % 1 != 0) return amount.ToString("F1");
        return amount.ToString("F0");
    }

    internal static string FormatBetValue(double value) =>
        value % 1 == 0 ? value.ToString("F0") : value.ToString("F2");

    internal static string FormatBalance(double balance) => balance.ToString("F2");
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
        List<ChipCombinationItem> result = new List<ChipCombinationItem>();
        if (availableChipValues == null || availableChipValues.Count == 0) return result;

        List<double> sortedValues = new List<double>(availableChipValues);
        sortedValues.Sort((a, b) => b.CompareTo(a));

        double remaining = targetAmount;
        const double tolerance = 0.01;

        while (remaining > tolerance)
        {
            bool foundChip = false;
            for (int i = 0; i < sortedValues.Count; i++)
            {
                if (sortedValues[i] <= remaining + tolerance)
                {
                    result.Add(new ChipCombinationItem
                    {
                        amount = sortedValues[i],
                        chipIndex = availableChipValues.IndexOf(sortedValues[i])
                    });
                    remaining -= sortedValues[i];
                    foundChip = true;
                    break;
                }
            }

            if (!foundChip) break;
            if (result.Count >= MaxChipCombinationCount) break;
        }

        return result;
    }
    #endregion

    #region Validation
    internal static T GetFromList<T>(List<T> list, int index) where T : class
    {
        if (list == null || index < 0 || index >= list.Count) return null;
        return list[index];
    }
    #endregion
}