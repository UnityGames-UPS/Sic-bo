using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

internal class ChipWinAnimationController : MonoBehaviour
{
    #region Serialized Fields
    [Header("References")]
    [SerializeField] private RectTransform dealerSpawnPoint;
    [SerializeField] private GameObject chipPrefab;
    [SerializeField] private Sprite[] chipSprites;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private RectTransform playerNameTarget;
    [SerializeField] private BetController betController;
    [SerializeField] private GameManager gameManager;

    [Header("Pool")]
    [SerializeField] private int dealerPoolSize = 25;
    [SerializeField] private float dealerScatterX = 28f;
    [SerializeField] private float dealerScatterY = 18f;

    [Header("Dealer to Bet Area")]
    [SerializeField] private float dealerToBetDuration = 0.50f;
    [SerializeField] private float chipStaggerDelay = 0.055f;
    [SerializeField] private float betAreaScatterX = 11f;
    [SerializeField] private float betAreaScatterY = 9f;

    [Header("Bet Area to Player")]
    [SerializeField] private float betToPlayerDuration = 0.60f;
    [SerializeField] private float cashoutStagger = 0.04f;
    [SerializeField] private float arcHeight = 110f;

    [Header("Chip Visual")]
    [SerializeField] private float chipWorkingScale = 1.0f;
    [Header("Win Animation Settings")]
    [SerializeField] private float animationStartPercent = 0.6f;
    [SerializeField] private bool enableWinAnimations = true;

    [Header("Chip Count Settings")]
    [SerializeField] private int minChipsPerWin = 1;
    [SerializeField] private int maxChipsPerWin = 8;
    [SerializeField] private double minWinForExtraChips = 1.0; 
    #endregion

