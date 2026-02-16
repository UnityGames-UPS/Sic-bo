using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Centralized audio manager for Sic Bo game with 4 synced toggles
/// Handles all sounds with proper synchronization and focus management
/// Works for both WebGL and APK platforms
/// </summary>
public class AudioManager : MonoBehaviour
{
    #region Singleton
    private static AudioManager instance;
    internal static AudioManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<AudioManager>();
            }
            return instance;
        }
    }
    #endregion

    #region Audio Clips - Serialized Fields
    [Header("Background Music")]
    [SerializeField] private AudioClip bgMusicClip;

    [Header("UI Sounds")]
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip popupOpenSound;
    [SerializeField] private AudioClip lobbyButtonSound;
    [SerializeField] private AudioClip arrowButtonSound;
    [SerializeField] private AudioClip chipselectionOpenSound;

    [Header("Timer Sounds")]
    [SerializeField] private AudioClip clockTickSound;

    [Header("Betting Sounds")]
    [SerializeField] private AudioClip playerBetPlaceSound;
    [SerializeField] private AudioClip chipAddSound;

    [Header("Animation Sounds")]
    [SerializeField] private AudioClip roundStartSound;
    [SerializeField] private AudioClip shakeSound;
    [SerializeField] private AudioClip boxOpenSound;
    [SerializeField] private AudioClip boxCloseSound;
    [SerializeField] private AudioClip diceShowSound;

    [Header("Dice Result Sounds")]
    [SerializeField] private AudioClip diceOneSound;
    [SerializeField] private AudioClip diceTwoSound;
    [SerializeField] private AudioClip diceThreeSound;
    [SerializeField] private AudioClip diceFourSound;
    [SerializeField] private AudioClip diceFiveSound;
    [SerializeField] private AudioClip diceSixSound;

    [Header("Bonus Sounds")]
    [SerializeField] private AudioClip bonusSpawnSound;
    [SerializeField] private AudioClip bonusHitSound;

    [Header("Audio Source References")]
    [SerializeField] private AudioSource bgMusicSource;
    [SerializeField] private AudioSource sfxSource1;
    [SerializeField] private AudioSource sfxSource2;

    [Header("Volume Settings")]
    [SerializeField] private float bgMusicVolume = 0.5f;
    [SerializeField] private float sfxVolume = 1f;

    [Header("Toggle References - Home Screen")]
    [SerializeField] private Toggle sfxHomeToggle;
    [SerializeField] private Toggle musicHomeToggle;

    [Header("Toggle References - Game Screen")]
    [SerializeField] private Toggle sfxGameToggle;
    [SerializeField] private Toggle musicGameToggle;
    #endregion

    #region Private Fields
    private bool isBgMusicEnabled = true;
    private bool isSfxEnabled = true;
    private bool isAppFocused = true;

    private Coroutine clockTickCoroutine;
    private bool isClockTickPlaying = false;

    // Track paused state for focus management
    private bool wasBgMusicPlayingBeforePause = false;
    private float bgMusicPauseTime = 0f;

    // Track if we're updating toggles programmatically to prevent recursive calls
    private bool isUpdatingToggles = false;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Singleton pattern
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeAudioSources();
        LoadAudioSettings();
    }

    private void Start()
    {print("AudioManager started");
        SetupToggleListeners();
        UpdateAllToggles();
        PlayBackgroundMusic();
    }

   private void OnApplicationFocus(bool hasFocus)
    {
        isAppFocused = hasFocus;

        if (hasFocus)
        {
            OnApplicationResume();
        }
        else
        {
            OnApplicationPause();
        }
    }

   private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            OnApplicationPause();
        }
        else
        {
            OnApplicationResume();
        }
    }
   
    private void OnDestroy()
    {
        if (clockTickCoroutine != null)
        {
            StopCoroutine(clockTickCoroutine);
        }

        RemoveToggleListeners();
    }
    #endregion

    #region Initialization
    private void InitializeAudioSources()
    {
        // Create audio sources if not assigned
        if (bgMusicSource == null)
        {
            bgMusicSource = gameObject.AddComponent<AudioSource>();
            bgMusicSource.loop = true;
            bgMusicSource.playOnAwake = false;
            bgMusicSource.volume = bgMusicVolume;
        }

        if (sfxSource1 == null)
        {
            sfxSource1 = gameObject.AddComponent<AudioSource>();
            sfxSource1.loop = false;
            sfxSource1.playOnAwake = false;
            sfxSource1.volume = sfxVolume;
        }

        if (sfxSource2 == null)
        {
            sfxSource2 = gameObject.AddComponent<AudioSource>();
            sfxSource2.loop = false;
            sfxSource2.playOnAwake = false;
            sfxSource2.volume = sfxVolume;
        }

        Debug.Log("[AudioManager] Audio sources initialized");
    }

    private void LoadAudioSettings()
    {
        // Load settings from PlayerPrefs
        isBgMusicEnabled = PlayerPrefs.GetInt("BgMusicEnabled", 1) == 1;
        isSfxEnabled = PlayerPrefs.GetInt("SfxEnabled", 1) == 1;

        ApplyAudioSettings();

        Debug.Log($"[AudioManager] Settings loaded - BG Music: {isBgMusicEnabled}, SFX: {isSfxEnabled}");
    }

    private void SaveAudioSettings()
    {
        PlayerPrefs.SetInt("BgMusicEnabled", isBgMusicEnabled ? 1 : 0);
        PlayerPrefs.SetInt("SfxEnabled", isSfxEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ApplyAudioSettings()
    {
        if (bgMusicSource != null)
        {
            bgMusicSource.mute = !isBgMusicEnabled;
        }

        if (sfxSource1 != null)
        {
            sfxSource1.mute = !isSfxEnabled;
        }

        if (sfxSource2 != null)
        {
            sfxSource2.mute = !isSfxEnabled;
        }
    }
    #endregion

    #region Toggle Management
    private void SetupToggleListeners()
    {
        if (sfxHomeToggle != null)
        {
            sfxHomeToggle.onValueChanged.AddListener(OnSfxHomeToggleChanged);
        }

        if (musicHomeToggle != null)
        {
            musicHomeToggle.onValueChanged.AddListener(OnMusicHomeToggleChanged);
        }

        if (sfxGameToggle != null)
        {
            sfxGameToggle.onValueChanged.AddListener(OnSfxGameToggleChanged);
        }

        if (musicGameToggle != null)
        {
            musicGameToggle.onValueChanged.AddListener(OnMusicGameToggleChanged);
        }
    }

    private void RemoveToggleListeners()
    {
        if (sfxHomeToggle != null)
        {
            sfxHomeToggle.onValueChanged.RemoveListener(OnSfxHomeToggleChanged);
        }

        if (musicHomeToggle != null)
        {
            musicHomeToggle.onValueChanged.RemoveListener(OnMusicHomeToggleChanged);
        }

        if (sfxGameToggle != null)
        {
            sfxGameToggle.onValueChanged.RemoveListener(OnSfxGameToggleChanged);
        }

        if (musicGameToggle != null)
        {
            musicGameToggle.onValueChanged.RemoveListener(OnMusicGameToggleChanged);
        }
    }

    private void OnSfxHomeToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;

        isSfxEnabled = isOn;
        SaveAudioSettings();
        ApplyAudioSettings();

        // Sync other SFX toggle
        UpdateAllToggles();

        // Stop clock tick if disabled
        if (!isOn && isClockTickPlaying)
        {
            StopClockTick();
        }

        // Play test sound when enabling
        if (isOn)
        {
            PlayButtonClick();
        }

        Debug.Log($"[AudioManager] SFX toggled from Home: {isOn}");
    }

    private void OnMusicHomeToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;

        isBgMusicEnabled = isOn;
        SaveAudioSettings();
        ApplyAudioSettings();

        // Sync other Music toggle
        UpdateAllToggles();

        // Play test sound when enabling
        if (isOn)
        {
            PlayButtonClick();
        }

        Debug.Log($"[AudioManager] Music toggled from Home: {isOn}");
    }

    private void OnSfxGameToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;

        isSfxEnabled = isOn;
        SaveAudioSettings();
        ApplyAudioSettings();

        // Sync other SFX toggle
        UpdateAllToggles();

        // Stop clock tick if disabled
        if (!isOn && isClockTickPlaying)
        {
            StopClockTick();
        }

        // Play test sound when enabling
        if (isOn)
        {
            PlayButtonClick();
        }

        Debug.Log($"[AudioManager] SFX toggled from Game: {isOn}");
    }

    private void OnMusicGameToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;

        isBgMusicEnabled = isOn;
        SaveAudioSettings();
        ApplyAudioSettings();

        // Sync other Music toggle
        UpdateAllToggles();

        // Play test sound when enabling
        if (isOn)
        {
            PlayButtonClick();
        }

        Debug.Log($"[AudioManager] Music toggled from Game: {isOn}");
    }

    private void UpdateAllToggles()
    {
        isUpdatingToggles = true;

        if (sfxHomeToggle != null)
        {
            sfxHomeToggle.isOn = isSfxEnabled;
        }

        if (musicHomeToggle != null)
        {
            musicHomeToggle.isOn = isBgMusicEnabled;
        }

        if (sfxGameToggle != null)
        {
            sfxGameToggle.isOn = isSfxEnabled;
        }

        if (musicGameToggle != null)
        {
            musicGameToggle.isOn = isBgMusicEnabled;
        }

        isUpdatingToggles = false;
    }

    /// <summary>
    /// Call this when toggles are dynamically created/enabled
    /// </summary>
    internal void RefreshToggles()
    {
        SetupToggleListeners();
        UpdateAllToggles();
    }
    #endregion

    #region Focus Management
    private void OnApplicationPause()
    {
        if (bgMusicSource != null && bgMusicSource.isPlaying)
        {
            wasBgMusicPlayingBeforePause = true;
            bgMusicPauseTime = bgMusicSource.time;
            bgMusicSource.Pause();
        }

        if (sfxSource1 != null && sfxSource1.isPlaying)
        {
            sfxSource1.Pause();
        }

        if (sfxSource2 != null && sfxSource2.isPlaying)
        {
            sfxSource2.Pause();
        }

        Debug.Log("[AudioManager] Application paused - audio paused");
    }

    private void OnApplicationResume()
    {
        if (wasBgMusicPlayingBeforePause && bgMusicSource != null && isBgMusicEnabled)
        {
            bgMusicSource.time = bgMusicPauseTime;
            bgMusicSource.UnPause();
            wasBgMusicPlayingBeforePause = false;
        }

        if (sfxSource1 != null)
        {
            sfxSource1.UnPause();
        }

        if (sfxSource2 != null)
        {
            sfxSource2.UnPause();
        }

        Debug.Log("[AudioManager] Application resumed - audio resumed");
    }
    #endregion

    #region Internal API - Background Music
    internal void PlayBackgroundMusic()
    {
        if (bgMusicSource == null || bgMusicClip == null) return;

       
            bgMusicSource.clip = bgMusicClip;
            bgMusicSource.volume = bgMusicVolume;
            bgMusicSource.loop = true;
            bgMusicSource.Play();

            Debug.Log("[AudioManager] Background music started");
        
    }

    internal void StopBackgroundMusic()
    {
        if (bgMusicSource != null && bgMusicSource.isPlaying)
        {
            bgMusicSource.Stop();
            Debug.Log("[AudioManager] Background music stopped");
        }
    }

    internal bool IsBgMusicEnabled()
    {
        return isBgMusicEnabled;
    }
    #endregion

    #region Internal API - SFX
    internal bool IsSfxEnabled()
    {
        return isSfxEnabled;
    }
    #endregion

    #region Internal API - UI Sounds
    internal void PlayButtonClick()
    {
        PlaySfx(buttonClickSound);
    }

    internal void PlayPopupOpen()
    {
        PlaySfx(popupOpenSound);
    }
    internal void PlayChipSelectionOpen()
    {
        PlaySfx(chipselectionOpenSound);
    }
    internal void PlayLobbyButton()
    {
        PlaySfx(lobbyButtonSound);
    }

    internal void PlayArrowButton()
    {
        PlaySfx(arrowButtonSound);
    }
    #endregion

    #region Internal API - Timer Sounds
    internal void StartClockTick()
    {
        if (!isSfxEnabled || clockTickSound == null) return;

        if (isClockTickPlaying) return;

        isClockTickPlaying = true;

        if (clockTickCoroutine != null)
        {
            StopCoroutine(clockTickCoroutine);
        }

        clockTickCoroutine = StartCoroutine(ClockTickLoop());

        Debug.Log("[AudioManager] Clock tick started");
    }

    internal void StopClockTick()
    {
        if (clockTickCoroutine != null)
        {
            StopCoroutine(clockTickCoroutine);
            clockTickCoroutine = null;
        }

        isClockTickPlaying = false;

        Debug.Log("[AudioManager] Clock tick stopped");
    }

    private IEnumerator ClockTickLoop()
    {
        while (isClockTickPlaying && isSfxEnabled)
        {
            PlaySfx(clockTickSound);

            // Wait for 1 second between ticks
            yield return new WaitForSeconds(1f);
        }

        isClockTickPlaying = false;
        clockTickCoroutine = null;
    }
    #endregion

    #region Internal API - Betting Sounds
    internal void PlayPlayerBetPlace()
    {
        PlaySfx(playerBetPlaceSound);
    }

    internal void PlayChipAdd()
    {
        PlaySfx(chipAddSound);
    }
    #endregion

    #region Internal API - Animation Sounds
    internal void PlayRoundStart()
    {
        PlaySfx(roundStartSound);
    }

    internal void PlayShake()
    {
        PlaySfx(shakeSound);
    }

    internal void PlayBoxOpen()
    {
        PlaySfx(boxOpenSound);
    }

    internal void PlayBoxClose()
    {
        PlaySfx(boxCloseSound);
    }

    internal void PlayDiceShow()
    {
        PlaySfx(diceShowSound);
    }
    #endregion

    #region Internal API - Dice Result Sounds
    internal void PlayDiceResultSequence(int dice1, int dice2, int dice3, float delayBetweenDice = 0.5f)
    {
        StartCoroutine(PlayDiceSequenceCoroutine(dice1, dice2, dice3, delayBetweenDice));
    }

    private IEnumerator PlayDiceSequenceCoroutine(int dice1, int dice2, int dice3, float delay)
    {
        // Play first dice sound
        PlayDiceSound(dice1);
        yield return new WaitForSeconds(delay);

        // Play second dice sound
        PlayDiceSound(dice2);
        yield return new WaitForSeconds(delay);

        // Play third dice sound
        PlayDiceSound(dice3);
    }

    private void PlayDiceSound(int diceValue)
    {
        AudioClip clip = diceValue switch
        {
            1 => diceOneSound,
            2 => diceTwoSound,
            3 => diceThreeSound,
            4 => diceFourSound,
            5 => diceFiveSound,
            6 => diceSixSound,
            _ => null
        };

        if (clip != null)
        {
            PlaySfx(clip);
            Debug.Log($"[AudioManager] Playing dice sound: {diceValue}");
        }
    }
    #endregion

    #region Internal API - Bonus Sounds
    internal void PlayBonusSpawn()
    {
        PlaySfx(bonusSpawnSound);
    }

    internal void PlayBonusHit()
    {
        PlaySfx(bonusHitSound);
    }
    #endregion

    #region Private Methods - Core SFX Playback
    private void PlaySfx(AudioClip clip)
    {
        if (!isSfxEnabled || clip == null || !isAppFocused) return;

        // Try to use the first available SFX source
        if (sfxSource1 != null && !sfxSource1.isPlaying)
        {
            sfxSource1.PlayOneShot(clip, sfxVolume);
        }
        else if (sfxSource2 != null && !sfxSource2.isPlaying)
        {
            sfxSource2.PlayOneShot(clip, sfxVolume);
        }
        else if (sfxSource1 != null)
        {
            // If both are playing, use source 1 (it will overlap)
            sfxSource1.PlayOneShot(clip, sfxVolume);
        }
    }
    #endregion

    #region Internal API - Volume Control
    internal void SetBgMusicVolume(float volume)
    {
        bgMusicVolume = Mathf.Clamp01(volume);

        if (bgMusicSource != null)
        {
            bgMusicSource.volume = bgMusicVolume;
        }
    }

    internal void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);

        if (sfxSource1 != null)
        {
            sfxSource1.volume = sfxVolume;
        }

        if (sfxSource2 != null)
        {
            sfxSource2.volume = sfxVolume;
        }
    }

    internal float GetBgMusicVolume()
    {
        return bgMusicVolume;
    }

    internal float GetSfxVolume()
    {
        return sfxVolume;
    }
    #endregion
}