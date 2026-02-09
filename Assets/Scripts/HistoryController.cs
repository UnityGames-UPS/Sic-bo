using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages bet history display with pagination
/// - Disables all rows by default
/// - Enables only rows with data
/// - Handles pagination errors when no more data exists
/// </summary>
public class HistoryController : MonoBehaviour
{
    #region Serialized Fields
    [Header("History Panel")]
    [SerializeField] private GameObject HistoryPanel;
    [SerializeField] private List<HistoryRowView> HistoryRows; // Fixed rows (typically 8-10)
    [SerializeField] private TMP_Text PageInfo_Text;
    [SerializeField] private Button PrevPage_Button;
    [SerializeField] private Button NextPage_Button;
    [SerializeField] private Button Close_Button;

    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private UIController uiController;
    #endregion

    #region Private Fields
    private int currentPage = 1;
    private int totalPages = 1;
    private List<HistoryEntry> currentHistoryData = new List<HistoryEntry>();
    private bool isWaitingForData = false;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        SetupButtons();
        InitializeRows();
        HideHistoryPanel();
    }
    #endregion

    #region Setup
    private void SetupButtons()
    {
        if (PrevPage_Button) PrevPage_Button.onClick.AddListener(OnPrevPageClicked);
        if (NextPage_Button) NextPage_Button.onClick.AddListener(OnNextPageClicked);
        if (Close_Button) Close_Button.onClick.AddListener(HideHistoryPanel);
    }

    /// <summary>
    /// Initialize all rows as disabled at start
    /// They will be enabled when data is received
    /// </summary>
    private void InitializeRows()
    {
        if (HistoryRows == null) return;

        foreach (var row in HistoryRows)
        {
            if (row != null)
            {
                row.gameObject.SetActive(false);
            }
        }

        Debug.Log($"[HISTORY] Initialized {HistoryRows.Count} rows (all disabled)");
    }
    #endregion

    #region Public API
    /// <summary>
    /// Show history panel and request first page
    /// </summary>
    internal void ShowHistoryPanel()
    {
        if (HistoryPanel) HistoryPanel.SetActive(true);

        // Reset to page 1
        currentPage = 1;
        totalPages = 1;

        RequestPage(1);
    }

    /// <summary>
    /// Hide history panel
    /// </summary>
    internal void HideHistoryPanel()
    {
        if (HistoryPanel) HistoryPanel.SetActive(false);
        isWaitingForData = false;
    }

    /// <summary>
    /// Update history data from server response
    /// Called by GameManager when history data is received
    /// </summary>
    internal void UpdateHistoryData(List<HistoryEntry> history, HistoryMeta meta)
    {
        if (history == null || meta == null)
        {
            Debug.LogWarning("[HISTORY] Received null history data");
            isWaitingForData = false;
            return;
        }

        isWaitingForData = false;
        currentHistoryData = history;
        currentPage = meta.page;
        totalPages = meta.pages;

        Debug.Log($"[HISTORY] Received page {currentPage}/{totalPages} with {history.Count} entries");

        UpdateRows();
        UpdatePageInfo();
        UpdateNavigationButtons();
    }
    #endregion

    #region Private Methods - Pagination
    /// <summary>
    /// Request a specific page of history from server
    /// </summary>
    private void RequestPage(int page)
    {
        // Validate page number
        if (page < 1)
        {
            Debug.LogWarning($"[HISTORY] Invalid page number: {page}");
            return;
        }

        // Don't request if already waiting
        if (isWaitingForData)
        {
            Debug.Log("[HISTORY] Already waiting for data");
            return;
        }

        // Check if page exceeds known total (but still allow first request)
        if (totalPages > 0 && page > totalPages)
        {
            Debug.Log($"[HISTORY] Page {page} exceeds total pages {totalPages}");
            if (uiController)
            {
                uiController.ShowErrorPopup("No more history available");
            }
            return;
        }

        Debug.Log($"[HISTORY] Requesting page {page}");
        isWaitingForData = true;
        gameManager.RequestHistory(page);
    }

    private void OnPrevPageClicked()
    {
        if (currentPage > 1)
        {
            RequestPage(currentPage - 1);
        }
        else
        {
            Debug.Log("[HISTORY] Already at first page");
        }
    }

    private void OnNextPageClicked()
    {
        if (currentPage < totalPages)
        {
            RequestPage(currentPage + 1);
        }
        else
        {
            Debug.Log("[HISTORY] Already at last page");
            if (uiController)
            {
                uiController.ShowErrorPopup("No more history available");
            }
        }
    }
    #endregion

    #region Private Methods - Display
    /// <summary>
    /// Update row displays with current history data
    /// Disables unused rows
    /// </summary>
    private void UpdateRows()
    {
        if (HistoryRows == null || currentHistoryData == null)
        {
            Debug.LogWarning("[HISTORY] Cannot update rows - missing data");
            return;
        }

        // First, disable all rows
        foreach (var row in HistoryRows)
        {
            if (row != null)
            {
                row.gameObject.SetActive(false);
            }
        }

        // Calculate how many rows to show
        int rowsToShow = Mathf.Min(HistoryRows.Count, currentHistoryData.Count);

        Debug.Log($"[HISTORY] Updating {rowsToShow} rows with data");

        // Enable and populate rows with data
        for (int i = 0; i < rowsToShow; i++)
        {
            if (HistoryRows[i] != null && currentHistoryData[i] != null)
            {
                // Calculate actual row number (considering pagination)
                int displayRowNumber = ((currentPage - 1) * HistoryRows.Count) + i + 1;

                // Set data and enable
                HistoryRows[i].SetData(currentHistoryData[i], displayRowNumber);
                HistoryRows[i].gameObject.SetActive(true);

                Debug.Log($"[HISTORY] Row {i}: Round {currentHistoryData[i].round_id} - " +
                         $"Bet: {currentHistoryData[i].bet_amount} - Win: {currentHistoryData[i].win_amount}");
            }
        }

        // Log unused rows
        if (rowsToShow < HistoryRows.Count)
        {
            Debug.Log($"[HISTORY] {HistoryRows.Count - rowsToShow} rows remain disabled");
        }
    }

    /// <summary>
    /// Update page info display (e.g., "1 / 3")
    /// </summary>
    private void UpdatePageInfo()
    {
        if (PageInfo_Text)
        {
            PageInfo_Text.text = $"{currentPage} / {totalPages}";
            Debug.Log($"[HISTORY] Page info: {currentPage}/{totalPages}");
        }
    }

    /// <summary>
    /// Update navigation button interactability
    /// Prev disabled on first page, Next disabled on last page
    /// </summary>
    private void UpdateNavigationButtons()
    {
        if (PrevPage_Button)
        {
            bool canGoPrev = currentPage > 1 && !isWaitingForData;
            PrevPage_Button.interactable = canGoPrev;
            Debug.Log($"[HISTORY] Prev button: {(canGoPrev ? "enabled" : "disabled")}");
        }

        if (NextPage_Button)
        {
            bool canGoNext = currentPage < totalPages && !isWaitingForData;
            NextPage_Button.interactable = canGoNext;
            Debug.Log($"[HISTORY] Next button: {(canGoNext ? "enabled" : "disabled")}");
        }
    }
    #endregion
}