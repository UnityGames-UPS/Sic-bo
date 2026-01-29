using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using JetBrains.Annotations;

public class UiManager : MonoBehaviour
{
    [SerializeField]
    private SocketIOManager socketManager;

    [Header("Screens UI")]
    [SerializeField] private GameObject HomeScreen_Object;
    [SerializeField] private GameObject GameScreen_Object;



    [Header("bottom bar Main Buttons")]
    [SerializeField] private Button HistoryMain_button;
    [SerializeField] private Button MenuMain_button;
    [SerializeField] private Button CasualGame_button;
    [SerializeField] private Button NoviceGame_button;
    [SerializeField] private Button ExpertGame_button;
    [SerializeField] private Button HighRollerGame_button;

    [Header("side panel")]
    [SerializeField] private Button MenuInGame_button;
    [SerializeField] private RectTransform menuMainButton;
    [SerializeField] private Button History_button;
    [SerializeField] private Button Info_button;
    [SerializeField] private Button Sound_button;
    [SerializeField] private Button Music_button;
    [SerializeField] private Button SoundMute_button;
    [SerializeField] private Button MusicMute_button;
    [SerializeField] private Button Home_button;
    [SerializeField] private Button YesHome_button;
    [SerializeField] private Button NoHome_button;

    [SerializeField] private GameObject MenuPanel_Object;
    [SerializeField] private GameObject MenuPanelContainer_Object;
    [SerializeField] private GameObject Homebutton_Object;

    [SerializeField] private Button HistoryClose_button;

    [SerializeField] private Button InfoClose_button;

    [SerializeField] private Button InfoLeft_button;

    [SerializeField] private Button InfoRight_button;

    [SerializeField] private List<GameObject> InfoPages_Objects;
    [SerializeField] private List<GameObject> InfoActive_Objects;
    private int currentInfoPage = 0;

    private bool IsMenuPanelOpen = false;


    //  [Header("Andar Bahar Menu Buttons")]
    // [SerializeField] private Button MenuInGame_button;
    // [SerializeField] private Button InfoInGame_button;
    // [SerializeField] private Button SoundInGame_button;
    // [SerializeField] private Button MusicInGame_button;
    // [SerializeField] private Button HomeInGame_button;











    [Header("Popus UI")]
    [SerializeField]
    private GameObject MainPopup_Object;
    [SerializeField]
    private GameObject PaytablePopup_Object;
    [SerializeField] private GameObject GameQuitPopup;
    [SerializeField] private GameObject HistoryPopup_Object;
    [SerializeField] private GameObject InfoPopup_Object;



    [Header("Settings Popup")]
    [SerializeField]
    private GameObject SettingsPopup_Object;
    [SerializeField]
    private Button SettingsExit_Button;
    [SerializeField]
    private Button Sound_Button;
    [SerializeField]
    private Button Music_Button;

    [SerializeField]
    private GameObject MusicOn_Object;
    [SerializeField]
    private GameObject MusicOff_Object;
    [SerializeField]
    private GameObject SoundOn_Object;
    [SerializeField]
    private GameObject SoundOff_Object;

    [Header("Disconnection Popup")]
    [SerializeField]
    private Button CloseDisconnect_Button;
    [SerializeField]
    private GameObject DisconnectPopup_Object;

    [Header("AnotherDevice Popup")]
    [SerializeField]
    private Button CloseAD_Button;
    [SerializeField]
    private GameObject ADPopup_Object;

    [Header("Reconnection Popup")]
    [SerializeField]
    private TMP_Text reconnect_Text;
    [SerializeField]
    private GameObject ReconnectPopup_Object;

    [Header("LowBalance Popup")]
    [SerializeField]
    private Button LBExit_Button;
    [SerializeField]
    private GameObject LBPopup_Object;

    [Header("Quit Popup")]
    [SerializeField]
    private GameObject QuitPopup_Object;
    [SerializeField]
    private Button YesQuit_Button;
    [SerializeField]
    private Button NoQuit_Button;
    [SerializeField]
    private Button CrossQuit_Button;

    [SerializeField]
    internal GameObject touchDisable;
    [SerializeField]
    private Button Settings_Button;
    [SerializeField]
    private Button Paytable_Button;
    [SerializeField]
    private Button PaytableExit_Button;
    [SerializeField]
    private Button GameExit_Button;
    [SerializeField]
    private GameManager gameManager;










