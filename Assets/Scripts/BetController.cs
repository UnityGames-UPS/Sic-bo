using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Improved BetController with:
/// 1. Dynamic chip spawning (supports 6+ chips from init data)
/// 2. Chip stack visualization for player bets (shows individual chips)
/// 3. Gray chip for opponent bets with count
/// 4. 10 random chip sprites assigned to chip values
/// </summary>
public class BetController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Chip Selector - Main Chip")]
    [SerializeField] private Button MainChip_Button;
    [SerializeField] private Image MainChip_Image;
    [SerializeField] private TMP_Text MainChip_Text;
    [SerializeField] private GameObject ChipSelector_Panel;
    [SerializeField] private GameObject ChipSelector_BlackBG;
    [SerializeField] private Transform ChipOptions_Container; // Container for chip options

    [Header("Chip Prefabs")]
    [SerializeField] private GameObject chipSelectorPrefab; // Prefab for chip selector buttons (with Chip component)
    [SerializeField] private GameObject playerChipStackPrefab; // Prefab for player bet chips (multiple chips stacked)
    [SerializeField] private GameObject opponentChipPrefab; // Gray chip prefab for opponents
    [SerializeField] private Sprite[] chipSprites; // 10 different chip sprites
    [SerializeField] private Sprite grayChipSprite; // Gray chip for opponents

    [Header("Bet Areas - Main")]
    [SerializeField] private BetAreaView SmallArea;
    [SerializeField] private BetAreaView BigArea;
    [SerializeField] private BetAreaView OddArea;
    [SerializeField] private BetAreaView EvenArea;

    [Header("Bet Areas - Triple Dice (6 areas)")]
    [SerializeField] private List<TripleDiceArea> TripleDiceAreas;

    [Header("Bet Areas - Single Number")]
    [SerializeField] private List<BetAreaView> SingleAreas; // 1-6

    [Header("Bet Areas - Sum")]
    [SerializeField] private List<BetAreaView> SumAreas; // 4-17

    [Header("Bet Controls - Repeat")]
    [SerializeField] private GameObject RepeatPanel;
    [SerializeField] private Button Repeat_Button;

    [Header("Bet Controls - Actions")]
    [SerializeField] private GameObject BetActionsPanel;
    [SerializeField] private Button Undo_Button;
    [SerializeField] private Button Cancel_Button;
    [SerializeField] private Button Double_Button;

    [Header("Total Bet Display")]
    [SerializeField] private TMP_Text TotalBet_Text;
    [SerializeField] private GameObject TotalBetPanel;

    [Header("References")]
    [SerializeField] private GameManager gameManager;
    #endregion

    #region Private Fields
    private List<double> currentChipValues = new List<double>();
    private Dictionary<double, Sprite> chipValueToSprite = new Dictionary<double, Sprite>(); // Maps chip value to sprite
    private List<GameObject> spawnedChipOptions = new List<GameObject>(); // Dynamically spawned chip selectors
    private int selectedChipIndex = 0;
    private double currentTotalBet = 0;
    private bool isBettingEnabled = false;
    private bool isChipSelectorOpen = false;
    private Dictionary<string, BetAreaView> betAreaMap = new Dictionary<string, BetAreaView>();
    private Dictionary<int, TripleDiceArea> tripleDiceMap = new Dictionary<int, TripleDiceArea>();
    private Wagers wagerData = null;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        SetupButtonListeners();
        MapBetAreas();
        DisableBetting();
    }
    #endregion

    #region Setup
    private void SetupButtonListeners()
    {
        // Main chip selector button
        if (MainChip_Button) MainChip_Button.onClick.AddListener(ToggleChipSelector);

        // Black background closes selector
        if (ChipSelector_BlackBG)
        {
            Button bgButton = ChipSelector_BlackBG.GetComponent<Button>();
            if (bgButton == null) bgButton = ChipSelector_BlackBG.AddComponent<Button>();
            bgButton.onClick.AddListener(CloseChipSelector);
        }

        // Bet action buttons
        if (Undo_Button) Undo_Button.onClick.AddListener(OnUndoClicked);
        if (Cancel_Button) Cancel_Button.onClick.AddListener(OnCancelClicked);
        if (Double_Button) Double_Button.onClick.AddListener(OnDoubleClicked);
        if (Repeat_Button) Repeat_Button.onClick.AddListener(OnRepeatClicked);

        // Setup bet area clicks
        SetupBetAreaListeners();
    }

    private void SetupBetAreaListeners()
    {
        // Main bets
        if (SmallArea?.Button != null) SmallArea.Button.onClick.AddListener(() => OnBetAreaClicked("small"));
        if (BigArea?.Button != null) BigArea.Button.onClick.AddListener(() => OnBetAreaClicked("big"));
        if (OddArea?.Button != null) OddArea.Button.onClick.AddListener(() => OnBetAreaClicked("odd"));
        if (EvenArea?.Button != null) EvenArea.Button.onClick.AddListener(() => OnBetAreaClicked("even"));

        // Triple dice areas
        for (int i = 0; i < TripleDiceAreas.Count && i < 6; i++)
        {
            int diceNum = i + 1;
            if (TripleDiceAreas[i]?.Button != null)
            {
                TripleDiceAreas[i].Button.onClick.AddListener(() => OnTripleDiceClicked(diceNum));
            }
        }

        // Single number bets
        for (int i = 0; i < SingleAreas.Count; i++)
        {
            int num = i + 1;
            if (SingleAreas[i]?.Button != null) SingleAreas[i].Button.onClick.AddListener(() => OnBetAreaClicked($"single_{num}"));
        }

        

        // Sum bets
        for (int i = 0; i < SumAreas.Count; i++)
        {
            int sum = i + 4;
            if (SumAreas[i]?.Button != null) SumAreas[i].Button.onClick.AddListener(() => OnBetAreaClicked($"sum_{sum}"));
        }
    }

    private void MapBetAreas()
    {
        betAreaMap.Clear();
        tripleDiceMap.Clear();

        // Main bets
        if (SmallArea != null) betAreaMap["small"] = SmallArea;
        if (BigArea != null) betAreaMap["big"] = BigArea;
        if (OddArea != null) betAreaMap["odd"] = OddArea;
        if (EvenArea != null) betAreaMap["even"] = EvenArea;

        // Triple dice areas
        for (int i = 0; i < TripleDiceAreas.Count && i < 6; i++)
        {
            if (TripleDiceAreas[i] != null)
            {
                int diceNum = i + 1;
                tripleDiceMap[diceNum] = TripleDiceAreas[i];
            }
        }

        // Single number bets
        for (int i = 0; i < SingleAreas.Count; i++)
        {
            if (SingleAreas[i] != null) betAreaMap[$"single_{i + 1}"] = SingleAreas[i];
        }

        // Sum bets
        for (int i = 0; i < SumAreas.Count; i++)
        {
            if (SumAreas[i] != null) betAreaMap[$"sum_{i + 4}"] = SumAreas[i];
        }
    }
    #endregion

    #region Public API
    /// <summary>
    /// Setup chips dynamically based on init data
    /// Assigns random sprites from 10 available chip sprites
    /// </summary>
    internal void SetupChips(List<double> chipValues, Wagers wagers = null)
    {
        if (chipValues == null || chipValues.Count == 0) return;

        // Clear previous chips
        ClearChipSelectors();

        currentChipValues = chipValues;
        wagerData = wagers;

        // Assign random sprites to chip values
        AssignChipSprites(chipValues);

        // Create chip selector buttons dynamically
        for (int i = 0; i < chipValues.Count; i++)
        {
            CreateChipSelectorButton(chipValues[i], i);
        }

        // Select first chip as default
        selectedChipIndex = 0;
        UpdateMainChipDisplay();

        // Setup win ratios on all bet areas
        if (wagers != null)
        {
            SetupWinRatios(wagers);
        }

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
        UpdateTotalBetDisplay();

        // Clear main bet areas
        foreach (var area in betAreaMap.Values)
        {
            area.ClearMyBet();
            area.ClearOtherBets();
            area.HideHighlight();
        }

        // Clear triple dice areas
        foreach (var tripleArea in tripleDiceMap.Values)
        {
            tripleArea.ClearMyBet();
            tripleArea.ClearOtherBets();
            tripleArea.HideHighlight();
        }

        // Show repeat panel if betting enabled
        if (isBettingEnabled)
        {
            ShowRepeatPanel();
        }
    }

    internal void ShowOtherPlayerBet(BetPlacedData data)
    {
        if (data == null) return;

        // Handle triple dice bets differently
        if (data.betOption.StartsWith("single_"))
        {
            // Parse dice number from bet option
            if (int.TryParse(data.betOption.Replace("single_", ""), out int diceNum))
            {
                if (tripleDiceMap.ContainsKey(diceNum))
                {
                    if (data.amount > 0)
                        tripleDiceMap[diceNum].AddOtherPlayerBet(data.amount, grayChipSprite);
                    else
                        tripleDiceMap[diceNum].RemoveOtherPlayerBet(-data.amount);
                }
            }
        }
        else if (betAreaMap.ContainsKey(data.betOption))
        {
            if (data.amount > 0)
                betAreaMap[data.betOption].AddOtherPlayerBet(data.amount, grayChipSprite);
            else
                betAreaMap[data.betOption].RemoveOtherPlayerBet(-data.amount);
        }
    }

    internal void HighlightWinningAreas(string matchSide, int sum)
    {
        // Highlight main bet (small/big/odd/even)
        if (betAreaMap.ContainsKey(matchSide))
        {
            betAreaMap[matchSide].ShowWinHighlight();
        }

        // Highlight corresponding sum
        string sumKey = $"sum_{sum}";
        if (betAreaMap.ContainsKey(sumKey))
        {
            betAreaMap[sumKey].ShowWinHighlight();
        }

        // Check odd/even
        string oddEven = (sum % 2 == 0) ? "even" : "odd";
        if (betAreaMap.ContainsKey(oddEven))
        {
            betAreaMap[oddEven].ShowWinHighlight();
        }
    }

    internal void HighlightTripleDiceResult(int dice1, int dice2, int dice3)
    {
        // Count occurrences of each dice number
        Dictionary<int, int> diceCount = new Dictionary<int, int>();

        void CountDice(int diceValue)
        {
            if (!diceCount.ContainsKey(diceValue))
                diceCount[diceValue] = 0;
            diceCount[diceValue]++;
        }

        CountDice(dice1);
        CountDice(dice2);
        CountDice(dice3);

        // Highlight triple dice areas based on matches
        foreach (var kvp in diceCount)
        {
            int diceNum = kvp.Key;
            int count = kvp.Value;

            if (tripleDiceMap.ContainsKey(diceNum))
            {
                // 2 or 3 matches wins the bet
                if (count >= 2)
                {
                    tripleDiceMap[diceNum].ShowWinHighlight();
                }
            }
        }

        // Also check for specific doubles and triples
        if (diceCount.Count == 1) // All three same
        {
            if (betAreaMap.ContainsKey("specific_3"))
            {
                betAreaMap["specific_3"].ShowWinHighlight();
            }
        }
        else if (diceCount.Count == 2) // Two pairs
        {
            if (betAreaMap.ContainsKey("specific_2"))
            {
                betAreaMap["specific_2"].ShowWinHighlight();
            }
        }
    }
    #endregion

    #region Private Methods - Chip Management
    /// <summary>
    /// Assign random chip sprites to chip values from 10 available sprites
    /// </summary>
    private void AssignChipSprites(List<double> chipValues)
    {
        chipValueToSprite.Clear();

        if (chipSprites == null || chipSprites.Length == 0)
        {
            Debug.LogWarning("[BET] No chip sprites available");
            return;
        }

        // Create a list of available sprite indices
        List<int> availableIndices = new List<int>();
        for (int i = 0; i < chipSprites.Length; i++)
        {
            availableIndices.Add(i);
        }

        // Shuffle the indices
        for (int i = availableIndices.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = availableIndices[i];
            availableIndices[i] = availableIndices[j];
            availableIndices[j] = temp;
        }

        // Assign sprites to chip values
        for (int i = 0; i < chipValues.Count; i++)
        {
            int spriteIndex = availableIndices[i % chipSprites.Length];
            chipValueToSprite[chipValues[i]] = chipSprites[spriteIndex];
        }

        Debug.Log($"[BET] Assigned {chipValues.Count} chip sprites from {chipSprites.Length} available");
    }

    /// <summary>
    /// Create a chip selector button dynamically
    /// </summary>
    private void CreateChipSelectorButton(double chipValue, int index)
    {
        if (chipSelectorPrefab == null || ChipOptions_Container == null) return;

        GameObject chipButton = Instantiate(chipSelectorPrefab, ChipOptions_Container);
        spawnedChipOptions.Add(chipButton);

        Chip chipComponent = chipButton.GetComponent<Chip>();
        if (chipComponent != null && chipValueToSprite.ContainsKey(chipValue))
        {
            chipComponent.SetData(chipValueToSprite[chipValue], chipValue.ToString("F0"), index);
        }

        // Add click listener
        Button button = chipButton.GetComponent<Button>();
        if (button != null)
        {
            int capturedIndex = index;
            button.onClick.AddListener(() => OnChipSelected(capturedIndex));
        }
    }

    /// <summary>
    /// Clear all dynamically spawned chip selectors
    /// </summary>
    private void ClearChipSelectors()
    {
        foreach (var chip in spawnedChipOptions)
        {
            if (chip != null) Destroy(chip);
        }
        spawnedChipOptions.Clear();
        chipValueToSprite.Clear();
    }

    private Sprite GetChipSprite(double chipValue)
    {
        if (chipValueToSprite.ContainsKey(chipValue))
        {
            return chipValueToSprite[chipValue];
        }
        return chipSprites != null && chipSprites.Length > 0 ? chipSprites[0] : null;
    }
    #endregion

    #region Private Methods - Chip Selector
    private void ToggleChipSelector()
    {
        if (!isBettingEnabled) return;

        isChipSelectorOpen = !isChipSelectorOpen;

        if (ChipSelector_Panel) ChipSelector_Panel.SetActive(isChipSelectorOpen);
        if (ChipSelector_BlackBG) ChipSelector_BlackBG.SetActive(isChipSelectorOpen);
    }

    private void CloseChipSelector()
    {
        isChipSelectorOpen = false;

        if (ChipSelector_Panel) ChipSelector_Panel.SetActive(false);
        if (ChipSelector_BlackBG) ChipSelector_BlackBG.SetActive(false);
    }

    private void OnChipSelected(int index)
    {
        if (index < 0 || index >= currentChipValues.Count) return;

        selectedChipIndex = index;
        UpdateMainChipDisplay();
        CloseChipSelector();
    }

    private void UpdateMainChipDisplay()
    {
        if (selectedChipIndex < 0 || selectedChipIndex >= currentChipValues.Count) return;

        double chipValue = currentChipValues[selectedChipIndex];
        Sprite chipSprite = GetChipSprite(chipValue);

        if (MainChip_Image && chipSprite) MainChip_Image.sprite = chipSprite;
        if (MainChip_Text) MainChip_Text.text = chipValue.ToString("F0");
    }
    #endregion

    #region Private Methods - Win Ratio Setup
    private void SetupWinRatios(Wagers wagers)
    {
        // Setup main bets win ratios
        SetWinRatio("small", wagers.main_bets?.small);
        SetWinRatio("big", wagers.main_bets?.big);
        SetWinRatio("odd", wagers.main_bets?.odd);
        SetWinRatio("even", wagers.main_bets?.even);

        // Setup single match win ratios (single_1 to single_6)
        SetWinRatio("single_1", wagers.side_bets?.single_match_1);
        SetWinRatio("single_2", wagers.side_bets?.single_match_2);
        SetWinRatio("single_3", wagers.side_bets?.single_match_3);

        // Note: single_4, 5, 6 should use the same logic - adjust based on your data structure
        SetWinRatio("single_4", wagers.side_bets?.single_match_3);
        SetWinRatio("single_5", wagers.side_bets?.single_match_3);
        SetWinRatio("single_6", wagers.side_bets?.single_match_3);

        // Setup specific bets
        SetWinRatio("specific_2", wagers.side_bets?.specific_2);
        SetWinRatio("specific_3", wagers.side_bets?.specific_3);

        // Setup sum bets (sum_4 to sum_17)
        SetWinRatio("sum_4", wagers.op_bets?.sum_4);
        SetWinRatio("sum_5", wagers.op_bets?.sum_5);
        SetWinRatio("sum_6", wagers.op_bets?.sum_6);
        SetWinRatio("sum_7", wagers.op_bets?.sum_7);
        SetWinRatio("sum_8", wagers.op_bets?.sum_8);
        SetWinRatio("sum_9", wagers.op_bets?.sum_9);
        SetWinRatio("sum_10", wagers.op_bets?.sum_10);
        SetWinRatio("sum_11", wagers.op_bets?.sum_11);
        SetWinRatio("sum_12", wagers.op_bets?.sum_12);
        SetWinRatio("sum_13", wagers.op_bets?.sum_13);
        SetWinRatio("sum_14", wagers.op_bets?.sum_14);
        SetWinRatio("sum_15", wagers.op_bets?.sum_15);
        SetWinRatio("sum_16", wagers.op_bets?.sum_16);
        SetWinRatio("sum_17", wagers.op_bets?.sum_17);
    }

    private void SetWinRatio(string betOption, BetWager wagerInfo)
    {
        if (wagerInfo == null || wagerInfo.payout == null || wagerInfo.payout.Count < 2) return;

        string ratioText = $"1 : {wagerInfo.payout[1]:F2}";

        if (betAreaMap.ContainsKey(betOption))
        {
            betAreaMap[betOption].SetWinRatio(ratioText);
        }
    }
    #endregion

    #region Private Methods - Bet Actions
    private void OnTripleDiceClicked(int diceNum)
    {
        if (!isBettingEnabled) return;

        OnBetAreaClicked($"single_{diceNum}");
    }

    private void OnBetAreaClicked(string betOption)
    {
        if (!isBettingEnabled) return;

        if (selectedChipIndex < 0 || selectedChipIndex >= currentChipValues.Count)
        {
            Debug.LogWarning("[BET] Invalid chip selection");
            return;
        }

        // Place bet
        double betAmount = currentChipValues[selectedChipIndex];
        Sprite chipSprite = GetChipSprite(betAmount);

        gameManager.PlaceBet(betOption, selectedChipIndex);

        // Update UI - Handle triple dice separately
        if (betOption.StartsWith("single_"))
        {
            if (int.TryParse(betOption.Replace("single_", ""), out int diceNum))
            {
                if (tripleDiceMap.ContainsKey(diceNum))
                {
                    tripleDiceMap[diceNum].AddMyBet(betAmount, chipSprite, playerChipStackPrefab);
                }
            }
        }
        else if (betAreaMap.ContainsKey(betOption))
        {
            betAreaMap[betOption].AddMyBet(betAmount, chipSprite, playerChipStackPrefab);
        }

        currentTotalBet += betAmount;
        UpdateTotalBetDisplay();
        ShowBetActionsPanel();
    }

    private void OnUndoClicked()
    {
        gameManager.UndoBet();
    }

    private void OnCancelClicked()
    {
        gameManager.CancelAllBets();
        ClearAllBets();
    }

    private void OnDoubleClicked()
    {
        gameManager.DoubleBet();
    }

    private void OnRepeatClicked()
    {
        gameManager.RepeatBet();
        ShowBetActionsPanel();
    }

    private void UpdateTotalBetDisplay()
    {
        if (TotalBet_Text) TotalBet_Text.text = currentTotalBet.ToString("F2");

        if (TotalBetPanel)
        {
            TotalBetPanel.SetActive(currentTotalBet > 0);
        }
    }

    private void ShowRepeatPanel()
    {
        if (RepeatPanel) RepeatPanel.SetActive(true);
        if (BetActionsPanel) BetActionsPanel.SetActive(false);
    }

    private void ShowBetActionsPanel()
    {
        if (RepeatPanel) RepeatPanel.SetActive(false);
        if (BetActionsPanel) BetActionsPanel.SetActive(true);
    }

    private void HideBetPanels()
    {
        if (RepeatPanel) RepeatPanel.SetActive(false);
        if (BetActionsPanel) BetActionsPanel.SetActive(false);
    }
    #endregion
}

