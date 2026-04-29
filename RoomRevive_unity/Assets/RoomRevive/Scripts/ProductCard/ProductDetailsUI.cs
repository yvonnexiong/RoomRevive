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
    /// Full product details panel.
    /// Design reference: roomrevive_product_details_v2.html
    /// Add to an empty GameObject in UIScene; right-click → "Rebuild UI".
    /// Triggered via ProductDetailsUI.OnShowRequested?.Invoke(product).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public class ProductDetailsUI : MonoBehaviour
    {
        // ── Design tokens ────────────────────────────────────────────────────
        static readonly Color Dark      = Hex(0x3A4055);
        static readonly Color Muted     = Hex(0x6B7388);
        static readonly Color CtaText   = Hex(0xE6E9F0);
        static readonly Color DimTileBg = new Color(1f, 1f, 1f, 0.35f);
        static readonly Color DivColor  = new Color(0.227f, 0.251f, 0.333f, 0.15f);
        static readonly Color SwatchRingWhite = new Color(1f, 1f, 1f, 0.85f);
        static readonly Color SwatchRingAmber = Hex(0xE8B894);
        static readonly Color CloseCircle = new Color(0.227f, 0.251f, 0.333f, 0.10f);

        // ── Static trigger ───────────────────────────────────────────────────
        public static System.Action<ProductSO> OnShowRequested;

        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Canvas")]
        [SerializeField] public float  worldScale  = 0.001f;
        [SerializeField] public Camera eventCamera;

        [Header("Head Follow")]
        [SerializeField] public float distance    = 1.2f;
        [SerializeField] public float rightOffset = 0.1f;
        [SerializeField] public float upOffset    = 0.05f;

        [Header("Layout (px)")]
        [SerializeField] public float cardWidth  = 540f;
        [SerializeField] public float padTop     = 32f;
        [SerializeField] public float padSide    = 28f;
        [SerializeField] public float padBottom  = 28f;
        [SerializeField] public float radiusCard = 28f;
        [SerializeField] public float sectionGap = 26f;

        [Header("Auto Rebuild")]
        [SerializeField] public bool autoRebuildInEditor = true;

        [Header("Test")]
        [SerializeField] public ProductSO testProduct;

        // ── Runtime ──────────────────────────────────────────────────────────
        Canvas       _canvas;
        CanvasScaler _scaler;
        CanvasGroup  _cg;
        Transform    _cam;
        ProductSO    _product;
        Coroutine    _fade;
        GameObject   _root;
        Texture2D    _gradientTex;

        // Content refs updated by Populate()
        TMP_Text            _brandText, _nameText, _descText;
        TMP_Text            _materialsText, _storageText, _priceText;
        TMP_Text[]          _dimValueTexts  = new TMP_Text[4];
        TMP_Text[]          _dimLabelTexts  = new TMP_Text[4];
        GameObject[]        _dimTiles       = new GameObject[4];
        List<TMP_Text>      _featureTexts   = new List<TMP_Text>();
        List<UIImage>       _swatchImages   = new List<UIImage>();
        List<UIImage>       _swatchAmbers   = new List<UIImage>();
        List<TMP_Text>      _swatchLabels   = new List<TMP_Text>();

#if UNITY_EDITOR
        bool _rebuildQueued;
#endif

        // ── Lifecycle ────────────────────────────────────────────────────────

        void Awake()
        {
            Grab(); SetupCanvas();
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
            if (Input.GetKeyDown(KeyCode.Y) && testProduct != null) TestShow();
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
            _product = testProduct;
            if (_root == null) Rebuild();
            else Populate();
            SetVisible(true);
        }

        [ContextMenu("Rebuild UI")]
        public void Rebuild()
        {
            Clear();
            Grab();
            SetupCanvas();
            _featureTexts.Clear();
            _swatchImages.Clear(); _swatchAmbers.Clear(); _swatchLabels.Clear();
            _root = MakeGO("Generated_ProductDetails", transform);
            Stretch(_root);
            BuildCard(_root.transform);
            if (Application.isPlaying && _product != null) Populate();
        }

        void Show(ProductSO p)
        {
            _product = p;
            if (_root == null) Rebuild();
            else Populate();
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeRoutine(true));
        }

        void Hide()
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeRoutine(false));
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

        // ── Populate ─────────────────────────────────────────────────────────

        void Populate()
        {
            if (_product == null) return;

            if (_brandText)  _brandText.text  = (_product.brandName  ?? "").ToUpper();
            if (_nameText)   _nameText.text   = _product.productName ?? "";
            if (_descText)   _descText.text   = _product.shortDescription ?? "";
            if (_materialsText) _materialsText.text = _product.materials ?? "";
            if (_storageText)   _storageText.text   = _product.storage   ?? "";
            if (_priceText)     _priceText.text      = string.IsNullOrEmpty(_product.fromPrice) ? "" : _product.fromPrice;

            // Dimensions
            var dims = _product.dimensions;
            for (int i = 0; i < _dimTiles.Length; i++)
            {
                bool hasDim = dims != null && i < dims.Length;
                if (_dimTiles[i] != null) _dimTiles[i].SetActive(hasDim);
                if (hasDim)
                {
                    if (_dimLabelTexts[i]) _dimLabelTexts[i].text = (dims[i].label ?? "").ToUpper();
                    if (_dimValueTexts[i]) _dimValueTexts[i].text = dims[i].value ?? "";
                }
            }

            // Features
            var feats = _product.features;
            for (int i = 0; i < _featureTexts.Count; i++)
            {
                bool hasFeat = feats != null && i < feats.Length;
                _featureTexts[i].gameObject.SetActive(hasFeat);
                if (hasFeat) _featureTexts[i].text = feats[i];
            }

            // Swatches
            var variants = _product.variants;
            for (int i = 0; i < _swatchImages.Count; i++)
            {
                bool hasVar = variants != null && i < variants.Length;
                _swatchImages[i].transform.parent.gameObject.SetActive(hasVar);
                if (!hasVar) continue;
                _swatchImages[i].color   = variants[i].swatchColor;
                _swatchLabels[i].text    = variants[i].name ?? "";
                _swatchAmbers[i].enabled = (i == 0); // first variant selected by default
            }
        }

        // ── Build ────────────────────────────────────────────────────────────

        void BuildCard(Transform parent)
        {
            float inner = cardWidth - padSide * 2f;

            // Pre-measure section heights
            const float headerContent    = 16f + 8f + 28f + 4f + 18f; // brand+gap+name+gap+desc
            const float headerFooter     = 22f + 1f + 24f;             // bot-pad + divider + margin
            const float dimsSectionH     = 16f + 12f + 68f;
            const float matsSectionH     = 16f + 10f + 44f;
            const float featSectionH     = 16f + 8f + 4f * 22f + 3f * 8f; // 4 items
            const float colorSectionH    = 16f + 14f + 78f;
            const float storSectionH     = 16f + 10f + 24f;
            const float priceSectionH    = 44f;
            const float ctaH             = 46f;
            const float closeSize        = 44f;

            float cardH = padTop + closeSize + 14f
                        + headerContent + headerFooter
                        + dimsSectionH  + sectionGap
                        + matsSectionH  + sectionGap
                        + featSectionH  + sectionGap
                        + colorSectionH + sectionGap
                        + storSectionH  + sectionGap
                        + priceSectionH + 16f
                        + ctaH
                        + padBottom;

            // ── Card background (gradient + rounded corners) ─────────────────
            var card = MakeGO("Card", parent);
            var cRT  = card.GetComponent<RectTransform>();
            cRT.anchorMin = cRT.anchorMax = cRT.pivot = new Vector2(0.5f, 0.5f);
            cRT.sizeDelta = new Vector2(cardWidth, cardH);
            cRT.anchoredPosition = Vector2.zero;
            var cardImg = card.AddComponent<UIImage>();
            cardImg.color = Color.white; cardImg.raycastTarget = false;
            cardImg.sprite = GradientRoundedSprite(cardWidth, cardH);
            cardImg.type = Image.Type.Simple; cardImg.preserveAspect = false;

            float y = -padTop;

            // ── Close button (top-right absolute) ────────────────────────────
            var closeGO = MakeGO("CloseButton", card.transform);
            var clRT    = closeGO.GetComponent<RectTransform>();
            clRT.anchorMin = clRT.anchorMax = new Vector2(1f, 1f);
            clRT.pivot     = new Vector2(1f, 1f);
            clRT.sizeDelta = new Vector2(closeSize, closeSize);
            clRT.anchoredPosition = new Vector2(-padSide, -padTop);
            var clBg  = closeGO.AddComponent<UIImage>(); clBg.color = CloseCircle; Round(clBg, closeSize * 0.5f);
            var clBtn = closeGO.AddComponent<Button>();  clBtn.targetGraphic = clBg;
            clBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            clBtn.onClick.AddListener(Hide);
            var clLbl = Tmp("×", closeGO.transform, closeSize * 0.45f, Dark);
            Stretch(clLbl.gameObject); clLbl.alignment = TextAlignmentOptions.Center; clLbl.raycastTarget = false;

            y -= closeSize + 14f;

            // ── Header ───────────────────────────────────────────────────────
            _brandText = AbsText("BRAND", card.transform, new Vector2(inner, 16f), new Vector2(0f, y), 11f, Muted);
            _brandText.characterSpacing = 20f; _brandText.fontStyle = FontStyles.Bold;

            y -= 16f + 8f;
            _nameText = AbsText("Product Name", card.transform, new Vector2(inner, 32f), new Vector2(0f, y), 22f, Dark);
            _nameText.fontStyle = FontStyles.Bold; _nameText.enableWordWrapping = true;

            y -= 28f + 4f;
            _descText = AbsText("Short description", card.transform, new Vector2(inner, 18f), new Vector2(0f, y), 13f, Muted);
            _descText.enableWordWrapping = true;

            y -= 18f + 22f;
            // Divider
            var divGO = MakeGO("Divider", card.transform);
            var dRT   = divGO.GetComponent<RectTransform>();
            dRT.anchorMin = new Vector2(0f, 1f); dRT.anchorMax = new Vector2(1f, 1f);
            dRT.pivot     = new Vector2(0.5f, 1f);
            dRT.sizeDelta = new Vector2(0f, 1f);
            dRT.anchoredPosition = new Vector2(0f, y);
            divGO.AddComponent<UIImage>().color = DivColor;

            y -= 1f + 24f;

            // ── Dimensions ───────────────────────────────────────────────────
            SectionLabel("DIMENSIONS", card.transform, ref y, inner);
            y -= 12f;
            BuildDimensionGrid(card.transform, inner, y);
            y -= 68f + sectionGap;

            // ── Materials ────────────────────────────────────────────────────
            SectionLabel("MATERIALS", card.transform, ref y, inner);
            y -= 10f;
            _materialsText = AbsText("Lacquered MDF fronts, European oak butcher-block, brushed-brass hardware",
                card.transform, new Vector2(inner, 44f), new Vector2(0f, y), 14f, Dark);
            _materialsText.enableWordWrapping = true; _materialsText.lineSpacing = 5f;
            y -= 44f + sectionGap;

            // ── Features ─────────────────────────────────────────────────────
            SectionLabel("FEATURES", card.transform, ref y, inner);
            y -= 8f;
            string[] stubFeats = { "Soft-close hinges throughout", "Integrated dimmable under-cabinet lighting", "Push-to-open island drawers", "FSC-certified oak, low-VOC finishes" };
            _featureTexts.Clear();
            for (int i = 0; i < 4; i++)
            {
                var ft = AbsText(stubFeats[i], card.transform, new Vector2(inner, 22f), new Vector2(0f, y), 14f, Dark);
                ft.enableWordWrapping = false;
                _featureTexts.Add(ft);
                y -= 22f + (i < 3 ? 8f : 0f);
            }
            y -= sectionGap;

            // ── Color & finish ───────────────────────────────────────────────
            SectionLabel("COLOR & FINISH", card.transform, ref y, inner);
            y -= 14f;
            BuildSwatches(card.transform, inner, y);
            y -= 78f + sectionGap;

            // ── Storage ──────────────────────────────────────────────────────
            SectionLabel("STORAGE", card.transform, ref y, inner);
            y -= 10f;
            _storageText = AbsText("12 base drawers, 4 wall cabinets, integrated pantry pull-out",
                card.transform, new Vector2(inner, 24f), new Vector2(0f, y), 14f, Dark);
            _storageText.enableWordWrapping = true;
            y -= 24f + sectionGap;

            // ── Price ────────────────────────────────────────────────────────
            var fromLbl = AbsText("FROM", card.transform, new Vector2(60f, 14f), new Vector2(0f, y), 10f, Muted);
            fromLbl.characterSpacing = 16f; fromLbl.fontStyle = FontStyles.Normal;
            y -= 14f + 2f;
            _priceText = AbsText("€5,240", card.transform, new Vector2(inner, 26f), new Vector2(0f, y), 18f, Dark);
            _priceText.fontStyle = FontStyles.Bold;
            y -= 26f + 16f;

            // ── CTA ──────────────────────────────────────────────────────────
            var ctaGO  = MakeGO("CTAButton", card.transform);
            var ctaRT  = ctaGO.GetComponent<RectTransform>();
            ctaRT.anchorMin = ctaRT.anchorMax = new Vector2(0.5f, 1f);
            ctaRT.pivot     = new Vector2(0.5f, 1f);
            ctaRT.sizeDelta = new Vector2(inner, ctaH);
            ctaRT.anchoredPosition = new Vector2(0f, y);
            var ctaBg  = ctaGO.AddComponent<UIImage>(); ctaBg.color = Dark; Round(ctaBg, 14f);
            var ctaBtn = ctaGO.AddComponent<Button>();  ctaBtn.targetGraphic = ctaBg;
            ctaBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            var ctaLbl = Tmp("Add to cart", ctaGO.transform, 14f, CtaText);
            Stretch(ctaLbl.gameObject);
            ctaLbl.alignment = TextAlignmentOptions.Center;
            ctaLbl.characterSpacing = 2f; ctaLbl.fontStyle = FontStyles.Normal; ctaLbl.raycastTarget = false;
        }

        // Builds the 4-tile dimensions row
        void BuildDimensionGrid(Transform card, float inner, float y)
        {
            const float gap     = 8f;
            float tileW = (inner - gap * 3f) / 4f;
            const float tileH   = 68f;
            string[] stubLabels = { "WIDTH", "HEIGHT", "DEPTH", "ISLAND" };
            string[] stubValues = { "240 cm", "220 cm", "62 cm", "180 cm" };

            for (int i = 0; i < 4; i++)
            {
                float x = -inner * 0.5f + i * (tileW + gap) + tileW * 0.5f;
                var tileGO = MakeGO($"DimTile_{i}", card);
                var tRT    = tileGO.GetComponent<RectTransform>();
                tRT.anchorMin = tRT.anchorMax = new Vector2(0.5f, 1f);
                tRT.pivot     = new Vector2(0.5f, 1f);
                tRT.sizeDelta = new Vector2(tileW, tileH);
                tRT.anchoredPosition = new Vector2(x, y);
                var bg = tileGO.AddComponent<UIImage>(); bg.color = DimTileBg; Round(bg, 14f);
                bg.raycastTarget = false;

                _dimTiles[i] = tileGO;

                // Label
                var lbl = Tmp(stubLabels[i], tileGO.transform, 9f, Muted);
                var lRT = lbl.GetComponent<RectTransform>();
                lRT.anchorMin = new Vector2(0f, 1f); lRT.anchorMax = new Vector2(1f, 1f);
                lRT.pivot     = new Vector2(0.5f, 1f);
                lRT.sizeDelta = new Vector2(0f, 16f);
                lRT.anchoredPosition = new Vector2(0f, -12f);
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.characterSpacing = 16f; lbl.fontStyle = FontStyles.Normal; lbl.raycastTarget = false;
                _dimLabelTexts[i] = lbl;

                // Value
                var val = Tmp(stubValues[i], tileGO.transform, 14f, Dark);
                var vRT = val.GetComponent<RectTransform>();
                vRT.anchorMin = new Vector2(0f, 1f); vRT.anchorMax = new Vector2(1f, 1f);
                vRT.pivot     = new Vector2(0.5f, 1f);
                vRT.sizeDelta = new Vector2(0f, 20f);
                vRT.anchoredPosition = new Vector2(0f, -12f - 16f - 4f);
                val.alignment = TextAlignmentOptions.Center;
                val.fontStyle = FontStyles.Bold; val.raycastTarget = false;
                _dimValueTexts[i] = val;
            }
        }

        // Builds color swatch row
        void BuildSwatches(Transform card, float inner, float y)
        {
            const float swSize = 56f;
            const float swGap  = 12f;
            const float lblH   = 14f;
            const float lblGap = 8f;
            // column width: 56px swatch, centred
            string[] stubNames   = { "Cream", "Oak", "Slate" };
            Color[]  stubColors  = { Hex(0xDDE1EC), Hex(0xC9A472), Hex(0x8A93AB) };
            bool[]   stubSelect  = { false, false, true }; // Slate selected in HTML

            _swatchImages.Clear(); _swatchAmbers.Clear(); _swatchLabels.Clear();

            for (int i = 0; i < 3; i++)
            {
                float x = -inner * 0.5f + i * (swSize + swGap) + swSize * 0.5f;

                // Column root
                var col = MakeGO($"Swatch_{i}", card);
                var colRT = col.GetComponent<RectTransform>();
                colRT.anchorMin = colRT.anchorMax = new Vector2(0.5f, 1f);
                colRT.pivot     = new Vector2(0.5f, 1f);
                colRT.sizeDelta = new Vector2(swSize, swSize + lblGap + lblH);
                colRT.anchoredPosition = new Vector2(x, y);

                // Amber outer ring (selection indicator, shown when selected)
                float amberSize = swSize + 11f; // +5.5 each side
                var amber = MakeGO("AmberRing", col.transform);
                var aRT   = amber.GetComponent<RectTransform>();
                aRT.anchorMin = aRT.anchorMax = aRT.pivot = new Vector2(0.5f, 1f);
                aRT.sizeDelta = new Vector2(amberSize, amberSize);
                aRT.anchoredPosition = new Vector2(0f, -(swSize - amberSize) * 0.5f);
                var amberImg = amber.AddComponent<UIImage>();
                amberImg.color = SwatchRingAmber; Round(amberImg, amberSize * 0.5f);
                amberImg.raycastTarget = false;
                amberImg.enabled = stubSelect[i];
                _swatchAmbers.Add(amberImg);

                // White inner ring
                float whiteSize = swSize + 8f; // +4 each side
                var white = MakeGO("WhiteRing", col.transform);
                var wRT   = white.GetComponent<RectTransform>();
                wRT.anchorMin = wRT.anchorMax = wRT.pivot = new Vector2(0.5f, 1f);
                wRT.sizeDelta = new Vector2(whiteSize, whiteSize);
                wRT.anchoredPosition = new Vector2(0f, -(swSize - whiteSize) * 0.5f);
                var whiteImg = white.AddComponent<UIImage>();
                whiteImg.color = stubSelect[i] ? SwatchRingWhite : new Color(1f, 1f, 1f, 0.5f);
                Round(whiteImg, whiteSize * 0.5f); whiteImg.raycastTarget = false;

                // Swatch colour square
                var swatch = MakeGO("SwatchColor", col.transform);
                var sRT    = swatch.GetComponent<RectTransform>();
                sRT.anchorMin = sRT.anchorMax = sRT.pivot = new Vector2(0.5f, 1f);
                sRT.sizeDelta = new Vector2(swSize, swSize);
                sRT.anchoredPosition = Vector2.zero;
                var swatchImg = swatch.AddComponent<UIImage>();
                swatchImg.color = stubColors[i]; Round(swatchImg, 12f);
                swatchImg.raycastTarget = false;
                _swatchImages.Add(swatchImg);

                // Label
                var lbl = Tmp(stubNames[i], col.transform, 11f, stubSelect[i] ? Dark : Muted);
                var lRT = lbl.GetComponent<RectTransform>();
                lRT.anchorMin = new Vector2(0f, 1f); lRT.anchorMax = new Vector2(1f, 1f);
                lRT.pivot     = new Vector2(0.5f, 1f);
                lRT.sizeDelta = new Vector2(0f, lblH);
                lRT.anchoredPosition = new Vector2(0f, -(swSize + lblGap));
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.fontStyle = stubSelect[i] ? FontStyles.Bold : FontStyles.Normal;
                lbl.raycastTarget = false;
                _swatchLabels.Add(lbl);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        // Places a section label at the current y, then advances y by 16px.
        void SectionLabel(string text, Transform parent, ref float y, float inner)
        {
            var t = AbsText(text, parent, new Vector2(inner, 16f), new Vector2(0f, y), 11f, Muted);
            t.characterSpacing = 20f; t.fontStyle = FontStyles.Bold;
            y -= 16f;
        }

        // Creates a TMP_Text anchored top-centre at (offsetX, offsetY) with given size.
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

            float inner  = cardWidth - padSide * 2f;
            const float headerContent = 16f + 8f + 28f + 4f + 18f;
            const float headerFooter  = 22f + 1f + 24f;
            const float closeH        = 44f + 14f;
            const float dimsSH        = 16f + 12f + 68f;
            const float matsSH        = 16f + 10f + 44f;
            const float featSH        = 16f + 8f + 4f * 22f + 3f * 8f;
            const float colorSH       = 16f + 14f + 78f;
            const float storSH        = 16f + 10f + 24f;
            const float priceSH       = 44f;
            const float ctaH          = 46f;
            float sg = sectionGap;
            float cardH = padTop + closeH + headerContent + headerFooter
                        + dimsSH + sg + matsSH + sg + featSH + sg
                        + colorSH + sg + storSH + sg + priceSH + 16f + ctaH + padBottom;

            float totalW = cardWidth + 40f;
            var rt = GetComponent<RectTransform>();
            if (rt) { rt.sizeDelta = new Vector2(totalW, cardH + 40f); rt.pivot = Vector2.one * 0.5f; }
            if (_scaler) { _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; _scaler.referencePixelsPerUnit = 100f; }
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
            var found = transform.Find("Generated_ProductDetails");
            if (found != null) Destroy2(found.gameObject);
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
            var go  = new GameObject(label.Length > 24 ? label.Substring(0, 24) : label, typeof(RectTransform));
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
        // with rounded corner alpha into a single texture.
        Sprite GradientRoundedSprite(float cardW, float cardH)
        {
            int texH = 256;
            int texW = Mathf.Max(1, Mathf.RoundToInt(texH * cardW / cardH));
            float rPx = radiusCard / cardH * texH;

            var c0 = new Color(0.831f, 0.851f, 0.898f); // #D4D9E5
            var c1 = new Color(0.710f, 0.737f, 0.816f); // #B5BCD0
            var c2 = new Color(0.612f, 0.643f, 0.737f); // #9CA4BC

            const float cx = 0.35f, cyCSS = 0.25f;
            float cy = 1f - cyCSS;
            float rx = Mathf.Max(cx, 1f - cx);
            float ry = Mathf.Max(cyCSS, 1f - cyCSS);

            var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;
            var pixels = new Color32[texW * texH];

            for (int y = 0; y < texH; y++)
            for (int x = 0; x < texW; x++)
            {
                float u  = (x + 0.5f) / texW;
                float v  = (y + 0.5f) / texH;
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
