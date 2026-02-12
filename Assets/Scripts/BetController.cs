using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class BetController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Chip Selector")]
    [SerializeField] private Button MainChip_Button;
    [SerializeField] private Image MainChip_Image;
    [SerializeField] private TMP_Text MainChip_Text;
    [SerializeField] private GameObject ChipSelector_Panel;
    [SerializeField] private GameObject ChipSelector_BlackBG;
    [SerializeField] private Transform ChipOptions_Container;
    [SerializeField] private RectTransform ChipAreaPanel;
    [SerializeField] private RectTransform TotalStakePanel;

    [Header("Chip Prefabs & Sprites")]
    [SerializeField] private GameObject chipSelectorPrefab;
    [SerializeField] private Sprite[] chipSprites;

    [Header("PlayerBetComponent Pool - NEW")]
    [SerializeField] private PlayerBetComponent playerBetComponentPrefab;

    [Header("Bet Areas - Main")]
    [SerializeField] private SimpleBetArea SmallArea;
    [SerializeField] private SimpleBetArea BigArea;
    [SerializeField] private SimpleBetArea OddArea;
    [SerializeField] private SimpleBetArea EvenArea;

    [Header("Bet Areas - Triple Dice")]
    [SerializeField] private List<TripleSameDiceArea> TripleDiceAreas;

    [Header("Bet Areas - Single Dice")]
    [SerializeField] private List<SingleDiceArea> SingleDiceAreas;

    [Header("Bet Areas - Sum")]
    [SerializeField] private List<SumArea> SumAreas;

    [Header("Bet Controls")]
    [SerializeField] private GameObject RepeatPanelMain;
    [SerializeField] private RectTransform RepeatPanel;
    [SerializeField] private Button Repeat_Button;
    [SerializeField] private GameObject BetActionsPanelMain;
    [SerializeField] private RectTransform BetActionsPanel;
    [SerializeField] private Button Undo_Button;
    [SerializeField] private Button Cancel_Button;
    [SerializeField] private Button Double_Button;

    [Header("Total Bet Display")]
    [SerializeField] private TMP_Text TotalBet_Text;

    [Header("Min/Max Bet Display")]
    [SerializeField] private TMP_Text MinBet_Text;
    [SerializeField] private TMP_Text MaxBet_Text;

    [Header("Shared Win Ratio - Triple Dice")]
    [SerializeField] private TMP_Text SharedTripleWinRatio_Text;

    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private UIController uiController;
    [SerializeField] private BonusIndicatorController bonusIndicatorController;
    [SerializeField] private OpponentChipManager opponentChipManager;
    #endregion

    #region Private Fields - Pool System
    private List<PlayerBetComponent> componentPool = new List<PlayerBetComponent>();
    private Dictionary<string, PlayerBetComponent> activeComponents = new Dictionary<string, PlayerBetComponent>();
    private bool isPoolInitialized = false;
    #endregion

    #region Private Fields - Betting
    private List<double> currentChipValues = new List<double>();
    private Dictionary<double, Sprite> chipValueToSprite = new Dictionary<double, Sprite>();
    private List<Chip> existingChips = new List<Chip>();
    private List<Vector3> originalChipPositions = new List<Vector3>();
    private Vector3 centerPosition;
    private int selectedChipIndex = 0;
    private double currentTotalBet = 0;
    private bool isBettingEnabled = false;
    private bool isChipSelectorOpen = false;

    // Bet tracking
    private Dictionary<string, double> areaBets = new Dictionary<string, double>();
    private List<BetAction> betHistory = new List<BetAction>();
    private List<BetAction> previousRoundBets = new List<BetAction>();

    // Configuration
    private Wagers wagerData = null;
    private string currentLevel = "";
    private double minBetAmount = 0;
    private double maxBetAmount = 0;

    // State tracking
    private bool placedBetInPreviousRound = false;
    private bool hasPlacedBetThisRound = false;
    private bool isProcessingBetAction = false;

    // Tracks the betOption of the in-flight single PLACE_BET (used for limit popup)
    private string pendingBetOption = "";

    // Bet action broadcast tracking
    private string currentBetAction = "";
    private int receivedBroadcastCount = 0;

    // Animation constants
    private const float CHIP_OPEN_DURATION = 0.5f;
    private const float CHIP_CLOSE_DURATION = 0.4f;
    private const float PANEL_SLIDE_DURATION = 0.3f;
    private const float REPEAT_PANEL_SHOW_DURATION = 5f;
    private const float REPEAT_PANEL_DELAY = 0.5f;

    private Sequence chipAnimationSequence;
    private Coroutine repeatPanelCoroutine;
    #endregion
    #region Private Fields - Opponent System
    private Dictionary<string, Dictionary<string, double>> opponentBets =
        new Dictionary<string, Dictionary<string, double>>(); // username -> betOption -> amount
    private string currentPlayerUsername = ""; // Set from GameManager
    #endregion
    #region Unity Lifecycle
    private void Start()
    {
        InitializePool();

        SetupButtonListeners();
        SetupBetAreaListeners();
        InitializeExistingChips();

        DisableBetting();
    }

    private void OnDestroy()
    {
        CleanupPool();

        chipAnimationSequence?.Kill();
        if (repeatPanelCoroutine != null) StopCoroutine(repeatPanelCoroutine);
    }
    #endregion

    #region Pool System - Core
    private void InitializePool()
    {
        if (isPoolInitialized) return;

        if (playerBetComponentPrefab == null)
        {
            Debug.LogError("[BetController] playerBetComponentPrefab is null!");
            return;
        }

        int spawnedCount = 0;

        // Spawn in Main areas
        spawnedCount += SpawnComponentInArea(SmallArea, "small");
        spawnedCount += SpawnComponentInArea(BigArea, "big");
        spawnedCount += SpawnComponentInArea(OddArea, "odd");
        spawnedCount += SpawnComponentInArea(EvenArea, "even");

        // Spawn in Triple dice areas
        for (int i = 0; i < TripleDiceAreas.Count; i++)
            spawnedCount += SpawnComponentInArea(TripleDiceAreas[i], $"triple_{i + 1}");

        // Spawn in Single dice areas
        for (int i = 0; i < SingleDiceAreas.Count; i++)
            spawnedCount += SpawnComponentInArea(SingleDiceAreas[i], $"single_{i + 1}");

        // Spawn in Sum areas
        for (int i = 0; i < SumAreas.Count; i++)
            spawnedCount += SpawnComponentInArea(SumAreas[i], $"sum_{i + 4}");

        isPoolInitialized = true;
        bonusIndicatorController?.InitializePool(GetBetAreaContainerMap());

        // Initialize opponent chip manager with bet area containers
        if (opponentChipManager != null)
        {
            Dictionary<string, Transform> betAreaMap = GetOpponentBetAreaContainerMap();
            opponentChipManager.InitializeContainers(betAreaMap);
        }

        Debug.Log($"[BetController] Pool initialized with {spawnedCount} components");
    }

    private int SpawnComponentInArea(SimpleBetArea area, string areaId)
    {
        if (area == null || area.PlayerBetContainer == null) return 0;

        PlayerBetComponent component = Instantiate(playerBetComponentPrefab, area.PlayerBetContainer);
        component.name = $"PlayerBetComponent_{areaId}";
        component.transform.localPosition = Vector3.zero;
        component.transform.localScale = Vector3.one;
        component.gameObject.SetActive(false);

        // UPGRADED: pass currentChipValues so component can do chip combination math
        component.Initialize(chipSprites, currentChipValues);

        area.playerBetComponent = component;
        componentPool.Add(component);
        activeComponents[areaId] = component;

        return 1;
    }

    private int SpawnComponentInArea(TripleSameDiceArea area, string areaId)
    {
        if (area == null || area.PlayerBetContainer == null) return 0;

        PlayerBetComponent component = Instantiate(playerBetComponentPrefab, area.PlayerBetContainer);
        component.name = $"PlayerBetComponent_{areaId}";
        component.transform.localPosition = Vector3.zero;
        component.transform.localScale = Vector3.one;
        component.gameObject.SetActive(false);
        component.Initialize(chipSprites, currentChipValues);
        area.playerBetComponent = component;
        componentPool.Add(component);
        activeComponents[areaId] = component;

        return 1;
    }

    private int SpawnComponentInArea(SingleDiceArea area, string areaId)
    {
        if (area == null || area.PlayerBetContainer == null) return 0;

        PlayerBetComponent component = Instantiate(playerBetComponentPrefab, area.PlayerBetContainer);
        component.name = $"PlayerBetComponent_{areaId}";
        component.transform.localPosition = Vector3.zero;
        component.transform.localScale = Vector3.one;
        component.gameObject.SetActive(false);
        component.Initialize(chipSprites, currentChipValues);
        area.playerBetComponent = component;
        componentPool.Add(component);
        activeComponents[areaId] = component;

        return 1;
    }

    private int SpawnComponentInArea(SumArea area, string areaId)
    {
        if (area == null || area.PlayerBetContainer == null) return 0;

        PlayerBetComponent component = Instantiate(playerBetComponentPrefab, area.PlayerBetContainer);
        component.name = $"PlayerBetComponent_{areaId}";
        component.transform.localPosition = Vector3.zero;
        component.transform.localScale = Vector3.one;
        component.gameObject.SetActive(false);
        component.Initialize(chipSprites, currentChipValues);
        area.playerBetComponent = component;
        componentPool.Add(component);
        activeComponents[areaId] = component;

        return 1;
    }

    private void CleanupPool()
    {
        activeComponents.Clear();

        foreach (var component in componentPool)
        {
            if (component != null)
                Destroy(component.gameObject);
        }

        componentPool.Clear();
        isPoolInitialized = false;
    }
    #endregion

    #region Public API - Round Management
    internal void OnRoundStart()
    {
        ClearAllBets();
    }

    internal void OnRoundEnd()
    {
        // Components stay visible until next round starts
    }
    internal void ResetAllComponents()
    {
        ClearAllBets();
    }
    #endregion

    #region Setup
    private void SetupButtonListeners()
    {
        if (MainChip_Button) MainChip_Button.onClick.AddListener(ToggleChipSelector);

        if (ChipSelector_BlackBG)
        {
            Button bgButton = ChipSelector_BlackBG.GetComponent<Button>();
            if (bgButton == null) bgButton = ChipSelector_BlackBG.AddComponent<Button>();
            bgButton.onClick.AddListener(CloseChipSelector);
        }

        if (Undo_Button) Undo_Button.onClick.AddListener(OnUndoClicked);
        if (Cancel_Button) Cancel_Button.onClick.AddListener(OnCancelClicked);
        if (Double_Button) Double_Button.onClick.AddListener(OnDoubleClicked);
        if (Repeat_Button) Repeat_Button.onClick.AddListener(OnRepeatClicked);
    }

    private void SetupBetAreaListeners()
    {
        // Main areas
        if (SmallArea?.Button) SmallArea.Button.onClick.AddListener(() => OnBetAreaClicked("small"));
        if (BigArea?.Button) BigArea.Button.onClick.AddListener(() => OnBetAreaClicked("big"));
        if (OddArea?.Button) OddArea.Button.onClick.AddListener(() => OnBetAreaClicked("odd"));
        if (EvenArea?.Button) EvenArea.Button.onClick.AddListener(() => OnBetAreaClicked("even"));

        // Triple dice areas
        for (int i = 0; i < TripleDiceAreas.Count; i++)
        {
            int diceNum = i + 1;
            if (TripleDiceAreas[i]?.Button)
                TripleDiceAreas[i].Button.onClick.AddListener(() => OnTripleDiceAreaClicked(diceNum));
        }

        // Single dice areas
        for (int i = 0; i < SingleDiceAreas.Count; i++)
        {
            int diceNum = i + 1;
            if (SingleDiceAreas[i]?.Button)
                SingleDiceAreas[i].Button.onClick.AddListener(() => OnSingleDiceAreaClicked(diceNum));
        }

        // Sum areas
        for (int i = 0; i < SumAreas.Count; i++)
        {
            int sumValue = i + 4;
            if (SumAreas[i]?.Button)
                SumAreas[i].Button.onClick.AddListener(() => OnBetAreaClicked($"sum_{sumValue}"));
        }
    }
    private void InitializeExistingChips()
    {
        existingChips.Clear();
        originalChipPositions.Clear();

        if (ChipOptions_Container == null) return;

        Chip[] chips = ChipOptions_Container.GetComponentsInChildren<Chip>(true);

        for (int i = 0; i < Mathf.Min(6, chips.Length); i++)
        {
            existingChips.Add(chips[i]);
            originalChipPositions.Add(chips[i].transform.localPosition);

            Button chipButton = chips[i].GetComponent<Button>();
            if (chipButton != null)
            {
                int index = i;
                chipButton.onClick.RemoveAllListeners();
                chipButton.onClick.AddListener(() => OnChipSelected(index));
            }
        }

        if (existingChips.Count > 0)
            centerPosition = existingChips[0].transform.localPosition;
    }
    #endregion

    #region Public API - Called by GameManager
    internal void SetupChips(List<double> chipValues, Wagers wagers, string level)
    {
        currentChipValues = chipValues;
        currentLevel = level;
        wagerData = wagers;

        chipValueToSprite.Clear();

        int chipCount = Mathf.Min(chipValues.Count, chipSprites.Length);
        for (int i = 0; i < chipCount; i++)
        {
            chipValueToSprite[chipValues[i]] = chipSprites[i];

            if (i < existingChips.Count)
            {
                existingChips[i].SetData(chipSprites[i], FormatChipAmount(chipValues[i]), i);
                existingChips[i].SetActive(true);
            }
        }

        for (int i = chipCount; i < existingChips.Count; i++)
            existingChips[i].SetActive(false);

        if (chipCount > 0)
            SelectChipAt(0);

        if (chipValues.Count > 0)
        {
            minBetAmount = chipValues[0];
            maxBetAmount = CalculateMaxBetForAllOptions();
        }

        UpdateAllComponentChipValues();

        SetupWinRatios();
        UpdateMinMaxDisplay();
    }
    private double CalculateMaxBetForAllOptions()
    {
        if (wagerData == null || string.IsNullOrEmpty(currentLevel))
            return 0;

        double highestMax = 0;

        // Check Main Bets
        if (wagerData.main_bets != null)
        {
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.main_bets.small));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.main_bets.big));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.main_bets.odd));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.main_bets.even));
        }

        // Check Side Bets
        if (wagerData.side_bets != null)
        {
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.side_bets.single_match_1));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.side_bets.single_match_2));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.side_bets.single_match_3));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.side_bets.specific_2));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.side_bets.specific_3));
        }

        // Check Op Bets (Sum 4-17)
        if (wagerData.op_bets != null)
        {
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.op_bets.sum_4));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.op_bets.sum_5));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.op_bets.sum_6));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.op_bets.sum_7));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.op_bets.sum_8));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.op_bets.sum_9));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.op_bets.sum_10));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.op_bets.sum_11));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.op_bets.sum_12));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.op_bets.sum_13));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.op_bets.sum_14));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.op_bets.sum_15));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.op_bets.sum_16));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxFromWager(wagerData.op_bets.sum_17));
        }

        return highestMax;
    }

    private double GetMaxFromWager(BetWager wager)
    {
        if (wager == null) return 0;
        return wager.GetMaxBet(currentLevel);
    }
    private void UpdateAllComponentChipValues()
    {
        foreach (var kvp in activeComponents)
        {
            if (kvp.Value != null)
                kvp.Value.UpdateChipValues(currentChipValues);
        }
    }
    private void SetupWinRatios()
    {
        if (wagerData == null) return;

        SetWinRatio(SmallArea, wagerData.main_bets?.small);
        SetWinRatio(BigArea, wagerData.main_bets?.big);
        SetWinRatio(OddArea, wagerData.main_bets?.odd);
        SetWinRatio(EvenArea, wagerData.main_bets?.even);

        for (int i = 0; i < SumAreas.Count; i++)
        {
            int sum = i + 4;
            BetWager wager = GetSumWager(sum);
            SetWinRatio(SumAreas[i], wager);
        }

        if (SharedTripleWinRatio_Text != null && wagerData.side_bets != null)
        {
            string combinedRatio = BetWager.GetCombinedSpecificPayoutString(
                wagerData.side_bets.specific_2,
                wagerData.side_bets.specific_3
            );
            SharedTripleWinRatio_Text.text = combinedRatio;
        }
    }
    private BetWager GetSumWager(int sum)
    {
        if (wagerData?.op_bets == null) return null;

        return sum switch
        {
            4 => wagerData.op_bets.sum_4,
            5 => wagerData.op_bets.sum_5,
            6 => wagerData.op_bets.sum_6,
            7 => wagerData.op_bets.sum_7,
            8 => wagerData.op_bets.sum_8,
            9 => wagerData.op_bets.sum_9,
            10 => wagerData.op_bets.sum_10,
            11 => wagerData.op_bets.sum_11,
            12 => wagerData.op_bets.sum_12,
            13 => wagerData.op_bets.sum_13,
            14 => wagerData.op_bets.sum_14,
            15 => wagerData.op_bets.sum_15,
            16 => wagerData.op_bets.sum_16,
            17 => wagerData.op_bets.sum_17,
            _ => null
        };
    }
    private void UpdateMinMaxDisplay()
    {
        if (MinBet_Text) MinBet_Text.text = FormatChipAmount(minBetAmount);
        if (MaxBet_Text) MaxBet_Text.text = FormatChipAmount(maxBetAmount);
    }
    internal void EnableBetting()
    {
        isBettingEnabled = true;
        hasPlacedBetThisRound = false;
        AnimateBetUnlocked();

        if (placedBetInPreviousRound && previousRoundBets.Count > 0)
            ShowRepeatPanelAnimated();
    }
    internal void DisableBetting()
    {
        isBettingEnabled = false;
        CloseChipSelector();
        AnimateBetLocked();
        HideBetPanels();

        if (hasPlacedBetThisRound && betHistory.Count > 0)
        {
            previousRoundBets = new List<BetAction>(betHistory);
            placedBetInPreviousRound = true;
        }
        else
        {
            previousRoundBets.Clear();
            placedBetInPreviousRound = false;
        }
    }
    internal void ClearAllBets()
    {
        areaBets.Clear();
        currentTotalBet = 0;
        betHistory.Clear();
        bonusIndicatorController?.UpdatePlayerBetAreas(null);

        ClearArea(SmallArea);
        ClearArea(BigArea);
        ClearArea(OddArea);
        ClearArea(EvenArea);

        foreach (var area in TripleDiceAreas) ClearArea(area);
        foreach (var area in SingleDiceAreas) ClearArea(area);
        foreach (var area in SumAreas) ClearArea(area);
        ClearAllOpponentBets();

        UpdateTotalBet();
        HideBetActionsPanel();
    }
    internal void HighlightWinningAreas(string matchSide, int sum)
    {
        // small / big come directly from the server's matchSide field
        SetAreaHighlight(SmallArea, matchSide == "small");
        SetAreaHighlight(BigArea, matchSide == "big");

        // odd / even are never sent by the server — always calculate from sum parity.
        SetAreaHighlight(OddArea, sum % 2 == 1);
        SetAreaHighlight(EvenArea, sum % 2 == 0);

        int sumIndex = sum - 4;
        if (sumIndex >= 0 && sumIndex < SumAreas.Count && SumAreas[sumIndex] != null)
            SumAreas[sumIndex].SetHighlight(true);
    }
    internal void HighlightTripleDiceResult(int dice1, int dice2, int dice3)
    {
        if (dice1 == dice2 && dice2 == dice3)
        {
            int diceIndex = dice1 - 1;
            if (diceIndex >= 0 && diceIndex < TripleDiceAreas.Count && TripleDiceAreas[diceIndex] != null)
                TripleDiceAreas[diceIndex].SetHighlight(true);
        }

        HashSet<int> uniqueDice = new HashSet<int> { dice1, dice2, dice3 };
        foreach (int num in uniqueDice)
        {
            int index = num - 1;
            if (index >= 0 && index < SingleDiceAreas.Count && SingleDiceAreas[index] != null)
                SingleDiceAreas[index].SetHighlight(true);
        }
    }
    internal void ClearAllWinHighlights()
    {
        SetAreaHighlight(SmallArea, false);
        SetAreaHighlight(BigArea, false);
        SetAreaHighlight(OddArea, false);
        SetAreaHighlight(EvenArea, false);

        foreach (var area in TripleDiceAreas) if (area != null) area.SetHighlight(false);
        foreach (var area in SingleDiceAreas) if (area != null) area.SetHighlight(false);
        foreach (var area in SumAreas) if (area != null) area.SetHighlight(false);
    }
    #endregion

    #region Bet Action Broadcast Handling
    internal void OnBetPlacedBroadcast(BetPlacedData data)
    {
        if (data == null) return;

        // CHECK IF THIS IS AN OPPONENT'S BET
        if (!string.IsNullOrEmpty(data.username) &&
            data.username != currentPlayerUsername)
        {
            // This is an opponent's bet - handle separately
            HandleOpponentBet(data);
            return;
        }

        // EXISTING CODE FOR PLAYER'S OWN BETS
        if (isProcessingBetAction && !string.IsNullOrEmpty(currentBetAction))
        {
            receivedBroadcastCount++;

            Debug.Log($"[BET] Broadcast {receivedBroadcastCount} for {currentBetAction}: " +
                      $"{data.betOption} amount={data.amount}");

            switch (currentBetAction)
            {
                case "REPEAT":
                    HandleRepeatBroadcast(data);
                    break;
                case "DOUBLE":
                    HandleDoubleBroadcast(data);
                    break;
                case "UNDO":
                    HandleUndoBroadcast(data);
                    break;
                case "CANCEL":
                    HandleCancelBroadcast(data);
                    break;
            }
        }
        else
        {
            HandleSingleBetBroadcast(data);
        }
    }
    private void HandleSingleBetBroadcast(BetPlacedData data)
    {
        if (data == null || data.amount <= 0) return;

        Debug.Log($"[BET] Single bet confirmed: {data.betOption} amount={data.amount}");

        // Spawn chips using server amount — PlayerBetComponent.AddBetFromServer()
        // will decompose e.g. 13 into 10+3 chips automatically.
        AddBetToAreaFromServer(data.betOption, data.amount);

        // Record actual server-confirmed amount for undo/cancel/double tracking
        if (!areaBets.ContainsKey(data.betOption)) areaBets[data.betOption] = 0;
        areaBets[data.betOption] += data.amount;
        currentTotalBet += data.amount;
        bonusIndicatorController?.AddPlayerBetArea(data.betOption);

        int chipIndex = GetChipIndexForAmount(data.amount);
        betHistory.Add(new BetAction
        {
            betOption = data.betOption,
            amount = data.amount,
            chipIndex = chipIndex
        });

        hasPlacedBetThisRound = true;
        pendingBetOption = "";

        UpdateTotalBet();
        ShowBetActionsPanelAnimated();
    }

    private void HandleRepeatBroadcast(BetPlacedData data)
    {
        if (data.amount <= 0) return;

        int chipIndex = GetChipIndexForAmount(data.amount);

        // UPGRADED: use server-driven chip spawning path
        AddBetToAreaFromServer(data.betOption, data.amount);

        if (!areaBets.ContainsKey(data.betOption)) areaBets[data.betOption] = 0;
        areaBets[data.betOption] += data.amount;
        currentTotalBet += data.amount;

        betHistory.Add(new BetAction
        {
            betOption = data.betOption,
            amount = data.amount,
            chipIndex = chipIndex
        });

        UpdateTotalBet();
    }

    private void HandleDoubleBroadcast(BetPlacedData data)
    {
        if (data.amount <= 0) return;

        int chipIndex = GetChipIndexForAmount(data.amount);

        AddBetToAreaFromServer(data.betOption, data.amount);

        if (!areaBets.ContainsKey(data.betOption)) areaBets[data.betOption] = 0;
        areaBets[data.betOption] += data.amount;
        currentTotalBet += data.amount;

        betHistory.Add(new BetAction
        {
            betOption = data.betOption,
            amount = data.amount,
            chipIndex = chipIndex
        });

        UpdateTotalBet();
    }

    private void HandleUndoBroadcast(BetPlacedData data)
    {
        if (data.amount >= 0) return;

        double removeAmount = System.Math.Abs(data.amount);

        // Remove the last matching bet from history
        for (int i = betHistory.Count - 1; i >= 0; i--)
        {
            if (betHistory[i].betOption == data.betOption)
            {
                betHistory.RemoveAt(i);
                break;
            }
        }

        // Update tracking
        if (areaBets.ContainsKey(data.betOption))
        {
            areaBets[data.betOption] -= removeAmount;
            if (areaBets[data.betOption] <= 0.01)
                areaBets.Remove(data.betOption);
        }
        currentTotalBet -= removeAmount;

        // Remove last chip
        RemoveLastChipFromArea(data.betOption);
        UpdateTotalBet();
    }


    private void HandleCancelBroadcast(BetPlacedData data)
    {
        if (data.amount >= 0) return;

        double removeAmount = System.Math.Abs(data.amount);

        // Remove ALL matching bets from history (not just one)
        double totalRemovedFromHistory = 0;
        for (int i = betHistory.Count - 1; i >= 0; i--)
        {
            if (betHistory[i].betOption == data.betOption)
            {
                totalRemovedFromHistory += betHistory[i].amount;
                betHistory.RemoveAt(i);

                // Stop when we've removed the exact amount
                if (System.Math.Abs(totalRemovedFromHistory - removeAmount) < 0.01)
                    break;
            }
        }

        // Update tracking
        if (areaBets.ContainsKey(data.betOption))
        {
            areaBets[data.betOption] -= removeAmount;
            if (areaBets[data.betOption] <= 0.01)
                areaBets.Remove(data.betOption);
        }
        currentTotalBet -= removeAmount;


        ClearBetsFromArea(data.betOption);

        UpdateTotalBet();
    }
    private void HandleOpponentBet(BetPlacedData data)
    {
        if (data == null || data.amount == 0 || opponentChipManager == null) return;

        // Initialize opponent tracking if needed
        if (!opponentBets.ContainsKey(data.username))
            opponentBets[data.username] = new Dictionary<string, double>();

        if (data.amount > 0)
        {
            // Opponent placed a bet - spawn and animate chip
            Debug.Log($"[OPPONENT] {data.username} bet {data.amount} on {data.betOption}");

            opponentChipManager.AddOpponentBet(data.betOption, data.amount);

            // Track opponent bet
            if (!opponentBets[data.username].ContainsKey(data.betOption))
                opponentBets[data.username][data.betOption] = 0;
            opponentBets[data.username][data.betOption] += data.amount;
        }
        else
        {
            // Opponent removed a bet (undo/cancel)
            // For simplicity, we don't remove individual chips, just track the amount
            double removeAmount = System.Math.Abs(data.amount);

            if (opponentBets[data.username].ContainsKey(data.betOption))
            {
                opponentBets[data.username][data.betOption] -= removeAmount;

                if (opponentBets[data.username][data.betOption] <= 0.01)
                {
                    opponentBets[data.username].Remove(data.betOption);
                }
            }
        }
    }

    private void RemoveLastChipFromArea(string betOption)
    {
        if (betOption == "small" && SmallArea != null)
            SmallArea.RemoveLastBet();
        else if (betOption == "big" && BigArea != null)
            BigArea.RemoveLastBet();
        else if (betOption == "odd" && OddArea != null)
            OddArea.RemoveLastBet();
        else if (betOption == "even" && EvenArea != null)
            EvenArea.RemoveLastBet();
        else if (betOption.StartsWith("specific_3_"))
        {
            if (int.TryParse(betOption.Replace("specific_3_", ""), out int diceNum))
            {
                int index = diceNum - 1;
                if (index >= 0 && index < TripleDiceAreas.Count && TripleDiceAreas[index] != null)
                    TripleDiceAreas[index].RemoveLastBet();
            }
        }
        else if (betOption.StartsWith("single_"))
        {
            if (int.TryParse(betOption.Replace("single_", ""), out int diceNum))
            {
                int index = diceNum - 1;
                if (index >= 0 && index < SingleDiceAreas.Count && SingleDiceAreas[index] != null)
                    SingleDiceAreas[index].RemoveLastBet();
            }
        }
        else if (betOption.StartsWith("sum_"))
        {
            if (int.TryParse(betOption.Replace("sum_", ""), out int sum))
            {
                int index = sum - 4;
                if (index >= 0 && index < SumAreas.Count && SumAreas[index] != null)
                    SumAreas[index].RemoveLastBet();
            }
        }
    }

    private void ClearBetsFromArea(string betOption)
    {
        if (betOption == "small" && SmallArea != null)
            SmallArea.ClearBets();
        else if (betOption == "big" && BigArea != null)
            BigArea.ClearBets();
        else if (betOption == "odd" && OddArea != null)
            OddArea.ClearBets();
        else if (betOption == "even" && EvenArea != null)
            EvenArea.ClearBets();
        else if (betOption.StartsWith("specific_3_"))
        {
            if (int.TryParse(betOption.Replace("specific_3_", ""), out int diceNum))
            {
                int index = diceNum - 1;
                if (index >= 0 && index < TripleDiceAreas.Count && TripleDiceAreas[index] != null)
                    TripleDiceAreas[index].ClearBets();
            }
        }
        else if (betOption.StartsWith("single_"))
        {
            if (int.TryParse(betOption.Replace("single_", ""), out int diceNum))
            {
                int index = diceNum - 1;
                if (index >= 0 && index < SingleDiceAreas.Count && SingleDiceAreas[index] != null)
                    SingleDiceAreas[index].ClearBets();
            }
        }
        else if (betOption.StartsWith("sum_"))
        {
            if (int.TryParse(betOption.Replace("sum_", ""), out int sum))
            {
                int index = sum - 4;
                if (index >= 0 && index < SumAreas.Count && SumAreas[index] != null)
                    SumAreas[index].ClearBets();
            }
        }
    }
    private void ClearAllOpponentBets()
    {
        opponentBets.Clear();

        // Clear opponent chips via manager
        if (opponentChipManager != null)
        {
            opponentChipManager.ClearAllOpponentBets();
        }
    }

    internal void OnBetLimitReached()
    {
        // Get the pending bet option that failed
        if (string.IsNullOrEmpty(pendingBetOption))
        {
            uiController?.ShowInGamePopup("Max bet limit reached");
            return;
        }

        // Get max bet for this specific option
        double maxBet = gameManager.GetMaxBetForBetOption(pendingBetOption);

        // Show formatted message
        string message = $"Max bet for {FormatBetOptionName(pendingBetOption)} is {GameUtilities.FormatCurrency(maxBet)}";
        uiController?.ShowInGamePopup(message);
    }
    private string FormatBetOptionName(string betOption)
    {
        if (betOption == "small") return "SMALL";
        if (betOption == "big") return "BIG";
        if (betOption == "odd") return "ODD";
        if (betOption == "even") return "EVEN";
        if (betOption.StartsWith("single_")) return "SINGLE " + betOption.Substring(7);
        if (betOption.StartsWith("specific_3_")) return "TRIPLE " + betOption.Substring(11);
        if (betOption.StartsWith("sum_")) return "SUM " + betOption.Substring(4);
        return betOption.ToUpper();
    }
    internal void OnBetActionResponse(BetAckResponse response)
    {
        if (response == null || response.payload == null)
        {
            pendingBetOption = "";
            ResetBetActionState();
            return;
        }

        int betCount = response.payload.bets != null ? response.payload.bets.Count : 0;
        Debug.Log($"[BET] ACK received for {currentBetAction}: {betCount} bets received");

        // Server rejected the action — show its message and bail out without touching UI
        if (!response.success)
        {
            string msg = !string.IsNullOrEmpty(response.payload.message)
                ? response.payload.message
                : "Action failed";
            uiController?.ShowInGamePopup(msg);
            pendingBetOption = "";
            ResetBetActionState();
            return;
        }

        // Reconcile local tracking from server-authoritative ACK data
        ReconcileStateFromAck(response);

        pendingBetOption = "";

        if (currentTotalBet > 0)
        {
            ShowBetActionsPanelAnimated();
            hasPlacedBetThisRound = true;
        }
        else
        {
            HideBetActionsPanel();
            hasPlacedBetThisRound = false;
        }

        ResetBetActionState();
    }

    /// <summary>
    /// Reconcile areaBets, betHistory and currentTotalBet from the server ACK payload.
    /// The chip visuals have already been updated by the broadcast handler — this just
    /// makes sure the local tracking dictionaries stay in sync with the server truth.
    /// </summary>
    private void ReconcileStateFromAck(BetAckResponse response)
    {
        if (response?.payload == null) return;

        switch (currentBetAction)
        {
            case "DOUBLE":
                // ACK contains totalBet (the new total after doubling).
                // Broadcasts already added the delta chips and updated areaBets/currentTotalBet,
                // but we override with the server's definitive totalBet to stay in sync.
                if (response.payload.totalBet > 0)
                {
                    currentTotalBet = response.payload.totalBet;

                    // Sync per-area amounts: each bet entry carries the new total for that betOption.
                    if (response.payload.bets != null)
                    {
                        foreach (var bet in response.payload.bets)
                        {
                            if (bet == null || string.IsNullOrEmpty(bet.betOption)) continue;
                            // bet.amount = delta (added this round); accumulate onto what broadcasts set.
                            // To avoid double-counting we trust broadcasts for chips and only fix
                            // the running total here if it drifts.
                        }
                    }
                }
                UpdateTotalBet();
                break;

            case "CANCEL":
                // All bets wiped — broadcasts already cleared chip visuals.
                // Ensure tracking state is fully reset regardless of broadcast timing.
                areaBets.Clear();
                betHistory.Clear();
                currentTotalBet = 0;
                bonusIndicatorController?.UpdatePlayerBetAreas(null);
                UpdateTotalBet();
                break;

            case "UNDO":
                // Broadcasts already removed the last chip and decremented currentTotalBet.
                // Use server totalBet to correct any drift (undo payload may not always include it).
                if (response.payload.totalBet >= 0 && response.payload.bets != null)
                {
                    currentTotalBet = response.payload.totalBet;
                    UpdateTotalBet();
                }
                break;
        }
    }

    private void ResetBetActionState()
    {
        currentBetAction = "";
        receivedBroadcastCount = 0;
        isProcessingBetAction = false;
    }
    #endregion

    #region Private Methods - Bet Placement
    private void OnBetAreaClicked(string betOption)
    {
        if (!isBettingEnabled)
        {
            uiController?.ShowInGamePopup("Betting is locked. Wait for next round.");
            return;
        }

        if (currentChipValues.Count == 0) return;

        pendingBetOption = betOption;
        gameManager.PlaceBet(betOption, selectedChipIndex);
        CloseChipSelector();
    }
    private void OnTripleDiceAreaClicked(int diceNum)
    {
        if (!isBettingEnabled)
        {
            uiController?.ShowInGamePopup("Betting is locked. Wait for next round.");
            return;
        }

        if (currentChipValues.Count == 0) return;

        string betOption = $"specific_3_{diceNum}";

        pendingBetOption = betOption;
        gameManager.PlaceBet(betOption, selectedChipIndex);
        CloseChipSelector();
    }

    private void OnSingleDiceAreaClicked(int diceNum)
    {
        if (!isBettingEnabled)
        {
            uiController?.ShowInGamePopup("Betting is locked. Wait for next round.");
            return;
        }

        if (currentChipValues.Count == 0) return;

        string betOption = $"single_{diceNum}";

        pendingBetOption = betOption;
        gameManager.PlaceBet(betOption, selectedChipIndex);
        CloseChipSelector();
    }
    /*private void AddBetToArea(string betOption, double betAmount, int chipIndex)
    {
        AddBetToAreaVisual(betOption, betAmount, chipIndex);
        RecordBet(betOption, betAmount, chipIndex);
    }
    private void AddBetToAreaVisual(string betOption, double betAmount, int chipIndex)
    {
        SimpleBetArea targetArea = null;

        switch (betOption)
        {
            case "small": targetArea = SmallArea; break;
            case "big": targetArea = BigArea; break;
            case "odd": targetArea = OddArea; break;
            case "even": targetArea = EvenArea; break;
        }

        if (targetArea != null)
        {
            targetArea.AddBet(betAmount, chipIndex);
        }
        else if (betOption.StartsWith("sum_"))
        {
            if (int.TryParse(betOption.Replace("sum_", ""), out int sum))
            {
                int index = sum - 4;
                if (index >= 0 && index < SumAreas.Count && SumAreas[index] != null)
                    SumAreas[index].AddBet(betAmount, chipIndex);
            }
        }
        else if (betOption.StartsWith("single_"))
        {
            if (int.TryParse(betOption.Replace("single_", ""), out int diceNum))
            {
                int index = diceNum - 1;
                if (index >= 0 && index < SingleDiceAreas.Count && SingleDiceAreas[index] != null)
                    SingleDiceAreas[index].AddBet(betAmount, chipIndex);
            }
        }
        else if (betOption.StartsWith("specific_3_"))
        {
            if (int.TryParse(betOption.Replace("specific_3_", ""), out int diceNum))
            {
                int index = diceNum - 1;
                if (index >= 0 && index < TripleDiceAreas.Count && TripleDiceAreas[index] != null)
                    TripleDiceAreas[index].AddBet(betAmount, chipIndex);
            }
        }
        else
        {
            Debug.LogWarning($"[BET] Unknown bet option in AddBetToAreaVisual: {betOption}");
        }
    }*/
    private void AddBetToAreaFromServer(string betOption, double amount)
    {
        SimpleBetArea targetArea = null;

        switch (betOption)
        {
            case "small": targetArea = SmallArea; break;
            case "big": targetArea = BigArea; break;
            case "odd": targetArea = OddArea; break;
            case "even": targetArea = EvenArea; break;
        }

        if (targetArea != null && targetArea.playerBetComponent != null)
        {
            targetArea.playerBetComponent.AddBetFromServer(amount);
            return;
        }

        if (betOption.StartsWith("sum_"))
        {
            if (int.TryParse(betOption.Replace("sum_", ""), out int sum))
            {
                int index = sum - 4;
                if (index >= 0 && index < SumAreas.Count && SumAreas[index]?.playerBetComponent != null)
                    SumAreas[index].playerBetComponent.AddBetFromServer(amount);
            }
        }
        else if (betOption.StartsWith("single_"))
        {
            if (int.TryParse(betOption.Replace("single_", ""), out int diceNum))
            {
                int index = diceNum - 1;
                if (index >= 0 && index < SingleDiceAreas.Count && SingleDiceAreas[index]?.playerBetComponent != null)
                    SingleDiceAreas[index].playerBetComponent.AddBetFromServer(amount);
            }
        }
        else if (betOption.StartsWith("specific_3_"))
        {
            if (int.TryParse(betOption.Replace("specific_3_", ""), out int diceNum))
            {
                int index = diceNum - 1;
                if (index >= 0 && index < TripleDiceAreas.Count && TripleDiceAreas[index]?.playerBetComponent != null)
                    TripleDiceAreas[index].playerBetComponent.AddBetFromServer(amount);
            }
        }
        else
        {
            Debug.LogWarning($"[BET] Unknown bet option in AddBetToAreaFromServer: {betOption}");
        }
    }

    private void RecordBet(string betOption, double betAmount, int chipIndex, int diceNumber = 0)
    {
        if (!areaBets.ContainsKey(betOption))
            areaBets[betOption] = 0;

        areaBets[betOption] += betAmount;
        currentTotalBet += betAmount;

        betHistory.Add(new BetAction
        {
            betOption = betOption,
            amount = betAmount,
            chipIndex = chipIndex,
            diceNumber = diceNumber
        });

        UpdateTotalBet();
    }

    /*private bool CanPlaceBet(string betOption, double betAmount)
    {
        double currentAreaBet = areaBets.ContainsKey(betOption) ? areaBets[betOption] : 0;
        double areaMaxBet = GetMaxBetForArea(betOption);

        if (currentAreaBet + betAmount > areaMaxBet)
        {
            uiController?.ShowInGamePopup($"Maximum bet for this area is {FormatChipAmount(areaMaxBet)}");
            return false;
        }

        if (currentTotalBet + betAmount > maxBetAmount)
        {
            uiController?.ShowInGamePopup($"Maximum total bet is {FormatChipAmount(maxBetAmount)}");
            return false;
        }

        return true;
    }*/

    private double GetMaxBetForArea(string betOption)
    {
        if (wagerData == null || string.IsNullOrEmpty(currentLevel))
            return maxBetAmount;

        BetWager wager = null;

        if (betOption == "small") wager = wagerData.main_bets?.small;
        else if (betOption == "big") wager = wagerData.main_bets?.big;
        else if (betOption == "odd") wager = wagerData.main_bets?.odd;
        else if (betOption == "even") wager = wagerData.main_bets?.even;
        else if (betOption.StartsWith("single_")) wager = wagerData.side_bets?.single_match_1;
        else if (betOption.StartsWith("specific_3_")) wager = wagerData.side_bets?.specific_3;
        else if (betOption.StartsWith("sum_"))
        {
            if (int.TryParse(betOption.Replace("sum_", ""), out int sum))
                wager = GetSumWager(sum);
        }

        return wager != null ? wager.GetMaxBet(currentLevel) : maxBetAmount;
    }

    private int GetChipIndexForAmount(double amount)
    {
        for (int i = 0; i < currentChipValues.Count; i++)
        {
            if (System.Math.Abs(currentChipValues[i] - amount) < 0.01)
                return i;
        }
        return 0;
    }
    #endregion

    #region Private Methods - Chip Selector & Animations
    private void ToggleChipSelector()
    {
        if (!isBettingEnabled) return;

        if (isChipSelectorOpen) CloseChipSelector();
        else OpenChipSelector();
    }

    private void OpenChipSelector()
    {
        if (ChipSelector_Panel) ChipSelector_Panel.SetActive(true);
        if (ChipSelector_BlackBG) ChipSelector_BlackBG.SetActive(true);
        isChipSelectorOpen = true;

        AnimateChipsOpen();
    }

    private void CloseChipSelector()
    {
        AnimateChipsClose(() =>
        {
            if (ChipSelector_Panel) ChipSelector_Panel.SetActive(false);
            if (ChipSelector_BlackBG) ChipSelector_BlackBG.SetActive(false);
            isChipSelectorOpen = false;
        });
    }

    private void AnimateChipsOpen()
    {
        chipAnimationSequence?.Kill();
        chipAnimationSequence = DOTween.Sequence();

        int activeChips = Mathf.Min(currentChipValues.Count, existingChips.Count);

        for (int i = 0; i < activeChips; i++)
        {
            if (!existingChips[i].gameObject.activeSelf) continue;

            Transform chipTransform = existingChips[i].transform;
            Vector3 targetPos = originalChipPositions[i];

            chipTransform.localPosition = Vector3.zero;
            chipTransform.localRotation = Quaternion.identity;

            chipAnimationSequence.Join(
                chipTransform.DOLocalMove(targetPos, CHIP_OPEN_DURATION).SetEase(Ease.OutBack));
            chipAnimationSequence.Join(
                chipTransform.DOLocalRotate(new Vector3(0, 0, 360), CHIP_OPEN_DURATION, RotateMode.FastBeyond360).SetEase(Ease.OutQuad));
        }

        chipAnimationSequence.Play();
    }

    private void AnimateChipsClose(System.Action onComplete = null)
    {
        chipAnimationSequence?.Kill();
        chipAnimationSequence = DOTween.Sequence();

        int activeChips = Mathf.Min(currentChipValues.Count, existingChips.Count);

        for (int i = 0; i < activeChips; i++)
        {
            if (!existingChips[i].gameObject.activeSelf) continue;

            Transform chipTransform = existingChips[i].transform;

            chipAnimationSequence.Join(
                chipTransform.DOLocalMove(Vector3.zero, CHIP_CLOSE_DURATION).SetEase(Ease.InBack));
            chipAnimationSequence.Join(
                chipTransform.DOLocalRotate(new Vector3(0, 0, -360), CHIP_CLOSE_DURATION, RotateMode.FastBeyond360).SetEase(Ease.InQuad));
        }

        if (onComplete != null)
            chipAnimationSequence.OnComplete(() => onComplete());

        chipAnimationSequence.Play();
    }

    private void OnChipSelected(int index)
    {
        SelectChipAt(index);
        CloseChipSelector();
    }

    private void SelectChipAt(int index)
    {
        if (index < 0 || index >= currentChipValues.Count) return;

        selectedChipIndex = index;
        double chipValue = currentChipValues[index];
        Sprite chipSprite = GetChipSprite(chipValue);

        if (MainChip_Image) MainChip_Image.sprite = chipSprite;
        if (MainChip_Text) MainChip_Text.text = FormatChipAmount(chipValue);
    }

    private Sprite GetChipSprite(double value)
    {
        if (chipValueToSprite.ContainsKey(value))
            return chipValueToSprite[value];

        return chipSprites.Length > 0 ? chipSprites[0] : null;
    }

    private string FormatChipAmount(double amount)
    {
        if (amount >= 1000)
            return $"{(amount / 1000)}K";

        if (amount < 1)
            return amount.ToString("F1");

        if (amount % 1 != 0)
            return amount.ToString("F1");

        return amount.ToString("F0");
    }
    #endregion

    #region Private Methods - Panel Animations
    private void AnimateBetLocked()
    {
        if (ChipAreaPanel != null)
            ChipAreaPanel.DOAnchorPosY(-200f, PANEL_SLIDE_DURATION).SetEase(Ease.InOutQuad);

        if (TotalStakePanel != null)
            TotalStakePanel.DOAnchorPosY(40f, PANEL_SLIDE_DURATION).SetEase(Ease.InOutQuad);
    }

    private void AnimateBetUnlocked()
    {
        if (ChipAreaPanel != null)
            ChipAreaPanel.DOAnchorPosY(0f, PANEL_SLIDE_DURATION).SetEase(Ease.InOutQuad);

        if (TotalStakePanel != null)
            TotalStakePanel.DOAnchorPosY(-200f, PANEL_SLIDE_DURATION).SetEase(Ease.InOutQuad);
    }

    private void ShowRepeatPanelAnimated()
    {
        if (RepeatPanel == null) return;

        if (repeatPanelCoroutine != null)
            StopCoroutine(repeatPanelCoroutine);

        repeatPanelCoroutine = StartCoroutine(RepeatPanelSequence());
    }

    private IEnumerator RepeatPanelSequence()
    {
        if (BetActionsPanel != null)
        {
            BetActionsPanelMain.SetActive(false);
            BetActionsPanel.gameObject.SetActive(false);
        }

        yield return new WaitForSeconds(REPEAT_PANEL_DELAY);

        RepeatPanelMain.SetActive(true);
        RepeatPanel.gameObject.SetActive(true);
        RepeatPanel.anchoredPosition = new Vector2(-200f, RepeatPanel.anchoredPosition.y);
        RepeatPanel.DOAnchorPosX(0f, PANEL_SLIDE_DURATION).SetEase(Ease.InOutQuad);

        yield return new WaitForSeconds(REPEAT_PANEL_SHOW_DURATION);

        RepeatPanel.DOAnchorPosX(-200f, PANEL_SLIDE_DURATION).SetEase(Ease.InOutQuad)
            .OnComplete(() =>
            {
                RepeatPanel.gameObject.SetActive(false);
                RepeatPanelMain.SetActive(false);
            });

        repeatPanelCoroutine = null;
    }

    private void ShowBetActionsPanelAnimated()
    {
        if (BetActionsPanel == null) return;

        if (repeatPanelCoroutine != null)
        {
            StopCoroutine(repeatPanelCoroutine);
            repeatPanelCoroutine = null;
        }

        if (RepeatPanel != null)
        {
            RepeatPanelMain.SetActive(false);
            RepeatPanel.gameObject.SetActive(false);
        }

        if (!BetActionsPanel.gameObject.activeSelf)
        {
            BetActionsPanelMain.SetActive(true);
            BetActionsPanel.gameObject.SetActive(true);
            BetActionsPanel.anchoredPosition = new Vector2(-300f, BetActionsPanel.anchoredPosition.y);
            BetActionsPanel.DOAnchorPosX(0f, PANEL_SLIDE_DURATION).SetEase(Ease.InOutQuad);
        }
    }

    private void HideBetActionsPanel()
    {
        if (BetActionsPanel != null)
        {
            BetActionsPanelMain.SetActive(false);
            BetActionsPanel.gameObject.SetActive(false);
        }
    }
    #endregion

    #region Private Methods - UI Updates
    private void UpdateTotalBet()
    {
        if (TotalBet_Text) TotalBet_Text.text = $"{currentTotalBet:F2}";
    }

    private void HideBetPanels()
    {
        HideBetActionsPanel();

        if (RepeatPanel != null)
        {
            RepeatPanelMain.SetActive(false);
            RepeatPanel.gameObject.SetActive(false);
        }

        if (repeatPanelCoroutine != null)
        {
            StopCoroutine(repeatPanelCoroutine);
            repeatPanelCoroutine = null;
        }
    }
    #endregion

    #region Private Methods - Button Handlers
    private void OnUndoClicked()
    {
        if (!isBettingEnabled || isProcessingBetAction)
        {
            uiController?.ShowInGamePopup("Please wait...");
            return;
        }

        isProcessingBetAction = true;
        currentBetAction = "UNDO";
        receivedBroadcastCount = 0;

        gameManager.UndoBet();
    }

    private void OnCancelClicked()
    {
        if (!isBettingEnabled || isProcessingBetAction)
        {
            uiController?.ShowInGamePopup("Please wait...");
            return;
        }

        isProcessingBetAction = true;
        currentBetAction = "CANCEL";
        receivedBroadcastCount = 0;

        gameManager.CancelAllBets();
    }

    private void OnDoubleClicked()
    {
        if (!isBettingEnabled || isProcessingBetAction)
        {
            uiController?.ShowInGamePopup("Please wait...");
            return;
        }

        isProcessingBetAction = true;
        currentBetAction = "DOUBLE";
        receivedBroadcastCount = 0;

        gameManager.DoubleBet();
    }

    private void OnRepeatClicked()
    {
        if (!isBettingEnabled || isProcessingBetAction)
        {
            uiController?.ShowInGamePopup("Please wait...");
            return;
        }

        if (previousRoundBets.Count == 0)
        {
            uiController?.ShowInGamePopup("No previous bets to repeat");
            return;
        }

        isProcessingBetAction = true;
        currentBetAction = "REPEAT";
        receivedBroadcastCount = 0;

        if (RepeatPanel != null)
        {
            RepeatPanelMain.SetActive(false);
            RepeatPanel.gameObject.SetActive(false);
        }

        if (repeatPanelCoroutine != null)
        {
            StopCoroutine(repeatPanelCoroutine);
            repeatPanelCoroutine = null;
        }

        gameManager.RepeatBet();
    }
    #endregion

    #region Private Methods - Helpers
    private void SetWinRatio(SimpleBetArea area, BetWager wager)
    {
        if (area != null && wager != null)
            area.SetWinRatio(wager.GetPayoutRatioString());
    }

    private void SetWinRatio(SumArea area, BetWager wager)
    {
        if (area != null && wager != null)
            area.SetWinRatio(wager.GetPayoutRatioString());
    }
    internal void SetCurrentPlayerUsername(string username)
    {
        currentPlayerUsername = username;
    }
    private void ClearArea(SimpleBetArea area) { if (area != null) area.ClearBets(); }
    private void ClearArea(TripleSameDiceArea area) { if (area != null) area.ClearBets(); }
    private void ClearArea(SingleDiceArea area) { if (area != null) area.ClearBets(); }
    private void ClearArea(SumArea area) { if (area != null) area.ClearBets(); }

    private void SetAreaHighlight(SimpleBetArea area, bool highlight)
    {
        if (area != null) area.SetHighlight(highlight);
    }
    #endregion

    #region Debug Helpers
    [ContextMenu("Debug Pool Status")]
    private void DebugPoolStatus()
    {
        int available = 0;
        foreach (var comp in componentPool)
        {
            if (comp != null && !comp.gameObject.activeInHierarchy)
                available++;
        }
        Debug.Log($"[BetController] Pool: {componentPool.Count} total, {available} inactive");
        foreach (var kvp in activeComponents)
            Debug.Log($"  Active component: {kvp.Key}");
    }
    #endregion

    #region Win Animation Data Collection
    /// <summary>
    /// Collect data about winning areas for chip animation
    /// </summary>
    internal List<WinAreaData> GetWinningAreasData()
    {
        List<WinAreaData> winAreas = new List<WinAreaData>();

        // Check all bet areas that have bets
        foreach (var kvp in areaBets)
        {
            string betOption = kvp.Key;
            double betAmount = kvp.Value;

            // Find the corresponding bet area transform
            Transform areaTransform = GetBetAreaTransform(betOption);
            if (areaTransform != null)
            {
                // Check if this area is highlighted (winning)
                GameObject winImage = GetWinImage(betOption);
                if (winImage != null && winImage.activeSelf)
                {
                    // Calculate win amount
                    BetWager wager = gameManager.GetWagerForBetOption(betOption);
                    double winAmount = 0;
                    if (wager != null)
                    {
                        winAmount = wager.CalculateWin(betAmount);
                    }

                    winAreas.Add(new WinAreaData
                    {
                        betOption = betOption,
                        betAreaTarget = areaTransform,
                        betAmount = betAmount,
                        winAmount = winAmount
                    });
                }
            }
        }

        return winAreas;
    }

    /// <summary>
    /// Get the transform for a bet area's player bet container
    /// </summary>
    private Transform GetBetAreaTransform(string betOption)
    {
        // Main bets
        if (betOption == "small" && SmallArea != null)
            return SmallArea.PlayerBetContainer;
        if (betOption == "big" && BigArea != null)
            return BigArea.PlayerBetContainer;
        if (betOption == "odd" && OddArea != null)
            return OddArea.PlayerBetContainer;
        if (betOption == "even" && EvenArea != null)
            return EvenArea.PlayerBetContainer;

        // Triple dice areas
        if (betOption.StartsWith("specific_3_"))
        {
            string numberStr = betOption.Substring(11);
            if (int.TryParse(numberStr, out int number) && number >= 1 && number <= 6)
            {
                int index = number - 1;
                if (index < TripleDiceAreas.Count && TripleDiceAreas[index] != null)
                {
                    return TripleDiceAreas[index].PlayerBetContainer;
                }
            }
        }

        // Single dice areas
        if (betOption.StartsWith("single_"))
        {
            string numberStr = betOption.Substring(7);
            if (int.TryParse(numberStr, out int number) && number >= 1 && number <= 6)
            {
                int index = number - 1;
                if (index < SingleDiceAreas.Count && SingleDiceAreas[index] != null)
                {
                    return SingleDiceAreas[index].PlayerBetContainer;
                }
            }
        }

        // Sum areas
        if (betOption.StartsWith("sum_"))
        {
            string numberStr = betOption.Substring(4);
            if (int.TryParse(numberStr, out int sum) && sum >= 4 && sum <= 17)
            {
                int index = sum - 4;
                if (index < SumAreas.Count && SumAreas[index] != null)
                {
                    return SumAreas[index].PlayerBetContainer;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Get the win image GameObject for a bet area
    /// </summary>
    private GameObject GetWinImage(string betOption)
    {
        // Main bets
        if (betOption == "small" && SmallArea != null)
            return SmallArea.WinImage;
        if (betOption == "big" && BigArea != null)
            return BigArea.WinImage;
        if (betOption == "odd" && OddArea != null)
            return OddArea.WinImage;
        if (betOption == "even" && EvenArea != null)
            return EvenArea.WinImage;

        // Triple dice areas
        if (betOption.StartsWith("specific_3_"))
        {
            string numberStr = betOption.Substring(11);
            if (int.TryParse(numberStr, out int number) && number >= 1 && number <= 6)
            {
                int index = number - 1;
                if (index < TripleDiceAreas.Count && TripleDiceAreas[index] != null)
                {
                    return TripleDiceAreas[index].WinImage;
                }
            }
        }

        // Single dice areas
        if (betOption.StartsWith("single_"))
        {
            string numberStr = betOption.Substring(7);
            if (int.TryParse(numberStr, out int number) && number >= 1 && number <= 6)
            {
                int index = number - 1;
                if (index < SingleDiceAreas.Count && SingleDiceAreas[index] != null)
                {
                    return SingleDiceAreas[index].WinImage;
                }
            }
        }

        // Sum areas
        if (betOption.StartsWith("sum_"))
        {
            string numberStr = betOption.Substring(4);
            if (int.TryParse(numberStr, out int sum) && sum >= 4 && sum <= 17)
            {
                int index = sum - 4;
                if (index < SumAreas.Count && SumAreas[index] != null)
                {
                    return SumAreas[index].WinImage;
                }
            }
        }

        return null;
    }
    #endregion

    #region Bonus System Support
    /// <summary>
    /// Get map of betOption -> Transform for bonus indicator placement
    /// </summary>
    internal Dictionary<string, Transform> GetBetAreaContainerMap()
    {
        return BonusIndicatorController.BuildBetAreaContainerMap(
            SmallArea, BigArea, OddArea, EvenArea,
            TripleDiceAreas, SingleDiceAreas, SumAreas
        );
    }

    /// <summary>
    /// Get list of currently winning bet options (areas with active WinImage)
    /// </summary>
    internal List<string> GetWinningBetOptions()
    {
        List<string> winningOptions = new List<string>();

        // Check all bet areas that have bets
        foreach (var kvp in areaBets)
        {
            string betOption = kvp.Key;
            GameObject winImage = GetWinImage(betOption);

            if (winImage != null && winImage.activeSelf)
            {
                winningOptions.Add(betOption);
            }
        }

        return winningOptions;
    }

    /// <summary>
    /// Get map of betOption -> OpponentBetContainer Transform for opponent chip placement
    /// </summary>
    private Dictionary<string, Transform> GetOpponentBetAreaContainerMap()
    {
        Dictionary<string, Transform> map = new Dictionary<string, Transform>();

        // Main bets
        if (SmallArea != null && SmallArea.OpponentBetContainer != null)
            map["small"] = SmallArea.OpponentBetContainer;
        if (BigArea != null && BigArea.OpponentBetContainer != null)
            map["big"] = BigArea.OpponentBetContainer;
        if (OddArea != null && OddArea.OpponentBetContainer != null)
            map["odd"] = OddArea.OpponentBetContainer;
        if (EvenArea != null && EvenArea.OpponentBetContainer != null)
            map["even"] = EvenArea.OpponentBetContainer;

        // Triple dice areas    
        for (int i = 0; i < TripleDiceAreas.Count; i++)
        {
            if (TripleDiceAreas[i] != null && TripleDiceAreas[i].OpponentBetContainer != null)
            {
                map[$"specific_3_{i + 1}"] = TripleDiceAreas[i].OpponentBetContainer;
            }
        }

        // Single dice areas
        for (int i = 0; i < SingleDiceAreas.Count; i++)
        {
            if (SingleDiceAreas[i] != null && SingleDiceAreas[i].OpponentBetContainer != null)
            {
                map[$"single_{i + 1}"] = SingleDiceAreas[i].OpponentBetContainer;
            }
        }

        // Sum areas
        for (int i = 0; i < SumAreas.Count; i++)
        {
            if (SumAreas[i] != null && SumAreas[i].OpponentBetContainer != null)
            {
                map[$"sum_{i + 4}"] = SumAreas[i].OpponentBetContainer;
            }
        }

        return map;
    }
    #endregion

}