    [Space(100)]
    [Header("HomePage")]
    [SerializeField] private Button CloseStartupPanelBtn;
    [SerializeField] private Button ReadmoreStartupPanelBtn;
    [SerializeField] private GameObject StartupPanel;
    [SerializeField] private RectTransform ToggleTextObj;
    [Header("sidePanel")]
    [SerializeField] private Button MenueButton;
    [SerializeField] private Button GameRules;
    [SerializeField] private Button History;
    [SerializeField] private Button Sound;
    [SerializeField] private Button Music;
    [SerializeField] private GameObject sidepanel;


    [Space(100)]
    [Header("gamePage")]
    [SerializeField] private Button coinSelector;      // Main button
    [SerializeField] private List<Button> Coins;       // Other coins

    [Header("sidePanel")]
    [SerializeField] private Button MenueButtonGP;
    [SerializeField] private Button GameRulesGP;
    [SerializeField] private Button HistoryGP;
    [SerializeField] private Button SoundGP;
    [SerializeField] private Button MusicGP;
    [SerializeField] private Button HomeGP;
    [SerializeField] private GameObject sidepanelGP;

    // [SerializeField] private float spacing = 70f;      // Space between coins
    // [SerializeField] private float duration = 0.3f;    // Animation duration

    [Space(100)]
    [Header("loadingPage")]
    [SerializeField] private GameObject loadingPage;




    [Space(100)]
    [Header("Animation Settings")]


    private List<Button> menuButtons;
    private List<Button> menuButtonsGP;
    private bool isMenueExpanded = false;



    private bool isExpanded = false;

    private Vector3 startPos;

    public float spacing = 100f;
    public float duration = 0.5f;
    public float delayStep = 0.05f;

    private Vector3[] originalPositions;
    private RectTransform[] buttonRects;
    private CanvasGroup[] buttonGroups;
    private Vector2 menuMainPos;

    [SerializeField]
    private AudioManager audioController;
    bool isExit;
    bool isMusic;
    bool isSound;




    private void Start()
    {

        assignButtonListeners();




        // homepage toggle text scroll
        startPos = ToggleTextObj.anchoredPosition;

        StartScroll();



        // bhutton panel anim
        //  menuMainPos = menuMainButton.anchoredPosition;
        buttonRects = new RectTransform[] {
            History_button.GetComponent<RectTransform>(),
            Info_button.GetComponent<RectTransform>(),
            Sound_button.GetComponent<RectTransform>(),
            Music_button.GetComponent<RectTransform>(),
            SoundMute_button.GetComponent<RectTransform>(),
            Home_button.GetComponent<RectTransform>()
        };

        buttonGroups = new CanvasGroup[buttonRects.Length];
        originalPositions = new Vector3[buttonRects.Length];

        for (int i = 0; i < buttonRects.Length; i++)
        {
            originalPositions[i] = buttonRects[i].anchoredPosition;

            // Add CanvasGroup if missing
            var cg = buttonRects[i].GetComponent<CanvasGroup>();
            if (cg == null)
                cg = buttonRects[i].gameObject.AddComponent<CanvasGroup>();

            cg.alpha = 0f; // start hidden
            buttonGroups[i] = cg;
        }



        //new
        // Collect menu buttons into a list
        menuButtons = new List<Button> { GameRules, History, Sound, Music };

        // Hide them initially
        foreach (var btn in menuButtons)
        {
            btn.gameObject.SetActive(false);
            var cg = btn.GetComponent<CanvasGroup>();
            if (cg == null) cg = btn.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0;
        }

        // Hook menu toggle
        MenueButton.onClick.RemoveAllListeners();
        MenueButton.onClick.AddListener(ToggleMenu);






        menuButtonsGP = new List<Button> { GameRulesGP, HistoryGP, SoundGP, MusicGP, HomeGP };

        // Hide them initially
        foreach (var btn in menuButtonsGP)
        {
            btn.gameObject.SetActive(false);
            var cg = btn.GetComponent<CanvasGroup>();
            if (cg == null) cg = btn.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0;
        }

        MenueButtonGP.onClick.RemoveAllListeners();
        MenueButtonGP.onClick.AddListener(ToggleMenuGP);

    }



    #region ButtonSetup

