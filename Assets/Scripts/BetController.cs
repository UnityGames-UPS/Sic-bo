using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    [SerializeField] private GameObject RepeatPanel;
    [SerializeField] private Button Repeat_Button;
    [SerializeField] private GameObject BetActionsPanel;
    [SerializeField] private Button Undo_Button;
    [SerializeField] private Button Cancel_Button;
    [SerializeField] private Button Double_Button;

    [Header("Total Bet Display")]
    [SerializeField] private TMP_Text TotalBet_Text;
    [SerializeField] private GameObject TotalBetPanel;

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
    private List<GameObject> spawnedChipOptions = new List<GameObject>();
    private int selectedChipIndex = 0;
    private double currentTotalBet = 0;
    private bool isBettingEnabled = false;
    private bool isChipSelectorOpen = false;
    private Dictionary<string, double> areaBets = new Dictionary<string, double>();
    private Wagers wagerData = null;
    private string currentLevel = "";
    private double minBetAmount = 0;
    private double maxBetAmount = 0;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        SetupButtonListeners();
        SetupBetAreaListeners();
        DisableBetting();
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
    #endregion

    #region Public API - Chip Setup
    internal void SetupChips(List<double> chipValues, Wagers wagers, string level)
    {
        currentChipValues = chipValues ?? new List<double>();
        wagerData = wagers;
        currentLevel = level;

        if (currentChipValues.Count == 0) return;

        BuildChipValueToSpriteMap();
        CreateChipSelector();
        SelectChipAt(0);
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

    private void CreateChipSelector()
    {
        foreach (var chip in spawnedChipOptions)
        {
            if (chip != null) Destroy(chip);
        }
        spawnedChipOptions.Clear();

        if (ChipOptions_Container == null || chipSelectorPrefab == null) return;

        for (int i = 0; i < currentChipValues.Count; i++)
        {
            GameObject chipObj = Instantiate(chipSelectorPrefab, ChipOptions_Container);
            Chip chip = chipObj.GetComponent<Chip>();

            if (chip != null)
            {
                Sprite chipSprite = GetChipSprite(currentChipValues[i]);
                chip.SetData(chipSprite, FormatChipAmount(currentChipValues[i]), i);

                Button button = chipObj.GetComponent<Button>();
                if (button != null)
                {
                    int index = i;
                    button.onClick.AddListener(() => OnChipSelected(index));
                }
            }

            spawnedChipOptions.Add(chipObj);
        }
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
        ShowRepeatPanel();
        UpdateTotalBet();
    }

    internal void DisableBetting()
    {
        isBettingEnabled = false;
        CloseChipSelector();
        HideBetPanels();
    }

    /// <summary>
    /// ✅ FIXED: Only destroys spawned chip GameObjects, keeps parent containers visible
    /// </summary>
    internal void ClearAllBets()
    {
        Debug.Log("[BET] Clearing all bets - destroying only chip objects");

        areaBets.Clear();
        currentTotalBet = 0;

        // Clear main areas - only destroys chips
        ClearArea(SmallArea);
        ClearArea(BigArea);
        ClearArea(OddArea);
        ClearArea(EvenArea);

        // Clear triple dice areas - only destroys chips
        foreach (var area in TripleDiceAreas) ClearArea(area);

        // Clear single dice areas - only destroys chips
        foreach (var area in SingleDiceAreas) ClearArea(area);

        // Clear sum areas - only destroys chips
        foreach (var area in SumAreas) ClearArea(area);

        UpdateTotalBet();
        HideBetPanels();

        if (isBettingEnabled) ShowRepeatPanel();

        Debug.Log("[BET] All bets cleared - board remains visible");
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

        if (!ValidateBet(betOption, betAmount)) return;

        AddBetToArea(betOption, betAmount, chipSprite);
        gameManager.PlaceBet(betOption, selectedChipIndex);

        CloseChipSelector();
        ShowBetActionsPanel();
    }

    private void OnTripleDiceAreaClicked(int diceNum)
    {
        if (!isBettingEnabled || currentChipValues.Count == 0) return;

        string betOption = $"specific_3";
        double betAmount = currentChipValues[selectedChipIndex];
        Sprite chipSprite = GetChipSprite(betAmount);

        if (!ValidateBet(betOption, betAmount)) return;

        int areaIndex = diceNum - 1;
        if (areaIndex >= 0 && areaIndex < TripleDiceAreas.Count && TripleDiceAreas[areaIndex] != null)
        {
            TripleDiceAreas[areaIndex].AddPlayerBet(betAmount, chipSprite, playerChipStackPrefab);
            RecordBet(betOption, betAmount);
        }

        gameManager.PlaceBet(betOption, selectedChipIndex);
        CloseChipSelector();
        ShowBetActionsPanel();
    }

    private void OnSingleDiceAreaClicked(int diceNum)
    {
        if (!isBettingEnabled || currentChipValues.Count == 0) return;

        string betOption = $"single_{diceNum}";
        double betAmount = currentChipValues[selectedChipIndex];
        Sprite chipSprite = GetChipSprite(betAmount);

        if (!ValidateBet(betOption, betAmount)) return;

        int areaIndex = diceNum - 1;
        if (areaIndex >= 0 && areaIndex < SingleDiceAreas.Count && SingleDiceAreas[areaIndex] != null)
        {
            SingleDiceAreas[areaIndex].AddPlayerBet(betAmount, chipSprite, playerChipStackPrefab);
            RecordBet(betOption, betAmount);
        }

        gameManager.PlaceBet(betOption, selectedChipIndex);
        CloseChipSelector();
        ShowBetActionsPanel();
    }

    private void AddBetToArea(string betOption, double amount, Sprite chipSprite)
    {
        if (betOption == "small" && SmallArea != null)
        {
            SmallArea.AddPlayerBet(amount, chipSprite, playerChipStackPrefab);
        }
        else if (betOption == "big" && BigArea != null)
        {
            BigArea.AddPlayerBet(amount, chipSprite, playerChipStackPrefab);
        }
        else if (betOption == "odd" && OddArea != null)
        {
            OddArea.AddPlayerBet(amount, chipSprite, playerChipStackPrefab);
        }
        else if (betOption == "even" && EvenArea != null)
        {
            EvenArea.AddPlayerBet(amount, chipSprite, playerChipStackPrefab);
        }
        else if (betOption == "specific_2")
        {
            int index = ExtractDiceNumberFromSpecific2(betOption);
            if (index >= 0 && index < TripleDiceAreas.Count && TripleDiceAreas[index] != null)
            {
                TripleDiceAreas[index].AddPlayerBet(amount, chipSprite, playerChipStackPrefab);
            }
        }
        else if (betOption.StartsWith("sum_"))
        {
            if (int.TryParse(betOption.Replace("sum_", ""), out int sum))
            {
                int index = sum - 4;
                if (index >= 0 && index < SumAreas.Count && SumAreas[index] != null)
                {
                    SumAreas[index].AddPlayerBet(amount, chipSprite, playerChipStackPrefab);
                }
            }
        }

        RecordBet(betOption, amount);
    }

    private int ExtractDiceNumberFromSpecific2(string betOption)
    {
        return -1;
    }

    private void RecordBet(string betOption, double amount)
    {
        if (!areaBets.ContainsKey(betOption))
        {
            areaBets[betOption] = 0;
        }

        areaBets[betOption] += amount;
        currentTotalBet += amount;
        UpdateTotalBet();
    }

    private bool ValidateBet(string betOption, double amount)
    {
        BetWager wager = GetWagerForBetOption(betOption);
        if (wager == null) return true;

        double maxBetForArea = wager.GetMaxBet(currentLevel);
        double currentBetOnArea = areaBets.ContainsKey(betOption) ? areaBets[betOption] : 0;

        if (currentBetOnArea + amount > maxBetForArea)
        {
            Debug.LogWarning($"Bet exceeds max for {betOption}. Max: {maxBetForArea}, Current: {currentBetOnArea}");
            return false;
        }

        return true;
    }

    private BetWager GetWagerForBetOption(string betOption)
    {
        if (wagerData == null) return null;

        if (betOption == "small") return wagerData.main_bets?.small;
        if (betOption == "big") return wagerData.main_bets?.big;
        if (betOption == "odd") return wagerData.main_bets?.odd;
        if (betOption == "even") return wagerData.main_bets?.even;

        if (betOption.StartsWith("single_")) return wagerData.side_bets?.single_match_1;
        if (betOption == "specific_2") return wagerData.side_bets?.specific_2;
        if (betOption == "specific_3") return wagerData.side_bets?.specific_3;

        if (betOption.StartsWith("sum_"))
        {
            if (int.TryParse(betOption.Replace("sum_", ""), out int sum))
            {
                return sum switch
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

        return null;
    }
    #endregion

    #region Private Methods - Chip Selector
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
    }

    private void CloseChipSelector()
    {
        if (ChipSelector_Panel) ChipSelector_Panel.SetActive(false);
        if (ChipSelector_BlackBG) ChipSelector_BlackBG.SetActive(false);
        isChipSelectorOpen = false;
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

    #region Private Methods - UI Updates
    private void UpdateTotalBet()
    {
        if (TotalBet_Text) TotalBet_Text.text = $"{currentTotalBet:F2}";

        if (TotalBetPanel)
        {
            TotalBetPanel.SetActive(currentTotalBet > 0);
        }
    }

    private void ShowBetActionsPanel()
    {
        if (BetActionsPanel) BetActionsPanel.SetActive(true);
        if (RepeatPanel) RepeatPanel.SetActive(false);
    }

    private void ShowRepeatPanel()
    {
        if (BetActionsPanel) BetActionsPanel.SetActive(false);
        if (RepeatPanel) RepeatPanel.SetActive(true);
    }

    private void HideBetPanels()
    {
        if (BetActionsPanel) BetActionsPanel.SetActive(false);
        if (RepeatPanel) RepeatPanel.SetActive(false);
    }
    #endregion

    #region Private Methods - Button Handlers
    private void OnUndoClicked()
    {
        gameManager.UndoBet();
    }

    private void OnCancelClicked()
    {
        gameManager.CancelAllBets();
    }

    private void OnDoubleClicked()
    {
        gameManager.DoubleBet();
    }

    private void OnRepeatClicked()
    {
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
}

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
        playerBetAmount += amount;

        if (PlayerBetContainer != null && prefab != null)
        {
            GameObject chipObj = Object.Instantiate(prefab, PlayerBetContainer);
            Chip chip = chipObj.GetComponent<Chip>();
            if (chip != null)
            {
                chip.SetSprite(chipSprite);
                chip.SetAmount(amount.ToString("F0"));
            }

            playerChips.Add(chipObj);

            if (playerChips.Count > 6)
            {
                GameObject oldChip = playerChips[0];
                playerChips.RemoveAt(0);
                Object.Destroy(oldChip);
            }
        }
    }

    public void AddOpponentBet(double amount, Sprite graySprite)
    {
        opponentBetAmount += amount;
    }

    /// <summary>
    /// ✅ FIXED: Only destroys chip GameObjects, keeps PlayerBetContainer visible
    /// </summary>
    public void ClearBets()
    {
        playerBetAmount = 0;
        opponentBetAmount = 0;

        // Destroy all player chips
        foreach (var chip in playerChips)
        {
            if (chip != null) Object.Destroy(chip);
        }
        playerChips.Clear();

        // Destroy opponent chip
        if (opponentChip != null)
        {
            Object.Destroy(opponentChip);
            opponentChip = null;
        }

        // Hide win indicator
        if (WinImage) WinImage.SetActive(false);

        // ✅ REMOVED: No longer calls UpdateDisplay() which was hiding the container
        // PlayerBetContainer stays active so the board remains visible
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

    private double playerBetAmount = 0;
    private List<GameObject> playerChips = new List<GameObject>();

    public void AddPlayerBet(double amount, Sprite chipSprite, GameObject prefab)
    {
        playerBetAmount += amount;

        if (PlayerBetContainer != null && prefab != null)
        {
            GameObject chipObj = Object.Instantiate(prefab, PlayerBetContainer);
            Chip chip = chipObj.GetComponent<Chip>();
            if (chip != null)
            {
                chip.SetSprite(chipSprite);
                chip.SetAmount(amount.ToString("F0"));
            }

            playerChips.Add(chipObj);

            if (playerChips.Count > 6)
            {
                GameObject oldChip = playerChips[0];
                playerChips.RemoveAt(0);
                Object.Destroy(oldChip);
            }
        }
    }

    /// <summary>
    /// ✅ FIXED: Only destroys chip GameObjects, keeps PlayerBetContainer visible
    /// </summary>
    public void ClearBets()
    {
        playerBetAmount = 0;

        // Destroy all player chips
        foreach (var chip in playerChips)
        {
            if (chip != null) Object.Destroy(chip);
        }
        playerChips.Clear();

        // Hide win indicator
        if (WinImage) WinImage.SetActive(false);

        // ✅ REMOVED: No longer calls UpdateDisplay()
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

    private double playerBetAmount = 0;
    private double opponentBetAmount = 0;
    private List<GameObject> playerChips = new List<GameObject>();
    private GameObject opponentChip;

    public void AddPlayerBet(double amount, Sprite chipSprite, GameObject prefab)
    {
        playerBetAmount += amount;

        if (PlayerBetContainer != null && prefab != null)
        {
            GameObject chipObj = Object.Instantiate(prefab, PlayerBetContainer);
            Chip chip = chipObj.GetComponent<Chip>();
            if (chip != null)
            {
                chip.SetSprite(chipSprite);
                chip.SetAmount(amount.ToString("F0"));
            }

            playerChips.Add(chipObj);

            if (playerChips.Count > 6)
            {
                GameObject oldChip = playerChips[0];
                playerChips.RemoveAt(0);
                Object.Destroy(oldChip);
            }
        }
    }

    public void AddOpponentBet(double amount, Sprite graySprite)
    {
        opponentBetAmount += amount;
    }

    /// <summary>
    /// ✅ FIXED: Only destroys chip GameObjects, keeps PlayerBetContainer visible
    /// </summary>
    public void ClearBets()
    {
        playerBetAmount = 0;
        opponentBetAmount = 0;

        // Destroy all player chips
        foreach (var chip in playerChips)
        {
            if (chip != null) Object.Destroy(chip);
        }
        playerChips.Clear();

        // Destroy opponent chip
        if (opponentChip != null)
        {
            Object.Destroy(opponentChip);
            opponentChip = null;
        }

        // Hide win indicator
        if (WinImage) WinImage.SetActive(false);

        // ✅ REMOVED: No longer calls UpdateDisplay()
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

    private double playerBetAmount = 0;
    private List<GameObject> playerChips = new List<GameObject>();

    public void SetWinRatio(string ratio)
    {
        if (WinRatio_Text) WinRatio_Text.text = ratio;
    }

    public void AddPlayerBet(double amount, Sprite chipSprite, GameObject prefab)
    {
        playerBetAmount += amount;

        if (PlayerBetContainer != null && prefab != null)
        {
            GameObject chipObj = Object.Instantiate(prefab, PlayerBetContainer);
            Chip chip = chipObj.GetComponent<Chip>();
            if (chip != null)
            {
                chip.SetSprite(chipSprite);
                chip.SetAmount(amount.ToString("F0"));
            }

            playerChips.Add(chipObj);

            if (playerChips.Count > 6)
            {
                GameObject oldChip = playerChips[0];
                playerChips.RemoveAt(0);
                Object.Destroy(oldChip);
            }
        }
    }

    public void AddOpponentBet(double amount, Sprite graySprite)
    {
        // Optional: implement if needed
    }

    /// <summary>
    /// ✅ FIXED: Only destroys chip GameObjects, keeps PlayerBetContainer visible
    /// </summary>
    public void ClearBets()
    {
        playerBetAmount = 0;

        // Destroy all player chips
        foreach (var chip in playerChips)
        {
            if (chip != null) Object.Destroy(chip);
        }
        playerChips.Clear();

        // Hide win indicator
        if (WinImage) WinImage.SetActive(false);

        // ✅ REMOVED: No longer calls UpdateDisplay()
    }

    public void SetHighlight(bool highlight)
    {
        if (WinImage) WinImage.SetActive(highlight);
    }
}
#endregion