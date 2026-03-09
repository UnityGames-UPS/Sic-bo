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
    [SerializeField] private RectTransform MainChip_RectTransform;
    [SerializeField] private GameObject ChipSelector_Panel;
    [SerializeField] private GameObject ChipSelector_BlackBG;
    [SerializeField] private Transform ChipOptions_Container;
    [SerializeField] private RectTransform ChipAreaPanel;
    [SerializeField] private RectTransform TotalStakePanel;

    [Header("Chip Prefabs & Sprites")]
    [SerializeField] private GameObject chipSelectorPrefab;
    [Header("Level-Based Chip Sprites (6 chips each)")]
    [SerializeField] private LevelChipSprites levelChipSprites = new LevelChipSprites();

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
    [SerializeField] private TMP_Text TopMinBet_Text;
    [SerializeField] private TMP_Text MaxBet_Text;

    [Header("Shared Win Ratio - Triple Dice")]
    [SerializeField] private TMP_Text SharedTripleWinRatio_Text;

    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private UIController uiController;
    [SerializeField] private BonusIndicatorController bonusIndicatorController;
    [SerializeField] private OpponentChipManager opponentChipManager;
    #endregion

    #region Private Fields - Pool
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
    private Dictionary<string, double> areaBets = new Dictionary<string, double>();
    private List<BetAction> betHistory = new List<BetAction>();
    private List<BetAction> previousRoundBets = new List<BetAction>();
    private Wagers wagerData = null;
    private string currentLevel = "";
    private double minBetAmount = 0;
    private double maxBetAmount = 0;
    private bool placedBetInPreviousRound = false;
    private bool hasPlacedBetThisRound = false;
    private bool isProcessingBetAction = false;
    private string pendingBetOption = "";
    private string currentBetAction = "";
    private int receivedBroadcastCount = 0;
    private Sequence chipAnimationSequence;
    private Coroutine repeatPanelCoroutine;
    private Vector2 mainChipOriginalPosition;
    private Tween mainChipTween;
    private Sprite[] currentLevelChipSprites; // Cache for current level's chip sprites

    // GC optimisation: reuse collections instead of allocating every call/round
    private readonly HashSet<int> _uniqueDiceCache = new HashSet<int>();
    private readonly List<WinAreaData> _winAreasCache = new List<WinAreaData>();
    private readonly List<string> _winOptionsCache = new List<string>();
    private readonly List<string> _betOptionsCache = new List<string>();
    #endregion

    #region Private Fields - Opponent System
    private Dictionary<string, Dictionary<string, double>> opponentBets = new Dictionary<string, Dictionary<string, double>>();
    private string currentPlayerUsername = "";
    private Leaderboards currentLeaderboards = null;
    private bool isPlayerRichest = false;
    private bool isPlayerWinner = false;
    #endregion

    #region Animation Constants
    private const float CHIP_OPEN_DURATION = 0.5f;
    private const float CHIP_CLOSE_DURATION = 0.4f;
    private const float PANEL_SLIDE_DURATION = 0.3f;
    private const float REPEAT_PANEL_SHOW_DURATION = 5f;
    private const float REPEAT_PANEL_DELAY = 0.5f;
    private const int MAX_CHIP_COMBINATION_COUNT = 20;
    private const float MAIN_CHIP_Y_OFFSET = 30f;
    private const float MAIN_CHIP_ANIMATION_DURATION = 0.3f;
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

    #region Pool System
    private void InitializePool()
    {
        if (isPoolInitialized) return;
        if (playerBetComponentPrefab == null) return;

        SpawnComponentInArea(SmallArea, "small");
        SpawnComponentInArea(BigArea, "big");
        SpawnComponentInArea(OddArea, "odd");
        SpawnComponentInArea(EvenArea, "even");

        for (int i = 0; i < TripleDiceAreas.Count; i++)
            SpawnComponentInArea(TripleDiceAreas[i], $"specific_3_{i + 1}");

        for (int i = 0; i < SingleDiceAreas.Count; i++)
            SpawnComponentInArea(SingleDiceAreas[i], $"single_{i + 1}");

        for (int i = 0; i < SumAreas.Count; i++)
            SpawnComponentInArea(SumAreas[i], $"sum_{i + 4}");

        isPoolInitialized = true;
        bonusIndicatorController?.InitializePool(GetBetAreaContainerMap());

        if (opponentChipManager != null)
            opponentChipManager.InitializeContainers(GetOpponentBetAreaContainerMap());
    }

    private PlayerBetComponent CreateComponent(Transform parent, string areaId)
    {
        if (parent == null) return null;
        PlayerBetComponent component = Instantiate(playerBetComponentPrefab, parent);
        component.name = $"PlayerBetComponent_{areaId}";
        component.transform.localPosition = Vector3.zero;
        component.transform.localScale = Vector3.one;
        component.gameObject.SetActive(false);
        component.Initialize(GetCurrentLevelSprites(), currentChipValues);
        componentPool.Add(component);
        activeComponents[areaId] = component;
        return component;
    }

    private void SpawnComponentInArea(SimpleBetArea area, string areaId)
    {
        if (area == null || area.PlayerBetContainer == null) return;
        var c = CreateComponent(area.PlayerBetContainer, areaId);
        if (c != null) area.playerBetComponent = c;
    }

    private void SpawnComponentInArea(TripleSameDiceArea area, string areaId)
    {
        if (area == null || area.PlayerBetContainer == null) return;
        var c = CreateComponent(area.PlayerBetContainer, areaId);
        if (c != null) area.playerBetComponent = c;
    }

    private void SpawnComponentInArea(SingleDiceArea area, string areaId)
    {
        if (area == null || area.PlayerBetContainer == null) return;
        var c = CreateComponent(area.PlayerBetContainer, areaId);
        if (c != null) area.playerBetComponent = c;
    }

    private void SpawnComponentInArea(SumArea area, string areaId)
    {
        if (area == null || area.PlayerBetContainer == null) return;
        var c = CreateComponent(area.PlayerBetContainer, areaId);
        if (c != null) area.playerBetComponent = c;
    }

    private void CleanupPool()
    {
        activeComponents.Clear();
        foreach (var component in componentPool)
            if (component != null) Destroy(component.gameObject);
        componentPool.Clear();
        isPoolInitialized = false;
    }
    #endregion

    #region Internal API - Round Management
    internal void OnRoundStart() => ClearAllBets(true);

    internal void OnRoundEnd()
    {
        TotalStakePanel?.DOKill();
        TotalStakePanel?.DOAnchorPosY(-200f, PANEL_SLIDE_DURATION).SetEase(Ease.InOutQuad);
        ChipAreaPanel?.DOKill();
        ChipAreaPanel?.DOAnchorPosY(-200f, PANEL_SLIDE_DURATION).SetEase(Ease.InOutQuad);
    }
    #endregion

    #region Setup
    private void SetupButtonListeners()
    {
        if (MainChip_Button) MainChip_Button.onClick.AddListener(() => { AudioManager.Instance?.PlayChipSelectionOpen(); ToggleChipSelector(); });

        if (ChipSelector_BlackBG)
        {
            Button bgButton = ChipSelector_BlackBG.GetComponent<Button>() ?? ChipSelector_BlackBG.AddComponent<Button>();
            bgButton.onClick.AddListener(CloseChipSelector);
        }

        if (Undo_Button) Undo_Button.onClick.AddListener(() => { AudioManager.Instance?.PlayButtonClick(); OnUndoClicked(); });
        if (Cancel_Button) Cancel_Button.onClick.AddListener(() => { AudioManager.Instance?.PlayButtonClick(); OnCancelClicked(); });
        if (Double_Button) Double_Button.onClick.AddListener(() => { AudioManager.Instance?.PlayButtonClick(); OnDoubleClicked(); });
        if (Repeat_Button) Repeat_Button.onClick.AddListener(() => { AudioManager.Instance?.PlayButtonClick(); OnRepeatClicked(); });
    }

    private void SetupBetAreaListeners()
    {
        if (SmallArea?.Button) SmallArea.Button.onClick.AddListener(() => OnBetAreaClicked("small"));
        if (BigArea?.Button) BigArea.Button.onClick.AddListener(() => OnBetAreaClicked("big"));
        if (OddArea?.Button) OddArea.Button.onClick.AddListener(() => OnBetAreaClicked("odd"));
        if (EvenArea?.Button) EvenArea.Button.onClick.AddListener(() => OnBetAreaClicked("even"));

        for (int i = 0; i < TripleDiceAreas.Count; i++)
        {
            int diceNum = i + 1;
            if (TripleDiceAreas[i]?.Button)
                TripleDiceAreas[i].Button.onClick.AddListener(() => OnTripleDiceAreaClicked(diceNum));
        }

        for (int i = 0; i < SingleDiceAreas.Count; i++)
        {
            int diceNum = i + 1;
            if (SingleDiceAreas[i]?.Button)
                SingleDiceAreas[i].Button.onClick.AddListener(() => OnSingleDiceAreaClicked(diceNum));
        }

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
        mainChipOriginalPosition = MainChip_RectTransform != null ? MainChip_RectTransform.anchoredPosition : Vector2.zero;
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

    #region Internal API - Setup & Display
    internal void SetupChips(List<double> chipValues, Wagers wagers, string level)
    {
        currentChipValues = chipValues;
        currentLevel = level;
        wagerData = wagers;
        chipValueToSprite.Clear();

        // Get sprites for the current level
        currentLevelChipSprites = GetSpritesForLevel(level);

        int chipCount = Mathf.Min(chipValues.Count, currentLevelChipSprites.Length);
        for (int i = 0; i < chipCount; i++)
        {
            chipValueToSprite[chipValues[i]] = currentLevelChipSprites[i];
            if (i < existingChips.Count)
            {
                existingChips[i].SetData(currentLevelChipSprites[i], FormatChipAmount(chipValues[i]), i);
                existingChips[i].SetActive(true);
            }
        }

        for (int i = chipCount; i < existingChips.Count; i++)
            existingChips[i].SetActive(false);

        if (chipCount > 0) SelectChipAt(0);

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
        if (wagerData == null || string.IsNullOrEmpty(currentLevel)) return 0;

        double highestMax = 0;

        if (wagerData.main_bets != null)
        {
            highestMax = System.Math.Max(highestMax, GetMaxFromWager(wagerData.main_bets.small));
            highestMax = System.Math.Max(highestMax, GetMaxFromWager(wagerData.main_bets.big));
            highestMax = System.Math.Max(highestMax, GetMaxFromWager(wagerData.main_bets.odd));
            highestMax = System.Math.Max(highestMax, GetMaxFromWager(wagerData.main_bets.even));
        }

        if (wagerData.side_bets != null)
        {
            highestMax = System.Math.Max(highestMax, GetMaxFromWager(wagerData.side_bets.single_match_1));
            highestMax = System.Math.Max(highestMax, GetMaxFromWager(wagerData.side_bets.single_match_2));
            highestMax = System.Math.Max(highestMax, GetMaxFromWager(wagerData.side_bets.single_match_3));
            highestMax = System.Math.Max(highestMax, GetMaxFromWager(wagerData.side_bets.specific_2));
            highestMax = System.Math.Max(highestMax, GetMaxFromWager(wagerData.side_bets.specific_3));
        }

        if (wagerData.op_bets != null)
        {
            for (int sum = 4; sum <= 17; sum++)
                highestMax = System.Math.Max(highestMax, GetMaxFromWager(GetSumWager(sum)));
        }

        return highestMax;
    }

    private double GetMaxFromWager(BetWager wager) => wager?.GetMaxBet(currentLevel) ?? 0;

    private void UpdateAllComponentChipValues()
    {
        foreach (var kvp in activeComponents)
            kvp.Value?.UpdateChipValues(currentChipValues);
    }

    private void SetupWinRatios()
    {
        if (wagerData == null) return;

        SetWinRatio(SmallArea, wagerData.main_bets?.small);
        SetWinRatio(BigArea, wagerData.main_bets?.big);
        SetWinRatio(OddArea, wagerData.main_bets?.odd);
        SetWinRatio(EvenArea, wagerData.main_bets?.even);

        for (int i = 0; i < SumAreas.Count; i++)
            SetWinRatio(SumAreas[i], GetSumWager(i + 4));

        if (SharedTripleWinRatio_Text != null && wagerData.side_bets != null)
        {
            SharedTripleWinRatio_Text.text = BetWager.GetCombinedSpecificPayoutString(
                wagerData.side_bets.specific_2, wagerData.side_bets.specific_3);
        }
    }

    private void UpdateMinMaxDisplay()
    {
        if (MinBet_Text) MinBet_Text.text = FormatChipAmount(minBetAmount);
        if (TopMinBet_Text) TopMinBet_Text.text = FormatChipAmount(minBetAmount);
        if (MaxBet_Text) MaxBet_Text.text = FormatChipAmount(maxBetAmount);
    }
    #endregion

    #region Internal API - Betting State
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
            previousRoundBets.Clear();
            previousRoundBets.AddRange(betHistory);
            placedBetInPreviousRound = true;
        }
        else
        {
            previousRoundBets.Clear();
            placedBetInPreviousRound = false;
        }
    }

    internal void ClearAllBets(bool opponentBetClear)
    {
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

        if (opponentBetClear) ClearAllOpponentBets();

        UpdateTotalBet();
        HideBetActionsPanel();
    }
    #endregion

    #region Internal API - Highlights
    internal void HighlightWinningAreas(string matchSide, int sum)
    {
        SetAreaHighlight(SmallArea, matchSide == "small");
        SetAreaHighlight(BigArea, matchSide == "big");
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

        _uniqueDiceCache.Clear();
        _uniqueDiceCache.Add(dice1);
        _uniqueDiceCache.Add(dice2);
        _uniqueDiceCache.Add(dice3);
        foreach (int num in _uniqueDiceCache)
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
        foreach (var area in TripleDiceAreas) area?.SetHighlight(false);
        foreach (var area in SingleDiceAreas) area?.SetHighlight(false);
        foreach (var area in SumAreas) area?.SetHighlight(false);
    }
    #endregion

    #region Bet Broadcast Handling
    internal void OnBetPlacedBroadcast(BetPlacedData data)
    {
        if (data == null) return;

        if (!string.IsNullOrEmpty(data.username) && data.username != currentPlayerUsername)
        {
            HandleOpponentBet(data);
            return;
        }

        if (isProcessingBetAction && !string.IsNullOrEmpty(currentBetAction))
        {
            receivedBroadcastCount++;
            switch (currentBetAction)
            {
                case "REPEAT": HandleRepeatBroadcast(data); break;
                case "DOUBLE": HandleDoubleBroadcast(data); break;
                case "UNDO": HandleUndoBroadcast(data); break;
                case "CANCEL": HandleCancelBroadcast(data); break;
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

        AddBetToAreaFromServer(data.betOption, data.amount);
        ApplyBadgesToContainer(GetBetAreaByOption(data.betOption)?.PlayerBetContainer);

        if (!areaBets.ContainsKey(data.betOption)) areaBets[data.betOption] = 0;
        areaBets[data.betOption] += data.amount;
        currentTotalBet += data.amount;


        betHistory.Add(new BetAction
        {
            betOption = data.betOption,
            amount = data.amount,
            chipIndex = GetChipIndexForAmount(data.amount)
        });

        hasPlacedBetThisRound = true;
        pendingBetOption = "";

        UpdateTotalBet();
        ShowBetActionsPanelAnimated();
    }

    private void HandleRepeatBroadcast(BetPlacedData data)
    {
        if (data.amount <= 0) return;
        AddBetToAreaFromServer(data.betOption, data.amount);
        ApplyBadgesToContainer(GetBetAreaByOption(data.betOption)?.PlayerBetContainer);
        if (!areaBets.ContainsKey(data.betOption)) areaBets[data.betOption] = 0;
        areaBets[data.betOption] += data.amount;
        currentTotalBet += data.amount;
        betHistory.Add(new BetAction { betOption = data.betOption, amount = data.amount, chipIndex = GetChipIndexForAmount(data.amount) });
        UpdateTotalBet();
    }

    private void HandleDoubleBroadcast(BetPlacedData data)
    {
        if (data.amount <= 0) return;
        AddBetToAreaFromServer(data.betOption, data.amount);
        ApplyBadgesToContainer(GetBetAreaByOption(data.betOption)?.PlayerBetContainer);
        if (!areaBets.ContainsKey(data.betOption)) areaBets[data.betOption] = 0;
        areaBets[data.betOption] += data.amount;
        currentTotalBet += data.amount;
        betHistory.Add(new BetAction { betOption = data.betOption, amount = data.amount, chipIndex = GetChipIndexForAmount(data.amount) });
        UpdateTotalBet();
    }

    private void HandleUndoBroadcast(BetPlacedData data)
    {
        if (data.amount >= 0) return;
        double removeAmount = System.Math.Abs(data.amount);

        for (int i = betHistory.Count - 1; i >= 0; i--)
        {
            if (betHistory[i].betOption == data.betOption) { betHistory.RemoveAt(i); break; }
        }

        if (areaBets.ContainsKey(data.betOption))
        {
            areaBets[data.betOption] -= removeAmount;
            if (areaBets[data.betOption] <= 0.01)
            {
                areaBets.Remove(data.betOption);
            }
        }

        currentTotalBet -= removeAmount;
        RemoveLastChipFromArea(data.betOption);
        UpdateTotalBet();
    }

    private void HandleCancelBroadcast(BetPlacedData data)
    {
        if (data.amount >= 0) return;
        double removeAmount = System.Math.Abs(data.amount);
        double totalRemoved = 0;

        for (int i = betHistory.Count - 1; i >= 0; i--)
        {
            if (betHistory[i].betOption == data.betOption)
            {
                totalRemoved += betHistory[i].amount;
                betHistory.RemoveAt(i);
                if (System.Math.Abs(totalRemoved - removeAmount) < 0.01) break;
            }
        }

        if (areaBets.ContainsKey(data.betOption))
        {
            areaBets[data.betOption] -= removeAmount;
            if (areaBets[data.betOption] <= 0.01)
            {
                areaBets.Remove(data.betOption);
            }
        }

        currentTotalBet -= removeAmount;
        ClearBetsFromArea(data.betOption);
        UpdateTotalBet();
    }

    private void HandleOpponentBet(BetPlacedData data)
    {
        if (data == null || data.amount == 0 || opponentChipManager == null) return;

        if (!opponentBets.ContainsKey(data.username))
            opponentBets[data.username] = new Dictionary<string, double>();

        if (data.amount > 0)
        {
            AudioManager.Instance?.PlayChipAdd();
            opponentChipManager.AddOpponentBet(data.betOption, data.amount, data.username);
            if (!opponentBets[data.username].ContainsKey(data.betOption))
                opponentBets[data.username][data.betOption] = 0;
            opponentBets[data.username][data.betOption] += data.amount;
        }
        else
        {
            double removeAmount = System.Math.Abs(data.amount);
            if (opponentBets[data.username].ContainsKey(data.betOption))
            {
                opponentBets[data.username][data.betOption] -= removeAmount;
                if (opponentBets[data.username][data.betOption] <= 0.01)
                    opponentBets[data.username].Remove(data.betOption);
            }
        }
    }
    #endregion

    #region Internal API - Bet Response
    internal void OnBetLimitReached()
    {
        if (string.IsNullOrEmpty(pendingBetOption))
        {
            uiController?.ShowInGamePopup("Max bet limit reached");
            return;
        }

        double maxBet = gameManager.GetMaxBetForBetOption(pendingBetOption);
        uiController?.ShowInGamePopup($"Max bet for {FormatBetOptionName(pendingBetOption)} is {GameUtilities.FormatCurrency(maxBet)}");
    }

    internal void OnBetActionResponse(BetAckResponse response)
    {
        if (response == null || response.payload == null)
        {
            pendingBetOption = "";
            ResetBetActionState();
            return;
        }

        if (!response.success)
        {
            string msg = !string.IsNullOrEmpty(response.payload.message) ? response.payload.message : "Action failed";
            uiController?.ShowInGamePopup(msg);
            pendingBetOption = "";
            ResetBetActionState();
            return;
        }

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

    private void ReconcileStateFromAck(BetAckResponse response)
    {
        if (response?.payload == null) return;

        switch (currentBetAction)
        {
            case "DOUBLE":
                if (response.payload.totalBet > 0)
                {
                    currentTotalBet = response.payload.totalBet;
                    if (response.payload.bets != null)
                    {
                        foreach (var bet in response.payload.bets)
                        {
                            if (bet == null || string.IsNullOrEmpty(bet.betOption)) continue;
                            areaBets[bet.betOption] = bet.amount > 0 ? bet.amount : (areaBets.ContainsKey(bet.betOption) ? areaBets[bet.betOption] : 0);
                        }
                    }
                }
                UpdateTotalBet();
                break;

            case "CANCEL":
                areaBets.Clear();
                betHistory.Clear();
                currentTotalBet = 0;
                UpdateTotalBet();
                break;

            case "UNDO":
                if (response.payload.totalBet >= 0)
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

    #region Bet Area Operations
    private void AddBetToAreaFromServer(string betOption, double amount)
    {
        BaseBetArea area = GetBetAreaByOption(betOption);
        area?.playerBetComponent?.AddBetFromServer(amount);
    }

    private void RemoveLastChipFromArea(string betOption)
    {
        BaseBetArea area = GetBetAreaByOption(betOption);
        area?.RemoveLastBet();
    }

    private void ClearBetsFromArea(string betOption)
    {
        BaseBetArea area = GetBetAreaByOption(betOption);
        area?.ClearBets();
    }

    private void ClearAllOpponentBets()
    {
        opponentBets.Clear();
        opponentChipManager?.ClearAllOpponentBets();
    }
    #endregion

    #region Bet Area Click Handlers
    private void OnBetAreaClicked(string betOption)
    {
        if (!isBettingEnabled) { uiController?.ShowInGamePopup("Betting is locked. Wait for next round."); return; }
        if (currentChipValues.Count == 0) return;
        pendingBetOption = betOption;
        gameManager.PlaceBet(betOption, selectedChipIndex);
        CloseChipSelector();
    }

    private void OnTripleDiceAreaClicked(int diceNum)
    {
        if (!isBettingEnabled) { uiController?.ShowInGamePopup("Betting is locked. Wait for next round."); return; }
        if (currentChipValues.Count == 0) return;
        string betOption = $"specific_3_{diceNum}";
        pendingBetOption = betOption;
        gameManager.PlaceBet(betOption, selectedChipIndex);
        CloseChipSelector();
    }

    private void OnSingleDiceAreaClicked(int diceNum)
    {
        if (!isBettingEnabled) { uiController?.ShowInGamePopup("Betting is locked. Wait for next round."); return; }
        if (currentChipValues.Count == 0) return;
        string betOption = $"single_{diceNum}";
        pendingBetOption = betOption;
        gameManager.PlaceBet(betOption, selectedChipIndex);
        CloseChipSelector();
    }
    #endregion

    #region Chip Selector & Animation
    private void ToggleChipSelector()
    {
        if (!isBettingEnabled) return;
        if (isChipSelectorOpen) CloseChipSelector(); else OpenChipSelector();

        AudioManager.Instance?.PlayChipSelectionOpen();
    }

    private void OpenChipSelector()
    {
        if (ChipSelector_Panel) ChipSelector_Panel.SetActive(true);
        if (ChipSelector_BlackBG) ChipSelector_BlackBG.SetActive(true);
        isChipSelectorOpen = true;
        if (MainChip_Button) MainChip_Button.interactable = false;
        AnimateChipsOpen(() => { if (MainChip_Button) MainChip_Button.interactable = true; });
        AnimateMainChipUp();
    }
    private void AnimateMainChipUp()
    {
        if (MainChip_RectTransform == null) return;
        mainChipTween?.Kill();
        Vector2 targetPosition = new Vector2(mainChipOriginalPosition.x, mainChipOriginalPosition.y + MAIN_CHIP_Y_OFFSET);
        mainChipTween = MainChip_RectTransform.DOAnchorPos(targetPosition, MAIN_CHIP_ANIMATION_DURATION).SetEase(Ease.OutQuad);
    }


    private void CloseChipSelector()
    {
        if (MainChip_Button) MainChip_Button.interactable = false;
        AnimateChipsClose(() =>
        {
            if (ChipSelector_Panel) ChipSelector_Panel.SetActive(false);
            if (ChipSelector_BlackBG) ChipSelector_BlackBG.SetActive(false);
            isChipSelectorOpen = false;
            if (MainChip_Button) MainChip_Button.interactable = true;
        });
        AnimateMainChipDown();
    }
    private void AnimateMainChipDown()
    {
        if (MainChip_RectTransform == null) return;
        mainChipTween?.Kill();
        mainChipTween = MainChip_RectTransform.DOAnchorPos(mainChipOriginalPosition, MAIN_CHIP_ANIMATION_DURATION).SetEase(Ease.InQuad);
    }
    private void AnimateChipsOpen(System.Action onComplete = null)
    {
        chipAnimationSequence?.Kill();
        chipAnimationSequence = DOTween.Sequence();
        int activeChips = Mathf.Min(currentChipValues.Count, existingChips.Count);
        for (int i = 0; i < activeChips; i++)
        {
            if (!existingChips[i].gameObject.activeSelf) continue;
            Transform t = existingChips[i].transform;
            Vector3 targetPos = originalChipPositions[i];

            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.Euler(0, 0, 180);


            chipAnimationSequence.Join(t.DOLocalMove(targetPos, CHIP_OPEN_DURATION).SetEase(Ease.OutBack));
            chipAnimationSequence.Join(t.DOLocalRotate(new Vector3(0, 0, 0), CHIP_OPEN_DURATION, RotateMode.FastBeyond360).SetEase(Ease.OutQuad));
        }
        if (onComplete != null) chipAnimationSequence.OnComplete(() => onComplete());
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
            Transform t = existingChips[i].transform;
            chipAnimationSequence.Join(t.DOLocalMove(Vector3.zero, CHIP_CLOSE_DURATION).SetEase(Ease.InBack));
            chipAnimationSequence.Join(t.DOLocalRotate(new Vector3(0, 0, 180), CHIP_CLOSE_DURATION, RotateMode.FastBeyond360).SetEase(Ease.InQuad));
        }
        if (onComplete != null) chipAnimationSequence.OnComplete(() => onComplete());
        chipAnimationSequence.Play();
    }

    private void OnChipSelected(int index) { SelectChipAt(index); CloseChipSelector(); }

    private void SelectChipAt(int index)
    {
        if (index < 0 || index >= currentChipValues.Count) return;
        selectedChipIndex = index;
        double chipValue = currentChipValues[index];
        if (MainChip_Image) MainChip_Image.sprite = GetChipSprite(chipValue);
        if (MainChip_Text) MainChip_Text.text = FormatChipAmount(chipValue);
    }

    private Sprite GetChipSprite(double value)
    {
        if (chipValueToSprite.TryGetValue(value, out var s))
            return s;

        Sprite[] sprites = GetCurrentLevelSprites();
        return sprites.Length > 0 ? sprites[0] : null;
    }
    #endregion

    #region Panel Animations
    private void AnimateBetLocked()
    {
        ChipAreaPanel?.DOAnchorPosY(-200f, PANEL_SLIDE_DURATION).SetEase(Ease.InOutQuad);
        TotalStakePanel?.DOAnchorPosY(-12f, PANEL_SLIDE_DURATION).SetEase(Ease.InOutQuad);
    }

    private void AnimateBetUnlocked()
    {
        ChipAreaPanel?.DOAnchorPosY(0f, PANEL_SLIDE_DURATION).SetEase(Ease.InOutQuad);
        TotalStakePanel?.DOAnchorPosY(-200f, PANEL_SLIDE_DURATION).SetEase(Ease.InOutQuad).OnComplete(() =>
        {
            currentTotalBet = 0;
            UpdateTotalBet();
        });
    }

    private void ShowRepeatPanelAnimated()
    {
        if (RepeatPanel == null) return;
        if (repeatPanelCoroutine != null) StopCoroutine(repeatPanelCoroutine);
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
            .OnComplete(() => { RepeatPanel.gameObject.SetActive(false); RepeatPanelMain.SetActive(false); });

        repeatPanelCoroutine = null;
    }

    private void ShowBetActionsPanelAnimated()
    {
        if (BetActionsPanel == null) return;
        if (repeatPanelCoroutine != null) { StopCoroutine(repeatPanelCoroutine); repeatPanelCoroutine = null; }
        if (RepeatPanel != null) { RepeatPanelMain.SetActive(false); RepeatPanel.gameObject.SetActive(false); }

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

    #region UI Updates
    private void UpdateTotalBet()
    {
        if (TotalBet_Text) TotalBet_Text.text = currentTotalBet % 1 == 0 ? $"{currentTotalBet:F0}" : $"{currentTotalBet:F2}";
    }

    private void HideBetPanels()
    {
        HideBetActionsPanel();
        if (RepeatPanel != null) { RepeatPanelMain.SetActive(false); RepeatPanel.gameObject.SetActive(false); }
        if (repeatPanelCoroutine != null) { StopCoroutine(repeatPanelCoroutine); repeatPanelCoroutine = null; }
    }
    #endregion

    #region Button Handlers
    private void OnUndoClicked()
    {
        if (!isBettingEnabled || isProcessingBetAction) { uiController?.ShowInGamePopup("Please wait..."); return; }
        AudioManager.Instance?.PlayButtonClick();
        isProcessingBetAction = true;
        currentBetAction = "UNDO";
        receivedBroadcastCount = 0;
        gameManager.UndoBet();
    }

    private void OnCancelClicked()
    {
        if (!isBettingEnabled || isProcessingBetAction) { uiController?.ShowInGamePopup("Please wait..."); return; }
        AudioManager.Instance?.PlayButtonClick();
        isProcessingBetAction = true;
        currentBetAction = "CANCEL";
        receivedBroadcastCount = 0;
        gameManager.CancelAllBets();
    }

    private void OnDoubleClicked()
    {
        if (!isBettingEnabled || isProcessingBetAction) { uiController?.ShowInGamePopup("Please wait..."); return; }
        AudioManager.Instance?.PlayButtonClick();
        isProcessingBetAction = true;
        currentBetAction = "DOUBLE";
        receivedBroadcastCount = 0;
        gameManager.DoubleBet();
    }

    private void OnRepeatClicked()
    {
        if (!isBettingEnabled || isProcessingBetAction) { uiController?.ShowInGamePopup("No previous bets to repeat"); return; }
        if (previousRoundBets.Count == 0) { uiController?.ShowInGamePopup("No previous bets to repeat"); return; }

        AudioManager.Instance?.PlayButtonClick();
        isProcessingBetAction = true;
        currentBetAction = "REPEAT";
        receivedBroadcastCount = 0;

        if (RepeatPanel != null) { RepeatPanelMain.SetActive(false); RepeatPanel.gameObject.SetActive(false); }
        if (repeatPanelCoroutine != null) { StopCoroutine(repeatPanelCoroutine); repeatPanelCoroutine = null; }

        gameManager.RepeatBet();
    }
    #endregion

    #region Helpers
    private void SetWinRatio(SimpleBetArea area, BetWager wager)
    {
        if (area != null && wager != null) area.SetWinRatio(wager.GetPayoutRatioString());
    }

    private void SetWinRatio(SumArea area, BetWager wager)
    {
        if (area != null && wager != null) area.SetWinRatio(wager.GetPayoutRatioString());
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

    private string FormatChipAmount(double amount)
    {
        if (amount >= 1000) return $"{amount / 1000}K";
        if (amount < 1) return amount.ToString("F1");
        if (amount % 1 != 0) return amount.ToString("F1");
        return amount.ToString("F0");
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

    private int GetChipIndexForAmount(double amount)
    {
        for (int i = 0; i < currentChipValues.Count; i++)
            if (System.Math.Abs(currentChipValues[i] - amount) < 0.01) return i;
        return 0;
    }

    private BaseBetArea GetBetAreaByOption(string betOption)
    {
        if (betOption == "small") return SmallArea;
        if (betOption == "big") return BigArea;
        if (betOption == "odd") return OddArea;
        if (betOption == "even") return EvenArea;

        if (betOption.StartsWith("specific_3_") && int.TryParse(betOption.Substring(11), out int tripleNum))
        {
            int idx = tripleNum - 1;
            return (idx >= 0 && idx < TripleDiceAreas.Count) ? TripleDiceAreas[idx] : null;
        }

        if (betOption.StartsWith("single_") && int.TryParse(betOption.Substring(7), out int singleNum))
        {
            int idx = singleNum - 1;
            return (idx >= 0 && idx < SingleDiceAreas.Count) ? SingleDiceAreas[idx] : null;
        }

        if (betOption.StartsWith("sum_") && int.TryParse(betOption.Substring(4), out int sumNum))
        {
            int idx = sumNum - 4;
            return (idx >= 0 && idx < SumAreas.Count) ? SumAreas[idx] : null;
        }

        return null;
    }

    private void ClearArea(SimpleBetArea area) { area?.ClearBets(); }
    private void ClearArea(TripleSameDiceArea area) { area?.ClearBets(); }
    private void ClearArea(SingleDiceArea area) { area?.ClearBets(); }
    private void ClearArea(SumArea area) { area?.ClearBets(); }

    private void SetAreaHighlight(SimpleBetArea area, bool highlight) { area?.SetHighlight(highlight); }
    #endregion

    #region Leaderboard
    internal void SetCurrentPlayerUsername(string username)
    {
        currentPlayerUsername = username;
        RefreshPlayerLeaderboardStatus();
    }

    internal void SetLeaderboardData(Leaderboards leaderboards)
    {
        currentLeaderboards = leaderboards;

        if (opponentChipManager != null)
        {
            bool hasActiveChips = opponentChipManager.IsCashoutRunning() || opponentChipManager.HasActiveChips();

            if (!hasActiveChips)
            {
                opponentChipManager.SetLeaderboardData(leaderboards);

            }

        }

        RefreshPlayerLeaderboardStatus();
        RefreshAllPlayerChipBadges();
    }

    internal void SetCashoutData(List<Payout> payouts)
    {
        opponentChipManager?.SetCashoutData(payouts);
    }

    private void RefreshPlayerLeaderboardStatus()
    {
        isPlayerRichest = IsUsernameInLeaderboardList(currentPlayerUsername, currentLeaderboards?.richest);
        isPlayerWinner = IsUsernameInLeaderboardList(currentPlayerUsername, currentLeaderboards?.winners);
    }

    private void RefreshAllPlayerChipBadges()
    {
        ApplyBadgesToContainer(SmallArea?.PlayerBetContainer);
        ApplyBadgesToContainer(BigArea?.PlayerBetContainer);
        ApplyBadgesToContainer(OddArea?.PlayerBetContainer);
        ApplyBadgesToContainer(EvenArea?.PlayerBetContainer);
        foreach (var area in TripleDiceAreas) ApplyBadgesToContainer(area?.PlayerBetContainer);
        foreach (var area in SingleDiceAreas) ApplyBadgesToContainer(area?.PlayerBetContainer);
        foreach (var area in SumAreas) ApplyBadgesToContainer(area?.PlayerBetContainer);
    }

    private void ApplyBadgesToContainer(Transform container)
    {
        if (container == null) return;
        Chip[] chips = container.GetComponentsInChildren<Chip>(true);
        foreach (var chip in chips)
        {
            if (chip == null) continue;
            if (isPlayerRichest || isPlayerWinner) chip.SetLeaderboardBadge(isPlayerRichest, isPlayerWinner);
            else chip.ClearLeaderboardBadge();
        }
    }

    internal void RefreshBadgesForContainer(Transform container)
    {
        ApplyBadgesToContainer(container);
    }

    private static bool IsUsernameInLeaderboardList(string username, List<LeaderboardEntry> entries)
    {
        if (string.IsNullOrEmpty(username) || entries == null) return false;
        int count = Mathf.Min(3, entries.Count);
        for (int i = 0; i < count; i++)
            if (entries[i] != null && entries[i].username == username) return true;
        return false;
    }
    #endregion

    #region Win Animation Data
    internal List<WinAreaData> GetWinningAreasData()
    {
        _winAreasCache.Clear();
        foreach (var kvp in areaBets)
        {
            Transform areaTransform = GetBetAreaTransform(kvp.Key);
            if (areaTransform == null) continue;

            GameObject winImage = GetWinImage(kvp.Key);
            if (winImage == null || !winImage.activeSelf) continue;

            BetWager wager = gameManager.GetWagerForBetOption(kvp.Key);
            _winAreasCache.Add(new WinAreaData
            {
                betOption = kvp.Key,
                betAreaTarget = areaTransform,
                betAmount = kvp.Value,
                winAmount = wager?.CalculateWin(kvp.Value) ?? 0
            });
        }
        return _winAreasCache;
    }

    private Transform GetBetAreaTransform(string betOption) => GetBetAreaByOption(betOption)?.PlayerBetContainer;
    private GameObject GetWinImage(string betOption) => GetBetAreaByOption(betOption)?.WinImage;

    internal List<string> GetWinningBetOptions()
    {
        _winOptionsCache.Clear();
        foreach (var kvp in areaBets)
        {
            GameObject winImage = GetWinImage(kvp.Key);
            if (winImage != null && winImage.activeSelf)
                _winOptionsCache.Add(kvp.Key);
        }
        return _winOptionsCache;
    }

    internal List<string> GetCurrentBetOptions()
    {
        _betOptionsCache.Clear();
        foreach (var key in areaBets.Keys)
            _betOptionsCache.Add(key);
        return _betOptionsCache;
    }
    #endregion

    #region Bonus Support
    internal Dictionary<string, Transform> GetBetAreaContainerMap() =>
        BonusIndicatorController.BuildBetAreaContainerMap(SmallArea, BigArea, OddArea, EvenArea, TripleDiceAreas, SingleDiceAreas, SumAreas);

    internal PlayerBetComponent GetPlayerBetComponent(string betOption) =>
        activeComponents.TryGetValue(betOption, out var c) ? c : GetBetAreaByOption(betOption)?.playerBetComponent;
    internal List<double> GetChipValues() => new List<double>(currentChipValues);
    internal Sprite[] GetChipSprites() => GetCurrentLevelSprites();

    private Dictionary<string, Transform> GetOpponentBetAreaContainerMap()
    {
        Dictionary<string, Transform> map = new Dictionary<string, Transform>();
        if (SmallArea?.OpponentBetContainer != null) map["small"] = SmallArea.OpponentBetContainer;
        if (BigArea?.OpponentBetContainer != null) map["big"] = BigArea.OpponentBetContainer;
        if (OddArea?.OpponentBetContainer != null) map["odd"] = OddArea.OpponentBetContainer;
        if (EvenArea?.OpponentBetContainer != null) map["even"] = EvenArea.OpponentBetContainer;

        for (int i = 0; i < TripleDiceAreas.Count; i++)
            if (TripleDiceAreas[i]?.OpponentBetContainer != null)
                map[$"specific_3_{i + 1}"] = TripleDiceAreas[i].OpponentBetContainer;

        for (int i = 0; i < SingleDiceAreas.Count; i++)
            if (SingleDiceAreas[i]?.OpponentBetContainer != null)
                map[$"single_{i + 1}"] = SingleDiceAreas[i].OpponentBetContainer;

        for (int i = 0; i < SumAreas.Count; i++)
            if (SumAreas[i]?.OpponentBetContainer != null)
                map[$"sum_{i + 4}"] = SumAreas[i].OpponentBetContainer;

        return map;
    }
    #endregion

    #region Level Sprite Helpers
    /// <summary>
    /// Gets the chip sprites for a given level
    /// </summary>
    private Sprite[] GetSpritesForLevel(string level)
    {
        return level switch
        {
            "casual" => levelChipSprites.casualChips,
            "novice" => levelChipSprites.noviceChips,
            "expert" => levelChipSprites.expertChips,
            "high_roller" => levelChipSprites.highRollerChips,
            _ => levelChipSprites.casualChips // Default fallback
        };
    }

    /// <summary>
    /// Gets the current level's chip sprites, or defaults to casual if no level is set
    /// </summary>
    private Sprite[] GetCurrentLevelSprites()
    {
        if (currentLevelChipSprites != null && currentLevelChipSprites.Length > 0)
            return currentLevelChipSprites;

        if (!string.IsNullOrEmpty(currentLevel))
            return GetSpritesForLevel(currentLevel);

        return levelChipSprites.casualChips;
    }
    #endregion
}

/// <summary>
/// Holds chip sprite arrays for each level
/// </summary>
[System.Serializable]
public class LevelChipSprites
{
    [Header("Casual Level (6 chips)")]
    public Sprite[] casualChips = new Sprite[6];

    [Header("Novice Level (6 chips)")]
    public Sprite[] noviceChips = new Sprite[6];

    [Header("Expert Level (6 chips)")]
    public Sprite[] expertChips = new Sprite[6];

    [Header("High Roller Level (6 chips)")]
    public Sprite[] highRollerChips = new Sprite[6];
}