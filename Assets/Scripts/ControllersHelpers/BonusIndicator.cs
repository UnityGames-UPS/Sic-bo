using UnityEngine;
using UnityEngine.UI;
using System;
using DG.Tweening;

public class BonusIndicator : MonoBehaviour
{
    #region Row Data Structure
      [Serializable]
    public class IndicatorRow
    {
        [Header("Row Images")]
        public Image multiplierImage;
        public Image number1Image;
        public Image number2Image;
        public Image number3Image;
        public Image number4Image;
        public GameObject rowObject;
        public HorizontalLayoutGroup layoutGroup;

        internal void Show() { if (rowObject) rowObject.SetActive(true); }

        internal void Hide()
        {
            if (rowObject) rowObject.SetActive(false);
            if (multiplierImage) multiplierImage.gameObject.SetActive(false);
            if (number1Image) number1Image.gameObject.SetActive(false);
            if (number2Image) number2Image.gameObject.SetActive(false);
            if (number3Image) number3Image.gameObject.SetActive(false);
            if (number4Image) number4Image.gameObject.SetActive(false);
        }
    }
    #endregion

    #region Serialized Fields
    [Header("Main Background")]
    [SerializeField] private Image mainBgImage;
    [SerializeField] private GameObject backgroundAnimObject;

    [Header("Main Number Holder")]
    [SerializeField] private GameObject numberHolder;

    [Header("Three Indicator Rows")]
    [SerializeField] private IndicatorRow row1;
    [SerializeField] private IndicatorRow row2;
    [SerializeField] private IndicatorRow row3;
    #endregion

    #region Internal State
    internal string betOption;
    internal bool isWon;
    private IndicatorRow[] allRows;
    #endregion

    #region Unity Lifecycle
    private void Awake() => EnsureInitialized();
    #endregion

    #region Internal API
    internal void Setup(int[] multipliers, Sprite[] numberSprites, Sprite multiplierSprite,
        Sprite bgSprite = null, Sprite dotSprite = null, bool isWonState = false)
    {
        EnsureInitialized();
        HideAllRows();

        if (mainBgImage != null && bgSprite != null) mainBgImage.sprite = bgSprite;
        if (numberHolder != null) numberHolder.transform.localScale = Vector3.one;

        int rowCount = Mathf.Min(multipliers.Length, 3);
        for (int i = 0; i < rowCount; i++)
            SetupRow(allRows[i], multipliers[i], numberSprites, multiplierSprite, isWonState, rowCount);
    }

    internal void Setup(float[] multipliers, Sprite[] numberSprites, Sprite multiplierSprite,
        Sprite brownDotSprite, Sprite greenDotSprite, bool isWonState, Sprite bgSprite = null)
    {
        EnsureInitialized();
        HideAllRows();

        if (mainBgImage != null && bgSprite != null) mainBgImage.sprite = bgSprite;
        if (numberHolder != null) numberHolder.transform.localScale = Vector3.one;

        int rowCount = Mathf.Min(multipliers.Length, 3);
        for (int i = 0; i < rowCount; i++)
            SetupRowSmart(allRows[i], multipliers[i], numberSprites, multiplierSprite,
                brownDotSprite, greenDotSprite, isWonState, rowCount);
    }

    internal void AnimateToGreen(Sprite[] greenNumberSprites, Sprite greenMultiplierSprite,
        Sprite greenBgSprite, Sprite greenDotSprite, float scaleOutDuration, float scaleInDuration,
        Action onComplete = null)
    {
        EnsureInitialized();
        if (numberHolder == null) return;

        numberHolder.transform.DOKill();
        EnableBackgroundAnim();

        if (mainBgImage != null && greenBgSprite != null)
            mainBgImage.sprite = greenBgSprite;

        DOTween.Sequence()
            .Append(numberHolder.transform.DOScale(0f, scaleOutDuration).SetEase(Ease.InBack))
            .AppendCallback(() => SwapAllRowsToGreen(greenNumberSprites, greenMultiplierSprite, greenDotSprite))
            .Append(numberHolder.transform.DOScale(1f, scaleInDuration).SetEase(Ease.OutBack))
            .OnComplete(() => onComplete?.Invoke())
            .Play();
    }