    private void assignButtonListeners()
    {
        if (Paytable_Button) Paytable_Button.onClick.RemoveAllListeners();
        if (Paytable_Button) Paytable_Button.onClick.AddListener(delegate { OpenPopup(PaytablePopup_Object); });

        if (PaytableExit_Button) PaytableExit_Button.onClick.RemoveAllListeners();
        if (PaytableExit_Button) PaytableExit_Button.onClick.AddListener(delegate { ClosePopup(PaytablePopup_Object); });

        if (Settings_Button) Settings_Button.onClick.RemoveAllListeners();
        if (Settings_Button) Settings_Button.onClick.AddListener(delegate { OpenPopup(SettingsPopup_Object); });

        if (SettingsExit_Button) SettingsExit_Button.onClick.RemoveAllListeners();
        if (SettingsExit_Button) SettingsExit_Button.onClick.AddListener(delegate { ClosePopup(SettingsPopup_Object); });

        if (MusicOn_Object) MusicOn_Object.SetActive(true);
        if (MusicOff_Object) MusicOff_Object.SetActive(false);

        if (SoundOn_Object) SoundOn_Object.SetActive(true);
        if (SoundOff_Object) SoundOff_Object.SetActive(false);

        if (GameExit_Button) GameExit_Button.onClick.RemoveAllListeners();
        if (GameExit_Button) GameExit_Button.onClick.AddListener(delegate
        {
            OpenPopup(QuitPopup_Object);
            Debug.Log("Quit event: pressed Big_X button");

        });

        if (NoQuit_Button) NoQuit_Button.onClick.RemoveAllListeners();
        if (NoQuit_Button) NoQuit_Button.onClick.AddListener(delegate
        {
            if (!isExit)
            {
                ClosePopup(QuitPopup_Object);
                Debug.Log("quit event: pressed NO Button ");
            }
        });

        if (CrossQuit_Button) CrossQuit_Button.onClick.RemoveAllListeners();
        if (CrossQuit_Button) CrossQuit_Button.onClick.AddListener(delegate
        {
            if (!isExit)
            {
                ClosePopup(QuitPopup_Object);
                Debug.Log("quit event: pressed Small_X Button ");

            }
        });

        if (LBExit_Button) LBExit_Button.onClick.RemoveAllListeners();
        if (LBExit_Button) LBExit_Button.onClick.AddListener(delegate { ClosePopup(LBPopup_Object); });

        if (YesQuit_Button) YesQuit_Button.onClick.RemoveAllListeners();
        if (YesQuit_Button) YesQuit_Button.onClick.AddListener(delegate
        {
            CallOnExitFunction();
            Debug.Log("quit event: pressed YES Button ");

        });

        if (CloseDisconnect_Button) CloseDisconnect_Button.onClick.RemoveAllListeners();
        if (CloseDisconnect_Button) CloseDisconnect_Button.onClick.AddListener((delegate { CallOnExitFunction(); socketManager.ReactNativeCallOnFailedToConnect(); }));

        if (CloseAD_Button) CloseAD_Button.onClick.RemoveAllListeners();
        if (CloseAD_Button) CloseAD_Button.onClick.AddListener(CallOnExitFunction);



        if (audioController) audioController.ToggleMute(false);

        isMusic = true;
        isSound = true;

        if (Sound_Button) Sound_Button.onClick.RemoveAllListeners();
        if (Sound_Button) Sound_Button.onClick.AddListener(ToggleSound);

        if (Music_Button) Music_Button.onClick.RemoveAllListeners();
        if (Music_Button) Music_Button.onClick.AddListener(ToggleMusic);


        // Andar Bahar 
        if (HistoryMain_button) HistoryMain_button.onClick.RemoveAllListeners();
        if (HistoryMain_button) HistoryMain_button.onClick.AddListener(delegate { OpenPopup(HistoryPopup_Object); });

        // if (MenuMain_button) MenuMain_button.onClick.RemoveAllListeners();
        // if (MenuMain_button) MenuMain_button.onClick.AddListener(delegate { ResetMenuPanel(false); ToggleMenuPanel(); });

        // if (MenuInGame_button) MenuInGame_button.onClick.RemoveAllListeners();
        // if (MenuInGame_button) MenuInGame_button.onClick.AddListener(delegate { ResetMenuPanel(true); ToggleMenuPanel(); });

        if (CasualGame_button) CasualGame_button.onClick.RemoveAllListeners();
        if (CasualGame_button) CasualGame_button.onClick.AddListener(delegate { ResetMenuPanel(true); GameScreen_Object.SetActive(true); });

        if (NoviceGame_button) NoviceGame_button.onClick.RemoveAllListeners();
        if (NoviceGame_button) NoviceGame_button.onClick.AddListener(delegate { ResetMenuPanel(true); GameScreen_Object.SetActive(true); });

        if (ExpertGame_button) ExpertGame_button.onClick.RemoveAllListeners();
        if (ExpertGame_button) ExpertGame_button.onClick.AddListener(delegate { ResetMenuPanel(true); GameScreen_Object.SetActive(true); });

        if (HighRollerGame_button) HighRollerGame_button.onClick.RemoveAllListeners();
        if (HighRollerGame_button) HighRollerGame_button.onClick.AddListener(delegate { ResetMenuPanel(true); GameScreen_Object.SetActive(true); });

        if (GameRules) GameRules.onClick.RemoveAllListeners();
        if (GameRules) GameRules.onClick.AddListener(delegate { OpenPopup(InfoPopup_Object); MenuPanel_Object.SetActive(false); });

        if (History) History.onClick.RemoveAllListeners();
        if (History) History.onClick.AddListener(delegate { OpenPopup(HistoryPopup_Object); MenuPanel_Object.SetActive(false); });

        if (Sound) Sound.onClick.RemoveAllListeners();
        if (Sound) Sound.onClick.AddListener(delegate { ToggleSound(); });

        if (SoundMute_button) SoundMute_button.onClick.RemoveAllListeners();
        if (SoundMute_button) SoundMute_button.onClick.AddListener(delegate { ToggleSound(); });

        if (Music) Music.onClick.RemoveAllListeners();
        if (Music) Music.onClick.AddListener(delegate { ToggleMusic(); });

        if (MusicMute_button) MusicMute_button.onClick.RemoveAllListeners();
        if (MusicMute_button) MusicMute_button.onClick.AddListener(delegate { ToggleMusic(); });

        //Gamepage

        if (GameRulesGP) GameRulesGP.onClick.RemoveAllListeners();
        if (GameRulesGP) GameRulesGP.onClick.AddListener(delegate { OpenPopup(InfoPopup_Object); MenuPanel_Object.SetActive(false); });

        if (HistoryGP) HistoryGP.onClick.RemoveAllListeners();
        if (HistoryGP) HistoryGP.onClick.AddListener(delegate { OpenPopup(HistoryPopup_Object); MenuPanel_Object.SetActive(false); });

        if (SoundGP) SoundGP.onClick.RemoveAllListeners();
        if (SoundGP) SoundGP.onClick.AddListener(delegate { ToggleSound(); });

        if (SoundMute_button) SoundMute_button.onClick.RemoveAllListeners();
        if (SoundMute_button) SoundMute_button.onClick.AddListener(delegate { ToggleSound(); });

        if (MusicGP) MusicGP.onClick.RemoveAllListeners();
        if (MusicGP) MusicGP.onClick.AddListener(delegate { ToggleMusic(); });

        if (MusicMute_button) MusicMute_button.onClick.RemoveAllListeners();
        if (MusicMute_button) MusicMute_button.onClick.AddListener(delegate { ToggleMusic(); });

        if (HomeGP) HomeGP.onClick.RemoveAllListeners();
        if (HomeGP) HomeGP.onClick.AddListener(delegate { OpenPopup(GameQuitPopup); });



        // end

        if (YesHome_button) YesHome_button.onClick.RemoveAllListeners();
        if (YesHome_button) YesHome_button.onClick.AddListener(delegate { ClosePopup(GameQuitPopup); HomeScreen_Object.SetActive(true); GameScreen_Object.SetActive(false); ResetMenuPanel(false); });

        if (NoHome_button) NoHome_button.onClick.RemoveAllListeners();
        if (NoHome_button) NoHome_button.onClick.AddListener(delegate { ClosePopup(GameQuitPopup); });

        if (InfoLeft_button) InfoLeft_button.onClick.RemoveAllListeners();
        if (InfoLeft_button) InfoLeft_button.onClick.AddListener(delegate { GoToPreviousInfoPage(); });

        if (InfoRight_button) InfoRight_button.onClick.RemoveAllListeners();
        if (InfoRight_button) InfoRight_button.onClick.AddListener(delegate { GoToNextInfoPage(); });

        if (InfoClose_button) InfoClose_button.onClick.RemoveAllListeners();
        if (InfoClose_button) InfoClose_button.onClick.AddListener(delegate { ClosePopup(InfoPopup_Object); });

        if (HistoryClose_button) HistoryClose_button.onClick.RemoveAllListeners();
        if (HistoryClose_button) HistoryClose_button.onClick.AddListener(delegate { ClosePopup(HistoryPopup_Object); });


        if (coinSelector) coinSelector.onClick.RemoveAllListeners();
        if (coinSelector) coinSelector.onClick.AddListener(delegate { ToggleCoins(); });




        if (CloseStartupPanelBtn) CloseStartupPanelBtn.onClick.RemoveAllListeners();
        if (CloseStartupPanelBtn) CloseStartupPanelBtn.onClick.AddListener(delegate
        {

            ClosePopup(StartupPanel);



        });

        if (ReadmoreStartupPanelBtn) ReadmoreStartupPanelBtn.onClick.RemoveAllListeners();
        if (ReadmoreStartupPanelBtn) ReadmoreStartupPanelBtn.onClick.AddListener(delegate
        {

            // ClosePopup(StartupPanel);
            StartupPanel.SetActive(false);
            OpenPopup(InfoPopup_Object);

        });
    }
    private void UpdateFrequency(float value)
    {
        Mathf.Clamp(value, 0.2f, 2);
    }

