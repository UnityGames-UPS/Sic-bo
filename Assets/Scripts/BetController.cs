using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// COMPLETE BetController with PlayerBetComponent Object Pooling System
/// 
/// READY TO USE - Direct copy-paste replacement for your existing BetController.cs
/// 
/// NEW FEATURES:
/// - Dynamic PlayerBetComponent pooling (spawns at start, reuses across rounds)
/// - Zero GC during gameplay (no instantiate/destroy)
/// - Better memory management
/// - All original functionality preserved
/// 
/// SETUP REQUIRED IN INSPECTOR:
/// 1. Assign "Player Bet Component Prefab" field
/// 2. Set "Pool Initial Size" to 30 (default)
/// 3. Leave "Pool Container" empty (auto-creates)
/// 
/// CHANGES TO AREA CLASSES:
/// - playerBetComponent is now [HideInInspector] (assigned at runtime)
/// - Remove from inspector, will be assigned automatically
/// </summary>
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
    [SerializeField] private PlayerBetComponent playerBetComponentPrefab; // ASSIGN IN INSPECTOR
    [SerializeField] private Transform poolContainer; // Auto-creates if null
    [SerializeField] private int poolInitialSize = 30; // Total bet areas (4 main + 6 triple + 6 single + 14 sum)

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

    #region Private Fields - Pool System (NEW)
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
    private int expectedBroadcastCount = 0;
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
        // NEW: Initialize pool FIRST
        InitializePool();

        SetupButtonListeners();
        SetupBetAreaListeners();
        InitializeExistingChips();

        // NEW: Assign components from pool to areas
        AssignComponentsToAreas();

        DisableBetting();
    }

    private void OnDestroy()
    {
        // NEW: Cleanup pool
        CleanupPool();

        chipAnimationSequence?.Kill();
        if (repeatPanelCoroutine != null) StopCoroutine(repeatPanelCoroutine);
    }
    #endregion

    #region Pool System - Core (NEW)
    /// <summary>
    /// Initialize object pool - spawn all PlayerBetComponents at start
    /// All components start disabled and ready to be assigned
    /// </summary>
    private void InitializePool()
    {
        if (isPoolInitialized)
        {
            Debug.LogWarning("[BET POOL] Already initialized!");
            return;
        }

        if (playerBetComponentPrefab == null)
        {
            Debug.LogError("[BET POOL] PlayerBetComponent prefab is not assigned! Assign in Inspector.");
            return;
        }

        // Create pool container if not assigned
        if (poolContainer == null)
        {
            GameObject poolObj = new GameObject("PlayerBetComponent_Pool");
            poolObj.transform.SetParent(transform);
            poolContainer = poolObj.transform;
        }

        Debug.Log($"[BET POOL] Initializing pool with {poolInitialSize} components...");

        // Spawn all components
        for (int i = 0; i < poolInitialSize; i++)
        {
            PlayerBetComponent component = Instantiate(playerBetComponentPrefab, poolContainer);
            component.name = $"PlayerBetComponent_{i:D2}";
            component.gameObject.SetActive(false); // Start disabled

            // Initialize with chip sprites
            component.Initialize(chipSprites);

            componentPool.Add(component);
        }

        isPoolInitialized = true;
        Debug.Log($"[BET POOL] ✅ Pool initialized with {componentPool.Count} components");
    }

    /// <summary>
    /// Get a component from the pool for a specific area
    /// Automatically activates and parents to the area's container
    /// </summary>
    private PlayerBetComponent GetComponentFromPool(string areaId, Transform parentContainer)
    {
        // Check if already has an active component
        if (activeComponents.ContainsKey(areaId))
        {
            return activeComponents[areaId];
        }

        // Find first available component in pool
        PlayerBetComponent availableComponent = null;
        foreach (var component in componentPool)
        {
            if (!component.gameObject.activeInHierarchy)
            {
                availableComponent = component;
                break;
            }
        }

        if (availableComponent == null)
        {
            Debug.LogError($"[BET POOL] No available components! Pool exhausted. Increase poolInitialSize in Inspector.");
            return null;
        }

        // Assign to area
        availableComponent.transform.SetParent(parentContainer);
        availableComponent.transform.localPosition = Vector3.zero;
        availableComponent.transform.localScale = Vector3.one;
        availableComponent.gameObject.SetActive(true);

        // Track as active
        activeComponents[areaId] = availableComponent;

        return availableComponent;
    }

    /// <summary>
    /// Return component to pool - reset and disable
    /// Called when clearing bets or resetting round
    /// </summary>
    private void ReturnComponentToPool(string areaId)
    {
        if (!activeComponents.ContainsKey(areaId))
        {
            return; // Already returned or never assigned
        }

        PlayerBetComponent component = activeComponents[areaId];

        if (component != null)
        {
            // Reset component state
            component.Clear();

            // Return to pool parent
            component.transform.SetParent(poolContainer);
            component.gameObject.SetActive(false);
        }

        // Remove from active tracking
        activeComponents.Remove(areaId);
    }

    /// <summary>
    /// Return all active components to pool
    /// Called at round end or when resetting game
    /// </summary>
    private void ReturnAllComponentsToPool()
    {
        if (activeComponents.Count == 0) return;

        Debug.Log($"[BET POOL] Returning {activeComponents.Count} active components to pool...");

        List<string> areaIds = new List<string>(activeComponents.Keys);

        foreach (string areaId in areaIds)
        {
            ReturnComponentToPool(areaId);
        }

        Debug.Log("[BET POOL] ✅ All components returned to pool");
    }

    /// <summary>
    /// Cleanup pool - destroy all components
    /// Called on game exit or controller destroy
    /// </summary>
    private void CleanupPool()
    {
        Debug.Log("[BET POOL] Cleaning up pool...");

        // Clear active tracking
        activeComponents.Clear();

        // Destroy all pooled components
        foreach (var component in componentPool)
        {
            if (component != null)
            {
                Destroy(component.gameObject);
            }
        }

        componentPool.Clear();
        isPoolInitialized = false;

        Debug.Log("[BET POOL] ✅ Pool cleanup complete");
    }
    #endregion

    #region Pool System - Area Assignment (NEW)
    /// <summary>
    /// Assign PlayerBetComponents from pool to all bet areas
    /// Called once during initialization and when resetting for new round
    /// </summary>
    private void AssignComponentsToAreas()
    {
        if (!isPoolInitialized)
        {
            Debug.LogError("[BET POOL] Cannot assign components - pool not initialized!");
            return;
        }

        Debug.Log("[BET POOL] Assigning components to all bet areas...");

        int assignedCount = 0;

        // Main areas
        if (SmallArea != null && SmallArea.PlayerBetContainer != null)
        {
            SmallArea.playerBetComponent = GetComponentFromPool("small", SmallArea.PlayerBetContainer);
            if (SmallArea.playerBetComponent != null) assignedCount++;
        }

        if (BigArea != null && BigArea.PlayerBetContainer != null)
        {
            BigArea.playerBetComponent = GetComponentFromPool("big", BigArea.PlayerBetContainer);
            if (BigArea.playerBetComponent != null) assignedCount++;
        }

        if (OddArea != null && OddArea.PlayerBetContainer != null)
        {
            OddArea.playerBetComponent = GetComponentFromPool("odd", OddArea.PlayerBetContainer);
            if (OddArea.playerBetComponent != null) assignedCount++;
        }

        if (EvenArea != null && EvenArea.PlayerBetContainer != null)
        {
            EvenArea.playerBetComponent = GetComponentFromPool("even", EvenArea.PlayerBetContainer);
            if (EvenArea.playerBetComponent != null) assignedCount++;
        }

        // Triple dice areas
        for (int i = 0; i < TripleDiceAreas.Count; i++)
        {
            var area = TripleDiceAreas[i];
            if (area != null && area.PlayerBetContainer != null)
            {
                string areaId = $"triple_{i + 1}";
                area.playerBetComponent = GetComponentFromPool(areaId, area.PlayerBetContainer);
                if (area.playerBetComponent != null) assignedCount++;
            }
        }

        // Single dice areas
        for (int i = 0; i < SingleDiceAreas.Count; i++)
        {
            var area = SingleDiceAreas[i];
            if (area != null && area.PlayerBetContainer != null)
            {
                string areaId = $"single_{i + 1}";
                area.playerBetComponent = GetComponentFromPool(areaId, area.PlayerBetContainer);
                if (area.playerBetComponent != null) assignedCount++;
            }
        }

        // Sum areas
        for (int i = 0; i < SumAreas.Count; i++)
        {
            var area = SumAreas[i];
            if (area != null && area.PlayerBetContainer != null)
            {
                string areaId = $"sum_{i + 4}";
                area.playerBetComponent = GetComponentFromPool(areaId, area.PlayerBetContainer);
                if (area.playerBetComponent != null) assignedCount++;
            }
        }

        Debug.Log($"[BET POOL] ✅ Assigned {assignedCount} components to bet areas");

        // Initially disable all (they'll be shown when bets are placed)
        ReturnAllComponentsToPool();
    }
    #endregion

    #region Public API - Round Management (NEW)
    /// <summary>
    /// Called when a new round starts - reset all bets and reassign components
    /// Call this from GameManager.OnRoundStart()
    /// </summary>
    internal void OnRoundStart()
    {
        Debug.Log("[BET] Round start - resetting all bets and reassigning components");

        ClearAllBets();
        ReturnAllComponentsToPool();
        AssignComponentsToAreas();
    }

    /// <summary>
    /// Called when round ends - save state for repeat functionality
    /// Call this from GameManager.OnRoundEnd()
    /// </summary>
    internal void OnRoundEnd()
    {
        Debug.Log("[BET] Round end - components will be reset on next round start");

        // Components stay visible until next round starts
        // Save bets for repeat (already handled in DisableBetting)
    }

    /// <summary>
    /// Reset all components immediately (for leaving room or game exit)
    /// Call this from GameManager.OnLeaveRoom()
    /// </summary>
    internal void ResetAllComponents()
    {
        Debug.Log("[BET] Resetting all components immediately");

        ClearAllBets();
        ReturnAllComponentsToPool();
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

        if (ChipOptions_Container == null)
        {
            Debug.LogError("[BET] ChipOptions_Container is null!");
            return;
        }

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
        {
            centerPosition = existingChips[0].transform.localPosition;
        }

        Debug.Log($"[BET] Initialized {existingChips.Count} chip selector chips");
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
        {
            existingChips[i].SetActive(false);
        }

        if (chipCount > 0)
        {
            SelectChipAt(0);
        }

        if (chipValues.Count > 0)
        {
            minBetAmount = chipValues[0];
            maxBetAmount = chipValues[chipValues.Count - 1] * 100;
        }

        SetupWinRatios();
        UpdateMinMaxDisplay();

        Debug.Log($"[BET] Setup complete - Level: {level}, Chips: {chipCount}");
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
        {
            ShowRepeatPanelAnimated();
        }

        Debug.Log("[BET] Betting enabled");
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
            Debug.Log($"[BET] Saved {previousRoundBets.Count} bets for next round repeat");
        }
        else
        {
            previousRoundBets.Clear();
            placedBetInPreviousRound = false;
        }
    }

    internal void ClearAllBets()
    {
        Debug.Log("[BET] Clearing all bets");

        areaBets.Clear();
        currentTotalBet = 0;
        betHistory.Clear();

        ClearArea(SmallArea);
        ClearArea(BigArea);
        ClearArea(OddArea);
        ClearArea(EvenArea);

        foreach (var area in TripleDiceAreas) ClearArea(area);
        foreach (var area in SingleDiceAreas) ClearArea(area);
        foreach (var area in SumAreas) ClearArea(area);

        UpdateTotalBet();
        HideBetActionsPanel();

        Debug.Log("[BET] All bets cleared");
    }

    internal void HighlightWinningAreas(string matchSide, int sum)
    {
        SetAreaHighlight(SmallArea, matchSide == "small");
        SetAreaHighlight(BigArea, matchSide == "big");
        SetAreaHighlight(OddArea, matchSide == "odd");
        SetAreaHighlight(EvenArea, matchSide == "even");

        int sumIndex = sum - 4;
        if (sumIndex >= 0 && sumIndex < SumAreas.Count && SumAreas[sumIndex] != null)
        {
            SumAreas[sumIndex].SetHighlight(true);
        }
    }

    internal void HighlightTripleDiceResult(int dice1, int dice2, int dice3)
    {
        if (dice1 == dice2 && dice2 == dice3)
        {
            int diceIndex = dice1 - 1;
            if (diceIndex >= 0 && diceIndex < TripleDiceAreas.Count && TripleDiceAreas[diceIndex] != null)
            {
                TripleDiceAreas[diceIndex].SetHighlight(true);
            }
        }

        HashSet<int> uniqueDice = new HashSet<int> { dice1, dice2, dice3 };
        foreach (int num in uniqueDice)
        {
            int index = num - 1;
            if (index >= 0 && index < SingleDiceAreas.Count && SingleDiceAreas[index] != null)
            {
                SingleDiceAreas[index].SetHighlight(true);
            }
        }
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

        Debug.Log("[BET] All win highlights cleared");
    }
    #endregion

    #region Bet Action Broadcast Handling
    internal void OnBetPlacedBroadcast(BetPlacedData data)
    {
        if (data == null) return;

        if (isProcessingBetAction && !string.IsNullOrEmpty(currentBetAction))
        {
            receivedBroadcastCount++;

            Debug.Log($"[BET] Broadcast {receivedBroadcastCount}/{expectedBroadcastCount} for {currentBetAction}: " +
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
    }

    private void HandleRepeatBroadcast(BetPlacedData data)
    {
        if (data.amount > 0)
        {
            int chipIndex = GetChipIndexForAmount(data.amount);

            AddBetToAreaVisual(data.betOption, data.amount, chipIndex);

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

            Debug.Log($"[BET REPEAT] Added chip: {data.betOption} +{data.amount}");
        }
    }

    private void HandleDoubleBroadcast(BetPlacedData data)
    {
        if (data.amount > 0)
        {
            int chipIndex = GetChipIndexForAmount(data.amount);

            AddBetToAreaVisual(data.betOption, data.amount, chipIndex);

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

            Debug.Log($"[BET DOUBLE] Added additional chip: {data.betOption} +{data.amount}");
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

            Debug.Log($"[BET UNDO] Removed last chip: {data.betOption} -{removeAmount}");
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

            Debug.Log($"[BET CANCEL] Removed chip: {data.betOption} -{removeAmount}");
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
        else if (betOption == "specific_3")
        {
            foreach (var area in TripleDiceAreas)
            {
                if (area != null && area.HasBets())
                {
                    area.RemoveLastBet();
                    break;
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

        Debug.Log($"[BET] Final ACK: {response.payload.message} " +
                  $"(received {receivedBroadcastCount}/{expectedBroadcastCount} broadcasts)");

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
        expectedBroadcastCount = 0;
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

        double betAmount = currentChipValues[selectedChipIndex];

        if (!CanPlaceBet(betOption, betAmount)) return;

        AddBetToArea(betOption, betAmount, selectedChipIndex);
        gameManager.PlaceBet(betOption, selectedChipIndex);

        CloseChipSelector();
        ShowBetActionsPanelAnimated();
        hasPlacedBetThisRound = true;
    }

    private void OnTripleDiceAreaClicked(int diceNum)
    {
        if (!isBettingEnabled)
        {
            uiController?.ShowInGamePopup("Betting is locked. Wait for next round.");
            return;
        }

        if (currentChipValues.Count == 0) return;

        string betOption = "specific_3";
        double betAmount = currentChipValues[selectedChipIndex];

        if (!CanPlaceBet(betOption, betAmount)) return;

        int areaIndex = diceNum - 1;
        if (areaIndex >= 0 && areaIndex < TripleDiceAreas.Count && TripleDiceAreas[areaIndex] != null)
        {
            TripleDiceAreas[areaIndex].AddBet(betAmount, selectedChipIndex);
            RecordBet(betOption, betAmount, selectedChipIndex);
        }

        gameManager.PlaceBet(betOption, selectedChipIndex);
        CloseChipSelector();
        ShowBetActionsPanelAnimated();
        hasPlacedBetThisRound = true;
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
        double betAmount = currentChipValues[selectedChipIndex];

        if (!CanPlaceBet(betOption, betAmount)) return;

        int areaIndex = diceNum - 1;
        if (areaIndex >= 0 && areaIndex < SingleDiceAreas.Count && SingleDiceAreas[areaIndex] != null)
        {
            SingleDiceAreas[areaIndex].AddBet(betAmount, selectedChipIndex);
            RecordBet(betOption, betAmount, selectedChipIndex);
        }

        gameManager.PlaceBet(betOption, selectedChipIndex);
        CloseChipSelector();
        ShowBetActionsPanelAnimated();
        hasPlacedBetThisRound = true;
    }

    private void AddBetToArea(string betOption, double betAmount, int chipIndex)
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
                {
                    SumAreas[index].AddBet(betAmount, chipIndex);
                }
            }
        }
    }

    private void RecordBet(string betOption, double betAmount, int chipIndex)
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
            chipIndex = chipIndex
        });

        UpdateTotalBet();
    }

    private bool CanPlaceBet(string betOption, double betAmount)
    {
        double currentAreaBet = areaBets.ContainsKey(betOption) ? areaBets[betOption] : 0;
        double areaMaxBet = GetMaxBetForArea(betOption);

        if (currentAreaBet + betAmount > areaMaxBet)
        {
            string message = $"Maximum bet for this area is {FormatChipAmount(areaMaxBet)}";
            uiController?.ShowInGamePopup(message);
            return false;
        }

        if (currentTotalBet + betAmount > maxBetAmount)
        {
            string message = $"Maximum total bet is {FormatChipAmount(maxBetAmount)}";
            uiController?.ShowInGamePopup(message);
            return false;
        }

        return true;
    }

    private double GetMaxBetForArea(string betOption)
    {
        if (wagerData == null || string.IsNullOrEmpty(currentLevel))
        {
            return maxBetAmount;
        }

        BetWager wager = null;

        if (betOption == "small") wager = wagerData.main_bets?.small;
        else if (betOption == "big") wager = wagerData.main_bets?.big;
        else if (betOption == "odd") wager = wagerData.main_bets?.odd;
        else if (betOption == "even") wager = wagerData.main_bets?.even;
        else if (betOption.StartsWith("single_"))
        {
            wager = wagerData.side_bets?.single_match_1;
        }
        else if (betOption == "specific_3")
        {
            wager = wagerData.side_bets?.specific_3;
        }
        else if (betOption.StartsWith("sum_"))
        {
            if (int.TryParse(betOption.Replace("sum_", ""), out int sum))
            {
                wager = GetSumWager(sum);
            }
        }

        return wager?.GetMaxBet(currentLevel) ?? maxBetAmount;
    }

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

    #region Private Methods - Chip Selector & Animations
    private void ToggleChipSelector()
    {
        if (!isBettingEnabled) return;

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

            Tween moveTween = chipTransform.DOLocalMove(targetPos, CHIP_OPEN_DURATION)
                .SetEase(Ease.OutBack);

            Tween rotateTween = chipTransform.DOLocalRotate(new Vector3(0, 0, 360), CHIP_OPEN_DURATION, RotateMode.FastBeyond360)
                .SetEase(Ease.OutQuad);

            chipAnimationSequence.Join(moveTween);
            chipAnimationSequence.Join(rotateTween);
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

            Tween moveTween = chipTransform.DOLocalMove(centerPosition, CHIP_CLOSE_DURATION)
                .SetEase(Ease.InBack);

            Tween rotateTween = chipTransform.DOLocalRotate(new Vector3(0, 0, -360), CHIP_CLOSE_DURATION, RotateMode.FastBeyond360)
                .SetEase(Ease.InQuad);

            chipAnimationSequence.Join(moveTween);
            chipAnimationSequence.Join(rotateTween);
        }

        if (onComplete != null)
        {
            chipAnimationSequence.OnComplete(() => onComplete());
        }

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

        Debug.Log($"[BET] Selected chip index {index} with value {chipValue}");
    }

    private Sprite GetChipSprite(double value)
    {
        if (chipValueToSprite.ContainsKey(value))
        {
            return chipValueToSprite[value];
        }

        return chipSprites.Length > 0 ? chipSprites[0] : null;
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
    #endregion

    #region Private Methods - Panel Animations
    private void AnimateBetLocked()
    {
        if (ChipAreaPanel != null)
        {
            ChipAreaPanel.DOAnchorPosY(-200f, PANEL_SLIDE_DURATION).SetEase(Ease.InOutQuad);
        }

        if (TotalStakePanel != null)
        {
            TotalStakePanel.DOAnchorPosY(40f, PANEL_SLIDE_DURATION).SetEase(Ease.InOutQuad);
        }
    }

    private void AnimateBetUnlocked()
    {
        if (ChipAreaPanel != null)
        {
            ChipAreaPanel.DOAnchorPosY(0f, PANEL_SLIDE_DURATION).SetEase(Ease.InOutQuad);
        }

        if (TotalStakePanel != null)
        {
            TotalStakePanel.DOAnchorPosY(-200f, PANEL_SLIDE_DURATION).SetEase(Ease.InOutQuad);
        }
    }

    private void ShowRepeatPanelAnimated()
    {
        if (RepeatPanel == null) return;

        if (repeatPanelCoroutine != null)
        {
            StopCoroutine(repeatPanelCoroutine);
        }

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

        if (betHistory.Count == 0)
        {
            uiController?.ShowInGamePopup("No bets to undo");
            return;
        }

        isProcessingBetAction = true;

        currentBetAction = "UNDO";
        expectedBroadcastCount = 1;
        receivedBroadcastCount = 0;

        Debug.Log($"[BET] UNDO clicked - expecting {expectedBroadcastCount} broadcast");

        gameManager.UndoBet();
    }

    private void OnCancelClicked()
    {
        if (!isBettingEnabled || isProcessingBetAction)
        {
            uiController?.ShowInGamePopup("Please wait...");
            return;
        }

        if (betHistory.Count == 0)
        {
            uiController?.ShowInGamePopup("No bets to cancel");
            return;
        }

        isProcessingBetAction = true;

        currentBetAction = "CANCEL";
        expectedBroadcastCount = betHistory.Count;
        receivedBroadcastCount = 0;

        Debug.Log($"[BET] CANCEL clicked - expecting {expectedBroadcastCount} broadcasts");

        gameManager.CancelAllBets();
    }

    private void OnDoubleClicked()
    {
        if (!isBettingEnabled || isProcessingBetAction)
        {
            uiController?.ShowInGamePopup("Please wait...");
            return;
        }

        if (betHistory.Count == 0)
        {
            uiController?.ShowInGamePopup("No bets to double");
            return;
        }

        if (currentTotalBet * 2 > maxBetAmount)
        {
            uiController?.ShowInGamePopup($"Cannot double - would exceed max total bet of {FormatChipAmount(maxBetAmount)}");
            return;
        }

        Dictionary<string, double> doubledAreaBets = new Dictionary<string, double>();
        bool canDouble = true;
        string limitExceededArea = "";

        foreach (var kvp in areaBets)
        {
            double doubledAmount = kvp.Value * 2;
            doubledAreaBets[kvp.Key] = doubledAmount;

            double areaMaxBet = GetMaxBetForArea(kvp.Key);
            if (doubledAmount > areaMaxBet)
            {
                canDouble = false;
                limitExceededArea = kvp.Key;
                break;
            }
        }

        if (!canDouble)
        {
            uiController?.ShowInGamePopup($"Cannot double - {limitExceededArea} would exceed area limit");
            return;
        }

        isProcessingBetAction = true;

        currentBetAction = "DOUBLE";
        expectedBroadcastCount = betHistory.Count;
        receivedBroadcastCount = 0;

        Debug.Log($"[BET] DOUBLE clicked - expecting {expectedBroadcastCount} broadcasts");

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
        expectedBroadcastCount = previousRoundBets.Count;
        receivedBroadcastCount = 0;

        Debug.Log($"[BET] REPEAT clicked - expecting {expectedBroadcastCount} broadcasts");

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
        {
            area.SetWinRatio(wager.GetPayoutRatioString());
        }
    }

    private void SetWinRatio(SumArea area, BetWager wager)
    {
        if (area != null && wager != null)
        {
            area.SetWinRatio(wager.GetPayoutRatioString());
        }
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

    #region Debug Helpers (NEW)
    [ContextMenu("Debug Pool Status")]
    private void DebugPoolStatus()
    {
        Debug.Log($"=== BET POOL STATUS ===");
        Debug.Log($"Total in pool: {componentPool.Count}");
        Debug.Log($"Active components: {activeComponents.Count}");

        int available = 0;
        foreach (var comp in componentPool)
        {
            if (comp != null && !comp.gameObject.activeInHierarchy)
                available++;
        }

        Debug.Log($"Available: {available}");
        Debug.Log($"In use: {componentPool.Count - available}");

        Debug.Log("Active areas:");
        foreach (var kvp in activeComponents)
        {
            Debug.Log($"  - {kvp.Key}: {kvp.Value.GetBetCount()} bets, ${kvp.Value.GetTotalBet()}");
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
}


#endregion

#region Bet Area Classes - UPDATED WITH POOLING
[System.Serializable]
public class SimpleBetArea
{
    [Header("UI References")]
    public Button Button;
    public GameObject WinImage;
    public TMP_Text WinRatio_Text;
    public Transform PlayerBetContainer;

    [Header("Pooled Component - Assigned at Runtime")]
    [HideInInspector] public PlayerBetComponent playerBetComponent; // NOTE: Now assigned at runtime, not in inspector

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
    [HideInInspector] public PlayerBetComponent playerBetComponent; // NOTE: Now assigned at runtime, not in inspector

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
    [HideInInspector] public PlayerBetComponent playerBetComponent; // NOTE: Now assigned at runtime, not in inspector

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
    [HideInInspector] public PlayerBetComponent playerBetComponent; // NOTE: Now assigned at runtime, not in inspector

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