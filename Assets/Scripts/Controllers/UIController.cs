using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Screens")]
    [SerializeField] private GameObject HomeScreen;
    [SerializeField] private GameObject GameScreen;

    [Header("Home Screen")]
    [SerializeField] private TMP_Text TotalPlayers_Text;
    [SerializeField] private TMP_Text PlayerName_Text;
    [SerializeField] private TMP_Text PlayerBalance_Text;
    [SerializeField] private Button CasualRoom_Button;
    [SerializeField] private Button NoviceRoom_Button;
    [SerializeField] private Button ExpertRoom_Button;
    [SerializeField] private Button HighRollerRoom_Button;
    [SerializeField] private TMP_Text CasualCount_Text;
    [SerializeField] private TMP_Text NoviceCount_Text;
    [SerializeField] private TMP_Text ExpertCount_Text;
    [SerializeField] private TMP_Text HighRollerCount_Text;
    [SerializeField] private TMP_Text CasualMin_Text;
    [SerializeField] private TMP_Text CasualMax_Text;
    [SerializeField] private TMP_Text NoviceMin_Text;
    [SerializeField] private TMP_Text NoviceMax_Text;
    [SerializeField] private TMP_Text ExpertMin_Text;
    [SerializeField] private TMP_Text ExpertMax_Text;
    [SerializeField] private TMP_Text HighRollerMin_Text;
    [SerializeField] private TMP_Text HighRollerMax_Text;
    [SerializeField] private Button HistoryHome_Button;
    [SerializeField] private Button SettingsHome_Button;
    [SerializeField] private Button ExitHome_Button;

    [Header("Game Screen")]
    [SerializeField] private TMP_Text GamePlayerName_Text;
    [SerializeField] private TMP_Text GamePlayerId_Text;
    [SerializeField] private TMP_Text RoundId_Text;
    [SerializeField] private TMP_Text GameBalance_Text;
    [SerializeField] private TMP_Text PlayerCount_Text;
    [SerializeField] private Button SideMenuOpen_Button;

    [Header("Side Menu")]
    [SerializeField] private Button SideMenuClose_Button;
    [SerializeField] private Button ExitGame_Button;
    [SerializeField] private Button HistoryGame_Button;
    [SerializeField] private Button SettingsGame_Button;
    [SerializeField] private Toggle Sound_button;
    [SerializeField] private Toggle Music_button;
    [SerializeField] private GameObject MenuPanel_Object;
    [SerializeField] private GameObject MenuPanelContainer_Object;

    [Header("Side Menu Animation")]
    [SerializeField] private float panelSlideDuration = 0.3f;
    [SerializeField] private float buttonDropDuration = 0.5f;
    [SerializeField] private float buttonDropDelay = 0.1f;
    [SerializeField] private float panelSlideDistance = 500f;

    [Header("Error Popup")]
    [SerializeField] private GameObject ErrorPopupParent;
    [SerializeField] private GameObject ErrorPopup;
    [SerializeField] private TMP_Text ErrorTitle_Text;
    [SerializeField] private TMP_Text ErrorMessage_Text;
    [SerializeField] private Button ErrorOK_Button;
    [SerializeField] private TMP_Text ErrorOKButton_Text;

    [Header("In-Game Popup")]
    [SerializeField] private GameObject InGamePopupParent;
    [SerializeField] private GameObject InGamePopup;
    [SerializeField] private TMP_Text InGameMessage_Text;

    [Header("Other Popups")]
    [SerializeField] private GameObject ReconnectPopupParent;
    [SerializeField] private GameObject ReconnectPopup;
    [SerializeField] private GameObject DisconnectPopupParent;
    [SerializeField] private GameObject DisconnectPopup;
    [SerializeField] private Button DisconnectOK_Button;
    [SerializeField] private GameObject QuitPopupParent;
    [SerializeField] private GameObject QuitPopup;
    [SerializeField] private Button QuitYes_Button;
    [SerializeField] private Button QuitNo_Button;

    [Header("Win Animation")]
    [SerializeField] private TMP_Text WinAmount_Text;
    [SerializeField] private GameObject WinPanel;
    [SerializeField] private RectTransform BalanceTarget;
    [SerializeField] private float winHoldDuration = 1.2f;
    [SerializeField] private float winFlyDuration = 0.5f;
    [SerializeField] private float winFlyDistance = 200f;

    [Header("Loading Screen")]
    [SerializeField] private GameObject LoadingScreen_Object;
    [SerializeField] private TMP_Text LoadingMessage_Text;

    [Header("Player Avatar")]
    [SerializeField] private Image PlayerAvatar_HomeScreen;
    [SerializeField] private Image PlayerAvatar_GameScreen;
    [SerializeField] private Sprite[] playerAvatarSprites;

    [Header("Animation Settings")]
    [SerializeField] private float slideDistance = 1000f;
    [SerializeField] private float slideDuration = 0.3f;
    [SerializeField] private float inGamePopupDisplayTime = 1f;

    [Header("Controllers")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private MenuController menuController;
    [SerializeField] private BetTimerController betTimerController;
    [SerializeField] private LeaderboardController leaderboardController;
    [SerializeField] private JSFunctCalls jsFunctCalls;

    [Header("Stats Display")]
    [SerializeField] private TMP_Text StatsRoundCount_Text;
    [SerializeField] private TMP_Text StatsDice1_Text;
    [SerializeField] private TMP_Text StatsDice2_Text;
    [SerializeField] private TMP_Text StatsDice3_Text;
    [SerializeField] private TMP_Text StatsDice4_Text;
    [SerializeField] private TMP_Text StatsDice5_Text;
    [SerializeField] private TMP_Text StatsDice6_Text;
    [SerializeField] private TMP_Text StatsSmall_Text;
    [SerializeField] private TMP_Text StatsBig_Text;
    [SerializeField] private TMP_Text StatsOdd_Text;
    [SerializeField] private TMP_Text StatsEven_Text;

    [Header("Win Ratio Display")]
    [SerializeField] private TMP_Text WRMainBets_Text;
    [SerializeField] private TMP_Text WRSingleMatch1_Text;
    [SerializeField] private TMP_Text WRSingleMatch2_Text;
    [SerializeField] private TMP_Text WRSingleMatch3_Text;
    [SerializeField] private TMP_Text WRSpecific2_Text;
    [SerializeField] private TMP_Text WRSpecific3_Text;
    [SerializeField] private TMP_Text WRSum4_Text;
    [SerializeField] private TMP_Text WRSum5_Text;
    [SerializeField] private TMP_Text WRSum6_Text;
    [SerializeField] private TMP_Text WRSum7_Text;
    [SerializeField] private TMP_Text WRSum8_Text;
    [SerializeField] private TMP_Text WRSum9_Text;
    [SerializeField] private TMP_Text WRSum10_Text;
    [SerializeField] private TMP_Text WRSum11_Text;
    [SerializeField] private TMP_Text WRSum12_Text;
    [SerializeField] private TMP_Text WRSum13_Text;
    [SerializeField] private TMP_Text WRSum14_Text;
    [SerializeField] private TMP_Text WRSum15_Text;
    [SerializeField] private TMP_Text WRSum16_Text;
    [SerializeField] private TMP_Text WRSum17_Text;


    [Header("Expand / Shrink – Home Screen")]
    [SerializeField] private Button ExpandHome_Button;
    [SerializeField] private Button ShrinkHome_Button;

    [Header("Expand / Shrink – Menu Screen")]
    [SerializeField] private Button ExpandMenu_Button;
    [SerializeField] private Button ShrinkMenu_Button;

    [Header("Expand / Shrink – Side Menu")]
    [SerializeField] private Button ExpandSideMenu_Button;
    [SerializeField] private Button ShrinkSideMenu_Button;

    [Header("Lobby Hot Indicators")]
    [SerializeField] private GameObject CasualHot_Object;
    [SerializeField] private GameObject NoviceHot_Object;
    [SerializeField] private GameObject ExpertHot_Object;
    [SerializeField] private GameObject HighRollerHot_Object;

    [Header("Lobby Player Count Backgrounds")]
    [SerializeField] private GameObject CasualCountBg_Object;
    [SerializeField] private GameObject NoviceCountBg_Object;
    [SerializeField] private GameObject ExpertCountBg_Object;
    [SerializeField] private GameObject HighRollerCountBg_Object;
    [SerializeField] private Image CasualCountBg_Image;
    [SerializeField] private Image NoviceCountBg_Image;
    [SerializeField] private Image ExpertCountBg_Image;
    [SerializeField] private Image HighRollerCountBg_Image;
    [SerializeField] private Sprite normalCountBgSprite;
    [SerializeField] private Sprite hotCountBgSprite;


    #endregion

    #region Private Fields
    private Tween winTween;
    private Tween currentPopupTween;
    private Coroutine inGamePopupCoroutine;
    private string playerName;
    private Wagers gameWagers;
    private Bets gameBets;
    private bool isAnotherDeviceError = false;
    private bool hasPendingDisconnect = false;
    private RectTransform[] menuButtonRects;
    private Vector2[] menuButtonOriginalPositions;
    private RectTransform menuPanelContainerRect;
    private Vector2 panelOriginalPosition;
    private int selectedAvatarIndex = -1;
    private bool isExpanded = false;
    private Vector2 expandSideMenuOriginalPosition;
    private Vector2 shrinkSideMenuOriginalPosition;
    private readonly List<RectTransform> _animatingRects = new List<RectTransform>();
    private readonly List<Vector2> _animatingOrigPos = new List<Vector2>();
    private RectTransform winPanelRT;
    private Vector2 winPanelOriginalPos;
    private readonly List<RectTransform> _visibleRects = new List<RectTransform>();
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        SetupButtonListeners();
        ShowHomeScreen();
        InitializePopups();
        InitializeSideMenuAnimation();
        InitializeExpandShrink();

        if (LoadingScreen_Object != null) LoadingScreen_Object.SetActive(false);

        if (playerAvatarSprites != null && playerAvatarSprites.Length > 0)
        {
            selectedAvatarIndex = Random.Range(0, playerAvatarSprites.Length);
            UpdatePlayerAvatars();
        }

        leaderboardController?.Initialize();
        RegisterFullscreenListener();
    }

    private void OnDestroy()
    {
        winTween?.Kill();
        currentPopupTween?.Kill();
        if (inGamePopupCoroutine != null) StopCoroutine(inGamePopupCoroutine);
    }
    #endregion

    #region Setup
    private void SetupButtonListeners()
    {
        void Bind(Button btn, System.Action action)
        {
            if (btn) btn.onClick.AddListener(() => action());
        }

        Bind(CasualRoom_Button, () => { AudioManager.Instance?.PlayLobbyButton(); gameManager.JoinRoom("casual"); });
        Bind(NoviceRoom_Button, () => { AudioManager.Instance?.PlayLobbyButton(); gameManager.JoinRoom("novice"); });
        Bind(ExpertRoom_Button, () => { AudioManager.Instance?.PlayLobbyButton(); gameManager.JoinRoom("expert"); });
        Bind(HighRollerRoom_Button, () => { AudioManager.Instance?.PlayLobbyButton(); gameManager.JoinRoom("high_roller"); });
        Bind(HistoryHome_Button, () => { AudioManager.Instance?.PlayButtonClick(); OpenHistoryFromHome(); });
        Bind(SettingsHome_Button, () => { AudioManager.Instance?.PlayButtonClick(); OpenInfoFromHome(); });
        Bind(ExitHome_Button, () => { AudioManager.Instance?.PlayButtonClick(); ShowQuitPopup(); });
        Bind(SideMenuOpen_Button, () => { AudioManager.Instance?.PlayButtonClick(); OpenSideMenu(); });
        Bind(SideMenuClose_Button, () => { AudioManager.Instance?.PlayButtonClick(); CloseSideMenu(); });
        Bind(ExitGame_Button, () => { AudioManager.Instance?.PlayButtonClick(); CloseSideMenu(); gameManager.LeaveRoom(); });
        Bind(HistoryGame_Button, () => { AudioManager.Instance?.PlayButtonClick(); CloseSideMenu(); OpenHistoryFromGame(); });
        Bind(SettingsGame_Button, () => { AudioManager.Instance?.PlayButtonClick(); CloseSideMenu(); OpenInfoFromGame(); });
        Bind(ErrorOK_Button, () => { AudioManager.Instance?.PlayButtonClick(); OnErrorOK(); });
        Bind(DisconnectOK_Button, () => { AudioManager.Instance?.PlayButtonClick(); CloseDisconnectPopup(); gameManager.ExitGame(); });
        Bind(QuitYes_Button, () => { AudioManager.Instance?.PlayButtonClick(); CloseQuitPopup(); gameManager.ExitGame(); });
        Bind(QuitNo_Button, () => { AudioManager.Instance?.PlayButtonClick(); CloseQuitPopup(); });

        Bind(ExpandHome_Button, () => { AudioManager.Instance?.PlayButtonClick(); OnExpand(); });
        Bind(ShrinkHome_Button, () => { AudioManager.Instance?.PlayButtonClick(); OnShrink(); });
        Bind(ExpandMenu_Button, () => { AudioManager.Instance?.PlayButtonClick(); OnExpand(); });
        Bind(ShrinkMenu_Button, () => { AudioManager.Instance?.PlayButtonClick(); OnShrink(); });
        Bind(ExpandSideMenu_Button, () => { AudioManager.Instance?.PlayButtonClick(); OnExpand(); });
        Bind(ShrinkSideMenu_Button, () => { AudioManager.Instance?.PlayButtonClick(); OnShrink(); });

        // Setup background panel click to close side menu
        SetupMenuPanelBackgroundClick();

        AddButtonPressAnimation(CasualRoom_Button, 0.95f);
        AddButtonPressAnimation(NoviceRoom_Button, 0.95f);
        AddButtonPressAnimation(ExpertRoom_Button, 0.95f);
        AddButtonPressAnimation(HighRollerRoom_Button, 0.95f);
        AddButtonPressAnimation(HistoryHome_Button, 0.95f);
        AddButtonPressAnimation(SettingsHome_Button, 0.95f);
        AddButtonPressAnimation(SideMenuOpen_Button, 0.95f);
        AddButtonPressAnimation(SideMenuClose_Button, 0.95f);
        AddButtonPressAnimation(ExitGame_Button, 0.95f);
        AddButtonPressAnimation(HistoryGame_Button, 0.95f);
        AddButtonPressAnimation(SettingsGame_Button, 0.95f);
        AddButtonPressAnimation(ErrorOK_Button, 0.95f);
        AddButtonPressAnimation(DisconnectOK_Button, 0.95f);
        AddButtonPressAnimation(QuitYes_Button, 0.95f);
        AddButtonPressAnimation(QuitNo_Button, 0.95f);
        AddButtonPressAnimation(ExpandHome_Button, 0.95f);
        AddButtonPressAnimation(ShrinkHome_Button, 0.95f);
        AddButtonPressAnimation(ExpandMenu_Button, 0.95f);
        AddButtonPressAnimation(ShrinkMenu_Button, 0.95f);
        AddButtonPressAnimation(ExpandSideMenu_Button, 0.95f);
        AddButtonPressAnimation(ShrinkSideMenu_Button, 0.95f);
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

    private void SetupMenuPanelBackgroundClick()
    {
        if (MenuPanel_Object == null) return;

        // Ensure MenuPanel has a Button component to detect clicks
        Button bgButton = MenuPanel_Object.GetComponent<Button>();
        if (bgButton == null)
            bgButton = MenuPanel_Object.AddComponent<Button>();

        // Ensure MenuPanel has an Image component for raycast target
        Image bgImage = MenuPanel_Object.GetComponent<Image>();
        if (bgImage == null)
        {
            bgImage = MenuPanel_Object.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.01f); // Nearly transparent but still clickable
        }

        // Remove any existing listeners and add close menu action
        bgButton.onClick.RemoveAllListeners();
        bgButton.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayButtonClick();
            CloseSideMenu();
        });

        // Make sure MenuPanelContainer blocks raycasts so clicks inside the menu don't close it
        if (MenuPanelContainer_Object != null)
        {
            CanvasGroup containerGroup = MenuPanelContainer_Object.GetComponent<CanvasGroup>();
            if (containerGroup == null)
                containerGroup = MenuPanelContainer_Object.AddComponent<CanvasGroup>();

            containerGroup.blocksRaycasts = true; // Block clicks from reaching the background
        }
    }

    private void InitializePopups()
    {
        HidePopupImmediate(ErrorPopupParent, ErrorPopup);
        HidePopupImmediate(InGamePopupParent, InGamePopup);
        HidePopupImmediate(ReconnectPopupParent, ReconnectPopup);
        HidePopupImmediate(DisconnectPopupParent, DisconnectPopup);
        HidePopupImmediate(QuitPopupParent, QuitPopup);

        if (WinPanel)
        {
            winPanelRT = WinPanel.GetComponent<RectTransform>();
            if (winPanelRT != null) winPanelOriginalPos = winPanelRT.anchoredPosition;
            WinPanel.SetActive(false);
        }
        if (WinAmount_Text) WinAmount_Text.gameObject.SetActive(false);
    }

    internal void SetupInitialData(string name, double balance, Leaderboards leaderboards, Wagers wagers, Bets bets)
    {
        playerName = name;
        gameWagers = wagers;
        gameBets = bets;

        if (PlayerName_Text) PlayerName_Text.text = name;
        if (GamePlayerName_Text) GamePlayerName_Text.text = name;
        if (GamePlayerId_Text) GamePlayerId_Text.text = $"ID: {name}";
        UpdateBalance(balance);

        if (leaderboardController != null &&
            playerAvatarSprites != null &&
            selectedAvatarIndex >= 0 &&
            selectedAvatarIndex < playerAvatarSprites.Length)
        {
            leaderboardController.SetLocalPlayer(name, playerAvatarSprites[selectedAvatarIndex]);
        }

        if (leaderboards != null &&
            ((leaderboards.richest != null && leaderboards.richest.Count > 0) ||
             (leaderboards.winners != null && leaderboards.winners.Count > 0)))
        {
            UpdateLeaderboards(leaderboards);
        }

        UpdateLobbyMinMaxDisplay();
    }
    #endregion

    #region Side Menu Animation
    private void InitializeSideMenuAnimation()
    {
        if (MenuPanelContainer_Object)
        {
            menuPanelContainerRect = MenuPanelContainer_Object.GetComponent<RectTransform>();
            if (menuPanelContainerRect != null)
                panelOriginalPosition = menuPanelContainerRect.anchoredPosition;
        }

        var tempRects = new List<RectTransform>();
        if (HistoryGame_Button) tempRects.Add(HistoryGame_Button.GetComponent<RectTransform>());
        if (SettingsGame_Button) tempRects.Add(SettingsGame_Button.GetComponent<RectTransform>());
        if (Sound_button) tempRects.Add(Sound_button.GetComponent<RectTransform>());
        if (Music_button) tempRects.Add(Music_button.GetComponent<RectTransform>());
        if (ExitGame_Button) tempRects.Add(ExitGame_Button.GetComponent<RectTransform>());

        menuButtonRects = tempRects.ToArray();
        menuButtonOriginalPositions = new Vector2[menuButtonRects.Length];
        for (int i = 0; i < menuButtonRects.Length; i++)
            menuButtonOriginalPositions[i] = menuButtonRects[i].anchoredPosition;
        if (ExpandSideMenu_Button)
        {
            RectTransform rect = ExpandSideMenu_Button.GetComponent<RectTransform>();
            if (rect != null) expandSideMenuOriginalPosition = rect.anchoredPosition;
        }
        if (ShrinkSideMenu_Button)
        {
            RectTransform rect = ShrinkSideMenu_Button.GetComponent<RectTransform>();
            if (rect != null) shrinkSideMenuOriginalPosition = rect.anchoredPosition;
        }

        if (MenuPanel_Object) MenuPanel_Object.SetActive(false);
    }

    private void OpenSideMenu()
    {
        if (MenuPanel_Object) MenuPanel_Object.SetActive(true);

        if (SideMenuOpen_Button) SideMenuOpen_Button.interactable = false;


        if (menuPanelContainerRect != null)
        {
            menuPanelContainerRect.DOKill();
            Vector2 startPos = panelOriginalPosition;
            startPos.x += panelSlideDistance;
            menuPanelContainerRect.anchoredPosition = startPos;
            menuPanelContainerRect.DOAnchorPos(panelOriginalPosition, panelSlideDuration).SetEase(Ease.OutCubic);
        }

        Vector2 closeButtonPos = Vector2.zero;
        if (SideMenuClose_Button)
        {
            RectTransform r = SideMenuClose_Button.GetComponent<RectTransform>();
            if (r != null) closeButtonPos = r.anchoredPosition;
        }
        Button activeExpandShrinkButton = isExpanded ? ShrinkSideMenu_Button : ExpandSideMenu_Button;
        Button inactiveExpandShrinkButton = isExpanded ? ExpandSideMenu_Button : ShrinkSideMenu_Button;

        if (inactiveExpandShrinkButton != null)
        {
            RectTransform inactiveRect = inactiveExpandShrinkButton.GetComponent<RectTransform>();
            if (inactiveRect != null)
            {
                Vector2 inactiveOriginalPos = (inactiveExpandShrinkButton == ExpandSideMenu_Button)
                    ? expandSideMenuOriginalPosition
                    : shrinkSideMenuOriginalPosition;
                inactiveRect.anchoredPosition = inactiveOriginalPos;
            }
            inactiveExpandShrinkButton.gameObject.SetActive(false);
        }
        if (activeExpandShrinkButton != null)
        {
            activeExpandShrinkButton.gameObject.SetActive(true);
            activeExpandShrinkButton.interactable = true;
        }

        _animatingRects.Clear();
        _animatingOrigPos.Clear();
        for (int i = 0; i < menuButtonRects.Length; i++)
        {
            if (menuButtonRects[i] != null)
            {
                menuButtonRects[i].gameObject.SetActive(true);
                _animatingRects.Add(menuButtonRects[i]);
                _animatingOrigPos.Add(menuButtonOriginalPositions[i]);
            }
        }

        if (activeExpandShrinkButton != null)
        {
            RectTransform expandShrinkRect = activeExpandShrinkButton.GetComponent<RectTransform>();
            if (expandShrinkRect != null)
            {
                Vector2 expandShrinkOriginalPos = (activeExpandShrinkButton == ExpandSideMenu_Button)
                    ? expandSideMenuOriginalPosition
                    : shrinkSideMenuOriginalPosition;

                _animatingRects.Add(expandShrinkRect);
                _animatingOrigPos.Add(expandShrinkOriginalPos);
            }
        }
        for (int i = 0; i < _animatingRects.Count; i++)
        {
            _animatingRects[i].DOKill();
            _animatingRects[i].anchoredPosition = closeButtonPos;
            _animatingRects[i].DOAnchorPos(_animatingOrigPos[i], buttonDropDuration)
                .SetEase(Ease.OutCubic)
                .SetDelay(i * buttonDropDelay);
        }
    }

    private void CloseSideMenu()
    {
        Vector2 closeButtonPos = Vector2.zero;
        if (SideMenuClose_Button)
        {
            RectTransform r = SideMenuClose_Button.GetComponent<RectTransform>();
            if (r != null) closeButtonPos = r.anchoredPosition;
        }
        _visibleRects.Clear();

        for (int i = 0; i < menuButtonRects.Length; i++)
        {
            if (menuButtonRects[i] != null && menuButtonRects[i].gameObject.activeSelf)
                _visibleRects.Add(menuButtonRects[i]);
        }
        Button visibleExpandShrinkButton = isExpanded ? ShrinkSideMenu_Button : ExpandSideMenu_Button;
        if (visibleExpandShrinkButton != null && visibleExpandShrinkButton.gameObject.activeSelf)
        {
            RectTransform expandShrinkRect = visibleExpandShrinkButton.GetComponent<RectTransform>();
            if (expandShrinkRect != null)
                _visibleRects.Add(expandShrinkRect);
        }
        for (int i = 0; i < _visibleRects.Count; i++)
        {
            int reverseIndex = _visibleRects.Count - 1 - i;
            RectTransform rect = _visibleRects[reverseIndex];
            rect.DOKill();
            rect.DOAnchorPos(closeButtonPos, buttonDropDuration * 0.7f)
                .SetEase(Ease.InCubic)
                .SetDelay(i * buttonDropDelay)
                .OnComplete(() => rect.gameObject.SetActive(false));
        }

        float totalButtonTime = (_visibleRects.Count - 1) * buttonDropDelay + buttonDropDuration * 0.7f;
        if (menuPanelContainerRect != null)
        {
            Vector2 endPos = panelOriginalPosition;
            endPos.x += panelSlideDistance;
            menuPanelContainerRect.DOAnchorPos(endPos, panelSlideDuration)
                .SetEase(Ease.InCubic)
                .SetDelay(totalButtonTime)
                .OnComplete(() =>
                {
                    if (MenuPanel_Object) MenuPanel_Object.SetActive(false);
                    if (SideMenuOpen_Button) SideMenuOpen_Button.interactable = true;
                });
        }
        else
        {

            DOVirtual.DelayedCall(totalButtonTime, () =>
            {
                if (SideMenuOpen_Button) SideMenuOpen_Button.interactable = true;
            });
        }
    }
    #endregion

    #region Expand / Shrink

    private void InitializeExpandShrink()
    {

        SetExpandShrinkButtons(isExpanded: false);
    }

    private void OnExpand()
    {
        isExpanded = true;
        jsFunctCalls?.RequestExpandGame();
        SetExpandShrinkButtons(isExpanded: true);
    }

    private void OnShrink()
    {
        isExpanded = false;
        jsFunctCalls?.RequestShrinkGame();
        SetExpandShrinkButtons(isExpanded: false);
    }


    private void SetExpandShrinkButtons(bool isExpanded)
    {
        if (ExpandHome_Button) ExpandHome_Button.gameObject.SetActive(!isExpanded);
        if (ShrinkHome_Button) ShrinkHome_Button.gameObject.SetActive(isExpanded);
        if (ExpandMenu_Button) ExpandMenu_Button.gameObject.SetActive(!isExpanded);
        if (ShrinkMenu_Button) ShrinkMenu_Button.gameObject.SetActive(isExpanded);
        if (ExpandSideMenu_Button)
        {
            RectTransform rect = ExpandSideMenu_Button.GetComponent<RectTransform>();
            if (rect != null) rect.anchoredPosition = expandSideMenuOriginalPosition;
            ExpandSideMenu_Button.gameObject.SetActive(!isExpanded);
            ExpandSideMenu_Button.interactable = !isExpanded;
        }
        if (ShrinkSideMenu_Button)
        {
            RectTransform rect = ShrinkSideMenu_Button.GetComponent<RectTransform>();
            if (rect != null) rect.anchoredPosition = shrinkSideMenuOriginalPosition;
            ShrinkSideMenu_Button.gameObject.SetActive(isExpanded);
            ShrinkSideMenu_Button.interactable = isExpanded;
        }
    }

    private void RegisterFullscreenListener()
    {
        jsFunctCalls?.RegisterFullscreenListener(gameObject.name);
    }

    internal void OnFullscreenChanged(string isFullscreen)
    {
        bool newExpandedState = isFullscreen == "1";
        Debug.Log($"[UI] OnFullscreenChanged callback: isFullscreen={isFullscreen}, newState={newExpandedState}");

        // Only update if state actually changed
        if (isExpanded != newExpandedState)
        {
            isExpanded = newExpandedState;
            SetExpandShrinkButtons(isExpanded);
            Debug.Log($"[UI] Button states synced to fullscreen: {(isExpanded ? "EXPANDED" : "SHRINK")}");
        }
    }
    #endregion

    #region Screen Management
    private void HideAllScreens()
    {
        if (HomeScreen) HomeScreen.SetActive(false);
        if (GameScreen) GameScreen.SetActive(false);
    }

    internal void ShowHomeScreen()
    {
        HideAllScreens();
        if (HomeScreen) HomeScreen.SetActive(true);
        if (selectedAvatarIndex >= 0) UpdatePlayerAvatars();
    }

    internal void ShowGameScreen()
    {
        HideAllScreens();
        if (GameScreen) GameScreen.SetActive(true);
        if (selectedAvatarIndex >= 0) UpdatePlayerAvatars();
    }
    #endregion

    #region Popup Helpers
    private void HidePopupImmediate(GameObject parent, GameObject popup)
    {
        if (parent) parent.SetActive(false);
        if (popup) popup.SetActive(false);
    }

    private void SlideInPopup(GameObject parent, GameObject popup)
    {
        if (!parent || !popup) return;
        currentPopupTween?.Kill();
        parent.SetActive(true);
        popup.SetActive(true);
        RectTransform rt = popup.GetComponent<RectTransform>();
        if (rt == null) return;
        Vector2 startPos = rt.anchoredPosition;
        startPos.x = -slideDistance;
        rt.anchoredPosition = startPos;
        currentPopupTween = rt.DOAnchorPos(new Vector2(0, startPos.y), slideDuration).SetEase(Ease.OutCubic);
    }

    private void SlideOutPopup(GameObject parent, GameObject popup, System.Action onComplete = null)
    {
        if (!parent || !popup) return;
        currentPopupTween?.Kill();
        RectTransform rt = popup.GetComponent<RectTransform>();
        if (rt == null) { HidePopupImmediate(parent, popup); onComplete?.Invoke(); return; }
        Vector2 endPos = rt.anchoredPosition;
        endPos.x = slideDistance;
        currentPopupTween = rt.DOAnchorPos(endPos, slideDuration)
            .SetEase(Ease.InCubic)
            .OnComplete(() => { HidePopupImmediate(parent, popup); onComplete?.Invoke(); });
    }
    #endregion

    #region Error Popup
    internal void ShowErrorPopup(string message, string title = "Error", bool showDisconnectAfter = false)
    {
        if (DisconnectPopupParent && DisconnectPopupParent.activeSelf)
        {
            HidePopupImmediate(DisconnectPopupParent, DisconnectPopup);
        }

        hasPendingDisconnect = showDisconnectAfter;
        if (ErrorOKButton_Text) ErrorOKButton_Text.text = showDisconnectAfter ? "Exit" : "OK";

        if (ErrorTitle_Text) ErrorTitle_Text.text = title;
        if (ErrorMessage_Text) ErrorMessage_Text.text = message;
        AudioManager.Instance?.PlayPopupOpen();
        SlideInPopup(ErrorPopupParent, ErrorPopup);
    }

    private void CloseErrorPopup(System.Action onComplete = null) => SlideOutPopup(ErrorPopupParent, ErrorPopup, onComplete);

    private void OnErrorOK()
    {
        if (isAnotherDeviceError)
        {
            isAnotherDeviceError = false;
            CloseErrorPopup(() => gameManager.ExitGame());
        }
        else if (hasPendingDisconnect)
        {
            hasPendingDisconnect = false;
            if (ErrorOKButton_Text) ErrorOKButton_Text.text = "OK";
            CloseErrorPopup(() => gameManager.ExitGame());
        }
        else
        {
            CloseErrorPopup();
        }
    }
    #endregion

    #region In-Game Popup
    internal void ShowInGamePopup(string message)
    {
        if (inGamePopupCoroutine != null) StopCoroutine(inGamePopupCoroutine);
        if (InGameMessage_Text) InGameMessage_Text.text = message;
        AudioManager.Instance?.PlayPopupOpen();
        SlideInPopup(InGamePopupParent, InGamePopup);
        inGamePopupCoroutine = StartCoroutine(CloseInGamePopupAfterDelay());
    }

    private IEnumerator CloseInGamePopupAfterDelay()
    {
        yield return new WaitForSeconds(inGamePopupDisplayTime);
        SlideOutPopup(InGamePopupParent, InGamePopup);
        inGamePopupCoroutine = null;
    }
    #endregion

    #region Reconnect Popup
    internal void ShowReconnectPopup()
    {
        AudioManager.Instance?.PlayPopupOpen();
        SlideInPopup(ReconnectPopupParent, ReconnectPopup);
    }

    internal void CloseReconnectPopup() => SlideOutPopup(ReconnectPopupParent, ReconnectPopup);
    #endregion

    #region Disconnect Popup
    internal void ShowDisconnectPopup()
    {
        if (ReconnectPopupParent && ReconnectPopupParent.activeSelf)
            SlideOutPopup(ReconnectPopupParent, ReconnectPopup);

        AudioManager.Instance?.PlayPopupOpen();
        SlideInPopup(DisconnectPopupParent, DisconnectPopup);
    }

    private void CloseDisconnectPopup() => SlideOutPopup(DisconnectPopupParent, DisconnectPopup);
    #endregion

    #region Quit Popup
    private void ShowQuitPopup()
    {
        AudioManager.Instance?.PlayPopupOpen();
        SlideInPopup(QuitPopupParent, QuitPopup);
    }

    private void CloseQuitPopup() => SlideOutPopup(QuitPopupParent, QuitPopup);
    #endregion

    #region Another Device Popup
    internal void ShowAnotherDevicePopup()
    {
        isAnotherDeviceError = true;
        ShowErrorPopup("Another device has logged in with your account.", "Another Login Detected");
    }
    #endregion

    #region Menu Panels
    private void OpenHistoryFromHome() => menuController?.OpenMenuWithHistory();
    private void OpenHistoryFromGame() => menuController?.OpenMenuWithHistory();
    private void OpenInfoFromHome() => menuController?.OpenMenuWithInfo();
    private void OpenInfoFromGame() => menuController?.OpenMenuWithInfo();
    #endregion

    #region Data Updates
    internal void UpdateBalance(double balance)
    {
        string text = balance % 1 == 0 ? balance.ToString("F0") : balance.ToString("0.##");
        if (PlayerBalance_Text) PlayerBalance_Text.text = text;
        if (GameBalance_Text) GameBalance_Text.text = text;
    }

    internal void UpdateTimer(int secondsRemaining) => betTimerController?.UpdateBettingTimer(secondsRemaining);

    internal void UpdateRoundId(string roundId)
    {
        if (RoundId_Text)
            RoundId_Text.text = string.IsNullOrEmpty(roundId) ? "Waiting..." : $"RoundID : {roundId}";
    }

    internal void ClearRoundId() { if (RoundId_Text) RoundId_Text.text = "---"; }

    internal void UpdateTotalPlayerCount(int total) { if (TotalPlayers_Text) TotalPlayers_Text.text = total.ToString(); }
    internal void UpdatePlayerCountInLevel(int count) { if (PlayerCount_Text) PlayerCount_Text.text = count.ToString(); }

    internal void UpdateLobbyPlayerCounts(int casual = 0, int novice = 0, int expert = 0, int highRoller = 0)
    {
        if (CasualCount_Text) CasualCount_Text.text = $"{casual} ";
        if (NoviceCount_Text) NoviceCount_Text.text = $"{novice} ";
        if (ExpertCount_Text) ExpertCount_Text.text = $"{expert} ";
        if (HighRollerCount_Text) HighRollerCount_Text.text = $"{highRoller} ";

        // Update hot indicators and backgrounds for each lobby
        UpdateLobbyHotIndicators(casual, novice, expert, highRoller);
    }

    /// <summary>
    /// Updates the "hot" indicators and player count backgrounds based on which lobby has the highest player count.
    /// Hot is only shown when exactly one lobby has the highest count (no ties).
    /// When all non-zero lobbies share the same count, everyone gets the green/normal sprite and hot is hidden.
    /// </summary>
    internal void UpdateLobbyHotIndicators(int casual = 0, int novice = 0, int expert = 0, int highRoller = 0)
    {
        // Find the maximum count
        int maxCount = Mathf.Max(casual, novice, expert, highRoller);

        // Count how many lobbies share the maximum (to detect ties like 1,1,1,0)
        int tiedAtMax = 0;
        if (casual == maxCount) tiedAtMax++;
        if (novice == maxCount) tiedAtMax++;
        if (expert == maxCount) tiedAtMax++;
        if (highRoller == maxCount) tiedAtMax++;

        // Hot is only valid when exactly ONE lobby has the highest count and it has players.
        // If two or more lobbies share the top count (tie), nobody gets the hot/red treatment.
        bool hotEnabled = maxCount > 0 && tiedAtMax == 1;

        bool casualIsHot = hotEnabled && casual == maxCount;
        bool noviceIsHot = hotEnabled && novice == maxCount;
        bool expertIsHot = hotEnabled && expert == maxCount;
        bool highRollerIsHot = hotEnabled && highRoller == maxCount;

        // Update hot indicators and backgrounds for each level
        UpdateLobbyLevelUI("Casual", casual, casualIsHot, CasualHot_Object, CasualCountBg_Object, CasualCountBg_Image);
        UpdateLobbyLevelUI("Novice", novice, noviceIsHot, NoviceHot_Object, NoviceCountBg_Object, NoviceCountBg_Image);
        UpdateLobbyLevelUI("Expert", expert, expertIsHot, ExpertHot_Object, ExpertCountBg_Object, ExpertCountBg_Image);
        UpdateLobbyLevelUI("HighRoller", highRoller, highRollerIsHot, HighRollerHot_Object, HighRollerCountBg_Object, HighRollerCountBg_Image);
    }

    /// <summary>
    /// Updates UI elements for a specific lobby level.
    /// </summary>
    private void UpdateLobbyLevelUI(string levelName, int playerCount, bool isHot, GameObject hotObject, GameObject countBgObject, Image countBgImage)
    {
        // Update hot indicator - only show if lobby has players and is the highest
        if (hotObject != null)
            hotObject.SetActive(isHot && playerCount > 0);

        // Update player count background GameObject - disable entire object when no players
        if (countBgObject != null)
        {
            if (playerCount > 0)
            {
                countBgObject.SetActive(true);

                // Update sprite based on hot status
                if (countBgImage != null)
                {
                    if (isHot)
                        countBgImage.sprite = hotCountBgSprite;
                    else
                        countBgImage.sprite = normalCountBgSprite;
                }
            }
            else
            {
                // Disable entire background GameObject when no players
                countBgObject.SetActive(false);
            }
        }
    }

    internal void UpdateLeaderboards(Leaderboards leaderboards) => leaderboardController?.UpdateLeaderboard(leaderboards);

    internal void UpdateStats(StatsResult stats)
    {
        if (stats == null) return;

        if (StatsRoundCount_Text)
        {
            StatsRoundCount_Text.text = $"Last {stats.totalRounds} Rounds";
            AnimateStatText(StatsRoundCount_Text, 0f);
        }

        TMP_Text[] diceTexts = { StatsDice1_Text, StatsDice2_Text, StatsDice3_Text, StatsDice4_Text, StatsDice5_Text, StatsDice6_Text };
        for (int i = 0; i < diceTexts.Length; i++)
        {
            if (diceTexts[i] == null) continue;
            diceTexts[i].text = $"{stats.dicePct[i]}%";
            diceTexts[i].color = stats.dicePct[i] >= 18 ? Color.red : Color.black;
            AnimateStatText(diceTexts[i], (i + 1) * 0.04f);
        }

        float baseDelay = 7 * 0.04f;

        if (StatsSmall_Text) { StatsSmall_Text.text = $"{stats.smallPct}%"; AnimateStatText(StatsSmall_Text, baseDelay); }
        if (StatsBig_Text) { StatsBig_Text.text = $"{stats.bigPct}%"; AnimateStatText(StatsBig_Text, baseDelay + 0.04f); }
        if (StatsOdd_Text) { StatsOdd_Text.text = $"{stats.oddPct}%"; AnimateStatText(StatsOdd_Text, baseDelay + 0.08f); }
        if (StatsEven_Text) { StatsEven_Text.text = $"{stats.evenPct}%"; AnimateStatText(StatsEven_Text, baseDelay + 0.12f); }
    }

    private void AnimateStatText(TMP_Text text, float delay)
    {
        text.transform.DOKill();
        text.transform.localScale = Vector3.one;
        text.transform.DOPunchScale(Vector3.one * 0.35f, 0.3f, 1, 0.5f).SetDelay(delay);
    }

    internal void SetupWinRatios(Wagers wagers)
    {
        if (wagers == null) return;

        if (WRMainBets_Text) WRMainBets_Text.text = wagers.main_bets?.small?.GetPayoutRatioString() ?? "1 : 1";
        if (WRSingleMatch1_Text) WRSingleMatch1_Text.text = wagers.side_bets?.single_match_1?.GetPayoutRatioString() ?? "1 : 1";
        if (WRSingleMatch2_Text) WRSingleMatch2_Text.text = wagers.side_bets?.single_match_2?.GetPayoutRatioString() ?? "1 : 1";
        if (WRSingleMatch3_Text) WRSingleMatch3_Text.text = wagers.side_bets?.single_match_3?.GetPayoutRatioString() ?? "1 : 1";
        if (WRSpecific2_Text) WRSpecific2_Text.text = wagers.side_bets?.specific_2?.GetPayoutRatioString() ?? "1 : 1";
        if (WRSpecific3_Text) WRSpecific3_Text.text = wagers.side_bets?.specific_3?.GetPayoutRatioString() ?? "1 : 1";
        if (WRSum4_Text) WRSum4_Text.text = wagers.op_bets?.sum_4?.GetPayoutRatioString() ?? "1 : 1";
        if (WRSum5_Text) WRSum5_Text.text = wagers.op_bets?.sum_5?.GetPayoutRatioString() ?? "1 : 1";
        if (WRSum6_Text) WRSum6_Text.text = wagers.op_bets?.sum_6?.GetPayoutRatioString() ?? "1 : 1";
        if (WRSum7_Text) WRSum7_Text.text = wagers.op_bets?.sum_7?.GetPayoutRatioString() ?? "1 : 1";
        if (WRSum8_Text) WRSum8_Text.text = wagers.op_bets?.sum_8?.GetPayoutRatioString() ?? "1 : 1";
        if (WRSum9_Text) WRSum9_Text.text = wagers.op_bets?.sum_9?.GetPayoutRatioString() ?? "1 : 1";
        if (WRSum10_Text) WRSum10_Text.text = wagers.op_bets?.sum_10?.GetPayoutRatioString() ?? "1 : 1";
        if (WRSum11_Text) WRSum11_Text.text = wagers.op_bets?.sum_11?.GetPayoutRatioString() ?? "1 : 1";
        if (WRSum12_Text) WRSum12_Text.text = wagers.op_bets?.sum_12?.GetPayoutRatioString() ?? "1 : 1";
        if (WRSum13_Text) WRSum13_Text.text = wagers.op_bets?.sum_13?.GetPayoutRatioString() ?? "1 : 1";
        if (WRSum14_Text) WRSum14_Text.text = wagers.op_bets?.sum_14?.GetPayoutRatioString() ?? "1 : 1";
        if (WRSum15_Text) WRSum15_Text.text = wagers.op_bets?.sum_15?.GetPayoutRatioString() ?? "1 : 1";
        if (WRSum16_Text) WRSum16_Text.text = wagers.op_bets?.sum_16?.GetPayoutRatioString() ?? "1 : 1";
        if (WRSum17_Text) WRSum17_Text.text = wagers.op_bets?.sum_17?.GetPayoutRatioString() ?? "1 : 1";
    }

    private void UpdateLobbyMinMaxDisplay()
    {
        if (gameWagers == null || gameBets == null) return;

        UpdateRoomMinMax("casual", gameBets.casual, CasualMin_Text, CasualMax_Text);
        UpdateRoomMinMax("novice", gameBets.novice, NoviceMin_Text, NoviceMax_Text);
        UpdateRoomMinMax("expert", gameBets.expert, ExpertMin_Text, ExpertMax_Text);
        UpdateRoomMinMax("high_roller", gameBets.high_roller, HighRollerMin_Text, HighRollerMax_Text);
    }

    private void UpdateRoomMinMax(string room, List<double> chips, TMP_Text minText, TMP_Text maxText)
    {
        if (chips == null || chips.Count == 0) return;
        double min = chips[0];
        double max = gameWagers.main_bets?.small?.GetMaxBet(room) ?? 0;
        if (minText) minText.text = GameUtilities.FormatBetValue(min);
        if (maxText) maxText.text = GameUtilities.FormatBetValue(max);
    }
    #endregion

    #region Timer API
    internal void ShowBettingPhase(int seconds) => betTimerController?.ShowBettingPhase(seconds);
    internal void ShowBetLocked() => betTimerController?.ShowBetLocked();
    internal void ShowNextRound(int seconds) => betTimerController?.ShowNextRound(seconds);
    internal void HideAllTimers() => betTimerController?.HideAll();

    /// <summary>
    /// Server correction for the next-round countdown — mirrors UpdateTimer for betting phase.
    /// Called on every game:cashout_timer tick to keep the display phase-locked to the server.
    /// </summary>
    internal void UpdateNextRoundTimer(int seconds) => betTimerController?.UpdateNextRoundTimer(seconds);
    #endregion

    #region Win Animation
    internal void ShowWinAnimation(double winAmount)
    {
        if (WinAmount_Text == null || WinPanel == null) return;
        winTween?.Kill();

        // Always reset to original anchored position and scale before starting
        if (winPanelRT != null) winPanelRT.anchoredPosition = winPanelOriginalPos;
        WinPanel.transform.localScale = Vector3.zero;

        CanvasGroup cg = WinPanel.GetComponent<CanvasGroup>();
        if (cg == null) cg = WinPanel.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        WinAmount_Text.text = $"+{winAmount:F2}";
        WinAmount_Text.gameObject.SetActive(true);
        WinPanel.SetActive(true);

        // Simply fly upward by winFlyDistance (ignore BalanceTarget)
        float targetY = winPanelOriginalPos.y + winFlyDistance;

        winTween = DOTween.Sequence()
            // Pop in at original position
            .Append(WinPanel.transform.DOScale(1.2f, 0.25f).SetEase(Ease.OutBack))
            .Append(WinPanel.transform.DOScale(1f, 0.15f).SetEase(Ease.InOutSine))
            // Hold
            .AppendInterval(winHoldDuration)
            // Fly upward + scale UP (cloud dissipation effect) + fade out simultaneously
            .AppendCallback(() =>
            {
                if (winPanelRT != null)
                    winPanelRT.DOAnchorPosY(targetY, winFlyDuration).SetEase(Ease.OutCubic);
                // Scale UP to 1.3x for cloud/smoke dissipation effect
                WinPanel.transform.DOScale(1.3f, winFlyDuration).SetEase(Ease.OutCubic);
                cg.DOFade(0f, winFlyDuration * 0.8f).SetEase(Ease.InQuad);
            })
            .AppendInterval(winFlyDuration)
            .OnComplete(() =>
            {
                // Disable and reset everything for next use
                WinPanel.SetActive(false);
                if (WinAmount_Text) WinAmount_Text.gameObject.SetActive(false);
                if (winPanelRT != null) winPanelRT.anchoredPosition = winPanelOriginalPos;
                WinPanel.transform.localScale = Vector3.one;
                cg.alpha = 1f;
            });
    }
    #endregion

    #region Loading Screen
    internal void ShowLoadingScreen(string message)
    {
        if (LoadingScreen_Object == null) return;
        if (LoadingMessage_Text != null) LoadingMessage_Text.text = message;
        LoadingScreen_Object.SetActive(true);
    }

    internal void HideLoadingScreen()
    {
        if (LoadingScreen_Object != null) LoadingScreen_Object.SetActive(false);
    }
    #endregion

    #region Avatar  
    private void UpdatePlayerAvatars()
    {
        if (playerAvatarSprites == null || selectedAvatarIndex >= playerAvatarSprites.Length) return;
        Sprite avatar = playerAvatarSprites[selectedAvatarIndex];
        if (PlayerAvatar_HomeScreen != null) PlayerAvatar_HomeScreen.sprite = avatar;
        if (PlayerAvatar_GameScreen != null) PlayerAvatar_GameScreen.sprite = avatar;
    }
    #endregion
}