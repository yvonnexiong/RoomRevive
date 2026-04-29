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
    /// Paginated variant carousel — expanded product card.
    /// Design reference: roomrevive_expanded_v9_bigger_type.html
    /// Triggered via VariantCarouselUI.OnShowRequested += (product) => ...
    /// Add to an empty GameObject in UIScene; right-click → "Rebuild UI" in editor.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public class VariantCarouselUI : MonoBehaviour
    {
        // ── Design tokens (HTML reference) ──────────────────────────────────
        static readonly Color CardBg      = Hex(0xB5BCD0);
        static readonly Color Dark        = Hex(0x3A4055);
        static readonly Color Muted       = Hex(0x6B7388);
        static readonly Color CtaText     = Hex(0xE6E9F0);
        static readonly Color CloseCircle = new Color(0.227f, 0.251f, 0.333f, 0.10f);
        [Header("Colors")]
        [SerializeField] public Color imageBgColor = new Color(0.227f, 0.251f, 0.333f, 0.18f);
        static readonly Color DotOff      = new Color(0.227f, 0.251f, 0.333f, 0.25f);
        static readonly Color ArrowIcon   = Hex(0xE6E9F0);

        // ── Static trigger ──────────────────────────────────────────────────
        public static System.Action<ProductSO> OnShowRequested;

        // ── Inspector ───────────────────────────────────────────────────────
        [Header("Canvas")]
        [SerializeField] public float  worldScale = 0.001f;
        [SerializeField] public Camera eventCamera;

        [Header("Head Follow")]
        [SerializeField] public float distance    = 1.2f;
        [SerializeField] public float rightOffset = 0.1f;
        [SerializeField] public float upOffset    = 0.05f;

        [Header("Layout (px)")]
        [SerializeField] public float cardWidth   = 540f;
        [SerializeField] public float cardPad     = 28f;
        [SerializeField] public float arrowOffset      = 80f;
        [SerializeField] public float arrowSize        = 80f;
        [SerializeField] public float arrowScale       = 1.5f;
        [SerializeField] public float closeButtonSize  = 72f;
        [SerializeField] public int   defaultDots = 3;

        [Header("Corner Radii (px)")]
        [SerializeField] public float radiusCard  = 28f;
        [SerializeField] public float radiusImage = 18f;
        [SerializeField] public float radiusCTA   = 14f;
        [SerializeField] public float radiusClose = 36f;
        [SerializeField] public float radiusDot   = 4f;
        [SerializeField] public float radiusArrow = 24f;

        [Header("Text Block Heights (px)")]
        [SerializeField] public float brandHeight = 22f;
        [SerializeField] public float nameHeight  = 32f;
        [SerializeField] public float descHeight  = 26f;
        [SerializeField] public float textBlockTotalHeight = 110f;

        [Header("Auto Rebuild")]
        [SerializeField] public bool autoRebuildInEditor = true;

        [Header("Products")]
        [Tooltip("Fill this to browse multiple products with the arrows.")]
        [SerializeField] public ProductSO[] products;

        [Header("Test")]
        [SerializeField] public ProductSO testProduct;

        // ── Runtime ─────────────────────────────────────────────────────────
        Canvas       _canvas;
        CanvasScaler _scaler;
        CanvasGroup  _cg;
        Transform    _cam;
        ProductSO    _product;
        int          _idx;
        bool         _multiProductMode;
        Coroutine    _fade;
        GameObject   _root;

        UIImage        _imgArea;
        UIImage        _photoImage;
        TMP_Text       _imgPlaceholder;
        TMP_Text       _brand, _pname, _desc;
        List<UIImage>  _dots = new List<UIImage>();
        Texture2D      _gradientTex;

#if UNITY_EDITOR
        bool _rebuildQueued;
#endif

        // ── Lifecycle ────────────────────────────────────────────────────────

        void Awake()
        {
            Grab();
            SetupCanvas();
            if (Application.isPlaying) OnShowRequested += Show;
        }

        void OnDestroy()
        {
            if (Application.isPlaying) OnShowRequested -= Show;
        }

        void Start()
        {
            if (!Application.isPlaying) return;
            var eye = GameObject.Find("CenterEyeAnchor");
            _cam = eye ? eye.transform : Camera.main?.transform;
            Rebuild();
            SetVisible(false);
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            if (Input.GetKeyDown(KeyCode.LeftArrow))  NavigateLeft();
            if (Input.GetKeyDown(KeyCode.RightArrow)) NavigateRight();
            if (Input.GetKeyDown(KeyCode.T) && (testProduct != null || (products != null && products.Length > 0))) TestShow();
            if (Input.GetKeyDown(KeyCode.Escape)) Hide();
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

        [ContextMenu("Test Show")]
        public void TestShow()
        {
            _multiProductMode = products != null && products.Length > 1;
            if (_multiProductMode)
            {
                _idx = 0; _product = products[0];
            }
            else
            {
                if (testProduct == null) { Debug.LogWarning("[VariantCarouselUI] Assign a testProduct or fill the Products array first."); return; }
                _product = testProduct; _idx = 0;
            }
            if (_root == null) Rebuild();
            RefreshDots(); Populate(); SetVisible(true);
        }

        [ContextMenu("Rebuild UI")]
        public void Rebuild()
        {
            Clear();
            Grab();
            SetupCanvas();
            _root = MakeGO("Generated_VariantCarousel", transform);
            Stretch(_root);
            _dots.Clear();
            BuildCard(_root.transform);
            BuildArrow(_root.transform, left: true);
            BuildArrow(_root.transform, left: false);
            if (Application.isPlaying && _product != null) Populate();
        }

        public void NavigateLeft()
        {
            int n = Count();
            if (n < 2) return;
            _idx = (_idx - 1 + n) % n;
            if (_multiProductMode) _product = products[_idx];
            Populate();
        }

        public void NavigateRight()
        {
            int n = Count();
            if (n < 2) return;
            _idx = (_idx + 1) % n;
            if (_multiProductMode) _product = products[_idx];
            Populate();
        }

        // ── Show / Hide ──────────────────────────────────────────────────────

        void Show(ProductSO p)
        {
            _multiProductMode = products != null && products.Length > 1;
            _product = _multiProductMode ? products[0] : p;
            _idx     = 0;
            if (_root == null) Rebuild();
            RefreshDots();
            Populate();
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeRoutine(fadeIn: true));
        }

        void Hide()
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeRoutine(fadeIn: false));
        }

        void SetVisible(bool v)
        {
            if (_cg == null) return;
            _cg.alpha = v ? 1f : 0f;
            _cg.interactable = _cg.blocksRaycasts = v;
        }

        IEnumerator FadeRoutine(bool fadeIn)
        {
            if (_cg == null) yield break;
            if (fadeIn) { _cg.interactable = _cg.blocksRaycasts = true; }
            float start = _cg.alpha, end = fadeIn ? 1f : 0f, t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(t + Time.deltaTime / 0.2f, 1f);
                _cg.alpha = Mathf.Lerp(start, end, t);
                yield return null;
            }
            if (!fadeIn) SetVisible(false);
        }

        // ── Content ──────────────────────────────────────────────────────────

        void Populate()
        {
            if (_product == null) return;
            bool hasVar = _product.variants != null && _product.variants.Length > 0;
            var v = hasVar && _idx < _product.variants.Length ? _product.variants[_idx] : null;

            if (_brand) _brand.text = _product.brandName?.ToUpper() ?? "";
            if (_pname) _pname.text = v != null ? v.name  : _product.productName;
            if (_desc)  _desc.text  = v != null ? v.description : _product.shortDescription;

            if (_imgPlaceholder != null)
                _imgPlaceholder.gameObject.SetActive(!Application.isPlaying);

            if (_photoImage != null)
            {
                var spr = v?.image ?? _product.thumbnail;
                _photoImage.sprite = spr;
                _photoImage.gameObject.SetActive(spr != null);
            }

            for (int i = 0; i < _dots.Count; i++)
                _dots[i].color = i == _idx ? Dark : DotOff;
        }

        void RefreshDots()
        {
            if (_dots == null) return;
            int actual = Count();
            for (int i = 0; i < _dots.Count; i++)
                if (_dots[i] != null)
                    _dots[i].gameObject.SetActive(i < actual);
        }

        // ── Build ────────────────────────────────────────────────────────────

        void BuildCard(Transform parent)
        {
            float inner = cardWidth - cardPad * 2f;
            float imgH  = inner / 1.15f;
            float cardH = cardPad + closeButtonSize + 14f + imgH + 22f + textBlockTotalHeight + 24f + 60f + 22f + 38f + cardPad;

            // Card background
            var card = MakeGO("Card", parent);
            var cRT  = card.GetComponent<RectTransform>();
            cRT.anchorMin = cRT.anchorMax = cRT.pivot = new Vector2(0.5f, 0.5f);
            cRT.sizeDelta = new Vector2(cardWidth, cardH);
            cRT.anchoredPosition = Vector2.zero;
            var cardImg = card.AddComponent<UIImage>();
            cardImg.color          = Color.white;
            cardImg.raycastTarget  = false;
            cardImg.sprite         = GradientRoundedSprite(cardWidth, cardH);
            cardImg.type           = Image.Type.Simple;
            cardImg.preserveAspect = false;

            float y = -cardPad;

            // Close button (top-right, absolute)
            var closeGO = MakeGO("CloseButton", card.transform);
            var clRT    = closeGO.GetComponent<RectTransform>();
            clRT.anchorMin = clRT.anchorMax = new Vector2(1f, 1f);
            clRT.pivot     = new Vector2(1f, 1f);
            clRT.sizeDelta = new Vector2(closeButtonSize, closeButtonSize);
            clRT.anchoredPosition = new Vector2(-cardPad, -cardPad);
            var clBg  = closeGO.AddComponent<UIImage>();  clBg.color = CloseCircle; Round(clBg, closeButtonSize * 0.5f);
            var clBtn = closeGO.AddComponent<Button>();   clBtn.targetGraphic = clBg;
            clBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            clBtn.onClick.AddListener(Hide);
            var clLbl = Tmp("×", closeGO.transform, closeButtonSize * 0.45f, Dark);
            Stretch(clLbl.gameObject); clLbl.alignment = TextAlignmentOptions.Center; clLbl.raycastTarget = false;

            y -= closeButtonSize + 14f;

            // Image area
            var imgGO = MakeGO("ImageArea", card.transform);
            var iRT   = imgGO.GetComponent<RectTransform>();
            iRT.anchorMin = iRT.anchorMax = new Vector2(0.5f, 1f);
            iRT.pivot     = new Vector2(0.5f, 1f);
            iRT.sizeDelta = new Vector2(inner, imgH);
            iRT.anchoredPosition = new Vector2(0f, y);
            _imgArea = imgGO.AddComponent<UIImage>();
            _imgArea.color = imageBgColor; _imgArea.raycastTarget = false;
            Round(_imgArea, radiusImage);
            var mask = imgGO.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            // Fill image — clipped by mask to stay within rounded corners
            var photoGO = MakeGO("ProductPhoto", imgGO.transform);
            Stretch(photoGO);
            _photoImage = photoGO.AddComponent<UIImage>();
            _photoImage.type            = Image.Type.Simple;
            _photoImage.preserveAspect  = false;
            _photoImage.color           = Color.white;
            _photoImage.raycastTarget   = false;
            _photoImage.gameObject.SetActive(false);

            // Placeholder text — visible in editor, hidden at runtime
            var ph = Tmp("Product image", imgGO.transform, 14f, Muted);
            Stretch(ph.gameObject);
            ph.alignment = TextAlignmentOptions.Center;
            ph.characterSpacing = 16f; ph.fontStyle = FontStyles.UpperCase; ph.raycastTarget = false;
            _imgPlaceholder = ph;

            y -= imgH + 22f;

            // Text block
            var tbGO = MakeGO("TextBlock", card.transform);
            var tbRT = tbGO.GetComponent<RectTransform>();
            tbRT.anchorMin = tbRT.anchorMax = new Vector2(0.5f, 1f);
            tbRT.pivot     = new Vector2(0.5f, 1f);
            tbRT.sizeDelta = new Vector2(inner, textBlockTotalHeight);
            tbRT.anchoredPosition = new Vector2(0f, y);
            var vlg = tbGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f; vlg.padding = new RectOffset(4, 4, 0, 0);
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;

            _brand = Tmp("NOBILIA", tbGO.transform, 14f, Muted);
            _brand.characterSpacing = 20f; _brand.fontStyle = FontStyles.Bold; _brand.raycastTarget = false;
            MinH(_brand.gameObject, brandHeight);

            _pname = Tmp("Riva 893, two-tone", tbGO.transform, 24f, Dark);
            _pname.enableWordWrapping = true; _pname.raycastTarget = false;
            MinH(_pname.gameObject, nameHeight);

            _desc = Tmp("Cream + slate, oak butcher-block top", tbGO.transform, 17f, Muted);
            _desc.enableWordWrapping = true; _desc.raycastTarget = false;
            MinH(_desc.gameObject, descHeight);

            y -= 110f + 24f;

            // CTA button
            var ctaGO = MakeGO("CTAButton", card.transform);
            var ctaRT = ctaGO.GetComponent<RectTransform>();
            ctaRT.anchorMin = ctaRT.anchorMax = new Vector2(0.5f, 1f);
            ctaRT.pivot     = new Vector2(0.5f, 1f);
            ctaRT.sizeDelta = new Vector2(inner, 60f);
            ctaRT.anchoredPosition = new Vector2(0f, y);
            var ctaBg  = ctaGO.AddComponent<UIImage>(); ctaBg.color = Dark; Round(ctaBg, radiusCTA);
            var ctaBtn = ctaGO.AddComponent<Button>();  ctaBtn.targetGraphic = ctaBg;
            ctaBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            var ctaLbl = Tmp("See details", ctaGO.transform, 18f, CtaText);
            Stretch(ctaLbl.gameObject);
            ctaLbl.alignment = TextAlignmentOptions.Center;
            ctaLbl.characterSpacing = 2f; ctaLbl.fontStyle = FontStyles.Normal; ctaLbl.raycastTarget = false;

            y -= 60f + 22f;

            // Dots row
            var dotsGO = MakeGO("Dots", card.transform);
            var dRT    = dotsGO.GetComponent<RectTransform>();
            dRT.anchorMin = dRT.anchorMax = new Vector2(0.5f, 1f);
            dRT.pivot     = new Vector2(0.5f, 1f);
            dRT.sizeDelta = new Vector2(inner, 38f);
            dRT.anchoredPosition = new Vector2(0f, y);
            var hlg = dotsGO.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = 8f;
            hlg.childControlWidth = hlg.childControlHeight = false;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;

            int dotCount = Mathf.Max(defaultDots, Count());
            for (int i = 0; i < dotCount; i++)
            {
                var d  = MakeGO($"Dot_{i}", dotsGO.transform);
                d.GetComponent<RectTransform>().sizeDelta = new Vector2(8f, 8f);
                var di = d.AddComponent<UIImage>();
                di.color = i == 0 ? Dark : DotOff;
                di.raycastTarget = false;
                Round(di, radiusDot);
                _dots.Add(di);
            }
        }

        void BuildArrow(Transform parent, bool left)
        {
            float x = left
                ? -(cardWidth * 0.5f + arrowOffset)
                :  (cardWidth * 0.5f + arrowOffset);

            var go = MakeGO(left ? "ArrowLeft" : "ArrowRight", parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(arrowSize, arrowSize);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.localScale = Vector3.one * arrowScale;

            var bg  = go.AddComponent<UIImage>(); bg.color = new Color(Dark.r, Dark.g, Dark.b, 0.90f); Round(bg, arrowSize * 0.5f);
            var btn = go.AddComponent<Button>();  btn.targetGraphic = bg;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(left
                ? (UnityEngine.Events.UnityAction)NavigateLeft
                : NavigateRight);

            var lbl = Tmp(left ? "‹" : "›", go.transform, arrowSize * 0.45f, ArrowIcon);
            Stretch(lbl.gameObject);
            lbl.alignment = TextAlignmentOptions.Center; lbl.raycastTarget = false;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

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

            float inner  = cardWidth - cardPad * 2f;
            float imgH   = inner / 1.15f;
            float cardH  = cardPad + closeButtonSize + 14f + imgH + 22f + textBlockTotalHeight + 24f + 60f + 22f + 38f + cardPad;
            float totalW = cardWidth + (arrowOffset + arrowSize) * 2f;

            var rt = GetComponent<RectTransform>();
            if (rt) { rt.sizeDelta = new Vector2(totalW + 40f, cardH + 40f); rt.pivot = Vector2.one * 0.5f; }

            if (_scaler)
            {
                _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                _scaler.referencePixelsPerUnit = 100f;
            }
        }

        int Count()
        {
            if (_multiProductMode && products != null) return products.Length;
            if (_product?.variants != null && _product.variants.Length > 0) return _product.variants.Length;
            return 1;
        }

        void Clear()
        {
            if (_gradientTex != null)
            {
                if (Application.isPlaying) Destroy(_gradientTex);
                else DestroyImmediate(_gradientTex);
                _gradientTex = null;
            }
            if (_root != null) { Destroy2(_root); _root = null; }
            var found = transform.Find("Generated_VariantCarousel");
            if (found != null) Destroy2(found.gameObject);
            _dots.Clear();
            _imgArea = null; _photoImage = null; _imgPlaceholder = null;
            _brand = _pname = _desc = null;
        }

        // ── UI factory ───────────────────────────────────────────────────────

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

        static void MinH(GameObject go, float h)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = h;
        }

        static void Round(UIImage img, float radius)
        {
            img.sprite = RoundedSprite(radius);
            img.type   = Image.Type.Sliced;
        }

        static Sprite RoundedSprite(float radius)
        {
            const int size = 64;
            float r = Mathf.Clamp(radius, 0f, size * 0.5f);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = x + 0.5f, cy = y + 0.5f;
                    float dx = Mathf.Max(r - cx, cx - (size - r), 0f);
                    float dy = Mathf.Max(r - cy, cy - (size - r), 0f);
                    float a  = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f,
                100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        // Bakes CSS radial-gradient(ellipse at 35% 25%, #D4D9E5 0%, #B5BCD0 65%, #9CA4BC 100%)
        // plus rounded corner alpha into a single texture matching the card's aspect ratio.
        Sprite GradientRoundedSprite(float cardW, float cardH)
        {
            int texH = 256;
            int texW = Mathf.Max(1, Mathf.RoundToInt(texH * cardW / cardH));
            float rPx = radiusCard / cardH * texH;

            var c0 = new Color(0.831f, 0.851f, 0.898f); // #D4D9E5  0%
            var c1 = new Color(0.710f, 0.737f, 0.816f); // #B5BCD0 65%
            var c2 = new Color(0.612f, 0.643f, 0.737f); // #9CA4BC 100%

            // CSS center: 35% left, 25% top → Unity UV (y bottom-up): cy = 0.75
            const float cx = 0.35f, cyCSS = 0.25f;
            float cy = 1f - cyCSS;
            // farthest-corner ellipse radii
            float rx = Mathf.Max(cx, 1f - cx);       // 0.65
            float ry = Mathf.Max(cyCSS, 1f - cyCSS); // 0.75

            var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;
            var pixels = new Color32[texW * texH];

            for (int y = 0; y < texH; y++)
            for (int x = 0; x < texW; x++)
            {
                float u = (x + 0.5f) / texW;
                float v = (y + 0.5f) / texH;

                float du = (u - cx) / rx;
                float dv = (v - cy) / ry;
                float t  = Mathf.Sqrt(du * du + dv * dv);

                Color col = t <= 0.65f
                    ? Color.Lerp(c0, c1, t / 0.65f)
                    : Color.Lerp(c1, c2, Mathf.Clamp01((t - 0.65f) / 0.35f));

                float px = x + 0.5f, py = y + 0.5f;
                float ex = Mathf.Max(rPx - px, px - (texW - rPx), 0f);
                float ey = Mathf.Max(rPx - py, py - (texH - rPx), 0f);
                float a  = Mathf.Clamp01(rPx - Mathf.Sqrt(ex * ex + ey * ey) + 0.5f);

                pixels[y * texW + x] = new Color32(
                    (byte)(col.r * 255 + 0.5f),
                    (byte)(col.g * 255 + 0.5f),
                    (byte)(col.b * 255 + 0.5f),
                    (byte)(a    * 255 + 0.5f));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _gradientTex = tex;
            return Sprite.Create(tex, new Rect(0, 0, texW, texH), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        static void Destroy2(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }

        static Color Hex(uint rgb) => new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >>  8) & 0xFF) / 255f,
            ( rgb        & 0xFF) / 255f);
    }
}
