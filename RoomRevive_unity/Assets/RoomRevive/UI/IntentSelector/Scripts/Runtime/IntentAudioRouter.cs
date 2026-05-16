using UnityEngine;

namespace RoomRevive.IntentSelector
{
    /// <summary>
    /// Plays state.atmosphereMusic through AudioManager when a state is selected.
    /// No hard-coded room->clip map — the clip is on the IntentStateData asset.
    /// </summary>
    [DisallowMultipleComponent]
    public class IntentAudioRouter : MonoBehaviour
    {
        [Header("Audio Manager")]
        public AudioManager audioManager;
        public bool autoFindAudioManager = true;

        [Header("Behavior")]
        public bool playOnSelected = true;
        public bool playOnConfirmed = false;

        [Header("Debug")]
        public bool debugLogs = false;

        void Awake() => TryAutoFind();
        void OnEnable() => TryAutoFind();

        void TryAutoFind()
        {
            if (!autoFindAudioManager || audioManager != null) return;
            audioManager = AudioManager.GetOrFindInstance();
        }

        public void RouteSelected(IntentStateData state)
        {
            if (!playOnSelected) return;
            PlayState(state);
        }

        public void RouteConfirmed(IntentStateData state)
        {
            if (!playOnConfirmed) return;
            PlayState(state);
        }

        void PlayState(IntentStateData state)
        {
            if (state == null || state.atmosphereMusic == null) return;

            TryAutoFind();
            if (audioManager == null)
            {
                if (debugLogs) Debug.LogWarning("[IntentAudioRouter] No AudioManager found.", this);
                return;
            }

            audioManager.PlayAtmosphereClip(state.atmosphereMusic);
            if (debugLogs) Debug.Log($"[IntentAudioRouter] Playing {state.atmosphereMusic.name} for {state.name}", this);
        }
    }
}
