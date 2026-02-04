using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the menu screen with History and Info panels
/// Allows navigation between panels with a single main close button
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
    [SerializeField] private List<GameObject> infoPages; // 3 pages
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
        // Main close button
        if (mainCloseButton) mainCloseButton.onClick.AddListener(CloseMenu);

        // Panel navigation buttons
        if (historyNavButton) historyNavButton.onClick.AddListener(ShowHistoryPanel);
        if (infoNavButton) infoNavButton.onClick.AddListener(ShowInfoPanel);

        // Info page navigation
        if (forwardButton) forwardButton.onClick.AddListener(NextInfoPage);
        if (backwardButton) backwardButton.onClick.AddListener(PreviousInfoPage);
    }
    #endregion

    #region Public API
    /// <summary>
    /// Open menu and show history panel
    /// Called from Home screen History button
    /// </summary>
    internal void OpenMenuWithHistory()
    {
        ShowMenu();
        ShowHistoryPanel();
    }

    /// <summary>
    /// Open menu and show info panel (Settings button)
    /// Opens to first page by default
    /// </summary>
    internal void OpenMenuWithInfo()
    {
        ShowMenu();
        ShowInfoPanel();
        ShowInfoPage(0); // Default to first page
    }

    /// <summary>
    /// Close menu and hide all panels
    /// </summary>
    internal void CloseMenu()
    {
        HideMenu();
    }
    #endregion

    #region Private Methods
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

    private void ShowHistoryPanel()
    {
        // Hide all panels first
        HideAllPanels();

        // Show history panel
        if (historyPanel) historyPanel.SetActive(true);

        // Request history data
        if (historyController) historyController.ShowHistoryPanel();
    }

    private void ShowInfoPanel()
    {
        // Hide all panels first
        HideAllPanels();

        // Show info panel
        if (infoPanel) infoPanel.SetActive(true);

        // Show first page by default
        ShowInfoPage(0);
    }

    private void ShowInfoPage(int pageIndex)
    {
        if (infoPages == null || infoPages.Count == 0) return;

        // Clamp page index
        currentInfoPage = Mathf.Clamp(pageIndex, 0, TOTAL_INFO_PAGES - 1);

        // Hide all pages
        for (int i = 0; i < infoPages.Count; i++)
        {
            if (infoPages[i] != null)
            {
                infoPages[i].SetActive(i == currentInfoPage);
            }
        }

        // Update navigation buttons
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
        // Update forward button
        if (forwardButton)
        {
            forwardButton.interactable = currentInfoPage < TOTAL_INFO_PAGES - 1;
        }

        // Update backward button
        if (backwardButton)
        {
            backwardButton.interactable = currentInfoPage > 0;
        }
    }
    #endregion
}
