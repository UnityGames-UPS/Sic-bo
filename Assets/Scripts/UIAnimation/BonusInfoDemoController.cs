using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

public class BonusInfoDemoController : MonoBehaviour
{
    #region Serialized Fields
    [Header("10x Indicator")]
    [SerializeField] private Image single_BgImage;
    [SerializeField] private GameObject single_BgAnim;
    [SerializeField] private Transform single_NumberHolder;  // the child whose scale is animated
    [SerializeField] private Image single_Num1;   // "1"
    [SerializeField] private Image single_Num0;   // "0"
    [SerializeField] private Image single_X;      // "x" multiplier icon

    [Header("2x/4x/6x Indicator")]
    [SerializeField] private Image multi_BgImage;
    [SerializeField] private GameObject multi_BgAnim;
    [SerializeField] private Transform multi_NumberHolder;
    [SerializeField] private Image multi_Num2;    // "2"
    [SerializeField] private Image multi_Num4;    // "4"
    [SerializeField] private Image multi_Num6;    // "6"
    [SerializeField] private Image multi_X2;       // "x" multiplier icon
    [SerializeField] private Image multi_X4;
    [SerializeField] private Image multi_X6;

    [Header("Background Sprites")]
    [SerializeField] private Sprite brownBg;
    [SerializeField] private Sprite greenBg;

    [Header("Number Sprites – Brown")]
    [SerializeField] private Sprite brown_0;
    [SerializeField] private Sprite brown_1;
    [SerializeField] private Sprite brown_2;
    [SerializeField] private Sprite brown_4;
    [SerializeField] private Sprite brown_6;
    [SerializeField] private Sprite brown_X;

    [Header("Number Sprites – Green")]
    [SerializeField] private Sprite green_0;
    [SerializeField] private Sprite green_1;
    [SerializeField] private Sprite green_2;
    [SerializeField] private Sprite green_4;
    [SerializeField] private Sprite green_6;
    [SerializeField] private Sprite green_X;

    [Header("Timing")]
    [SerializeField] private float startDelay = 0.6f;
    [SerializeField] private float scaleOutDuration = 0.18f;
    [SerializeField] private float scaleInDuration = 0.22f;
    [SerializeField] private float staggerBetween = 0.15f;
    [SerializeField] private float BGresetDelay = 0.5f;

    #endregion

    private Tween _delayTween;

    #region Public API

    internal void PlayDemoOnce()
    {
        _delayTween?.Kill();

        ResetToBrown();

        _delayTween = DOVirtual.DelayedCall(startDelay, () =>
        {
            AnimateToGreen(single_BgImage, single_BgAnim, single_NumberHolder,
                           SwapSingleToGreen, 0f);

            AnimateToGreen(multi_BgImage, multi_BgAnim, multi_NumberHolder,
                           SwapMultiToGreen, staggerBetween);
        });
    }

    internal void ResetForNextOpen()
    {
        _delayTween?.Kill();
        ResetToBrown();
    }

    #endregion

    #region Private – Animation

    private void AnimateToGreen(Image bgImage, GameObject bgAnim, Transform numberHolder,
                                System.Action swapAction, float extraDelay)
    {
        if (numberHolder == null) return;

        DOVirtual.DelayedCall(extraDelay, () =>
        {
            numberHolder.DOKill();

            if (bgAnim != null) bgAnim.SetActive(true);
            if (bgImage != null && greenBg != null) bgImage.sprite = greenBg;

            DOTween.Sequence()
                .Append(numberHolder.DOScale(0f, scaleOutDuration).SetEase(Ease.InBack))
                .AppendCallback(() => swapAction?.Invoke())
                .Append(numberHolder.DOScale(1f, scaleInDuration).SetEase(Ease.OutBack));

            StartCoroutine(BGDisablewithDelay(bgAnim));
        }); 
       
    }
    private IEnumerator BGDisablewithDelay(GameObject bgAnim)
    {
        yield return new WaitForSeconds(BGresetDelay);
        if (bgAnim != null) bgAnim.SetActive(false);
    }
    private void ResetToBrown()
    {
        ResetHolder(single_NumberHolder);
        SetSprite(single_BgImage, brownBg);
        SetSprite(single_Num1, brown_1);
        SetSprite(single_Num0, brown_0);
        SetSprite(single_X, brown_X);

        ResetHolder(multi_NumberHolder);
        SetSprite(multi_BgImage, brownBg);
        SetSprite(multi_Num2, brown_2);
        SetSprite(multi_Num4, brown_4);
        SetSprite(multi_Num6, brown_6);
        SetSprite(multi_X2, brown_X);
        SetSprite(multi_X4, brown_X);
        SetSprite(multi_X6, brown_X);
    }

    private void SwapSingleToGreen()
    {
        SetSprite(single_Num1, green_1);
        SetSprite(single_Num0, green_0);
        SetSprite(single_X, green_X);
    }

    private void SwapMultiToGreen()
    {
        SetSprite(multi_Num2, green_2);
        SetSprite(multi_Num4, green_4);
        SetSprite(multi_Num6, green_6);
        SetSprite(multi_X2, green_X);
        SetSprite(multi_X4, green_X);
        SetSprite(multi_X6, green_X);

    }

    private static void ResetHolder(Transform t)
    {
        if (t == null) return;
        t.DOKill();
        t.localScale = Vector3.one;
    }

    private static void SetSprite(Image img, Sprite sprite)
    {
        if (img != null && sprite != null) img.sprite = sprite;
    }

    #endregion
}