#region Bet Area View Component
[System.Serializable]
public class BetAreaView
{
    [Header("Components")]
    public Button Button;
    public GameObject WinImage; // Win highlight image
    public TMP_Text WinRatio_Text; // Win ratio text (e.g., "1 : 0.95")

    [Header("Player Bet Display")]
    public Transform PlayerBetContainer; // Container to spawn chip stack prefab
 

    [Header("Other Players Display")]
    public Transform OtherBetsContainer; // Container for opponent chip
   

    private double myBetAmount = 0;
    private double otherBetsAmount = 0;
    private List<GameObject> myChipObjects = new List<GameObject>(); // Spawned chip objects
    private GameObject opponentChipObject = null; // Single gray chip

    /// <summary>
    /// Set the win ratio text for this bet area
    /// </summary>
    internal void SetWinRatio(string ratioText)
    {
        if (WinRatio_Text) WinRatio_Text.text = ratioText;
    }

    /// <summary>
    /// Add player's bet with chip visualization
    /// </summary>
    internal void AddMyBet(double amount, Sprite chipSprite, GameObject chipStackPrefab)
    {
        myBetAmount += amount;

        // Spawn chip object
        if (PlayerBetContainer != null && chipStackPrefab != null)
        {
            GameObject chipObj = GameObject.Instantiate(chipStackPrefab, PlayerBetContainer);

            // Setup chip sprite and text
            Chip chipComponent = chipObj.GetComponent<Chip>();
            if (chipComponent != null)
            {
                chipComponent.SetSprite(chipSprite);
                chipComponent.SetAmount(amount.ToString("F0"));
            }

            myChipObjects.Add(chipObj);

            // Show only up to 5-6 chips max
            if (myChipObjects.Count > 6)
            {
                GameObject oldChip = myChipObjects[0];
                myChipObjects.RemoveAt(0);
                GameObject.Destroy(oldChip);
            }
        }

        UpdateMyBetDisplay();
    }

