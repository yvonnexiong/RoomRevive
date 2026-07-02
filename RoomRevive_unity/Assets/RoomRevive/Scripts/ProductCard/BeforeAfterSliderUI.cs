using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UIImage = UnityEngine.UI.Image;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RoomRevive
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public class BeforeAfterSliderUI : MonoBehaviour
    {
        // ── Design tokens ─────────────────────────────────────────────────────
        static readonly Color PillBg      = new Color(0.835f, 0.851f, 0.898f, 0.35f);
        static readonly Color PillBorder  = new Color(1f, 1f, 1f, 0.18f);
        static readonly Color TrackBgCol  = new Color(0.227f, 0.251f, 0.333f, 0.35f);
        static readonly Color TrackFill   = Hex(0x3A4055);
        static readonly Color LabelColor  = Color.white;

        // ── Static trigger ────────────────────────────────────────────────────
        /// <summary>Fires whenever the slider value changes (0 = Before, 1 = After).</summary>
        public static System.Action<float> OnValueChanged;

        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Canvas")]
        [SerializeField] public float  worldScale  = 0.001f;
        [SerializeField] public Camera eventCamera;

        [Header("Head Follow")]
        [SerializeField] public float distance    = 1.4f;
        [SerializeField] public float rightOffset = 0f;
        [SerializeField] public float upOffset    = -0.3f;

        [Header("Layout (px)")]
        [SerializeField] public float pillWidth    = 560f;
        [SerializeField] public float padH         = 28f;
        [SerializeField] public float padV         = 22f;
        [SerializeField] public float labelWidth   = 50f;
        [SerializeField] public float gap          = 20f;

        [Header("Slider")]
        [Range(0f, 1f)]
        [SerializeField] public float initialValue = 0.65f;

        [Header("Font")]
        [SerializeField] public TMP_FontAsset fontAsset;

        [Header("Auto Rebuild")]
        [SerializeField] public bool autoRebuildInEditor = true;

        // ── Runtime ───────────────────────────────────────────────────────────
        Canvas          _canvas;
        CanvasScaler    _scaler;
        CanvasGroup     _cg;
        Transform       _cam;
        Coroutine       _fade;
        GameObject      _root;
        Slider          _slider;
        List<Texture2D> _textures = new List<Texture2D>();

        const float TrackH    = 6f;
        const float HandleSz  = 28f;
        const float DotSz     = 8f;
        const float PillR     = 8f;

#if UNITY_EDITOR
        bool _rebuildQueued;
#endif

        // ── Lifecycle ─────────────────────────────────────────────────────────

        void Awake()     { Grab(); SetupCanvas(); }
        void OnDestroy() { ClearTextures(); }

        void Start()
        {
            if (!Application.isPlaying) return;
            var eye = GameObject.Find("CenterEyeAnchor");
            _cam = eye ? eye.transform : Camera.main?.transform;
            Rebuild();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || _cam == null) return;
            if (_cg == null || _cg.alpha < 0.01f) return;
            transform.position = _cam.position
                + _cam.forward * distance
                + _cam.right   * rightOffset
                + _cam.up      * upOffset;
            transform.rotation = Quaternion.LookRotation(_cam.forward);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying || !autoRebuildInEditor || _rebuildQueued) return;
            _rebuildQueued = true;
            EditorApplication.delayCall += () =>
            {
                _rebuildQueued = false;
                if (this == null) return;
                Grab(); SetupCanvas(); Rebuild();
            };
        }
