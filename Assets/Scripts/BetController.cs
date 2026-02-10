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

    [Header("PlayerBetComponent Pool")]
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
    /// <summary>
    /// UPDATED: Initialize object pool - spawn PlayerBetComponents with chip values
    /// </summary>
    private void InitializePool()
    {
        if (isPoolInitialized)
        {
            return;
        }

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
        {
            spawnedCount += SpawnComponentInArea(TripleDiceAreas[i], $"triple_{i + 1}");
        }

        // Spawn in Single dice areas
        for (int i = 0; i < SingleDiceAreas.Count; i++)
        {
            spawnedCount += SpawnComponentInArea(SingleDiceAreas[i], $"single_{i + 1}");
        }

        // Spawn in Sum areas
        for (int i = 0; i < SumAreas.Count; i++)
        {
            spawnedCount += SpawnComponentInArea(SumAreas[i], $"sum_{i + 4}");
        }

        isPoolInitialized = true;
        Debug.Log($"[BetController] Pool initialized with {spawnedCount} components");
    }

    /// <summary>
    /// UPDATED: Spawn component with chip sprites AND chip values
    /// </summary>
    private int SpawnComponentInArea(SimpleBetArea area, string areaId)
    {
        if (area == null || area.PlayerBetContainer == null) return 0;

        PlayerBetComponent component = Instantiate(playerBetComponentPrefab, area.PlayerBetContainer);
        component.name = $"PlayerBetComponent_{areaId}";
        component.transform.localPosition = Vector3.zero;
        component.transform.localScale = Vector3.one;
        component.gameObject.SetActive(false);

        // UPDATED: Initialize with chip sprites and values
        component.Initialize(chipSprites, currentChipValues);

        area.playerBetComponent = component;
        componentPool.Add(component);
        activeComponents[areaId] = component;

        return 1;
    }

    /// <summary>
    /// UPDATED: Spawn component in TripleSameDiceArea
    /// </summary>
    private int SpawnComponentInArea(TripleSameDiceArea area, string areaId)
    {
        if (area == null || area.PlayerBetContainer == null) return 0;

        PlayerBetComponent component = Instantiate(playerBetComponentPrefab, area.PlayerBetContainer);
        component.name = $"PlayerBetComponent_{areaId}";
        component.transform.localPosition = Vector3.zero;
        component.transform.localScale = Vector3.one;
        component.gameObject.SetActive(false);

        // UPDATED: Initialize with chip sprites and values
        component.Initialize(chipSprites, currentChipValues);

        area.playerBetComponent = component;
        componentPool.Add(component);
        activeComponents[areaId] = component;

        return 1;
    }

    /// <summary>
    /// UPDATED: Spawn component in SingleDiceArea
    /// </summary>
    private int SpawnComponentInArea(SingleDiceArea area, string areaId)
    {
        if (area == null || area.PlayerBetContainer == null) return 0;

        PlayerBetComponent component = Instantiate(playerBetComponentPrefab, area.PlayerBetContainer);
        component.name = $"PlayerBetComponent_{areaId}";
        component.transform.localPosition = Vector3.zero;
        component.transform.localScale = Vector3.one;
        component.gameObject.SetActive(false);

        // UPDATED: Initialize with chip sprites and values
        component.Initialize(chipSprites, currentChipValues);

        area.playerBetComponent = component;
        componentPool.Add(component);
        activeComponents[areaId] = component;

        return 1;
    }

    /// <summary>
    /// UPDATED: Spawn component in SumArea
    /// </summary>
    private int SpawnComponentInArea(SumArea area, string areaId)
    {
        if (area == null || area.PlayerBetContainer == null) return 0;

        PlayerBetComponent component = Instantiate(playerBetComponentPrefab, area.PlayerBetContainer);
        component.name = $"PlayerBetComponent_{areaId}";
        component.transform.localPosition = Vector3.zero;
        component.transform.localScale = Vector3.one;
        component.gameObject.SetActive(false);

        // UPDATED: Initialize with chip sprites and values
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
            {
                Destroy(component.gameObject);
            }
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
            {
                TripleDiceAreas[i].Button.onClick.AddListener(() => OnTripleDiceAreaClicked(diceNum));
            }
        }

        // Single dice areas
        for (int i = 0; i < SingleDiceAreas.Count; i++)
        {
            int diceNum = i + 1;
            if (SingleDiceAreas[i]?.Button)
            {
                SingleDiceAreas[i].Button.onClick.AddListener(() => OnSingleDiceAreaClicked(diceNum));
            }
        }

        // Sum areas
        for (int i = 0; i < SumAreas.Count; i++)
        {
            int sumValue = i + 4;
            if (SumAreas[i]?.Button)
            {
                SumAreas[i].Button.onClick.AddListener(() => OnBetAreaClicked($"sum_{sumValue}"));
            }
        }
    }

    private void InitializeExistingChips()
    {
        existingChips.Clear();
        originalChipPositions.Clear();

        if (ChipOptions_Container == null) return;

        Chip[] chips = ChipOptions_Container.GetComponentsInChildren<Chip>(true);
        foreach (var chip in chips)
        {
            existingChips.Add(chip);
            originalChipPositions.Add(chip.transform.localPosition);
        }

        if (existingChips.Count > 0)
        {
            centerPosition = existingChips[0].transform.localPosition;
        }
    }

    /// <summary>
    /// UPDATED: Setup chips with values for PlayerBetComponents
    /// </summary>
    internal void SetupChips(List<double> chipValues, Wagers wagers, string level)
    {
        currentChipValues = new List<double>(chipValues);
        wagerData = wagers;
        currentLevel = level;

        if (chipValues.Count == 0) return;

        // UPDATED: Update chip values in all PlayerBetComponents
        UpdateAllComponentChipValues();

        // Setup chip selector UI
        SetupChipSelector(chipValues);

        // Set min/max bet display
        minBetAmount = chipValues[0];
        maxBetAmount = chipValues[chipValues.Count - 1];

        if (MinBet_Text) MinBet_Text.text = FormatChipAmount(minBetAmount);
        if (MaxBet_Text) MaxBet_Text.text = FormatChipAmount(maxBetAmount);

        // Setup win ratios
        SetupWinRatios();

        Debug.Log($"[BetController] Setup chips for {level}: {string.Join(", ", chipValues)}");
    }

    /// <summary>
    /// NEW: Update chip values in all active PlayerBetComponents
    /// </summary>
    private void UpdateAllComponentChipValues()
    {
        foreach (var kvp in activeComponents)
        {
            if (kvp.Value != null)
            {
                kvp.Value.UpdateChipValues(currentChipValues);
            }
        }
    }

    private void SetupChipSelector(List<double> chipValues)
    {
        // Clear existing chips
        foreach (Transform child in ChipOptions_Container)
        {
            Destroy(child.gameObject);
        }
        existingChips.Clear();

        chipValueToSprite.Clear();

        // Create chip selector buttons
        for (int i = 0; i < chipValues.Count; i++)
        {
            double value = chipValues[i];
            Sprite sprite = i < chipSprites.Length ? chipSprites[i] : chipSprites[0];

            GameObject chipObj = Instantiate(chipSelectorPrefab, ChipOptions_Container);
            Chip chip = chipObj.GetComponent<Chip>();

            if (chip != null)
            {
                chip.SetData(sprite, FormatChipAmount(value), i);
                existingChips.Add(chip);

                Button chipButton = chipObj.GetComponent<Button>();
                if (chipButton == null) chipButton = chipObj.AddComponent<Button>();

                int chipIndex = i;
                chipButton.onClick.AddListener(() => OnChipSelected(chipIndex));

                chipValueToSprite[value] = sprite;
            }
        }

        // Select first chip by default
        if (chipValues.Count > 0)
        {
            SelectChip(0);
        }
    }

    private void SetupWinRatios()
    {
        if (wagerData == null) return;

        // Main areas
        if (SmallArea != null && wagerData.main_bets?.small != null)
            SmallArea.SetWinRatio(wagerData.main_bets.small.GetPayoutRatioString());

        if (BigArea != null && wagerData.main_bets?.big != null)
            BigArea.SetWinRatio(wagerData.main_bets.big.GetPayoutRatioString());

        if (OddArea != null && wagerData.main_bets?.odd != null)
            OddArea.SetWinRatio(wagerData.main_bets.odd.GetPayoutRatioString());

        if (EvenArea != null && wagerData.main_bets?.even != null)
            EvenArea.SetWinRatio(wagerData.main_bets.even.GetPayoutRatioString());

        // Sum areas
        for (int i = 0; i < SumAreas.Count; i++)
        {
            int sumValue = i + 4;
            BetWager sumWager = GetSumWager(sumValue);
            if (SumAreas[i] != null && sumWager != null)
            {
                SumAreas[i].SetWinRatio(sumWager.GetPayoutRatioString());
            }
        }

        // Triple dice - shared ratio
        if (SharedTripleWinRatio_Text != null && wagerData.side_bets?.specific_3 != null && wagerData.side_bets?.specific_2 != null)
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
    #endregion

    #region Public API - Betting Control
    internal void EnableBetting()
    {
        isBettingEnabled = true;

        if (placedBetInPreviousRound)
        {
            ShowRepeatPanelDelayed();
        }
    }

    internal void DisableBetting()
    {
        isBettingEnabled = false;
        CloseChipSelector();

        if (hasPlacedBetThisRound)
        {
            previousRoundBets = new List<BetAction>(betHistory);
            placedBetInPreviousRound = true;
        }
        else
        {
            placedBetInPreviousRound = false;
        }

        hasPlacedBetThisRound = false;
        HideRepeatPanel();
    }
    #endregion

    #region Broadcast Handlers
    internal void OnBetPlacedBroadcast(BetPlacedData data)
    {
        if (data == null) return;

        if (currentBetAction == "PLACE_BET")
        {
            HandlePlaceBroadcast(data);
        }
        else if (currentBetAction == "UNDO_BET")
        {
            HandleUndoBroadcast(data);
        }
        else if (currentBetAction == "CANCEL_BET")
        {
            HandleCancelBroadcast(data);
        }
        else if (currentBetAction == "DOUBLE_BET" || currentBetAction == "REPEAT_BET")
        {
            HandlePlaceBroadcast(data);
        }

        receivedBroadcastCount++;
    }

    /// <summary>
    /// UPDATED: Handle place broadcast using server amount for chip combination
    /// Server determines actual amount placed (may be different from request)
    /// </summary>
    private void HandlePlaceBroadcast(BetPlacedData data)
    {
        if (data.amount > 0)
        {
            // UPDATED: Use server amount directly, let component calculate chips
            AddBetToAreaFromServer(data.betOption, data.amount);

            // Track bet in history
            if (!areaBets.ContainsKey(data.betOption))
                areaBets[data.betOption] = 0;

            areaBets[data.betOption] += data.amount;
            currentTotalBet += data.amount;

            // Record in history (chipIndex is not relevant here, use 0)
            betHistory.Add(new BetAction
            {
                betOption = data.betOption,
                amount = data.amount,
                chipIndex = 0 // Not used for server-based bets
            });

            UpdateTotalBet();

            Debug.Log($"[BetController] Bet placed: {data.betOption} = {data.amount}");
        }
    }

    private void HandleUndoBroadcast(BetPlacedData data)
    {
        if (data.amount < 0)
        {
            double removeAmount = System.Math.Abs(data.amount);

            for (int i = betHistory.Count - 1; i >= 0; i--)
            {
                if (betHistory[i].betOption == data.betOption)
                {
                    betHistory.RemoveAt(i);
                    break;
                }
            }

            if (areaBets.ContainsKey(data.betOption))
            {
                areaBets[data.betOption] -= removeAmount;
                if (areaBets[data.betOption] <= 0) areaBets.Remove(data.betOption);
            }
            currentTotalBet -= removeAmount;

            RemoveLastChipFromArea(data.betOption);
            UpdateTotalBet();
        }
    }

    private void HandleCancelBroadcast(BetPlacedData data)
    {
        if (data.amount < 0)
        {
            double removeAmount = System.Math.Abs(data.amount);

            for (int i = betHistory.Count - 1; i >= 0; i--)
            {
                if (betHistory[i].betOption == data.betOption)
                {
                    betHistory.RemoveAt(i);
                    break;
                }
            }

            if (areaBets.ContainsKey(data.betOption))
            {
                areaBets[data.betOption] -= removeAmount;
                if (areaBets[data.betOption] <= 0) areaBets.Remove(data.betOption);
            }
            currentTotalBet -= removeAmount;

            RemoveLastChipFromArea(data.betOption);
            UpdateTotalBet();
        }
    }

    private void RemoveLastChipFromArea(string betOption)
    {
        if (betOption == "small" && SmallArea != null)
        {
            SmallArea.RemoveLastBet();
        }
        else if (betOption == "big" && BigArea != null)
        {
            BigArea.RemoveLastBet();
        }
        else if (betOption == "odd" && OddArea != null)
        {
            OddArea.RemoveLastBet();
        }
        else if (betOption == "even" && EvenArea != null)
        {
            EvenArea.RemoveLastBet();
        }
        else if (betOption.StartsWith("specific_3_"))
        {
            if (int.TryParse(betOption.Replace("specific_3_", ""), out int diceNum))
            {
                int index = diceNum - 1;
                if (index >= 0 && index < TripleDiceAreas.Count && TripleDiceAreas[index] != null)
                {
                    TripleDiceAreas[index].RemoveLastBet();
                }
            }
        }
        else if (betOption.StartsWith("single_"))
        {
            if (int.TryParse(betOption.Replace("single_", ""), out int diceNum))
            {
                int index = diceNum - 1;
                if (index >= 0 && index < SingleDiceAreas.Count && SingleDiceAreas[index] != null)
                {
                    SingleDiceAreas[index].RemoveLastBet();
                }
            }
        }
        else if (betOption.StartsWith("sum_"))
        {
            if (int.TryParse(betOption.Replace("sum_", ""), out int sum))
            {
                int index = sum - 4;
                if (index >= 0 && index < SumAreas.Count && SumAreas[index] != null)
                {
                    SumAreas[index].RemoveLastBet();
                }
            }
        }
    }

    internal void OnBetActionResponse(BetAckResponse response)
    {
        if (response == null || response.payload == null)
        {
            ResetBetActionState();
            return;
        }

        int betCount = response.payload.bets != null ? response.payload.bets.Count : 0;
        Debug.Log($"[BET] ACK received for {currentBetAction}: {betCount} bets received");

        if (betHistory.Count > 0)
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

    private void ResetBetActionState()
    {
        currentBetAction = "";
        receivedBroadcastCount = 0;
        isProcessingBetAction = false;
    }
    #endregion

    #region Private Methods - Bet Placement
    /// <summary>
    /// UPDATED: Removed client-side max bet validation
    /// Server handles all bet limits and sends "Limit reached" error
    /// </summary>
    private void OnBetAreaClicked(string betOption)
    {
        if (!isBettingEnabled)
        {
            uiController?.ShowInGamePopup("Betting is locked. Wait for next round.");
            return;
        }

        if (currentChipValues.Count == 0) return;

        double betAmount = currentChipValues[selectedChipIndex];

        // UPDATED: Removed CanPlaceBet - server will handle limits

        // Optimistic UI update (will be corrected by server response)
        AddBetToArea(betOption, betAmount, selectedChipIndex);

        // Send to server
        gameManager.PlaceBet(betOption, selectedChipIndex);

        CloseChipSelector();
        ShowBetActionsPanelAnimated();
        hasPlacedBetThisRound = true;
    }

    /// <summary>
    /// UPDATED: Removed client-side validation
    /// </summary>
    private void OnTripleDiceAreaClicked(int diceNum)
    {
        if (!isBettingEnabled)
        {
            uiController?.ShowInGamePopup("Betting is locked. Wait for next round.");
            return;
        }

        if (currentChipValues.Count == 0) return;

        string betOption = $"specific_3_{diceNum}";
        double betAmount = currentChipValues[selectedChipIndex];

        // UPDATED: Removed CanPlaceBet - server will handle limits

        // Optimistic UI update
        AddBetToTripleDiceArea(diceNum, betAmount, selectedChipIndex);

        // Send to server
        gameManager.PlaceBet(betOption, selectedChipIndex);

        CloseChipSelector();
        ShowBetActionsPanelAnimated();
        hasPlacedBetThisRound = true;
    }

    /// <summary>
    /// UPDATED: Removed client-side validation
    /// </summary>
    private void OnSingleDiceAreaClicked(int diceNum)
    {
        if (!isBettingEnabled)
        {
            uiController?.ShowInGamePopup("Betting is locked. Wait for next round.");
            return;
        }

        if (currentChipValues.Count == 0) return;

        string betOption = $"single_{diceNum}";
        double betAmount = currentChipValues[selectedChipIndex];

        // UPDATED: Removed CanPlaceBet - server will handle limits

        // Optimistic UI update
        AddBetToSingleDiceArea(diceNum, betAmount, selectedChipIndex);

        // Send to server
        gameManager.PlaceBet(betOption, selectedChipIndex);

        CloseChipSelector();
        ShowBetActionsPanelAnimated();
        hasPlacedBetThisRound = true;
    }

    /// <summary>
    /// NEW: Add bet to area using server amount
    /// Component automatically calculates best chip combination
    /// </summary>
    private void AddBetToAreaFromServer(string betOption, double amount)
    {
        SimpleBetArea targetArea = null;

        // Main areas
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
        }
        else if (betOption.StartsWith("sum_"))
        {
            // Handle sum bets
            if (int.TryParse(betOption.Replace("sum_", ""), out int sum))
            {
                int index = sum - 4;
                if (index >= 0 && index < SumAreas.Count &&
                    SumAreas[index]?.playerBetComponent != null)
                {
                    SumAreas[index].playerBetComponent.AddBetFromServer(amount);
                }
            }
        }
        else if (betOption.StartsWith("single_"))
        {
            // Handle single dice bets
            if (int.TryParse(betOption.Replace("single_", ""), out int diceNum))
            {
                int index = diceNum - 1;
                if (index >= 0 && index < SingleDiceAreas.Count &&
                    SingleDiceAreas[index]?.playerBetComponent != null)
                {
                    SingleDiceAreas[index].playerBetComponent.AddBetFromServer(amount);
                }
            }
        }
        else if (betOption.StartsWith("specific_3_"))
        {
            // Handle specific triple dice bets
            if (int.TryParse(betOption.Replace("specific_3_", ""), out int diceNum))
            {
                int index = diceNum - 1;
                if (index >= 0 && index < TripleDiceAreas.Count &&
                    TripleDiceAreas[index]?.playerBetComponent != null)
                {
                    TripleDiceAreas[index].playerBetComponent.AddBetFromServer(amount);
                }
            }
        }
        else
        {
            Debug.LogWarning($"[BetController] Unknown bet option: {betOption}");
        }
    }

    /// <summary>
    /// Optimistic local bet addition (will be overridden by server response)
    /// </summary>
    private void AddBetToArea(string betOption, double betAmount, int chipIndex)
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
            targetArea.playerBetComponent.AddBet(betAmount, chipIndex);
        }
        else if (betOption.StartsWith("sum_"))
        {
            if (int.TryParse(betOption.Replace("sum_", ""), out int sum))
            {
                int index = sum - 4;
                if (index >= 0 && index < SumAreas.Count &&
                    SumAreas[index]?.playerBetComponent != null)
                {
                    SumAreas[index].playerBetComponent.AddBet(betAmount, chipIndex);
                }
            }
        }

        RecordBet(betOption, betAmount, chipIndex);
    }

    private void AddBetToTripleDiceArea(int diceNum, double betAmount, int chipIndex)
    {
        int index = diceNum - 1;
        if (index >= 0 && index < TripleDiceAreas.Count && TripleDiceAreas[index] != null)
        {
            TripleDiceAreas[index].AddBet(betAmount, chipIndex);
        }

        string betOption = $"specific_3_{diceNum}";
        RecordBet(betOption, betAmount, chipIndex, diceNum);
    }

    private void AddBetToSingleDiceArea(int diceNum, double betAmount, int chipIndex)
    {
        int index = diceNum - 1;
        if (index >= 0 && index < SingleDiceAreas.Count && SingleDiceAreas[index] != null)
        {
            SingleDiceAreas[index].AddBet(betAmount, chipIndex);
        }

        string betOption = $"single_{diceNum}";
        RecordBet(betOption, betAmount, chipIndex);
    }

    private void RecordBet(string betOption, double betAmount, int chipIndex, int diceNumber = 0)
    {
        if (!areaBets.ContainsKey(betOption))
        {
            areaBets[betOption] = 0;
        }

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

    // REMOVED: CanPlaceBet() - server handles validation
    // REMOVED: GetMaxBetForArea() - not needed on client

    private int GetChipIndexForAmount(double amount)
    {
        for (int i = 0; i < currentChipValues.Count; i++)
        {
            if (System.Math.Abs(currentChipValues[i] - amount) < 0.01)
            {
                return i;
            }
        }
        return 0;
    }
    #endregion

    #region Button Handlers
    private void OnUndoClicked()
    {
        if (!isBettingEnabled || betHistory.Count == 0) return;

        currentBetAction = "UNDO_BET";
        gameManager.UndoBet();
    }

    private void OnCancelClicked()
    {
        if (!isBettingEnabled || betHistory.Count == 0) return;

        currentBetAction = "CANCEL_BET";
        gameManager.CancelAllBets();
    }

    private void OnDoubleClicked()
    {
        if (!isBettingEnabled || betHistory.Count == 0) return;

        currentBetAction = "DOUBLE_BET";
        gameManager.DoubleBet();
    }

    private void OnRepeatClicked()
    {
        if (!isBettingEnabled || previousRoundBets.Count == 0) return;

        currentBetAction = "REPEAT_BET";
        gameManager.RepeatBet();

        HideRepeatPanel();
    }
    #endregion

    #region Chip Selector
    private void OnChipSelected(int chipIndex)
    {
        if (chipIndex < 0 || chipIndex >= currentChipValues.Count) return;

        SelectChip(chipIndex);
        CloseChipSelector();
    }

    private void SelectChip(int chipIndex)
    {
        selectedChipIndex = chipIndex;

        if (chipIndex < currentChipValues.Count && chipIndex < chipSprites.Length)
        {
            if (MainChip_Image) MainChip_Image.sprite = chipSprites[chipIndex];
            if (MainChip_Text) MainChip_Text.text = FormatChipAmount(currentChipValues[chipIndex]);
        }
    }

    private void ToggleChipSelector()
    {
        if (isChipSelectorOpen)
        {
            CloseChipSelector();
        }
        else
        {
            OpenChipSelector();
        }
    }

    private void OpenChipSelector()
    {
        if (!isBettingEnabled) return;

        isChipSelectorOpen = true;

        if (ChipSelector_Panel) ChipSelector_Panel.SetActive(true);
        if (ChipSelector_BlackBG) ChipSelector_BlackBG.SetActive(true);

        AnimateChipsOpen();
    }

    private void CloseChipSelector()
    {
        if (!isChipSelectorOpen) return;

        isChipSelectorOpen = false;

        AnimateChipsClose(() =>
        {
            if (ChipSelector_Panel) ChipSelector_Panel.SetActive(false);
            if (ChipSelector_BlackBG) ChipSelector_BlackBG.SetActive(false);
        });
    }

    private void AnimateChipsOpen()
    {
        chipAnimationSequence?.Kill();
        chipAnimationSequence = DOTween.Sequence();

        for (int i = 0; i < existingChips.Count; i++)
        {
            if (existingChips[i] == null) continue;

            Vector3 startPos = centerPosition;
            Vector3 endPos = originalChipPositions[i];

            existingChips[i].transform.localPosition = startPos;
            existingChips[i].transform.localScale = Vector3.zero;
            existingChips[i].gameObject.SetActive(true);

            chipAnimationSequence.Insert(i * 0.05f,
                existingChips[i].transform.DOLocalMove(endPos, CHIP_OPEN_DURATION).SetEase(Ease.OutBack));
            chipAnimationSequence.Insert(i * 0.05f,
                existingChips[i].transform.DOScale(1f, CHIP_OPEN_DURATION).SetEase(Ease.OutBack));
        }
    }

    private void AnimateChipsClose(System.Action onComplete)
    {
        chipAnimationSequence?.Kill();
        chipAnimationSequence = DOTween.Sequence();

        for (int i = existingChips.Count - 1; i >= 0; i--)
        {
            if (existingChips[i] == null) continue;

            int reverseIndex = existingChips.Count - 1 - i;

            chipAnimationSequence.Insert(reverseIndex * 0.05f,
                existingChips[i].transform.DOLocalMove(centerPosition, CHIP_CLOSE_DURATION).SetEase(Ease.InBack));
            chipAnimationSequence.Insert(reverseIndex * 0.05f,
                existingChips[i].transform.DOScale(0f, CHIP_CLOSE_DURATION).SetEase(Ease.InBack));
        }

        chipAnimationSequence.OnComplete(() =>
        {
            foreach (var chip in existingChips)
            {
                if (chip != null) chip.gameObject.SetActive(false);
            }
            onComplete?.Invoke();
        });
    }
    #endregion

    #region Panel Animations
    private void ShowBetActionsPanelAnimated()
    {
        if (BetActionsPanelMain == null || BetActionsPanel == null) return;

        BetActionsPanelMain.SetActive(true);

        Vector2 offscreenPos = new Vector2(BetActionsPanel.anchoredPosition.x, -200f);
        Vector2 onscreenPos = new Vector2(BetActionsPanel.anchoredPosition.x, 0f);

        BetActionsPanel.anchoredPosition = offscreenPos;
        BetActionsPanel.DOAnchorPos(onscreenPos, PANEL_SLIDE_DURATION).SetEase(Ease.OutBack);
    }

    private void HideBetActionsPanel()
    {
        if (BetActionsPanelMain == null || BetActionsPanel == null) return;

        Vector2 offscreenPos = new Vector2(BetActionsPanel.anchoredPosition.x, -200f);

        BetActionsPanel.DOAnchorPos(offscreenPos, PANEL_SLIDE_DURATION)
            .SetEase(Ease.InBack)
            .OnComplete(() => BetActionsPanelMain.SetActive(false));
    }

    private void ShowRepeatPanelDelayed()
    {
        if (RepeatPanelMain == null || RepeatPanel == null) return;

        if (repeatPanelCoroutine != null)
        {
            StopCoroutine(repeatPanelCoroutine);
        }

        repeatPanelCoroutine = StartCoroutine(ShowRepeatPanelCoroutine());
    }

    private IEnumerator ShowRepeatPanelCoroutine()
    {
        yield return new WaitForSeconds(REPEAT_PANEL_DELAY);

        RepeatPanelMain.SetActive(true);

        Vector2 offscreenPos = new Vector2(RepeatPanel.anchoredPosition.x, 200f);
        Vector2 onscreenPos = new Vector2(RepeatPanel.anchoredPosition.x, 0f);

        RepeatPanel.anchoredPosition = offscreenPos;
        RepeatPanel.DOAnchorPos(onscreenPos, PANEL_SLIDE_DURATION).SetEase(Ease.OutBack);

        yield return new WaitForSeconds(REPEAT_PANEL_SHOW_DURATION);

        HideRepeatPanel();
    }

    private void HideRepeatPanel()
    {
        if (repeatPanelCoroutine != null)
        {
            StopCoroutine(repeatPanelCoroutine);
            repeatPanelCoroutine = null;
        }

        if (RepeatPanelMain == null || RepeatPanel == null) return;

        Vector2 offscreenPos = new Vector2(RepeatPanel.anchoredPosition.x, 200f);

        RepeatPanel.DOAnchorPos(offscreenPos, PANEL_SLIDE_DURATION)
            .SetEase(Ease.InBack)
            .OnComplete(() => RepeatPanelMain.SetActive(false));
    }
    #endregion

    #region Clearing & Updates
    internal void ClearAllBets()
    {
        betHistory.Clear();
        areaBets.Clear();
        currentTotalBet = 0;

        ClearArea(SmallArea);
        ClearArea(BigArea);
        ClearArea(OddArea);
        ClearArea(EvenArea);

        foreach (var area in TripleDiceAreas) ClearArea(area);
        foreach (var area in SingleDiceAreas) ClearArea(area);
        foreach (var area in SumAreas) ClearArea(area);

        UpdateTotalBet();
        HideBetActionsPanel();
    }

    internal void ClearAllWinHighlights()
    {
        SetAreaHighlight(SmallArea, false);
        SetAreaHighlight(BigArea, false);
        SetAreaHighlight(OddArea, false);
        SetAreaHighlight(EvenArea, false);

        foreach (var area in TripleDiceAreas)
        {
            if (area != null) area.SetHighlight(false);
        }

        foreach (var area in SingleDiceAreas)
        {
            if (area != null) area.SetHighlight(false);
        }

        foreach (var area in SumAreas)
        {
            if (area != null) area.SetHighlight(false);
        }
    }

    internal void HighlightWinningAreas(string matchSide, int sum)
    {
        if (matchSide == "small")
        {
            SetAreaHighlight(SmallArea, true);
        }
        else if (matchSide == "big")
        {
            SetAreaHighlight(BigArea, true);
        }

        bool isOdd = (sum % 2) != 0;
        if (isOdd)
        {
            SetAreaHighlight(OddArea, true);
        }
        else
        {
            SetAreaHighlight(EvenArea, true);
        }

        for (int i = 0; i < SumAreas.Count; i++)
        {
            int sumValue = i + 4;
            if (sumValue == sum && SumAreas[i] != null)
            {
                SumAreas[i].SetHighlight(true);
                break;
            }
        }
    }

    internal void HighlightTripleDiceResult(int dice1, int dice2, int dice3)
    {
        bool[] diceMatches = new bool[6];

        diceMatches[dice1 - 1] = true;
        diceMatches[dice2 - 1] = true;
        diceMatches[dice3 - 1] = true;

        for (int i = 0; i < SingleDiceAreas.Count && i < 6; i++)
        {
            if (SingleDiceAreas[i] != null && diceMatches[i])
            {
                SingleDiceAreas[i].SetHighlight(true);
            }
        }

        if (dice1 == dice2 && dice2 == dice3)
        {
            int tripleIndex = dice1 - 1;
            if (tripleIndex >= 0 && tripleIndex < TripleDiceAreas.Count && TripleDiceAreas[tripleIndex] != null)
            {
                TripleDiceAreas[tripleIndex].SetHighlight(true);
            }
        }
    }

    private void UpdateTotalBet()
    {
        if (TotalBet_Text)
        {
            TotalBet_Text.text = FormatChipAmount(currentTotalBet);
        }
    }

    private string FormatChipAmount(double amount)
    {
        if (amount >= 1000)
        {
            return $"{(amount / 1000):F1}K";
        }

        if (amount < 1)
        {
            return amount.ToString("F2");
        }

        if (amount % 1 != 0)
        {
            return amount.ToString("F1");
        }

        return amount.ToString("F0");
    }

    private void ClearArea(SimpleBetArea area)
    {
        if (area != null) area.ClearBets();
    }

    private void ClearArea(TripleSameDiceArea area)
    {
        if (area != null) area.ClearBets();
    }

    private void ClearArea(SingleDiceArea area)
    {
        if (area != null) area.ClearBets();
    }

    private void ClearArea(SumArea area)
    {
        if (area != null) area.ClearBets();
    }

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
        foreach (var kvp in activeComponents)
        {
            Debug.Log($"Active: {kvp.Key}");
        }
    }
    #endregion
}

