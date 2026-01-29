using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Chip : MonoBehaviour
{
    [SerializeField] private UiManager uiManager;
    [SerializeField] private TMP_Text ChipText;
    [SerializeField] private Image chipCoinImg;
    [SerializeField] private Button buttonSelection;

    void Start()
    {
        if (buttonSelection) buttonSelection.onClick.RemoveAllListeners();
        if (buttonSelection) buttonSelection.onClick.AddListener(delegate { OnButtonClick(); });
    }

    void OnButtonClick()
    {
        if (uiManager) uiManager.OnCoinSelected(buttonSelection);
    }
    internal void SetData(Sprite coinImg, string coinValue)
    {
        chipCoinImg.sprite = coinImg;
        ChipText.text = coinValue;
    }
}
