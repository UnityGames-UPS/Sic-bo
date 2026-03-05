using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

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
    //[SerializeField] private Button Close_Button;

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
        PrevPage_Button?.onClick.AddListener(() => { AudioManager.Instance?.PlayArrowButton(); OnPrevPageClicked(); });
        NextPage_Button?.onClick.AddListener(() => { AudioManager.Instance?.PlayArrowButton(); OnNextPageClicked(); });
        Prev5Page_Button?.onClick.AddListener(() => { AudioManager.Instance?.PlayArrowButton(); OnPrev5PageClicked(); });
        Next5Page_Button?.onClick.AddListener(() => { AudioManager.Instance?.PlayArrowButton(); OnNext5PageClicked(); });
       // Close_Button?.onClick.AddListener(() => { AudioManager.Instance?.PlayButtonClick(); HideHistoryPanel(); });

        AddButtonPressAnimation(PrevPage_Button, 1.2f);
        AddButtonPressAnimation(NextPage_Button, 1.2f);
        AddButtonPressAnimation(Prev5Page_Button, 1.2f);
        AddButtonPressAnimation(Next5Page_Button, 1.2f);
       // AddButtonPressAnimation(Close_Button, 0.95f);
    }

    private void AddButtonPressAnimation(Button button, float targetScale)
    {
        if (button == null) return;

        EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        pointerDown.callback.AddListener((data) => { OnButtonPressed(button.transform, targetScale); });
        trigger.triggers.Add(pointerDown);

        EventTrigger.Entry pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        pointerUp.callback.AddListener((data) => { OnButtonReleased(button.transform); });
        trigger.triggers.Add(pointerUp);

        EventTrigger.Entry pointerExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        pointerExit.callback.AddListener((data) => { OnButtonReleased(button.transform); });
        trigger.triggers.Add(pointerExit);
    }

    private void OnButtonPressed(Transform buttonTransform, float targetScale)
    {
        buttonTransform.localScale = Vector3.one * targetScale;
    }

    private void OnButtonReleased(Transform buttonTransform)
    {
        buttonTransform.localScale = Vector3.one;
    }

    private void InitializeRows()
    {
        if (HistoryRows == null) return;
        foreach (var row in HistoryRows)
            row?.gameObject.SetActive(false);
    }
    #endregion

    #region Internal API
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
        if (history == null || meta == null) { isWaitingForData = false; return; }

        isWaitingForData = false;
        currentHistoryData = history;
        currentPage = meta.page;
        totalPages = meta.pages;

        UpdateRows();
        UpdatePageInfo();
        UpdateNavigationButtons();
    }
    #endregion

    #region Pagination
    private void RequestPage(int page)
    {
        if (page < 1 || isWaitingForData) return;

        if (totalPages > 0 && page > totalPages)
        {
            uiController?.ShowErrorPopup("No more history available");
            return;
        }

        isWaitingForData = true;
        gameManager.RequestHistory(page);
    }

    private void OnPrevPageClicked()
    {
        if (currentPage > 1) RequestPage(currentPage - 1);
    }

    private void OnPrev5PageClicked()
    {
        if (currentPage > 5) RequestPage(currentPage - 5);
    }

    private void OnNextPageClicked()
    {
        if (currentPage < totalPages)
            RequestPage(currentPage + 1);
        else
            uiController?.ShowErrorPopup("No more history available");
    }

    private void OnNext5PageClicked()
    {
        RequestPage(currentPage + 5 <= totalPages ? currentPage + 5 : totalPages);
    }
    #endregion

    #region Display
    private void UpdateRows()
    {
        if (HistoryRows == null || currentHistoryData == null) return;

        foreach (var row in HistoryRows)
            row?.gameObject.SetActive(false);

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
        if (PageInfo_Text) PageInfo_Text.text = $"{currentPage} / {totalPages}";
    }

    private void UpdateNavigationButtons()
    {
        if (PrevPage_Button) PrevPage_Button.interactable = currentPage > 1 && !isWaitingForData;
        if (NextPage_Button) NextPage_Button.interactable = currentPage < totalPages && !isWaitingForData;
        if (Prev5Page_Button) Prev5Page_Button.interactable = currentPage > 5 && !isWaitingForData;
        if (Next5Page_Button) Next5Page_Button.interactable = currentPage + 5 <= totalPages && !isWaitingForData;
    }
    #endregion
}