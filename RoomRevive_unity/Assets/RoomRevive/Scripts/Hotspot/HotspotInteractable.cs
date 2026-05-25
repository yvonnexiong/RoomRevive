using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RoomRevive
{
    [RequireComponent(typeof(Collider))]
    public class HotspotInteractable : MonoBehaviour
    {
        [SerializeField] private HotspotSO _data;

        [Header("Display")]
        [SerializeField] private GameObject displayTarget;

        [Header("Dwell Ring")]
        [SerializeField] private Image _dwellRing;
        [SerializeField] private RectTransform _dwellRingRect;

        [Header("Glow Dot")]
        [SerializeField] private SpriteRenderer _glowDot;
        [SerializeField] private float _glowDotSize = 1.92f;

        [Header("Pulse Visual")]
        [Tooltip("HotspotVisual driving the pulse shader. Auto-found on this GameObject if empty.")]
        [SerializeField] private HotspotVisual _visual;

        public static event System.Action<ProductSO> OnAnySelected;

        private Vector3 _baseScale;
        private Vector3 _glowDotBaseScale;

        private Coroutine _ringAlphaCoroutine;
        private Coroutine _glowDotScaleCoroutine;

        private Color _currentRingColor = new Color(0.96f, 0.65f, 0.14f, 1f);

        private bool _isGazed = false;
        private float _smoothedProximityT = 0f;

        void Awake()
        {
            _baseScale = transform.localScale;
            if (_visual == null) _visual = GetComponent<HotspotVisual>();
        }

        void Start()
        {
            // Hide the linked browser UI on startup — only revealed on gaze select.
            if (displayTarget != null) displayTarget.SetActive(false);

            if (_dwellRing != null)
            {
                _dwellRing.fillAmount = 1f;
                Color c = _currentRingColor;
                c.a = 0f;
                _dwellRing.color = c;
            }

            if (_glowDot != null)
            {
                _glowDotBaseScale = Vector3.one * _glowDotSize; // use serialized size, not transform
                _glowDot.transform.localScale = Vector3.zero;
                Color c = _glowDot.color;
                c.a = 0.6f;
                _glowDot.color = c;
            }

            if (IntentManager.Instance != null)
                IntentManager.Instance.OnIntentChanged += OnIntentChanged;
        }

        void OnDestroy()
        {
            if (IntentManager.Instance != null)
                IntentManager.Instance.OnIntentChanged -= OnIntentChanged;
        }

        public void OnGazeEnter()
        {
            _isGazed = true;
            StartRingAlpha(1f, 0.35f);
            ScaleGlowDot(_glowDotBaseScale, 0.35f);
        }

        public void OnGazeExit()
        {
            _isGazed = false;
            displayTarget?.SetActive(false);
            StartRingAlpha(0f, 0.35f);
            // Cancel dot coroutine — proximity takes over from current position
            if (_glowDotScaleCoroutine != null) { StopCoroutine(_glowDotScaleCoroutine); _glowDotScaleCoroutine = null; }
            // Sync smoothed value to current scale so proximity picks up smoothly
            if (_glowDot != null && _glowDotBaseScale.x > 0f)
                _smoothedProximityT = _glowDot.transform.localScale.x / _glowDotBaseScale.x;
        }

        public void OnGazeDwell(float t)
        {
            // no animation during dwell — ring stays steady
        }

        public void OnGazeSelect()
        {
            // Already open — don't re-fire OpenDiscover and clobber the browser's current state.
            if (displayTarget != null && displayTarget.activeSelf) return;

            _isGazed = false;
            if (_glowDotScaleCoroutine != null) { StopCoroutine(_glowDotScaleCoroutine); _glowDotScaleCoroutine = null; }
            if (_glowDot != null && _glowDotBaseScale.x > 0f)
                _smoothedProximityT = _glowDot.transform.localScale.x / _glowDotBaseScale.x;

            StartRingAlpha(0f, 0.15f);
            displayTarget?.SetActive(true);
        }

        // Called every frame by GazeHotspotDetector for all hotspots
        public void SetProximityScale(float targetT)
        {
            if (_isGazed)
            {
                _smoothedProximityT = 1f; // keep synced so exit transition is smooth
                return;
            }
            // Lazy init in case Start() hasn't run yet (hotspot was inactive at game start)
            if (_glowDotBaseScale.sqrMagnitude < 0.0001f)
                _glowDotBaseScale = Vector3.one * _glowDotSize;

            _smoothedProximityT = Mathf.Lerp(_smoothedProximityT, targetT, Time.deltaTime * 8f);
            if (_glowDot != null)
                _glowDot.transform.localScale = _glowDotBaseScale * _smoothedProximityT;
        }

        private void OnIntentChanged(IntentSO intent)
        {
            _currentRingColor = intent.hotspotColor;

            if (_dwellRing != null)
            {
                Color c = _currentRingColor;
                c.a = _dwellRing.color.a;
                _dwellRing.color = c;
            }

            if (_glowDot != null)
                _glowDot.color = new Color(_currentRingColor.r, _currentRingColor.g, _currentRingColor.b, 0.6f);

            _visual?.SetColor(_currentRingColor);
        }

        private void StartRingAlpha(float target, float duration)
        {
            if (_ringAlphaCoroutine != null) StopCoroutine(_ringAlphaCoroutine);
            _ringAlphaCoroutine = StartCoroutine(FadeRingAlpha(target, duration));
        }

        private void ScaleGlowDot(Vector3 target, float duration)
        {
            if (_glowDotScaleCoroutine != null) StopCoroutine(_glowDotScaleCoroutine);
            _glowDotScaleCoroutine = StartCoroutine(ScaleGlowDotTo(target, duration));
        }

        private IEnumerator FadeRingAlpha(float target, float duration)
        {
            if (_dwellRing == null) yield break;
            float startA = _dwellRing.color.a;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                Color c = _currentRingColor;
                c.a = Mathf.Lerp(startA, target, Mathf.Clamp01(t));
                _dwellRing.color = c;
                yield return null;
            }
            Color fc = _currentRingColor;
            fc.a = target;
            _dwellRing.color = fc;
        }

        private IEnumerator ScaleGlowDotTo(Vector3 target, float duration)
        {
            if (_glowDot == null) yield break;
            Vector3 start = _glowDot.transform.localScale;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                _glowDot.transform.localScale = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                yield return null;
            }
            _glowDot.transform.localScale = target;
        }
    }
}
