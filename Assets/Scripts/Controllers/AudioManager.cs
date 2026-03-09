using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    #region Singleton
    private static AudioManager instance;
    internal static AudioManager Instance
    {
        get
        {
            if (instance == null) instance = FindObjectOfType<AudioManager>();
            return instance;
        }
    }
    #endregion

    #region Serialized Fields
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

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgMusicSource;
    [SerializeField] private AudioSource sfxSource1;
    [SerializeField] private AudioSource sfxSource2;

    [Header("Volume Settings")]
    [SerializeField] private float bgMusicVolume = 0.5f;
    [SerializeField] private float sfxVolume = 1f;

    [Header("Toggles - Home Screen")]
    [SerializeField] private Toggle sfxHomeToggle;
    [SerializeField] private Toggle musicHomeToggle;

    [Header("Toggles - Game Screen")]
    [SerializeField] private Toggle sfxGameToggle;
    [SerializeField] private Toggle musicGameToggle;
    #endregion

    #region Private Fields
    private bool isBgMusicEnabled = true;
    private bool isSfxEnabled = true;
    private bool isAppFocused = true;
    private bool wasBgMusicPlayingBeforePause = false;
    private float bgMusicPauseTime = 0f;
    private bool isUpdatingToggles = false;
    private Coroutine clockTickCoroutine;
    private bool isClockTickPlaying = false;
    private bool isPaused = false;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        InitializeAudioSources();
        LoadAudioSettings();
    }

    private void Start()
    {
        SetupToggleListeners();
        UpdateAllToggles();
        PlayBackgroundMusic();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        isAppFocused = hasFocus;
        if (hasFocus)
        {
            ResumeAudio();
        }
        else
        {
            PauseAudio();
        }
    }

    private void OnDestroy()
    {
        if (clockTickCoroutine != null) StopCoroutine(clockTickCoroutine);
        RemoveToggleListeners();
        if (instance == this) instance = null;
    }
    #endregion

    #region Initialization
    private void InitializeAudioSources()
    {
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
    }

    private void LoadAudioSettings()
    {
        isBgMusicEnabled = PlayerPrefs.GetInt("BgMusicEnabled", 1) == 1;
        isSfxEnabled = PlayerPrefs.GetInt("SfxEnabled", 1) == 1;
        ApplyAudioSettings();
    }

    private void SaveAudioSettings()
    {
        PlayerPrefs.SetInt("BgMusicEnabled", isBgMusicEnabled ? 1 : 0);
        PlayerPrefs.SetInt("SfxEnabled", isSfxEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ApplyAudioSettings()
    {
        if (bgMusicSource != null) bgMusicSource.mute = !isBgMusicEnabled;
        if (sfxSource1 != null) sfxSource1.mute = !isSfxEnabled;
        if (sfxSource2 != null) sfxSource2.mute = !isSfxEnabled;
    }
    #endregion

    #region Toggle Management
    private void SetupToggleListeners()
    {
        SetupToggle(sfxHomeToggle);
        SetupToggle(musicHomeToggle);
        SetupToggle(sfxGameToggle);
        SetupToggle(musicGameToggle);

        sfxHomeToggle?.onValueChanged.AddListener(OnSfxToggleChanged);
        musicHomeToggle?.onValueChanged.AddListener(OnMusicToggleChanged);
        sfxGameToggle?.onValueChanged.AddListener(OnSfxToggleChanged);
        musicGameToggle?.onValueChanged.AddListener(OnMusicToggleChanged);
    }

    private void SetupToggle(Toggle toggle)
    {
        if (toggle == null) return;

        Image background = toggle.targetGraphic as Image;
        if (background == null) background = toggle.GetComponent<Image>();

        if (background != null)
        {
            UpdateToggleBackground(background, toggle.isOn);
        }

        toggle.onValueChanged.AddListener((isOn) =>
        {
            if (background != null)
            {
                UpdateToggleBackground(background, isOn);
            }
        });
    }

    private void UpdateToggleBackground(Image background, bool isOn)
    {
        Color color = background.color;
        color.a = isOn ? 0f : 1f;
        background.color = color;
    }

    private void RemoveToggleListeners()
    {
        sfxHomeToggle?.onValueChanged.RemoveListener(OnSfxToggleChanged);
        musicHomeToggle?.onValueChanged.RemoveListener(OnMusicToggleChanged);
        sfxGameToggle?.onValueChanged.RemoveListener(OnSfxToggleChanged);
        musicGameToggle?.onValueChanged.RemoveListener(OnMusicToggleChanged);
    }

    private void OnSfxToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;
        isSfxEnabled = !isOn;
        SaveAudioSettings();
        ApplyAudioSettings();
        UpdateAllToggles();
        if (!isSfxEnabled && isClockTickPlaying) StopClockTick();
        if (isSfxEnabled) PlayButtonClick();
    }

    private void OnMusicToggleChanged(bool isOn)
    {
        if (isUpdatingToggles) return;
        isBgMusicEnabled = !isOn;
        SaveAudioSettings();
        ApplyAudioSettings();
        UpdateAllToggles();
        if (isBgMusicEnabled) PlayButtonClick();
    }

    private void UpdateAllToggles()
    {
        isUpdatingToggles = true;
        if (sfxHomeToggle != null) sfxHomeToggle.isOn = !isSfxEnabled;
        if (musicHomeToggle != null) musicHomeToggle.isOn = !isBgMusicEnabled;
        if (sfxGameToggle != null) sfxGameToggle.isOn = !isSfxEnabled;
        if (musicGameToggle != null) musicGameToggle.isOn = !isBgMusicEnabled;
        isUpdatingToggles = false;
    }

    internal void RefreshToggles()
    {
        SetupToggleListeners();
        UpdateAllToggles();
    }
    #endregion

    #region Focus Management
    private void PauseAudio()
    {
        if (isPaused) return;
        isPaused = true;

        if (bgMusicSource != null && bgMusicSource.isPlaying)
        {
            wasBgMusicPlayingBeforePause = true;
            bgMusicPauseTime = bgMusicSource.time;
            bgMusicSource.Pause();
        }

        if (sfxSource1 != null && sfxSource1.isPlaying) sfxSource1.Pause();
        if (sfxSource2 != null && sfxSource2.isPlaying) sfxSource2.Pause();
    }

    private void ResumeAudio()
    {
        if (!isPaused) return;
        isPaused = false;

        if (wasBgMusicPlayingBeforePause && bgMusicSource != null && isBgMusicEnabled)
        {
            bgMusicSource.time = bgMusicPauseTime;
            bgMusicSource.UnPause();
            wasBgMusicPlayingBeforePause = false;
        }

        sfxSource1?.UnPause();
        sfxSource2?.UnPause();
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
    }

    internal void StopBackgroundMusic()
    {
        if (bgMusicSource != null && bgMusicSource.isPlaying) bgMusicSource.Stop();
    }

    internal bool IsBgMusicEnabled() => isBgMusicEnabled;
    internal bool IsSfxEnabled() => isSfxEnabled;
    #endregion

    #region Internal API - UI Sounds
    internal void PlayButtonClick() => PlaySfx(buttonClickSound);


    internal void PlayPopupOpen() => PlaySfx(popupOpenSound);
    internal void PlayChipSelectionOpen() => PlaySfx(chipselectionOpenSound);
    internal void PlayLobbyButton() => PlaySfx(lobbyButtonSound);
    internal void PlayArrowButton() => PlaySfx(arrowButtonSound);
    #endregion

    #region Internal API - Timer
    internal void StartClockTick()
    {
        if (!isSfxEnabled || clockTickSound == null || isClockTickPlaying) return;
        isClockTickPlaying = true;
        if (clockTickCoroutine != null) StopCoroutine(clockTickCoroutine);
        clockTickCoroutine = StartCoroutine(ClockTickLoop());
    }

    internal void StopClockTick()
    {
        if (clockTickCoroutine != null) { StopCoroutine(clockTickCoroutine); clockTickCoroutine = null; }
        isClockTickPlaying = false;
    }

    private IEnumerator ClockTickLoop()
    {
        while (isClockTickPlaying && isSfxEnabled)
        {
            PlaySfx(clockTickSound);
            yield return new WaitForSeconds(1f);
        }

        isClockTickPlaying = false;
        clockTickCoroutine = null;
    }
    #endregion

    #region Internal API - Betting Sounds
    internal void PlayPlayerBetPlace() => PlaySfx(playerBetPlaceSound);
    internal void PlayChipAdd() => PlaySfx(chipAddSound);
    #endregion

    #region Internal API - Animation Sounds
    internal void PlayRoundStart() => PlaySfx(roundStartSound);
    internal void PlayShake() => PlayAnimationSfx(shakeSound);
    internal void PlayBoxOpen() => PlayAnimationSfx(boxOpenSound);
    internal void PlayBoxClose() => PlayAnimationSfx(boxCloseSound);
    internal void PlayDiceShow() => PlayAnimationSfx(diceShowSound);
    #endregion

    #region Internal API - Dice Result Sounds
    internal void PlayDiceResultSequence(int dice1, int dice2, int dice3, float delayBetweenDice = 0.5f)
    {
        StartCoroutine(PlayDiceSequenceCoroutine(dice1, dice2, dice3, delayBetweenDice));
    }

    private IEnumerator PlayDiceSequenceCoroutine(int dice1, int dice2, int dice3, float delay)
    {
        PlayDiceSound(dice1);
        yield return new WaitForSeconds(delay);
        PlayDiceSound(dice2);
        yield return new WaitForSeconds(delay);
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
        if (clip != null) PlayAnimationSfx(clip);
    }
    #endregion

    #region Internal API - Bonus Sounds
    internal void PlayBonusSpawn() => PlaySfx(bonusSpawnSound);
    internal void PlayBonusHit() => PlaySfx(bonusHitSound);
    #endregion

    #region Internal API - Volume
    internal void SetBgMusicVolume(float volume)
    {
        bgMusicVolume = Mathf.Clamp01(volume);
        if (bgMusicSource != null) bgMusicSource.volume = bgMusicVolume;
    }

    internal void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (sfxSource1 != null) sfxSource1.volume = sfxVolume;
        if (sfxSource2 != null) sfxSource2.volume = sfxVolume;
    }

    internal float GetBgMusicVolume() => bgMusicVolume;
    internal float GetSfxVolume() => sfxVolume;
    #endregion

    #region Core Playback
    private void PlaySfx(AudioClip clip)
    {
        if (!isSfxEnabled || clip == null || !isAppFocused || isPaused) return;
        sfxSource1?.PlayOneShot(clip, sfxVolume);
    }

    private void PlayAnimationSfx(AudioClip clip)
    {
        if (!isSfxEnabled || clip == null || !isAppFocused || isPaused) return;
        sfxSource2?.PlayOneShot(clip, sfxVolume);
    }
    #endregion

    #region Cleanup
    internal void StopAllSounds()
    {
        StopClockTick();
        if (sfxSource1 != null) sfxSource1.Stop();
        if (sfxSource2 != null) sfxSource2.Stop();
    }
    #endregion
}