    internal void ClearMyBet()
    {
        myBetAmount = 0;

        // Destroy all spawned chips
        foreach (var chip in myChipObjects)
        {
            if (chip != null) GameObject.Destroy(chip);
        }
        myChipObjects.Clear();

        UpdateMyBetDisplay();
    }

    /// <summary>
    /// Add opponent's bet with gray chip
    /// </summary>
    internal void AddOtherPlayerBet(double amount, Sprite grayChipSprite)
    {
        otherBetsAmount += amount;

        // Create gray chip if doesn't exist
        if (opponentChipObject == null && OtherBetsContainer != null && grayChipSprite != null)
        {
            // Create simple chip object
            GameObject chipObj = new GameObject("OpponentChip");
            chipObj.transform.SetParent(OtherBetsContainer);
            chipObj.transform.localScale = Vector3.one;

            Image chipImage = chipObj.AddComponent<Image>();
            chipImage.sprite = grayChipSprite;

            opponentChipObject = chipObj;
        }

        UpdateOtherBetsDisplay();
    }

    internal void RemoveOtherPlayerBet(double amount)
    {
        otherBetsAmount = Mathf.Max(0, (float)(otherBetsAmount - amount));
        UpdateOtherBetsDisplay();
    }

    internal void ClearOtherBets()
    {
        otherBetsAmount = 0;

        if (opponentChipObject != null)
        {
            GameObject.Destroy(opponentChipObject);
            opponentChipObject = null;
        }

        UpdateOtherBetsDisplay();
    }

