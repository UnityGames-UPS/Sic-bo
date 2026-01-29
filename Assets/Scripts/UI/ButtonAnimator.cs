using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using UnityEngine.UI; // For DOTween

public class ButtonAnimator : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Transform buttonTransform; // Assign the Button's transform in the Inspector.
    private Button button;
    private float OrignalScale;
    private void OnValidate() {
        if (buttonTransform == null)
        {
            buttonTransform = transform; // Default to this GameObject's transform.
            OrignalScale=transform.localScale.x;
            button = transform.GetComponent<Button>();
        }
    }

    private void Start() {
        if (buttonTransform == null)
        {
            buttonTransform = transform; // Default to this GameObject's transform.
            OrignalScale=transform.localScale.x;
            button = transform.GetComponent<Button>();
        }   
    }

    // Called when the button is pressed.
    public void OnPointerDown(PointerEventData eventData)
    {
        // Debug.Log("Pointer Down");
        if(button.interactable)
            PressedAnimation(buttonTransform);
    }

    // Called when the button is released.
    public void OnPointerUp(PointerEventData eventData)
    {
        // Debug.Log("Pointer Up");
        if(button.interactable)
            OnClickedAnimation(buttonTransform);
    }

    void PressedAnimation(Transform transform)
    {
        transform.DOScale(0.8f, 0.2f); // Scale down on press.
    }

    void OnClickedAnimation(Transform transform)
    {
        transform.DOScale(OrignalScale, 0.2f); // Scale back to normal size after release.
    }
}
