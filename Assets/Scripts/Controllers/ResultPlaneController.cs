using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Newtonsoft.Json;

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

    [Header("Win Animation Sync")]
    [SerializeField] private float winAnimationDelaySeconds = 0.3f; // Delay to sync with win animations
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

    /// <summary>
    /// Hides all rows on Start so no scene placeholder data is ever visible.
    /// PopulateFromStats will show rows once real server data arrives.
    /// </summary>
    private void InitializeDisplay()
    {
        for (int i = 0; i < resultRows.Count; i++)
        {
            if (resultRows[i].rowContainer != null)
                resultRows[i].rowContainer.SetActive(false);
        }
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

    /// <summary>
    /// Pre-populates the result plane from the server stats list (up to 40 rounds).
    /// Shows the most recent 10: oldest in row 0 (left), newest in row 9 (right).
    /// If fewer than 10 exist the leftmost rows stay hidden.
    /// No animation — instant fill on room join.
    /// </summary>
    internal void PopulateFromStats(List<string> rawStats)
    {
        // Stop any ongoing animation
        if (animCoroutine != null) { StopCoroutine(animCoroutine); animCoroutine = null; }
        slideSeq?.Kill();
        scaleSeq?.Kill();
        isAnimating = false;

        // Reset all rows to canonical slot positions
        for (int i = 0; i < resultRows.Count; i++)
        {
            var rt = resultRows[i].RT;
            if (rt != null) rt.anchoredPosition = slotPositions[i];
        }

        // Always hide staging row
        if (resultRows[10].rowContainer != null)
            resultRows[10].rowContainer.SetActive(false);

        // Parse last up to 10 entries, oldest first
        List<ResultData> entries = ParseLast10Stats(rawStats);

        // Anchor newest (index 0) to row 9, oldest (index 9) to row 0
        int startRow = 10 - entries.Count;

        for (int i = 0; i < 10; i++)
        {
            // entryIndex reversed: row 9 = entries[0], row 0 = entries[entries.Count-1]
            int entryIndex = (10 - 1 - i) - startRow;
            bool hasData = entryIndex >= 0 && entryIndex < entries.Count;

            var row = resultRows[i];
            if (row.rowContainer != null)
                row.rowContainer.SetActive(hasData);

            if (hasData)
            {
                row.SetData(entries[entryIndex], GetDiceSprite);
                row.SetScaleToOne();
            }
        }
    }

    /// <summary>
    /// Clears all results and hides all rows (called when leaving the room).
    /// </summary>
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
            if (resultRows[i].rowContainer != null)
                resultRows[i].rowContainer.SetActive(false);
            resultRows[i].SetScaleToOne();
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Parses raw JSON stat strings, returns up to the last 10 as ResultData (oldest first).
    /// </summary>
    private List<ResultData> ParseLast10Stats(List<string> rawStats)
    {
        var results = new List<ResultData>();
        if (rawStats == null || rawStats.Count == 0) return results;

        int endIdx = Mathf.Min(rawStats.Count, 10);
        for (int i = 0; i < endIdx; i++)
        {
            try
            {
                ResultData data = JsonConvert.DeserializeObject<ResultData>(rawStats[i]);
                if (data == null) continue;
                if (data.dice1 < 1 || data.dice1 > 6 ||
                    data.dice2 < 1 || data.dice2 > 6 ||
                    data.dice3 < 1 || data.dice3 > 6) continue;

                // sum is not always present in stat entries — compute it
                if (data.sum == 0)
                    data.sum = data.dice1 + data.dice2 + data.dice3;

                results.Add(data);
            }
            catch { /* skip malformed entries */ }
        }
        return results;
    }

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

    #region Animation
    private IEnumerator CR_SlideAndAnimate(ResultData newResult)
    {
        isAnimating = true;

        // Add delay to sync with win animations
        if (winAnimationDelaySeconds > 0)
        {
            yield return new WaitForSeconds(winAnimationDelaySeconds);
        }

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

            // FIXED: Proper big/small logic with triple checking
            bool isTriple = data.dice1 == data.dice2 && data.dice2 == data.dice3;

            // Never show big/small for triples
            if (isTriple)
            {
                bigImage?.SetActive(false);
                smallImage?.SetActive(false);
            }
            else
            {
                // Use server's matchSide if available, otherwise calculate
                bool showBig = false;
                bool showSmall = false;

                if (!string.IsNullOrEmpty(data.matchSide))
                {
                    // Trust server's matchSide
                    string side = data.matchSide.ToLower();
                    showBig = side == "big";
                    showSmall = side == "small";
                }
                else
                {
                    // Fallback to calculation
                    showSmall = data.sum >= 4 && data.sum <= 10;
                    showBig = data.sum >= 11 && data.sum <= 17;
                }

                bigImage?.SetActive(showBig);
                smallImage?.SetActive(showSmall);
            }

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