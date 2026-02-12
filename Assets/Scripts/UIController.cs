using DG.Tweening;
using DG.Tweening.Core.Easing;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Screens")]
    [SerializeField] private GameObject HomeScreen;
    [SerializeField] private GameObject GameScreen;

    [Header("Home Screen Elements")]
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

    [Header("Game Screen Elements")]
    [SerializeField] private TMP_Text GamePlayerName_Text;
    [SerializeField] private TMP_Text GameBalance_Text;
    [SerializeField] private TMP_Text PlayerCount_Text;
    [SerializeField] private TMP_Text RoundPhase_Text;
    [SerializeField] private Button SideMenuOpen_Button;

    [Header("Side Menu Elements")]
    [SerializeField] private Button SideMenuClose_Button;
    [SerializeField] private Button ExitGame_Button;
    [SerializeField] private Button HistoryGame_Button;
    [SerializeField] private Button SettingsGame_Button;
    [SerializeField] private Toggle Sound_button;
    [SerializeField] private Toggle Music_button;
    [SerializeField] private GameObject MenuPanel_Object;
    [SerializeField] private GameObject MenuPanelContainer_Object;

    [Header("Side Menu Animation Settings")]
    [SerializeField] private float panelSlideDuration = 0.3f;
    [SerializeField] private float buttonDropDuration = 0.5f;
    [SerializeField] private float buttonDropDelay = 0.1f;
    [SerializeField] private float panelSlideDistance = 500f; // Distance panel slides from


    [Header("Error Popup - Separate Parent")]
    [SerializeField] private GameObject ErrorPopupParent;
    [SerializeField] private GameObject ErrorPopup;
    [SerializeField] private TMP_Text ErrorTitle_Text;
    [SerializeField] private TMP_Text ErrorMessage_Text;
    [SerializeField] private Button ErrorOK_Button;

    [Header("In-Game Popup - Separate Parent")]
    [SerializeField] private GameObject InGamePopupParent;
    [SerializeField] private GameObject InGamePopup;
    [SerializeField] private TMP_Text InGameMessage_Text;

    [Header("Other Popups - Separate Parents")]
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


    [Header("Animation Settings")]
    [SerializeField] private float slideDistance = 1000f;
    [SerializeField] private float slideDuration = 0.3f;
    [SerializeField] private float inGamePopupDisplayTime = 1f;

    [Header("Controllers")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private MenuController menuController;
    [SerializeField] private BetTimerController betTimerController;
    [SerializeField] private LeaderboardController leaderboardController;
    #endregion

    #region Private Fields
    private Tween winTween;
    private Tween bonusTween;
    private Tween currentPopupTween;
    private Coroutine inGamePopupCoroutine;
    private string playerName;
    private Wagers gameWagers;
    private Bets gameBets;
    private bool isAnotherDeviceError = false;

    // Side Menu Animation Fields
    private RectTransform[] menuButtonRects;
    private Vector2[] menuButtonOriginalPositions;
    private RectTransform menuPanelContainerRect;
    private Vector2 panelOriginalPosition;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        SetupButtonListeners();
        ShowHomeScreen();
        InitializePopups();
        InitializeSideMenuAnimation();

        // Initialize leaderboard controller
        if (leaderboardController != null)
        {
            leaderboardController.Initialize();
        }
    }

    private void OnDestroy()
    {
        // Clean up all tweens
        winTween?.Kill();
        bonusTween?.Kill();
        currentPopupTween?.Kill();
        if (inGamePopupCoroutine != null) StopCoroutine(inGamePopupCoroutine);
    }
    #endregion

    #region Setup
    private void SetupButtonListeners()
    {
        if (CasualRoom_Button) CasualRoom_Button.onClick.AddListener(() => gameManager.JoinRoom("casual"));
        if (NoviceRoom_Button) NoviceRoom_Button.onClick.AddListener(() => gameManager.JoinRoom("novice"));
        if (ExpertRoom_Button) ExpertRoom_Button.onClick.AddListener(() => gameManager.JoinRoom("expert"));
        if (HighRollerRoom_Button) HighRollerRoom_Button.onClick.AddListener(() => gameManager.JoinRoom("high_roller"));
        if (HistoryHome_Button) HistoryHome_Button.onClick.AddListener(OpenHistoryFromHome);
        if (SettingsHome_Button) SettingsHome_Button.onClick.AddListener(OpenInfoFromHome);
        if (ExitHome_Button) ExitHome_Button.onClick.AddListener(ShowQuitPopup);

        if (SideMenuOpen_Button) SideMenuOpen_Button.onClick.AddListener(OpenSideMenu);
        if (SideMenuClose_Button) SideMenuClose_Button.onClick.AddListener(CloseSideMenu);
        if (ExitGame_Button) ExitGame_Button.onClick.AddListener(() => { gameManager.LeaveRoom(); });
        if (HistoryGame_Button) HistoryGame_Button.onClick.AddListener(OpenHistoryFromGame);
        if (SettingsGame_Button) SettingsGame_Button.onClick.AddListener(OpenInfoFromGame);

        if (ErrorOK_Button) ErrorOK_Button.onClick.AddListener(OnErrorOK);
        if (DisconnectOK_Button) DisconnectOK_Button.onClick.AddListener(() => { CloseDisconnectPopup(); gameManager.ExitGame(); });
        if (QuitYes_Button) QuitYes_Button.onClick.AddListener(() => { CloseQuitPopup(); gameManager.ExitGame(); });
        if (QuitNo_Button) QuitNo_Button.onClick.AddListener(CloseQuitPopup);
    }

    private void InitializePopups()
    {
        // Hide all popups and their parents on start
        HidePopupImmediate(ErrorPopupParent, ErrorPopup);
        HidePopupImmediate(InGamePopupParent, InGamePopup);
        HidePopupImmediate(ReconnectPopupParent, ReconnectPopup);
        HidePopupImmediate(DisconnectPopupParent, DisconnectPopup);
        HidePopupImmediate(QuitPopupParent, QuitPopup);

        // Hide win panel at start � it is shown only when a win occurs
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

        // Only update leaderboards if there's actual data
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
        // Get panel container rect and store original position
        if (MenuPanelContainer_Object)
        {
            menuPanelContainerRect = MenuPanelContainer_Object.GetComponent<RectTransform>();
            if (menuPanelContainerRect != null)
            {
                panelOriginalPosition = menuPanelContainerRect.anchoredPosition;
            }
        }

        // Collect all menu buttons
        List<RectTransform> tempRects = new List<RectTransform>();

        if (HistoryGame_Button) tempRects.Add(HistoryGame_Button.GetComponent<RectTransform>());
        if (SettingsGame_Button) tempRects.Add(SettingsGame_Button.GetComponent<RectTransform>());
        if (Sound_button) tempRects.Add(Sound_button.GetComponent<RectTransform>());
        if (Music_button) tempRects.Add(Music_button.GetComponent<RectTransform>());
        if (ExitGame_Button) tempRects.Add(ExitGame_Button.GetComponent<RectTransform>());

        menuButtonRects = tempRects.ToArray();
        menuButtonOriginalPositions = new Vector2[menuButtonRects.Length];

        // Store original positions
        for (int i = 0; i < menuButtonRects.Length; i++)
        {
            menuButtonOriginalPositions[i] = menuButtonRects[i].anchoredPosition;
        }

        // Hide menu initially
        if (MenuPanel_Object) MenuPanel_Object.SetActive(false);
    }

    private void OpenSideMenu()
    {
        print("Opening side menu..........");

        // Enable menu panel
        if (MenuPanel_Object) MenuPanel_Object.SetActive(true);

        // Get close button position
        Vector2 closeButtonPos = Vector2.zero;
        if (SideMenuClose_Button)
        {
            RectTransform closeButtonRect = SideMenuClose_Button.GetComponent<RectTransform>();
            if (closeButtonRect != null)
            {
                closeButtonPos = closeButtonRect.anchoredPosition;
            }
        }

        // Slide panel container in from outside canvas
        if (menuPanelContainerRect != null)
        {
            Vector2 startPos = panelOriginalPosition;
            startPos.x += panelSlideDistance; // Start from right side outside canvas
            menuPanelContainerRect.anchoredPosition = startPos;

            menuPanelContainerRect.DOAnchorPos(panelOriginalPosition, panelSlideDuration)
                .SetEase(Ease.OutCubic);
        }

        // Animate buttons dropping from close button position
        for (int i = 0; i < menuButtonRects.Length; i++)
        {
            // Set initial position to close button
            menuButtonRects[i].anchoredPosition = closeButtonPos;

            float delay = i * buttonDropDelay;

            // Drop to original position with elastic/bounce effect
            menuButtonRects[i].DOAnchorPos(menuButtonOriginalPositions[i], buttonDropDuration)
                .SetEase(Ease.OutBounce)
                .SetDelay(delay);
        }
    }

    private void CloseSideMenu()
    {
        // Get close button position
        Vector2 closeButtonPos = Vector2.zero;
        if (SideMenuClose_Button)
        {
            RectTransform closeButtonRect = SideMenuClose_Button.GetComponent<RectTransform>();
            if (closeButtonRect != null)
            {
                closeButtonPos = closeButtonRect.anchoredPosition;
            }
        }

        // Animate buttons back to close button position in reverse order
        for (int i = 0; i < menuButtonRects.Length; i++)
        {
            int reverseIndex = menuButtonRects.Length - 1 - i;
            float delay = i * buttonDropDelay;

            menuButtonRects[reverseIndex].DOAnchorPos(closeButtonPos, buttonDropDuration * 0.7f)
                .SetEase(Ease.InBack)
                .SetDelay(delay);
        }

        // Slide panel container out
        if (menuPanelContainerRect != null)
        {
            Vector2 endPos = panelOriginalPosition;
            endPos.x += panelSlideDistance; // Slide to right outside canvas

            float totalButtonAnimTime = menuButtonRects.Length * buttonDropDelay + buttonDropDuration * 0.7f;

            menuPanelContainerRect.DOAnchorPos(endPos, panelSlideDuration)
                .SetEase(Ease.InCubic)
                .SetDelay(totalButtonAnimTime)
                .OnComplete(() =>
                {
                    // Disable menu panel after animation completes
                    if (MenuPanel_Object) MenuPanel_Object.SetActive(false);
                });
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
    }

    internal void ShowGameScreen()
    {
        HideAllScreens();
        if (GameScreen) GameScreen.SetActive(true);
    }
    #endregion

    #region Popup Helper Methods
    private void HidePopupImmediate(GameObject parent, GameObject popup)
    {
        if (parent) parent.SetActive(false);
        if (popup) popup.SetActive(false);
    }

    private void SlideInPopup(GameObject parent, GameObject popup)
    {
        if (!parent || !popup) return;

        currentPopupTween?.Kill();

        // Activate parent and popup
        parent.SetActive(true);
        popup.SetActive(true);

        // Start position: off-screen to the left
        RectTransform rectTransform = popup.GetComponent<RectTransform>();
        if (rectTransform == null) return;

        Vector3 startPos = rectTransform.anchoredPosition;
        startPos.x = -slideDistance;
        rectTransform.anchoredPosition = startPos;

        // Animate to center (x = 0)
        Vector3 endPos = startPos;
        endPos.x = 0;

        currentPopupTween = rectTransform.DOAnchorPos(endPos, slideDuration)
            .SetEase(Ease.OutCubic);
    }

    private void SlideOutPopup(GameObject parent, GameObject popup, System.Action onComplete = null)
    {
        if (!parent || !popup) return;

        currentPopupTween?.Kill();

        RectTransform rectTransform = popup.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            HidePopupImmediate(parent, popup);
            onComplete?.Invoke();
            return;
        }

        // Animate to right off-screen
        Vector3 endPos = rectTransform.anchoredPosition;
        endPos.x = slideDistance;

        currentPopupTween = rectTransform.DOAnchorPos(endPos, slideDuration)
            .SetEase(Ease.InCubic)
            .OnComplete(() =>
            {
                HidePopupImmediate(parent, popup);
                onComplete?.Invoke();
            });
    }
    #endregion

    #region Error Popup (For Connection/System Errors Only)
    /// <summary>
    /// Show error popup for ACTUAL ERRORS ONLY:
    /// - Connection failures (timeout, can't connect)
    /// - Authentication failures
    /// - Socket errors
    /// - Internal system errors
    /// - Another device login
    /// 
    /// DO NOT USE FOR:
    /// - Insufficient balance → use ShowInGamePopup
    /// - Bet locked → use ShowInGamePopup
    /// - Bet limit reached → use ShowInGamePopup
    /// </summary>
    internal void ShowErrorPopup(string message, string title = "Error")
    {
        if (ErrorTitle_Text) ErrorTitle_Text.text = title;
        if (ErrorMessage_Text) ErrorMessage_Text.text = message;

        SlideInPopup(ErrorPopupParent, ErrorPopup);
    }

    private void CloseErrorPopup()
    {
        SlideOutPopup(ErrorPopupParent, ErrorPopup);
    }

    private void OnErrorOK()
    {
        CloseErrorPopup();

        // If this was an "another device" error, exit the game
        if (isAnotherDeviceError)
        {
            isAnotherDeviceError = false;
            gameManager.ExitGame();
        }
    }
    #endregion

    #region In-Game Popup (For Game Notifications Only)
    /// <summary>
    /// Show in-game popup for GAME NOTIFICATIONS:
    /// - Insufficient balance
    /// - Betting is locked
    /// - Bet limit reached for [bet option]
    /// - Cannot place bet (betting not active)
    /// - Any server message response
    /// 
    /// Auto-closes after inGamePopupDisplayTime (default 1 second)
    /// </summary>
    internal void ShowInGamePopup(string message)
    {
        // Stop any existing coroutine
        if (inGamePopupCoroutine != null)
        {
            StopCoroutine(inGamePopupCoroutine);
        }

        if (InGameMessage_Text) InGameMessage_Text.text = message;

        SlideInPopup(InGamePopupParent, InGamePopup);

        // Auto-close after delay
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
    /// <summary>
    /// Show reconnect popup when 2 pings are missed
    /// Stays visible until connection is restored (ping received)
    /// If 15 pings are missed, this closes automatically and disconnect popup shows
    /// </summary>
    internal void ShowReconnectPopup()
    {
        SlideInPopup(ReconnectPopupParent, ReconnectPopup);
    }

    internal void CloseReconnectPopup()
    {
        SlideOutPopup(ReconnectPopupParent, ReconnectPopup);
    }
    #endregion

    #region Disconnect Popup
    /// <summary>
    /// Show disconnect popup when 15 pings are missed (max missed pings)
    /// OK button closes the game (close socket, send OnExit to platform, enable raycast)
    /// </summary>
    internal void ShowDisconnectPopup()
    {
        // Close reconnect popup first if it's showing
        if (ReconnectPopupParent && ReconnectPopupParent.activeSelf)
        {
            SlideOutPopup(ReconnectPopupParent, ReconnectPopup);
        }

        SlideInPopup(DisconnectPopupParent, DisconnectPopup);
    }

    private void CloseDisconnectPopup()
    {
        SlideOutPopup(DisconnectPopupParent, DisconnectPopup);
    }
    #endregion

    #region Quit Popup
    /// <summary>
    /// Show quit popup when Home screen Exit button is pressed
    /// YES button: closes the game (close socket, send OnExit to platform, enable raycast)
    /// NO button: just closes the popup
    /// </summary>
    private void ShowQuitPopup()
    {
        SlideInPopup(QuitPopupParent, QuitPopup);
    }

    private void CloseQuitPopup()
    {
        SlideOutPopup(QuitPopupParent, QuitPopup);
    }
    #endregion

    #region Another Device Popup
    /// <summary>
    /// Show when another device logs in with same credentials
    /// User must click OK button to exit the game
    /// </summary>
    internal void ShowAnotherDevicePopup()
    {
        // Set flag so error OK will trigger exit
        isAnotherDeviceError = true;

        // Show as error popup with custom title
        ShowErrorPopup("Another device has logged in with your account.", "Another Login Detected");
    }
    #endregion

    #region Menu Panels
    private void OpenHistoryFromHome()
    {
        if (menuController) menuController.OpenMenuWithHistory();
    }

    private void OpenHistoryFromGame()
    {
        if (menuController) menuController.OpenMenuWithHistory();
    }

    private void OpenInfoFromHome()
    {
        if (menuController) menuController.OpenMenuWithInfo();
    }

    private void OpenInfoFromGame()
    {
        if (menuController) menuController.OpenMenuWithInfo();
    }
    #endregion

    #region Data Updates
    internal void UpdateBalance(double balance)
    {
        string balanceText = balance.ToString("F2");
        if (PlayerBalance_Text) PlayerBalance_Text.text = balanceText;
        if (GameBalance_Text) GameBalance_Text.text = balanceText;
    }

    internal void UpdateTimer(int secondsRemaining)
    {
        if (betTimerController)
        {
            betTimerController.UpdateBettingTimer(secondsRemaining);
        }
    }

    internal void UpdatePlayerCount(int count)
    {
        if (PlayerCount_Text) PlayerCount_Text.text = count.ToString();
    }

    internal void UpdateRoundPhase(string phase)
    {
        if (RoundPhase_Text) RoundPhase_Text.text = phase.ToUpper();

        if (betTimerController)
        {
            switch (phase.ToLower())
            {
                case "betting":
                    break;
                case "rolling":
                case "result":
                    betTimerController.ShowBetLocked();
                    break;
                case "nextround":
                    break;
            }
        }
    }

    internal void UpdateLobbyPlayerCounts(int casual = 0, int novice = 0, int expert = 0, int highRoller = 0)
    {
        if (CasualCount_Text) CasualCount_Text.text = $"{casual} ";
        if (NoviceCount_Text) NoviceCount_Text.text = $"{novice} ";
        if (ExpertCount_Text) ExpertCount_Text.text = $"{expert} ";
        if (HighRollerCount_Text) HighRollerCount_Text.text = $"{highRoller} ";
    }

    internal void UpdateLeaderboards(Leaderboards leaderboards)
    {
        // Use new LeaderboardController if available
        if (leaderboardController != null)
        {
            leaderboardController.UpdateLeaderboard(leaderboards);
        }
    }



    private void UpdateLobbyMinMaxDisplay()
    {
        if (gameWagers == null || gameBets == null) return;

        if (CasualMin_Text && CasualMax_Text && gameBets.casual != null && gameBets.casual.Count > 0)
        {
            double min = gameBets.casual[0];
            double max = gameWagers.main_bets?.small?.GetMaxBet("casual") ?? 0;
            CasualMin_Text.text = $"{min:F2}";
            CasualMax_Text.text = $"{max:F2}";
        }

        if (NoviceMin_Text && NoviceMax_Text && gameBets.novice != null && gameBets.novice.Count > 0)
        {
            double min = gameBets.novice[0];
            double max = gameWagers.main_bets?.small?.GetMaxBet("novice") ?? 0;
            NoviceMin_Text.text = $"{min:F2}";
            NoviceMax_Text.text = $"{max:F2}";
        }

        if (ExpertMin_Text && ExpertMax_Text && gameBets.expert != null && gameBets.expert.Count > 0)
        {
            double min = gameBets.expert[0];
            double max = gameWagers.main_bets?.small?.GetMaxBet("expert") ?? 0;
            ExpertMin_Text.text = $"{min:F2}";
            ExpertMax_Text.text = $"{max:F2}";
        }

        if (HighRollerMin_Text && HighRollerMax_Text && gameBets.high_roller != null && gameBets.high_roller.Count > 0)
        {
            double min = gameBets.high_roller[0];
            double max = gameWagers.main_bets?.small?.GetMaxBet("high_roller") ?? 0;
            HighRollerMin_Text.text = $"{min:F2}";
            HighRollerMax_Text.text = $"{max:F2}";
        }
    }
    #endregion

    #region Betting Timer API
    internal void ShowBettingPhase(int seconds)
    {
        if (betTimerController) betTimerController.ShowBettingPhase(seconds);
    }

    internal void ShowBetLocked()
    {
        if (betTimerController) betTimerController.ShowBetLocked();
    }

    internal void ShowNextRound(int seconds)
    {
        if (betTimerController) betTimerController.ShowNextRound(seconds);
    }

    internal void HideAllTimers()
    {
        if (betTimerController) betTimerController.HideAll();
    }
    #endregion

    #region Animations
    internal void ShowWinAnimation(double winAmount)
    {
        if (WinAmount_Text == null || WinPanel == null) return;

        winTween?.Kill();

        WinAmount_Text.text = $"+{winAmount:F2}";
        WinAmount_Text.gameObject.SetActive(true);
        WinPanel.transform.localScale = Vector3.zero; // always reset before animating
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
}