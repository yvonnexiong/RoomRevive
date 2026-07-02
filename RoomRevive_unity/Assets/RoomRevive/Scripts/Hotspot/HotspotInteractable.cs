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

        [Header("Keep-Open Gaze")]
        [Tooltip("A collider placed over the UI / gaze sphere. Once the UI is open it stays open while " +
                 "the user faces this collider OR this hotspot; it closes when facing neither. Leave empty " +
                 "for the old behavior (close as soon as gaze leaves the hotspot).")]
        [SerializeField] private Collider _uiGazeCollider;
        [Tooltip("Gaze origin (head/camera). Auto-finds CenterEyeAnchor, then Camera.main, if empty.")]
        [SerializeField] private Transform _gazeOrigin;
        [Tooltip("Max ray distance for the facing test.")]
        [SerializeField] private float _maxGazeDistance = 10f;
        [Tooltip("Keep the UI open this long after the user looks away from both, to avoid flicker.")]
        [SerializeField] private float _closeGraceSeconds = 0.4f;

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

        private Collider _selfCollider;
        private bool _uiOpen = false;
        private float _awayTimer = 0f;

        void Awake()
        {
            _baseScale = transform.localScale;
            if (_visual == null) _visual = GetComponent<HotspotVisual>();
            _selfCollider = GetComponent<Collider>();
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

        private void OnDisable()
        {
            // Disabled GameObjects cannot run coroutines. Reset every transient
            // visual state so cached gaze references cannot leave stale visuals.
            StopAllCoroutines();
            _ringAlphaCoroutine = null;
            _glowDotScaleCoroutine = null;
            _isGazed = false;
            _smoothedProximityT = 0f;
            _uiOpen = false;
            _awayTimer = 0f;
            SetVisualsSuppressed(false);

            SetRingAlphaImmediate(0f);

            if (_glowDot != null)
                _glowDot.transform.localScale = Vector3.zero;

            if (displayTarget != null)
                displayTarget.SetActive(false);
        }

        public void OnGazeEnter()
        {
            if (!isActiveAndEnabled) return;

            _isGazed = true;
            StartRingAlpha(1f, 0.35f);
            ScaleGlowDot(_glowDotBaseScale, 0.35f);
        }

        public void OnGazeExit()
        {
            _isGazed = false;

            // With a keep-open collider, don't close on hotspot-exit — the per-frame facing check in
            // Update() decides, so the UI survives while the user is looking at the UI/gaze sphere.
            bool deferToFacingCheck = _uiGazeCollider != null && _uiOpen &&
                                      displayTarget != null && displayTarget.activeSelf;
            if (!deferToFacingCheck && displayTarget != null)
            {
                displayTarget.SetActive(false);   // Unity-null check: '?.' doesn't catch an unassigned (fake-null) field
                _uiOpen = false;
                SetVisualsSuppressed(false);
            }

            if (!isActiveAndEnabled)
            {
                SetRingAlphaImmediate(0f);
                return;
            }

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
            if (!isActiveAndEnabled) return;

            // Already open — don't re-fire OpenDiscover and clobber the browser's current state.
            if (displayTarget != null && displayTarget.activeSelf) return;

            _isGazed = false;
            if (_glowDotScaleCoroutine != null) { StopCoroutine(_glowDotScaleCoroutine); _glowDotScaleCoroutine = null; }
            if (_glowDot != null && _glowDotBaseScale.x > 0f)
                _smoothedProximityT = _glowDot.transform.localScale.x / _glowDotBaseScale.x;

            StartRingAlpha(0f, 0.15f);
            if (displayTarget != null) displayTarget.SetActive(true);   // Unity-null check (see OnGazeExit)

            _uiOpen = true;
            _awayTimer = 0f;
            SetVisualsSuppressed(true);   // keep the sphere/glow hidden while the UI is open
        }

        void Update()
        {
            // Keep-open facing check: while the UI is open, hold it open as long as the gaze ray points
            // at the UI/gaze-sphere collider OR this hotspot; close it once it faces neither for a moment.
            if (!_uiOpen || _uiGazeCollider == null) return;

            if (displayTarget == null || !displayTarget.activeSelf) { _uiOpen = false; return; }

            Transform origin = ResolveGazeOrigin();
            if (origin == null) return;

            Ray ray = new Ray(origin.position, origin.forward);
            bool facing =
                _uiGazeCollider.Raycast(ray, out _, _maxGazeDistance) ||
                (_selfCollider != null && _selfCollider.Raycast(ray, out _, _maxGazeDistance)) ||
                _isGazed;

            if (facing)
            {
                _awayTimer = 0f;
            }
            else
            {
                _awayTimer += Time.deltaTime;
                if (_awayTimer >= _closeGraceSeconds)
                {
                    displayTarget.SetActive(false);
                    _uiOpen = false;
                    SetVisualsSuppressed(false);   // let the sphere come back once the UI closes
                    StartRingAlpha(0f, 0.25f);
                }
            }
        }

        // Force-hide (or release) the hotspot sphere + glow dot while the UI is open.
        private void SetVisualsSuppressed(bool suppressed)
        {
            if (_visual != null) _visual.SetSuppressed(suppressed);
            if (suppressed && _glowDot != null)
                _glowDot.transform.localScale = Vector3.zero;
        }

        private Transform ResolveGazeOrigin()
        {
            if (_gazeOrigin != null) return _gazeOrigin;
            GameObject eye = GameObject.Find("CenterEyeAnchor");
            _gazeOrigin = eye != null ? eye.transform : (Camera.main != null ? Camera.main.transform : null);
            return _gazeOrigin;
        }

        // Called every frame by GazeHotspotDetector for all hotspots
        public void SetProximityScale(float targetT)
        {
            if (!isActiveAndEnabled) return;

            // While the UI is open, keep the glow dot hidden — no hotspot visuals should show.
            if (_uiOpen)
            {
                if (_glowDot != null) _glowDot.transform.localScale = Vector3.zero;
                return;
            }

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
            if (!isActiveAndEnabled)
            {
                SetRingAlphaImmediate(target);
                return;
            }

            if (_ringAlphaCoroutine != null) StopCoroutine(_ringAlphaCoroutine);
            _ringAlphaCoroutine = StartCoroutine(FadeRingAlpha(target, duration));
        }

        private void ScaleGlowDot(Vector3 target, float duration)
        {
            if (!isActiveAndEnabled)
            {
                if (_glowDot != null)
                    _glowDot.transform.localScale = target;
                return;
            }

            if (_glowDotScaleCoroutine != null) StopCoroutine(_glowDotScaleCoroutine);
            _glowDotScaleCoroutine = StartCoroutine(ScaleGlowDotTo(target, duration));
        }

        private void SetRingAlphaImmediate(float alpha)
        {
            if (_dwellRing == null) return;

            Color color = _currentRingColor;
            color.a = alpha;
            _dwellRing.color = color;
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
