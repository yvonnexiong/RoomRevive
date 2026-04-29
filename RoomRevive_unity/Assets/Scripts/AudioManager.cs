using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Plays startup music, atmosphere music, and transition SFX for each splat intent / room.
/// Startup music is intended for the first scene load before any room/intent trigger is active.
/// Room atmosphere music is started by SplatManager or MetaRayIntentCardMenu.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Singleton")]
    public bool useSingleton = true;
    public bool destroyDuplicateManagers = true;
    public bool dontDestroyOnLoad = false;

    [Header("Audio Sources")]
    [Tooltip("AudioSource used for looping startup music and looping atmosphere music.")]
    public AudioSource musicSource;

    [Tooltip("AudioSource used for one-shot transition sounds.")]
    public AudioSource sfxSource;

    [Header("Start Music")]
    [Tooltip("Music that plays when the scene first loads before any room/intent trigger takes over.")]
    public AudioClip startMusic;

    [Tooltip("If true, StartMusic plays automatically when this AudioManager starts in play mode.")]
    public bool playStartMusicOnSceneLoad = true;

    [Tooltip("If true, StartMusic loops until a room atmosphere replaces it.")]
    public bool loopStartMusic = true;

    [Header("Atmosphere Music Clips")]
    public AudioClip calmRoomAtmosphere;
    public AudioClip fastRoomAtmosphere;
    public AudioClip hostRoomAtmosphere;

    [Header("Transition Sound")]
    public AudioClip roomChangeSfx;

    [Header("Music Settings")]
    [Range(0f, 1f)]
    public float musicVolume = 0.55f;

    [Range(0f, 1f)]
    public float sfxVolume = 0.85f;

    [Tooltip("If true, atmosphere tracks loop.")]
    public bool loopAtmosphereMusic = true;

    [Tooltip("Crossfade duration when changing between startup music and room atmosphere tracks.")]
    [Min(0f)]
    public float musicFadeDuration = 0.8f;

    [Tooltip("If true, the same music clip will not restart if it is already playing.")]
    public bool doNotRestartSameMusic = true;

    [Header("Debug")]
    public bool debugLogs = true;

    private Coroutine _fadeRoutine;
    private SplatManager.SplatRoom _currentMusicRoom = SplatManager.SplatRoom.None;
    private bool _isPlayingStartMusic;

    // ─────────────────────────────────────────────────────────────
    // Singleton
    // ─────────────────────────────────────────────────────────────

    public static AudioManager GetOrFindInstance()
    {
        if (Instance != null)
            return Instance;

#if UNITY_2023_1_OR_NEWER
        Instance = FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);
#else
        Instance = FindObjectOfType<AudioManager>(true);
