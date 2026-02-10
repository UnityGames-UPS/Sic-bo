using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Wrapper class to hold min and max TextMeshProUGUI references for each bet option
/// </summary>
[System.Serializable]
public class BetLimitTexts
{
    public TextMeshProUGUI minText;
    public TextMeshProUGUI maxText;
}

public class BetLimitManager : MonoBehaviour
{
    #region Serialized References
    [Header("Panel References")]
    [SerializeField] private GameObject betLimitPanel;
    [SerializeField] private GameObject mainArea;
    [SerializeField] private Button openPanelButton;
    [SerializeField] private Button closePanelButton;

    [Header("Room Selection Buttons")]
    [SerializeField] private Button casualButton;
    [SerializeField] private Button noviceButton;
    [SerializeField] private Button expertButton;
    [SerializeField] private Button highRollerButton;

    [Header("Room Button Selection Indicators")]
    [SerializeField] private GameObject casualSelectionIndicator;
    [SerializeField] private GameObject noviceSelectionIndicator;
    [SerializeField] private GameObject expertSelectionIndicator;
    [SerializeField] private GameObject highRollerSelectionIndicator;

    [Header("Room Min/Max Bet Texts")]
    [SerializeField] private TextMeshProUGUI casualMinBetText;
    [SerializeField] private TextMeshProUGUI casualMaxBetText;
    [SerializeField] private TextMeshProUGUI noviceMinBetText;
    [SerializeField] private TextMeshProUGUI noviceMaxBetText;
    [SerializeField] private TextMeshProUGUI expertMinBetText;
    [SerializeField] private TextMeshProUGUI expertMaxBetText;
    [SerializeField] private TextMeshProUGUI highRollerMinBetText;
    [SerializeField] private TextMeshProUGUI highRollerMaxBetText;

    [Header("Bet Option Limit Texts - Grouped Lists")]
    [Tooltip("Main Bets: [0]=small, [1]=big, [2]=odd, [3]=even")]
    [SerializeField] private List<BetLimitTexts> mainBetsTexts = new List<BetLimitTexts>(4);

    [Tooltip("Specific 3 (Triple Dice): [0]=specific_3_1, [1]=specific_3_2, ..., [5]=specific_3_6")]
    [SerializeField] private List<BetLimitTexts> specific3Texts = new List<BetLimitTexts>(6);

    [Tooltip("Single Numbers: [0]=single_1, [1]=single_2, ..., [5]=single_6")]
    [SerializeField] private List<BetLimitTexts> singleNumberTexts = new List<BetLimitTexts>(6);

    [Tooltip("Sum Bets: [0]=sum_4, [1]=sum_5, ..., [13]=sum_17")]
    [SerializeField] private List<BetLimitTexts> sumBetsTexts = new List<BetLimitTexts>(14);

    [Header("Animation Settings")]
    [SerializeField] private float popupDuration = 0.3f;
    [SerializeField] private AnimationCurve popupCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    #endregion

    #region Private Variables
    private Wagers wagersData;
    private Bets betsData;
    private List<string> betOptions;
    private string currentSelectedRoom = "casual";
    private string playerCurrentRoom;
    private Coroutine popupCoroutine;
    #endregion

    #region Initialization
    private void Start()
    {
        SetupButtonListeners();
        betLimitPanel.SetActive(false);
    }

    private void SetupButtonListeners()
    {
        if (openPanelButton != null)
            openPanelButton.onClick.AddListener(OpenPanel);

        if (closePanelButton != null)
            closePanelButton.onClick.AddListener(ClosePanel);

        if (casualButton != null)
            casualButton.onClick.AddListener(() => OnRoomButtonClicked("casual"));

        if (noviceButton != null)
            noviceButton.onClick.AddListener(() => OnRoomButtonClicked("novice"));

        if (expertButton != null)
            expertButton.onClick.AddListener(() => OnRoomButtonClicked("expert"));

        if (highRollerButton != null)
            highRollerButton.onClick.AddListener(() => OnRoomButtonClicked("high_roller"));
    }

    public void Initialize(Wagers wagers, Bets bets, string currentRoom, List<string> receivedBetOptions)
    {
        wagersData = wagers;
        betsData = bets;
        betOptions = receivedBetOptions;
        playerCurrentRoom = currentRoom;
        currentSelectedRoom = currentRoom;

        UpdateRoomButtonMinMaxValues();
    }
    #endregion

