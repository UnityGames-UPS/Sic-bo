using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;


[System.Serializable]
public class BetLimitTexts
{
    public TextMeshProUGUI minText;
    public TextMeshProUGUI maxText;
}

public class BetLimitManager : MonoBehaviour
{
    #region Serialized Fields
    [Header("Panel References")]
    [SerializeField] private GameObject betLimitPanel;
    [SerializeField] private GameObject mainArea;
    [SerializeField] private Button openPanelButton;
    [SerializeField] private Button closePanelButton;
    [SerializeField] private Button confirmButton;

    [Header("Dependencies")]
    [SerializeField] private GameManager gameManager;

    [Header("Room Selection Buttons")]
    [SerializeField] private Button casualButton;
    [SerializeField] private Button noviceButton;
    [SerializeField] private Button expertButton;
    [SerializeField] private Button highRollerButton;

    [Header("Room Selection Indicators")]
    [SerializeField] private GameObject casualSelectionIndicator;
    [SerializeField] private GameObject noviceSelectionIndicator;
    [SerializeField] private GameObject expertSelectionIndicator;
    [SerializeField] private GameObject highRollerSelectionIndicator;

    [Header("Room Min/Max Texts")]
    [SerializeField] private TextMeshProUGUI casualMinBetText;
    [SerializeField] private TextMeshProUGUI casualMaxBetText;
    [SerializeField] private TextMeshProUGUI noviceMinBetText;
    [SerializeField] private TextMeshProUGUI noviceMaxBetText;
    [SerializeField] private TextMeshProUGUI expertMinBetText;
    [SerializeField] private TextMeshProUGUI expertMaxBetText;
    [SerializeField] private TextMeshProUGUI highRollerMinBetText;
    [SerializeField] private TextMeshProUGUI highRollerMaxBetText;

    [Header("Bet Option Texts")]
    [SerializeField] private List<BetLimitTexts> mainBetsTexts = new List<BetLimitTexts>(4);
    [SerializeField] private List<BetLimitTexts> specific3Texts = new List<BetLimitTexts>(6);
    [SerializeField] private List<BetLimitTexts> singleNumberTexts = new List<BetLimitTexts>(6);
    [SerializeField] private List<BetLimitTexts> sumBetsTexts = new List<BetLimitTexts>(14);

    [Header("Animation Settings")]
    [SerializeField] private float popupDuration = 0.3f;
    [SerializeField] private AnimationCurve popupCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Scroll Settings")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Scrollbar verticalScrollbar;
    [SerializeField] private float scrollbarFadeDelay = 1.5f;
    [SerializeField] private float scrollbarFadeDuration = 0.3f;
    #endregion

    #region Private Fields
    private Wagers wagersData;
    private Bets betsData;
    private List<string> betOptions;
    private string currentSelectedRoom = "casual";
    private string playerCurrentRoom;
    private Coroutine popupCoroutine;

