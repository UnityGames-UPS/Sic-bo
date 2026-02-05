using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

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
    [SerializeField] private Button ExitGame_Button;
    [SerializeField] private Button HistoryGame_Button;
    [SerializeField] private Button SettingsGame_Button;

    [Header("Leaderboard - Top 3 Players")]
    [SerializeField] private TMP_Text FirstPlace_Name;
    [SerializeField] private TMP_Text FirstPlace_Balance;
    [SerializeField] private TMP_Text SecondPlace_Name;
    [SerializeField] private TMP_Text SecondPlace_Balance;
    [SerializeField] private TMP_Text ThirdPlace_Name;
    [SerializeField] private TMP_Text ThirdPlace_Balance;

    [Header("Popups")]
    [SerializeField] private GameObject PopupContainer;
    [SerializeField] private GameObject ReconnectPopup;
    [SerializeField] private GameObject DisconnectPopup;
    [SerializeField] private GameObject QuitPopup;
    [SerializeField] private GameObject ErrorPopup;
    [SerializeField] private GameObject NotificationPopup;
    [SerializeField] private GameObject AnotherDevicePopup;

    [Header("Popup Elements")]
    [SerializeField] private TMP_Text ErrorTitle_Text;
    [SerializeField] private TMP_Text ErrorMessage_Text;
    [SerializeField] private TMP_Text Notification_Text;
    [SerializeField] private Button ErrorOK_Button;
    [SerializeField] private Button DisconnectOK_Button;
    [SerializeField] private Button QuitYes_Button;
    [SerializeField] private Button QuitNo_Button;

    [Header("Win Animation")]
    [SerializeField] private TMP_Text WinAmount_Text;
    [SerializeField] private GameObject WinPanel;

    [Header("Bonus Notification")]
    [SerializeField] private TMP_Text BonusNotification_Text;
    [SerializeField] private GameObject BonusPanel;

    [Header("Controllers")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private MenuController menuController;
    [SerializeField] private BetTimerController betTimerController;
    #endregion

    #region Private Fields
    private Tween winTween;
    private Tween bonusTween;
    private string playerName;
    private Wagers gameWagers;
    private Bets gameBets;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        SetupButtonListeners();
        ShowHomeScreen();
        CloseAllPopups();
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

        if (ExitGame_Button) ExitGame_Button.onClick.AddListener(ShowQuitPopup);
        if (HistoryGame_Button) HistoryGame_Button.onClick.AddListener(OpenHistoryFromGame);
        if (SettingsGame_Button) SettingsGame_Button.onClick.AddListener(OpenInfoFromGame);

        if (ErrorOK_Button) ErrorOK_Button.onClick.AddListener(CloseAllPopups);
        if (DisconnectOK_Button) DisconnectOK_Button.onClick.AddListener(() => gameManager.ExitGame());
        if (QuitYes_Button) QuitYes_Button.onClick.AddListener(() => { CloseAllPopups(); gameManager.LeaveRoom(); });
        if (QuitNo_Button) QuitNo_Button.onClick.AddListener(CloseAllPopups);
    }

    internal void SetupInitialData(string name, double balance, Leaderboards leaderboards, Wagers wagers, Bets bets)
    {
        playerName = name;
        gameWagers = wagers;
        gameBets = bets;

        if (PlayerName_Text) PlayerName_Text.text = name;
        if (GamePlayerName_Text) GamePlayerName_Text.text = name;

        UpdateBalance(balance);

        if (leaderboards != null)
        {
            UpdateLeaderboards(leaderboards);
        }

        UpdateLobbyMinMaxDisplay();
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
        Debug.Log("[UI] Home screen shown");
    }

    internal void ShowGameScreen()
    {
        HideAllScreens();
        if (GameScreen) GameScreen.SetActive(true);
        Debug.Log("[UI] Game screen shown");
    }
    #endregion

    #region Popup Management
    private void CloseAllPopups()
    {
        if (ReconnectPopup) ReconnectPopup.SetActive(false);
        if (DisconnectPopup) DisconnectPopup.SetActive(false);
        if (QuitPopup) QuitPopup.SetActive(false);
        if (ErrorPopup) ErrorPopup.SetActive(false);
        if (NotificationPopup) NotificationPopup.SetActive(false);
        if (AnotherDevicePopup) AnotherDevicePopup.SetActive(false);
        if (PopupContainer) PopupContainer.SetActive(false);
    }

    internal void ShowReconnectPopup()
    {
        CloseAllPopups();
        if (PopupContainer) PopupContainer.SetActive(true);
        if (ReconnectPopup) ReconnectPopup.SetActive(true);
    }

    internal void CloseReconnectPopup()
    {
        if (ReconnectPopup) ReconnectPopup.SetActive(false);
        if (PopupContainer) PopupContainer.SetActive(false);
    }

    internal void ShowDisconnectPopup()
    {
        CloseAllPopups();
        if (PopupContainer) PopupContainer.SetActive(true);
        if (DisconnectPopup) DisconnectPopup.SetActive(true);
    }

    private void ShowQuitPopup()
    {
        if (PopupContainer) PopupContainer.SetActive(true);
        if (QuitPopup) QuitPopup.SetActive(true);
    }

    internal void ShowErrorPopup(string message, string title = "Error")
    {
        CloseAllPopups();
        if (ErrorTitle_Text) ErrorTitle_Text.text = title;
        if (ErrorMessage_Text) ErrorMessage_Text.text = message;
        if (PopupContainer) PopupContainer.SetActive(true);
        if (ErrorPopup) ErrorPopup.SetActive(true);
    }

    internal void ShowNotification(string message)
    {
        if (Notification_Text) Notification_Text.text = message;
        if (NotificationPopup) NotificationPopup.SetActive(true);

        StartCoroutine(HideNotificationAfterDelay(3f));
    }

    internal void ShowAnotherDevicePopup()
    {
        CloseAllPopups();
        if (PopupContainer) PopupContainer.SetActive(true);
        if (AnotherDevicePopup) AnotherDevicePopup.SetActive(true);
    }

    private IEnumerator HideNotificationAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (NotificationPopup) NotificationPopup.SetActive(false);
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
        if (leaderboards?.richest == null) return;

        if (leaderboards.richest.Count > 0)
        {
            if (FirstPlace_Name) FirstPlace_Name.text = leaderboards.richest[0].username;
            if (FirstPlace_Balance) FirstPlace_Balance.text = leaderboards.richest[0].balance.ToString("F2");
        }
        else
        {
            if (FirstPlace_Name) FirstPlace_Name.text = "-";
            if (FirstPlace_Balance) FirstPlace_Balance.text = "0.00";
        }

        if (leaderboards.richest.Count > 1)
        {
            if (SecondPlace_Name) SecondPlace_Name.text = leaderboards.richest[1].username;
            if (SecondPlace_Balance) SecondPlace_Balance.text = leaderboards.richest[1].balance.ToString("F2");
        }
        else
        {
            if (SecondPlace_Name) SecondPlace_Name.text = "-";
            if (SecondPlace_Balance) SecondPlace_Balance.text = "0.00";
        }

        if (leaderboards.richest.Count > 2)
        {
            if (ThirdPlace_Name) ThirdPlace_Name.text = leaderboards.richest[2].username;
            if (ThirdPlace_Balance) ThirdPlace_Balance.text = leaderboards.richest[2].balance.ToString("F2");
        }
        else
        {
            if (ThirdPlace_Name) ThirdPlace_Name.text = "-";
            if (ThirdPlace_Balance) ThirdPlace_Balance.text = "0.00";
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
        WinPanel.SetActive(true);

        winTween = DOTween.Sequence()
            .Append(WinPanel.transform.DOScale(1.2f, 0.3f))
            .Append(WinPanel.transform.DOScale(1f, 0.2f))
            .AppendInterval(2f)
            .Append(WinPanel.transform.DOScale(0f, 0.3f))
            .OnComplete(() => WinPanel.SetActive(false));
    }

    internal void ShowBonusNotification(int bonusNumber, int multiplier)
    {
        if (BonusNotification_Text == null || BonusPanel == null) return;

        bonusTween?.Kill();

        BonusNotification_Text.text = $"BONUS {bonusNumber} x{multiplier}";
        BonusPanel.SetActive(true);

        bonusTween = DOTween.Sequence()
            .Append(BonusPanel.transform.DOScale(1.2f, 0.3f))
            .Append(BonusPanel.transform.DOScale(1f, 0.2f))
            .AppendInterval(1.5f)
            .Append(BonusPanel.transform.DOScale(0f, 0.3f))
            .OnComplete(() => BonusPanel.SetActive(false));
    }
    #endregion
}