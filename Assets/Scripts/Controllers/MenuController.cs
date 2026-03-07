using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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
    [SerializeField] private BonusInfoDemoController bonusInfoDemo;

    [Header("Animation Settings")]
    [SerializeField] private GameObject mainArea;
    [SerializeField] private float popupDuration = 0.3f;
    [SerializeField] private AnimationCurve popupCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    #endregion

    #region Private Fields
    private int currentInfoPage = 0;
    private const int TOTAL_INFO_PAGES = 3;
    private Coroutine popupCoroutine;
    private bool hasPlayedBonusDemoThisSession = false;
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

        AddButtonPressAnimation(mainCloseButton, 0.95f);
        AddButtonPressAnimation(historyNavButton, 0.95f);
        AddButtonPressAnimation(infoNavButton, 0.95f);
        AddButtonPressAnimation(forwardButton, 1.2f);
        AddButtonPressAnimation(backwardButton, 1.2f);
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
    private void ShowMenu()
    {
        if (menuScreen == null) return;
        menuScreen.SetActive(true);
        if (popupCoroutine != null) StopCoroutine(popupCoroutine);
        popupCoroutine = StartCoroutine(PlayPopupAnimation(true));
    }

    private void HideMenu()
    {
        if (popupCoroutine != null) StopCoroutine(popupCoroutine);
        popupCoroutine = StartCoroutine(PlayPopupAnimation(false));
        hasPlayedBonusDemoThisSession = false;
     
    }

    private void HideAllPanels()
    {
        historyPanel?.SetActive(false);
        infoPanel?.SetActive(false);
    }
    #endregion

    #region Animation
    private IEnumerator PlayPopupAnimation(bool isOpening)
    {
        if (mainArea == null) yield break;

        float startScale = isOpening ? 0f : 1f;
        float endScale = isOpening ? 1f : 0f;
        float elapsed = 0f;

        mainArea.transform.localScale = Vector3.one * startScale;

        if (!isOpening)
        {
            float bounceScale = 1.1f;
            float bounceDuration = 0.15f;
            float bounceElapsed = 0f;

            while (bounceElapsed < bounceDuration)
            {
                bounceElapsed += Time.deltaTime;
                float bounceProgress = bounceElapsed / bounceDuration;
                float scale = Mathf.Lerp(1f, bounceScale, Mathf.Sin(bounceProgress * Mathf.PI));
                mainArea.transform.localScale = Vector3.one * scale;
                yield return null;
            }

            mainArea.transform.localScale = Vector3.one;
        }

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
            menuScreen?.SetActive(false);
            bonusInfoDemo?.ResetForNextOpen();
        }

        popupCoroutine = null;
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

        if (!hasPlayedBonusDemoThisSession)
        {
            hasPlayedBonusDemoThisSession = true;
            bonusInfoDemo?.PlayDemoOnce();
        }
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