using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Base class for all bet areas with common functionality
/// </summary>
[System.Serializable]
public abstract class BaseBetArea
{
    [Header("UI References")]
    public Button Button;
    public GameObject WinImage;
    public Transform PlayerBetContainer;

    [HideInInspector] public PlayerBetComponent playerBetComponent;

    public virtual void AddBet(double amount, int chipIndex)
    {
        if (playerBetComponent != null)
            playerBetComponent.AddBet(amount, chipIndex);
    }

    public virtual void RemoveLastBet()
    {
        if (playerBetComponent != null)
            playerBetComponent.RemoveLastBet();
    }

    public virtual void ClearBets()
    {
        if (playerBetComponent != null)
            playerBetComponent.Clear();
    }

    public double GetTotalBet() =>
        playerBetComponent != null ? playerBetComponent.GetTotalBet() : 0;

    public bool HasBets() =>
        playerBetComponent != null && playerBetComponent.HasBets();

    public void SetHighlight(bool highlight)
    {
        if (WinImage) WinImage.SetActive(highlight);
    }
}

/// <summary>
/// Bet area with win ratio display (Main bets, Sum bets)
/// </summary>
[System.Serializable]
public class SimpleBetArea : BaseBetArea
{
    public TMP_Text WinRatio_Text;

    public void SetWinRatio(string ratio)
    {
        if (WinRatio_Text) WinRatio_Text.text = ratio;
    }
}

/// <summary>
/// Bet area for triple dice (specific_3_1 to specific_3_6)
/// </summary>
[System.Serializable]
public class TripleSameDiceArea : BaseBetArea
{
}

/// <summary>
/// Bet area for single dice matches (single_1 to single_6)
/// </summary>
[System.Serializable]
public class SingleDiceArea : BaseBetArea
{
}

/// <summary>
/// Bet area for sum bets (sum_4 to sum_17)
/// </summary>
[System.Serializable]
public class SumArea : BaseBetArea
{
    public TMP_Text WinRatio_Text;

    public void SetWinRatio(string ratio)
    {
        if (WinRatio_Text) WinRatio_Text.text = ratio;
    }
}   