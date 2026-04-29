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
    /// <summary>
    /// Intent selector panel — 3 cards (Calm & Unwind, Host & Gather, Fast & Focused).
    /// Design reference: roomrevive_intent_selector_v8_balanced.html
    /// Add to an empty GameObject in UIScene; right-click → "Rebuild UI".
    /// Subscribe to IntentCardSelectorUI.OnIntentSelected to react to picks.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public class IntentCardSelectorUI : MonoBehaviour
    {
        // ── Design tokens ────────────────────────────────────────────────────
        static readonly Color PanelBgA    = Hex(0x6B7080);
        static readonly Color PanelBgB    = Hex(0x8B8E9A);
        static readonly Color PanelBgC    = Hex(0x5C6270);
        static readonly Color HeaderBg    = new Color(0x73/255f, 0x7E/255f, 0x9C/255f, 195/255f);
        static readonly Color HeaderBorder= new Color(1f, 1f, 1f, 0.25f);
        static readonly Color TitleColor  = Hex(0x2D3245);
        static readonly Color SubColor    = Hex(0x4A5068);
        static readonly Color LabelBg     = new Color(0x73/255f, 0x7E/255f, 0x9C/255f, 0.55f);
        static readonly Color CardText    = Color.white;
        static readonly Color CardTextDim = new Color(1f, 1f, 1f, 0.85f);
        static readonly Color IconBorder  = Hex(0x737E9C);

        // Per-intent image gradient colours (160 deg, stops at 0 / 60% / 100%)
        static readonly Color[] GradCalm  = { Hex(0xE8DFCE), Hex(0xD4C8AC), Hex(0xB8A582) };
        static readonly Color[] GradHost  = { Hex(0x5A4030), Hex(0x3D2A1C), Hex(0x2A1C12) };
        static readonly Color[] GradFast  = { Hex(0xC8CDD5), Hex(0xA8AEB8), Hex(0x88909C) };
        static readonly float[] GradStops = { 0f, 0.6f, 1f };

        // ── Static trigger ───────────────────────────────────────────────────
        /// <summary>0 = Calm & Unwind, 1 = Host & Gather, 2 = Fast & Focused</summary>
        public static System.Action<int> OnIntentSelected;

        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Canvas")]
        [SerializeField] public float  worldScale  = 0.001f;
        [SerializeField] public Camera eventCamera;

        [Header("Head Follow")]
        [SerializeField] public float distance    = 1.4f;
        [SerializeField] public float rightOffset = 0f;
        [SerializeField] public float upOffset    = -0.1f;

        [Header("Layout (px)")]
        [SerializeField] public float cardWidth   = 260f;
        [SerializeField] public float cardGap     = 24f;
        [SerializeField] public float panelPadH   = 56f;
        [SerializeField] public float panelPadSide = 48f;
        [SerializeField] public float imageAspect = 1.1f;
        [SerializeField] public float labelAreaH  = 86f;
        [SerializeField] public float cardRadius  = 8f;

        [Header("Auto Rebuild")]
        [SerializeField] public bool autoRebuildInEditor = true;

        [Header("Font")]
        [SerializeField] public TMP_FontAsset fontAsset;

        [Header("Card Images (optional — overrides gradient)")]
        [SerializeField] public Sprite imageCalm;
        [SerializeField] public Sprite imageHost;
        [SerializeField] public Sprite imageFast;

        [Header("Card Icons (optional — replaces text placeholder)")]
        [SerializeField] public Sprite iconCalm;
        [SerializeField] public Sprite iconHost;
        [SerializeField] public Sprite iconFast;

        // ── Runtime ──────────────────────────────────────────────────────────
        Canvas       _canvas;
        CanvasScaler _scaler;
        CanvasGroup  _cg;
        Transform    _cam;
        Coroutine    _fade;
        GameObject   _root;
        List<Texture2D> _textures = new List<Texture2D>();

#if UNITY_EDITOR
        bool _rebuildQueued;
#endif

        // ── Lifecycle ────────────────────────────────────────────────────────

        void Awake()   { Grab(); SetupCanvas(); }
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

        // ── Public API ───────────────────────────────────────────────────────

        [ContextMenu("Rebuild UI")]
        public void Rebuild()
        {
            Clear();
            Grab();
            SetupCanvas();
            _root = MakeGO("Generated_IntentSelector", transform);
            Stretch(_root);
            BuildPanel(_root.transform);

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

        IEnumerator FadeOverlay(UIImage overlay, float targetAlpha)
        {
            if (overlay == null) yield break;
            float start = overlay.color.a, t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(t + Time.deltaTime / 0.12f, 1f);
                var c = overlay.color;
                c.a = Mathf.Lerp(start, targetAlpha, t);
                overlay.color = c;
                yield return null;
            }
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

        // ── Build ────────────────────────────────────────────────────────────

        void BuildPanel(Transform root)
        {
            float imageH      = cardWidth / imageAspect;
            float cardH       = imageH + labelAreaH;
            float headerPillH = 22f + 36f + 10f + 22f + 22f; // padV + title + gap + subtitle + padV
            float panelW      = cardWidth * 3f + cardGap * 2f + panelPadSide * 2f;
            float panelH      = panelPadH + headerPillH + 40f + cardH + panelPadH;

            // ── Panel background ─────────────────────────────────────────────
            var panel = MakeGO("Panel", root);
            var pRT   = panel.GetComponent<RectTransform>();
            pRT.anchorMin = pRT.anchorMax = pRT.pivot = new Vector2(0.5f, 0.5f);
            pRT.sizeDelta = new Vector2(panelW, panelH);
            pRT.anchoredPosition = Vector2.zero;

            float y = -panelPadH;

            // ── Header pill ──────────────────────────────────────────────────
            float pillW = panelW - panelPadSide * 2f;
            var pillGO = MakeGO("HeaderPill", panel.transform);
            var pillRT = pillGO.GetComponent<RectTransform>();
            pillRT.anchorMin = pillRT.anchorMax = new Vector2(0.5f, 1f);
            pillRT.pivot     = new Vector2(0.5f, 1f);
            pillRT.sizeDelta = new Vector2(pillW, headerPillH);
            pillRT.anchoredPosition = new Vector2(0f, y);
            var pillImg = pillGO.AddComponent<UIImage>();
            pillImg.color = HeaderBg; pillImg.raycastTarget = false;
            Round(pillImg, 8f);
            // Border
            var pillBorder = MakeGO("PillBorder", pillGO.transform);
            Stretch(pillBorder);
            var pbImg = pillBorder.AddComponent<UIImage>();
            pbImg.color = HeaderBorder; pbImg.raycastTarget = false;
            Round(pbImg, 8f);
            pbImg.enabled = false;

            // Title
            float pillInnerW = pillW - 56f * 2f;
            var title = AbsText("Choose how you want to live", pillGO.transform,
                new Vector2(pillInnerW, 40f), new Vector2(0f, -22f), 30f, Color.white);
            title.fontStyle = FontStyles.Bold; title.alignment = TextAlignmentOptions.Center;
            title.enableWordWrapping = false;

            // Subtitle
            var sub = AbsText("Pick the feeling. We'll shape your kitchen around it.",
                pillGO.transform, new Vector2(pillInnerW, 24f), new Vector2(0f, -22f - 36f - 10f), 14f, Color.white);
            sub.alignment = TextAlignmentOptions.Center; sub.enableWordWrapping = true;

            y -= headerPillH + 40f;

            // ── 3 intent cards ───────────────────────────────────────────────
            string[] titles   = { "Calm & Unwind", "Host & Gather", "Fast & Focused" };
            string[] taglines = { "I want to relax and take my time", "I want to gather, host, and share", "I want it simple and efficient" };
            Sprite[] images   = { imageCalm, imageHost, imageFast };
            Sprite[] icons    = { iconCalm,  iconHost,  iconFast  };
            Color[][] grads   = { GradCalm, GradHost, GradFast };

            float totalCardsW = cardWidth * 3f + cardGap * 2f;
            for (int i = 0; i < 3; i++)
            {
                float x = -totalCardsW * 0.5f + i * (cardWidth + cardGap) + cardWidth * 0.5f;
                BuildCard(panel.transform, i, titles[i], taglines[i], images[i], icons[i], grads[i], new Vector2(x, y), cardH);
            }
        }

        void BuildCard(Transform parent, int intentIdx, string title, string tagline,
                       Sprite cardImage, Sprite iconSprite, Color[] gradColors, Vector2 pos, float cardH)
        {
            float imageH = cardWidth / imageAspect;

            // ── Card container (Mask for rounded clipping) ───────────────────
            var cardGO = MakeGO($"Card_{title}", parent);
            var cRT    = cardGO.GetComponent<RectTransform>();
            cRT.anchorMin = cRT.anchorMax = new Vector2(0.5f, 1f);
            cRT.pivot     = new Vector2(0.5f, 1f);
            cRT.sizeDelta = new Vector2(cardWidth, cardH);
            cRT.anchoredPosition = pos;

            // Rounded mask
            var maskImg = cardGO.AddComponent<UIImage>();
            maskImg.color = Color.white; maskImg.raycastTarget = true;
            Round(maskImg, cardRadius);
            var mask = cardGO.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // Button — targetGraphic set after hover overlay is created
            var btn = cardGO.AddComponent<Button>();
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            int captured = intentIdx;
            btn.onClick.AddListener(() => OnIntentSelected?.Invoke(captured));

            // ── Image area ───────────────────────────────────────────────────
            var imgGO  = MakeGO("ImageArea", cardGO.transform);
            var iRT    = imgGO.GetComponent<RectTransform>();
            iRT.anchorMin = iRT.anchorMax = new Vector2(0.5f, 1f);
            iRT.pivot     = new Vector2(0.5f, 1f);
            iRT.sizeDelta = new Vector2(cardWidth, imageH);
            iRT.anchoredPosition = Vector2.zero;
            var imgBg  = imgGO.AddComponent<UIImage>();
            imgBg.color = Color.white; imgBg.raycastTarget = false;
            imgBg.sprite = cardImage != null ? cardImage
                : LinearGradientSprite(Mathf.RoundToInt(cardWidth), Mathf.RoundToInt(imageH), 160f, gradColors, GradStops, 0f);
            imgBg.type           = Image.Type.Simple;
            imgBg.preserveAspect = false;

            // ── Label area ───────────────────────────────────────────────────
            var lblGO = MakeGO("LabelArea", cardGO.transform);
            var lRT   = lblGO.GetComponent<RectTransform>();
            lRT.anchorMin = lRT.anchorMax = new Vector2(0.5f, 1f);
            lRT.pivot     = new Vector2(0.5f, 1f);
            lRT.sizeDelta = new Vector2(cardWidth, labelAreaH);
            lRT.anchoredPosition = new Vector2(0f, -imageH);
            var lblBg = lblGO.AddComponent<UIImage>(); lblBg.color = LabelBg; lblBg.raycastTarget = false;

            float padH = 16f, innerW = cardWidth - padH * 2f;

            // Icon circle
            const float iconSize = 28f;
            var iconGO = MakeGO("IconCircle", lblGO.transform);
            var iconRT = iconGO.GetComponent<RectTransform>();
            iconRT.anchorMin = iconRT.anchorMax = new Vector2(0f, 1f);
            iconRT.pivot     = new Vector2(0f, 1f);
            iconRT.sizeDelta = new Vector2(iconSize, iconSize);
            iconRT.anchoredPosition = new Vector2(padH, -padH);
            var iconBg  = iconGO.AddComponent<UIImage>(); iconBg.color = IconBorder;
            Round(iconBg, iconSize * 0.5f); iconBg.raycastTarget = false;
            iconBg.enabled = false;
            var iconContent = MakeGO("IconContent", iconGO.transform);
            Stretch(iconContent);
            var icImg = iconContent.AddComponent<UIImage>();
            icImg.raycastTarget = false;
            if (iconSprite != null)
            {
                icImg.sprite = iconSprite;
                icImg.color  = Color.white;
                icImg.type   = Image.Type.Simple;
                icImg.preserveAspect = true;
            }
            else
            {
                icImg.color = new Color(0f, 0f, 0f, 0f);
            }

            // Title (right of icon)
            float titleX = 35f;
            float titleW = cardWidth - titleX - padH;
            var titleGO  = AbsText(title, lblGO.transform,
                new Vector2(titleW, iconSize), new Vector2(titleX, -padH), 17f, CardText);
            titleGO.fontStyle = FontStyles.Bold; titleGO.verticalAlignment = VerticalAlignmentOptions.Middle;

            // Tagline
            var tagGO = AbsText(tagline, lblGO.transform,
                new Vector2(innerW, 26f), new Vector2(padH + iconSize + 10f - 10f, -padH - iconSize - 6f),
                12f, CardTextDim);
            tagGO.enableWordWrapping = true;

            // ── Hover overlay (top-most child) ───────────────────────────────
            var hoverGO  = MakeGO("HoverOverlay", cardGO.transform);
            Stretch(hoverGO);
            var hoverImg = hoverGO.AddComponent<UIImage>();
            hoverImg.color = new Color(1f, 1f, 1f, 0f);
            hoverImg.raycastTarget = false;

            btn.targetGraphic = maskImg;
            var cb = ColorBlock.defaultColorBlock;
            cb.normalColor = cb.highlightedColor = cb.pressedColor = cb.selectedColor = Color.white;
            btn.colors = cb;

            // EventTrigger for reliable hover in XR
            var et = cardGO.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            UIImage ho = hoverImg;

            var enter = new UnityEngine.EventSystems.EventTrigger.Entry
                { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => StartCoroutine(FadeOverlay(ho, 0.18f)));
            et.triggers.Add(enter);

            var exit = new UnityEngine.EventSystems.EventTrigger.Entry
                { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => StartCoroutine(FadeOverlay(ho, 0f)));
            et.triggers.Add(exit);

            var press = new UnityEngine.EventSystems.EventTrigger.Entry
                { eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown };
            press.callback.AddListener(_ => StartCoroutine(FadeOverlay(ho, 0.32f)));
            et.triggers.Add(press);

            var release = new UnityEngine.EventSystems.EventTrigger.Entry
                { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
            release.callback.AddListener(_ => StartCoroutine(FadeOverlay(ho, 0.18f)));
            et.triggers.Add(release);
        }

        // ── Canvas setup ─────────────────────────────────────────────────────

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

            float imageH  = cardWidth / imageAspect;
            float cardH   = imageH + labelAreaH;
            float pillH   = 22f + 36f + 10f + 22f + 22f;
            float panelW  = cardWidth * 3f + cardGap * 2f + panelPadSide * 2f;
            float panelH  = panelPadH + pillH + 40f + cardH + panelPadH;

            var rt = GetComponent<RectTransform>();
            if (rt) { rt.sizeDelta = new Vector2(panelW + 40f, panelH + 40f); rt.pivot = Vector2.one * 0.5f; }
            if (_scaler) { _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; _scaler.referencePixelsPerUnit = 100f; }
        }

        void Clear()
        {
            ClearTextures();
            if (_root != null) { Destroy2(_root); _root = null; }
            var found = transform.Find("Generated_IntentSelector");
            if (found) Destroy2(found.gameObject);
        }

        void ClearTextures()
        {
            foreach (var t in _textures)
                if (t != null) { if (Application.isPlaying) Destroy(t); else DestroyImmediate(t); }
            _textures.Clear();
        }

        // ── Gradient texture ─────────────────────────────────────────────────

        Sprite LinearGradientSprite(int w, int h, float angleDeg, Color[] colors, float[] stops, float cornerRadius)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;
            var pixels = new Color32[w * h];

            // Gradient direction in Unity UV space (y-up). CSS 0° = up, 90° = right.
            float rad = angleDeg * Mathf.Deg2Rad;
            float dx  =  Mathf.Sin(rad);
            float dy  = -Mathf.Cos(rad); // flip y because Unity UV y is opposite CSS

            // Find min/max projection across corners to normalise t to [0,1]
            float[] c = {
                0*dx + 0*dy, 1*dx + 0*dy,
                0*dx + 1*dy, 1*dx + 1*dy
            };
            float minT = Mathf.Min(c[0], c[1], c[2], c[3]);
            float maxT = Mathf.Max(c[0], c[1], c[2], c[3]);
            float range = maxT - minT;

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                float v = (y + 0.5f) / h;
                float t = ((u * dx + v * dy) - minT) / range;

                // Colour stop interpolation
                Color col = colors[0];
                for (int i = 0; i < stops.Length - 1; i++)
                {
                    if (t >= stops[i] && t <= stops[i + 1])
                    {
                        col = Color.Lerp(colors[i], colors[i + 1],
                            (t - stops[i]) / (stops[i + 1] - stops[i]));
                        break;
                    }
                }
                if (t > stops[stops.Length - 1]) col = colors[colors.Length - 1];

                // Optional rounded corner alpha
                float a = 1f;
                if (cornerRadius > 0f)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float rPx = cornerRadius;
                    float ex  = Mathf.Max(rPx - px, px - (w - rPx), 0f);
                    float ey  = Mathf.Max(rPx - py, py - (h - rPx), 0f);
                    a = Mathf.Clamp01(rPx - Mathf.Sqrt(ex * ex + ey * ey) + 0.5f);
                }

                pixels[y * w + x] = new Color32(
                    (byte)(col.r * 255 + 0.5f),
                    (byte)(col.g * 255 + 0.5f),
                    (byte)(col.b * 255 + 0.5f),
                    (byte)(a    * 255 + 0.5f));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _textures.Add(tex);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        // ── UI helpers ────────────────────────────────────────────────────────

        TMP_Text AbsText(string label, Transform parent, Vector2 size, Vector2 pos, float fontSize, Color color)
        {
            var go  = new GameObject(label.Length > 24 ? label.Substring(0, 24) : label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = gameObject.layer;
            var rt  = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = fontSize; tmp.color = color;
            tmp.enableAutoSizing = false; tmp.raycastTarget = false;
            return tmp;
        }

        GameObject MakeGO(string n, Transform p)
        {
            var g = new GameObject(n, typeof(RectTransform));
            g.transform.SetParent(p, false);
            g.layer = gameObject.layer;
            return g;
        }

        static TMP_Text Tmp(string label, Transform parent, float size, Color color)
        {
            var go  = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = size; tmp.color = color;
            tmp.enableAutoSizing = false;
            return tmp;
        }

        static void Stretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Round(UIImage img, float radius)
        {
            img.sprite = RoundedSprite(radius);
            img.type   = Image.Type.Sliced;
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
