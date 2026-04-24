using UnityEngine;
using UnityEngine.UI;

namespace RoomRevive
{
    public class IntentSelectorUI : MonoBehaviour
    {
        [SerializeField] private Button calmButton;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button fastButton;

        [SerializeField] private IntentSO calm;
        [SerializeField] private IntentSO host;
        [SerializeField] private IntentSO fast;

        private AudioSource _audio;

        void Start()
        {
            // Audio on IntentManager so it keeps playing after this panel hides
            _audio = IntentManager.Instance.gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _audio.clip = GenerateBeep(0.12f, 880f);

            calmButton.onClick.AddListener(() => OnButtonClick(calm));
            hostButton.onClick.AddListener(() => OnButtonClick(host));
            fastButton.onClick.AddListener(() => OnButtonClick(fast));

        }

        void OnButtonClick(IntentSO intent)
        {
            _audio?.Play();
            IntentManager.Instance.SetIntent(intent);
            gameObject.SetActive(false);
        }

        AudioClip GenerateBeep(float duration, float frequency)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            var clip = AudioClip.Create("Beep", samples, 1, sampleRate, false);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * (1f - t / duration) * 0.5f;
            }
            clip.SetData(data, 0);
            return clip;
        }
    }
}
