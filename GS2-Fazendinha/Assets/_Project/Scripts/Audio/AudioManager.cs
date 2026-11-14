using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    public float MusicVolume => musicVolume;
    public float EffectsVolume => effectsVolume;
    public AudioClip LastPlayedSound { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource effectsSource;
    [SerializeField] private AudioSource ambienceSource;

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private AudioMixerGroup musicGroup;
    [SerializeField] private AudioMixerGroup sfxGroup;
    [SerializeField] private AudioMixerGroup ambienceGroup;

    [Header("Music Playlists")]
    [SerializeField] private AudioClip[] menuPlaylist = new AudioClip[0];
    [SerializeField] private AudioClip[] gameplayPlaylist = new AudioClip[0];

    [Header("Audio Clips")]
    [SerializeField] private AudioClip startingMusic;
    [SerializeField] private AudioClip uiClickSound;
    [SerializeField] private AudioClip farmClickSound;
    [SerializeField] private AudioClip cropClickSound;
    [SerializeField] private AudioClip farmCloseSound;
    [SerializeField] private AudioClip menuCloseSound;
    [SerializeField] private AudioClip purchaseSound;
    [SerializeField] private AudioClip saleSound;
    [SerializeField] private AudioClip marketTechnologySound;
    [SerializeField] private AudioClip farmTechnologySound;
    [SerializeField] private AudioClip missionSound;
    [SerializeField] private AudioClip tutorialMissionSound;
    [SerializeField] private AudioClip tutorialCompleteSound;
    [SerializeField] private AudioClip[] wateringSounds = new AudioClip[0];
    [SerializeField] private AudioClip[] harvestSounds = new AudioClip[0];
    [SerializeField] private AudioClip[] fertilizerSounds = new AudioClip[0];

    [Header("Farm Ambience")]
    [SerializeField] private AudioClip tutorialAmbience;
    [SerializeField] private AudioClip gameplayAmbience;
    [SerializeField] private AudioClip chickenSound;
    [SerializeField, Min(1f)] private float chickenInterval = 25f;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.3f;

    [Header("Volume Settings")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float effectsVolume = 1f;

    private AudioClip[] currentPlaylist;
    private int playlistIndex = -1;
    private bool playlistActive;
    private bool sceneConfigured;
    private bool menuContext;
    private bool tutorialContext;
    private float chickenTimer;
    private AudioClip lastWateringSound;
    private AudioClip lastHarvestSound;
    private AudioClip lastFertilizerSound;
    private bool started;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ConfigureSources();
        FarmSaveManager.LoadAudioSettings(this);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        started = true;
        ApplyVolumes();
        ConfigureSceneAudio(SceneManager.GetActiveScene().name);
    }

    private void Update()
    {
        UpdatePlaylist();
        UpdateAmbience();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (started) ConfigureSceneAudio(scene.name);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void ConfigureSources()
    {
        if (musicSource == null)
            musicSource = gameObject.AddComponent<AudioSource>();

        if (effectsSource == null || effectsSource == musicSource)
            effectsSource = gameObject.AddComponent<AudioSource>();

        if (ambienceSource == null || ambienceSource == musicSource || ambienceSource == effectsSource)
            ambienceSource = gameObject.AddComponent<AudioSource>();

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.volume = 1f;
        musicSource.outputAudioMixerGroup = musicGroup;

        effectsSource.playOnAwake = false;
        effectsSource.loop = false;
        effectsSource.spatialBlend = 0f;
        effectsSource.volume = 1f;
        effectsSource.outputAudioMixerGroup = sfxGroup;

        ambienceSource.playOnAwake = false;
        ambienceSource.loop = true;
        ambienceSource.spatialBlend = 0f;
        ambienceSource.volume = 1f;
        ambienceSource.outputAudioMixerGroup = ambienceGroup;
    }

    private void ConfigureSceneAudio(string sceneName)
    {
        bool nextMenuContext = sceneName == "MainMenu";
        tutorialContext = sceneName == "Tutorial";
        if (!sceneConfigured || menuContext != nextMenuContext)
        {
            menuContext = nextMenuContext;
            currentPlaylist = menuContext ? menuPlaylist : gameplayPlaylist;
            playlistIndex = -1;
            playlistActive = true;
            PlayNextTrack();
        }
        sceneConfigured = true;
        ConfigureAmbience();
    }

    private void UpdatePlaylist()
    {
        if (playlistActive && musicSource != null && !musicSource.isPlaying)
            PlayNextTrack();
    }

    private void PlayNextTrack()
    {
        if (currentPlaylist != null && currentPlaylist.Length > 0)
        {
            for (int offset = 0; offset < currentPlaylist.Length; offset++)
            {
                playlistIndex = (playlistIndex + 1) % currentPlaylist.Length;
                AudioClip clip = currentPlaylist[playlistIndex];
                if (clip == null) continue;
                musicSource.loop = false;
                musicSource.clip = clip;
                musicSource.Play();
                return;
            }
        }

        playlistActive = false;
        musicSource.Stop();
        musicSource.clip = null;
        if (startingMusic != null) PlayMusic(startingMusic);
    }

    private void ConfigureAmbience()
    {
        AudioClip clip = menuContext ? null : (tutorialContext ? tutorialAmbience : gameplayAmbience);
        if (ambienceSource.clip == clip && ambienceSource.isPlaying) return;
        ambienceSource.Stop();
        ambienceSource.clip = clip;
        chickenTimer = 0f;
        if (clip != null) ambienceSource.Play();
    }

    private void UpdateAmbience()
    {
        if (menuContext || tutorialContext || chickenSound == null) return;
        chickenTimer += Time.unscaledDeltaTime;
        if (chickenTimer < chickenInterval) return;
        chickenTimer %= chickenInterval;
        if (ambienceSource != null) ambienceSource.PlayOneShot(chickenSound);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || musicSource == null)
            return;

        playlistActive = false;
        musicSource.loop = true;

        if (musicSource.clip == clip && musicSource.isPlaying)
            return;

        musicSource.clip = clip;
        musicSource.Play();
    }

    public void StopMusic()
    {
        playlistActive = false;
        if (musicSource != null)
            musicSource.Stop();
    }

    public void PlaySound(AudioClip clip)
    {
        if (clip != null && effectsSource != null)
        {
            LastPlayedSound = clip;
            effectsSource.PlayOneShot(clip);
        }
    }

    public void PlayUIClick()
    {
        PlaySound(uiClickSound);
    }

    public void PlayFarmClick() => PlaySound(farmClickSound);
    public void PlayCropClick() => PlaySound(cropClickSound);
    public void PlayFarmClose() => PlaySound(farmCloseSound);
    public void PlayMenuClose() => PlaySound(menuCloseSound);
    public void PlayPurchase() => PlaySound(purchaseSound);
    public void PlaySale() => PlaySound(saleSound);
    public void PlayTechnologyPurchase(bool marketExclusive) => PlaySound(marketExclusive ? marketTechnologySound : farmTechnologySound);
    public void PlayMissionComplete() => PlaySound(tutorialContext ? tutorialMissionSound : missionSound);
    public void PlayTutorialComplete() => PlaySound(tutorialCompleteSound);
    public void PlayWatering() => lastWateringSound = PlayRandomSound(wateringSounds, lastWateringSound);
    public void PlayHarvest() => lastHarvestSound = PlayRandomSound(harvestSounds, lastHarvestSound);
    public void PlayFertilizer() => lastFertilizerSound = PlayRandomSound(fertilizerSounds, lastFertilizerSound);

    // Sorteia outra versão quando possível, sem repetir a anterior imediatamente.
    private AudioClip PlayRandomSound(AudioClip[] clips, AudioClip previous)
    {
        if (clips == null || clips.Length == 0) return null;
        int choices = 0;
        foreach (AudioClip clip in clips)
            if (clip != null && clip != previous) choices++;
        if (choices == 0)
        {
            foreach (AudioClip clip in clips)
                if (clip != null) { PlaySound(clip); return clip; }
            return null;
        }
        int selected = Random.Range(0, choices);
        foreach (AudioClip clip in clips)
            if (clip != null && clip != previous && selected-- == 0)
            { PlaySound(clip); return clip; }
        return null;
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (started) SetGain("MusicVolume", musicVolume);
    }

    public void SetEffectsVolume(float volume)
    {
        effectsVolume = Mathf.Clamp01(volume);
        if (!started) return;
        SetGain("SFXVolume", effectsVolume);
        SetGain("AmbienceVolume", effectsVolume * ambienceVolume);
    }

    private void ApplyVolumes()
    {
        SetGain("MusicVolume", musicVolume);
        SetGain("SFXVolume", effectsVolume);
        SetGain("AmbienceVolume", effectsVolume * ambienceVolume);
    }

    private void SetGain(string parameter, float volume)
    {
        if (mixer == null) return;
        float decibels = volume > 0.0001f ? Mathf.Log10(volume) * 20f : -80f;
        if (!mixer.SetFloat(parameter, decibels))
            Debug.LogError("Audio Mixer parameter missing: " + parameter, this);
    }
}