#region Helper Classes
[System.Serializable]
public class BetAction
{
    public string betOption;
    public double amount;
    public int chipIndex;
    public int diceNumber;
}
#endregion

#region Bet Area Classes
[System.Serializable]
public class SimpleBetArea
{
    [Header("UI References")]
    public Button Button;
    public GameObject WinImage;
    public TMP_Text WinRatio_Text;
    public Transform PlayerBetContainer;

    [Header("Pooled Component - Assigned at Runtime")]
    [HideInInspector] public PlayerBetComponent playerBetComponent;

    public void SetWinRatio(string ratio)
    {
        if (WinRatio_Text) WinRatio_Text.text = ratio;
    }

    public void AddBet(double amount, int chipIndex)
    {
        if (playerBetComponent != null)
        {
            playerBetComponent.AddBet(amount, chipIndex);
        }
    }

    public void RemoveLastBet()
    {
        if (playerBetComponent != null)
        {
            playerBetComponent.RemoveLastBet();
        }
    }

    public void ClearBets()
    {
        if (playerBetComponent != null)
        {
            playerBetComponent.Clear();
        }
    }

    public double GetTotalBet()
    {
        return playerBetComponent != null ? playerBetComponent.GetTotalBet() : 0;
    }

    public bool HasBets()
    {
        return playerBetComponent != null && playerBetComponent.HasBets();
    }