    internal void ShowWinHighlight()
    {
        if (WinImage) WinImage.SetActive(true);
        
    }

    internal void ShowLoseHighlight()
    {
        if (WinImage) WinImage.SetActive(false);
  
    }

    internal void HideHighlight()
    {
        if (WinImage) WinImage.SetActive(false);

    }

    private void UpdateMyBetDisplay()
    {

        if (PlayerBetContainer)
        {
            PlayerBetContainer.gameObject.SetActive(myBetAmount > 0);
        }
    }

    private void UpdateOtherBetsDisplay()
    {
        bool hasOtherBets = otherBetsAmount > 0;

        if (OtherBetsContainer)
        {
            OtherBetsContainer.gameObject.SetActive(hasOtherBets);
        }

        if (opponentChipObject)
        {
            opponentChipObject.SetActive(hasOtherBets);
        }
    }
}
#endregion

#region Triple Dice Area Component
[System.Serializable]
public class TripleDiceArea
{
    [Header("Components")]
    public Button Button;
    public GameObject WinImage;

    [Header("Player Bet Display")]
    public Transform PlayerBetContainer;
    

    [Header("Other Players Display")]
    public Transform OtherBetsContainer;

    private double myBetAmount = 0;
    private double otherBetsAmount = 0;
    private List<GameObject> myChipObjects = new List<GameObject>();
    private GameObject opponentChipObject = null;


