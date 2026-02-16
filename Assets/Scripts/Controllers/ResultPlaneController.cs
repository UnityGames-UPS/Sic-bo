using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Manages the result history plane showing last 10 rounds
/// Each row displays: Sum (with background color), BIG/SMALL indicators, and 3 dice
/// Rows 0-9 are visible, Row 10 is hidden and used for recycling
/// When new result arrives: all rows slide left, newest animates at position 9
/// </summary>
public class ResultPlaneController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Row References")]
    [SerializeField] private List<ResultRow> resultRows = new List<ResultRow>(); // 11 rows (0-10)

    [Header("Slide Animation Settings")]
    [SerializeField] private float slideDuration = 0.3f;
    [SerializeField] private Ease slideEase = Ease.OutCubic;

    [Header("Scale Animation Settings")]
    [SerializeField] private float scaleAnimationDuration = 0.2f;
    [SerializeField] private float chainDelay = 0.05f; // Delay between each element
    [SerializeField] private Ease scaleEase = Ease.OutBack;

    [Header("Colors")]
    [SerializeField] private Color evenSumColor = new Color(0.1f, 0.1f, 0.1f, 1f); // Black for even
    [SerializeField] private Color oddSumColor = new Color(0.8f, 0.1f, 0.1f, 1f);  // Red for odd

    [Header("Dice Sprites")]
    [SerializeField] private Sprite dice1Sprite;
    [SerializeField] private Sprite dice2Sprite;
    [SerializeField] private Sprite dice3Sprite;
    [SerializeField] private Sprite dice4Sprite;
    [SerializeField] private Sprite dice5Sprite;
    [SerializeField] private Sprite dice6Sprite;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    #endregion

    #region Private Fields
    private Sequence currentAnimationSequence;
    private Vector2[] originalRowPositions; // Store original local positions
    private float rowWidth; // Width of one row for sliding calculation
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        ValidateSetup();
        CacheRowPositions();
        InitializeDisplay();
    }

    private void OnDestroy()
    {
        currentAnimationSequence?.Kill();
    }
    #endregion

    #region Initialization
    /// <summary>
    /// Validate that all rows are properly set up
    /// </summary>
    private void ValidateSetup()
    {
        if (resultRows.Count != 11)
        {
            Debug.LogError($"[ResultPlane] Expected 11 rows, found {resultRows.Count}");
            return;
        }

        for (int i = 0; i < resultRows.Count; i++)
        {
            if (resultRows[i] == null)
            {
                Debug.LogError($"[ResultPlane] Row {i} is null!");
                continue;
            }

            if (!resultRows[i].IsValid())
            {
                Debug.LogError($"[ResultPlane] Row {i} has missing references!");
            }
        }
    }

    /// <summary>
    /// Cache original positions of all rows for smooth sliding
    /// </summary>
    private void CacheRowPositions()
    {
        originalRowPositions = new Vector2[11];

        for (int i = 0; i < resultRows.Count; i++)
        {
            if (resultRows[i].rowContainer != null)
            {
                RectTransform rectTransform = resultRows[i].rowContainer.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    originalRowPositions[i] = rectTransform.anchoredPosition;
                }
            }
        }

        // Calculate row width based on the distance between row 0 and row 1
        if (resultRows.Count >= 2 && resultRows[0].rowContainer != null && resultRows[1].rowContainer != null)
        {
            RectTransform rect0 = resultRows[0].rowContainer.GetComponent<RectTransform>();
            RectTransform rect1 = resultRows[1].rowContainer.GetComponent<RectTransform>();

            if (rect0 != null && rect1 != null)
            {
                rowWidth = Mathf.Abs(rect1.anchoredPosition.x - rect0.anchoredPosition.x);
                if (enableDebugLogs)
                    Debug.Log($"[ResultPlane] Calculated row width: {rowWidth}");
            }
        }

        if (rowWidth == 0)
        {
            rowWidth = 100f; // Fallback default
            Debug.LogWarning("[ResultPlane] Could not calculate row width, using default 100");
        }
    }

    /// <summary>
    /// Initialize display - rows 0-9 visible, row 10 hidden
    /// NO dummy data - assumes data already exists in scene
    /// </summary>
    private void InitializeDisplay()
    {
        if (enableDebugLogs)
            Debug.Log("[ResultPlane] Initializing display - rows 0-9 visible, row 10 hidden");

        // Rows 0-9 should already have data in the scene
        // Just ensure they're visible and at correct scale
        for (int i = 0; i < 10; i++)
        {
            if (resultRows[i].rowContainer != null)
            {
                resultRows[i].rowContainer.SetActive(true);
                resultRows[i].SetScaleToOne(); // Ensure all elements are at scale 1
            }
        }

        // Row 10 should be hidden
        if (resultRows[10].rowContainer != null)
        {
            resultRows[10].rowContainer.SetActive(false);
        }
    }
    #endregion

    #region Public API
    /// <summary>
    /// Add new result - called when game:dice_result is received
    /// All rows slide left, newest animates at position 9
    /// </summary>
    public void AddNewResult(DiceResultData resultData)
    {
        if (resultData == null)
        {
            Debug.LogError("[ResultPlane] Cannot add null result data");
            return;
        }

        if (enableDebugLogs)
            Debug.Log($"[ResultPlane] Adding result: dice=[{resultData.dice1},{resultData.dice2},{resultData.dice3}] sum={resultData.sum} side={resultData.matchSide}");

        // Convert to internal format
        ResultData newResult = new ResultData
        {
            dice1 = resultData.dice1,
            dice2 = resultData.dice2,
            dice3 = resultData.dice3,
            sum = resultData.sum,
            matchSide = resultData.matchSide
        };

        // Start the slide and animate sequence
        StartCoroutine(SlideAndAnimateSequence(newResult));
    }

    /// <summary>
    /// Clear all results (useful for testing or reset)
    /// </summary>
    public void ClearAllResults()
    {
        // Hide all rows except 0-9
        for (int i = 0; i < 10; i++)
        {
            if (resultRows[i].rowContainer != null)
                resultRows[i].rowContainer.SetActive(false);
        }

        // Hide row 10
        if (resultRows[10].rowContainer != null)
            resultRows[10].rowContainer.SetActive(false);
    }
    #endregion

    #region Animation Logic
    /// <summary>
    /// Main animation sequence:
    /// 1. Update data in all rows (Row 1→0, Row 2→1, ... Row 10→9, new data→Row 10)
    /// 2. Slide all rows left smoothly
    /// 3. Animate Row 10's elements with chain effect at position 9
    /// 4. Recycle Row 0 to become new Row 10
    /// </summary>
    private IEnumerator SlideAndAnimateSequence(ResultData newResult)
    {
        // Kill any ongoing animation
        currentAnimationSequence?.Kill();

        // STEP 1: Update data in all rows (shift left)
        // Row 0 gets data from Row 1, Row 1 gets data from Row 2, etc.
        for (int i = 0; i < 9; i++)
        {
            CopyDataFromRowToRow(i + 1, i);
        }

        // Row 9 gets data from hidden Row 10
        CopyDataFromRowToRow(10, 9);

        // Row 10 gets the new result data
        resultRows[10].SetData(newResult, GetDiceSprite);
        resultRows[10].SetScaleToZero(); // Prepare for animation
        resultRows[10].rowContainer.SetActive(true); // Make visible

        // STEP 2: Slide all rows LEFT smoothly
        Sequence slideSequence = DOTween.Sequence();

        for (int i = 0; i < 11; i++)
        {
            if (resultRows[i].rowContainer != null)
            {
                RectTransform rectTransform = resultRows[i].rowContainer.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    // Calculate target position (one row width to the left)
                    Vector2 targetPosition = rectTransform.anchoredPosition - new Vector2(rowWidth, 0);

                    slideSequence.Join(
                        rectTransform.DOAnchorPos(targetPosition, slideDuration).SetEase(slideEase)
                    );
                }
            }
        }

        // Wait for slide to complete
        yield return slideSequence.WaitForCompletion();

        // STEP 3: Animate Row 10's elements with chain effect (now at position 9)
        AnimateRowElements(resultRows[10]);

        // Wait for scale animation to complete
        yield return new WaitForSeconds(scaleAnimationDuration + (chainDelay * 5));

        // STEP 4: Recycle Row 0
        RecycleRow0ToRow10();
    }

    /// <summary>
    /// Copy visual data from one row to another (for sliding effect)
    /// </summary>
    private void CopyDataFromRowToRow(int sourceIndex, int targetIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= resultRows.Count ||
            targetIndex < 0 || targetIndex >= resultRows.Count)
            return;

        ResultRow source = resultRows[sourceIndex];
        ResultRow target = resultRows[targetIndex];

        // Copy sum text
        if (source.sumText != null && target.sumText != null)
            target.sumText.text = source.sumText.text;

        // Copy BIG/SMALL visibility
        if (source.bigImage != null && target.bigImage != null)
            target.bigImage.SetActive(source.bigImage.activeSelf);
        if (source.smallImage != null && target.smallImage != null)
            target.smallImage.SetActive(source.smallImage.activeSelf);

        // Copy dice sprites
        if (source.dice1Image != null && target.dice1Image != null)
            target.dice1Image.sprite = source.dice1Image.sprite;
        if (source.dice2Image != null && target.dice2Image != null)
            target.dice2Image.sprite = source.dice2Image.sprite;
        if (source.dice3Image != null && target.dice3Image != null)
            target.dice3Image.sprite = source.dice3Image.sprite;

        // Ensure target is at scale 1
        target.SetScaleToOne();
    }

    /// <summary>
    /// Animate a row's elements with chain effect (scale 0 → 1)
    /// </summary>
    private void AnimateRowElements(ResultRow row)
    {
        currentAnimationSequence = DOTween.Sequence();

        float currentDelay = 0f;

        // 1. Sum text
        currentAnimationSequence.InsertCallback(currentDelay, () =>
        {
            if (row.sumText != null)
                row.sumText.transform.DOScale(1f, scaleAnimationDuration).SetEase(scaleEase);
        });
        currentDelay += chainDelay;

        // 2. Big/Small image
        currentAnimationSequence.InsertCallback(currentDelay, () =>
        {
            if (row.bigImage != null && row.bigImage.activeSelf)
                row.bigImage.transform.DOScale(1f, scaleAnimationDuration).SetEase(scaleEase);
            if (row.smallImage != null && row.smallImage.activeSelf)
                row.smallImage.transform.DOScale(1f, scaleAnimationDuration).SetEase(scaleEase);
        });
        currentDelay += chainDelay;

        // 3. Dice 1
        currentAnimationSequence.InsertCallback(currentDelay, () =>
        {
            if (row.dice1Image != null)
                row.dice1Image.transform.DOScale(1f, scaleAnimationDuration).SetEase(scaleEase);
        });
        currentDelay += chainDelay;

        // 4. Dice 2
        currentAnimationSequence.InsertCallback(currentDelay, () =>
        {
            if (row.dice2Image != null)
                row.dice2Image.transform.DOScale(1f, scaleAnimationDuration).SetEase(scaleEase);
        });
        currentDelay += chainDelay;

        // 5. Dice 3
        currentAnimationSequence.InsertCallback(currentDelay, () =>
        {
            if (row.dice3Image != null)
                row.dice3Image.transform.DOScale(1f, scaleAnimationDuration).SetEase(scaleEase);
        });
    }

    /// <summary>
    /// Recycle Row 0 to become new Row 10
    /// Move it to the far right and reset its position
    /// </summary>
    private void RecycleRow0ToRow10()
    {
        if (resultRows.Count != 11) return;

        ResultRow row0 = resultRows[0];

        // Reset Row 0's position to original Row 10 position
        if (row0.rowContainer != null)
        {
            RectTransform rectTransform = row0.rowContainer.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = originalRowPositions[10];
            }

            // Hide it
            row0.rowContainer.SetActive(false);

            // Move to end of hierarchy
            row0.rowContainer.transform.SetSiblingIndex(10);
        }

        // Update the list order
        resultRows.RemoveAt(0);
        resultRows.Add(row0);

        // Update cached positions array to match new order
        Vector2 temp = originalRowPositions[0];
        for (int i = 0; i < 10; i++)
        {
            originalRowPositions[i] = originalRowPositions[i + 1];
        }
        originalRowPositions[10] = temp;

        if (enableDebugLogs)
            Debug.Log("[ResultPlane] Recycled row 0 to row 10 position");
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Get dice sprite based on dice value
    /// </summary>
    private Sprite GetDiceSprite(int diceValue)
    {
        return diceValue switch
        {
            1 => dice1Sprite,
            2 => dice2Sprite,
            3 => dice3Sprite,
            4 => dice4Sprite,
            5 => dice5Sprite,
            6 => dice6Sprite,
            _ => null
        };
    }

    /// <summary>
    /// Get background color for sum text based on even/odd
    /// </summary>
    private Color GetSumBackgroundColor(int sum)
    {
        return (sum % 2 == 0) ? evenSumColor : oddSumColor;
    }
    #endregion

    #region Nested Class - Result Row
    [System.Serializable]
    public class ResultRow
    {
        [Header("Row Container")]
        public GameObject rowContainer; // The parent GameObject containing all elements

        [Header("UI Elements")]
        public TMP_Text sumText;
        public GameObject bigImage;
        public GameObject smallImage;
        public Image dice1Image;
        public Image dice2Image;
        public Image dice3Image;

        // Cached transform reference
        private Transform _transform;
        public Transform transform
        {
            get
            {
                if (_transform == null && rowContainer != null)
                    _transform = rowContainer.transform;
                return _transform;
            }
        }

        /// <summary>
        /// Check if all references are valid
        /// </summary>
        public bool IsValid()
        {
            return rowContainer != null &&
                   sumText != null &&
                   bigImage != null &&
                   smallImage != null &&
                   dice1Image != null &&
                   dice2Image != null &&
                   dice3Image != null;
        }

        /// <summary>
        /// Set data for this row
        /// </summary>
        public void SetData(ResultData data, System.Func<int, Sprite> getDiceSprite)
        {
            // Set sum text
            if (sumText != null)
                sumText.text = data.sum.ToString();
                Color bgColor = (data.sum % 2 == 0)
                    ? new Color(0.1f, 0.1f, 0.1f, 1f)  // Black for even
                    : new Color(0.8f, 0.1f, 0.1f, 1f); // Red for odd
                sumText.color = bgColor;
            

            // Set BIG/SMALL visibility
            bool isBig = data.matchSide == "big";
            if (bigImage != null) bigImage.SetActive(isBig);
            if (smallImage != null) smallImage.SetActive(!isBig);

            // Set dice sprites
            if (dice1Image != null) dice1Image.sprite = getDiceSprite(data.dice1);
            if (dice2Image != null) dice2Image.sprite = getDiceSprite(data.dice2);
            if (dice3Image != null) dice3Image.sprite = getDiceSprite(data.dice3);
        }

        /// <summary>
        /// Set all elements to scale 1 (visible, normal size)
        /// </summary>
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