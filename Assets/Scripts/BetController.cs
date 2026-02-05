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

    [Header("Bet Areas - Main (Small/Big/Odd/Even)")]
    [SerializeField] private SimpleBetArea SmallArea;
    [SerializeField] private SimpleBetArea BigArea;
    [SerializeField] private SimpleBetArea OddArea;
    [SerializeField] private SimpleBetArea EvenArea;

    [Header("Bet Areas - Triple Dice (1-6)")]
    [SerializeField] private List<TripleSameDiceArea> TripleDiceAreas;

    [Header("Bet Areas - Single Dice (1-6)")]
    [SerializeField] private List<SingleDiceArea> SingleDiceAreas;

    [Header("Bet Areas - Sum (4-17)")]
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
        if (SmallArea?.Button != null) SmallArea.Button.onClick.AddListener(() => OnBetAreaClicked("small"));
        if (BigArea?.Button != null) BigArea.Button.onClick.AddListener(() => OnBetAreaClicked("big"));
        if (OddArea?.Button != null) OddArea.Button.onClick.AddListener(() => OnBetAreaClicked("odd"));
        if (EvenArea?.Button != null) EvenArea.Button.onClick.AddListener(() => OnBetAreaClicked("even"));

        for (int i = 0; i < TripleDiceAreas.Count && i < 6; i++)
        {
            int diceNum = i + 1;
            if (TripleDiceAreas[i]?.Button != null)
            {
                TripleDiceAreas[i].Button.onClick.AddListener(() => OnBetAreaClicked($"single_{diceNum}"));
            }
        }

        for (int i = 0; i < SingleDiceAreas.Count && i < 6; i++)
        {
            int num = i + 1;
            if (SingleDiceAreas[i]?.Button != null)
            {
                SingleDiceAreas[i].Button.onClick.AddListener(() => OnBetAreaClicked($"single_{num}"));
            }
        }

        for (int i = 0; i < SumAreas.Count; i++)
        {
            int sum = i + 4;
            if (SumAreas[i]?.Button != null)
            {
                SumAreas[i].Button.onClick.AddListener(() => OnBetAreaClicked($"sum_{sum}"));
            }
        }
    }
    #endregion

    #region Public API
    internal void SetupChips(List<double> chipValues, Wagers wagers = null, string level = "")
    {
        if (chipValues == null || chipValues.Count == 0) return;

        ClearChipSelectors();
        currentChipValues = chipValues;
        wagerData = wagers;
        currentLevel = level;

        if (chipValues.Count > 0)
        {
            minBetAmount = chipValues[0];
            maxBetAmount = 0;
        }

        AssignChipSprites(chipValues);

        for (int i = 0; i < chipValues.Count; i++)
        {
            CreateChipSelectorButton(chipValues[i], i);
        }

        selectedChipIndex = 0;
        UpdateMainChipDisplay();

        if (wagers != null)
        {
            SetupWinRatios(wagers);
            CalculateMaxBet(wagers, level);
        }

        UpdateMinMaxDisplay();
        CloseChipSelector();
    }

    internal void EnableBetting()
    {
        isBettingEnabled = true;

        if (currentTotalBet > 0)
        {
            ShowBetActionsPanel();
        }
        else
        {
            ShowRepeatPanel();
        }
    }

    internal void DisableBetting()
    {
        isBettingEnabled = false;
        HideBetPanels();
        CloseChipSelector();
    }

    internal void ClearAllBets()
    {
        currentTotalBet = 0;
        areaBets.Clear();
        UpdateTotalBetDisplay();

        if (SmallArea != null) SmallArea.ClearBets();
        if (BigArea != null) BigArea.ClearBets();
        if (OddArea != null) OddArea.ClearBets();
        if (EvenArea != null) EvenArea.ClearBets();

        foreach (var area in TripleDiceAreas)
        {
            if (area != null) area.ClearBets();
        }

        foreach (var area in SingleDiceAreas)
        {
            if (area != null) area.ClearBets();
        }

        foreach (var area in SumAreas)
        {
            if (area != null) area.ClearBets();
        }

        if (isBettingEnabled)
        {
            ShowRepeatPanel();
        }
    }

    internal void HighlightWinningAreas(string matchSide, int sum)
    {
        if (SmallArea != null) SmallArea.SetHighlight(matchSide == "small");
        if (BigArea != null) BigArea.SetHighlight(matchSide == "big");
        if (OddArea != null) OddArea.SetHighlight(matchSide == "odd");
        if (EvenArea != null) EvenArea.SetHighlight(matchSide == "even");

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
            int diceNum = dice1 - 1;
            if (diceNum >= 0 && diceNum < TripleDiceAreas.Count && TripleDiceAreas[diceNum] != null)
            {
                TripleDiceAreas[diceNum].SetHighlight(true);
            }
        }
        else if (dice1 == dice2 || dice2 == dice3 || dice1 == dice3)
        {
            int matchNum = dice1 == dice2 ? dice1 : (dice2 == dice3 ? dice2 : dice1);
            int diceIndex = matchNum - 1;
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

        SimpleBetArea mainArea = GetMainBetArea(data.betOption);
        if (mainArea != null)
        {
            mainArea.AddOpponentBet(data.amount, grayChipSprite);
            return;
        }

        if (data.betOption.StartsWith("single_"))
        {
            if (int.TryParse(data.betOption.Substring(7), out int num))
            {
                int index = num - 1;
                if (index >= 0 && index < SingleDiceAreas.Count && SingleDiceAreas[index] != null)
                {
                    SingleDiceAreas[index].AddOpponentBet(data.amount, grayChipSprite);
                }
            }
            return;
        }

        if (data.betOption.StartsWith("sum_"))
        {
            if (int.TryParse(data.betOption.Substring(4), out int sum))
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

    #region Private Methods
    private void OnBetAreaClicked(string betOption)
    {
        if (!isBettingEnabled || selectedChipIndex < 0 || selectedChipIndex >= currentChipValues.Count)
        {
            return;
        }

        double betAmount = currentChipValues[selectedChipIndex];

        if (!areaBets.ContainsKey(betOption))
        {
            areaBets[betOption] = 0;
        }

        double currentBetOnArea = areaBets[betOption];
        double maxBetOnArea = GetMaxBetForOption(betOption);

        if (currentBetOnArea + betAmount > maxBetOnArea)
        {
            uiController?.ShowNotification($"Max bet on this area is {maxBetOnArea:F2}");
            return;
        }

        if (currentTotalBet + betAmount > maxBetAmount)
        {
            uiController?.ShowNotification($"Total max bet is {maxBetAmount:F2}");
            return;
        }

        gameManager.PlaceBet(betOption, selectedChipIndex);

        areaBets[betOption] += betAmount;
        currentTotalBet += betAmount;

        Sprite chipSprite = chipValueToSprite.ContainsKey(betAmount) ? chipValueToSprite[betAmount] : chipSprites[0];

        SimpleBetArea mainArea = GetMainBetArea(betOption);
        if (mainArea != null)
        {
            mainArea.AddPlayerBet(betAmount, chipSprite, playerChipStackPrefab);
        }
        else if (betOption.StartsWith("single_"))
        {
            if (int.TryParse(betOption.Substring(7), out int num))
            {
                int index = num - 1;
                if (index >= 0 && index < SingleDiceAreas.Count && SingleDiceAreas[index] != null)
                {
                    SingleDiceAreas[index].AddPlayerBet(betAmount, chipSprite, playerChipStackPrefab);
                }
            }
        }
        else if (betOption.StartsWith("sum_"))
        {
            if (int.TryParse(betOption.Substring(4), out int sum))
            {
                int index = sum - 4;
                if (index >= 0 && index < SumAreas.Count && SumAreas[index] != null)
                {
                    SumAreas[index].AddPlayerBet(betAmount, chipSprite, playerChipStackPrefab);
                }
            }
        }

        UpdateTotalBetDisplay();
        ShowBetActionsPanel();
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

    private void ShowBetActionsPanel()
    {
        if (RepeatPanel) RepeatPanel.SetActive(false);
        if (BetActionsPanel) BetActionsPanel.SetActive(true);
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

    private void UpdateTotalBetDisplay()
    {
        if (TotalBet_Text) TotalBet_Text.text = currentTotalBet.ToString("F2");
        if (TotalBetPanel) TotalBetPanel.SetActive(currentTotalBet > 0);
    }

    private void UpdateMinMaxDisplay()
    {
        if (MinBet_Text) MinBet_Text.text = $"{minBetAmount:F2}";
        if (MaxBet_Text) MaxBet_Text.text = $"{maxBetAmount:F2}";
    }

    private void UpdateMainChipDisplay()
    {
        if (selectedChipIndex < 0 || selectedChipIndex >= currentChipValues.Count) return;

        double value = currentChipValues[selectedChipIndex];

        if (MainChip_Image && chipValueToSprite.ContainsKey(value))
        {
            MainChip_Image.sprite = chipValueToSprite[value];
        }

        if (MainChip_Text)
        {
            MainChip_Text.text = value.ToString("F2");
        }
    }

    private void CreateChipSelectorButton(double value, int index)
    {
        if (chipSelectorPrefab == null || ChipOptions_Container == null) return;

        GameObject chipObj = Instantiate(chipSelectorPrefab, ChipOptions_Container);
        Chip chipComponent = chipObj.GetComponent<Chip>();

        if (chipComponent != null)
        {
            Sprite sprite = chipValueToSprite.ContainsKey(value) ? chipValueToSprite[value] : chipSprites[0];
            chipComponent.SetData(sprite, value.ToString("F2"), index);

            Button button = chipObj.GetComponent<Button>();
            if (button != null)
            {
                int chipIdx = index;
                button.onClick.AddListener(() => OnChipSelected(chipIdx));
            }
        }

        spawnedChipOptions.Add(chipObj);
    }

    private void OnChipSelected(int chipIndex)
    {
        selectedChipIndex = chipIndex;
        UpdateMainChipDisplay();
        CloseChipSelector();
    }

    private void ClearChipSelectors()
    {
        foreach (GameObject chipObj in spawnedChipOptions)
        {
            if (chipObj != null) Destroy(chipObj);
        }
        spawnedChipOptions.Clear();
        chipValueToSprite.Clear();
    }

    private void AssignChipSprites(List<double> chipValues)
    {
        if (chipSprites == null || chipSprites.Length == 0) return;

        for (int i = 0; i < chipValues.Count; i++)
        {
            int spriteIndex = i % chipSprites.Length;
            chipValueToSprite[chipValues[i]] = chipSprites[spriteIndex];
        }
    }

    private void SetupWinRatios(Wagers wagers)
    {
        if (wagers == null) return;

        if (SmallArea != null && wagers.main_bets?.small != null)
        {
            SmallArea.SetWinRatio(wagers.main_bets.small.GetPayoutRatioString());
        }

        if (BigArea != null && wagers.main_bets?.big != null)
        {
            BigArea.SetWinRatio(wagers.main_bets.big.GetPayoutRatioString());
        }

        if (OddArea != null && wagers.main_bets?.odd != null)
        {
            OddArea.SetWinRatio(wagers.main_bets.odd.GetPayoutRatioString());
        }

        if (EvenArea != null && wagers.main_bets?.even != null)
        {
            EvenArea.SetWinRatio(wagers.main_bets.even.GetPayoutRatioString());
        }

        for (int i = 0; i < TripleDiceAreas.Count && i < 6; i++)
        {
            if (TripleDiceAreas[i] != null && wagers.side_bets?.specific_3 != null)
            {
                TripleDiceAreas[i].SetWinRatio(wagers.side_bets.specific_3.GetMultiMatchPayoutString());
            }
        }

        for (int i = 0; i < SumAreas.Count; i++)
        {
            int sum = i + 4;
            BetWager wager = GetSumWager(wagers, sum);
            if (wager != null && SumAreas[i] != null)
            {
                SumAreas[i].SetWinRatio(wager.GetPayoutRatioString());
            }
        }
    }

    private void CalculateMaxBet(Wagers wagers, string level)
    {
        if (wagers == null) return;

        double highestMax = 0;

        if (wagers.main_bets?.small != null)
        {
            highestMax = System.Math.Max(highestMax, wagers.main_bets.small.GetMaxBet(level));
        }

        if (wagers.op_bets?.sum_10 != null)
        {
            highestMax = System.Math.Max(highestMax, wagers.op_bets.sum_10.GetMaxBet(level));
        }

        maxBetAmount = highestMax;
    }

    private double GetMaxBetForOption(string betOption)
    {
        if (wagerData == null || string.IsNullOrEmpty(currentLevel)) return 0;

        BetWager wager = null;

        if (betOption == "small") wager = wagerData.main_bets?.small;
        else if (betOption == "big") wager = wagerData.main_bets?.big;
        else if (betOption == "odd") wager = wagerData.main_bets?.odd;
        else if (betOption == "even") wager = wagerData.main_bets?.even;
        else if (betOption.StartsWith("single_")) wager = wagerData.side_bets?.single_match_1;
        else if (betOption.StartsWith("sum_"))
        {
            if (int.TryParse(betOption.Substring(4), out int sum))
            {
                wager = GetSumWager(wagerData, sum);
            }
        }

        return wager?.GetMaxBet(currentLevel) ?? 0;
    }

    private BetWager GetSumWager(Wagers wagers, int sum)
    {
        if (wagers?.op_bets == null) return null;

        return sum switch
        {
            4 => wagers.op_bets.sum_4,
            5 => wagers.op_bets.sum_5,
            6 => wagers.op_bets.sum_6,
            7 => wagers.op_bets.sum_7,
            8 => wagers.op_bets.sum_8,
            9 => wagers.op_bets.sum_9,
            10 => wagers.op_bets.sum_10,
            11 => wagers.op_bets.sum_11,
            12 => wagers.op_bets.sum_12,
            13 => wagers.op_bets.sum_13,
            14 => wagers.op_bets.sum_14,
            15 => wagers.op_bets.sum_15,
            16 => wagers.op_bets.sum_16,
            17 => wagers.op_bets.sum_17,
            _ => null
        };
    }

    private SimpleBetArea GetMainBetArea(string betOption)
    {
        return betOption switch
        {
            "small" => SmallArea,
            "big" => BigArea,
            "odd" => OddArea,
            "even" => EvenArea,
            _ => null
        };
    }
    #endregion
}

#region Bet Area Components
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
    private GameObject opponentChip = null;

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

        UpdateDisplay();
    }

    public void AddOpponentBet(double amount, Sprite graySprite)
    {
        opponentBetAmount += amount;
        UpdateDisplay();
    }

    public void ClearBets()
    {
        playerBetAmount = 0;
        opponentBetAmount = 0;

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

        if (WinImage) WinImage.SetActive(false);
        UpdateDisplay();
    }

    public void SetHighlight(bool highlight)
    {
        if (WinImage) WinImage.SetActive(highlight);
    }

    private void UpdateDisplay()
    {
        if (PlayerBetContainer) PlayerBetContainer.gameObject.SetActive(playerBetAmount > 0);
    }
}

[System.Serializable]
public class TripleSameDiceArea
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

        UpdateDisplay();
    }

    public void ClearBets()
    {
        playerBetAmount = 0;

        foreach (var chip in playerChips)
        {
            if (chip != null) Object.Destroy(chip);
        }
        playerChips.Clear();

        if (WinImage) WinImage.SetActive(false);
        UpdateDisplay();
    }

    public void SetHighlight(bool highlight)
    {
        if (WinImage) WinImage.SetActive(highlight);
    }

    private void UpdateDisplay()
    {
        if (PlayerBetContainer) PlayerBetContainer.gameObject.SetActive(playerBetAmount > 0);
    }
}