    // Scrollbar visibility management
    private CanvasGroup scrollbarCanvasGroup;
    private Coroutine scrollbarFadeCoroutine;
    private bool isScrolling = false;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        SetupButtonListeners();
        SetupScrollbar();
        betLimitPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        // Clean up scroll listener
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
        }
    }
    #endregion

    #region Setup
    private void SetupButtonListeners()
    {
        if (openPanelButton != null)
            openPanelButton.onClick.AddListener(() => { OpenPanel(); });

        if (closePanelButton != null)
            closePanelButton.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                ClosePanel();
            });

        if (confirmButton != null)
            confirmButton.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                OnConfirmClicked();
            });

        if (casualButton != null)
            casualButton.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                OnRoomButtonClicked("casual");
            });

        if (noviceButton != null)
            noviceButton.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                OnRoomButtonClicked("novice");
            });

        if (expertButton != null)
            expertButton.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                OnRoomButtonClicked("expert");
            });

        if (highRollerButton != null)
            highRollerButton.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                OnRoomButtonClicked("high_roller");
            });

        AddButtonPressAnimation(openPanelButton, 0.95f);
        AddButtonPressAnimation(closePanelButton, 0.95f);
        AddButtonPressAnimation(confirmButton, 0.95f);
        AddButtonPressAnimation(casualButton, 0.95f);
        AddButtonPressAnimation(noviceButton, 0.95f);
        AddButtonPressAnimation(expertButton, 0.95f);
        AddButtonPressAnimation(highRollerButton, 0.95f);
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

    private void SetupScrollbar()
    {
        if (verticalScrollbar == null)
        {
            Debug.LogWarning("BetLimitManager: Vertical scrollbar not assigned!");
            return;
        }

        // Get or add CanvasGroup for fade animations
        scrollbarCanvasGroup = verticalScrollbar.GetComponent<CanvasGroup>();
        if (scrollbarCanvasGroup == null)
        {
            scrollbarCanvasGroup = verticalScrollbar.gameObject.AddComponent<CanvasGroup>();
        }

        // Ensure scrollbar can be interacted with
        scrollbarCanvasGroup.interactable = true;
        scrollbarCanvasGroup.blocksRaycasts = false; // Don't block clicks when invisible

        // Initially hide the scrollbar
        scrollbarCanvasGroup.alpha = 0f;

        // Add scroll listener
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
            Debug.Log("BetLimitManager: Scrollbar setup complete. ScrollRect connected.");
        }
        else
        {
            Debug.LogWarning("BetLimitManager: ScrollRect not assigned!");
        }
    }

    private void OnScrollValueChanged(Vector2 scrollPosition)
    {
        // Always show scrollbar when scrolling happens
        ShowScrollbar();

        // Mark as actively scrolling
        isScrolling = true;

        // Reset the fade timer - stop any ongoing hide animation
        if (scrollbarFadeCoroutine != null)
        {
            StopCoroutine(scrollbarFadeCoroutine);
        }

        // Start new hide delay
        scrollbarFadeCoroutine = StartCoroutine(HideScrollbarAfterDelay());
    }

    private void ShowScrollbar()
    {
        if (scrollbarCanvasGroup == null) return;

        // Stop any ongoing fade animation
        if (scrollbarFadeCoroutine != null)
        {
            StopCoroutine(scrollbarFadeCoroutine);
        }

        // Immediately show at full opacity for instant feedback
        scrollbarCanvasGroup.alpha = 1f;

        // Optional: Use fade-in for smoother appearance (comment out line above if using this)
        // scrollbarFadeCoroutine = StartCoroutine(FadeScrollbar(1f, scrollbarFadeDuration * 0.5f));
    }

    private IEnumerator HideScrollbarAfterDelay()
    {
        yield return new WaitForSeconds(scrollbarFadeDelay);

        isScrolling = false;
        yield return StartCoroutine(FadeScrollbar(0f, scrollbarFadeDuration));
    }

    private IEnumerator FadeScrollbar(float targetAlpha, float duration)
    {
        if (scrollbarCanvasGroup == null) yield break;

        // Clamp target alpha to valid range
        targetAlpha = Mathf.Clamp01(targetAlpha);

        float startAlpha = scrollbarCanvasGroup.alpha;
        float elapsed = 0f;

        // If duration is very small or zero, set immediately
        if (duration <= 0.01f)
        {
            scrollbarCanvasGroup.alpha = targetAlpha;
            yield break;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            scrollbarCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        // Ensure we reach exact target value
        scrollbarCanvasGroup.alpha = targetAlpha;
    }
    #endregion

    #region Public API
    internal void Initialize(Wagers wagers, Bets bets, string currentRoom, List<string> receivedBetOptions)
    {
        wagersData = wagers;
        betsData = bets;
        betOptions = receivedBetOptions;
        playerCurrentRoom = currentRoom;
        currentSelectedRoom = currentRoom;

        UpdateRoomButtonMinMaxValues();
    }

    /*public void UpdatePlayerCurrentRoom(string newRoom)
    {
        playerCurrentRoom = newRoom;
    }

    public void RefreshData(Wagers wagers, Bets bets, List<string> receivedBetOptions)
    {
        wagersData = wagers;
        betsData = bets;
        betOptions = receivedBetOptions;

        UpdateRoomButtonMinMaxValues();

        if (betLimitPanel.activeSelf)
        {
            UpdateBetLimitDisplays();
        }
    }*/

    // Debug method to test scrollbar visibility - can be called from inspector or other scripts
    public void TestScrollbarVisibility(bool show)
    {
        if (scrollbarCanvasGroup != null)
        {
            scrollbarCanvasGroup.alpha = show ? 1f : 0f;
            Debug.Log($"BetLimitManager: Scrollbar manually set to {(show ? "visible" : "hidden")} - Alpha: {scrollbarCanvasGroup.alpha}");
        }
        else
        {
            Debug.LogWarning("BetLimitManager: ScrollbarCanvasGroup is null!");
        }
    }
    #endregion

    #region Panel Control
    internal void OpenPanel()
    {
        if (wagersData == null || betsData == null)
        {
            Debug.LogWarning("BetLimitManager: Data not initialized!");
            return;
        }

        betLimitPanel.SetActive(true);

        currentSelectedRoom = playerCurrentRoom;
        UpdateRoomSelection();
        UpdateBetLimitDisplays();
        UpdateConfirmButton();

        // Reset scroll position and hide scrollbar
        ResetScrollbar();

        // Force canvas update to ensure scroll rect is ready
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
        }

        if (popupCoroutine != null)
            StopCoroutine(popupCoroutine);
        popupCoroutine = StartCoroutine(PlayPopupAnimation(true));
    }

    internal void ClosePanel()
    {
        // Stop any ongoing scrollbar fade animation
        if (scrollbarFadeCoroutine != null)
        {
            StopCoroutine(scrollbarFadeCoroutine);
            scrollbarFadeCoroutine = null;
        }

        // Hide scrollbar immediately
        if (scrollbarCanvasGroup != null)
        {
            scrollbarCanvasGroup.alpha = 0f;
        }

        isScrolling = false;

        if (popupCoroutine != null)
            StopCoroutine(popupCoroutine);
        popupCoroutine = StartCoroutine(PlayPopupAnimation(false));
    }

    private void ResetScrollbar()
    {
        // Reset scroll position to top
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
        }

        // Stop any ongoing fade animation
        if (scrollbarFadeCoroutine != null)
        {
            StopCoroutine(scrollbarFadeCoroutine);
            scrollbarFadeCoroutine = null;
        }

        // Hide scrollbar
        if (scrollbarCanvasGroup != null)
        {
            scrollbarCanvasGroup.alpha = 0f;
        }

        isScrolling = false;
    }
    #endregion

    #region Confirm / Room Switch

    private void OnConfirmClicked()
    {
        if (currentSelectedRoom == playerCurrentRoom)
        {
            ClosePanel();
            return;
        }

        string targetRoom = currentSelectedRoom;


        if (popupCoroutine != null) StopCoroutine(popupCoroutine);
        betLimitPanel.SetActive(false);


        gameManager?.SwitchRoom(targetRoom);
    }

    private void UpdateConfirmButton()
    {
        if (confirmButton == null) return;
        confirmButton.interactable = true;
    }

    #endregion

    #region Private Methods - Animation
    private IEnumerator PlayPopupAnimation(bool isOpening)
    {
        if (mainArea == null) yield break;

        float startScale = isOpening ? 0f : 1f;
        float endScale = isOpening ? 1f : 0f;
        float elapsed = 0f;

        mainArea.transform.localScale = Vector3.one * startScale;

        while (elapsed < popupDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / popupDuration;
            float curveValue = popupCurve.Evaluate(progress);
            float scale = Mathf.Lerp(startScale, endScale, curveValue);

            mainArea.transform.localScale = Vector3.one * scale;

            yield return null;
        }

        mainArea.transform.localScale = Vector3.one * endScale;

        if (!isOpening)
        {
            betLimitPanel.SetActive(false);
        }
    }
    #endregion

    #region Private Methods - Room Selection
    private void OnRoomButtonClicked(string roomName)
    {
        if (currentSelectedRoom == roomName) return;

        currentSelectedRoom = roomName;
        UpdateRoomSelection();
        UpdateBetLimitDisplays();
        UpdateConfirmButton();

        Canvas.ForceUpdateCanvases();
    }

    private void UpdateRoomSelection()
    {
        if (casualSelectionIndicator != null)
            casualSelectionIndicator.SetActive(false);
        if (noviceSelectionIndicator != null)
            noviceSelectionIndicator.SetActive(false);
        if (expertSelectionIndicator != null)
            expertSelectionIndicator.SetActive(false);
        if (highRollerSelectionIndicator != null)
            highRollerSelectionIndicator.SetActive(false);

        switch (currentSelectedRoom)
        {
            case "casual":
                if (casualSelectionIndicator != null)
                    casualSelectionIndicator.SetActive(true);
                break;
            case "novice":
                if (noviceSelectionIndicator != null)
                    noviceSelectionIndicator.SetActive(true);
                break;
            case "expert":
                if (expertSelectionIndicator != null)
                    expertSelectionIndicator.SetActive(true);
                break;
            case "high_roller":
                if (highRollerSelectionIndicator != null)
                    highRollerSelectionIndicator.SetActive(true);
                break;
        }
    }
    #endregion

    #region Private Methods - Room Min/Max Display
    private void UpdateRoomButtonMinMaxValues()
    {
        if (wagersData == null || betsData == null) return;

        UpdateRoomMinMax("casual", casualMinBetText, casualMaxBetText);
        UpdateRoomMinMax("novice", noviceMinBetText, noviceMaxBetText);
        UpdateRoomMinMax("expert", expertMinBetText, expertMaxBetText);
        UpdateRoomMinMax("high_roller", highRollerMinBetText, highRollerMaxBetText);
    }

    private void UpdateRoomMinMax(string roomName, TextMeshProUGUI minText, TextMeshProUGUI maxText)
    {
        double minBet = GetMinBetForRoom(roomName);
        double maxBet = GetMaxBetFromWager(wagersData.main_bets?.small, roomName);

        if (minText != null)
            minText.text = GameUtilities.FormatBetValue(minBet);

        if (maxText != null)
            maxText.text = GameUtilities.FormatBetValue(maxBet);
    }

    private double GetMaxBetFromWager(BetWager wager, string roomName)
    {
        if (wager == null) return 0;
        return wager.GetMaxBet(roomName);
    }
    #endregion

    #region Private Methods - Bet Option Display
    private void UpdateBetLimitDisplays()
    {
        if (wagersData == null || betOptions == null) return;

        foreach (string betOption in betOptions)
        {
            if (betOption == "specific_2")
                continue;

            BetWager wager = GetWagerForBetOption(betOption);
            if (wager == null)
                continue;

            UpdateBetOptionDisplay(betOption, wager);
        }
    }

    private void UpdateBetOptionDisplay(string betOption, BetWager wager)
    {
        double minBet = GetMinBetForRoom(currentSelectedRoom);
        double maxBet = wager.GetMaxBet(currentSelectedRoom);

        BetLimitTexts texts = GetTextsForBetOption(betOption);
        if (texts == null)
        {
            Debug.LogWarning($"BetLimitManager: No text mapping for: {betOption}");
            return;
        }

        if (texts.minText != null)
            texts.minText.text = GameUtilities.FormatBetValue(minBet);

        if (texts.maxText != null)
            texts.maxText.text = GameUtilities.FormatBetValue(maxBet);
    }

    private BetLimitTexts GetTextsForBetOption(string betOption)
    {
        switch (betOption)
        {
            case "small":
                return GameUtilities.GetFromList(mainBetsTexts, 0);
            case "big":
                return GameUtilities.GetFromList(mainBetsTexts, 1);
            case "odd":
                return GameUtilities.GetFromList(mainBetsTexts, 2);
            case "even":
                return GameUtilities.GetFromList(mainBetsTexts, 3);
        }

        if (betOption.StartsWith("specific_3_"))
        {
            string numberStr = betOption.Substring(11);
            if (int.TryParse(numberStr, out int number) && number >= 1 && number <= 6)
            {
                return GameUtilities.GetFromList(specific3Texts, number - 1);
            }
        }

        if (betOption.StartsWith("single_"))
        {
            string numberStr = betOption.Substring(7);
            if (int.TryParse(numberStr, out int number) && number >= 1 && number <= 6)
            {
                return GameUtilities.GetFromList(singleNumberTexts, number - 1);
            }
        }

        if (betOption.StartsWith("sum_"))
        {
            string numberStr = betOption.Substring(4);
            if (int.TryParse(numberStr, out int sum) && sum >= 4 && sum <= 17)
            {
                return GameUtilities.GetFromList(sumBetsTexts, sum - 4);
            }
        }

        return null;
    }

    private BetWager GetWagerForBetOption(string betOption)
    {
        if (wagersData == null)
            return null;

        if (betOption == "small") return wagersData.main_bets?.small;
        if (betOption == "big") return wagersData.main_bets?.big;
        if (betOption == "odd") return wagersData.main_bets?.odd;
        if (betOption == "even") return wagersData.main_bets?.even;

        if (betOption == "single_1") return wagersData.side_bets?.single_match_1;
        if (betOption == "single_2") return wagersData.side_bets?.single_match_2;
        if (betOption == "single_3") return wagersData.side_bets?.single_match_3;
        if (betOption == "single_4") return wagersData.side_bets?.single_match_1;
        if (betOption == "single_5") return wagersData.side_bets?.single_match_2;
        if (betOption == "single_6") return wagersData.side_bets?.single_match_3;

        if (betOption == "specific_2") return wagersData.side_bets?.specific_2;

        if (betOption.StartsWith("specific_3_"))
            return wagersData.side_bets?.specific_3;

        if (betOption == "sum_4") return wagersData.op_bets?.sum_4;
        if (betOption == "sum_5") return wagersData.op_bets?.sum_5;
        if (betOption == "sum_6") return wagersData.op_bets?.sum_6;
        if (betOption == "sum_7") return wagersData.op_bets?.sum_7;
        if (betOption == "sum_8") return wagersData.op_bets?.sum_8;
        if (betOption == "sum_9") return wagersData.op_bets?.sum_9;
        if (betOption == "sum_10") return wagersData.op_bets?.sum_10;
        if (betOption == "sum_11") return wagersData.op_bets?.sum_11;
        if (betOption == "sum_12") return wagersData.op_bets?.sum_12;
        if (betOption == "sum_13") return wagersData.op_bets?.sum_13;
        if (betOption == "sum_14") return wagersData.op_bets?.sum_14;
        if (betOption == "sum_15") return wagersData.op_bets?.sum_15;
        if (betOption == "sum_16") return wagersData.op_bets?.sum_16;
        if (betOption == "sum_17") return wagersData.op_bets?.sum_17;

        return null;
    }

    private double GetMinBetForRoom(string room)
    {
        if (betsData == null) return 0;

        return room switch
        {
            "casual" => betsData.casual != null && betsData.casual.Count > 0 ? betsData.casual[0] : 0,
            "novice" => betsData.novice != null && betsData.novice.Count > 0 ? betsData.novice[0] : 0,
            "expert" => betsData.expert != null && betsData.expert.Count > 0 ? betsData.expert[0] : 0,
            "high_roller" => betsData.high_roller != null && betsData.high_roller.Count > 0 ? betsData.high_roller[0] : 0,
            _ => 0
        };
    }
    #endregion
}