using UnityEngine;

namespace RoomRevive
{
    /// <summary>
    /// Drives RoomRevive/HotspotPulseAdvanced via MaterialPropertyBlock.
    ///
    /// GazeT is computed every frame from the angle between the camera forward and the
    /// direction to the sphere collider center — no binary on/off, fully continuous.
    ///
    ///   angle = 0°             → GazeT 1  (camera aimed directly at sphere → invisible)
    ///   angle = _gazeAngle°    → GazeT 0  (camera aimed away → fully visible)
    ///
    /// _Alpha (shader "Overall Alpha") is driven 0–_idleAlpha via GazeT,
    /// making the sphere completely transparent when looked at directly.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class HotspotVisual : MonoBehaviour
    {
        [Header("Renderer")]
        [Tooltip("Sphere renderer using RoomRevive/HotspotPulseAdvanced. Auto-found on this GameObject if empty.")]
        [SerializeField] MeshRenderer _renderer;

        [Header("Gaze Detection")]
        [Tooltip("Sphere collider whose center is used as the gaze target. Auto-found on this GameObject if empty.")]
        [SerializeField] SphereCollider _collider;
        [Tooltip("Camera used for gaze direction. Defaults to Camera.main at runtime.")]
        [SerializeField] Camera _gazeCamera;
        [Tooltip("Angle in degrees from the sphere center at which the sphere is fully invisible (GazeT = 1). " +
                 "As the camera aims further than this angle away, GazeT smoothly falls back to 0.")]
        [SerializeField] float _gazeAngle = 20f;

        [Header("Scale")]
        [Tooltip("Local scale when the camera is NOT aimed at the sphere.")]
        [SerializeField] float _idleScale  = 1f;
        [Tooltip("Local scale when the camera is aimed directly at the sphere. 0 = fully collapsed.")]
        [SerializeField] float _gazedScale = 0.5f;

        [Header("Alpha")]
        [Tooltip("Overall Alpha (_Alpha) pushed to the shader when not gazed. Matches the shader range 0-2.")]
        [Range(0f, 2f)]
        [SerializeField] float _idleAlpha = 1f;

        [Header("Pulse")]
        [Tooltip("Oscillations per second while not gazed.")]
        [SerializeField] float _pulseSpeed = 1.4f;
        [Tooltip("Minimum pulse brightness at the trough (0 = fully dim, 1 = constant bright).")]
        [SerializeField] float _pulseMin   = 0.15f;

        [Header("Gaze Transition")]
        [Tooltip("How fast GazeT tracks the computed gaze value.")]
        [SerializeField] float _gazeSmoothing = 8f;

        [Header("Color")]
        [Tooltip("Primary rim color. Overridden at runtime by SetColor() when intent changes.")]
        [SerializeField] Color _baseColor = new Color(0.96f, 0.65f, 0.14f, 1f);

        [Header("Editor Preview")]
        [Tooltip("Toggle to preview the fully-gazed (invisible) state in the Scene view.")]
        [SerializeField] bool _previewGazed;

        // ── Shader property IDs ──────────────────────────────────────────────
        static readonly int ID_Color  = Shader.PropertyToID("_Color");
        static readonly int ID_Alpha  = Shader.PropertyToID("_Alpha");
        static readonly int ID_PulseT = Shader.PropertyToID("_PulseT");
        static readonly int ID_GazeT  = Shader.PropertyToID("_GazeT");

        // ── Runtime state ────────────────────────────────────────────────────
        MaterialPropertyBlock _mpb;
        float _gazeT;
        Color _currentColor;
        bool _suppressed;   // forced-hidden while the linked UI is open

        // ── Unity lifecycle ──────────────────────────────────────────────────

        void OnEnable()
        {
            Init();
            PushToShader(pulseT: 0f, gazeT: 0f);
        }

        void OnValidate()
        {
            Init();
            float g = _previewGazed ? 1f : 0f;
            transform.localScale = Vector3.one * Mathf.Lerp(_idleScale, _gazedScale, Mathf.SmoothStep(0f, 1f, g));
            PushToShader(pulseT: 0.5f, gazeT: g);
        }

        void Update()
        {
            if (!Application.isPlaying)
            {
                float g = _previewGazed ? 1f : 0f;
                transform.localScale = Vector3.one * Mathf.Lerp(_idleScale, _gazedScale, Mathf.SmoothStep(0f, 1f, g));
                PushToShader(pulseT: 0.5f, gazeT: g);
                return;
            }

            // ── Compute GazeT from camera angle (or force hidden while suppressed) ──
            float targetGazeT = _suppressed ? 1f : ComputeGazeT();
            _gazeT = Mathf.Lerp(_gazeT, targetGazeT, Time.deltaTime * _gazeSmoothing);

            // ── Scale ────────────────────────────────────────────────────────
            transform.localScale = Vector3.one * Mathf.Lerp(_idleScale, _gazedScale, Mathf.SmoothStep(0f, 1f, _gazeT));

            // ── Pulse — fades out as gaze increases ──────────────────────────
            float raw    = Mathf.Sin(Time.time * _pulseSpeed * Mathf.PI * 2f) * 0.5f + 0.5f;
            float pulseT = Mathf.Lerp(_pulseMin, 1f, raw) * (1f - _gazeT);

            PushToShader(pulseT, _gazeT);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        float ComputeGazeT()
        {
            Camera cam = _gazeCamera != null ? _gazeCamera : Camera.main;
            if (cam == null) return 0f;

            // World-space sphere center (respects local offset on the collider)
            Vector3 center = _collider != null
                ? transform.TransformPoint(_collider.center)
                : transform.position;

            float angle = Vector3.Angle(cam.transform.forward, center - cam.transform.position);

            // 0° → fully gazed (GazeT 1), _gazeAngle° → not gazed (GazeT 0)
            float t = Mathf.Clamp01(angle / Mathf.Max(0.01f, _gazeAngle));
            return 1f - Mathf.SmoothStep(0f, 1f, t);
        }

        void Init()
        {
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            if (_collider  == null) _collider = GetComponent<SphereCollider>();
            if (_mpb == null)       _mpb      = new MaterialPropertyBlock();
            if (_currentColor == default) _currentColor = _baseColor;
        }

        void PushToShader(float pulseT, float gazeT)
        {
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(ID_Color,  _currentColor == default ? _baseColor : _currentColor);
            _mpb.SetFloat(ID_Alpha,  Mathf.Lerp(_idleAlpha, 0f, gazeT));
            _mpb.SetFloat(ID_PulseT, pulseT);
            _mpb.SetFloat(ID_GazeT,  gazeT);
            _renderer.SetPropertyBlock(_mpb);
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Called by HotspotInteractable when the active intent color changes.</summary>
        public void SetColor(Color color)
        {
            _currentColor = color;
            PushToShader(0f, _gazeT);
        }

        /// <summary>Force the sphere hidden regardless of gaze angle (e.g. while the linked UI is open).</summary>
        public void SetSuppressed(bool suppressed) => _suppressed = suppressed;
    }
}
