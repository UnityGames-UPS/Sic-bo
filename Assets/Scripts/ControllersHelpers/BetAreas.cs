using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public abstract class BaseBetArea
{
    [Header("UI References")]
    public Button Button;
    public GameObject WinImage;
    public Transform PlayerBetContainer;
    public Transform OpponentBetContainer;

    [HideInInspector] public PlayerBetComponent playerBetComponent;

    internal virtual void AddBet(double amount, int chipIndex)
    {
        playerBetComponent?.AddBet(amount, chipIndex);
    }

    internal virtual void RemoveLastBet()
    {
        playerBetComponent?.RemoveLastBet();
    }

    internal virtual void ClearBets()
    {
        playerBetComponent?.Clear();
    }

    internal double GetTotalBet() => playerBetComponent != null ? playerBetComponent.GetTotalBet() : 0;
    internal bool HasBets() => playerBetComponent != null && playerBetComponent.HasBets();

    internal void SetHighlight(bool highlight)
    {
        if (WinImage) WinImage.SetActive(highlight);
    }
}

[System.Serializable]
public class SimpleBetArea : BaseBetArea
{
    public TMP_Text WinRatio_Text;

    internal void SetWinRatio(string ratio)
    {
        if (WinRatio_Text) WinRatio_Text.text = ratio;
    }
}

[System.Serializable]
public class TripleSameDiceArea : BaseBetArea { }

[System.Serializable]
public class SingleDiceArea : BaseBetArea { }

[System.Serializable]
public class SumArea : BaseBetArea
{
    public TMP_Text WinRatio_Text;

    internal void SetWinRatio(string ratio)
    {
        if (WinRatio_Text) WinRatio_Text.text = ratio;
    }
}