    internal void AddMyBet(double amount, Sprite chipSprite, GameObject chipStackPrefab)
    {
        myBetAmount += amount;

        if (PlayerBetContainer != null && chipStackPrefab != null)
        {
            GameObject chipObj = GameObject.Instantiate(chipStackPrefab, PlayerBetContainer);

            Chip chipComponent = chipObj.GetComponent<Chip>();
            if (chipComponent != null)
            {
                chipComponent.SetSprite(chipSprite);
                chipComponent.SetAmount(amount.ToString("F0"));
            }

            myChipObjects.Add(chipObj);

            if (myChipObjects.Count > 6)
            {
                GameObject oldChip = myChipObjects[0];
                myChipObjects.RemoveAt(0);
                GameObject.Destroy(oldChip);
            }
        }

        UpdateMyBetDisplay();
    }

    internal void ClearMyBet()
    {
        myBetAmount = 0;

        foreach (var chip in myChipObjects)
        {
            if (chip != null) GameObject.Destroy(chip);
        }
        myChipObjects.Clear();

        UpdateMyBetDisplay();
    }

    internal void AddOtherPlayerBet(double amount, Sprite grayChipSprite)
    {
        otherBetsAmount += amount;

        if (opponentChipObject == null && OtherBetsContainer != null && grayChipSprite != null)
        {
            GameObject chipObj = new GameObject("OpponentChip");
            chipObj.transform.SetParent(OtherBetsContainer);
            chipObj.transform.localScale = Vector3.one;

            Image chipImage = chipObj.AddComponent<Image>();
            chipImage.sprite = grayChipSprite;

            opponentChipObject = chipObj;
        }

        UpdateOtherBetsDisplay();
    }

