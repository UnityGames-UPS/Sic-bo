using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.TextCore.Text;

public class ComingSoonEffect : MonoBehaviour
{
    #region Serialized Fields
    [Header("Canvas")]
    [Tooltip("The root Canvas used to parent the effect labels. Auto-detected if left blank.")]
    [SerializeField] private Canvas targetCanvas;

    [Header("Text Settings")]
    [SerializeField] private string displayText = "Coming Soon";
    [SerializeField] private float fontSize = 28f;
    [SerializeField] private Color textColor = new Color(1f, 0.85f, 0.2f, 1f); // Golden yellow
    [SerializeField] private TMP_FontAsset customFont;

    [Header("Smoke Puff Settings")]
    [Tooltip("How many text copies spawn per click for the smoke-cloud effect.")]
    [SerializeField] private int puffCount = 4;
    [SerializeField] private float puffStagger = 0.07f;      // Delay between each puff
    [SerializeField] private float riseHeight = 120f;         // How far up the text travels
    [SerializeField] private float riseSpread = 35f;          // Horizontal scatter per puff
    [SerializeField] private float riseDuration = 1.0f;       // Time to reach top
    [SerializeField] private float fadeDelay = 0.35f;         // Wait before starting to fade
    [SerializeField] private float fadeDuration = 0.55f;      // Fade-out duration
    [SerializeField] private float scaleStart = 0.5f;         // Starting scale (pops in)
    [SerializeField] private float scaleEnd = 1.4f;           // Ending scale (grows as it fades)
    [SerializeField] private Ease riseEase = Ease.OutCubic;

    [Header("Spawn Offset")]
    [Tooltip("Offset in canvas-space from the button's center where puffs originate.")]
    [SerializeField] private Vector2 spawnOffset = new Vector2(0f, 30f);

    [Header("Block During Animation")]
    [Tooltip("Prevent multiple clicks while effect is playing.")]
    [SerializeField] private bool blockDuringAnimation = false;
    #endregion

    #region Private Fields
    private Button attachedButton;
    private bool isAnimating = false;
    private List<GameObject> activePuffs = new List<GameObject>();
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        attachedButton = GetComponent<Button>();

        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>();

        if (attachedButton != null)
            attachedButton.onClick.AddListener(OnButtonClicked);
    }

    private void OnDestroy()
    {
        // Clean up any live puffs
        foreach (var puff in activePuffs)
            if (puff != null) Destroy(puff);
        activePuffs.Clear();

        if (attachedButton != null)
            attachedButton.onClick.RemoveListener(OnButtonClicked);
    }
    #endregion

    #region Public API
    /// <summary>
    /// Trigger the effect manually (e.g. from another script).
    /// </summary>
    public void TriggerEffect()
    {
        OnButtonClicked();
    }
    #endregion

    #region Private Methods
    private void OnButtonClicked()
    {
        if (blockDuringAnimation && isAnimating) return;

        StartCoroutine(CR_SpawnSmokePuffs());
    }

    private IEnumerator CR_SpawnSmokePuffs()
    {
        isAnimating = true;

        // Get the spawn position in canvas space
        Vector2 originPos = GetCanvasPosition(GetComponent<RectTransform>()) + spawnOffset;

        for (int i = 0; i < puffCount; i++)
        {
            SpawnPuff(originPos, i);
            yield return new WaitForSeconds(puffStagger);
        }

        // Wait for the longest possible animation to finish before unlocking
        float totalDuration = riseDuration + fadeDelay + fadeDuration + puffStagger * puffCount;
        yield return new WaitForSeconds(totalDuration);

        isAnimating = false;
    }

    private void SpawnPuff(Vector2 originPos, int puffIndex)
    {
        if (targetCanvas == null) return;

        // --- Create GameObject ---
        GameObject puffObj = new GameObject($"ComingSoonPuff_{puffIndex}");
        RectTransform rt = puffObj.AddComponent<RectTransform>();
        rt.SetParent(targetCanvas.transform, false);
        rt.sizeDelta = new Vector2(300f, 60f);
        rt.anchoredPosition = originPos;
        rt.localScale = Vector3.one * scaleStart;

        // Make sure it renders on top
        rt.SetAsLastSibling();

        // --- CanvasGroup for alpha ---
        CanvasGroup cg = puffObj.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        // --- TMP_Text ---
        TMP_Text label = puffObj.AddComponent<TextMeshProUGUI>();
        label.text = displayText;
        label.fontSize = fontSize;
        label.color = textColor;
        label.font = customFont;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;

        activePuffs.Add(puffObj);

        // --- Horizontal scatter: alternates left/right, grows with puff index ---
        float xScatter = (puffIndex % 2 == 0 ? 1f : -1f) * Random.Range(0f, riseSpread * (0.3f + puffIndex * 0.25f));

        // --- Animate: rise + scale grow ---
        Vector2 targetPos = originPos + new Vector2(xScatter, riseHeight);

        rt.DOAnchorPos(targetPos, riseDuration).SetEase(riseEase);
        rt.DOScale(scaleEnd, riseDuration).SetEase(Ease.OutQuad);

        // --- Fade out after delay ---
        cg.DOFade(0f, fadeDuration)
            .SetDelay(fadeDelay)
            .SetEase(Ease.InQuad)
            .OnComplete(() =>
            {
                activePuffs.Remove(puffObj);
                if (puffObj != null) Destroy(puffObj);
            });
    }

    /// <summary>
    /// Convert a RectTransform's world position to canvas-space anchoredPosition
    /// </summary>
    private Vector2 GetCanvasPosition(RectTransform rt)
    {   
        if (rt == null || targetCanvas == null) return Vector2.zero;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            targetCanvas.worldCamera, rt.position);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetCanvas.GetComponent<RectTransform>(),
            screenPoint,
            targetCanvas.worldCamera,
            out Vector2 localPoint);

        return localPoint;
    }
    #endregion
}