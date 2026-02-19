using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Menu Screen")]
    [SerializeField] private GameObject menuScreen;
    [SerializeField] private Button mainCloseButton;

    [Header("Panel Navigation")]
    [SerializeField] private Button historyNavButton;
    [SerializeField] private Button infoNavButton;

    [Header("Panels")]
    [SerializeField] private GameObject historyPanel;
    [SerializeField] private GameObject infoPanel;

    [Header("Info Panel Pages")]
    [SerializeField] private List<GameObject> infoPages;
    [SerializeField] private Button forwardButton;
    [SerializeField] private Button backwardButton;

    [Header("References")]
    [SerializeField] private HistoryController historyController;
    #endregion

    #region Private Fields
    private int currentInfoPage = 0;
    private const int TOTAL_INFO_PAGES = 3;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        SetupButtons();
        HideMenu();
    }
    #endregion

    #region Setup
    private void SetupButtons()
    {
        mainCloseButton?.onClick.AddListener(() => { AudioManager.Instance?.PlayButtonClick(); CloseMenu(); });
        historyNavButton?.onClick.AddListener(() => { AudioManager.Instance?.PlayButtonClick(); ShowHistoryPanel(); });
        infoNavButton?.onClick.AddListener(() => { AudioManager.Instance?.PlayButtonClick(); ShowInfoPanel(); });
        forwardButton?.onClick.AddListener(() => { AudioManager.Instance?.PlayArrowButton(); NextInfoPage(); });
        backwardButton?.onClick.AddListener(() => { AudioManager.Instance?.PlayArrowButton(); PreviousInfoPage(); });
    }
    #endregion

    #region Internal API
    internal void OpenMenuWithHistory()
    {
        ShowMenu();
        ShowHistoryPanel();
    }

    internal void OpenMenuWithInfo()
    {
        ShowMenu();
        ShowInfoPanel();
        ShowInfoPage(0);
    }

    internal void CloseMenu() => HideMenu();
    #endregion

    #region Menu Management
    private void ShowMenu() => menuScreen?.SetActive(true);

    private void HideMenu()
    {
        menuScreen?.SetActive(false);
        HideAllPanels();
    }

    private void HideAllPanels()
    {
        historyPanel?.SetActive(false);
        infoPanel?.SetActive(false);
    }
    #endregion

    #region Panel Navigation
    private void ShowHistoryPanel()
    {
        HideAllPanels();
        historyPanel?.SetActive(true);
        historyController?.ShowHistoryPanel();
    }

    private void ShowInfoPanel()
    {
        HideAllPanels();
        infoPanel?.SetActive(true);
        ShowInfoPage(0);
    }
    #endregion

    #region Info Page Navigation
    private void ShowInfoPage(int pageIndex)
    {
        if (infoPages == null || infoPages.Count == 0) return;

        currentInfoPage = Mathf.Clamp(pageIndex, 0, TOTAL_INFO_PAGES - 1);

        for (int i = 0; i < infoPages.Count; i++)
            infoPages[i]?.SetActive(i == currentInfoPage);

        UpdateInfoNavigationButtons();
    }

    private void NextInfoPage()
    {
        if (currentInfoPage < TOTAL_INFO_PAGES - 1) ShowInfoPage(currentInfoPage + 1);
    }

    private void PreviousInfoPage()
    {
        if (currentInfoPage > 0) ShowInfoPage(currentInfoPage - 1);
    }

    private void UpdateInfoNavigationButtons()
    {
        if (forwardButton) forwardButton.interactable = currentInfoPage < TOTAL_INFO_PAGES - 1;
        if (backwardButton) backwardButton.interactable = currentInfoPage > 0;
    }
    #endregion
}