    public void SetHighlight(bool highlight)
    {
        if (WinImage) WinImage.SetActive(highlight);
    }
}

[System.Serializable]
public class TripleSameDiceArea
{
    [Header("UI References")]
    public Button Button;
    public GameObject WinImage;
    public Transform PlayerBetContainer;

    [Header("Pooled Component - Assigned at Runtime")]
    [HideInInspector] public PlayerBetComponent playerBetComponent;

    public void AddBet(double amount, int chipIndex)
    {
        if (playerBetComponent != null)
        {
            playerBetComponent.AddBet(amount, chipIndex);
        }
    }

    public void RemoveLastBet()
    {
        if (playerBetComponent != null)
        {
            playerBetComponent.RemoveLastBet();
        }
    }

    public void ClearBets()
    {
        if (playerBetComponent != null)
        {
            playerBetComponent.Clear();
        }
    }

    public double GetTotalBet()
    {
        return playerBetComponent != null ? playerBetComponent.GetTotalBet() : 0;
    }

    public bool HasBets()
    {
        return playerBetComponent != null && playerBetComponent.HasBets();
    }

    public void SetHighlight(bool highlight)
    {
        if (WinImage) WinImage.SetActive(highlight);
    }
}

[System.Serializable]
public class SingleDiceArea
{
    [Header("UI References")]
    public Button Button;
    public GameObject WinImage;
    public Transform PlayerBetContainer;