    internal void RemoveOtherPlayerBet(double amount)
    {
        otherBetsAmount = Mathf.Max(0, (float)(otherBetsAmount - amount));
        UpdateOtherBetsDisplay();
    }

    internal void ClearOtherBets()
    {
        otherBetsAmount = 0;

        if (opponentChipObject != null)
        {
            GameObject.Destroy(opponentChipObject);
            opponentChipObject = null;
        }

        UpdateOtherBetsDisplay();
    }

    internal void ShowWinHighlight()
    {
        if (WinImage) WinImage.SetActive(true);

    }

    internal void ShowLoseHighlight()
    {
        if (WinImage) WinImage.SetActive(false);
       
    }

    internal void HideHighlight()
    {
        if (WinImage) WinImage.SetActive(false);

    }

    private void UpdateMyBetDisplay()
    {

        if (PlayerBetContainer)
        {
            PlayerBetContainer.gameObject.SetActive(myBetAmount > 0);
        }
    }

    private void UpdateOtherBetsDisplay()
    {
        bool hasOtherBets = otherBetsAmount > 0;

        if (OtherBetsContainer)
        {
            OtherBetsContainer.gameObject.SetActive(hasOtherBets);
        }

    
        if (opponentChipObject)
        {
            opponentChipObject.SetActive(hasOtherBets);
        }
    }
}
#endregion