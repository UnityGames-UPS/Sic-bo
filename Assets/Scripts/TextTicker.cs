using UnityEngine;
using TMPro;
using DG.Tweening;

public class TextTicker : MonoBehaviour
{
    public RectTransform textRect;
    public RectTransform parentRect;

    public float speed = 120f;     
   

    float startX;
    float endX;
    float centerX;

    void Start()
    {
        SetupPositions();
        StartTicker();
    }

    void SetupPositions()
    {
        float parentWidth = parentRect.rect.width;
        float textWidth = textRect.rect.width;

        startX = parentWidth;        // start outside right
        endX = -textWidth;         // fully outside left
        centerX = (parentWidth - textWidth) / 2f;
    }

    void StartTicker()
    {
        float totalDistance = startX - endX;
        float duration = totalDistance / speed;

        Sequence seq = DOTween.Sequence();

        seq.AppendCallback(() =>
        {
            textRect.anchoredPosition =
                new Vector2(startX, textRect.anchoredPosition.y);
        });

        // move to center
        float centerDuration = (startX - centerX) / speed;
        seq.Append(textRect.DOAnchorPosX(centerX, centerDuration).SetEase(Ease.Linear));
        // continue left
        float endDuration = (centerX - endX) / speed;
        seq.Append(textRect.DOAnchorPosX(endX, endDuration).SetEase(Ease.Linear));
        // loop forever
        seq.SetLoops(-1);
    }
}
