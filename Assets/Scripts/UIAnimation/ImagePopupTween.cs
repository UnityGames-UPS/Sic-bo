using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[RequireComponent(typeof(Image))]
public class ImagePopupTween : MonoBehaviour
{
    public float popScale = 1.15f;

    public float popTime = 0.2f;
    public float visibleTime = 2.5f;   
    public float fadeTime = 0.4f;
    public float loopDelay = 2f;       

    Image img;
    RectTransform rect;
    Tween loopTween;

    void Awake()
    {
        img = GetComponent<Image>();
        rect = GetComponent<RectTransform>();
    }

    void OnEnable()
    {
        Play();
    }

    void OnDisable()
    {
        loopTween?.Kill();
    }

    public void Play()
    {
        loopTween?.Kill();

        loopTween = DOTween.Sequence()
            .SetLoops(-1)
            .AppendCallback(ResetState)

            // POP
            .Append(rect.DOScale(popScale, popTime).SetEase(Ease.OutBack))

            // WAIT
            .AppendInterval(visibleTime)

            // FADE
            .Append(img.DOFade(0, fadeTime))

            // EXTRA DELAY BEFORE NEXT LOOP
            .AppendInterval(loopDelay);
    }

    void ResetState()
    {
        rect.localScale = Vector3.zero;

        Color c = img.color;
        c.a = 1;
        img.color = c;
    }
}