    internal void HideAllRows()
    {
        EnsureInitialized();
        foreach (var row in allRows) row?.Hide();
        if (backgroundAnimObject != null) backgroundAnimObject.SetActive(false);
    }

    internal void EnableBackgroundAnim()
    {
        if (backgroundAnimObject != null) backgroundAnimObject.SetActive(true);
    }

    internal Transform GetRowTransform(int rowIndex)
    {
        EnsureInitialized();
        if (rowIndex >= 0 && rowIndex < allRows.Length && allRows[rowIndex] != null)
            return allRows[rowIndex].rowObject?.transform;
        return null;
    }
    #endregion

    #region Setup Helpers
    private void EnsureInitialized()
    {
        if (allRows == null) allRows = new IndicatorRow[] { row1, row2, row3 };
    }

    private void SetupRow(IndicatorRow row, int multiplier, Sprite[] numberSprites,
        Sprite multiplierSprite, bool isWonState, int totalRowCount = 1)
    {
        if (row == null) return;

        row.Show();
        HideRowImages(row);
        SetImage(row.multiplierImage, multiplierSprite);

        string s = multiplier.ToString();

        if (s.Length == 1)
        {
            if (row.layoutGroup != null)
                row.layoutGroup.spacing = totalRowCount == 3 ? -37f : totalRowCount == 2 ? -25f : 0f;
            SetDigit(row.number1Image, s[0], numberSprites);
        }
        else if (s.Length == 2)
        {
            if (row.layoutGroup != null)
                row.layoutGroup.spacing = totalRowCount == 3 ? -24f : totalRowCount == 2 ? -10f : 0f;
            SetDigit(row.number1Image, s[0], numberSprites);
            SetDigit(row.number2Image, s[1], numberSprites);
        }
        else if (s.Length == 3)
        {
            if (row.layoutGroup != null)
                row.layoutGroup.spacing = totalRowCount == 3 ? -15f : 0f;
            SetDigit(row.number1Image, s[0], numberSprites);
            SetDigit(row.number2Image, s[1], numberSprites);
            SetDigit(row.number3Image, s[2], numberSprites);
        }
        else
        {
            if (row.layoutGroup != null)
                row.layoutGroup.spacing = totalRowCount == 3 ? -5f : 0f;
            SetDigit(row.number1Image, s[0], numberSprites);
            SetDigit(row.number2Image, s[1], numberSprites);
            SetDigit(row.number3Image, s[2], numberSprites);
            SetDigit(row.number4Image, s[3], numberSprites);
        }
    }

    private void SetupRowSmart(IndicatorRow row, float multiplier, Sprite[] numberSprites,
        Sprite multiplierSprite, Sprite brownDotSprite, Sprite greenDotSprite,
        bool isWonState, int totalRowCount = 1)
    {
        if (row == null) return;

        if (multiplier % 1 == 0)
        {
            SetupRow(row, Mathf.RoundToInt(multiplier), numberSprites, multiplierSprite, isWonState, totalRowCount);
            return;
        }

        row.Show();
        HideRowImages(row);
        SetImage(row.multiplierImage, multiplierSprite);

        Sprite dotSprite = isWonState ? greenDotSprite : brownDotSprite;
        string formatted = multiplier.ToString("F1");
        string[] parts = formatted.Split('.');
        string whole = parts[0];
        char dec = parts.Length > 1 ? parts[1][0] : '0';

        if (whole.Length == 1)
        {
            if (row.layoutGroup != null)
                row.layoutGroup.spacing = totalRowCount == 3 ? -10f : totalRowCount == 2 ? -5f : 0f;
            SetDigit(row.number1Image, whole[0], numberSprites);
            SetImage(row.number2Image, dotSprite);
            SetDigit(row.number3Image, dec, numberSprites);
        }
        else if (whole.Length == 2)
        {
            if (row.layoutGroup != null)
                row.layoutGroup.spacing = totalRowCount == 3 ? -10f : totalRowCount == 2 ? -5f : 0f;
            SetDigit(row.number1Image, whole[0], numberSprites);
            SetDigit(row.number2Image, whole[1], numberSprites);
            SetImage(row.number3Image, dotSprite);
            SetDigit(row.number4Image, dec, numberSprites);
        }
        else
        {
            if (row.layoutGroup != null)
                row.layoutGroup.spacing = totalRowCount == 3 ? -10f : 0f;
            SetDigit(row.number1Image, whole[0], numberSprites);
            SetDigit(row.number2Image, whole[1], numberSprites);
            SetDigit(row.number3Image, whole[2], numberSprites);
            if (whole.Length >= 4) SetDigit(row.number4Image, whole[3], numberSprites);
        }
    }

