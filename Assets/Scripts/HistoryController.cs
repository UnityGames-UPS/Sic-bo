using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages bet history display with pagination
/// </summary>
public class HistoryController : MonoBehaviour
{
    #region Serialized Fields
    [Header("History Panel")]
    [SerializeField] private GameObject HistoryPanel;
    [SerializeField] private List<HistoryRowView> HistoryRows;
    [SerializeField] private TMP_Text PageInfo_Text;
    [SerializeField] private Button PrevPage_Button;
    [SerializeField] private Button NextPage_Button;
    [SerializeField] private Button Prev5Page_Button;
    [SerializeField] private Button Next5Page_Button;
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
        if (Prev5Page_Button) Prev5Page_Button.onClick.AddListener(OnPrev5PageClicked);
        if (Next5Page_Button) Next5Page_Button.onClick.AddListener(OnNext5PageClicked);
        if (Close_Button) Close_Button.onClick.AddListener(HideHistoryPanel);
    }

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
    }
    #endregion

    #region Public API
    internal void ShowHistoryPanel()
    {
        if (HistoryPanel) HistoryPanel.SetActive(true);

        currentPage = 1;
        totalPages = 1;

        RequestPage(1);
    }

    internal void HideHistoryPanel()
    {
        if (HistoryPanel) HistoryPanel.SetActive(false);
        isWaitingForData = false;
    }

    internal void UpdateHistoryData(List<HistoryEntry> history, HistoryMeta meta)
    {
        if (history == null || meta == null)
        {
            isWaitingForData = false;
            return;
        }

        isWaitingForData = false;
        currentHistoryData = history;
        currentPage = meta.page;
        totalPages = meta.pages;

        UpdateRows();
        UpdatePageInfo();
        UpdateNavigationButtons();
    }
    #endregion

    #region Private Methods - Pagination
    private void RequestPage(int page)
    {
        if (page < 1) return;

        if (isWaitingForData) return;

        if (totalPages > 0 && page > totalPages)
        {
            if (uiController)
            {
                uiController.ShowErrorPopup("No more history available");
            }
            return;
        }

        isWaitingForData = true;
        gameManager.RequestHistory(page);
    }

    private void OnPrevPageClicked()
    {
        if (currentPage > 1)
        {
            RequestPage(currentPage - 1);
        }
    }
    private void OnPrev5PageClicked()
    {
        if (currentPage > 5)
        {
            RequestPage(currentPage - 5);
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
            if (uiController)
            {
                uiController.ShowErrorPopup("No more history available");
            }
        }
    }

    private void OnNext5PageClicked()
    {
        if (currentPage + 5 <= totalPages)
        {
            RequestPage(currentPage + 5);
        }
        else
        {
            RequestPage(totalPages);
        }
    }
    #endregion

    #region Private Methods - Display
    private void UpdateRows()
    {
        if (HistoryRows == null || currentHistoryData == null) return;

        foreach (var row in HistoryRows)
        {
            if (row != null)
            {
                row.gameObject.SetActive(false);
            }
        }

        int rowsToShow = Mathf.Min(HistoryRows.Count, currentHistoryData.Count);

        for (int i = 0; i < rowsToShow; i++)
        {
            if (HistoryRows[i] != null && currentHistoryData[i] != null)
            {
                int displayRowNumber = ((currentPage - 1) * HistoryRows.Count) + i + 1;

                HistoryRows[i].SetData(currentHistoryData[i], displayRowNumber);
                HistoryRows[i].gameObject.SetActive(true);
            }
        }
    }

    private void UpdatePageInfo()
    {
        if (PageInfo_Text)
        {
            PageInfo_Text.text = $"{currentPage} / {totalPages}";
        }
    }

    private void UpdateNavigationButtons()
    {
        if (PrevPage_Button)
        {
            bool canGoPrev = currentPage > 1 && !isWaitingForData;
            PrevPage_Button.interactable = canGoPrev;
        }

        if (NextPage_Button)
        {
            bool canGoNext = currentPage < totalPages && !isWaitingForData;
            NextPage_Button.interactable = canGoNext;
        }

        if (Prev5Page_Button)
        {
            bool canGoPrev5 = currentPage > 5 && !isWaitingForData;
            Prev5Page_Button.interactable = canGoPrev5;
        }

        if (Next5Page_Button)
        {
            bool canGoNext5 = currentPage + 5 <= totalPages && !isWaitingForData;
            Next5Page_Button.interactable = canGoNext5;
        }
    }
    #endregion
}