#endif

        return Instance;
    }

    private void Reset()
    {
        EnsureAudioSources();
    }

    private void Awake()
    {
        RegisterSingleton();
        EnsureAudioSources();
    }

    private void OnEnable()
    {
        RegisterSingleton();
        EnsureAudioSources();
    }

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        EnsureAudioSources();

        if (playStartMusicOnSceneLoad)
            PlayStartMusic();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void RegisterSingleton()
    {
        if (!useSingleton)
            return;

        if (Instance == null || Instance == this)
        {
            Instance = this;

            if (dontDestroyOnLoad && Application.isPlaying)
                DontDestroyOnLoad(gameObject);

            return;
        }

        if (Instance != this)
        {
            if (debugLogs)
            {
                Debug.LogWarning(
                    $"<b>[AudioManager]</b> Duplicate AudioManager found on {name}. Existing instance is {Instance.name}.",
                    this
                );
            }

            if (destroyDuplicateManagers && Application.isPlaying)
                Destroy(gameObject);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────

    public void PlayStartMusic()
    {
        EnsureAudioSources();

        if (startMusic == null)
        {
            if (debugLogs)
                Debug.LogWarning("<b>[AudioManager]</b> No StartMusic clip assigned.", this);

            return;
        }

        if (doNotRestartSameMusic &&
            _isPlayingStartMusic &&
            musicSource.clip == startMusic &&
            musicSource.isPlaying)
        {
            return;
        }

        _currentMusicRoom = SplatManager.SplatRoom.None;
        _isPlayingStartMusic = true;

        PlayMusicClip(startMusic, loopStartMusic, "StartMusic");
    }

    public void PlayAtmosphereForRoom(SplatManager.SplatRoom room)
    {
        EnsureAudioSources();

        AudioClip targetClip = GetAtmosphereClipForRoom(room);

        if (targetClip == null)
        {
            if (debugLogs)
                Debug.LogWarning($"<b>[AudioManager]</b> No atmosphere clip assigned for {room}.", this);

            return;
        }

        if (doNotRestartSameMusic &&
            !_isPlayingStartMusic &&
            _currentMusicRoom == room &&
            musicSource.clip == targetClip &&
            musicSource.isPlaying)
        {
            return;
        }

        _currentMusicRoom = room;
        _isPlayingStartMusic = false;

        PlayMusicClip(targetClip, loopAtmosphereMusic, $"atmosphere for {room}");
    }

    public void PlayRoomChangeSfx()
    {
        EnsureAudioSources();

        if (roomChangeSfx == null)
        {
            if (debugLogs)
                Debug.LogWarning("<b>[AudioManager]</b> No roomChangeSfx assigned.", this);

            return;
        }

        sfxSource.PlayOneShot(roomChangeSfx, sfxVolume);

        if (debugLogs)
            Debug.Log($"<color=orange><b>[AudioManager]</b></color> Playing room change SFX: {roomChangeSfx.name}", this);
    }

    public void StopAtmosphereMusic()
    {
        EnsureAudioSources();

        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        musicSource.Stop();
        musicSource.clip = null;
        _currentMusicRoom = SplatManager.SplatRoom.None;
        _isPlayingStartMusic = false;

        if (debugLogs)
            Debug.Log("<b>[AudioManager]</b> Stopped music.", this);
    }

    public void StopAllAudio()
    {
        StopAtmosphereMusic();

        if (sfxSource != null)
            sfxSource.Stop();

        if (debugLogs)
            Debug.Log("<b>[AudioManager]</b> Stopped all audio.", this);
    }

    // ─────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────

    private void PlayMusicClip(AudioClip targetClip, bool loop, string debugLabel)
    {
        if (targetClip == null)
            return;

        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        if (musicFadeDuration <= 0f || !Application.isPlaying)
        {
            musicSource.clip = targetClip;
            musicSource.volume = musicVolume;
            musicSource.loop = loop;
            musicSource.Play();

            if (debugLogs)
                Debug.Log($"<color=orange><b>[AudioManager]</b></color> Playing {debugLabel}: {targetClip.name}", this);

            return;
        }

        _fadeRoutine = StartCoroutine(FadeToNewMusic(targetClip, loop, debugLabel));
    }

    private AudioClip GetAtmosphereClipForRoom(SplatManager.SplatRoom room)
    {
        switch (room)
        {
            case SplatManager.SplatRoom.CalmRoom:
                return calmRoomAtmosphere;

            case SplatManager.SplatRoom.FastRoom:
                return fastRoomAtmosphere;

            case SplatManager.SplatRoom.HostRoom:
                return hostRoomAtmosphere;

            default:
                return null;
        }
    }

    private IEnumerator FadeToNewMusic(AudioClip targetClip, bool loop, string debugLabel)
    {
        float startVolume = musicSource.volume;
        float fadeOutDuration = musicSource.isPlaying ? musicFadeDuration * 0.5f : 0f;

        if (fadeOutDuration > 0f)
        {
            float t = 0f;

            while (t < fadeOutDuration)
            {
                t += Time.deltaTime;
                float normalized = Mathf.Clamp01(t / fadeOutDuration);
                musicSource.volume = Mathf.Lerp(startVolume, 0f, normalized);
                yield return null;
            }
        }

        musicSource.Stop();
        musicSource.clip = targetClip;
        musicSource.loop = loop;
        musicSource.volume = 0f;
        musicSource.Play();

        float fadeInDuration = musicFadeDuration * 0.5f;
        float tIn = 0f;

        while (tIn < fadeInDuration)
        {
            tIn += Time.deltaTime;
            float normalized = Mathf.Clamp01(tIn / fadeInDuration);
            musicSource.volume = Mathf.Lerp(0f, musicVolume, normalized);
            yield return null;
        }

        musicSource.volume = musicVolume;
        _fadeRoutine = null;

        if (debugLogs)
            Debug.Log($"<color=orange><b>[AudioManager]</b></color> Playing {debugLabel}: {targetClip.name}", this);
    }

    private void EnsureAudioSources()
    {
        if (musicSource == null)
        {
            Transform existingMusic = transform.Find("MusicSource");

            if (existingMusic != null)
                musicSource = existingMusic.GetComponent<AudioSource>();

            if (musicSource == null)
            {
                GameObject musicGO = new GameObject("MusicSource");
                musicGO.transform.SetParent(transform, false);
                musicSource = musicGO.AddComponent<AudioSource>();
            }
        }

        if (sfxSource == null)
        {
            Transform existingSfx = transform.Find("SfxSource");

            if (existingSfx != null)
                sfxSource = existingSfx.GetComponent<AudioSource>();

            if (sfxSource == null)
            {
                GameObject sfxGO = new GameObject("SfxSource");
                sfxGO.transform.SetParent(transform, false);
                sfxSource = sfxGO.AddComponent<AudioSource>();
            }
        }

        musicSource.playOnAwake = false;
        musicSource.loop = loopAtmosphereMusic;
        musicSource.volume = musicVolume;

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.volume = sfxVolume;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            EnsureAudioSources();

        musicVolume = Mathf.Clamp01(musicVolume);
        sfxVolume = Mathf.Clamp01(sfxVolume);
        musicFadeDuration = Mathf.Max(0f, musicFadeDuration);

        EditorUtility.SetDirty(this);
    }
#endif
}
