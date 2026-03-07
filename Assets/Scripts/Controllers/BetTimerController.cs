using System.Collections;
using UnityEngine;
using TMPro;
using DG.Tweening;

public class BetTimerController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Betting Phase")]
    [SerializeField] private GameObject placeBetPanel;
    [SerializeField] private GameObject last5SecIndicator;
    [SerializeField] private TMP_Text bettingTimer_Text;

    [Header("Locked Phase")]
    [SerializeField] private GameObject betLockedPanel;

    [Header("Next Round Phase")]
    [SerializeField] private GameObject nextRoundPanel;
    [SerializeField] private TMP_Text nextRoundTimer_Text;

    [Header("Animation Settings")]
    [SerializeField] private float heartbeatScale = 1.3f;
    [SerializeField] private float heartbeatDuration = 0.2f;
    [SerializeField] private float popScale = 1.2f;
    [SerializeField] private float popDuration = 0.3f;
    #endregion

    #region Private Fields
    private BetTimerState currentState = BetTimerState.Hidden;
    private int currentSeconds = 0;
    private Coroutine countdownCoroutine;
    private Coroutine localBettingCountdownCoroutine;
    private bool isClockTickActive = false;
    private bool isBetLockedActive = false; // Track if bet locked is already showing
    #endregion

    #region Unity Lifecycle
    private void OnDestroy()
    {
        StopCountdown();
        StopLocalBettingCountdown();
        StopClockTick();

        if (bettingTimer_Text != null && bettingTimer_Text.transform != null)
            bettingTimer_Text.transform.DOKill();
        if (betLockedPanel != null && betLockedPanel.transform != null)
            betLockedPanel.transform.DOKill();
        if (placeBetPanel != null && placeBetPanel.transform != null)
            placeBetPanel.transform.DOKill();
        if (nextRoundPanel != null && nextRoundPanel.transform != null)
            nextRoundPanel.transform.DOKill();
    }
    #endregion

    #region Internal API
    internal void ShowBettingPhase(int secondsRemaining)
    {
        StopCountdown();
        StopLocalBettingCountdown();

        currentState = BetTimerState.Betting;
        currentSeconds = secondsRemaining;
        isBetLockedActive = false; // Reset bet locked flag

        if (placeBetPanel)
        {
            placeBetPanel.SetActive(true);
            PlayPopAnimation(placeBetPanel.transform);
        }
        if (betLockedPanel) betLockedPanel.SetActive(false);
        if (nextRoundPanel) nextRoundPanel.SetActive(false);
        if (bettingTimer_Text) bettingTimer_Text.transform.localScale = Vector3.one;

        ApplyBettingTimerDisplay(secondsRemaining);
        localBettingCountdownCoroutine = StartCoroutine(LocalBettingCountdown());
    }

    internal void UpdateBettingTimer(int secondsRemaining)
    {
        // Server correction: snap to authoritative value and restart local countdown
        StopLocalBettingCountdown();
        currentSeconds = secondsRemaining;
        ApplyBettingTimerDisplay(secondsRemaining);

        if (currentState == BetTimerState.Betting && secondsRemaining > 0)
            localBettingCountdownCoroutine = StartCoroutine(LocalBettingCountdown());
    }

    internal void ShowBetLocked()
    {
        StopCountdown();
        StopLocalBettingCountdown();
        StopClockTick();

        currentState = BetTimerState.Locked;

        if (placeBetPanel) placeBetPanel.SetActive(false);
        if (betLockedPanel)
        {
            // Only play animation if it wasn't already active
            bool wasAlreadyActive = isBetLockedActive;

            betLockedPanel.SetActive(true);

            if (!wasAlreadyActive)
            {
                PlayPopAnimation(betLockedPanel.transform);
                isBetLockedActive = true;
            }
        }
        if (nextRoundPanel) nextRoundPanel.SetActive(false);
        if (last5SecIndicator) last5SecIndicator.SetActive(false);
    }

    internal void ShowNextRound(int secondsUntilNextRound)
    {
        StopCountdown();
        StopLocalBettingCountdown();
        StopClockTick();

        currentState = BetTimerState.NextRound;
        currentSeconds = secondsUntilNextRound;
        isBetLockedActive = false; // Reset bet locked flag

        if (placeBetPanel) placeBetPanel.SetActive(false);
        if (betLockedPanel) betLockedPanel.SetActive(false);
        if (nextRoundPanel) nextRoundPanel.SetActive(true);
        if (last5SecIndicator) last5SecIndicator.SetActive(false);

        UpdateNextRoundTimer(secondsUntilNextRound);
        countdownCoroutine = StartCoroutine(NextRoundCountdown());
    }

    internal void UpdateNextRoundTimer(int seconds)
    {
        currentSeconds = seconds;
        if (nextRoundTimer_Text) nextRoundTimer_Text.text = seconds.ToString();
    }

    internal void HideAll()
    {
        StopCountdown();
        StopLocalBettingCountdown();
        StopClockTick();

        currentState = BetTimerState.Hidden;
        isBetLockedActive = false; // Reset bet locked flag

        if (placeBetPanel) placeBetPanel.SetActive(false);
        if (betLockedPanel) betLockedPanel.SetActive(false);
        if (nextRoundPanel) nextRoundPanel.SetActive(false);
        if (last5SecIndicator) last5SecIndicator.SetActive(false);
    }

    internal BetTimerState GetState() => currentState;
    internal int GetCurrentSeconds() => currentSeconds;
    #endregion

    #region Countdown
    /// <summary>
    /// Runs a local 1-second tick between server corrections so the display
    /// counts down smoothly without waiting for the next packet.
    /// Each server update calls UpdateBettingTimer which restarts this coroutine,
    /// keeping it phase-locked to the server.
    /// </summary>
    private IEnumerator LocalBettingCountdown()
    {
        while (currentSeconds > 0 && currentState == BetTimerState.Betting)
        {
            yield return new WaitForSeconds(1f);
            if (currentState != BetTimerState.Betting) break;
            currentSeconds--;
            ApplyBettingTimerDisplay(currentSeconds);
        }
        localBettingCountdownCoroutine = null;
    }

    private void StopLocalBettingCountdown()
    {
        if (localBettingCountdownCoroutine != null)
        {
            StopCoroutine(localBettingCountdownCoroutine);
            localBettingCountdownCoroutine = null;
        }
    }

    private IEnumerator NextRoundCountdown()
    {
        while (currentSeconds > 0 && currentState == BetTimerState.NextRound)
        {
            yield return new WaitForSeconds(1f);
            currentSeconds--;
            UpdateNextRoundTimer(currentSeconds);
        }
        countdownCoroutine = null;
    }

    private void StopCountdown()
    {
        if (countdownCoroutine != null) { StopCoroutine(countdownCoroutine); countdownCoroutine = null; }
    }
    #endregion

    #region Animation
    /// <summary>
    /// Updates the visible betting timer display. Called both from server corrections
    /// (UpdateBettingTimer) and from the local countdown coroutine (LocalBettingCountdown).
    /// </summary>
    private void ApplyBettingTimerDisplay(int seconds)
    {
        if (bettingTimer_Text)
        {
            bettingTimer_Text.text = seconds.ToString();

            if (seconds <= 5 && seconds > 0)
            {
                PopTimerText();
                if (!isClockTickActive) StartClockTick();
            }
            else if (seconds > 5 && isClockTickActive)
            {
                StopClockTick();
            }
        }

        if (last5SecIndicator)
            last5SecIndicator.SetActive(seconds <= 5 && seconds > 0);

        if (seconds == 0)
            StopClockTick();
    }

    private void PopTimerText()
    {
        if (bettingTimer_Text == null) return;
        bettingTimer_Text.transform.DOKill();
        bettingTimer_Text.transform.localScale = Vector3.one;
        bettingTimer_Text.transform.DOScale(heartbeatScale, heartbeatDuration)
            .SetEase(Ease.OutBack)
            .OnComplete(() => bettingTimer_Text.transform.DOScale(1f, heartbeatDuration).SetEase(Ease.InBack));
    }

    /// <summary>
    /// Pop animation for panel objects. Animates only the panel transform,
    /// children are not affected due to localScale usage.
    /// Stores original child scales to preserve them during parent animation.
    /// </summary>
    private void PlayPopAnimation(Transform panelTransform)
    {
        if (panelTransform == null) return;

        // Store original child scales
        Vector3[] originalChildScales = new Vector3[panelTransform.childCount];
        for (int i = 0; i < panelTransform.childCount; i++)
        {
            originalChildScales[i] = panelTransform.GetChild(i).localScale;
        }

        // Kill any existing animation on this transform
        panelTransform.DOKill();

        // Reset to normal scale
        panelTransform.localScale = Vector3.one;

        // Big snap effect: scale up then back to normal
        panelTransform.DOScale(popScale, popDuration)
            .SetEase(Ease.OutBack)
            .OnUpdate(() => {
                // Counter-scale children to keep them at original size
                for (int i = 0; i < panelTransform.childCount; i++)
                {
                    if (i < originalChildScales.Length)
                    {
                        panelTransform.GetChild(i).localScale = new Vector3(
                            originalChildScales[i].x / panelTransform.localScale.x,
                            originalChildScales[i].y / panelTransform.localScale.y,
                            originalChildScales[i].z / panelTransform.localScale.z
                        );
                    }
                }
            })
            .OnComplete(() => {
                panelTransform.DOScale(Vector3.one, popDuration * 0.5f)
                    .SetEase(Ease.InOutQuad)
                    .OnUpdate(() => {
                        // Counter-scale children during scale down
                        for (int i = 0; i < panelTransform.childCount; i++)
                        {
                            if (i < originalChildScales.Length)
                            {
                                panelTransform.GetChild(i).localScale = new Vector3(
                                    originalChildScales[i].x / panelTransform.localScale.x,
                                    originalChildScales[i].y / panelTransform.localScale.y,
                                    originalChildScales[i].z / panelTransform.localScale.z
                                );
                            }
                        }
                    })
                    .OnComplete(() => {
                        // Restore original child scales
                        for (int i = 0; i < panelTransform.childCount; i++)
                        {
                            if (i < originalChildScales.Length)
                            {
                                panelTransform.GetChild(i).localScale = originalChildScales[i];
                            }
                        }
                    });
            });
    }
    #endregion

    #region Clock Tick
    private void StartClockTick()
    {
        if (AudioManager.Instance == null) return;
        AudioManager.Instance.StartClockTick();
        isClockTickActive = true;
    }

    private void StopClockTick()
    {
        if (AudioManager.Instance != null && isClockTickActive)
            AudioManager.Instance.StopClockTick();
        isClockTickActive = false;
    }
    #endregion
}