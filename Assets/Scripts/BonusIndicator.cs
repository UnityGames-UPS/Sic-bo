using UnityEngine;
using UnityEngine.UI;
using System;
using DG.Tweening;

/// <summary>
/// Bonus indicator with 3 pre-defined rows, each row can display multipliers
/// Each row has 5 images: X + 4 number/dot images to support formats like "X12.1"
/// </summary>
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
        internal void Show()
        {
            if (rowObject) rowObject.SetActive(true);
        }

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
    [SerializeField] private Image mainBgImage; // Static sprite background (used for win/green state swap)

    [SerializeField] private GameObject backgroundAnimObject;

    [Header("Main Number Holder")]
    [SerializeField] private GameObject numberHolder; // Single container for all rows' numbers

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
    private void Awake()
    { 
        EnsureInitialized();
    }

    /// <summary>
    /// Lazy-initializes allRows. Called before every method that uses allRows
    /// because Awake() may not have run yet (prefab root starts inactive).
    /// </summary>
    private void EnsureInitialized()
    {
        if (allRows == null)
            allRows = new IndicatorRow[] { row1, row2, row3 };
    }
    #endregion

    #region Public API
    /// <summary>
    /// Setup indicator with array of integer multipliers (no decimals)
    /// Shows up to 3 rows based on array length
    /// </summary>
    internal void Setup(int[] multipliers, Sprite[] numberSprites, Sprite multiplierSprite,
        Sprite bgSprite = null, Sprite dotSprite = null, bool isWonState = false)
    {
        EnsureInitialized();
        HideAllRows();

        if (mainBgImage != null && bgSprite != null)
        {
            mainBgImage.sprite = bgSprite;
        }

        // Reset number holder scale
        if (numberHolder != null)
        {
            numberHolder.transform.localScale = Vector3.one;
        }

        int rowCount = Mathf.Min(multipliers.Length, 3);

        for (int i = 0; i < rowCount; i++)
        {
            SetupRow(allRows[i], multipliers[i], numberSprites, multiplierSprite, isWonState, rowCount);
        }
    }

    /// <summary>
    /// Setup indicator with array of float multipliers (for decimal support)
    /// Only shows decimal point when the value actually has decimals
    /// </summary>
    internal void Setup(float[] multipliers, Sprite[] numberSprites, Sprite multiplierSprite,
        Sprite brownDotSprite, Sprite greenDotSprite, bool isWonState, Sprite bgSprite = null)
    {
        EnsureInitialized();
        HideAllRows();

        if (mainBgImage != null && bgSprite != null)
        {
            mainBgImage.sprite = bgSprite;
        }

        // Reset number holder scale
        if (numberHolder != null)
        {
            numberHolder.transform.localScale = Vector3.one;
        }

        int rowCount = Mathf.Min(multipliers.Length, 3);

        for (int i = 0; i < rowCount; i++)
        {
            SetupRowSmart(allRows[i], multipliers[i], numberSprites, multiplierSprite,
                brownDotSprite, greenDotSprite, isWonState, rowCount);
        }
    }

    /// <summary>
    /// Animate all rows to green in one unified animation.
    /// Changes main background and animates single number holder.
    /// onComplete fires after the scale-back-up finishes.
    /// </summary>
    internal void AnimateToGreen(Sprite[] greenNumberSprites, Sprite greenMultiplierSprite,
        Sprite greenBgSprite, Sprite greenDotSprite, float scaleOutDuration, float scaleInDuration,
        Action onComplete = null)
    {
        EnsureInitialized();
        if (numberHolder == null) return;
        numberHolder.transform.DOKill();
        EnableBackgroundAnim();

        // Step 1: Change main background to green immediately
        if (mainBgImage != null && greenBgSprite != null)
        {
            mainBgImage.sprite = greenBgSprite;
        }

        // Step 2: Scale down all numbers, swap sprites, scale back up
        Sequence sequence = DOTween.Sequence();

        // Scale down
        sequence.Append(numberHolder.transform.DOScale(0f, scaleOutDuration).SetEase(Ease.InBack));

        // Swap sprites while scaled to 0
        sequence.AppendCallback(() =>
        {
            SwapAllRowsToGreen(greenNumberSprites, greenMultiplierSprite, greenDotSprite);
        });

        // Scale back up
        sequence.Append(numberHolder.transform.DOScale(1f, scaleInDuration).SetEase(Ease.OutBack));

        // Fire callback when done
        if (onComplete != null)
            sequence.OnComplete(() => onComplete());

        sequence.Play();
    }

    /// <summary>
    /// Hide all rows and disable the animated background
    /// </summary>
    internal void HideAllRows()
    {
        EnsureInitialized();
        if (allRows != null)
        {
            foreach (var row in allRows)
            {
                if (row != null) row.Hide();
            }
        }

        // Stop the background animation
        if (backgroundAnimObject != null)
            backgroundAnimObject.SetActive(false);
    }
    internal void EnableBackgroundAnim()
    {
        if (backgroundAnimObject != null)
        {
            backgroundAnimObject.SetActive(true);
        }
    }
    /// <summary>
    /// Get the transform of a specific row for animation
    /// </summary>
    internal Transform GetRowTransform(int rowIndex)
    {
        EnsureInitialized();
        if (rowIndex >= 0 && rowIndex < allRows.Length && allRows[rowIndex] != null)
        {
            return allRows[rowIndex].rowObject?.transform;
        }
        return null;
    }
    #endregion

    #region Private Methods - Setup
    /// <summary>
    /// Setup row with integer multiplier (whole numbers only)
    /// </summary>
    private void SetupRow(IndicatorRow row, int multiplier, Sprite[] numberSprites,
        Sprite multiplierSprite, bool isWonState, int totalRowCount = 1)
    {
        if (row == null) return;

        Debug.Log($"[BonusIndicator] SetupRow called with multiplier: {multiplier}");

        row.Show();
        HideRowImages(row);

        // Set multiplier X
        SetImage(row.multiplierImage, multiplierSprite);

        string s = multiplier.ToString();

        // Display digits with dynamic spacing based on row count
        if (s.Length == 1)
        {
            // X2
            // 3 rows: -40, 2 rows: -20, 1 row: 0
            if (row.layoutGroup != null)
                row.layoutGroup.spacing = totalRowCount == 3 ? -40.0f : (totalRowCount == 2 ? -20.0f : 0f);
            SetDigit(row.number1Image, s[0], numberSprites);
        }
        else if (s.Length == 2)
        {
            // X12
            // 3 rows: -30, 2 rows: -10, 1 row: 0
            if (row.layoutGroup != null)
                row.layoutGroup.spacing = totalRowCount == 3 ? -30.0f : (totalRowCount == 2 ? -10.0f : 0f);
            SetDigit(row.number1Image, s[0], numberSprites);
            SetDigit(row.number2Image, s[1], numberSprites);
        }
        else if (s.Length == 3)
        {
            // X123
            // 3 rows: -20, 2 rows: -5, 1 row: 0
            if (row.layoutGroup != null)
                row.layoutGroup.spacing = totalRowCount == 3 ? -20.0f : (totalRowCount == 2 ? 0f : 0f);
            SetDigit(row.number1Image, s[0], numberSprites);
            SetDigit(row.number2Image, s[1], numberSprites);
            SetDigit(row.number3Image, s[2], numberSprites);
        }
        else if (s.Length >= 4)
        {
            // X1234
            // 3 rows: -10, 2 rows: 0, 1 row: 0
            if (row.layoutGroup != null)
                row.layoutGroup.spacing = totalRowCount == 3 ? -10.0f : 0f;
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

        row.Show();
        HideRowImages(row);

        // Set multiplier X
        SetImage(row.multiplierImage, multiplierSprite);

        // Check if number has decimals
        bool hasDecimal = (multiplier % 1 != 0);

        if (!hasDecimal)
        {
            // Treat as integer (e.g., 2.0 → "2", 12.0 → "12")
            int wholeValue = Mathf.RoundToInt(multiplier);
            SetupRow(row, wholeValue, numberSprites, multiplierSprite, isWonState, totalRowCount);
            return;
        }

        // Has decimal - format with 1 decimal place
        Sprite dotSprite = isWonState ? greenDotSprite : brownDotSprite;
        string formatted = multiplier.ToString("F1");
        string[] parts = formatted.Split('.');
        string whole = parts[0];
        char dec = parts.Length > 1 ? parts[1][0] : '0';

        if (whole.Length == 1)
        {
            // X2.5 (single digit + decimal)
            // 3 rows: -10, 2 rows: -5, 1 row: 0
            if (row.layoutGroup != null)
                row.layoutGroup.spacing = totalRowCount == 3 ? -10.0f : (totalRowCount == 2 ? -5.0f : 0f);
            SetDigit(row.number1Image, whole[0], numberSprites);
            SetImage(row.number2Image, dotSprite);
            SetDigit(row.number3Image, dec, numberSprites);
        }
        else if (whole.Length == 2)
        {
            // X12.5 (two digits + decimal)
            // 3 rows: -10, 2 rows: -5, 1 row: 0
            if (row.layoutGroup != null)
                row.layoutGroup.spacing = totalRowCount == 3 ? -10.0f : (totalRowCount == 2 ? -5.0f : 0f);
            SetDigit(row.number1Image, whole[0], numberSprites);
            SetDigit(row.number2Image, whole[1], numberSprites);
            SetImage(row.number3Image, dotSprite);
            SetDigit(row.number4Image, dec, numberSprites);
        }
        else if (whole.Length >= 3)
        {
            // X123 or X123.5 (three+ digits)
            // 3 rows: -10, 2 rows: 0, 1 row: 0
            if (row.layoutGroup != null)
                row.layoutGroup.spacing = totalRowCount == 3 ? -10.0f : 0f;
            SetDigit(row.number1Image, whole[0], numberSprites);
            SetDigit(row.number2Image, whole[1], numberSprites);
            SetDigit(row.number3Image, whole[2], numberSprites);
            if (whole.Length >= 4)
            {
                SetDigit(row.number4Image, whole[3], numberSprites);
            }
        }
    }

    private void HideRowImages(IndicatorRow row)
    {
        if (row == null) return;

        Hide(row.multiplierImage);
        Hide(row.number1Image);
        Hide(row.number2Image);
        Hide(row.number3Image);
        Hide(row.number4Image);
    }

    private void SetImage(Image img, Sprite sprite)
    {
        if (img == null || sprite == null) return;
        img.sprite = sprite;
        img.gameObject.SetActive(true);

        // Mark as a dot sprite (not a digit)
        DigitTracker tracker = img.GetComponent<DigitTracker>();
        if (tracker == null)
            tracker = img.gameObject.AddComponent<DigitTracker>();
        tracker.isDotSprite = true;
        tracker.digitIndex = -1;
    }

    private void SetDigit(Image img, char digit, Sprite[] sprites)
    {
        if (img == null || sprites == null) return;
        int idx = digit - '0';
        if (idx < 0 || idx >= sprites.Length) return;
        img.sprite = sprites[idx];
        img.gameObject.SetActive(true);

        // Store the digit index as a tag for reliable swapping later
        DigitTracker tracker = img.GetComponent<DigitTracker>();
        if (tracker == null)
            tracker = img.gameObject.AddComponent<DigitTracker>();
        tracker.digitIndex = idx;
    }

    private void Hide(Image img)
    {
        if (img != null) img.gameObject.SetActive(false);
    }
    #endregion

    #region Private Methods - Animation Helpers
    /// <summary>
    /// Swap all rows' sprites to green versions
    /// </summary>
    private void SwapAllRowsToGreen(Sprite[] greenNumberSprites, Sprite greenMultiplierSprite, Sprite greenDotSprite)
    {
        foreach (var row in allRows)
        {
            if (row == null) continue;

            // Change multiplier X to green
            if (row.multiplierImage != null && greenMultiplierSprite != null)
            {
                row.multiplierImage.sprite = greenMultiplierSprite;
            }

            // Change all active number images to green
            SwapRowNumbersToGreen(row, greenNumberSprites, greenDotSprite);
        }
    }

    /// <summary>
    /// Swap a single row's number sprites to green
    /// </summary>
    private void SwapRowNumbersToGreen(IndicatorRow row, Sprite[] greenNumberSprites, Sprite greenDotSprite)
    {
        if (row == null) return;

        // Check each number image and swap if active
        SwapSpriteIfActive(row.number1Image, greenNumberSprites, greenDotSprite);
        SwapSpriteIfActive(row.number2Image, greenNumberSprites, greenDotSprite);
        SwapSpriteIfActive(row.number3Image, greenNumberSprites, greenDotSprite);
        SwapSpriteIfActive(row.number4Image, greenNumberSprites, greenDotSprite);
    }

    private void SwapSpriteIfActive(Image img, Sprite[] greenNumberSprites, Sprite greenDotSprite)
    {
        if (img == null || !img.gameObject.activeSelf) return;

        // Get the stored digit tracker
        DigitTracker tracker = img.GetComponent<DigitTracker>();

        if (tracker == null)
        {
            Debug.LogWarning("[BonusIndicator] DigitTracker not found on image - cannot swap sprite");
            return;
        }

        // Check if this is a dot sprite
        if (tracker.isDotSprite)
        {
            if (greenDotSprite != null)
            {
                img.sprite = greenDotSprite;
            }
            return;
        }

        // It's a digit - swap to the corresponding green digit
        int digitIndex = tracker.digitIndex;
        if (digitIndex >= 0 && digitIndex < greenNumberSprites.Length && greenNumberSprites[digitIndex] != null)
        {
            img.sprite = greenNumberSprites[digitIndex];
        }
    }
    #endregion
}

/// <summary>
/// Helper component to track which digit (0-9) an Image represents
/// This ensures reliable sprite swapping without depending on sprite names
/// </summary>
internal class DigitTracker : MonoBehaviour
{
    internal int digitIndex = -1;  // 0-9 for digits, -1 for non-digits
    internal bool isDotSprite = false;  // true if this is a decimal dot
}