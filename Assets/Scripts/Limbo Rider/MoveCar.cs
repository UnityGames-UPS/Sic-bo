using UnityEngine;
using DG.Tweening;

public class MOveCar : MonoBehaviour
{
    public Transform model;
    public float moveDuration = 1f;
    public float shrinkScale = 0.15f;

    private Vector3 startPos = new Vector3(0, -150, 0);
    private Vector3 endPos = new Vector3(0, -20, 0);
    private Tween revBounceTween;

    public void CarAnim()
    {
        StartRev();
        Invoke("StopRev", 3f);
        Invoke("MoveCarAnim", 3f + moveDuration);
    }


    public void StartRev(float tiltAngle = 1f)
    {
        if (model == null)
        {
            Debug.LogError("Model not assigned!");
            return;
        }

        // Reset
        model.localPosition = startPos;
        model.localScale = Vector3.one;
        model.localRotation = Quaternion.identity;

        // Loop rotation left-right (rev effect)
        model.DOLocalRotate(new Vector3(0, 0, tiltAngle), 0.001f)
             .SetLoops(-1, LoopType.Yoyo)
             .SetEase(Ease.InOutSine)
             .SetId("RevTween");

        // 🚗 Suspension bounce effect (down by -3 on Y)
        model.DOLocalMoveY(startPos.y - 3f, 0.2f).SetEase(Ease.OutSine);
    }

    // 🛑 Stop rev and reset rotation + position
    public void StopRev()
    {
        DOTween.Kill("RevTween");
        DOTween.Kill("RevBounceTween");

        // Smoothly return to start position
        model.DOLocalMove(startPos, 0.3f).SetEase(Ease.OutSine);
        model.DOLocalRotate(Vector3.zero, 0.2f).SetEase(Ease.OutSine);
    }

    public void MoveCarAnim()
    {
        if (model == null)
        {
            Debug.LogError("Model not assigned!");
            return;
        }

        // Reset before anim
        model.localPosition = startPos;
        model.localScale = Vector3.one;
        model.localRotation = Quaternion.identity;

        Sequence seq = DOTween.Sequence();

        // 🚗 Move upward and shrink at the same time
        seq.Append(model.DOLocalMove(endPos, moveDuration).SetEase(Ease.OutCubic));
        seq.Join(model.DOScale(Vector3.one * shrinkScale, moveDuration).SetEase(Ease.OutCubic));

        // Optional reset after complete (remove if you want it to stay at endPos)
        seq.OnComplete(() =>
        {
            ResetCar();
        });
    }

    private void ResetCar()
    {
        model.localPosition = startPos;
        model.localScale = Vector3.one;
        model.localRotation = Quaternion.identity;
    }
}
