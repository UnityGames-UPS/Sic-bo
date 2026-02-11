using UnityEngine;
using UnityEngine.UI;

public class BonusIndicator : MonoBehaviour
{
    #region Serialized Fields
    [Header("Images (children, in display order)")]
    [SerializeField] private Image multiplierImage;   // "X" prefix – always shown
    [SerializeField] private Image number1Image;      // first digit / whole part digit 1
    [SerializeField] private Image number2Image;      // second digit OR decimal dot
    [SerializeField] private Image number3Image;      // decimal digit (after dot)
    #endregion

    #region Internal State
    internal float multiplier;
    internal bool isWon;
    internal string betOption;
    #endregion

    #region Auto-find
    private void OnValidate()
    {
        if (multiplierImage == null) TryFind("MultiplierX", ref multiplierImage);
        if (number1Image == null) TryFind("Number1", ref number1Image);
        if (number2Image == null) TryFind("Number2", ref number2Image);
        if (number3Image == null) TryFind("Number3", ref number3Image);
    }

    private void TryFind(string childName, ref Image field)
    {
        Transform t = transform.Find(childName);
        if (t != null) field = t.GetComponent<Image>();
    }
    #endregion

    #region Public API
    /// <summary>Setup for integer multiplier  →  X2 / X12 / X123</summary>
    public void SetupInteger(int value, Sprite[] numberSprites, Sprite multiplierSprite,
        Sprite bgSprite = null, Sprite dotSprite = null)
    {
        multiplier = value;
        HideAll();

        // X prefix
        SetImage(multiplierImage, multiplierSprite);

        string s = value.ToString();

        if (s.Length == 1)
        {
            // X2
            SetDigit(number1Image, s[0], numberSprites);
        }
        else if (s.Length == 2)
        {
            // X12
            SetDigit(number1Image, s[0], numberSprites);
            SetDigit(number2Image, s[1], numberSprites);
        }
        else
        {
            // X123 (3+ digits – no X fits, show all three)
            SetDigit(number1Image, s[0], numberSprites);
            SetDigit(number2Image, s[1], numberSprites);
            SetDigit(number3Image, s[2], numberSprites);
        }
    }

    /// <summary>Setup for decimal multiplier  →  X1.2 / X12.3 (dot in number2 or number3 slot)</summary>
    public void SetupDecimal(float value, Sprite[] numberSprites, Sprite multiplierSprite,
        Sprite brownDotSprite, Sprite greenDotSprite, bool isWonState, Sprite bgSprite = null)
    {
        multiplier = value;
        HideAll();

        Sprite dotSprite = isWonState ? greenDotSprite : brownDotSprite;
        string formatted = value.ToString("F1");   // e.g. "2.5" or "12.5"
        string[] parts = formatted.Split('.');
        string whole = parts[0];
        char dec = parts.Length > 1 ? parts[1][0] : '0';

        // X prefix
        SetImage(multiplierImage, multiplierSprite);

        if (whole.Length == 1)
        {
            // X1.2  →  [X][1][.][2]
            SetDigit(number1Image, whole[0], numberSprites);
            SetImage(number2Image, dotSprite);
            SetDigit(number3Image, dec, numberSprites);
        }
        else
        {
            // X12.5  →  [X][1][2][.]  (decimal digit doesn't fit, omit it)
            SetDigit(number1Image, whole[0], numberSprites);
            SetDigit(number2Image, whole[1], numberSprites);
            SetImage(number3Image, dotSprite);
            // Note: if you add a Number4 image to the prefab you can show the decimal digit too
        }
    }

    /// <summary>Swap all sprites to the green / won colour set.</summary>
    public void ChangeToWonState(Sprite[] greenNumberSprites, Sprite greenMultiplierSprite,
        Sprite greenBgSprite, Sprite greenDotSprite)
    {
        isWon = true;

        bool isDecimal = multiplier % 1f != 0f;

        if (isDecimal)
            SetupDecimal(multiplier, greenNumberSprites, greenMultiplierSprite,
                null, greenDotSprite, true, greenBgSprite);
        else
            SetupInteger(Mathf.RoundToInt(multiplier), greenNumberSprites,
                greenMultiplierSprite, greenBgSprite);
    }
    #endregion

    #region Helpers
    private void HideAll()
    {
        Hide(multiplierImage);
        Hide(number1Image);
        Hide(number2Image);
        Hide(number3Image);
    }

    private void SetImage(Image img, Sprite sprite)
    {
        if (img == null || sprite == null) return;
        img.sprite = sprite;
        img.gameObject.SetActive(true);
    }

    private void SetDigit(Image img, char digit, Sprite[] sprites)
    {
        if (img == null || sprites == null) return;
        int idx = digit - '0';
        if (idx < 0 || idx >= sprites.Length) return;
        img.sprite = sprites[idx];
        img.gameObject.SetActive(true);
    }

    private static void Hide(Image img)
    {
        if (img != null) img.gameObject.SetActive(false);
    }
    #endregion
}