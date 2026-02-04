using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable chip component for betting areas
/// Displays chip image and amount text
/// Can be used in both chip selector and bet displays
/// </summary>
public class Chip : MonoBehaviour
{
    [SerializeField] internal Image chipImage;
    [SerializeField] internal TMP_Text chipText;
    
    internal int chipIndex { get; private set; }

    /// <summary>
    /// Set chip data including sprite, amount text, and index
    /// </summary>
    internal void SetData(Sprite chip, string amount, int chipIndex)
    {
        if (chipImage != null) chipImage.sprite = chip;
        if (chipText != null) chipText.text = amount;
        this.chipIndex = chipIndex;
    }

    /// <summary>
    /// Update only the amount text without changing sprite or index
    /// </summary>
    internal void SetAmount(string amount)
    {
        if (chipText != null) chipText.text = amount;
    }

    /// <summary>
    /// Update only the chip sprite without changing text or index
    /// </summary>
    internal void SetSprite(Sprite chip)
    {
        if (chipImage != null) chipImage.sprite = chip;
    }

    /// <summary>
    /// Show or hide this chip
    /// </summary>
    internal void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }

    /// <summary>
    /// Check if this chip is currently active
    /// </summary>
    internal bool IsActive()
    {
        return gameObject.activeSelf;
    }
}
