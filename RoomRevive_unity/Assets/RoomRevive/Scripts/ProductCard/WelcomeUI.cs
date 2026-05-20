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
    public class WelcomeUI : MonoBehaviour
    {
        // ── Design tokens ─────────────────────────────────────────────────────
        static readonly Color Dark      = Hex(0x3A4055);
        static readonly Color BtnText   = Hex(0xE6E9F0);
        static readonly Color TagColor  = Hex(0x3A4055);
        static readonly Color PlaceholderBorder = new Color(0.227f, 0.251f, 0.333f, 0.30f);
        static readonly Color PlaceholderFill   = new Color(0.227f, 0.251f, 0.333f, 0.04f);
        static readonly Color PlaceholderLabel  = Hex(0x6B7388);

        // ── Static trigger ────────────────────────────────────────────────────
        public static System.Action OnScanRequested;

        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Canvas")]
        [SerializeField] public float  worldScale  = 0.001f;
        [SerializeField] public Camera eventCamera;

        [Header("Head Follow")]
        [SerializeField] public float distance    = 1.4f;
        [SerializeField] public float rightOffset = 0f;
        [SerializeField] public float upOffset    = -0.1f;

        [Header("Logo (optional — replaces placeholder)")]
        [SerializeField] public Sprite logoSprite;

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
        List<Texture2D> _textures = new List<Texture2D>();

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

        void Update()
        {
            if (!Application.isPlaying) return;
            if (KeyInput.GetKeyDown(KeyCode.Return) || KeyInput.GetKeyDown(KeyCode.KeypadEnter))
                OnScanRequested?.Invoke();
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
            _root = MakeGO("Generated_WelcomeUI", transform);
            Stretch(_root);
            Build(_root.transform);

            if (fontAsset != null)
                foreach (var t in _root.GetComponentsInChildren<TextMeshProUGUI>(true))
                    t.font = fontAsset;
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

        // Layout constants matching HTML
        const float CardW      = 460f;
        const float PadTop     = 30f;
        const float PadSide    = 40f;
        const float PadBot     = 40f;
        const float LogoW      = 200f;
        const float LogoH      = 120f;
        const float LogoRadius = 16f;
        const float GapLogoTag = 16f;
        const float TagH       = 24f;
        const float GapTagBtn  = 20f;
        const float BtnH       = 50f;
        const float BtnRadius  = 16f;
        const float CardRadius = 32f;

        void Build(Transform root)
        {
            float cardH = PadTop + LogoH + GapLogoTag + TagH + GapTagBtn + BtnH + PadBot;

            // ── Card background (radial gradient) ────────────────────────────
            var cardGO  = MakeGO("Card", root);
            var cRT     = cardGO.GetComponent<RectTransform>();
            cRT.anchorMin = cRT.anchorMax = cRT.pivot = new Vector2(0.5f, 0.5f);
            cRT.sizeDelta = new Vector2(CardW, cardH);
            cRT.anchoredPosition = Vector2.zero;
            var cardImg = cardGO.AddComponent<UIImage>();
            cardImg.color = Color.white; cardImg.raycastTarget = false;
            cardImg.sprite = RadialGradientSprite(Mathf.RoundToInt(CardW), Mathf.RoundToInt(cardH), CardRadius);
            cardImg.type   = Image.Type.Simple; cardImg.preserveAspect = false;

            float y = -PadTop; // y offset from card top (anchor = top-centre)

            // ── Logo area ────────────────────────────────────────────────────
            var logoGO = MakeGO("LogoArea", cardGO.transform);
            var lRT    = logoGO.GetComponent<RectTransform>();
            lRT.anchorMin = lRT.anchorMax = new Vector2(0.5f, 1f);
            lRT.pivot     = new Vector2(0.5f, 1f);
            lRT.sizeDelta = new Vector2(LogoW, LogoH);
            lRT.anchoredPosition = new Vector2(0f, y);

            if (logoSprite != null)
            {
                var li = logoGO.AddComponent<UIImage>();
                li.sprite = logoSprite; li.color = Color.white;
                li.type = Image.Type.Simple; li.preserveAspect = true;
                li.raycastTarget = false;
            }
            else
            {
                // Subtle placeholder fill
                var fill = logoGO.AddComponent<UIImage>();
                fill.color = PlaceholderFill; fill.raycastTarget = false;
                Round(fill, LogoRadius);

                // Border overlay (low opacity)
                var borderGO = MakeGO("LogoBorder", logoGO.transform);
                Stretch(borderGO);
                var border = borderGO.AddComponent<UIImage>();
                border.color = PlaceholderBorder; border.raycastTarget = false;
                Round(border, LogoRadius);

                // "LOGO" label
                var lbl = Tmp("LOGO", logoGO.transform, 11f, PlaceholderLabel);
                Stretch(lbl.gameObject);
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.fontStyle = FontStyles.Normal;
                lbl.raycastTarget = false;
            }

            y -= LogoH + GapLogoTag;

            // ── Tagline ──────────────────────────────────────────────────────
            float innerW = CardW - PadSide * 2f;
            var tagGO = MakeGO("Tagline", cardGO.transform);
            var tRT   = tagGO.GetComponent<RectTransform>();
            tRT.anchorMin = tRT.anchorMax = new Vector2(0.5f, 1f);
            tRT.pivot     = new Vector2(0.5f, 1f);
            tRT.sizeDelta = new Vector2(innerW, TagH);
            tRT.anchoredPosition = new Vector2(0f, y);
            var tagTmp = tagGO.AddComponent<TextMeshProUGUI>();
            tagTmp.text           = "Step into how you want to live";
            tagTmp.fontSize       = 14f;
            tagTmp.color          = TagColor;
            tagTmp.alignment      = TextAlignmentOptions.Center;
            tagTmp.enableWordWrapping = false;
            tagTmp.raycastTarget  = false;
            tagTmp.characterSpacing = 1.6f; // ~0.04em

            y -= TagH + GapTagBtn;

            // ── Button ───────────────────────────────────────────────────────
            float btnW = CardW - PadSide * 2f;
            var btnGO  = MakeGO("ScanButton", cardGO.transform);
            var bRT    = btnGO.GetComponent<RectTransform>();
            bRT.anchorMin = bRT.anchorMax = new Vector2(0.5f, 1f);
            bRT.pivot     = new Vector2(0.5f, 1f);
            bRT.sizeDelta = new Vector2(btnW, BtnH);
            bRT.anchoredPosition = new Vector2(0f, y);

            var btnImg = btnGO.AddComponent<UIImage>();
            btnImg.color = Dark; btnImg.raycastTarget = true;
            Round(btnImg, BtnRadius);

            var btnTmp = Tmp("Scan your room", btnGO.transform, 15f, BtnText);
            Stretch(btnTmp.gameObject);
            btnTmp.alignment  = TextAlignmentOptions.Center;
            btnTmp.fontStyle  = FontStyles.Normal;
            btnTmp.characterSpacing = 0.4f;
            btnTmp.raycastTarget = false;

            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.navigation    = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(() => OnScanRequested?.Invoke());

            // Hover overlay
            var hoverGO  = MakeGO("HoverOverlay", btnGO.transform);
            Stretch(hoverGO);
            var hoverImg = hoverGO.AddComponent<UIImage>();
            hoverImg.color = new Color(1f, 1f, 1f, 0f); hoverImg.raycastTarget = false;
            Round(hoverImg, BtnRadius);

            btn.targetGraphic = btnImg;
            var cb = ColorBlock.defaultColorBlock;
            cb.normalColor = cb.highlightedColor = cb.pressedColor = cb.selectedColor = Color.white;
            btn.colors = cb;

            var et = btnGO.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            UIImage ho = hoverImg;
            AddTrigger(et, UnityEngine.EventSystems.EventTriggerType.PointerEnter, _ => StartCoroutine(FadeOverlay(ho, 0.18f)));
            AddTrigger(et, UnityEngine.EventSystems.EventTriggerType.PointerExit,  _ => StartCoroutine(FadeOverlay(ho, 0f)));
            AddTrigger(et, UnityEngine.EventSystems.EventTriggerType.PointerDown,  _ => StartCoroutine(FadeOverlay(ho, 0.32f)));
            AddTrigger(et, UnityEngine.EventSystems.EventTriggerType.PointerUp,    _ => StartCoroutine(FadeOverlay(ho, 0.18f)));
        }

        static void AddTrigger(UnityEngine.EventSystems.EventTrigger et,
            UnityEngine.EventSystems.EventTriggerType type,
            UnityEngine.Events.UnityAction<UnityEngine.EventSystems.BaseEventData> action)
        {
            var entry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(action);
            et.triggers.Add(entry);
        }

        IEnumerator FadeOverlay(UIImage overlay, float targetAlpha)
        {
            if (overlay == null) yield break;
            float start = overlay.color.a, t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(t + Time.deltaTime / 0.12f, 1f);
                var c = overlay.color; c.a = Mathf.Lerp(start, targetAlpha, t);
                overlay.color = c; yield return null;
            }
        }

        // ── Radial gradient (matches ProductDetailsUI / VariantCarouselUI) ───

        Sprite RadialGradientSprite(int w, int h, float cornerRadiusPx)
        {
            int texH = 256;
            int texW = Mathf.Max(1, Mathf.RoundToInt(texH * (float)w / h));
            float rPx = cornerRadiusPx / h * texH;

            var c0 = new Color(0.831f, 0.851f, 0.898f); // #D4D9E5
            var c1 = new Color(0.710f, 0.737f, 0.816f); // #B5BCD0
            var c2 = new Color(0.612f, 0.643f, 0.737f); // #9CA4BC

            const float cx = 0.35f, cyCSS = 0.25f;
            float cy = 1f - cyCSS;
            float rx = Mathf.Max(cx, 1f - cx);
            float ry = Mathf.Max(cyCSS, 1f - cyCSS);

            var tex    = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear; tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[texW * texH];

            for (int y = 0; y < texH; y++)
            for (int x = 0; x < texW; x++)
            {
                float u = (x + 0.5f) / texW;
                float v = (y + 0.5f) / texH;
                float ddx = (u - cx) / rx;
                float ddy = (v - cy) / ry;
                float t   = Mathf.Clamp01(Mathf.Sqrt(ddx * ddx + ddy * ddy));

                Color col;
                if      (t <= 0.65f) col = Color.Lerp(c0, c1, t / 0.65f);
                else                 col = Color.Lerp(c1, c2, (t - 0.65f) / 0.35f);

                float ex = Mathf.Max(rPx - (x + 0.5f), (x + 0.5f) - (texW - rPx), 0f);
                float ey = Mathf.Max(rPx - (y + 0.5f), (y + 0.5f) - (texH - rPx), 0f);
                float a  = Mathf.Clamp01(rPx - Mathf.Sqrt(ex * ex + ey * ey) + 0.5f);

                pixels[y * texW + x] = new Color32(
                    (byte)(col.r * 255 + 0.5f), (byte)(col.g * 255 + 0.5f),
                    (byte)(col.b * 255 + 0.5f), (byte)(a    * 255 + 0.5f));
            }
            tex.SetPixels32(pixels); tex.Apply();
            _textures.Add(tex);
            return Sprite.Create(tex, new Rect(0, 0, texW, texH), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
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
            float cardH = PadTop + LogoH + GapLogoTag + TagH + GapTagBtn + BtnH + PadBot;
            var rt = GetComponent<RectTransform>();
            if (rt) { rt.sizeDelta = new Vector2(CardW + 40f, cardH + 40f); rt.pivot = Vector2.one * 0.5f; }
            if (_scaler) { _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; _scaler.referencePixelsPerUnit = 100f; }
        }

        void Clear()
        {
            ClearTextures();
            if (_root != null) { Destroy2(_root); _root = null; }
            var found = transform.Find("Generated_WelcomeUI");
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

        static TMP_Text Tmp(string label, Transform parent, float size, Color color)
        {
            var go  = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = size; tmp.color = color;
            tmp.enableAutoSizing = false; return tmp;
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
