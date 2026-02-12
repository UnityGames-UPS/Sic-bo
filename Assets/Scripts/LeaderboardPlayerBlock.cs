using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Represents a single player info block in the leaderboard.
/// Attach this to each leaderboard slot GameObject in the scene.
/// </summary>
public class LeaderboardPlayerBlock : MonoBehaviour
{
    [Header("UI Elements")]
    public Image AvatarImage;
    public TMP_Text NameText;
    public TMP_Text BalanceText;
    public GameObject Container; // Parent container to show/hide entire block

    /// <summary>
    /// Set player data and show the block
    /// </summary>
    public void SetPlayerData(string username, double balance, Sprite avatar)
    {


        if (NameText != null)
            NameText.text = MaskUsername(username);

        if (BalanceText != null)
            BalanceText.text = GameUtilities.FormatBalance(balance);

        if (AvatarImage != null && avatar != null)
            AvatarImage.sprite = avatar;

        if (Container != null)
        {
            Container.SetActive(true);
          
        }
        else
        {
            Debug.LogWarning("[LeaderboardPlayerBlock] Container is null!");
        }

        // Start with name visible
        ShowName();
    }

    /// <summary>
    /// Mask username like "ase*****re"
    /// </summary>
    private string MaskUsername(string username)
    {
        if (string.IsNullOrEmpty(username))
            return username;

        if (username.Length <= 4)
            return username; // Too short to mask

        // Take first 3 and last 2 characters
        int firstChars = 3;
        int lastChars = 2;
        int maskedLength = username.Length - firstChars - lastChars;

        if (maskedLength <= 0)
            return username;

        string first = username.Substring(0, firstChars);
        string last = username.Substring(username.Length - lastChars);
        string masked = new string('*', maskedLength);

        return first + masked + last;
    }

    /// <summary>
    /// Update only the balance text
    /// </summary>
    public void UpdateBalance(double balance)
    {
        if (BalanceText != null)
            BalanceText.text = GameUtilities.FormatBalance(balance);
    }

    /// <summary>
    /// Show name, hide balance
    /// </summary>
    public void ShowName()
    {
        if (NameText != null)
            NameText.gameObject.SetActive(true);

        if (BalanceText != null)
            BalanceText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Show balance, hide name
    /// </summary>
    public void ShowBalance()
    {
        if (NameText != null)
            NameText.gameObject.SetActive(false);

        if (BalanceText != null)
            BalanceText.gameObject.SetActive(true);
    }

    /// <summary>
    /// Hide entire block
    /// </summary>
    public void HideAll()
    {
        if (Container != null)
        {
            Container.SetActive(false);
        }
        else
        {
            // Fallback if no container specified
            if (NameText != null)
                NameText.gameObject.SetActive(false);

            if (BalanceText != null)
                BalanceText.gameObject.SetActive(false);

            if (AvatarImage != null)
                AvatarImage.gameObject.SetActive(false);
        }
    }
}   