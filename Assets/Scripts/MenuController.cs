using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages menu screen navigation (History and Info panels)
/// </summary>
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
        if (mainCloseButton) mainCloseButton.onClick.AddListener(CloseMenu);

        if (historyNavButton) historyNavButton.onClick.AddListener(ShowHistoryPanel);
        if (infoNavButton) infoNavButton.onClick.AddListener(ShowInfoPanel);

        if (forwardButton) forwardButton.onClick.AddListener(NextInfoPage);
        if (backwardButton) backwardButton.onClick.AddListener(PreviousInfoPage);
    }
    #endregion

    #region Public API
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

    internal void CloseMenu()
    {
        HideMenu();
    }
    #endregion

    #region Private Methods - Menu Management
    private void ShowMenu()
    {
        if (menuScreen) menuScreen.SetActive(true);
    }

    private void HideMenu()
    {
        if (menuScreen) menuScreen.SetActive(false);
        HideAllPanels();
    }

    private void HideAllPanels()
    {
        if (historyPanel) historyPanel.SetActive(false);
        if (infoPanel) infoPanel.SetActive(false);
    }
    #endregion

    #region Private Methods - Panel Navigation
    private void ShowHistoryPanel()
    {
        HideAllPanels();

        if (historyPanel) historyPanel.SetActive(true);

        if (historyController) historyController.ShowHistoryPanel();
    }

    private void ShowInfoPanel()
    {
        HideAllPanels();

        if (infoPanel) infoPanel.SetActive(true);

        ShowInfoPage(0);
    }
    #endregion

    #region Private Methods - Info Page Navigation
    private void ShowInfoPage(int pageIndex)
    {
        if (infoPages == null || infoPages.Count == 0) return;

        currentInfoPage = Mathf.Clamp(pageIndex, 0, TOTAL_INFO_PAGES - 1);

        for (int i = 0; i < infoPages.Count; i++)
        {
            if (infoPages[i] != null)
            {
                infoPages[i].SetActive(i == currentInfoPage);
            }
        }

        UpdateInfoNavigationButtons();
    }

    private void NextInfoPage()
    {
        if (currentInfoPage < TOTAL_INFO_PAGES - 1)
        {
            ShowInfoPage(currentInfoPage + 1);
        }
    }

    private void PreviousInfoPage()
    {
        if (currentInfoPage > 0)
        {
            ShowInfoPage(currentInfoPage - 1);
        }
    }

    private void UpdateInfoNavigationButtons()
    {
        if (forwardButton)
        {
            forwardButton.interactable = currentInfoPage < TOTAL_INFO_PAGES - 1;
        }

        if (backwardButton)
        {
            backwardButton.interactable = currentInfoPage > 0;
        }
    }
    #endregion
}