    #region Panel Control
    public void OpenPanel()
    {
        if (wagersData == null || betsData == null)
        {
            Debug.LogWarning("BetLimitManager: Wagers or Bets data not initialized!");
            return;
        }

        betLimitPanel.SetActive(true);

        
        currentSelectedRoom = playerCurrentRoom;
        UpdateRoomSelection();
        UpdateBetLimitDisplays();

        // Play popup animation
        if (popupCoroutine != null)
            StopCoroutine(popupCoroutine);
        popupCoroutine = StartCoroutine(PlayPopupAnimation(true));
    }

    public void ClosePanel()
    {
        if (popupCoroutine != null)
            StopCoroutine(popupCoroutine);
        popupCoroutine = StartCoroutine(PlayPopupAnimation(false));
    }

    private IEnumerator PlayPopupAnimation(bool isOpening)
    {
        if (mainArea == null) yield break;

        float startScale = isOpening ? 0f : 1f;
        float endScale = isOpening ? 1f : 0f;
        float elapsed = 0f;

        mainArea.transform.localScale = Vector3.one * startScale;

        while (elapsed < popupDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / popupDuration;
            float curveValue = popupCurve.Evaluate(progress);
            float scale = Mathf.Lerp(startScale, endScale, curveValue);

            mainArea.transform.localScale = Vector3.one * scale;

            yield return null;
        }

        mainArea.transform.localScale = Vector3.one * endScale;

        if (!isOpening)
        {
            betLimitPanel.SetActive(false);
        }
    }
    #endregion

    #region Room Selection
    private void OnRoomButtonClicked(string roomName)
    {
        if (currentSelectedRoom == roomName) return;

        currentSelectedRoom = roomName;
        UpdateRoomSelection();
        UpdateBetLimitDisplays();

        // Rebuild canvas to ensure proper layout
        Canvas.ForceUpdateCanvases();
    }

    private void UpdateRoomSelection()
    {
        // Disable all selection indicators
        if (casualSelectionIndicator != null)
            casualSelectionIndicator.SetActive(false);
        if (noviceSelectionIndicator != null)
            noviceSelectionIndicator.SetActive(false);
        if (expertSelectionIndicator != null)
            expertSelectionIndicator.SetActive(false);
        if (highRollerSelectionIndicator != null)
            highRollerSelectionIndicator.SetActive(false);

        // Enable the selected room's indicator
        switch (currentSelectedRoom)
        {
            case "casual":
                if (casualSelectionIndicator != null)
                    casualSelectionIndicator.SetActive(true);
                break;
            case "novice":
                if (noviceSelectionIndicator != null)
                    noviceSelectionIndicator.SetActive(true);
                break;
            case "expert":
                if (expertSelectionIndicator != null)
                    expertSelectionIndicator.SetActive(true);
                break;
            case "high_roller":
                if (highRollerSelectionIndicator != null)
                    highRollerSelectionIndicator.SetActive(true);
                break;
        }
    }
    #endregion

    #region Display Updates

    private void UpdateRoomButtonMinMaxValues()
    {
        if (betsData == null || wagersData == null) return;  // CHANGED: Added wagersData null check

        // Casual - CHANGED: Added "casual" parameter
        UpdateRoomButtonText(casualMinBetText, casualMaxBetText, betsData.casual, "casual");

        // Novice - CHANGED: Added "novice" parameter
        UpdateRoomButtonText(noviceMinBetText, noviceMaxBetText, ConvertToDoubleList(betsData.novice), "novice");

        // Expert - CHANGED: Added "expert" parameter
        UpdateRoomButtonText(expertMinBetText, expertMaxBetText, ConvertToDoubleList(betsData.expert), "expert");

        // High Roller - CHANGED: Added "high_roller" parameter
        UpdateRoomButtonText(highRollerMinBetText, highRollerMaxBetText, ConvertToDoubleList(betsData.high_roller), "high_roller");
    }

    private void UpdateRoomButtonText(TextMeshProUGUI minText, TextMeshProUGUI maxText, List<double> chipValues, string roomName)  // CHANGED: Added roomName parameter
    {
        if (chipValues == null || chipValues.Count == 0) return;

        // Min bet is the smallest chip value
        double minBet = chipValues[0];

        // CHANGED: Max bet is now calculated from wagers data, not from chip values
        double maxBet = GetHighestMaxBetLimitForRoom(roomName);

        if (minText != null)
            minText.text = $"MIN: {FormatBetValue(minBet)}";

        if (maxText != null)
            maxText.text = $"MAX: {FormatBetValue(maxBet)}";
    }