    #region Private Fields
    private readonly List<RectTransform> dealerPool = new List<RectTransform>();
    private readonly List<RectTransform> activeWinChips = new List<RectTransform>();
    private readonly List<RectTransform> stakeReturnChips = new List<RectTransform>();
    private bool isAnimating;
    private Coroutine winCoroutine;
    private Coroutine cashoutCoroutine;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
        PreSpawnDealerPool();
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        foreach (var c in dealerPool) { if (c) c.DOKill(); }
        foreach (var c in activeWinChips) { if (c) c.DOKill(); }
    }
    #endregion

    #region Internal API
    internal void PlayDiceResultAnimation(List<WinAreaData> winAreas, DiceResultData diceResult)
    {
        if (isAnimating || winAreas == null || winAreas.Count == 0 || diceResult == null) return;
        if (winCoroutine != null) StopCoroutine(winCoroutine);
        var recalculatedWinAreas = RecalculateWinAmounts(winAreas, diceResult);
        winCoroutine = StartCoroutine(CR_DealerToBetAreas(recalculatedWinAreas));
    }

    internal void PlayCashoutAnimation()
    {
        if (cashoutCoroutine != null) StopCoroutine(cashoutCoroutine);
        cashoutCoroutine = StartCoroutine(CR_Cashout());
    }

    internal void ResetAll()
    {
        if (winCoroutine != null) StopCoroutine(winCoroutine);
        if (cashoutCoroutine != null) StopCoroutine(cashoutCoroutine);
        winCoroutine = cashoutCoroutine = null;

        foreach (var chip in dealerPool)
        {
            if (chip == null) continue;
            chip.DOKill();
            if (chip.parent != dealerSpawnPoint) chip.SetParent(dealerSpawnPoint, worldPositionStays: false);
            chip.localPosition = new Vector3(
                Random.Range(-dealerScatterX, dealerScatterX),
                Random.Range(-dealerScatterY, dealerScatterY), 0f);
            chip.localScale = Vector3.zero;
            chip.gameObject.SetActive(false);
        }

        activeWinChips.Clear();
        stakeReturnChips.Clear();
        isAnimating = false;
    }
    #endregion

    #region Win Calculation
    private List<WinAreaData> RecalculateWinAmounts(List<WinAreaData> winAreas, DiceResultData diceResult)
    {
        var recalculated = new List<WinAreaData>();

        foreach (var area in winAreas)
        {
            if (area.betAmount <= 0) continue;

            double actualWinAmount = CalculateActualWin(area.betOption, area.betAmount, diceResult);

            if (actualWinAmount > 0)
            {
                recalculated.Add(new WinAreaData
                {
                    betOption = area.betOption,
                    betAreaTarget = area.betAreaTarget,
                    betAmount = area.betAmount,
                    winAmount = actualWinAmount,
                    winRatio = actualWinAmount / area.betAmount
                });
            }
        }

        return recalculated;
    }

    private double CalculateActualWin(string betOption, double betAmount, DiceResultData diceResult)
    {
        if (gameManager == null) return 0;

        if (betOption.StartsWith("single_"))
        {
            int diceNumber = GetDiceNumberFromBetOption(betOption);
            if (diceNumber == -1) return 0;

            int matchCount = CountDiceMatches(diceNumber, diceResult);
            return CalculateSingleDiceWin(betAmount, matchCount);
        }

        if (betOption.StartsWith("specific_3_"))
        {
            int diceNumber = GetDiceNumberFromBetOption(betOption);
            if (diceNumber == -1) return 0;

            int matchCount = CountDiceMatches(diceNumber, diceResult);
            return CalculateSpecificTripleWin(betAmount, matchCount);
        }

        var wager = GetWagerForBetOption(betOption);
        if (wager != null)
        {
            return wager.CalculateWin(betAmount);
        }

        return 0;
    }

    private int GetDiceNumberFromBetOption(string betOption)
    {
        string[] parts = betOption.Split('_');
        if (parts.Length > 0)
        {
            string lastPart = parts[parts.Length - 1];
            if (int.TryParse(lastPart, out int diceNumber) && diceNumber >= 1 && diceNumber <= 6)
            {
                return diceNumber;
            }
        }
        return -1;
    }

    private int CountDiceMatches(int targetDice, DiceResultData diceResult)
    {
        int count = 0;
        if (diceResult.dice1 == targetDice) count++;
        if (diceResult.dice2 == targetDice) count++;
        if (diceResult.dice3 == targetDice) count++;
        return count;
    }

    private double CalculateSingleDiceWin(double betAmount, int matchCount)
    {
        if (gameManager?.CurrentWagers?.side_bets == null) return 0;

        switch (matchCount)
        {
            case 3:
   
                if (gameManager.CurrentWagers.side_bets.single_match_3 != null)
                    return gameManager.CurrentWagers.side_bets.single_match_3.CalculateWin(betAmount);
                break;
            case 2:
   
                if (gameManager.CurrentWagers.side_bets.single_match_2 != null)
                    return gameManager.CurrentWagers.side_bets.single_match_2.CalculateWin(betAmount);
                break;
            case 1:
     
                if (gameManager.CurrentWagers.side_bets.single_match_1 != null)
                    return gameManager.CurrentWagers.side_bets.single_match_1.CalculateWin(betAmount);
                break;
        }

        return 0;
    }

    private double CalculateSpecificTripleWin(double betAmount, int matchCount)
    {
        if (gameManager?.CurrentWagers?.side_bets == null) return 0;

        switch (matchCount)
        {
            case 3:
        
                if (gameManager.CurrentWagers.side_bets.specific_3 != null)
                    return gameManager.CurrentWagers.side_bets.specific_3.CalculateWin(betAmount);
                break;
            case 2:
          
                if (gameManager.CurrentWagers.side_bets.specific_2 != null)
                    return gameManager.CurrentWagers.side_bets.specific_2.CalculateWin(betAmount);
                break;
        }

        return 0;
    }

    private BetWager GetWagerForBetOption(string betOption)
    {
        if (gameManager?.CurrentWagers == null) return null;

        switch (betOption)
        {
            case "small": return gameManager.CurrentWagers.main_bets?.small;
            case "big": return gameManager.CurrentWagers.main_bets?.big;
            case "odd": return gameManager.CurrentWagers.main_bets?.odd;
            case "even": return gameManager.CurrentWagers.main_bets?.even;
        }

        if (betOption.StartsWith("sum_"))
        {
            switch (betOption)
            {
                case "sum_4": return gameManager.CurrentWagers.op_bets?.sum_4;
                case "sum_5": return gameManager.CurrentWagers.op_bets?.sum_5;
                case "sum_6": return gameManager.CurrentWagers.op_bets?.sum_6;
                case "sum_7": return gameManager.CurrentWagers.op_bets?.sum_7;
                case "sum_8": return gameManager.CurrentWagers.op_bets?.sum_8;
                case "sum_9": return gameManager.CurrentWagers.op_bets?.sum_9;
                case "sum_10": return gameManager.CurrentWagers.op_bets?.sum_10;
                case "sum_11": return gameManager.CurrentWagers.op_bets?.sum_11;
                case "sum_12": return gameManager.CurrentWagers.op_bets?.sum_12;
                case "sum_13": return gameManager.CurrentWagers.op_bets?.sum_13;
                case "sum_14": return gameManager.CurrentWagers.op_bets?.sum_14;
                case "sum_15": return gameManager.CurrentWagers.op_bets?.sum_15;
                case "sum_16": return gameManager.CurrentWagers.op_bets?.sum_16;
                case "sum_17": return gameManager.CurrentWagers.op_bets?.sum_17;
            }
        }

        return null;
    }
    #endregion

    #region Pool
    private void PreSpawnDealerPool()
    {
        if (chipPrefab == null || dealerSpawnPoint == null) return;

        for (int i = 0; i < dealerPoolSize; i++)
        {
            GameObject go = Instantiate(chipPrefab, dealerSpawnPoint);
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) { Destroy(go); continue; }

            rt.localPosition = new Vector3(
                Random.Range(-dealerScatterX, dealerScatterX),
                Random.Range(-dealerScatterY, dealerScatterY), 0f);
            rt.localScale = Vector3.zero;

            if (chipSprites != null && chipSprites.Length > 0)
                SetSprite(rt, Random.Range(2, chipSprites.Length));

            go.SetActive(false);
            dealerPool.Add(rt);
        }
    }
    #endregion

    #region Dealer → Bet Areas
    private IEnumerator CR_DealerToBetAreas(List<WinAreaData> winAreas)
    {
        isAnimating = true;

        double totalWin = 0;
        foreach (var a in winAreas)
            totalWin += a.winAmount;

        var assignments = new List<(RectTransform chip, Transform parent, Vector3 localPos)>();
        int poolIdx = 0;

        foreach (var area in winAreas)
        {
            if (area.betAreaTarget == null) continue;

            
            PlayerBetComponent playerBetComp = betController?.GetPlayerBetComponent(area.betOption);
            if (playerBetComp == null) continue;

            Transform chipParent = playerBetComp.transform;

            AudioManager.Instance?.PlayChipAdd();

           
            bool shouldSpawnWinChips = area.winRatio > 1.0 && area.winRatio >= minWinForExtraChips;
            if (shouldSpawnWinChips)
            {
                int chipCount = CalculateChipCount(area);
                for (int i = 0; i < chipCount && poolIdx < dealerPool.Count; i++, poolIdx++)
                {
                    RectTransform chip = dealerPool[poolIdx];
                    if (chip == null) continue;

                    SetSprite(chip, SpriteIndex(area.winAmount / chipCount));
                    chip.gameObject.SetActive(true);
                    chip.localPosition = new Vector3(
                        Random.Range(-dealerScatterX, dealerScatterX),
                        Random.Range(-dealerScatterY, dealerScatterY), 0f);
                    chip.localScale = Vector3.zero;
                    chip.DOScale(chipWorkingScale, 0.18f).SetEase(Ease.OutBack);

                    Vector3 localPos = new Vector3(
                        Random.Range(-betAreaScatterX, betAreaScatterX),
                        Random.Range(-betAreaScatterY, betAreaScatterY), 0f);

                    assignments.Add((chip, chipParent, localPos));
                    activeWinChips.Add(chip);
                }
            }

          
            int stakeChipCount = CalculateStakeReturnChipCount(area.winRatio, area.betAmount);
            for (int i = 0; i < stakeChipCount && poolIdx < dealerPool.Count; i++, poolIdx++)
            {
                RectTransform chip = dealerPool[poolIdx];
                if (chip == null) continue;

                double stakeReturnValue = area.winRatio * area.betAmount;
                SetSprite(chip, SpriteIndex(stakeReturnValue / Mathf.Max(1, stakeChipCount)));

               
                RectTransform parentRT = chipParent as RectTransform;
                if (parentRT == null) continue;

                chip.SetParent(chipParent, worldPositionStays: false);
                chip.SetAsFirstSibling();
                chip.localPosition = new Vector3(
                    Random.Range(-betAreaScatterX, betAreaScatterX),
                    Random.Range(-betAreaScatterY, betAreaScatterY), 0f);
                chip.localScale = Vector3.zero;
                chip.gameObject.SetActive(false); 

                stakeReturnChips.Add(chip);
            }
        }

        yield return new WaitForSeconds(0.20f);

      
        var animData = new List<(RectTransform chip, Vector2 worldTarget)>();

        foreach (var (chip, parent, localPos) in assignments)
        {
            if (chip == null || parent == null) continue;

            RectTransform parentRT = parent as RectTransform;
            if (parentRT == null) continue;

            Vector3 worldTarget = parentRT.TransformPoint(localPos);

            chip.SetParent(parent, worldPositionStays: true);
            chip.SetAsFirstSibling();

            animData.Add((chip, worldTarget));
        }

      
        if (enableWinAnimations && assignments.Count > 0)
        {
            DOVirtual.DelayedCall(
                dealerToBetDuration * animationStartPercent,
                () => TriggerAllWinCountingAnimations(winAreas));
        }


        foreach (var (chip, worldTarget) in animData)
        {
            if (chip == null) continue;

            chip.DOMove(worldTarget, dealerToBetDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    if (chip != null)
                        chip.localPosition = chip.parent.InverseTransformPoint(worldTarget);
                });

            yield return new WaitForSeconds(chipStaggerDelay);
        }

        yield return new WaitForSeconds(dealerToBetDuration);

        isAnimating = false;
        winCoroutine = null;
    }

    private int CalculateChipCount(WinAreaData area)
    {
        if (area.winAmount <= 0) return 0;

        double ratio = area.winRatio;

        if (ratio <= minWinForExtraChips) return 0; // No extra chips for small wins

        int count;

        if (ratio >= 50) count = maxChipsPerWin;
        else if (ratio >= 30) count = Mathf.Min(7, maxChipsPerWin);
        else if (ratio >= 15) count = Mathf.Min(6, maxChipsPerWin);
        else if (ratio >= 10) count = Mathf.Min(5, maxChipsPerWin);
        else if (ratio >= 5) count = Mathf.Min(4, maxChipsPerWin);
        else if (ratio >= 3) count = Mathf.Min(3, maxChipsPerWin);
        else count = Mathf.Min(2, maxChipsPerWin);

        return Mathf.Max(minChipsPerWin, count);
    }

    private int CalculateStakeReturnChipCount(double winRatio, double betAmount)
    {
        if (winRatio <= 0) return 0;

        double stakeReturnValue = winRatio * betAmount;

        if (stakeReturnValue >= 20) return 3;
        if (stakeReturnValue >= 5) return 2;
        return 1;
    }

    private void TriggerAllWinCountingAnimations(List<WinAreaData> winAreas)
    {
        if (betController == null) return;

        foreach (var winArea in winAreas)
        {
            PlayerBetComponent playerBetComp = betController.GetPlayerBetComponent(winArea.betOption);
            if (playerBetComp == null) continue;
            if (winArea.betAmount <= 0) continue;

            double ratio = winArea.winAmount / winArea.betAmount;
            playerBetComp.AnimateWinWithRatio(ratio);
        }
    }
    #endregion

    #region Bet Areas → Player (Cashout)
    private IEnumerator CR_Cashout()
    {
        if (playerNameTarget == null) yield break;

        var toSweep = new List<RectTransform>(activeWinChips);

        foreach (var chip in stakeReturnChips)
        {
            if (chip == null) continue;
            chip.gameObject.SetActive(true);
            chip.DOScale(chipWorkingScale, 0.12f).SetEase(Ease.OutBack);
            betController?.RefreshBadgesForContainer(chip.parent);

            toSweep.Add(chip);
        }
        stakeReturnChips.Clear();


        int extraNeeded = Mathf.Min(3, dealerPool.Count);
        var extraChips = new List<RectTransform>();
        foreach (var chip in dealerPool)
        {
            if (extraNeeded <= 0) break;
            if (activeWinChips.Contains(chip)) continue;

            chip.gameObject.SetActive(true);
            chip.localPosition = new Vector3(
                Random.Range(-dealerScatterX, dealerScatterX),
                Random.Range(-dealerScatterY, dealerScatterY), 0f);
            chip.localScale = Vector3.zero;
            chip.DOScale(chipWorkingScale * 0.70f, 0.14f).SetEase(Ease.OutBack);

            extraChips.Add(chip);
            toSweep.Add(chip);
            extraNeeded--;
        }

        yield return new WaitForSeconds(0.18f);


        var chipCanvasPositions = new Dictionary<RectTransform, Vector2>();
        foreach (var chip in toSweep)
        {
            if (chip == null) continue;
   
            chipCanvasPositions[chip] = GetCanvasPosition(chip);
        }

        foreach (var chip in toSweep)
        {
            if (chip == null) continue;

            if (chip.parent != targetCanvas.transform)
            {
                chip.SetParent(targetCanvas.transform, worldPositionStays: false);
                chip.SetAsLastSibling(); 
                if (chipCanvasPositions.ContainsKey(chip))
                {
                    chip.anchoredPosition = chipCanvasPositions[chip];
                }
            }
        }

        Vector2 playerCanvasPos = GetCanvasPosition(playerNameTarget);

        foreach (var chip in toSweep)
        {
            if (chip == null) continue;

            Vector2 startPos = chip.anchoredPosition;
            Vector2 midPos = Vector2.Lerp(startPos, playerCanvasPos, 0.5f)
                           + new Vector2(Random.Range(-18f, 18f), arcHeight);

            float halfDur = betToPlayerDuration * 0.45f;
            float landDur = betToPlayerDuration * 0.55f;

            DOTween.Sequence()
                .Append(chip.DOAnchorPos(midPos, halfDur).SetEase(Ease.OutQuad))
                .Append(chip.DOAnchorPos(playerCanvasPos, landDur).SetEase(Ease.InQuad))
                .Join(chip.DOScale(Vector3.zero, landDur).SetDelay(halfDur).SetEase(Ease.InBack))
                .OnComplete(() =>
                {
                    if (chip == null) return;
                    AudioManager.Instance?.PlayChipAdd();
                    chip.gameObject.SetActive(false);
                    chip.SetParent(dealerSpawnPoint, worldPositionStays: false);
                    chip.localPosition = Vector3.zero;
                });

            yield return new WaitForSeconds(cashoutStagger);
        }

        yield return new WaitForSeconds(betToPlayerDuration + 0.25f);

        activeWinChips.Clear();
        stakeReturnChips.Clear();
        cashoutCoroutine = null;
    }
    #endregion

    #region Helpers
    private Vector2 GetCanvasPosition(RectTransform rt)
    {
        if (rt == null || targetCanvas == null) return Vector2.zero;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(targetCanvas.worldCamera, rt.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetCanvas.GetComponent<RectTransform>(), screenPoint, targetCanvas.worldCamera, out Vector2 localPoint);
        return localPoint;
    }

    private void SetSprite(RectTransform rt, int index)
    {
        if (chipSprites == null || chipSprites.Length == 0) return;
        var img = rt.GetComponent<UnityEngine.UI.Image>();
        if (img != null) img.sprite = chipSprites[Mathf.Clamp(index, 0, chipSprites.Length - 1)];
    }

    private int SpriteIndex(double winAmount)
    {
        if (winAmount >= 500) return 0;
        if (winAmount >= 100) return 1;
        if (winAmount >= 50) return 2;
        if (winAmount >= 10) return 3;
        if (winAmount >= 5) return 4;
        return chipSprites != null ? Mathf.Min(5, chipSprites.Length - 1) : 0;
    }
    #endregion
}

[System.Serializable]
internal class WinAreaData
{
    internal string betOption;
    internal Transform betAreaTarget;
    internal double betAmount;
    internal double winAmount;
    internal double winRatio; // winAmount / betAmount
}