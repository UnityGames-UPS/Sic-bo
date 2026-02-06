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

    [Header("Chip Prefabs")]
    [SerializeField] private GameObject chipSelectorPrefab;
    [SerializeField] private GameObject playerChipStackPrefab;
    [SerializeField] private GameObject opponentChipPrefab;
    [SerializeField] private Sprite[] chipSprites;
    [SerializeField] private Sprite grayChipSprite;

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

    #region Private Fields
    private List<double> currentChipValues = new List<double>();
    private Dictionary<double, Sprite> chipValueToSprite = new Dictionary<double, Sprite>();
    private List<Chip> existingChips = new List<Chip>();
    private List<Vector3> originalChipPositions = new List<Vector3>(); 
    private Vector3 centerPosition;
    private int selectedChipIndex = 0;
    private double currentTotalBet = 0;
    private bool isBettingEnabled = false;
    private bool isChipSelectorOpen = false;
    private Dictionary<string, double> areaBets = new Dictionary<string, double>();
    private List<BetAction> betHistory = new List<BetAction>(); 
    private Wagers wagerData = null;
    private string currentLevel = "";
    private double minBetAmount = 0;
    private double maxBetAmount = 0;
    private bool placedBetInPreviousRound = false;
    private bool hasPlacedBetThisRound = false;

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
        SetupButtonListeners();
        SetupBetAreaListeners();
        InitializeExistingChips();
        DisableBetting();
    }

    private void OnDestroy()
    {
        // Clean up tweens
        chipAnimationSequence?.Kill();
        if (repeatPanelCoroutine != null) StopCoroutine(repeatPanelCoroutine);
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

    /// <summary>
    /// Initialize references to existing 6 chips in scene and calculate positions
    /// </summary>
    private void InitializeExistingChips()
    {
        existingChips.Clear();
        originalChipPositions.Clear();

        if (ChipOptions_Container == null)
        {
            Debug.LogError("[BET] ChipOptions_Container is null!");
            return;
        }

        // Get all Chip components from children
        Chip[] chips = ChipOptions_Container.GetComponentsInChildren<Chip>(true);

        // Store up to 6 chips
        for (int i = 0; i < Mathf.Min(6, chips.Length); i++)
        {
            existingChips.Add(chips[i]);
            originalChipPositions.Add(chips[i].transform.localPosition);

            // Add button listener for chip selection
            Button chipButton = chips[i].GetComponent<Button>();
            if (chipButton != null)
            {
                int index = i;
                chipButton.onClick.RemoveAllListeners();
                chipButton.onClick.AddListener(() => OnChipSelected(index));
            }
        }

        // Calculate center position (average of all original positions)
        if (originalChipPositions.Count > 0)
        {
            centerPosition = Vector3.zero;
            foreach (Vector3 pos in originalChipPositions)
            {
                centerPosition += pos;
            }
            centerPosition /= originalChipPositions.Count;
        }

        Debug.Log($"[BET] Initialized {existingChips.Count} existing chips. Center position: {centerPosition}");
    }
    #endregion

    #region Public API - Chip Setup
    internal void SetupChips(List<double> chipValues, Wagers wagers, string level)
    {
        currentChipValues = chipValues ?? new List<double>();
        wagerData = wagers;
        currentLevel = level;

        if (currentChipValues.Count == 0) return;

        Debug.Log($"[BET] SetupChips called with {currentChipValues.Count} chip values");

        BuildChipValueToSpriteMap();
        ConfigureExistingChips();
        SelectChipAt(0); // Default to index 0
        SetupMinMaxBet();
        SetupWinRatios();
    }

    private void BuildChipValueToSpriteMap()
    {
        chipValueToSprite.Clear();
        int maxIndex = Mathf.Min(currentChipValues.Count, chipSprites.Length);

        for (int i = 0; i < maxIndex; i++)
        {
            chipValueToSprite[currentChipValues[i]] = chipSprites[i];
        }
    }

    /// <summary>
    /// Configure existing chips instead of spawning new ones
    /// </summary>
    private void ConfigureExistingChips()
    {
        int chipsToUse = Mathf.Min(currentChipValues.Count, existingChips.Count);

        // Configure chips with data
        for (int i = 0; i < chipsToUse; i++)
        {
            Chip chip = existingChips[i];
            Sprite chipSprite = GetChipSprite(currentChipValues[i]);
            chip.SetData(chipSprite, FormatChipAmount(currentChipValues[i]), i);
            chip.gameObject.SetActive(true);

            // Set to center position initially
            chip.transform.localPosition = centerPosition;
        }

        // Hide unused chips
        for (int i = chipsToUse; i < existingChips.Count; i++)
        {
            existingChips[i].gameObject.SetActive(false);
        }

        // If we have more chip values than existing chips (>6), spawn additional ones
        if (currentChipValues.Count > existingChips.Count)
        {
            Debug.LogWarning($"[BET] Backend sent {currentChipValues.Count} chips but only {existingChips.Count} exist in scene. Spawning additional chips.");
            // TODO: Spawn additional chips if needed
        }

        Debug.Log($"[BET] Configured {chipsToUse} existing chips");
    }

    private void SetupMinMaxBet()
    {
        if (currentChipValues.Count > 0)
        {
            minBetAmount = currentChipValues[0];

            if (wagerData?.main_bets?.small != null)
            {
                maxBetAmount = wagerData.main_bets.small.GetMaxBet(currentLevel);
            }
        }

        if (MinBet_Text) MinBet_Text.text = $"Min: {FormatChipAmount(minBetAmount)}";
        if (MaxBet_Text) MaxBet_Text.text = $"Max: {FormatChipAmount(maxBetAmount)}";
    }

    private void SetupWinRatios()
    {
        if (wagerData == null) return;

        if (wagerData.main_bets != null)
        {
            SetWinRatio(SmallArea, wagerData.main_bets.small);
            SetWinRatio(BigArea, wagerData.main_bets.big);
            SetWinRatio(OddArea, wagerData.main_bets.odd);
            SetWinRatio(EvenArea, wagerData.main_bets.even);
        }

        if (wagerData.side_bets != null && SharedTripleWinRatio_Text != null)
        {
            string combinedRatio = BetWager.GetCombinedSpecificPayoutString(
                wagerData.side_bets.specific_2,
                wagerData.side_bets.specific_3
            );
            SharedTripleWinRatio_Text.text = combinedRatio;
        }

        if (wagerData.op_bets != null)
        {
            var opBets = wagerData.op_bets;
            BetWager[] sumWagers = {
                opBets.sum_4, opBets.sum_5, opBets.sum_6, opBets.sum_7,
                opBets.sum_8, opBets.sum_9, opBets.sum_10, opBets.sum_11,
                opBets.sum_12, opBets.sum_13, opBets.sum_14, opBets.sum_15,
                opBets.sum_16, opBets.sum_17
            };

            for (int i = 0; i < Mathf.Min(sumWagers.Length, SumAreas.Count); i++)
            {
                SetWinRatio(SumAreas[i], sumWagers[i]);
            }
        }
    }
    #endregion

    #region Public API - Betting Control
    internal void EnableBetting()
    {
        isBettingEnabled = true;
        hasPlacedBetThisRound = false;
        AnimateBetUnlocked(); 

        // Show repeat panel if user bet in previous round
        if (placedBetInPreviousRound)
        {
            ShowRepeatPanelAnimated();
        }

        UpdateTotalBet();
    }

    internal void DisableBetting()
    {
        isBettingEnabled = false;
        CloseChipSelector();
        AnimateBetLocked(); // Slide panels to locked position
        HideBetPanels();

        // Track if user placed bets this round for next round
        placedBetInPreviousRound = hasPlacedBetThisRound;
    }

    internal void ClearAllBets()
    {
        Debug.Log("[BET] Clearing all bets");

        areaBets.Clear();
        currentTotalBet = 0;
        betHistory.Clear();

        // Clear main areas
        ClearArea(SmallArea);
        ClearArea(BigArea);
        ClearArea(OddArea);
        ClearArea(EvenArea);

        // Clear triple dice areas
        foreach (var area in TripleDiceAreas) ClearArea(area);

        // Clear single dice areas
        foreach (var area in SingleDiceAreas) ClearArea(area);

        // Clear sum areas
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

    internal void ShowOtherPlayerBet(BetPlacedData data)
    {
        if (data == null) return;

        string betOption = data.betOption;

        if (betOption == "small" && SmallArea != null)
        {
            SmallArea.AddOpponentBet(data.amount, grayChipSprite);
        }
        else if (betOption == "big" && BigArea != null)
        {
            BigArea.AddOpponentBet(data.amount, grayChipSprite);
        }
        else if (betOption == "odd" && OddArea != null)
        {
            OddArea.AddOpponentBet(data.amount, grayChipSprite);
        }
        else if (betOption == "even" && EvenArea != null)
        {
            EvenArea.AddOpponentBet(data.amount, grayChipSprite);
        }
        else if (betOption.StartsWith("single_"))
        {
            if (int.TryParse(betOption.Replace("single_", ""), out int diceNum))
            {
                int index = diceNum - 1;
                if (index >= 0 && index < SingleDiceAreas.Count && SingleDiceAreas[index] != null)
                {
                    SingleDiceAreas[index].AddOpponentBet(data.amount, grayChipSprite);
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
                    SumAreas[index].AddOpponentBet(data.amount, grayChipSprite);
                }
            }
        }
    }
    #endregion

    #region Private Methods - Bet Placement
    private void OnBetAreaClicked(string betOption)
    {
        if (!isBettingEnabled || currentChipValues.Count == 0) return;

        double betAmount = currentChipValues[selectedChipIndex];
        Sprite chipSprite = GetChipSprite(betAmount);

        if (!CanPlaceBet(betOption, betAmount)) return;

        AddBetToArea(betOption, betAmount, chipSprite);
        gameManager.PlaceBet(betOption, selectedChipIndex);

        CloseChipSelector();
        ShowBetActionsPanelAnimated();
        hasPlacedBetThisRound = true;
    }

    private void OnTripleDiceAreaClicked(int diceNum)
    {
        if (!isBettingEnabled || currentChipValues.Count == 0) return;

        string betOption = $"specific_3";
        double betAmount = currentChipValues[selectedChipIndex];
        Sprite chipSprite = GetChipSprite(betAmount);

        if (!CanPlaceBet(betOption, betAmount)) return;

        int areaIndex = diceNum - 1;
        if (areaIndex >= 0 && areaIndex < TripleDiceAreas.Count && TripleDiceAreas[areaIndex] != null)
        {
            TripleDiceAreas[areaIndex].AddPlayerBet(betAmount, chipSprite, playerChipStackPrefab);
            RecordBet(betOption, betAmount);
        }

        gameManager.PlaceBet(betOption, selectedChipIndex);
        CloseChipSelector();
        ShowBetActionsPanelAnimated();
        hasPlacedBetThisRound = true;
    }

    private void OnSingleDiceAreaClicked(int diceNum)
    {
        if (!isBettingEnabled || currentChipValues.Count == 0) return;

        string betOption = $"single_{diceNum}";
        double betAmount = currentChipValues[selectedChipIndex];
        Sprite chipSprite = GetChipSprite(betAmount);

        if (!CanPlaceBet(betOption, betAmount)) return;

        int areaIndex = diceNum - 1;
        if (areaIndex >= 0 && areaIndex < SingleDiceAreas.Count && SingleDiceAreas[areaIndex] != null)
        {
            SingleDiceAreas[areaIndex].AddPlayerBet(betAmount, chipSprite, playerChipStackPrefab);
            RecordBet(betOption, betAmount);
        }

        gameManager.PlaceBet(betOption, selectedChipIndex);
        CloseChipSelector();
        ShowBetActionsPanelAnimated();
        hasPlacedBetThisRound = true;
    }

    private void AddBetToArea(string betOption, double betAmount, Sprite chipSprite)
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
            targetArea.AddPlayerBet(betAmount, chipSprite, playerChipStackPrefab);
            RecordBet(betOption, betAmount);
        }
        else if (betOption.StartsWith("sum_"))
        {
            if (int.TryParse(betOption.Replace("sum_", ""), out int sum))
            {
                int index = sum - 4;
                if (index >= 0 && index < SumAreas.Count && SumAreas[index] != null)
                {
                    SumAreas[index].AddPlayerBet(betAmount, chipSprite, playerChipStackPrefab);
                    RecordBet(betOption, betAmount);
                }
            }
        }
    }

    private void RecordBet(string betOption, double betAmount)
    {
        if (!areaBets.ContainsKey(betOption))
        {
            areaBets[betOption] = 0;
        }

        areaBets[betOption] += betAmount;
        currentTotalBet += betAmount;

        // Add to bet history for undo
        betHistory.Add(new BetAction { betOption = betOption, amount = betAmount });

        UpdateTotalBet();
    }

    /// <summary>
    /// Validate if bet can be placed based on min/max limits
    /// </summary>
    private bool CanPlaceBet(string betOption, double betAmount)
    {
        // Get current bet in this area
        double currentAreaBet = areaBets.ContainsKey(betOption) ? areaBets[betOption] : 0;

        // Get max bet for this specific area
        double areaMaxBet = GetMaxBetForArea(betOption);

        // Check area limit
        if (currentAreaBet + betAmount > areaMaxBet)
        {
            string message = $"Maximum bet for this area is {FormatChipAmount(areaMaxBet)}";
            if (uiController != null)
            {
                uiController.ShowErrorPopup(message);
            }
            else
            {
                Debug.LogWarning($"[BET] {message}");
            }
            return false;
        }

        // Check total bet limit
        if (currentTotalBet + betAmount > maxBetAmount)
        {
            string message = $"Maximum total bet is {FormatChipAmount(maxBetAmount)}";
            if (uiController != null)
            {
                uiController.ShowErrorPopup(message);
            }
            else
            {
                Debug.LogWarning($"[BET] {message}");
            }
            return false;
        }

        return true;
    }

    /// <summary>
    /// Get maximum bet allowed for specific area from wagerData
    /// </summary>
    private double GetMaxBetForArea(string betOption)
    {
        if (wagerData == null || string.IsNullOrEmpty(currentLevel))
        {
            return maxBetAmount; // Fallback to global max
        }

        BetWager wager = null;

        // Main bets
        if (betOption == "small") wager = wagerData.main_bets?.small;
        else if (betOption == "big") wager = wagerData.main_bets?.big;
        else if (betOption == "odd") wager = wagerData.main_bets?.odd;
        else if (betOption == "even") wager = wagerData.main_bets?.even;

        // Side bets (single dice)
        else if (betOption.StartsWith("single_"))
        {
            wager = wagerData.side_bets?.single_match_1; // All single dice share same limits
        }

        // Side bets (triple dice)
        else if (betOption == "specific_3")
        {
            wager = wagerData.side_bets?.specific_3;
        }

        // Op bets (sum)
        else if (betOption.StartsWith("sum_"))
        {
            if (int.TryParse(betOption.Replace("sum_", ""), out int sum))
            {
                wager = sum switch
                {
                    4 => wagerData.op_bets?.sum_4,
                    5 => wagerData.op_bets?.sum_5,
                    6 => wagerData.op_bets?.sum_6,
                    7 => wagerData.op_bets?.sum_7,
                    8 => wagerData.op_bets?.sum_8,
                    9 => wagerData.op_bets?.sum_9,
                    10 => wagerData.op_bets?.sum_10,
                    11 => wagerData.op_bets?.sum_11,
                    12 => wagerData.op_bets?.sum_12,
                    13 => wagerData.op_bets?.sum_13,
                    14 => wagerData.op_bets?.sum_14,
                    15 => wagerData.op_bets?.sum_15,
                    16 => wagerData.op_bets?.sum_16,
                    17 => wagerData.op_bets?.sum_17,
                    _ => null
                };
            }
        }

        return wager?.GetMaxBet(currentLevel) ?? maxBetAmount;
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

    /// <summary>
    /// Animate chips from center to their original positions in semicircular arc with spinning
    /// </summary>
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

            // Move from center to original position
            Tween moveTween = chipTransform.DOLocalMove(targetPos, CHIP_OPEN_DURATION)
                .SetEase(Ease.OutBack);

            // Rotate 360 degrees during movement
            Tween rotateTween = chipTransform.DOLocalRotate(new Vector3(0, 0, 360), CHIP_OPEN_DURATION, RotateMode.FastBeyond360)
                .SetEase(Ease.OutQuad);

            chipAnimationSequence.Join(moveTween);
            chipAnimationSequence.Join(rotateTween);
        }

        chipAnimationSequence.Play();
    }

    /// <summary>
    /// Animate chips from original positions back to center with inverse spinning
    /// </summary>
    private void AnimateChipsClose(System.Action onComplete = null)
    {
        chipAnimationSequence?.Kill();
        chipAnimationSequence = DOTween.Sequence();

        int activeChips = Mathf.Min(currentChipValues.Count, existingChips.Count);

        for (int i = 0; i < activeChips; i++)
        {
            if (!existingChips[i].gameObject.activeSelf) continue;

            Transform chipTransform = existingChips[i].transform;

            // Move from original position to center
            Tween moveTween = chipTransform.DOLocalMove(centerPosition, CHIP_CLOSE_DURATION)
                .SetEase(Ease.InBack);

            // Rotate -360 degrees during movement (inverse)
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
        return amount.ToString("F0");
    }
    #endregion

    #region Private Methods - Panel Animations
    /// <summary>
    /// Animate panels when bet gets locked (slide chip panel down, stake panel up)
    /// </summary>
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

    /// <summary>
    /// Animate panels when bet gets unlocked (reverse positions)
    /// </summary>
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

    /// <summary>
    /// Show repeat panel with animation if user placed bet in previous round
    /// </summary>
    private void ShowRepeatPanelAnimated()
    {
        if (RepeatPanel == null) return;

        // Stop any existing coroutine
        if (repeatPanelCoroutine != null)
        {
            StopCoroutine(repeatPanelCoroutine);
        }

        repeatPanelCoroutine = StartCoroutine(RepeatPanelSequence());
    }

    private IEnumerator RepeatPanelSequence()
    {
        // Hide bet actions panel
        if (BetActionsPanel != null)
        {
            BetActionsPanelMain.SetActive(false);
            BetActionsPanel.gameObject.SetActive(false);
        }

        // Wait for delay
        yield return new WaitForSeconds(REPEAT_PANEL_DELAY);

        RepeatPanelMain.SetActive(true);
        RepeatPanel.gameObject.SetActive(true);
        RepeatPanel.anchoredPosition = new Vector2(-200f, RepeatPanel.anchoredPosition.y);
        RepeatPanel.DOAnchorPosX(0f, PANEL_SLIDE_DURATION).SetEase(Ease.InOutQuad);

        // Wait for show duration
        yield return new WaitForSeconds(REPEAT_PANEL_SHOW_DURATION);

        // Hide repeat panel
        RepeatPanel.gameObject.SetActive(false);
        RepeatPanelMain.SetActive(false);
        repeatPanelCoroutine = null;
    }

    /// <summary>
    /// Show bet actions panel with animation when user places first bet
    /// </summary>
    private void ShowBetActionsPanelAnimated()
    {
        if (BetActionsPanel == null) return;

        // Stop repeat panel if showing
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

        // Show and animate bet actions panel from X:-300 to X:0
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
        if (betHistory.Count == 0) return;

        // Get last bet
        BetAction lastBet = betHistory[betHistory.Count - 1];
        betHistory.RemoveAt(betHistory.Count - 1);

        // Revert the bet
        if (areaBets.ContainsKey(lastBet.betOption))
        {
            areaBets[lastBet.betOption] -= lastBet.amount;
            if (areaBets[lastBet.betOption] <= 0)
            {
                areaBets.Remove(lastBet.betOption);
            }
        }

        currentTotalBet -= lastBet.amount;
        UpdateTotalBet();

        // Request server to undo
        gameManager.UndoBet();

        Debug.Log($"[BET] Undid bet: {lastBet.betOption} - {lastBet.amount}");
    }

    private void OnCancelClicked()
    {
        ClearAllBets();
        gameManager.CancelAllBets();
    }

    private void OnDoubleClicked()
    {
        // Check if doubling would exceed limits
        if (currentTotalBet * 2 > maxBetAmount)
        {
            if (uiController != null)
            {
                uiController.ShowErrorPopup($"Cannot double - would exceed max bet of {FormatChipAmount(maxBetAmount)}");
            }
            return;
        }

        // Check each area limit
        foreach (var kvp in areaBets)
        {
            double areaMaxBet = GetMaxBetForArea(kvp.Key);
            if (kvp.Value * 2 > areaMaxBet)
            {
                if (uiController != null)
                {
                    uiController.ShowErrorPopup($"Cannot double - would exceed area limit");
                }
                return;
            }
        }

        gameManager.DoubleBet();
    }

    private void OnRepeatClicked()
    {
        gameManager.RepeatBet();

        // Hide repeat panel after use
        if (RepeatPanel != null)
        {
            RepeatPanelMain.SetActive(false);
            RepeatPanel.gameObject.SetActive(false);
        }
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
}

#region Helper Classes
[System.Serializable]
public class BetAction
{
    public string betOption;
    public double amount;
}
#endregion

#region Bet Area Classes
[System.Serializable]
public class SimpleBetArea
{
    public Button Button;
    public GameObject WinImage;
    public TMP_Text WinRatio_Text;
    public Transform PlayerBetContainer;

    private double playerBetAmount = 0;
    private double opponentBetAmount = 0;
    private List<GameObject> playerChips = new List<GameObject>();
    private GameObject opponentChip;

    public void SetWinRatio(string ratio)
    {
        if (WinRatio_Text) WinRatio_Text.text = ratio;
    }

    public void AddPlayerBet(double amount, Sprite chipSprite, GameObject prefab)
    {
        if (PlayerBetContainer == null || prefab == null) return;

        GameObject chipObj = Object.Instantiate(prefab, PlayerBetContainer);
        Image chipImage = chipObj.GetComponent<Image>();
        if (chipImage) chipImage.sprite = chipSprite;

        playerChips.Add(chipObj);
        playerBetAmount += amount;
    }

    public void AddOpponentBet(double amount, Sprite chipSprite)
    {
        if (PlayerBetContainer == null) return;

        if (opponentChip == null)
        {
            GameObject chipObj = new GameObject("OpponentChip");
            chipObj.transform.SetParent(PlayerBetContainer);
            Image image = chipObj.AddComponent<Image>();
            image.sprite = chipSprite;
            opponentChip = chipObj;
        }

        opponentBetAmount += amount;
    }

    public void ClearBets()
    {
        foreach (var chip in playerChips)
        {
            if (chip != null) Object.Destroy(chip);
        }
        playerChips.Clear();

        if (opponentChip != null)
        {
            Object.Destroy(opponentChip);
            opponentChip = null;
        }

        playerBetAmount = 0;
        opponentBetAmount = 0;
        SetHighlight(false);
    }

    public void SetHighlight(bool highlight)
    {
        if (WinImage) WinImage.SetActive(highlight);
    }
}

[System.Serializable]
public class TripleSameDiceArea
{
    public Button Button;
    public GameObject WinImage;
    public Transform PlayerBetContainer;

    private List<GameObject> playerChips = new List<GameObject>();

    public void AddPlayerBet(double amount, Sprite chipSprite, GameObject prefab)
    {
        if (PlayerBetContainer == null || prefab == null) return;

        GameObject chipObj = Object.Instantiate(prefab, PlayerBetContainer);
        Image chipImage = chipObj.GetComponent<Image>();
        if (chipImage) chipImage.sprite = chipSprite;

        playerChips.Add(chipObj);
    }

    public void ClearBets()
    {
        foreach (var chip in playerChips)
        {
            if (chip != null) Object.Destroy(chip);
        }
        playerChips.Clear();
        SetHighlight(false);
    }

    public void SetHighlight(bool highlight)
    {
        if (WinImage) WinImage.SetActive(highlight);
    }
}

[System.Serializable]
public class SingleDiceArea
{
    public Button Button;
    public GameObject WinImage;
    public Transform PlayerBetContainer;

    private List<GameObject> playerChips = new List<GameObject>();
    private GameObject opponentChip;

    public void AddPlayerBet(double amount, Sprite chipSprite, GameObject prefab)
    {
        if (PlayerBetContainer == null || prefab == null) return;

        GameObject chipObj = Object.Instantiate(prefab, PlayerBetContainer);
        Image chipImage = chipObj.GetComponent<Image>();
        if (chipImage) chipImage.sprite = chipSprite;

        playerChips.Add(chipObj);
    }

    public void AddOpponentBet(double amount, Sprite chipSprite)
    {
        if (PlayerBetContainer == null) return;

        if (opponentChip == null)
        {
            GameObject chipObj = new GameObject("OpponentChip");
            chipObj.transform.SetParent(PlayerBetContainer);
            Image image = chipObj.AddComponent<Image>();
            image.sprite = chipSprite;
            opponentChip = chipObj;
        }
    }

    public void ClearBets()
    {
        foreach (var chip in playerChips)
        {
            if (chip != null) Object.Destroy(chip);
        }
        playerChips.Clear();

        if (opponentChip != null)
        {
            Object.Destroy(opponentChip);
            opponentChip = null;
        }

        SetHighlight(false);
    }

    public void SetHighlight(bool highlight)
    {
        if (WinImage) WinImage.SetActive(highlight);
    }
}

[System.Serializable]
public class SumArea
{
    public Button Button;
    public GameObject WinImage;
    public TMP_Text WinRatio_Text;
    public Transform PlayerBetContainer;

    private List<GameObject> playerChips = new List<GameObject>();
    private GameObject opponentChip;

    public void SetWinRatio(string ratio)
    {
        if (WinRatio_Text) WinRatio_Text.text = ratio;
    }

    public void AddPlayerBet(double amount, Sprite chipSprite, GameObject prefab)
    {
        if (PlayerBetContainer == null || prefab == null) return;

        GameObject chipObj = Object.Instantiate(prefab, PlayerBetContainer);
        Image chipImage = chipObj.GetComponent<Image>();
        if (chipImage) chipImage.sprite = chipSprite;

        playerChips.Add(chipObj);
    }

    public void AddOpponentBet(double amount, Sprite chipSprite)
    {
        if (PlayerBetContainer == null) return;

        if (opponentChip == null)
        {
            GameObject chipObj = new GameObject("OpponentChip");
            chipObj.transform.SetParent(PlayerBetContainer);
            Image image = chipObj.AddComponent<Image>();
            image.sprite = chipSprite;
            opponentChip = chipObj;
        }
    }

    public void ClearBets()
    {
        foreach (var chip in playerChips)
        {
            if (chip != null) Object.Destroy(chip);
        }
        playerChips.Clear();

        if (opponentChip != null)
        {
            Object.Destroy(opponentChip);
            opponentChip = null;
        }

        SetHighlight(false);
    }

    public void SetHighlight(bool highlight)
    {
        if (WinImage) WinImage.SetActive(highlight);
    }
}
#endregion