    private void ResetMenuPanel(bool IsGameScreen)
    {
        MenuPanel_Object.SetActive(false);
        if (IsGameScreen)
        {
            Homebutton_Object.SetActive(true);
            //  MenuPanelContainer_Object.transform.localPosition = new Vector2(56, 394);
            MenuPanelContainer_Object.GetComponent<RectTransform>().anchoredPosition = new Vector2(56, 394);
            MenuPanel_Object.transform.SetParent(GameScreen_Object.transform, true);
            int lastIndex = GameScreen_Object.transform.childCount - 1;
            MenuPanel_Object.transform.SetSiblingIndex(lastIndex - 1);
            // Spread();
            ExpandMenu();
        }
        else
        {
            Homebutton_Object.SetActive(false);
            // MenuPanelContainer_Object.transform.localPosition = new Vector2(56, 221);
            MenuPanelContainer_Object.GetComponent<RectTransform>().anchoredPosition = new Vector2(56, 221);
            MenuPanel_Object.transform.SetParent(HomeScreen_Object.transform, true);
            int lastIndex = HomeScreen_Object.transform.childCount - 1;
            MenuPanel_Object.transform.SetSiblingIndex(lastIndex - 1);
            //  Retract();
        }
    }
    private void ToggleMenu()
    {
        if (isMenueExpanded)
            RetractMenu();
        else
            ExpandMenu();
    }

