using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class ResultPlaneController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Row References")]
    [SerializeField] private List<ResultRow> resultRows = new List<ResultRow>();

    [Header("Slide Animation")]
    [SerializeField] private float slideDuration = 0.3f;
    [SerializeField] private Ease slideEase = Ease.OutCubic;

    [Header("Pop-in Animation")]
    [SerializeField] private float scaleAnimationDuration = 0.2f;
    [SerializeField] private float chainDelay = 0.05f;
    [SerializeField] private Ease scaleEase = Ease.OutBack;

    [Header("Dice Sprites")]
    [SerializeField] private Sprite dice1Sprite;
    [SerializeField] private Sprite dice2Sprite;
    [SerializeField] private Sprite dice3Sprite;
    [SerializeField] private Sprite dice4Sprite;
    [SerializeField] private Sprite dice5Sprite;
    [SerializeField] private Sprite dice6Sprite;
    #endregion

    #region Private Fields
    private Vector2[] slotPositions;
    private float rowWidth;
    private Sequence slideSeq;
    private Sequence scaleSeq;
    private Coroutine animCoroutine;
    private bool isAnimating;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        CacheSlotPositions();
        InitializeDisplay();
    }

    private void OnDestroy()
    {
        slideSeq?.Kill();
        scaleSeq?.Kill();
        if (animCoroutine != null) StopCoroutine(animCoroutine);
    }
    #endregion

    #region Initialization
    private void CacheSlotPositions()
    {
        slotPositions = new Vector2[11];

        for (int i = 0; i < 10; i++)
        {
            var rt = resultRows[i].RT;
            if (rt != null) slotPositions[i] = rt.anchoredPosition;
        }

        var rt0 = resultRows[0].RT;
        var rt1 = resultRows[1].RT;
        if (rt0 != null && rt1 != null)
            rowWidth = Mathf.Abs(rt1.anchoredPosition.x - rt0.anchoredPosition.x);

        if (rowWidth < 1f) rowWidth = 100f;

        slotPositions[10] = slotPositions[9] + new Vector2(rowWidth, 0f);

        var rt10 = resultRows[10].RT;
        if (rt10 != null) rt10.anchoredPosition = slotPositions[10];
    }

    private void InitializeDisplay()
    {
        for (int i = 0; i < 10; i++)
        {
            if (resultRows[i].rowContainer != null)
            {
                resultRows[i].rowContainer.SetActive(true);
                resultRows[i].SetScaleToOne();
            }
        }
        if (resultRows[10].rowContainer != null)
            resultRows[10].rowContainer.SetActive(false);
    }
    #endregion

    #region Internal API
    internal void AddNewResult(DiceResultData resultData)
    {
        if (resultData == null) return;

        if (animCoroutine != null) { StopCoroutine(animCoroutine); animCoroutine = null; }
        slideSeq?.Kill();
        scaleSeq?.Kill();

        if (isAnimating) { RecycleRow0(); isAnimating = false; }

        var r = new ResultData
        {
            dice1 = resultData.dice1,
            dice2 = resultData.dice2,
            dice3 = resultData.dice3,
            sum = resultData.sum,
            matchSide = resultData.matchSide
        };

        animCoroutine = StartCoroutine(CR_SlideAndAnimate(r));
    }

    internal void ClearAllResults()
    {
        if (animCoroutine != null) { StopCoroutine(animCoroutine); animCoroutine = null; }
        slideSeq?.Kill();
        scaleSeq?.Kill();
        isAnimating = false;

        for (int i = 0; i < resultRows.Count; i++)
        {
            var rt = resultRows[i].RT;
            if (rt != null) rt.anchoredPosition = slotPositions[i];
            if (resultRows[i].rowContainer != null) resultRows[i].rowContainer.SetActive(i < 10);
            resultRows[i].SetScaleToOne();
        }
    }
    #endregion

    #region Animation
    private IEnumerator CR_SlideAndAnimate(ResultData newResult)
    {
        isAnimating = true;

        for (int i = 0; i < 11; i++)
        {
            var rt = resultRows[i].RT;
            if (rt != null) rt.anchoredPosition = slotPositions[i];
        }

        ResultRow staging = resultRows[10];
        staging.SetData(newResult, GetDiceSprite);
        staging.SetScaleToZero();
        staging.rowContainer.SetActive(true);

        slideSeq = DOTween.Sequence();
        for (int i = 0; i < 11; i++)
        {
            var rt = resultRows[i].RT;
            if (rt == null) continue;
            Vector2 from = slotPositions[i];
            Vector2 to = from - new Vector2(rowWidth, 0f);
            slideSeq.Join(rt.DOAnchorPos(to, slideDuration).From(from).SetEase(slideEase));
        }

        yield return slideSeq.WaitForCompletion();

        AnimateRowElements(staging);
        yield return new WaitForSeconds(scaleAnimationDuration + chainDelay * 5f);

        RecycleRow0();
        isAnimating = false;
        animCoroutine = null;
    }

    private void AnimateRowElements(ResultRow row)
    {
        scaleSeq?.Kill();
        scaleSeq = DOTween.Sequence();
        float d = 0f;

        scaleSeq.InsertCallback(d, () =>
        { if (row.sumText != null) row.sumText.transform.DOScale(1f, scaleAnimationDuration).SetEase(scaleEase); });
        d += chainDelay;

        scaleSeq.InsertCallback(d, () =>
        {
            if (row.bigImage != null && row.bigImage.activeSelf) row.bigImage.transform.DOScale(1f, scaleAnimationDuration).SetEase(scaleEase);
            if (row.smallImage != null && row.smallImage.activeSelf) row.smallImage.transform.DOScale(1f, scaleAnimationDuration).SetEase(scaleEase);
        });
        d += chainDelay;

        scaleSeq.InsertCallback(d, () =>
        { if (row.dice1Image != null) row.dice1Image.transform.DOScale(1f, scaleAnimationDuration).SetEase(scaleEase); });
        d += chainDelay;

        scaleSeq.InsertCallback(d, () =>
        { if (row.dice2Image != null) row.dice2Image.transform.DOScale(1f, scaleAnimationDuration).SetEase(scaleEase); });
        d += chainDelay;

        scaleSeq.InsertCallback(d, () =>
        { if (row.dice3Image != null) row.dice3Image.transform.DOScale(1f, scaleAnimationDuration).SetEase(scaleEase); });
    }

    private void RecycleRow0()
    {
        if (resultRows.Count != 11) return;

        ResultRow old0 = resultRows[0];
        var rt = old0.RT;
        if (rt != null) rt.anchoredPosition = slotPositions[10];
        if (old0.rowContainer != null) old0.rowContainer.SetActive(false);

        resultRows.RemoveAt(0);
        resultRows.Add(old0);
    }
    #endregion

    #region Helpers
    private Sprite GetDiceSprite(int v) => v switch
    {
        1 => dice1Sprite,
        2 => dice2Sprite,
        3 => dice3Sprite,
        4 => dice4Sprite,
        5 => dice5Sprite,
        6 => dice6Sprite,
        _ => null
    };
    #endregion

    #region Nested Class
    [System.Serializable]
    public class ResultRow
    {
        [Header("Container")]
        public GameObject rowContainer;

        [Header("UI Elements")]
        public TMP_Text sumText;
        public GameObject bigImage;
        public GameObject smallImage;
        public Image dice1Image;
        public Image dice2Image;
        public Image dice3Image;

        // Cached RectTransform — avoids GetComponent every frame during slide animation
        private RectTransform _rt;
        public RectTransform RT
        {
            get
            {
                if (_rt == null && rowContainer != null)
                    _rt = rowContainer.GetComponent<RectTransform>();
                return _rt;
            }
        }

        // Cached Color structs — avoids new Color() allocations every result
        private static readonly Color EvenColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        private static readonly Color OddColor = new Color(0.8f, 0.1f, 0.1f, 1f);

        public bool IsValid() =>
            rowContainer != null && sumText != null && bigImage != null &&
            smallImage != null && dice1Image != null && dice2Image != null && dice3Image != null;

        public void SetData(ResultData data, System.Func<int, Sprite> getDiceSprite)
        {
            if (sumText != null)
            {
                sumText.text = data.sum.ToString();
                sumText.color = data.sum % 2 == 0 ? EvenColor : OddColor;
            }

            bool isTriple = data.dice1 == data.dice2 && data.dice2 == data.dice3;
            bool showSmall = !isTriple && data.sum >= 4 && data.sum <= 10;
            bool showBig = !isTriple && data.sum >= 11 && data.sum <= 17;

            bigImage?.SetActive(showBig);
            smallImage?.SetActive(showSmall);

            if (dice1Image != null) dice1Image.sprite = getDiceSprite(data.dice1);
            if (dice2Image != null) dice2Image.sprite = getDiceSprite(data.dice2);
            if (dice3Image != null) dice3Image.sprite = getDiceSprite(data.dice3);
        }

        public void SetScaleToOne()
        {
            if (sumText != null) sumText.transform.localScale = Vector3.one;
            if (bigImage != null) bigImage.transform.localScale = Vector3.one;
            if (smallImage != null) smallImage.transform.localScale = Vector3.one;
            if (dice1Image != null) dice1Image.transform.localScale = Vector3.one;
            if (dice2Image != null) dice2Image.transform.localScale = Vector3.one;
            if (dice3Image != null) dice3Image.transform.localScale = Vector3.one;
        }

        public void SetScaleToZero()
        {
            if (sumText != null) sumText.transform.localScale = Vector3.zero;
            if (bigImage != null) bigImage.transform.localScale = Vector3.zero;
            if (smallImage != null) smallImage.transform.localScale = Vector3.zero;
            if (dice1Image != null) dice1Image.transform.localScale = Vector3.zero;
            if (dice2Image != null) dice2Image.transform.localScale = Vector3.zero;
            if (dice3Image != null) dice3Image.transform.localScale = Vector3.zero;
        }
    }
    #endregion
}