#endif

        // ── Public API ────────────────────────────────────────────────────────

        [ContextMenu("Rebuild UI")]
        public void Rebuild()
        {
            Clear();
            Grab();
            SetupCanvas();
            _root = MakeGO("Generated_BeforeAfterSlider", transform);
            Stretch(_root);
            Build(_root.transform);

            if (fontAsset != null)
                foreach (var t in _root.GetComponentsInChildren<TextMeshProUGUI>(true))
                    t.font = fontAsset;

            if (_slider != null)
                _slider.value = initialValue;
        }

        public void Show()
        {
            if (_root == null) Rebuild();
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeRoutine(true));
        }

        public void Hide()
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeRoutine(false));
        }

        IEnumerator FadeRoutine(bool fadeIn)
        {
            if (_cg == null) yield break;
            if (fadeIn) { _cg.interactable = _cg.blocksRaycasts = true; }
            float start = _cg.alpha, end = fadeIn ? 1f : 0f, t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(t + Time.deltaTime / 0.25f, 1f);
                _cg.alpha = Mathf.Lerp(start, end, t);
                yield return null;
            }
            if (!fadeIn) { _cg.interactable = _cg.blocksRaycasts = false; }
        }

        // ── Build ─────────────────────────────────────────────────────────────

        void Build(Transform root)
        {
            float pillH = HandleSz + padV * 2f;

            // ── Pill ─────────────────────────────────────────────────────────
            var pillGO = MakeGO("Pill", root);
            var pRT    = pillGO.GetComponent<RectTransform>();
            pRT.anchorMin = pRT.anchorMax = pRT.pivot = new Vector2(0.5f, 0.5f);
            pRT.sizeDelta = new Vector2(pillWidth, pillH);
            pRT.anchoredPosition = Vector2.zero;

            var pillImg = pillGO.AddComponent<UIImage>();
            pillImg.color = PillBg; pillImg.raycastTarget = false;
            Round(pillImg, PillR);

            // Border
            var borderGO = MakeGO("PillBorder", pillGO.transform);
            Stretch(borderGO);
            var borderImg = borderGO.AddComponent<UIImage>();
            borderImg.color = PillBorder; borderImg.raycastTarget = false;
            Round(borderImg, PillR);

            // ── "Before" label ───────────────────────────────────────────────
            var beforeGO = MakeGO("Before", pillGO.transform);
            var bRT      = beforeGO.GetComponent<RectTransform>();
            bRT.anchorMin = new Vector2(0f, 0f);
            bRT.anchorMax = new Vector2(0f, 1f);
            bRT.pivot     = new Vector2(0f, 0.5f);
            bRT.offsetMin = new Vector2(padH, 0f);
            bRT.offsetMax = new Vector2(padH + 65f, 0f);
            var beforeTmp = beforeGO.AddComponent<TextMeshProUGUI>();
            beforeTmp.text = "Before"; beforeTmp.fontSize = 18f;
            beforeTmp.color = LabelColor; beforeTmp.fontStyle = FontStyles.Bold;
            beforeTmp.alignment = TextAlignmentOptions.MidlineLeft;
            beforeTmp.raycastTarget = false;

            // ── "After" label ────────────────────────────────────────────────
            var afterGO = MakeGO("After", pillGO.transform);
            var aRT     = afterGO.GetComponent<RectTransform>();
            aRT.anchorMin = new Vector2(1f, 0f);
            aRT.anchorMax = new Vector2(1f, 1f);
            aRT.pivot     = new Vector2(1f, 0.5f);
            aRT.offsetMin = new Vector2(-padH - labelWidth, 0f);
            aRT.offsetMax = new Vector2(-padH, 0f);
            var afterTmp = afterGO.AddComponent<TextMeshProUGUI>();
            afterTmp.text = "After"; afterTmp.fontSize = 18f;
            afterTmp.color = LabelColor; afterTmp.fontStyle = FontStyles.Bold;
            afterTmp.alignment = TextAlignmentOptions.MidlineRight;
            afterTmp.raycastTarget = false;

            // ── Slider ───────────────────────────────────────────────────────
            float sliderOffX = padH + labelWidth + gap;
            var sliderGO = MakeGO("Slider", pillGO.transform);
            var sRT      = sliderGO.GetComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0f, 0f);
            sRT.anchorMax = new Vector2(1f, 1f);
            sRT.offsetMin = new Vector2(sliderOffX, 0f);
            sRT.offsetMax = new Vector2(-sliderOffX, 0f);

            // Transparent raycast catcher so Slider receives pointer events
            var sliderBg = sliderGO.AddComponent<UIImage>();
            sliderBg.color = new Color(0f, 0f, 0f, 0f);
            sliderBg.raycastTarget = true;

            // Track background
            var trackBgGO = MakeGO("TrackBackground", sliderGO.transform);
            var tbRT      = trackBgGO.GetComponent<RectTransform>();
            tbRT.anchorMin = new Vector2(0f, 0.5f);
            tbRT.anchorMax = new Vector2(1f, 0.5f);
            tbRT.pivot     = new Vector2(0.5f, 0.5f);
            tbRT.offsetMin = new Vector2(0f, -TrackH * 0.5f);
            tbRT.offsetMax = new Vector2(0f,  TrackH * 0.5f);
            var tbImg = trackBgGO.AddComponent<UIImage>();
            tbImg.color = TrackBgCol; tbImg.raycastTarget = false;
            Round(tbImg, TrackH * 0.5f);

            // Fill Area — matches the TrackBackground bounds exactly (no half-handle inset), so the
            // fill spans the full track. The handle is hidden, so no inset is needed.
            var fillAreaGO = MakeGO("FillArea", sliderGO.transform);
            var faRT       = fillAreaGO.GetComponent<RectTransform>();
            faRT.anchorMin = new Vector2(0f, 0.5f);
            faRT.anchorMax = new Vector2(1f, 0.5f);
            faRT.pivot     = new Vector2(0.5f, 0.5f);
            faRT.offsetMin = new Vector2(0f, -TrackH * 0.5f);
            faRT.offsetMax = new Vector2(0f,  TrackH * 0.5f);

            var fillGO = MakeGO("Fill", fillAreaGO.transform);
            var fillRT = fillGO.GetComponent<RectTransform>();
            fillRT.anchorMin = new Vector2(0f, 0f);
            fillRT.anchorMax = new Vector2(1f, 1f);
            fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;
            var fillImg = fillGO.AddComponent<UIImage>();
            fillImg.color = TrackFill; fillImg.raycastTarget = false;
            Round(fillImg, TrackH * 0.5f);

            // Handle Slide Area (same bounds as Fill Area)
            var hsaGO = MakeGO("HandleSlideArea", sliderGO.transform);
            var hsaRT = hsaGO.GetComponent<RectTransform>();
            hsaRT.anchorMin = new Vector2(0f, 0.5f);
            hsaRT.anchorMax = new Vector2(1f, 0.5f);
            hsaRT.pivot     = new Vector2(0.5f, 0.5f);
            hsaRT.offsetMin = new Vector2(HandleSz * 0.5f, -HandleSz * 0.5f);
            hsaRT.offsetMax = new Vector2(-HandleSz * 0.5f, HandleSz * 0.5f);

            var handleGO = MakeGO("Handle", hsaGO.transform);
            var handleRT = handleGO.GetComponent<RectTransform>();
            handleRT.anchorMin = handleRT.anchorMax = new Vector2(0f, 0.5f);
            handleRT.pivot     = new Vector2(0.5f, 0.5f);
            handleRT.sizeDelta = new Vector2(HandleSz, HandleSz);
            handleRT.anchoredPosition = Vector2.zero;
            var handleImg = handleGO.AddComponent<UIImage>();
            handleImg.color = Color.white; handleImg.raycastTarget = false;
            Round(handleImg, HandleSz * 0.5f);

            // Center dot
            var dotGO = MakeGO("CenterDot", handleGO.transform);
            var dotRT  = dotGO.GetComponent<RectTransform>();
            dotRT.anchorMin = dotRT.anchorMax = dotRT.pivot = new Vector2(0.5f, 0.5f);
            dotRT.sizeDelta = new Vector2(DotSz, DotSz);
            dotRT.anchoredPosition = Vector2.zero;
            var dotImg = dotGO.AddComponent<UIImage>();
            dotImg.color = TrackFill; dotImg.raycastTarget = false;
            Round(dotImg, DotSz * 0.5f);

            // ── Wire up Slider component ─────────────────────────────────────
            _slider = sliderGO.AddComponent<Slider>();
            _slider.direction  = Slider.Direction.LeftToRight;
            _slider.minValue   = 0f;
            _slider.maxValue   = 1f;
            _slider.fillRect   = fillRT;
            _slider.handleRect = handleRT;
            _slider.value      = initialValue;

            var sliderCb = ColorBlock.defaultColorBlock;
            sliderCb.normalColor = sliderCb.highlightedColor =
                sliderCb.pressedColor = sliderCb.selectedColor = Color.white;
            _slider.colors = sliderCb;
            _slider.targetGraphic = sliderBg;

            _slider.onValueChanged.AddListener(v => OnValueChanged?.Invoke(v));
        }

        // ── Canvas setup ──────────────────────────────────────────────────────

        void Grab()
        {
            _canvas = GetComponent<Canvas>();
            _scaler = GetComponent<CanvasScaler>();
            _cg     = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        }

        void SetupCanvas()
        {
            if (_canvas == null) return;
            _canvas.renderMode  = RenderMode.WorldSpace;
            _canvas.worldCamera = eventCamera ? eventCamera : Camera.main;
            transform.localScale = Vector3.one * worldScale;
            float pillH = HandleSz + padV * 2f;
            var rt = GetComponent<RectTransform>();
            if (rt) { rt.sizeDelta = new Vector2(pillWidth + 40f, pillH + 40f); rt.pivot = Vector2.one * 0.5f; }
            if (_scaler) { _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; _scaler.referencePixelsPerUnit = 100f; }
        }

        void Clear()
        {
            ClearTextures();
            _slider = null;
            if (_root != null) { Destroy2(_root); _root = null; }
            var found = transform.Find("Generated_BeforeAfterSlider");
            if (found) Destroy2(found.gameObject);
        }

        void ClearTextures()
        {
            foreach (var t in _textures)
                if (t != null) { if (Application.isPlaying) Destroy(t); else DestroyImmediate(t); }
            _textures.Clear();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        GameObject MakeGO(string n, Transform p)
        {
            var g = new GameObject(n, typeof(RectTransform));
            g.transform.SetParent(p, false); g.layer = gameObject.layer; return g;
        }

        static void Stretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Round(UIImage img, float radius)
        {
            img.sprite = RoundedSprite(radius); img.type = Image.Type.Sliced;
        }

        static Sprite RoundedSprite(float radius)
        {
            const int size = 64;
            float r   = Mathf.Clamp(radius, 0f, size * 0.5f);
            var tex   = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear; tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = x + 0.5f, cy = y + 0.5f;
                float ex = Mathf.Max(r - cx, cx - (size - r), 0f);
                float ey = Mathf.Max(r - cy, cy - (size - r), 0f);
                float a  = Mathf.Clamp01(r - Mathf.Sqrt(ex * ex + ey * ey) + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(pixels); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f,
                100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        static void Destroy2(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
        }

        static Color Hex(uint rgb) => new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >>  8) & 0xFF) / 255f,
            ( rgb        & 0xFF) / 255f);
    }
}
