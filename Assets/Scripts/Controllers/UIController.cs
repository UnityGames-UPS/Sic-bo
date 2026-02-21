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
    [SerializeField] private TMP_Text RoundId_Text;
    [SerializeField] private TMP_Text GameBalance_Text;
    [SerializeField] private TMP_Text PlayerCount_Text;
    //[SerializeField] private TMP_Text RoundPhase_Text;
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
    #endregion

    #region Private Fields
    private Tween winTween;
    private Tween currentPopupTween;
    private Coroutine inGamePopupCoroutine;
    private string playerName;
    private Wagers gameWagers;
    private Bets gameBets;
    private bool isAnotherDeviceError = false;
    private RectTransform[] menuButtonRects;
    private Vector2[] menuButtonOriginalPositions;
    private RectTransform menuPanelContainerRect;
    private Vector2 panelOriginalPosition;
    private int selectedAvatarIndex = -1;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        SetupButtonListeners();
        ShowHomeScreen();
        InitializePopups();
        InitializeSideMenuAnimation();

        if (LoadingScreen_Object != null) LoadingScreen_Object.SetActive(false);

        if (playerAvatarSprites != null && playerAvatarSprites.Length > 0)
        {
            selectedAvatarIndex = Random.Range(0, playerAvatarSprites.Length);
            UpdatePlayerAvatars();
        }

        leaderboardController?.Initialize();
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

    private void InitializePopups()
    {
        HidePopupImmediate(ErrorPopupParent, ErrorPopup);
        HidePopupImmediate(InGamePopupParent, InGamePopup);
        HidePopupImmediate(ReconnectPopupParent, ReconnectPopup);
        HidePopupImmediate(DisconnectPopupParent, DisconnectPopup);
        HidePopupImmediate(QuitPopupParent, QuitPopup);
        if (WinPanel) WinPanel.SetActive(false);
        if (WinAmount_Text) WinAmount_Text.gameObject.SetActive(false);
    }

    internal void SetupInitialData(string name, double balance, Leaderboards leaderboards, Wagers wagers, Bets bets)
    {
        playerName = name;
        gameWagers = wagers;
        gameBets = bets;

        if (PlayerName_Text) PlayerName_Text.text = name;
        if (GamePlayerName_Text) GamePlayerName_Text.text = name;

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

        List<RectTransform> tempRects = new List<RectTransform>();
        if (HistoryGame_Button) tempRects.Add(HistoryGame_Button.GetComponent<RectTransform>());
        if (SettingsGame_Button) tempRects.Add(SettingsGame_Button.GetComponent<RectTransform>());
        if (Sound_button) tempRects.Add(Sound_button.GetComponent<RectTransform>());
        if (Music_button) tempRects.Add(Music_button.GetComponent<RectTransform>());
        if (ExitGame_Button) tempRects.Add(ExitGame_Button.GetComponent<RectTransform>());

        menuButtonRects = tempRects.ToArray();
        menuButtonOriginalPositions = new Vector2[menuButtonRects.Length];
        for (int i = 0; i < menuButtonRects.Length; i++)
            menuButtonOriginalPositions[i] = menuButtonRects[i].anchoredPosition;

        if (MenuPanel_Object) MenuPanel_Object.SetActive(false);
    }

    private void OpenSideMenu()
    {
        if (MenuPanel_Object) MenuPanel_Object.SetActive(true);

        Vector2 closeButtonPos = Vector2.zero;
        if (SideMenuClose_Button)
        {
            RectTransform r = SideMenuClose_Button.GetComponent<RectTransform>();
            if (r != null) closeButtonPos = r.anchoredPosition;
        }

        if (menuPanelContainerRect != null)
        {
            Vector2 startPos = panelOriginalPosition;
            startPos.x += panelSlideDistance;
            menuPanelContainerRect.anchoredPosition = startPos;
            menuPanelContainerRect.DOAnchorPos(panelOriginalPosition, panelSlideDuration).SetEase(Ease.OutCubic);
        }

        for (int i = 0; i < menuButtonRects.Length; i++)
        {
            menuButtonRects[i].gameObject.SetActive(true);
            menuButtonRects[i].anchoredPosition = closeButtonPos;
            menuButtonRects[i].DOAnchorPos(menuButtonOriginalPositions[i], buttonDropDuration)
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

        for (int i = 0; i < menuButtonRects.Length; i++)
        {
            int reverseIndex = menuButtonRects.Length - 1 - i;
            RectTransform rect = menuButtonRects[reverseIndex];
            rect.DOAnchorPos(closeButtonPos, buttonDropDuration * 0.7f)
                .SetEase(Ease.InCubic)
                .SetDelay(i * buttonDropDelay)
                .OnComplete(() => rect.gameObject.SetActive(false));
        }

        if (menuPanelContainerRect != null)
        {
            Vector2 endPos = panelOriginalPosition;
            endPos.x += panelSlideDistance;
            float totalTime = menuButtonRects.Length * buttonDropDelay + buttonDropDuration * 0.7f;
            menuPanelContainerRect.DOAnchorPos(endPos, panelSlideDuration)
                .SetEase(Ease.InCubic)
                .SetDelay(totalTime)
                .OnComplete(() => { if (MenuPanel_Object) MenuPanel_Object.SetActive(false); });
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
    internal void ShowErrorPopup(string message, string title = "Error")
    {
        if (ErrorTitle_Text) ErrorTitle_Text.text = title;
        if (ErrorMessage_Text) ErrorMessage_Text.text = message;
        AudioManager.Instance?.PlayPopupOpen();
        SlideInPopup(ErrorPopupParent, ErrorPopup);
    }

    private void CloseErrorPopup() => SlideOutPopup(ErrorPopupParent, ErrorPopup);

    private void OnErrorOK()
    {
        CloseErrorPopup();
        if (isAnotherDeviceError) { isAnotherDeviceError = false; gameManager.ExitGame(); }
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
        string text = balance.ToString("F2");
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

    /*internal void UpdateRoundPhase(string phase)
    {
       // if (RoundPhase_Text) RoundPhase_Text.text = phase.ToUpper();
        if (betTimerController != null)
        {
            string lower = phase.ToLower();
            if (lower == "rolling" || lower == "result")
                betTimerController.ShowBetLocked();
        }
    }*/

    internal void UpdateLobbyPlayerCounts(int casual = 0, int novice = 0, int expert = 0, int highRoller = 0)
    {
        if (CasualCount_Text) CasualCount_Text.text = $"{casual} ";
        if (NoviceCount_Text) NoviceCount_Text.text = $"{novice} ";
        if (ExpertCount_Text) ExpertCount_Text.text = $"{expert} ";
        if (HighRollerCount_Text) HighRollerCount_Text.text = $"{highRoller} ";
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
        if (minText) minText.text = $"{min:F2}";
        if (maxText) maxText.text = $"{max:F2}";
    }
    #endregion

    #region Timer API
    internal void ShowBettingPhase(int seconds) => betTimerController?.ShowBettingPhase(seconds);
    internal void ShowBetLocked() => betTimerController?.ShowBetLocked();
    internal void ShowNextRound(int seconds) => betTimerController?.ShowNextRound(seconds);
    internal void HideAllTimers() => betTimerController?.HideAll();
    #endregion

    #region Win Animation
    internal void ShowWinAnimation(double winAmount)
    {
        if (WinAmount_Text == null || WinPanel == null) return;
        winTween?.Kill();
        WinAmount_Text.text = $"+{winAmount:F2}";
        WinAmount_Text.gameObject.SetActive(true);
        WinPanel.transform.localScale = Vector3.zero;
        WinPanel.SetActive(true);

        winTween = DOTween.Sequence()
            .Append(WinPanel.transform.DOScale(1.2f, 0.3f))
            .Append(WinPanel.transform.DOScale(1f, 0.2f))
            .AppendInterval(2f)
            .Append(WinPanel.transform.DOScale(0f, 0.3f))
            .OnComplete(() =>
            {
                WinPanel.SetActive(false);
                if (WinAmount_Text) WinAmount_Text.gameObject.SetActive(false);
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