[System.Serializable]
public class SingleDiceArea
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

        UpdateDisplay();
    }

    public void AddOpponentBet(double amount, Sprite graySprite)
    {
        UpdateDisplay();
    }

    public void ClearBets()
    {
        playerBetAmount = 0;

        foreach (var chip in playerChips)
        {
            if (chip != null) Object.Destroy(chip);
        }
        playerChips.Clear();

        if (WinImage) WinImage.SetActive(false);
        UpdateDisplay();
    }

    public void SetHighlight(bool highlight)
    {
        if (WinImage) WinImage.SetActive(highlight);
    }

    private void UpdateDisplay()
    {
        if (PlayerBetContainer) PlayerBetContainer.gameObject.SetActive(playerBetAmount > 0);
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

        UpdateDisplay();
    }

    public void AddOpponentBet(double amount, Sprite graySprite)
    {
        UpdateDisplay();
    }

    public void ClearBets()
    {
        playerBetAmount = 0;

        foreach (var chip in playerChips)
        {
            if (chip != null) Object.Destroy(chip);
        }
        playerChips.Clear();

        if (WinImage) WinImage.SetActive(false);
        UpdateDisplay();
    }

    public void SetHighlight(bool highlight)
    {
        if (WinImage) WinImage.SetActive(highlight);
    }

    private void UpdateDisplay()
    {
        if (PlayerBetContainer) PlayerBetContainer.gameObject.SetActive(playerBetAmount > 0);
    }
}
#endregion