    private void ExpandMenu()
    {
        sidepanel.SetActive(true); // show panel immediately

        for (int i = 0; i < menuButtons.Count; i++)
        {
            var btn = menuButtons[i];
            btn.gameObject.SetActive(true);

            btn.transform.localPosition = MenueButton.transform.localPosition;

            float delay = i * delayStep;

            btn.transform.DOLocalMoveY(
                MenueButton.transform.localPosition.y - spacing * 3 * (i + 1),
                duration
            ).SetEase(Ease.OutBack).SetDelay(delay);

            btn.GetComponent<CanvasGroup>().DOFade(1, duration).SetDelay(delay);
        }

        isMenueExpanded = true;
    }

    private void RetractMenu()
    {
        for (int i = 0; i < menuButtons.Count; i++)
        {
            var btn = menuButtons[i];
            float delay = i * delayStep;

            // If it's the last button → turn off sidepanel after animation
            bool isLast = (i == menuButtons.Count - 1);

            btn.transform.DOLocalMoveY(
                MenueButton.transform.localPosition.y,
                duration
            ).SetEase(Ease.InBack).SetDelay(delay)
             .OnComplete(() =>
             {
                 btn.gameObject.SetActive(false);
                 if (isLast)
                     sidepanel.SetActive(false); // hide panel after last finishes
             });

            btn.GetComponent<CanvasGroup>().DOFade(0, duration).SetDelay(delay);
        }

        isMenueExpanded = false;
    }
    private void ToggleMenuPanel()
    {
        if (IsMenuPanelOpen)
        {
            MenuPanel_Object.SetActive(false);
            IsMenuPanelOpen = false;
        }
        else
        {
            MenuPanel_Object.SetActive(true);
            IsMenuPanelOpen = true;
        }
    }



