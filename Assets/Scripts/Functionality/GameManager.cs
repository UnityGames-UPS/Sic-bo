using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;

public class GameManager : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button TBetPlus_Button;
    [SerializeField] private Button TBetMinus_Button;
    [SerializeField] private Button TBetMin_Button;
    [SerializeField] private Button TBetMax_Button;

    [SerializeField] private Button MultiplierPlus_Button;
    [SerializeField] private Button MultiplierMinus_Button;
    [SerializeField] private Button Bet_Button;
    internal int BetCounter;
    internal int MultiplierCounter;

    [Header("Texts")]
    [SerializeField] private TMP_Text TotalBet_text;
    [SerializeField] private TMP_Text balance_text;
    [SerializeField] private TMP_Text win_text;
    [SerializeField] private TMP_Text Multiplier_text;
    [SerializeField] private TMP_Text WinChance_text;
    [SerializeField] private TMP_Text ResponseMult_text;


    [Header("Managers")]
    [SerializeField] SocketIOManager socketManager;
    [SerializeField] internal UiManager uiManager;

    private double currentTotalBet = 0;
    private double currentBalance;
    private double animationduration = 2f;

    [SerializeField] AudioManager audioManager;

    [Header("GameObject")]
    [SerializeField] private Transform CarObject;
    private Vector3 startPos = new Vector3(0, -138, 0);
    private Vector3 endPos = new Vector3(0, -10, 0);

    private void Start()
    {
        BetCounter = 0;
        if (TBetPlus_Button) TBetPlus_Button.onClick.RemoveAllListeners();
        if (TBetPlus_Button) TBetPlus_Button.onClick.AddListener(delegate { ChangeBet(true); audioManager.PlayButtonAudio(); });

        if (TBetMinus_Button) TBetMinus_Button.onClick.RemoveAllListeners();
        if (TBetMinus_Button) TBetMinus_Button.onClick.AddListener(delegate { ChangeBet(false); audioManager.PlayButtonAudio(); });

        if (TBetMin_Button) TBetMin_Button.onClick.RemoveAllListeners();
        if (TBetMin_Button) TBetMin_Button.onClick.AddListener(delegate { SetBetToMin(); audioManager.PlayButtonAudio(); });

        if (TBetMax_Button) TBetMax_Button.onClick.RemoveAllListeners();
        if (TBetMax_Button) TBetMax_Button.onClick.AddListener(delegate { SetBetToMax(); audioManager.PlayButtonAudio(); ; });

        if (MultiplierPlus_Button) MultiplierPlus_Button.onClick.RemoveAllListeners();
        if (MultiplierPlus_Button) MultiplierPlus_Button.onClick.AddListener(delegate { ChangeMultiplier(true); audioManager.PlayButtonAudio(); });

        if (MultiplierMinus_Button) MultiplierMinus_Button.onClick.RemoveAllListeners();
        if (MultiplierMinus_Button) MultiplierMinus_Button.onClick.AddListener(delegate { ChangeMultiplier(false); audioManager.PlayButtonAudio(); });

        if (Bet_Button) Bet_Button.onClick.RemoveAllListeners();
        if (Bet_Button) Bet_Button.onClick.AddListener(delegate { StartBet(); audioManager.PlayBetButtonAudio(); });


    }


    private void ChangeBet(bool IncDec)
    {
        Debug.Log("changeBetRan");
        if (IncDec)
        {
            BetCounter++;
            if (BetCounter >= socketManager.initialData.bets.Count)
            {
                BetCounter = 0;
            }
        }
        else
        {
            BetCounter--;
            if (BetCounter < 0)
            {
                BetCounter = socketManager.initialData.bets.Count - 1;
            }
        }
        UpdateBetUI();

    }
    private void SetBetToMin()
    {
        BetCounter = 0;
        UpdateBetUI();
    }

    private void SetBetToMax()
    {
        BetCounter = socketManager.initialData.bets.Count - 1;
        UpdateBetUI();
    }
    private void UpdateBetUI()
    {
        if (TotalBet_text)
            TotalBet_text.text = socketManager.initialData.bets[BetCounter].ToString("F2");

        currentTotalBet = socketManager.initialData.bets[BetCounter];
    }

    private void ChangeMultiplier(bool IncDec)
    {
        if (IncDec)
        {
            MultiplierCounter++;
            if (MultiplierCounter >= socketManager.initialData.multipliers.Count)
            {
                MultiplierCounter = 0;
            }
        }
        else
        {
            MultiplierCounter--;
            if (MultiplierCounter < 0)
            {
                MultiplierCounter = socketManager.initialData.multipliers.Count - 1;
            }
        }
        Multiplier_text.text = "x" + socketManager.initialData.multipliers[MultiplierCounter].ToString("f2");
        SetWinningChance(MultiplierCounter);

    }

    private void StartBet()
    {
        StartCoroutine(accumulateResult());
    }



    IEnumerator accumulateResult()
    {
        win_text.text = "WIN:0.00";
        currentBalance = socketManager.playerdata.balance;
        if (currentBalance < currentTotalBet)
        {
            lowBalance();
            yield break;
        }

        else
        {
            ToggleButtongroup(false);
            updateBalance(currentTotalBet, false);
            StartAccelerateAnimation();
            socketManager.AccumulateResult(BetCounter, socketManager.initialData.multipliers[MultiplierCounter]);
            yield return new WaitUntil(() => socketManager.isResultdone);
            AnimateValue(socketManager.resultData.crashPoint, 1.4f);
            yield return new WaitForSeconds(1f);
            StopAccelerateAnimation();
            if (socketManager.resultData.winAmount > 0)
            {

                win_text.rectTransform.localScale = Vector3.one;

                win_text.rectTransform
                    .DOScale(1.5f, 1f)
                    .SetEase(Ease.Linear)   
                    .OnComplete(() =>
                    {
                        win_text.rectTransform
                            .DOScale(1f, 0.75f)
                            .SetEase(Ease.Linear);
                    });
                audioManager.PlayWinAudio();
            }
            win_text.text = "WIN:" + socketManager.resultData.winAmount.ToString("f2");
            balance_text.text = socketManager.playerdata.balance.ToString("f2");
            yield return new WaitForSeconds(1f);
        }

    }



    internal void setInitialUI()
    {

        currentBalance = socketManager.playerdata.balance;
        balance_text.text = socketManager.playerdata.balance.ToString("f2");
        currentTotalBet = socketManager.initialData.bets[0];
        if (TotalBet_text) TotalBet_text.text = currentTotalBet.ToString("f2");
        currentTotalBet = socketManager.initialData.bets[BetCounter];
        Multiplier_text.text = "x" + socketManager.initialData.multipliers[0].ToString("f2");
        win_text.text = "WIN:0.00";
        SetWinningChance(0);

    }


    void lowBalance()
    {
        ToggleButtongroup(true);
        uiManager.LowBalPopup();
    }

    internal void updateBalance(double amount, bool add)
    {
        if (add)
        {

            currentBalance += amount;
            balance_text.text = currentBalance.ToString("f2");
        }
        else
        {
            currentBalance -= amount;
            balance_text.text = currentBalance.ToString("f2");

        }
    }


    private void ToggleButtongroup(bool toggle)
    {
        Debug.Log($"toggleUI ran with {toggle}");
        TBetPlus_Button.interactable = toggle;
        TBetMinus_Button.interactable = toggle;
        TBetMax_Button.interactable = toggle;
        TBetMin_Button.interactable = toggle;

        MultiplierPlus_Button.interactable = toggle;
        MultiplierMinus_Button.interactable = toggle;

        Bet_Button.interactable = toggle;




    }


    public void SetWinningChance(int multiplierIndex)
    {
        if (WinChance_text == null) return;

        if (multiplierIndex < 0 || multiplierIndex >= socketManager.initialData.multipliers.Count)
            return;

        double winchance = ((1 - socketManager.initialData.houseEdge) / (socketManager.initialData.multipliers[multiplierIndex]) * 100);

        WinChance_text.text = $"Win Chance: {winchance:F2}%";
    }

    public void AnimateValue(double targetValue, float duration)
    {
        Debug.Log($"##### animate value is  called :");
        ResponseMult_text.alignment = TextAlignmentOptions.Center;
        double currentValue = 1.00;
        audioManager.PlayWLAudio("numberchange");
        DOTween.Kill(this);
        DOTween.To(
            () => currentValue,
            x => currentValue = x,
            targetValue,
            duration
        )
        .OnUpdate(() =>
        {
            if (ResponseMult_text != null)
            {

                ResponseMult_text.text = currentValue.ToString("F2") + "X";
            }
        })
        .OnComplete(() =>
      {
          if (ResponseMult_text != null && socketManager != null && socketManager.resultData != null)
          {
              if (socketManager.resultData.winAmount > 0)
                  ResponseMult_text.color = Color.green;
              else
                  ResponseMult_text.color = Color.red;
          }
      })
        .SetEase(Ease.Linear)
        .SetId(this);
    }

    #region Car Animation

    public void StartAccelerateAnimation(float tiltAngle = 1f)
    {
        if (CarObject == null)
        {
            Debug.LogError("Model not assigned!");
            return;
        }

        CarObject.localPosition = startPos;
        CarObject.localScale = Vector3.one;
        CarObject.localRotation = Quaternion.identity;

        CarObject.DOLocalRotate(new Vector3(0, 0, tiltAngle), 0.001f)
             .SetLoops(-1, LoopType.Yoyo)
             .SetEase(Ease.InOutSine)
             .SetId("RevTween");

        CarObject.DOLocalMoveY(startPos.y - 3f, 0.2f).SetEase(Ease.OutSine);
    }

    public void StopAccelerateAnimation()
    {
        DOTween.Kill("RevTween");
        DOTween.Kill("RevBounceTween");

        Sequence seq = DOTween.Sequence();

        seq.Join(CarObject.DOLocalMove(startPos, 0.3f).SetEase(Ease.OutSine));
        seq.Join(CarObject.DOLocalRotate(Vector3.zero, 0.2f).SetEase(Ease.OutSine));

        seq.OnComplete(() => MoveCarAnim());
    }

    public void MoveCarAnim()
    {
        audioManager.PlayWLAudio("car");

        if (CarObject == null)
        {
            Debug.LogError("Model not assigned!");
            return;
        }

        CarObject.localPosition = startPos;
        CarObject.localScale = Vector3.one;
        CarObject.localRotation = Quaternion.identity;

        Sequence seq = DOTween.Sequence();

        seq.Append(CarObject.DOLocalMove(endPos, 1.5f).SetEase(Ease.OutCubic));
        seq.Join(CarObject.DOScale(Vector3.one * 0.001f, 1.5f).SetEase(Ease.OutCubic));

        seq.OnComplete(() =>
        {
            Reset();
        });
    }


    private void Reset()
    {
        ToggleButtongroup(true);
        ResponseMult_text.text = "1.00X";
        ResponseMult_text.color = Color.white;
        ResetCar();
    }

    private void ResetCar()
    {
        CarObject.localPosition = startPos;
        CarObject.localScale = Vector3.one;
        CarObject.localRotation = Quaternion.identity;
    }

    #endregion

}
