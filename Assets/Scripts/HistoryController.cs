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
    [SerializeField] private List<HistoryRowView> HistoryRows; // Fixed 10 rows
    [SerializeField] private TMP_Text PageInfo_Text;
    [SerializeField] private Button PrevPage_Button;
    [SerializeField] private Button NextPage_Button;
    [SerializeField] private Button Close_Button;

    [Header("References")]
    [SerializeField] private GameManager gameManager;
    #endregion

    #region Private Fields
    private int currentPage = 1;
    private int totalPages = 1;
    private List<HistoryEntry> currentHistoryData = new List<HistoryEntry>();
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        SetupButtons();
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
    #endregion

    #region Public API
    internal void ShowHistoryPanel()
    {
        if (HistoryPanel) HistoryPanel.SetActive(true);
        RequestPage(1);
    }

    internal void HideHistoryPanel()
    {
        if (HistoryPanel) HistoryPanel.SetActive(false);
    }

    internal void UpdateHistoryData(List<HistoryEntry> history, HistoryMeta meta)
    {
        if (history == null || meta == null) return;

        currentHistoryData = history;
        currentPage = meta.page;
        totalPages = meta.pages;

        UpdateRows();
        UpdatePageInfo();
        UpdateNavigationButtons();
    }
    #endregion

    #region Private Methods
    private void RequestPage(int page)
    {
        if (page < 1) page = 1;
        if (page > totalPages && totalPages > 0) page = totalPages;

        gameManager.RequestHistory(page);
    }

    private void OnPrevPageClicked()
    {
        if (currentPage > 1)
        {
            RequestPage(currentPage - 1);
        }
    }

    private void OnNextPageClicked()
    {
        if (currentPage < totalPages)
        {
            RequestPage(currentPage + 1);
        }
    }

    private void UpdateRows()
    {
        if (HistoryRows == null) return;

        // Update visible rows
        int maxRows = Mathf.Min(HistoryRows.Count, currentHistoryData.Count);

        for (int i = 0; i < maxRows; i++)
        {
            HistoryRows[i].SetData(currentHistoryData[i], i + 1);
            HistoryRows[i].gameObject.SetActive(true);
        }

        // Hide unused rows
        for (int i = maxRows; i < HistoryRows.Count; i++)
        {
            HistoryRows[i].gameObject.SetActive(false);
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
        if (PrevPage_Button) PrevPage_Button.interactable = currentPage > 1;
        if (NextPage_Button) NextPage_Button.interactable = currentPage < totalPages;
    }
    #endregion
}