    [Header("Pooled Component - Assigned at Runtime")]
    [HideInInspector] public PlayerBetComponent playerBetComponent;

    public void AddBet(double amount, int chipIndex)
    {
        if (playerBetComponent != null)
        {
            playerBetComponent.AddBet(amount, chipIndex);
        }
    }

    public void RemoveLastBet()
    {
        if (playerBetComponent != null)
        {
            playerBetComponent.RemoveLastBet();
        }
    }

    public void ClearBets()
    {
        if (playerBetComponent != null)
        {
            playerBetComponent.Clear();
        }
    }

    public double GetTotalBet()
    {
        return playerBetComponent != null ? playerBetComponent.GetTotalBet() : 0;
    }

    public bool HasBets()
    {
        return playerBetComponent != null && playerBetComponent.HasBets();
    }

    public void SetHighlight(bool highlight)
    {
        if (WinImage) WinImage.SetActive(highlight);
    }
}

[System.Serializable]
public class SumArea
{
    [Header("UI References")]
    public Button Button;
    public GameObject WinImage;
    public TMP_Text WinRatio_Text;
    public Transform PlayerBetContainer;

    [Header("Pooled Component - Assigned at Runtime")]
    [HideInInspector] public PlayerBetComponent playerBetComponent;

    public void SetWinRatio(string ratio)
    {
        if (WinRatio_Text) WinRatio_Text.text = ratio;
    }

    public void AddBet(double amount, int chipIndex)
    {
        if (playerBetComponent != null)
        {
            playerBetComponent.AddBet(amount, chipIndex);
        }
    }

    public void RemoveLastBet()
    {
        if (playerBetComponent != null)
        {
            playerBetComponent.RemoveLastBet();
        }
    }

    public void ClearBets()
    {
        if (playerBetComponent != null)
        {
            playerBetComponent.Clear();
        }
    }

    public double GetTotalBet()
    {
        return playerBetComponent != null ? playerBetComponent.GetTotalBet() : 0;
    }

    public bool HasBets()
    {
        return playerBetComponent != null && playerBetComponent.HasBets();
    }

    public void SetHighlight(bool highlight)
    {
        if (WinImage) WinImage.SetActive(highlight);
    }
}
#endregion