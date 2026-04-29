using System.Collections;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace RoomRevive
{
    public class EffectMeshToggle : MonoBehaviour
    {
        private EffectMesh _effectMesh;
        private bool _isVisible = false;
        private AudioSource _audio;

        private AudioClip _beepA;
        private AudioClip _beepB;

        void Start()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f; // 2D audio — not affected by distance
            _audio.volume = 1f;
            _beepA = GenerateBeep(frequency: 880f, durationSeconds: 0.1f);
            _beepB = GenerateBeep(frequency: 660f, durationSeconds: 0.12f);

            if (MRUK.Instance != null)
            {
                MRUK.Instance.RegisterSceneLoadedCallback(OnSceneLoaded);
                if (!MRUK.Instance.IsInitialized)
                    MRUK.Instance.LoadSceneFromDevice();
            }
        }

        private void OnSceneLoaded()
        {
            // Wait one frame so EffectMesh finishes generating its meshes first
            StartCoroutine(HideAfterFrame());
        }

        private IEnumerator HideAfterFrame()
        {
            yield return null;
            _effectMesh = FindObjectOfType<EffectMesh>();
            if (_effectMesh != null)
            {
                _isVisible = false;
                _effectMesh.ToggleEffectMeshVisibility(false);
            }
        }

        void Update()
        {
            if (OVRInput.GetDown(OVRInput.Button.One))
            {
                _effectMesh = FindObjectOfType<EffectMesh>();

                if (_effectMesh != null)
                {
                    _isVisible = !_isVisible;
                    _effectMesh.ToggleEffectMeshVisibility(_isVisible);
                }

                _audio.PlayOneShot(_beepA);
            }

            if (OVRInput.GetDown(OVRInput.Button.Two))
            {
                _audio.PlayOneShot(_beepB);
            }
        }

        private AudioClip GenerateBeep(float frequency, float durationSeconds)
        {
            int sampleRate = AudioSettings.outputSampleRate;
            if (sampleRate <= 0) sampleRate = 48000;
            int sampleCount = Mathf.RoundToInt(sampleRate * durationSeconds);

            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f - (float)i / sampleCount;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.5f;
            }

            AudioClip clip = AudioClip.Create("Beep", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}

