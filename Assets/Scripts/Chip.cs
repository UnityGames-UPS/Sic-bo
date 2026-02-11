using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable chip component for betting displays
/// </summary>
public class Chip : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] internal Image chipImage;
    [SerializeField] internal TMP_Text chipText;
    #endregion

    #region Public Properties
    internal int chipIndex { get; private set; }
    #endregion

    #region Public API
    internal void SetData(Sprite chip, string amount, int chipIndex)
    {
        if (chipImage != null) chipImage.sprite = chip;
        if (chipText != null) chipText.text = amount;
        this.chipIndex = chipIndex;
    }

    internal void SetAmount(string amount)
    {
        if (chipText != null) chipText.text = amount;
    }

    internal void SetSprite(Sprite chip)
    {
        if (chipImage != null) chipImage.sprite = chip;
    }

    internal void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }

    internal bool IsActive() => gameObject.activeSelf;
    #endregion
}
