using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class ClickRippleEffect : MonoBehaviour
{
    [Header("Ripple Settings")]
    [SerializeField] private GameObject ripplePrefab;   //  ripple prefab
    [SerializeField] private Canvas canvas;

    [Header("Animation")]
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private float startSize = 0f;
    [SerializeField] private float endSize = 80f;
    [SerializeField] private Color rippleColor = new Color(1f, 0.9f, 0f, 1f);
    private GameObject activeRipple; 

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            SpawnRipple(Input.mousePosition);
        }

        // Touch support
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            SpawnRipple(Input.GetTouch(0).position);
        }
    }


    private void SpawnRipple(Vector2 screenPosition)
    {

        if (activeRipple != null)
        {
            activeRipple.GetComponent<RectTransform>().DOKill();
            activeRipple.GetComponent<Image>().DOKill();
            Destroy(activeRipple);
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            screenPosition,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out Vector2 localPoint
        );

        activeRipple = Instantiate(ripplePrefab, canvas.transform);
        RectTransform rect = activeRipple.GetComponent<RectTransform>();
        rect.localPosition = localPoint;
        rect.sizeDelta = new Vector2(startSize, startSize);

        Image img = activeRipple.GetComponent<Image>();
        img.color = rippleColor;

        rect.DOSizeDelta(new Vector2(endSize, endSize), duration).SetEase(Ease.OutQuad);

        img.DOFade(0f, duration).SetEase(Ease.InQuad)
            .OnComplete(() =>
            {
                if (activeRipple != null) Destroy(activeRipple);
                activeRipple = null;
            });
    }
}