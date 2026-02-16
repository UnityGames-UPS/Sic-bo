using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared utility methods used across the Sic Bo game
/// </summary>
public static class GameUtilities
{
    #region Currency Formatting
    /// <summary>
    /// Format currency with K suffix for thousands
    /// </summary>
    internal static string FormatCurrency(double amount)
    {
        if (amount >= 1000)
        {
            return $"{(amount / 1000):F1}K";
        }

        if (amount < 1)
        {
            return amount.ToString("F1");
        }

        if (amount % 1 != 0)
        {
            return amount.ToString("F1");
        }

        return amount.ToString("F0");
    }

    /// <summary>
    /// Format bet value with 2 decimal places if needed
    /// </summary>
    internal static string FormatBetValue(double value)
    {
        if (value % 1 == 0)
            return value.ToString("F0");
        else
            return value.ToString("F2");
    }

    /// <summary>
    /// Format currency with 2 decimal places for balance display
    /// </summary>
    internal static string FormatBalance(double balance)
    {
        return balance.ToString("F2");
    }
    #endregion

    #region Time Calculations
    /// <summary>
    /// Calculate remaining seconds from server timestamps
    /// </summary>
    internal static int CalculateTimeRemaining(long endTime, long serverTime)
    {
        long remainingMs = endTime - serverTime;
        float remainingSeconds = remainingMs / 1000f;
        return Mathf.Max(0, Mathf.RoundToInt(remainingSeconds));
    }

    /// <summary>
    /// Format DateTime to readable string
    /// </summary>
    internal static string FormatDateTime(DateTime dateTime)
    {
        if (dateTime == DateTime.MinValue) return "Unknown";
        return dateTime.ToString("dd/MM/yyyy hh:mm tt");
    }

    /// <summary>
    /// Parse ISO 8601 timestamp
    /// </summary>
    internal static DateTime ParseTimestamp(string timestamp)
    {
        try
        {
            return DateTime.Parse(timestamp);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }
    #endregion

    #region List Conversions
    /// <summary>
    /// Convert integer list to double list
    /// </summary>
    internal static List<double> ConvertToDoubleList(List<int> intList)
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
    #endregion

    #region Chip Combination Algorithm
    /// <summary>
    /// Find optimal chip combination for target amount
    /// </summary>
    internal static List<ChipCombinationItem> FindChipCombination(double targetAmount, List<double> availableChipValues)
    {
        List<ChipCombinationItem> result = new List<ChipCombinationItem>();

        if (availableChipValues == null || availableChipValues.Count == 0)
        {
            Debug.LogWarning("[GameUtilities] No chip values available for combination");
            return result;
        }

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
                    int chipIndex = availableChipValues.IndexOf(sortedValues[i]);

                    result.Add(new ChipCombinationItem
                    {
                        amount = sortedValues[i],
                        chipIndex = chipIndex
                    });

                    remaining -= sortedValues[i];
                    foundChip = true;
                    break;
                }
            }

            if (!foundChip)
            {
                Debug.LogWarning($"[GameUtilities] Cannot find chip combination for remaining: {remaining}");
                break;
            }

            if (result.Count >= 20)
            {
                Debug.LogWarning($"[GameUtilities] Chip combination exceeded 20 chips");
                break;
            }
        }

        return result;
    }
    #endregion

    #region Validation Helpers
    /// <summary>
    /// Safely get element from list with bounds checking
    /// </summary>
    internal static T GetFromList<T>(List<T> list, int index) where T : class
    {
        if (list == null || index < 0 || index >= list.Count)
            return null;

        return list[index];
    }
    #endregion
}

