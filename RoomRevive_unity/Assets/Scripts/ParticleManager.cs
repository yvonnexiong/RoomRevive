using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Plays particle feedback for each splat intent / room.
/// Called by SplatManager.
/// </summary>
public class ParticlesManager : MonoBehaviour
{
    public static ParticlesManager Instance { get; private set; }

    [Header("Singleton")]
    public bool useSingleton = true;
    public bool destroyDuplicateManagers = true;
    public bool dontDestroyOnLoad = false;

    [Header("Intent Particle Effects")]
    [Tooltip("Particle effect for CalmRoom intent.")]
    public ParticleSystem calmRoomParticles;

    [Tooltip("Particle effect for FastRoom intent.")]
    public ParticleSystem fastRoomParticles;

    [Tooltip("Particle effect for HostRoom intent.")]
    public ParticleSystem hostRoomParticles;

    [Header("Transition Particle Effect")]
    [Tooltip("Particle effect played whenever changing between intents.")]
    public ParticleSystem roomChangeParticles;

    [Header("Playback")]
    [Tooltip("If true, only the active intent particle effect will play.")]
    public bool stopOtherIntentParticlesWhenPlayingNewIntent = true;

    [Tooltip("If true, intent particles are cleared before playing.")]
    public bool clearIntentParticlesBeforePlay = true;

    [Tooltip("If true, transition particles are cleared before playing.")]
    public bool clearTransitionParticlesBeforePlay = true;

    [Tooltip("If true, room particle effects are stopped when SplatManager hides all rooms.")]
    public bool stopParticlesWhenNoRoomActive = true;

    [Header("Debug")]
    public bool debugLogs = true;

    // ─────────────────────────────────────────────────────────────
    // Singleton
    // ─────────────────────────────────────────────────────────────

    public static ParticlesManager GetOrFindInstance()
    {
        if (Instance != null)
            return Instance;

#if UNITY_2023_1_OR_NEWER
        Instance = FindFirstObjectByType<ParticlesManager>(FindObjectsInactive.Include);
#else
        Instance = FindObjectOfType<ParticlesManager>(true);
#endif

        return Instance;
    }

    private void Awake()
    {
        RegisterSingleton();
    }

    private void OnEnable()
    {
        RegisterSingleton();
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
                    $"<b>[ParticlesManager]</b> Duplicate ParticlesManager found on {name}. Existing instance is {Instance.name}.",
                    this
                );
            }

            if (destroyDuplicateManagers && Application.isPlaying)
                Destroy(gameObject);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Public API called by SplatManager
    // ─────────────────────────────────────────────────────────────

    public void PlayIntentParticles(SplatManager.SplatRoom room)
    {
        ParticleSystem target = GetParticlesForRoom(room);

        if (target == null)
        {
            if (debugLogs)
                Debug.LogWarning($"<b>[ParticlesManager]</b> No particle effect assigned for {room}.", this);

            return;
        }

        if (stopOtherIntentParticlesWhenPlayingNewIntent)
            StopAllIntentParticlesExcept(target);

        PlayParticleSystem(target, clearIntentParticlesBeforePlay);

        if (debugLogs)
            Debug.Log($"<color=cyan><b>[ParticlesManager]</b></color> Playing intent particles for {room}: {target.name}", this);
    }

    public void PlayRoomChangeParticles()
    {
        if (roomChangeParticles == null)
        {
            if (debugLogs)
                Debug.LogWarning("<b>[ParticlesManager]</b> No roomChangeParticles assigned.", this);

            return;
        }

        PlayParticleSystem(roomChangeParticles, clearTransitionParticlesBeforePlay);

        if (debugLogs)
            Debug.Log($"<color=cyan><b>[ParticlesManager]</b></color> Playing room change particles: {roomChangeParticles.name}", this);
    }

    public void StopAllIntentParticles()
    {
        StopParticleSystem(calmRoomParticles);
        StopParticleSystem(fastRoomParticles);
        StopParticleSystem(hostRoomParticles);

        if (debugLogs)
            Debug.Log("<b>[ParticlesManager]</b> Stopped all intent particles.", this);
    }

    public void StopAllParticles()
    {
        StopAllIntentParticles();
        StopParticleSystem(roomChangeParticles);

        if (debugLogs)
            Debug.Log("<b>[ParticlesManager]</b> Stopped all particles.", this);
    }

    // ─────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────

    private ParticleSystem GetParticlesForRoom(SplatManager.SplatRoom room)
    {
        switch (room)
        {
            case SplatManager.SplatRoom.CalmRoom:
                return calmRoomParticles;

            case SplatManager.SplatRoom.FastRoom:
                return fastRoomParticles;

            case SplatManager.SplatRoom.HostRoom:
                return hostRoomParticles;

            default:
                return null;
        }
    }

    private void StopAllIntentParticlesExcept(ParticleSystem exception)
    {
        if (calmRoomParticles != exception)
            StopParticleSystem(calmRoomParticles);

        if (fastRoomParticles != exception)
            StopParticleSystem(fastRoomParticles);

        if (hostRoomParticles != exception)
            StopParticleSystem(hostRoomParticles);
    }

    private void PlayParticleSystem(ParticleSystem particles, bool clearBeforePlay)
    {
        if (particles == null)
            return;

        if (!particles.gameObject.activeSelf)
            particles.gameObject.SetActive(true);

        if (clearBeforePlay)
            particles.Clear(true);

        particles.Play(true);
    }

    private void StopParticleSystem(ParticleSystem particles)
    {
        if (particles == null)
            return;

        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EditorUtility.SetDirty(this);
    }
#endif
}