    internal void LowBalPopup()
    {
        OpenPopup(LBPopup_Object);
    }

    internal void DisconnectionPopup()
    {
        if (!isExit)
        {
            OpenPopup(DisconnectPopup_Object);
        }
    }

    internal void ReconnectionPopup()
    {
        OpenPopup(ReconnectPopup_Object);
    }

    internal void CheckAndClosePopups()
    {
        if (ReconnectPopup_Object.activeInHierarchy)
        {
            ClosePopup(ReconnectPopup_Object);
        }
        if (DisconnectPopup_Object.activeInHierarchy)
        {
            ClosePopup(DisconnectPopup_Object);
        }
    }



    internal void ADfunction()
    {
        OpenPopup(ADPopup_Object);
    }


    private void CallOnExitFunction()
    {
        StartCoroutine(socketManager.CloseSocket());
        isExit = true;
        audioController.PlayButtonAudio();

    }



    internal void OpenPopup(GameObject Popup)
    {
        if (audioController) audioController.PlayButtonAudio();
        if (MainPopup_Object) MainPopup_Object.SetActive(true);

        if (Popup)
        {
            Popup.SetActive(true);
            var rect = Popup.transform;

            // Start from small
            rect.localScale = Vector3.zero;

            // Scale up with bounce
            rect.DOScale(Vector3.one, 0.4f)
                .SetEase(Ease.OutBack);
        }
    }

