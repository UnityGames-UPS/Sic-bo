using UnityEngine;

/// <summary>
/// Frame-driven mask with AnimationCurve for pixel-perfect speed matching.
///
/// WHY AnimationCurve:
///   The box lid in the animation does NOT move at constant speed — it has
///   acceleration, deceleration, and may pause mid-way. A simple Lerp(t) will
///   always be out of sync with the sprite. With an AnimationCurve you can
///   shape the exact speed profile in the Inspector to match frame-by-frame.
///
/// HOW TO TUNE THE CURVE:
///   1. Play the game and watch where the mask lags behind or runs ahead.
///   2. Open DiceMaskFollowPath → openingCurve in the Inspector.
///   3. X axis = normalized frame progress (0 = boxOpeningStartFrame, 1 = boxFullyOpenFrame)
///   4. Y axis = normalized mask position  (0 = MaskStartRect,        1 = MaskEndRect)
///   5. Add keyframes / adjust tangents until mask matches lid exactly.
///   Same process for closingCurve (X=0 is boxStartClosingFrame, X=1 is boxClosedFrame).
///
/// SCENE:
///   MaskContainer          ← RectMask2D  +  this script
///   └── DiceAnchor         ← empty RectTransform, parent of Dice1/2/3
///           ├── Dice1
///           ├── Dice2
///           └── Dice3
///   MaskStartRect          ← placed at box-lid position at boxOpeningStartFrame
///   MaskEndRect            ← placed at box-lid position at boxFullyOpenFrame
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DiceMaskFollowPath : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform maskRect;
    [SerializeField] private RectTransform diceAnchor;

    [Header("Path Points — RectTransforms inside Canvas")]
    [Tooltip("Mask position when box first starts opening (boxOpeningStartFrame).")]
    [SerializeField] private RectTransform maskStartRect;

    [Tooltip("Mask position when box is fully open (boxFullyOpenFrame).")]
    [SerializeField] private RectTransform maskEndRect;

    [Header("Opening Curve")]
    [Tooltip("X = normalized frame progress (0→1 across opening frames)\n" +
             "Y = normalized mask position  (0=MaskStart, 1=MaskEnd)\n\n" +
             "Shape this curve to match how fast the box lid actually moves in the sprite frames.\n" +
             "Default is linear — add ease-in/out keyframes to match the animation.")]
    [SerializeField] private AnimationCurve openingCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Closing Curve")]
    [Tooltip("X = normalized frame progress (0→1 across closing frames)\n" +
             "Y = normalized mask position  (0=MaskEnd back toward MaskStart, 1=MaskStart)\n\n" +
             "Shape this to match the closing animation speed.")]
    [SerializeField] private AnimationCurve closingCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Scale-Up During Opening")]
    [Tooltip("Scale the mask (and its dice children) will reach by the end of the scale-up range.\n" +
             "Original scale is always 1,1,1 and is restored on every round reset.\n" +
             "The frame range that drives this is set in DiceBoxAnimationController\n" +
             "(boxScaleUpStartFrame / boxScaleUpEndFrame) — same pattern as the mask open/close frames.")]
    [SerializeField] private Vector3 targetScale = new Vector3(1.8f, 1.8f, 1.8f);

    // ── Scale constants ───────────────────────────────────────────────────────
    private static readonly Vector3 OriginalScale = Vector3.one;

    // ── Cached ────────────────────────────────────────────────────────────────
    private Vector2 _maskStartPos;
    private Vector2 _maskEndPos;

    // ── Unity ─────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (maskRect == null)
            maskRect = GetComponent<RectTransform>();
    }

    private void Start()
    {
        _maskStartPos = maskStartRect != null
                        ? maskStartRect.anchoredPosition
                        : maskRect.anchoredPosition;

        _maskEndPos = maskEndRect != null
                      ? maskEndRect.anchoredPosition
                      : maskRect.anchoredPosition;

        maskRect.anchoredPosition = _maskStartPos;

        Debug.Log($"[DiceMaskFollowPath] Start={_maskStartPos}  End={_maskEndPos}");
    }

    private void LateUpdate()
    {
        if (diceAnchor == null || maskRect == null) return;

        // Dice always appear at _maskEndPos on screen:
        // screen_pos = maskRect.anchoredPos + diceAnchor.anchoredPos = _maskEndPos
        // → diceAnchor.anchoredPos = _maskEndPos - maskRect.anchoredPos
        diceAnchor.anchoredPosition = _maskEndPos - maskRect.anchoredPosition;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called per-frame during Opening sequence.
    /// rawT = (currentFrame - boxOpeningStartFrame) / (boxFullyOpenFrame - boxOpeningStartFrame)
    /// The openingCurve remaps rawT so the mask position matches the lid movement.
    /// </summary>
    public void SetOpenProgress(float rawT)
    {
        if (maskRect == null) return;
        float curvedT = openingCurve.Evaluate(Mathf.Clamp01(rawT));
        maskRect.anchoredPosition = Vector2.Lerp(_maskStartPos, _maskEndPos, curvedT);
    }

    /// <summary>
    /// Called per-frame during Closing sequence.
    /// rawT = (currentFrame - boxStartClosingFrame) / (boxClosedFrame - boxStartClosingFrame)
    /// The closingCurve remaps rawT so the mask position matches the lid movement.
    /// </summary>
    public void SetCloseProgress(float rawT)
    {
        if (maskRect == null) return;
        float curvedT = closingCurve.Evaluate(Mathf.Clamp01(rawT));
        maskRect.anchoredPosition = Vector2.Lerp(_maskEndPos, _maskStartPos, curvedT);
    }

    /// <summary>
    /// Called per-frame during Opening sequence to scale the mask (and its dice children).
    /// rawT = (currentFrame - boxScaleUpStartFrame) / (boxScaleUpEndFrame - boxScaleUpStartFrame)
    ///
    /// The frame range and rawT computation live in DiceBoxAnimationController
    /// (boxScaleUpStartFrame / boxScaleUpEndFrame) — same pattern as SetOpenProgress / SetCloseProgress.
    ///   rawT = 0 → localScale = 1,1,1   (original)
    ///   rawT = 1 → localScale = targetScale (1.8,1.8,1.8)
    /// </summary>
    public void SetScaleProgress(float rawT)
    {
        if (maskRect == null) return;
        maskRect.localScale = Vector3.Lerp(OriginalScale, targetScale, Mathf.Clamp01(rawT));
    }

    /// <summary>
    /// Snap mask back to start (round reset / ForceHide).
    /// Also resets scale to original 1,1,1.
    /// </summary>
    public void ResetToStart()
    {
        if (maskRect != null)
        {
            maskRect.anchoredPosition = _maskStartPos;
            maskRect.localScale = OriginalScale;
        }
    }

#if UNITY_EDITOR
    // ── Editor helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Called by DiceBoxAnimationController context menu.
    /// Builds and assigns the opening curve from the real frame numbers so you
    /// never have to hand-tune keyframe tangents.
    /// </summary>
    public void EditorSetOpeningCurve(AnimationCurve curve)
    {
        openingCurve = curve;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    /// <summary>
    /// Called by DiceBoxAnimationController context menu.
    /// </summary>
    public void EditorSetClosingCurve(AnimationCurve curve)
    {
        closingCurve = curve;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    /// <summary>
    /// Call from a custom Editor button or from the Inspector context menu
    /// to auto-generate a default EaseInOut curve as a starting point.
    /// </summary>
    [ContextMenu("Reset Opening Curve to EaseInOut")]
    private void ResetOpeningCurve()
    {
        openingCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.3f, 0.05f, 0f, 0f),   // slow start  (lid barely moves at first)
            new Keyframe(0.6f, 0.7f, 0f, 0f),    // fast middle (lid swings up quickly)
            new Keyframe(1f, 1f, 0f, 0f)          // slow end    (lid settles open)
        );
    }

    [ContextMenu("Reset Closing Curve to EaseInOut")]
    private void ResetClosingCurve()
    {
        closingCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.4f, 0.8f, 0f, 0f),    // fast initial drop
            new Keyframe(1f, 1f, 0f, 0f)
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (maskStartRect != null)
        {
            UnityEditor.Handles.color = Color.cyan;
            UnityEditor.Handles.Label(maskStartRect.position, "MaskStart");
        }
        if (maskEndRect != null)
        {
            UnityEditor.Handles.color = Color.green;
            UnityEditor.Handles.Label(maskEndRect.position, "MaskEnd");
        }
    }
#endif
}