    // NEW METHOD: Get the highest max_bet_limit across all bet options for a given room
    /// <summary>
    /// Get the highest max_bet_limit across all bet options for a given room.
    /// This represents the maximum amount a player can bet on ANY single bet area.
    /// According to the data, all bet options have the same max_bet_limit per room:
    /// Casual: 6, Novice: 13, Expert: 33, High Roller: 66
    /// </summary>
    private double GetHighestMaxBetLimitForRoom(string roomName)
    {
        if (wagersData == null) return 0;

        double highestMax = 0;

        // Check Main Bets
        if (wagersData.main_bets != null)
        {
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.main_bets.small, roomName));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.main_bets.big, roomName));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.main_bets.odd, roomName));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.main_bets.even, roomName));
        }

        // Check Side Bets
        if (wagersData.side_bets != null)
        {
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.side_bets.single_match_1, roomName));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.side_bets.single_match_2, roomName));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.side_bets.single_match_3, roomName));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.side_bets.specific_2, roomName));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.side_bets.specific_3, roomName));
        }

        // Check Op Bets (Sum 4-17)
        if (wagersData.op_bets != null)
        {
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.op_bets.sum_4, roomName));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.op_bets.sum_5, roomName));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.op_bets.sum_6, roomName));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.op_bets.sum_7, roomName));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.op_bets.sum_8, roomName));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.op_bets.sum_9, roomName));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.op_bets.sum_10, roomName));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.op_bets.sum_11, roomName));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.op_bets.sum_12, roomName));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.op_bets.sum_13, roomName));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.op_bets.sum_14, roomName));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.op_bets.sum_15, roomName));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.op_bets.sum_16, roomName));
            highestMax = Mathf.Max((float)highestMax, (float)GetMaxBetFromWager(wagersData.op_bets.sum_17, roomName));
        }

        return highestMax;
    }

    // NEW METHOD: Helper to get max bet from a specific wager for a specific room
    /// <summary>
    /// Get max bet limit from a specific wager for a specific room
    /// </summary>
    private double GetMaxBetFromWager(BetWager wager, string roomName)
    {
        if (wager == null) return 0;
        return wager.GetMaxBet(roomName);
    }


    private void UpdateBetLimitDisplays()
    {
        if (wagersData == null || betOptions == null) return;

        // Process each bet option from init data
        foreach (string betOption in betOptions)
        {
            // Skip specific_2 - we don't display it
            if (betOption == "specific_2")
                continue;

            // Get the wager data for this bet option
            BetWager wager = GetWagerForBetOption(betOption);
            if (wager == null)
                continue;

            // Update the text displays based on bet option type
            UpdateBetOptionDisplay(betOption, wager);
        }
    }

    private void UpdateBetOptionDisplay(string betOption, BetWager wager)
    {
        double minBet = GetMinBetForRoom(currentSelectedRoom);
        double maxBet = wager.GetMaxBet(currentSelectedRoom);

        BetLimitTexts texts = GetTextsForBetOption(betOption);
        if (texts == null)
        {
            Debug.LogWarning($"BetLimitManager: No text mapping found for bet option: {betOption}");
            return;
        }

        // Update min and max texts
        if (texts.minText != null)
            texts.minText.text = FormatBetValue(minBet);

        if (texts.maxText != null)
            texts.maxText.text = FormatBetValue(maxBet);
    }

    /// <summary>
    /// Get the BetLimitTexts for a given bet option from the appropriate grouped list
    /// </summary>
    private BetLimitTexts GetTextsForBetOption(string betOption)
    {
        // Main Bets: small, big, odd, even
        switch (betOption)
        {
            case "small":
                return GetFromList(mainBetsTexts, 0);
            case "big":
                return GetFromList(mainBetsTexts, 1);
            case "odd":
                return GetFromList(mainBetsTexts, 2);
            case "even":
                return GetFromList(mainBetsTexts, 3);
        }

        // Specific 3 (Triple Dice): specific_3_1 through specific_3_6
        if (betOption.StartsWith("specific_3_"))
        {
            string numberStr = betOption.Substring(11); // Extract number after "specific_3_"
            if (int.TryParse(numberStr, out int number) && number >= 1 && number <= 6)
            {
                return GetFromList(specific3Texts, number - 1);
            }
        }

        // Single Numbers: single_1 through single_6
        if (betOption.StartsWith("single_"))
        {
            string numberStr = betOption.Substring(7); // Extract number after "single_"
            if (int.TryParse(numberStr, out int number) && number >= 1 && number <= 6)
            {
                return GetFromList(singleNumberTexts, number - 1);
            }
        }

        // Sum Bets: sum_4 through sum_17
        if (betOption.StartsWith("sum_"))
        {
            string numberStr = betOption.Substring(4); // Extract number after "sum_"
            if (int.TryParse(numberStr, out int sum) && sum >= 4 && sum <= 17)
            {
                return GetFromList(sumBetsTexts, sum - 4);
            }
        }

        return null;
    }

    /// <summary>
    /// Safely get element from list with bounds checking
    /// </summary>
    private BetLimitTexts GetFromList(List<BetLimitTexts> list, int index)
    {
        if (list == null || index < 0 || index >= list.Count)
            return null;

        return list[index];
    }

    private BetWager GetWagerForBetOption(string betOption)
    {
        if (wagersData == null)
            return null;

        // Main Bets
        if (betOption == "small") return wagersData.main_bets?.small;
        if (betOption == "big") return wagersData.main_bets?.big;
        if (betOption == "odd") return wagersData.main_bets?.odd;
        if (betOption == "even") return wagersData.main_bets?.even;

        // Single Match (side_bets) - single_1 through single_6
        if (betOption == "single_1") return wagersData.side_bets?.single_match_1;
        if (betOption == "single_2") return wagersData.side_bets?.single_match_2;
        if (betOption == "single_3") return wagersData.side_bets?.single_match_3;
        if (betOption == "single_4") return wagersData.side_bets?.single_match_1; // Use single_match_1 as fallback
        if (betOption == "single_5") return wagersData.side_bets?.single_match_2; // Use single_match_2 as fallback
        if (betOption == "single_6") return wagersData.side_bets?.single_match_3; // Use single_match_3 as fallback

        // Specific 2 (not displayed, but included for completeness)
        if (betOption == "specific_2") return wagersData.side_bets?.specific_2;

        // Specific 3 - All variants use the same wager data
        if (betOption.StartsWith("specific_3_"))
            return wagersData.side_bets?.specific_3;

        // Sum Bets (op_bets)
        if (betOption == "sum_4") return wagersData.op_bets?.sum_4;
        if (betOption == "sum_5") return wagersData.op_bets?.sum_5;
        if (betOption == "sum_6") return wagersData.op_bets?.sum_6;
        if (betOption == "sum_7") return wagersData.op_bets?.sum_7;
        if (betOption == "sum_8") return wagersData.op_bets?.sum_8;
        if (betOption == "sum_9") return wagersData.op_bets?.sum_9;
        if (betOption == "sum_10") return wagersData.op_bets?.sum_10;
        if (betOption == "sum_11") return wagersData.op_bets?.sum_11;
        if (betOption == "sum_12") return wagersData.op_bets?.sum_12;
        if (betOption == "sum_13") return wagersData.op_bets?.sum_13;
        if (betOption == "sum_14") return wagersData.op_bets?.sum_14;
        if (betOption == "sum_15") return wagersData.op_bets?.sum_15;
        if (betOption == "sum_16") return wagersData.op_bets?.sum_16;
        if (betOption == "sum_17") return wagersData.op_bets?.sum_17;

        return null;
    }

    private double GetMinBetForRoom(string room)
    {
        if (betsData == null) return 0;

        return room switch
        {
            "casual" => betsData.casual != null && betsData.casual.Count > 0 ? betsData.casual[0] : 0,
            "novice" => betsData.novice != null && betsData.novice.Count > 0 ? betsData.novice[0] : 0,
            "expert" => betsData.expert != null && betsData.expert.Count > 0 ? betsData.expert[0] : 0,
            "high_roller" => betsData.high_roller != null && betsData.high_roller.Count > 0 ? betsData.high_roller[0] : 0,
            _ => 0
        };
    }
    #endregion

    #region Helper Methods
    private List<double> ConvertToDoubleList(List<int> intList)
    {
        List<double> result = new List<double>();
        if (intList != null)
        {
            foreach (int value in intList)
            {
                result.Add((double)value);
            }
        }
        return result;
    }

    private string FormatBetValue(double value)
    {
        // Format with 2 decimal places if needed, otherwise show as integer
        if (value % 1 == 0)
            return value.ToString("F0");
        else
            return value.ToString("F2");
    }
    #endregion

    #region Public Update Methods
    /// <summary>
    /// Call this when player changes room to update the current room reference
    /// </summary>
    public void UpdatePlayerCurrentRoom(string newRoom)
    {
        playerCurrentRoom = newRoom;
    }

    /// <summary>
    /// Call this to refresh data if wagers or bets are updated
    /// </summary>
    public void RefreshData(Wagers wagers, Bets bets, List<string> receivedBetOptions)
    {
        wagersData = wagers;
        betsData = bets;
        betOptions = receivedBetOptions;

        UpdateRoomButtonMinMaxValues();

        if (betLimitPanel.activeSelf)
        {
            UpdateBetLimitDisplays();
        }
    }
    #endregion
}