    internal void ClosePopup(GameObject Popup)
    {
        if (audioController) audioController.PlayButtonAudio();

        if (Popup)
        {
            var rect = Popup.transform;

            // Scale down smoothly
            rect.DOScale(Vector3.zero, 0.3f)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    Popup.SetActive(false);
                    if (MainPopup_Object) MainPopup_Object.SetActive(false);
                });
        }
        else
        {
            if (MainPopup_Object) MainPopup_Object.SetActive(false);
        }
    }

    private void ToggleMusic()
    {
        isMusic = !isMusic;
        if (isMusic)
        {
            Music_button.gameObject.SetActive(true);
            MusicMute_button.gameObject.SetActive(false);
            audioController.ToggleMute(false, "bg");
        }
        else
        {
            Music_button.gameObject.SetActive(false);
            MusicMute_button.gameObject.SetActive(true);
            audioController.ToggleMute(true, "bg");
        }
    }

    private void UrlButtons(string url)
    {
        Application.OpenURL(url);
    }

    private void ToggleSound()
    {
        isSound = !isSound;
        if (isSound)
        {
            Sound_button.gameObject.SetActive(true);
            SoundMute_button.gameObject.SetActive(false);
            if (audioController) audioController.ToggleMute(false, "button");
            if (audioController) audioController.ToggleMute(false, "wl");
            if (audioController) audioController.ToggleMute(false, "win");
            if (audioController) audioController.ToggleMute(false, "bet");

        }
        else
        {
            Sound_button.gameObject.SetActive(false);
            SoundMute_button.gameObject.SetActive(true);
            if (audioController) audioController.ToggleMute(true, "button");
            if (audioController) audioController.ToggleMute(true, "wl");
            if (audioController) audioController.ToggleMute(true, "win");
            if (audioController) audioController.ToggleMute(true, "bet");
        }
    }

    private void UpdateInfoUI()
    {
        for (int i = 0; i < InfoPages_Objects.Count; i++)
            InfoPages_Objects[i].SetActive(i == currentInfoPage);

        for (int i = 0; i < InfoActive_Objects.Count; i++)
            InfoActive_Objects[i].SetActive(i == currentInfoPage);

    }

    private void GoToPreviousInfoPage()
    {
        if (audioController) audioController.PlayButtonAudio();
        currentInfoPage--;
        if (currentInfoPage < 0)
            currentInfoPage = InfoPages_Objects.Count - 1;

        UpdateInfoUI();
    }

    private void GoToNextInfoPage()
    {
        if (audioController) audioController.PlayButtonAudio();
        currentInfoPage++;
        if (currentInfoPage >= InfoPages_Objects.Count)
            currentInfoPage = 0;

        UpdateInfoUI();
    }

    #endregion




    #region  homePage

    void StartScroll()
    {
        // Start at "fromX"
        ToggleTextObj.anchoredPosition = new Vector2(1000f, startPos.y);

        // Tween to "toX"
        ToggleTextObj.DOAnchorPosX(-1000f, 10f)
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                ToggleTextObj.anchoredPosition = new Vector2(1000f, startPos.y);
                StartScroll(); // repeat
            });
    }



    #endregion

    #region  gamePage


    private void ToggleCoins()
    {
        if (isExpanded)
            RetractCoins();
        else
            ExpandCoins();
    }

    private void ExpandCoins()
    {
        float radius = 150f;            // Distance from center
        float startAngle = 180f;        // Start of arc (left)
        float endAngle = 0f;            // End of arc (right)

        for (int i = 0; i < Coins.Count; i++)
        {
            var coin = Coins[i];
            coin.gameObject.SetActive(true);

            // Calculate angle for this coin on the arc (in radians)
            float t = (float)i / (Coins.Count - 1); // normalized 0 → 1
            float angleDeg = Mathf.Lerp(startAngle, endAngle, t);
            float angleRad = angleDeg * Mathf.Deg2Rad;

            // Calculate arc position relative to selector
            float targetX = coinSelector.transform.localPosition.x + radius * Mathf.Cos(angleRad);
            float targetY = coinSelector.transform.localPosition.y + radius * Mathf.Sin(angleRad);

            // Move on arc
            coin.transform.DOLocalMove(new Vector3(targetX, targetY, 0), duration)
                .SetEase(Ease.OutBack);

            // Fade-in
            coin.GetComponent<CanvasGroup>().DOFade(1, duration);
        }

        isExpanded = true;
    }

    private void RetractCoins()
    {
        for (int i = 0; i < Coins.Count; i++)
        {
            var coin = Coins[i];

            coin.transform.DOLocalMove(
                coinSelector.transform.localPosition,
                duration
            )
            .SetEase(Ease.InBack)
            .OnComplete(() => coin.gameObject.SetActive(false));

            coin.GetComponent<CanvasGroup>().DOFade(0, duration);
        }

        isExpanded = false;
    }


    internal void OnCoinSelected(Button selectedCoin)
    {
        // Swap visuals (text, image) between main selector and selected coin
        var tempImage = coinSelector.image.sprite;
        coinSelector.image.sprite = selectedCoin.image.sprite;
        selectedCoin.image.sprite = tempImage;

        // Fold back coins
        RetractCoins();
    }









    private void ToggleMenuGP()
    {
        if (isMenueExpanded)
            RetractMenuGP();
        else
            ExpandMenuGP();
    }

    private void ExpandMenuGP()
    {
        sidepanelGP.SetActive(true); // show panel immediately

        for (int i = 0; i < menuButtonsGP.Count; i++)
        {
            var btn = menuButtonsGP[i];
            btn.gameObject.SetActive(true);

            btn.transform.localPosition = MenueButtonGP.transform.localPosition;

            float delay = i * delayStep;

            btn.transform.DOLocalMoveY(
                MenueButtonGP.transform.localPosition.y - spacing * 1.5f * (i + 1),
                duration
            ).SetEase(Ease.OutBack).SetDelay(delay);

            btn.GetComponent<CanvasGroup>().DOFade(1, duration).SetDelay(delay);
        }

        isMenueExpanded = true;
    }

    private void RetractMenuGP()
    {
        for (int i = 0; i < menuButtonsGP.Count; i++)
        {
            var btn = menuButtonsGP[i];
            float delay = i * delayStep;

            // If it's the last button → turn off sidepanel after animation
            bool isLast = (i == menuButtonsGP.Count - 1);

            btn.transform.DOLocalMoveY(
                MenueButtonGP.transform.localPosition.y,
                duration
            ).SetEase(Ease.InBack).SetDelay(delay)
             .OnComplete(() =>
             {
                 btn.gameObject.SetActive(false);
                 if (isLast)
                     sidepanelGP.SetActive(false); // hide panel after last finishes
             });

            btn.GetComponent<CanvasGroup>().DOFade(0, duration).SetDelay(delay);
        }

        isMenueExpanded = false;
    }
    #endregion


    #region  LOading page

    IEnumerator LoadingPageRoutine(GameObject panelToOpen, GameObject panelToClose)
    {
        loadingPage.SetActive(true);
        panelToClose.SetActive(false);
        yield return new WaitForSeconds(5f);
        loadingPage.SetActive(false);
        panelToOpen.SetActive(true);
    }









    #endregion
}