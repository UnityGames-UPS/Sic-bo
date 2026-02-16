using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

public class OrientationChange : MonoBehaviour
{
  [SerializeField] private RectTransform UIWrapper;
  [SerializeField] private CanvasScaler CanvasScaler;
  [SerializeField] private float MatchWidth = 0f;
  [SerializeField] private float MatchHeight = 1f;
  [SerializeField] private float PortraitMatchHeight = 1f;
  [SerializeField] private float transitionDuration = 0.2f;
  [SerializeField] private float waitForRotation = 0.2f;




  private Vector2 ReferenceAspect;
  private Tween matchTween;
  private Tween rotationTween;
  private Coroutine rotationRoutine;
  private bool isLandscape;
  private void Awake()
  {
    ReferenceAspect = CanvasScaler.referenceResolution;
  }

  void SwitchDisplay(string dimensions)
  {
    if (rotationRoutine != null) StopCoroutine(rotationRoutine);
    rotationRoutine = StartCoroutine(RotationCoroutine(dimensions));
  }

  IEnumerator RotationCoroutine(string dimensions)
  {
    yield return new WaitForSecondsRealtime(waitForRotation);
    string[] parts = dimensions.Split(',');
    if (parts.Length == 2 && int.TryParse(parts[0], out int width) && int.TryParse(parts[1], out int height) && width > 0 && height > 0)
    {
      Debug.LogWarning($"Unity: Received Dimensions - Width: {width}, Height: {height}");

      isLandscape = width > height;

      // Quaternion targetRotation = isLandscape ? Quaternion.identity : Quaternion.Euler(0, 0, -90);
      // if (rotationTween != null && rotationTween.IsActive()) rotationTween.Kill();
      // rotationTween = UIWrapper.DOLocalRotateQuaternion(targetRotation, transitionDuration).SetEase(Ease.OutCubic);

      float currentAspectRatio = isLandscape ? (float)width / height : (float)height / width;
      float referenceAspectRatio = ReferenceAspect.x / ReferenceAspect.y;
      Debug.LogWarning("currentAspect Ratio: " + currentAspectRatio);
      float targetMatch;

      // ---------------- LOG START ----------------
      Debug.Log("===== UI MATCH CALCULATOR START =====");
      Debug.Log($"isLandscape = {isLandscape}");
      Debug.Log($"currentAspectRatio = {currentAspectRatio}");
      Debug.Log($"referenceAspectRatio = {referenceAspectRatio}");

      // LANDSCAPE MODE ================================================
      if (isLandscape)
      {
        Debug.Log("→ Landscape mode detected");

        targetMatch = currentAspectRatio > referenceAspectRatio ? MatchHeight : MatchWidth;

        Debug.Log($"Landscape: Using {(currentAspectRatio > referenceAspectRatio ? "MatchHeight" : "MatchWidth")} = {targetMatch}");
      }
      // PORTRAIT MODE =================================================
      else
      {
        Debug.Log("→ Portrait mode detected");

        // iPad / Tablets (not phones)
        if (currentAspectRatio <= 1.35f)
        {
          targetMatch = 0.90f;
          Debug.LogWarning("[MATCH] Tablet/iPad (4:3) → 0.90");
        }
        else if (currentAspectRatio <= 1.45f)
        {
          targetMatch = 0.80f;
          Debug.LogWarning("[MATCH] Slightly tall tablet → 0.80");
        }
        // ------------------------------
        // ALL portrait phones = 0
        // ------------------------------
        else
        {
          targetMatch = 0f;
          Debug.LogWarning("[MATCH] Portrait PHONE → 0");
        }
      }

      // FINAL LOG =====================================================
      Debug.Log($"FINAL → targetMatch = {targetMatch}");
      Debug.Log("===== UI MATCH CALCULATOR END =====");


      if (matchTween != null && matchTween.IsActive()) matchTween.Kill();
      matchTween = DOTween.To(() => CanvasScaler.matchWidthOrHeight, x => CanvasScaler.matchWidthOrHeight = x, targetMatch, transitionDuration).SetEase(Ease.InOutQuad);

      Debug.LogWarning($"matchWidthOrHeight set to: {targetMatch}");
    }
    else
    {
      Debug.LogWarning("Unity: Invalid format received in SwitchDisplay");
    }
  }

#if UNITY_EDITOR
  private void Update()
  {
    if (Input.GetKeyDown(KeyCode.Space))
    {
      SwitchDisplay(Screen.width + "," + Screen.height);
    }
  }
#endif
}