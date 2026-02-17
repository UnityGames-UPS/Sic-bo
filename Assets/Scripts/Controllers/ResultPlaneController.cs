using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Manages the result history plane showing last 10 rounds.
///
/// LAYOUT (left → right):  Row[0]  Row[1]  ...  Row[9]   |  Row[10] hidden off-screen right
///
/// HOW IT WORKS (conveyor belt):
///   • Scene has 11 row GameObjects. Rows 0-9 visible with designer data. Row 10 off-screen right, hidden.
///   • slotPositions[0..10] are cached ONCE from the scene and NEVER modified — fixed screen coords.
///   • resultRows list rotates each cycle. After N cycles resultRows[i] is a different object,
///     but it always physically lives at slotPositions[i] before any slide starts.
///
///   On new result:
///     SNAP  — Instantly teleport every row to slotPositions[its list index].
///             This corrects any mid-animation drift if a previous cycle was interrupted.
///     STEP 1 — Write new data into resultRows[10] (hidden staging), scale-zero it, activate it.
///     STEP 2 — Slide ALL 11 rows left by rowWidth (from their now-correct snap positions).
///              Row[0] exits left. Row[10] enters at the Row[9] visual slot.
///     STEP 3 — Animate Row[10] elements scale 0→1 chain.
///     STEP 4 — Recycle: teleport Row[0] to slotPositions[10], hide it,
///              rotate resultRows list so it becomes the new Row[10].
///
///   NO DATA COPYING between rows. Each row keeps its own content.
/// </summary>
public class ResultPlaneController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Row References")]
    [SerializeField] private List<ResultRow> resultRows = new List<ResultRow>(); // 11 rows (0-10)

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

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    #endregion

    #region Private Fields
    // slotPositions[i] = the screen position of visual slot i.
    // FIXED forever after Start(). NEVER rotated or modified.
    // slotPositions[0..9] are the 10 visible columns, left to right.
    // slotPositions[10]   is the off-screen-right staging position.
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
        ValidateSetup();
        CacheSlotPositions();   // read scene positions once, lock them in
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
    private void ValidateSetup()
    {
        if (resultRows.Count != 11)
            Debug.LogError($"[ResultPlane] Expected 11 rows, found {resultRows.Count}. Add/remove rows in Inspector.");

        for (int i = 0; i < resultRows.Count; i++)
        {
            if (resultRows[i] == null)
                Debug.LogError($"[ResultPlane] resultRows[{i}] is null — assign it in Inspector!");
            else if (!resultRows[i].IsValid())
                Debug.LogError($"[ResultPlane] resultRows[{i}] ({resultRows[i].rowContainer?.name}) has missing UI references!");
        }
    }

    /// <summary>
    /// Reads every row's anchored position from the scene, stores them in slotPositions[],
    /// then runs a full layout audit:
    ///   • Logs each slot's name + X position so you can verify order in Console
    ///   • Detects duplicate X positions (two rows occupying the same visual slot)
    ///   • Detects non-uniform gaps (a slot is missing or misplaced)
    ///   • Verifies Row 10 is exactly one rowWidth right of Row 9 (staging requirement)
    /// </summary>
    private void CacheSlotPositions()
    {
        slotPositions = new Vector2[11];
        for (int i = 0; i < resultRows.Count; i++)
        {
            var rt = GetRT(resultRows[i]);
            if (rt != null)
                slotPositions[i] = rt.anchoredPosition;
        }

        // Derive rowWidth from slot 0→1 gap
        var rt0 = GetRT(resultRows[0]);
        var rt1 = GetRT(resultRows[1]);
        if (rt0 != null && rt1 != null)
            rowWidth = Mathf.Abs(rt1.anchoredPosition.x - rt0.anchoredPosition.x);

        if (rowWidth < 1f)
        {
            rowWidth = 100f;
            Debug.LogWarning("[ResultPlane] Could not read rowWidth from scene — using fallback 100. Check Row 0 and Row 1 positions.");
        }

        // ── Full layout audit ─────────────────────────────────────────────────
        ValidateSlotLayout();
    }

    /// <summary>
    /// Runs once at Start after CacheSlotPositions.
    /// Prints every slot and flags any structural problems that would break the conveyor belt.
    /// </summary>
    private void ValidateSlotLayout()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[ResultPlane] ══ SLOT LAYOUT AUDIT  rowWidth={rowWidth} ══");

        bool layoutOk = true;

        // ── 1. Print every slot ───────────────────────────────────────────────
        for (int i = 0; i < 11; i++)
        {
            string name = resultRows[i]?.rowContainer?.name ?? "<null>";
            string tag = i == 10 ? " ← STAGING (hidden)" : "";
            sb.AppendLine($"  slot[{i:D2}]  x={slotPositions[i].x,8:F1}  y={slotPositions[i].y,8:F1}  \"{name}\"{tag}");
        }

        // ── 2. Duplicate X check (two rows at same position) ─────────────────
        sb.AppendLine("  ── Duplicate X check ──");
        bool anyDupe = false;
        for (int i = 0; i < 11; i++)
        {
            for (int j = i + 1; j < 11; j++)
            {
                if (Mathf.Abs(slotPositions[i].x - slotPositions[j].x) < 0.5f)
                {
                    sb.AppendLine($"  ✗ DUPLICATE: slot[{i}] and slot[{j}] share x≈{slotPositions[i].x:F1}  " +
                                  $"(\"{resultRows[i].rowContainer?.name}\" / \"{resultRows[j].rowContainer?.name}\")");
                    anyDupe = true;
                    layoutOk = false;
                }
            }
        }
        if (!anyDupe) sb.AppendLine("  ✓ No duplicates");

        // ── 3. Uniform gap check (slots 0-9 must be evenly spaced) ───────────
        sb.AppendLine("  ── Gap uniformity check (slots 0-9) ──");
        bool anyGap = false;
        for (int i = 1; i < 10; i++)
        {
            float gap = slotPositions[i].x - slotPositions[i - 1].x;
            if (Mathf.Abs(Mathf.Abs(gap) - rowWidth) > 0.5f)
            {
                sb.AppendLine($"  ✗ GAP MISMATCH at slot[{i - 1}]→slot[{i}]: gap={gap:F1}  expected±{rowWidth:F1}");
                anyGap = true;
                layoutOk = false;
            }
        }
        if (!anyGap) sb.AppendLine($"  ✓ All gaps uniform ({rowWidth:F1}px)");

        // ── 4. Staging row offset check (slot[10] must be rowWidth right of slot[9]) ──
        sb.AppendLine("  ── Staging offset check (slot[10] vs slot[9]) ──");
        float stagingGap = slotPositions[10].x - slotPositions[9].x;
        if (Mathf.Abs(Mathf.Abs(stagingGap) - rowWidth) > 0.5f)
        {
            sb.AppendLine($"  ✗ STAGING OFFSET WRONG: slot[9].x={slotPositions[9].x:F1}  slot[10].x={slotPositions[10].x:F1}  " +
                          $"gap={stagingGap:F1}  expected {rowWidth:F1}");
            sb.AppendLine($"    → Move \"{resultRows[10].rowContainer?.name}\" so its X = {slotPositions[9].x + rowWidth:F1}");
            layoutOk = false;
        }
        else
        {
            sb.AppendLine($"  ✓ Staging offset correct ({stagingGap:F1}px)");
        }

        // ── 5. Visibility check (row 10 should be hidden at start) ───────────
        sb.AppendLine("  ── Initial visibility check ──");
        bool r10active = resultRows[10]?.rowContainer?.activeSelf ?? false;
        if (r10active)
        {
            sb.AppendLine($"  ✗ Row 10 (\"{resultRows[10].rowContainer?.name}\") is ACTIVE — it must start hidden (SetActive false in scene)");
            layoutOk = false;
        }
        else
        {
            sb.AppendLine("  ✓ Row 10 starts hidden");
        }

        // ── 6. Summary ────────────────────────────────────────────────────────
        sb.AppendLine(layoutOk
            ? "  ══ LAYOUT OK — conveyor belt ready ══"
            : "  ══ LAYOUT HAS ERRORS — fix the above before running ══");

        // Log as one block so it's easy to read in Console
        if (layoutOk)
            Debug.Log(sb.ToString());
        else
            Debug.LogError(sb.ToString());
    }

    private void InitializeDisplay()
    {
        // Rows 0-9 already have designer data in the scene — just make sure they're shown
        for (int i = 0; i < 10; i++)
        {
            if (resultRows[i].rowContainer != null)
            {
                resultRows[i].rowContainer.SetActive(true);
                resultRows[i].SetScaleToOne();
            }
        }
        // Row 10 is the hidden staging slot
        if (resultRows[10].rowContainer != null)
            resultRows[10].rowContainer.SetActive(false);
    }
    #endregion

    #region Public API
    public void AddNewResult(DiceResultData resultData)
    {
        if (resultData == null) { Debug.LogError("[ResultPlane] null result"); return; }

        // Stop old coroutine completely — kills tweens and prevents stale Recycle call
        if (animCoroutine != null)
        {
            StopCoroutine(animCoroutine);
            animCoroutine = null;
        }
        slideSeq?.Kill();
        scaleSeq?.Kill();

        // If interrupted mid-cycle the list rotation may be one step behind.
        // Recycle now so the list index is always in sync before the next snap.
        if (isAnimating)
        {
            RecycleRow0();
            isAnimating = false;
        }

        if (enableDebugLogs)
            Debug.Log($"[ResultPlane] New result  sum={resultData.sum}  side={resultData.matchSide}");

        ResultData r = new ResultData
        {
            dice1 = resultData.dice1,
            dice2 = resultData.dice2,
            dice3 = resultData.dice3,
            sum = resultData.sum,
            matchSide = resultData.matchSide
        };

        animCoroutine = StartCoroutine(CR_SlideAndAnimate(r));
    }

    public void ClearAllResults()
    {
        if (animCoroutine != null) { StopCoroutine(animCoroutine); animCoroutine = null; }
        slideSeq?.Kill();
        scaleSeq?.Kill();
        isAnimating = false;

        // Restore every row to its original canonical position
        for (int i = 0; i < resultRows.Count; i++)
        {
            var rt = GetRT(resultRows[i]);
            if (rt != null) rt.anchoredPosition = slotPositions[i];
            if (resultRows[i].rowContainer != null)
                resultRows[i].rowContainer.SetActive(i < 10);
            resultRows[i].SetScaleToOne();
        }
    }
    #endregion

    #region Animation
    private IEnumerator CR_SlideAndAnimate(ResultData newResult)
    {
        isAnimating = true;

        // ── SNAP ────────────────────────────────────────────────────────────────
        // Instantly correct any drift from a killed mid-animation.
        // resultRows[i] should physically sit at slotPositions[i].
        // This is the KEY fix: slide tweens always start from exact known positions.
        for (int i = 0; i < 11; i++)
        {
            var rt = GetRT(resultRows[i]);
            if (rt != null)
                rt.anchoredPosition = slotPositions[i];
        }

        // ── STEP 1: Populate staging row (Row[10]) ───────────────────────────
        ResultRow staging = resultRows[10];
        staging.SetData(newResult, GetDiceSprite);
        staging.SetScaleToZero();
        staging.rowContainer.SetActive(true);   // must activate BEFORE slide so it moves too

        // ── STEP 2: Slide all 11 rows left by exactly rowWidth ───────────────
        // Starting from slotPositions[i], target is slotPositions[i] - (rowWidth, 0).
        // Using the snapped position as the tween start guarantees no accumulation.
        slideSeq = DOTween.Sequence();
        for (int i = 0; i < 11; i++)
        {
            var rt = GetRT(resultRows[i]);
            if (rt == null) continue;

            Vector2 from = slotPositions[i];                          // snapped start
            Vector2 to = from - new Vector2(rowWidth, 0f);         // one slot left

            // Tween from the snap position (not from current, which might drift)
            slideSeq.Join(rt.DOAnchorPos(to, slideDuration).From(from).SetEase(slideEase));
        }

        yield return slideSeq.WaitForCompletion();

        // ── STEP 3: Pop-in animation for the new entry (now at visual slot 9) ─
        AnimateRowElements(staging);
        yield return new WaitForSeconds(scaleAnimationDuration + chainDelay * 5f);

        // ── STEP 4: Recycle Row[0] as the new hidden staging Row[10] ─────────
        RecycleRow0();

        isAnimating = false;
        animCoroutine = null;
    }

    /// <summary>
    /// Chain-animates each UI element of a row from scale 0 → 1.
    /// </summary>
    private void AnimateRowElements(ResultRow row)
    {
        scaleSeq?.Kill();
        scaleSeq = DOTween.Sequence();
        float d = 0f;

        scaleSeq.InsertCallback(d, () =>
        {
            if (row.sumText != null)
                row.sumText.transform.DOScale(1f, scaleAnimationDuration).SetEase(scaleEase);
        });
        d += chainDelay;

        scaleSeq.InsertCallback(d, () =>
        {
            if (row.bigImage != null && row.bigImage.activeSelf)
                row.bigImage.transform.DOScale(1f, scaleAnimationDuration).SetEase(scaleEase);
            if (row.smallImage != null && row.smallImage.activeSelf)
                row.smallImage.transform.DOScale(1f, scaleAnimationDuration).SetEase(scaleEase);
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

    /// <summary>
    /// Recycles the current Row[0] as the new Row[10] (hidden staging slot).
    ///
    /// slotPositions is FIXED — slotPositions[10] is always the off-screen-right position.
    /// Only the resultRows LIST rotates. After this call:
    ///   resultRows[0]  = what was resultRows[1]
    ///   resultRows[10] = what was resultRows[0], teleported to slotPositions[10], hidden.
    /// </summary>
    private void RecycleRow0()
    {
        if (resultRows.Count != 11) return;

        ResultRow old0 = resultRows[0];

        // Teleport to the fixed staging position and hide
        var rt = GetRT(old0);
        if (rt != null)
            rt.anchoredPosition = slotPositions[10]; // always the same right-edge coord
        if (old0.rowContainer != null)
            old0.rowContainer.SetActive(false);

        // Rotate list only (slotPositions is never touched)
        resultRows.RemoveAt(0);
        resultRows.Add(old0);

        if (enableDebugLogs)
        {
            Debug.Log("[ResultPlane] Recycled row 0 → new row 10");
            ValidateRuntimeSlots();   // catch any drift or double-occupancy immediately
        }
    }

    /// <summary>
    /// Called after every RecycleRow0 (debug mode only).
    /// Checks that each of the 11 slots has exactly one row sitting at its position.
    /// Reports any slot that is empty (no row) or double-occupied (two rows).
    /// This catches position drift the moment it happens, not after 3 broken rounds.
    /// </summary>
    private void ValidateRuntimeSlots()
    {
        // For each canonical slot position, count how many rows are physically there
        int[] occupancy = new int[11];
        string[] occupantNames = new string[11];
        for (int i = 0; i < 11; i++) occupantNames[i] = "";

        for (int r = 0; r < resultRows.Count; r++)
        {
            var rt = GetRT(resultRows[r]);
            if (rt == null) continue;

            Vector2 pos = rt.anchoredPosition;
            bool matched = false;
            for (int s = 0; s < 11; s++)
            {
                if (Mathf.Abs(pos.x - slotPositions[s].x) < 1f &&
                    Mathf.Abs(pos.y - slotPositions[s].y) < 1f)
                {
                    occupancy[s]++;
                    occupantNames[s] += $" \"{resultRows[r].rowContainer?.name}\"";
                    matched = true;
                    break;
                }
            }
            if (!matched)
                Debug.LogWarning($"[ResultPlane] DRIFT: row \"{resultRows[r].rowContainer?.name}\" " +
                                 $"at ({pos.x:F1},{pos.y:F1}) does not match any slot!");
        }

        bool ok = true;
        for (int s = 0; s < 11; s++)
        {
            if (occupancy[s] == 0)
            {
                Debug.LogError($"[ResultPlane] EMPTY SLOT {s} at x={slotPositions[s].x:F1} — no row here!");
                ok = false;
            }
            else if (occupancy[s] > 1)
            {
                Debug.LogError($"[ResultPlane] DOUBLE OCCUPANCY slot {s} at x={slotPositions[s].x:F1} — rows:{occupantNames[s]}");
                ok = false;
            }
        }

        if (ok && enableDebugLogs)
            Debug.Log("[ResultPlane] Runtime slot check ✓ — all 11 slots have exactly one row");
    }
    #endregion

    #region Helpers
    private RectTransform GetRT(ResultRow row)
    {
        if (row?.rowContainer == null) return null;
        return row.rowContainer.GetComponent<RectTransform>();
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

    #region Nested Class — ResultRow
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

        private Transform _t;
        public Transform transform
        {
            get { if (_t == null && rowContainer != null) _t = rowContainer.transform; return _t; }
        }

        public bool IsValid() =>
            rowContainer != null && sumText != null && bigImage != null &&
            smallImage != null && dice1Image != null && dice2Image != null && dice3Image != null;

        public void SetData(ResultData data, System.Func<int, Sprite> getDiceSprite)
        {
            if (sumText != null)
            {
                sumText.text = data.sum.ToString();
                sumText.color = (data.sum % 2 == 0)
                    ? new Color(0.1f, 0.1f, 0.1f, 1f)
                    : new Color(0.8f, 0.1f, 0.1f, 1f);
            }

            bool isBig = data.matchSide == "big";
            bool isSmall = data.matchSide == "small";
            if (bigImage != null) bigImage.SetActive(isBig);
            if (smallImage != null) smallImage.SetActive(isSmall);

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