    private void HideRowImages(IndicatorRow row)
    {
        if (row == null) return;
        HideImage(row.multiplierImage);
        HideImage(row.number1Image);
        HideImage(row.number2Image);
        HideImage(row.number3Image);
        HideImage(row.number4Image);
    }

    private void SetImage(Image img, Sprite sprite)
    {
        if (img == null || sprite == null) return;
        img.sprite = sprite;
        img.gameObject.SetActive(true);
        GetOrAddTracker(img).isDotSprite = true;
        GetOrAddTracker(img).digitIndex = -1;
    }

    private void SetDigit(Image img, char digit, Sprite[] sprites)
    {
        if (img == null || sprites == null) return;
        int idx = digit - '0';
        if (idx < 0 || idx >= sprites.Length) return;
        img.sprite = sprites[idx];
        img.gameObject.SetActive(true);
        GetOrAddTracker(img).digitIndex = idx;
        GetOrAddTracker(img).isDotSprite = false;
    }

    private void HideImage(Image img)
    {
        if (img != null) img.gameObject.SetActive(false);
    }

    private static DigitTracker GetOrAddTracker(Image img)
    {
        var t = img.GetComponent<DigitTracker>();
        return t != null ? t : img.gameObject.AddComponent<DigitTracker>();
    }
    #endregion

    #region Animation Helpers
    private void SwapAllRowsToGreen(Sprite[] greenNumberSprites, Sprite greenMultiplierSprite, Sprite greenDotSprite)
    {
        foreach (var row in allRows)
        {
            if (row == null) continue;
            if (row.multiplierImage != null && greenMultiplierSprite != null)
                row.multiplierImage.sprite = greenMultiplierSprite;
            SwapRowNumbersToGreen(row, greenNumberSprites, greenDotSprite);
        }
    }

    private void SwapRowNumbersToGreen(IndicatorRow row, Sprite[] greenNumberSprites, Sprite greenDotSprite)
    {
        if (row == null) return;
        SwapSpriteIfActive(row.number1Image, greenNumberSprites, greenDotSprite);
        SwapSpriteIfActive(row.number2Image, greenNumberSprites, greenDotSprite);
        SwapSpriteIfActive(row.number3Image, greenNumberSprites, greenDotSprite);
        SwapSpriteIfActive(row.number4Image, greenNumberSprites, greenDotSprite);
    }

    private void SwapSpriteIfActive(Image img, Sprite[] greenNumberSprites, Sprite greenDotSprite)
    {
        if (img == null || !img.gameObject.activeSelf) return;

        DigitTracker tracker = img.GetComponent<DigitTracker>();
        if (tracker == null) return;

        if (tracker.isDotSprite)
        {
            if (greenDotSprite != null) img.sprite = greenDotSprite;
            return;
        }

        int idx = tracker.digitIndex;
        if (idx >= 0 && idx < greenNumberSprites.Length && greenNumberSprites[idx] != null)
            img.sprite = greenNumberSprites[idx];
    }
    #endregion
}

internal class DigitTracker : MonoBehaviour
{
    internal int digitIndex = -1;
    internal bool isDotSprite = false;
}