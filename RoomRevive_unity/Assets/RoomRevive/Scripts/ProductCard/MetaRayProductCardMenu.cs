// MetaRayProductCardMenu.cs
// Drop on an empty GameObject.  Generates a world-space floating product-card
// carousel with Meta Interaction SDK ray-cast interaction.
//
// ── Files ────────────────────────────────────────────────────────────────────
//   MetaRayProductCardMenu.cs   (this file — menu + helper hitbox classes)
//   ProductCardData.cs          (ScriptableObject data per product)
//
// ── Requirements ─────────────────────────────────────────────────────────────
//   • Meta XR Interaction SDK in project
//   • A working Meta rig with HandRayInteractor / RayInteractor + selector
//   • TextMeshPro in project
//
// ── Quick-start ──────────────────────────────────────────────────────────────
//   1. Add component to an empty GameObject
//   2. Right-click component header → "Create Sample Product Cards"
//      → creates 3 SO assets and auto-assigns them to the Products list
//   3. Press Play (or hit Rebuild Menu in the inspector)
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UIImage = UnityEngine.UI.Image;
using UDebug = UnityEngine.Debug;
using UApp    = UnityEngine.Application;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RoomRevive
{
    /// <summary>
    /// Procedurally generates a world-space product-card carousel.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public class MetaRayProductCardMenu : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Inspector fields
        // ─────────────────────────────────────────────────────────────────────

        [Header("Product Data")]
        [Tooltip("Drag ProductCardData SO assets here. Order = carousel order.")]
        public List<ProductCardData> products = new List<ProductCardData>();
        [Range(1, 6)]
        public int fallbackProductCount = 3;

        [Serializable] public class ProductCardEvent : UnityEvent<int, ProductCardData> { }
        [Header("Events")]
        public ProductCardEvent onProductChanged  = new ProductCardEvent();
        public ProductCardEvent onProductConfirmed = new ProductCardEvent();

        [Header("World-Space Canvas")]
        public float worldScale            = 0.001f;
        public Camera eventCamera;
        public bool autoCreateEventSystem  = true;
        public bool autoRebuildInEditor    = true;
        public PlaneSurface.NormalFacing raySurfaceFacing = PlaneSurface.NormalFacing.Backward;

        [Header("Layout (pixels)")]
        public float cardWidth             = 320f;
        public float imageAreaHeight       = 196f;
        public float dotsRowHeight         = 26f;
        public float nameBlockHeight       = 84f;
        public float bottomPanelMinHeight  = 220f;
        public float panelGap              = 14f;
        public float arrowButtonSize       = 64f;
        public float arrowGap              = 24f;
        public float specChipHeight        = 68f;
        public int   specChipColumns       = 2;
        public float specGridGap           = 8f;

        [Header("Colors")]
        public Color panelBg         = new Color(1f, 1f, 1f, 0.07f);
        public Color panelBorder     = new Color(1f, 1f, 1f, 0.13f);
        public Color arrowBgNormal   = new Color(1f, 1f, 1f, 0.07f);
        public Color arrowBgHover    = new Color(1f, 1f, 1f, 0.20f);
        public Color arrowBorderCol  = new Color(1f, 1f, 1f, 0.18f);
        public Color textPrimary     = new Color(1f, 1f, 1f, 0.95f);
        public Color textSecondary   = new Color(1f, 1f, 1f, 0.42f);
        public Color textMuted       = new Color(1f, 1f, 1f, 0.38f);
        public Color chipBgNormal    = new Color(1f, 1f, 1f, 0.06f);
        public Color chipBgHover     = new Color(1f, 1f, 1f, 0.10f);
        public Color chipBorderCol   = new Color(1f, 1f, 1f, 0.07f);
        public Color dotInactive     = new Color(1f, 1f, 1f, 0.20f);

        // Fallback colors when no SO is assigned
        public Color fallbackImageBg0 = new Color(0.15f, 0.12f, 0.35f, 1f);
        public Color fallbackImageBg1 = new Color(0.10f, 0.15f, 0.35f, 1f);
        public Color fallbackImageBg2 = new Color(0.08f, 0.20f, 0.32f, 1f);
        public Color fallbackAccent0  = new Color(0.58f, 0.67f, 1f,  1f);
        public Color fallbackAccent1  = new Color(0.52f, 0.72f, 1f,  1f);
        public Color fallbackAccent2  = new Color(0.45f, 0.78f, 1f,  1f);

        [Header("Animation")]
        public float floatAmplitudeTop    = 7f;
        public float floatPeriodTop       = 6f;
        public float floatAmplitudeBottom = 4f;
        public float floatPeriodBottom    = 7f;
        public float floatPhaseBottom     = 0.8f;   // seconds delay offset
        public float transitionDuration   = 0.24f;
        public float slideDistancePx      = 18f;
        public float arrowHoverScale      = 1.08f;
        public float arrowPressScale      = 0.94f;
        public float arrowScaleLerpSpeed  = 12f;

        [Header("Typography")]
        public float productNameFontSize  = 22f;
        public float categoryFontSize     = 13f;
        public float specValueFontSize    = 15f;
        public float specLabelFontSize    = 10f;
        public float specsHeaderFontSize  = 10f;

        [Header("Debug")]
        public bool debugLogs    = true;
        public bool keyboardDebug = true;

        // ─────────────────────────────────────────────────────────────────────
        //  Serialised hidden references
        // ─────────────────────────────────────────────────────────────────────

        [SerializeField, HideInInspector] private GameObject    _generatedRoot;
        [SerializeField, HideInInspector] private GameObject    _raySurfaceRoot;
        [SerializeField, HideInInspector] private PointableCanvas _pointableCanvas;
        [SerializeField, HideInInspector] private RayInteractable _canvasRayInteractable;

        // ─────────────────────────────────────────────────────────────────────
        //  Private runtime state
        // ─────────────────────────────────────────────────────────────────────

        private Canvas          _canvas;
        private CanvasScaler    _canvasScaler;
        private GraphicRaycaster _graphicRaycaster;
        private RectTransform   _rectTransform;

        private int  _currentIndex;
        private bool _subscribedToRayState;

        // UI references rebuilt each time RebuildMenu() runs
        private RectTransform  _topPanelRT;
        private RectTransform  _bottomPanelRT;
        private RectTransform  _contentGroupRT;
        private CanvasGroup    _contentCanvasGroup;
        private UIImage        _imageAreaBg;
        private TextMeshProUGUI _productNameTMP;
        private TextMeshProUGUI _categoryTMP;
        private TextMeshProUGUI _specsHeaderTMP;
        private readonly List<RectTransform>  _dots      = new List<RectTransform>();
        private readonly List<SpecChipRuntime> _specChips = new List<SpecChipRuntime>();

        // Float animation base positions (set after layout is built)
        private Vector2 _topPanelBase;
        private Vector2 _bottomPanelBase;

        // Carousel slide/fade transition state machine
        private enum TransitionPhase { Idle, FadeOut, FadeIn }
        private TransitionPhase _transitionPhase = TransitionPhase.Idle;
        private float _transitionTimer;
        private int   _pendingIndex;
        private int   _pendingDirection; // –1 = left, +1 = right

        // Computed once in ConfigureCanvas, reused in BuildContent
        private float _computedBottomPanelHeight;

#if UNITY_EDITOR
        private bool _editorRebuildQueued;
#endif

        // ── Runtime helper ──────────────────────────────────────────────────

        private class SpecChipRuntime
        {
            public GameObject       root;
            public UIImage          background;
            public TextMeshProUGUI  iconTMP;
            public TextMeshProUGUI  labelTMP;
            public TextMeshProUGUI  valueTMP;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Unity lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Reset()
        {
            GrabComponents();
            ConfigureCanvas();
        }

        private void Awake()
        {
            GrabComponents();
            ConfigureCanvas();
        }

        private void Start()
        {
            if (!UApp.isPlaying) return;
            RebuildMenu();
            SubscribeRayState();
            ShowProduct(_currentIndex, instant: true);
        }

        private void Update()
        {
            if (!UApp.isPlaying) return;
            UpdateTransition();
            UpdateFloatAnimation();
            if (keyboardDebug) HandleKeyboardDebug();
        }

        private void OnDisable() => UnsubscribeRayState();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UApp.isPlaying || !autoRebuildInEditor) return;
            QueueEditorRebuild();
        }

        private void QueueEditorRebuild()
        {
            if (_editorRebuildQueued) return;
            _editorRebuildQueued = true;
            EditorApplication.delayCall += () =>
            {
                _editorRebuildQueued = false;
                if (this == null || gameObject == null || UApp.isPlaying) return;
                RebuildMenu();
            };
        }

        // ── Context-menu: creates 3 sample SOs ─────────────────────────────

        [ContextMenu("Create Sample Product Cards")]
        private void CreateSampleProductCards()
        {
            const string root    = "Assets/RoomRevive";
            const string folder  = root + "/ProductCards";

            if (!AssetDatabase.IsValidFolder(root))
                AssetDatabase.CreateFolder("Assets", "RoomRevive");
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(root, "ProductCards");

            var p1 = GetOrCreateAsset<ProductCardData>(folder, "PC_Kombi1PPAKB");
            p1.productName = "Kombi 1PPAKB";
            p1.category    = "Kitchen combination";
            p1.accentColor  = new Color(0.58f, 0.67f, 1f, 1f);
            p1.imageBgColor = new Color(0.15f, 0.12f, 0.35f, 1f);
            p1.specs = new List<ProductSpec>
            {
                new ProductSpec { icon = "📐", label = "BASE HEIGHT",    value = "10 cm"  },
                new ProductSpec { icon = "↔",  label = "OVERALL WIDTH",  value = "201 cm" },
                new ProductSpec { icon = "↕",  label = "WORKING HEIGHT", value = "93 cm"  },
                new ProductSpec { icon = "📏", label = "TOTAL LENGTH",   value = "64 cm"  },
                new ProductSpec { icon = "🚪", label = "DOOR TYPE",      value = "Hinged" },
                new ProductSpec { icon = "🗂", label = "COMPARTMENTS",   value = "13"     },
            };
            EditorUtility.SetDirty(p1);

            var p2 = GetOrCreateAsset<ProductCardData>(folder, "PC_MetodVAXHULT");
            p2.productName = "Metod VAXHULT";
            p2.category    = "Base cabinet with shelves";
            p2.accentColor  = new Color(0.52f, 0.72f, 1f, 1f);
            p2.imageBgColor = new Color(0.10f, 0.15f, 0.35f, 1f);
            p2.specs = new List<ProductSpec>
            {
                new ProductSpec { icon = "📐", label = "BASE HEIGHT",    value = "80 cm"     },
                new ProductSpec { icon = "↔",  label = "OVERALL WIDTH",  value = "160 cm"    },
                new ProductSpec { icon = "↕",  label = "WORKING HEIGHT", value = "88 cm"     },
                new ProductSpec { icon = "📏", label = "TOTAL LENGTH",   value = "60 cm"     },
                new ProductSpec { icon = "🚪", label = "DOOR TYPE",      value = "Push-open" },
                new ProductSpec { icon = "🗂", label = "COMPARTMENTS",   value = "9"         },
            };
            EditorUtility.SetDirty(p2);

            var p3 = GetOrCreateAsset<ProductCardData>(folder, "PC_SektionAXSTAD");
            p3.productName = "Sektion AXSTAD";
            p3.category    = "Wall cabinet combination";
            p3.accentColor  = new Color(0.45f, 0.78f, 1f, 1f);
            p3.imageBgColor = new Color(0.08f, 0.20f, 0.32f, 1f);
            p3.specs = new List<ProductSpec>
            {
                new ProductSpec { icon = "📐", label = "BASE HEIGHT",    value = "14 cm"   },
                new ProductSpec { icon = "↔",  label = "OVERALL WIDTH",  value = "180 cm"  },
                new ProductSpec { icon = "↕",  label = "WORKING HEIGHT", value = "90 cm"   },
                new ProductSpec { icon = "📏", label = "TOTAL LENGTH",   value = "58 cm"   },
                new ProductSpec { icon = "🚪", label = "DOOR TYPE",      value = "Sliding" },
                new ProductSpec { icon = "🗂", label = "COMPARTMENTS",   value = "7"       },
            };
            EditorUtility.SetDirty(p3);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            products.Clear();
            products.Add(p1);
            products.Add(p2);
            products.Add(p3);

            if (debugLogs)
                UDebug.Log("<b>[MetaRayProductCardMenu]</b> Created 3 product card SOs → Assets/RoomRevive/ProductCards/");
        }

        private static T GetOrCreateAsset<T>(string folder, string name) where T : ScriptableObject
        {
            string path = $"{folder}/{name}.asset";
            T existing  = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
#endif

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Destroys and fully rebuilds all generated UI.</summary>
        [ContextMenu("Rebuild Menu")]
        public void RebuildMenu()
        {
            UnsubscribeRayState();
            GrabComponents();
            ComputeBottomPanelHeight();
            ConfigureCanvas();
            EnsurePointableCanvas();
            EnsureEventSystem();
            ClearGeneratedObjects();

            _dots.Clear();
            _specChips.Clear();
            _transitionPhase = TransitionPhase.Idle;

            _generatedRoot = MakeUI("Generated_ProductCardMenu", transform);
            StretchFull(_generatedRoot);

            BuildRayInteractionSurface();
            BuildContent(_generatedRoot.transform);

            if (UApp.isPlaying)
            {
                SubscribeRayState();
                ShowProduct(_currentIndex, instant: true);
            }

            if (debugLogs)
                UDebug.Log($"<b>[MetaRayProductCardMenu]</b> Rebuilt — {GetCount()} product(s).");
        }

        /// <summary>Navigate one step to the left (wraps).</summary>
        public void NavigateLeft()
        {
            if (_transitionPhase != TransitionPhase.Idle) return;
            int count = GetCount();
            if (count <= 1) return;
            BeginTransition((_currentIndex - 1 + count) % count, direction: -1);
        }

        /// <summary>Navigate one step to the right (wraps).</summary>
        public void NavigateRight()
        {
            if (_transitionPhase != TransitionPhase.Idle) return;
            int count = GetCount();
            if (count <= 1) return;
            BeginTransition((_currentIndex + 1) % count, direction: +1);
        }

        /// <summary>Fires onConfirmed for the currently visible product.</summary>
        public void ConfirmCurrent()
        {
            var data = GetData(_currentIndex);
            if (debugLogs)
                UDebug.Log($"<color=lime><b>[MetaRayProductCardMenu]</b></color> Confirmed: {GetName(_currentIndex)}");
            data?.onConfirmed?.Invoke();
            onProductConfirmed?.Invoke(_currentIndex, data);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Build helpers
        // ─────────────────────────────────────────────────────────────────────

        private void BuildContent(Transform parent)
        {
            float topH   = imageAreaHeight + dotsRowHeight + nameBlockHeight;
            float totalH = topH + panelGap + _computedBottomPanelHeight;
            float totalW = cardWidth + 2f * (arrowGap + arrowButtonSize);

            // Left / right arrows — positioned absolutely in parent space
            BuildArrow(parent, isLeft: true);
            BuildArrow(parent, isLeft: false);

            // ContentGroup — handles slide+fade transition as a unit
            GameObject cg = MakeUI("ContentGroup", parent);
            _contentGroupRT        = cg.GetComponent<RectTransform>();
            _contentGroupRT.anchorMin = new Vector2(0.5f, 0.5f);
            _contentGroupRT.anchorMax = new Vector2(0.5f, 0.5f);
            _contentGroupRT.pivot     = new Vector2(0.5f, 0.5f);
            _contentGroupRT.sizeDelta = new Vector2(cardWidth, totalH);
            _contentGroupRT.anchoredPosition = Vector2.zero;

            _contentCanvasGroup               = cg.AddComponent<CanvasGroup>();
            _contentCanvasGroup.alpha          = 1f;
            _contentCanvasGroup.interactable   = true;
            _contentCanvasGroup.blocksRaycasts = true;

            BuildTopPanel(cg.transform, topH);
            BuildBottomPanel(cg.transform, topH);
        }

        // ── Top panel ──────────────────────────────────────────────────────

        private void BuildTopPanel(Transform parent, float topH)
        {
            GameObject go = MakeUI("TopPanel", parent);
            _topPanelRT = go.GetComponent<RectTransform>();
            _topPanelRT.anchorMin        = new Vector2(0.5f, 1f);
            _topPanelRT.anchorMax        = new Vector2(0.5f, 1f);
            _topPanelRT.pivot            = new Vector2(0.5f, 1f);
            _topPanelRT.sizeDelta        = new Vector2(cardWidth, topH);
            _topPanelRT.anchoredPosition = Vector2.zero;
            _topPanelBase                = Vector2.zero;

            var bg = go.AddComponent<UIImage>(); bg.color = panelBg; bg.raycastTarget = false;
            var ol = go.AddComponent<Outline>();  ol.effectColor = panelBorder; ol.effectDistance = new Vector2(1f, -1f);

            // Image area
            GameObject imgGO = MakeUI("ImageArea", go.transform);
            var imgRT = imgGO.GetComponent<RectTransform>();
            imgRT.anchorMin = new Vector2(0f, 1f); imgRT.anchorMax = new Vector2(1f, 1f);
            imgRT.pivot = new Vector2(0.5f, 1f);
            imgRT.anchoredPosition = Vector2.zero;
            imgRT.sizeDelta = new Vector2(0f, imageAreaHeight);

            _imageAreaBg = imgGO.AddComponent<UIImage>();
            _imageAreaBg.color = GetImageBg(0);
            _imageAreaBg.raycastTarget = false;

            // Placeholder text
            var ph = AddTMP("Placeholder", imgGO.transform);
            StretchFull(ph.gameObject);
            ph.text = "PRODUCT\nPHOTO";
            ph.fontSize = 11f;
            ph.color = new Color(1f, 1f, 1f, 0.22f);
            ph.alignment = TextAlignmentOptions.Center;
            ph.characterSpacing = 5f;
            ph.raycastTarget = false;

            // Dots row
            BuildDotsRow(go.transform);

            // Name block
            BuildNameBlock(go.transform);
        }

        private void BuildDotsRow(Transform parent)
        {
            int count = GetCount();
            GameObject go = MakeUI("DotsRow", parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -imageAreaHeight);
            rt.sizeDelta = new Vector2(cardWidth, dotsRowHeight);

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = hlg.childControlHeight = false;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;

            _dots.Clear();
            for (int i = 0; i < count; i++)
            {
                GameObject dot = MakeUI($"Dot_{i}", go.transform);
                var dotRT = dot.GetComponent<RectTransform>();
                dotRT.sizeDelta = new Vector2(i == 0 ? 24f : 6f, 6f);
                var img = dot.AddComponent<UIImage>();
                img.color = i == 0 ? GetAccent(0) : dotInactive;
                img.raycastTarget = false;
                _dots.Add(dotRT);
            }
        }

        private void BuildNameBlock(Transform parent)
        {
            float yOffset = imageAreaHeight + dotsRowHeight;
            GameObject go = MakeUI("NameBlock", parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -yOffset);
            rt.sizeDelta = new Vector2(0f, nameBlockHeight);

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 8, 18);
            vlg.spacing = 3f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false; vlg.childForceExpandHeight = false;

            _productNameTMP = AddTMP("ProductName", go.transform);
            _productNameTMP.text           = GetName(0);
            _productNameTMP.fontSize       = productNameFontSize;
            _productNameTMP.fontStyle      = FontStyles.Bold;
            _productNameTMP.color          = textPrimary;
            _productNameTMP.alignment      = TextAlignmentOptions.Left;
            _productNameTMP.enableWordWrapping = false;
            _productNameTMP.overflowMode   = TextOverflowModes.Ellipsis;
            _productNameTMP.raycastTarget  = false;
            SetMinHeight(_productNameTMP.gameObject, productNameFontSize * 1.5f);

            _categoryTMP = AddTMP("Category", go.transform);
            _categoryTMP.text           = GetCategory(0);
            _categoryTMP.fontSize       = categoryFontSize;
            _categoryTMP.fontStyle      = FontStyles.Normal;
            _categoryTMP.color          = textSecondary;
            _categoryTMP.alignment      = TextAlignmentOptions.Left;
            _categoryTMP.enableWordWrapping = false;
            _categoryTMP.overflowMode   = TextOverflowModes.Ellipsis;
            _categoryTMP.raycastTarget  = false;
            SetMinHeight(_categoryTMP.gameObject, categoryFontSize * 1.5f);
        }

        // ── Bottom panel ───────────────────────────────────────────────────

        private void BuildBottomPanel(Transform parent, float topH)
        {
            GameObject go = MakeUI("BottomPanel", parent);
            _bottomPanelRT = go.GetComponent<RectTransform>();
            _bottomPanelRT.anchorMin = new Vector2(0.5f, 1f);
            _bottomPanelRT.anchorMax = new Vector2(0.5f, 1f);
            _bottomPanelRT.pivot     = new Vector2(0.5f, 1f);
            _bottomPanelRT.sizeDelta = new Vector2(cardWidth, _computedBottomPanelHeight);
            _bottomPanelRT.anchoredPosition = new Vector2(0f, -(topH + panelGap));
            _bottomPanelBase = _bottomPanelRT.anchoredPosition;

            var bg = go.AddComponent<UIImage>(); bg.color = panelBg; bg.raycastTarget = false;
            var ol = go.AddComponent<Outline>();  ol.effectColor = panelBorder; ol.effectDistance = new Vector2(1f, -1f);

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 16, 16);
            vlg.spacing = 12f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false; vlg.childForceExpandHeight = false;

            // "SPECIFICATIONS" header
            _specsHeaderTMP = AddTMP("SpecsHeader", go.transform);
            _specsHeaderTMP.text             = "SPECIFICATIONS";
            _specsHeaderTMP.fontSize         = specsHeaderFontSize;
            _specsHeaderTMP.fontStyle        = FontStyles.Bold;
            _specsHeaderTMP.color            = GetAccent(0);
            _specsHeaderTMP.alignment        = TextAlignmentOptions.Left;
            _specsHeaderTMP.characterSpacing = 12f;
            _specsHeaderTMP.raycastTarget    = false;
            SetMinHeight(_specsHeaderTMP.gameObject, specsHeaderFontSize * 2.2f);

            BuildSpecGrid(go.transform);
        }

        private void BuildSpecGrid(Transform parent)
        {
            int specCount = GetMaxSpecCount();
            int rows      = Mathf.CeilToInt((float)specCount / specChipColumns);
            float gridH   = rows * specChipHeight + Mathf.Max(0, rows - 1) * specGridGap;

            GameObject grid = MakeUI("SpecsGrid", parent);
            SetMinHeight(grid, gridH);

            float chipW = (cardWidth - 32f - specGridGap * (specChipColumns - 1)) / specChipColumns;

            var glg = grid.AddComponent<GridLayoutGroup>();
            glg.cellSize    = new Vector2(chipW, specChipHeight);
            glg.spacing     = new Vector2(specGridGap, specGridGap);
            glg.startCorner = GridLayoutGroup.Corner.UpperLeft;
            glg.startAxis   = GridLayoutGroup.Axis.Horizontal;
            glg.childAlignment = TextAnchor.UpperLeft;
            glg.constraint  = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = specChipColumns;

            _specChips.Clear();
            for (int i = 0; i < specCount; i++)
                BuildSpecChip(grid.transform, i);
        }

        private void BuildSpecChip(Transform parent, int index)
        {
            var chip = new SpecChipRuntime();
            chip.root = MakeUI($"Chip_{index}", parent);

            // Outer border image (raycast target for hover)
            var borderImg = chip.root.AddComponent<UIImage>();
            borderImg.color = chipBorderCol;
            borderImg.raycastTarget = true;

            // Hover hitbox
            var hitbox = chip.root.AddComponent<ProductCardSpecChipHitbox>();

            // Inner content with inset
            GameObject content = MakeUI("Content", chip.root.transform);
            StretchInset(content, 1f);

            chip.background = content.AddComponent<UIImage>();
            chip.background.color = chipBgNormal;
            chip.background.raycastTarget = false;

            hitbox.Init(chip.background, chipBgNormal, chipBgHover);

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 10, 10);
            vlg.spacing = 4f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false; vlg.childForceExpandHeight = false;

            // Top row: icon + label
            GameObject topRow = MakeUI("TopRow", content.transform);
            SetMinHeight(topRow, specLabelFontSize + 4f);
            var hlg = topRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = hlg.childControlHeight = false;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;

            chip.iconTMP = AddTMP("Icon", topRow.transform);
            chip.iconTMP.fontSize      = specLabelFontSize + 2f;
            chip.iconTMP.color         = textMuted;
            chip.iconTMP.raycastTarget = false;
            chip.iconTMP.enableWordWrapping = false;
            SetMinWidth(chip.iconTMP.gameObject, 18f);
            SetMinHeight(chip.iconTMP.gameObject, specLabelFontSize + 4f);

            chip.labelTMP = AddTMP("Label", topRow.transform);
            chip.labelTMP.fontSize         = specLabelFontSize;
            chip.labelTMP.fontStyle        = FontStyles.Normal;
            chip.labelTMP.color            = textMuted;
            chip.labelTMP.alignment        = TextAlignmentOptions.Left;
            chip.labelTMP.characterSpacing = 7f;
            chip.labelTMP.raycastTarget    = false;
            chip.labelTMP.enableWordWrapping = false;
            SetMinWidth(chip.labelTMP.gameObject, 100f);
            SetMinHeight(chip.labelTMP.gameObject, specLabelFontSize + 4f);

            // Value
            chip.valueTMP = AddTMP("Value", content.transform);
            chip.valueTMP.fontSize      = specValueFontSize;
            chip.valueTMP.fontStyle     = FontStyles.Bold;
            chip.valueTMP.color         = textPrimary;
            chip.valueTMP.alignment     = TextAlignmentOptions.Left;
            chip.valueTMP.raycastTarget = false;
            chip.valueTMP.enableWordWrapping = false;
            SetMinHeight(chip.valueTMP.gameObject, specValueFontSize * 1.5f);

            _specChips.Add(chip);
        }

        // ── Arrow buttons ──────────────────────────────────────────────────

        private void BuildArrow(Transform parent, bool isLeft)
        {
            float xOff = isLeft
                ? -(cardWidth * 0.5f + arrowGap + arrowButtonSize * 0.5f)
                :  (cardWidth * 0.5f + arrowGap + arrowButtonSize * 0.5f);

            GameObject go = MakeUI(isLeft ? "LeftArrow" : "RightArrow", parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(arrowButtonSize, arrowButtonSize);
            rt.anchoredPosition = new Vector2(xOff, 0f);

            var bg = go.AddComponent<UIImage>(); bg.color = arrowBgNormal; bg.raycastTarget = true;
            var ol = go.AddComponent<Outline>();  ol.effectColor = arrowBorderCol; ol.effectDistance = new Vector2(1f, -1f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition    = Selectable.Transition.ColorTint;
            btn.navigation    = new Navigation { mode = Navigation.Mode.None };
            var cols          = btn.colors;
            cols.normalColor       = arrowBgNormal;
            cols.highlightedColor  = arrowBgHover;
            cols.pressedColor      = new Color(arrowBgHover.r, arrowBgHover.g, arrowBgHover.b, arrowBgHover.a * 0.6f);
            cols.selectedColor     = arrowBgNormal;
            btn.colors             = cols;
            btn.onClick.AddListener(isLeft ? (UnityAction)NavigateLeft : NavigateRight);

            // Scale hover/press hitbox
            var hitbox = go.AddComponent<ProductCardArrowHitbox>();
            hitbox.Init(rt, arrowHoverScale, arrowPressScale, arrowScaleLerpSpeed);

            var lbl = AddTMP("Label", go.transform);
            StretchFull(lbl.gameObject);
            lbl.text      = isLeft ? "‹" : "›";
            lbl.fontSize  = 28f;
            lbl.color     = new Color(1f, 1f, 1f, 0.9f);
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.raycastTarget = false;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Product display
        // ─────────────────────────────────────────────────────────────────────

        private void ShowProduct(int idx, bool instant = false)
        {
            var data   = GetData(idx);
            Color acc  = GetAccent(idx);

            if (_imageAreaBg    != null) _imageAreaBg.color   = GetImageBg(idx);
            if (_productNameTMP != null) _productNameTMP.text = GetName(idx);
            if (_categoryTMP    != null) _categoryTMP.text    = GetCategory(idx);
            if (_specsHeaderTMP != null) _specsHeaderTMP.color = acc;

            // Dots
            for (int i = 0; i < _dots.Count; i++)
            {
                bool active = i == idx;
                _dots[i].sizeDelta = new Vector2(active ? 24f : 6f, 6f);
                var img = _dots[i].GetComponent<UIImage>();
                if (img) img.color = active ? acc : dotInactive;
            }

            // Spec chips
            var specs = data?.specs;
            for (int i = 0; i < _specChips.Count; i++)
            {
                var chip = _specChips[i];
                if (chip.root == null) continue;
                bool hasSpec = specs != null && i < specs.Count;
                chip.root.SetActive(hasSpec);
                if (!hasSpec) continue;
                var s = specs[i];
                if (chip.iconTMP  != null) chip.iconTMP.text  = s.icon;
                if (chip.labelTMP != null) chip.labelTMP.text = s.label;
                if (chip.valueTMP != null) chip.valueTMP.text = s.value;
            }

            data?.onSelected?.Invoke();
            onProductChanged?.Invoke(idx, data);

            if (debugLogs)
                UDebug.Log($"<color=yellow><b>[MetaRayProductCardMenu]</b></color> Product {idx}: {GetName(idx)}");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Transition (slide + fade)
        // ─────────────────────────────────────────────────────────────────────

        private void BeginTransition(int target, int direction)
        {
            _pendingIndex     = target;
            _pendingDirection = direction;
            _transitionPhase  = TransitionPhase.FadeOut;
            _transitionTimer  = 0f;
        }

        private void UpdateTransition()
        {
            if (_transitionPhase == TransitionPhase.Idle) return;
            if (_contentCanvasGroup == null || _contentGroupRT == null) return;

            _transitionTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_transitionTimer / transitionDuration);

            if (_transitionPhase == TransitionPhase.FadeOut)
            {
                _contentCanvasGroup.alpha = 1f - t;
                _contentGroupRT.anchoredPosition = new Vector2(
                    slideDistancePx * _pendingDirection * t,
                    _contentGroupRT.anchoredPosition.y);

                if (t >= 1f)
                {
                    _currentIndex = _pendingIndex;
                    ShowProduct(_currentIndex, instant: true);
                    // Snap to opposite side before fading in
                    _contentGroupRT.anchoredPosition = new Vector2(
                        -slideDistancePx * _pendingDirection,
                        _contentGroupRT.anchoredPosition.y);
                    _transitionPhase = TransitionPhase.FadeIn;
                    _transitionTimer = 0f;
                }
            }
            else if (_transitionPhase == TransitionPhase.FadeIn)
            {
                _contentCanvasGroup.alpha = t;
                _contentGroupRT.anchoredPosition = new Vector2(
                    Mathf.Lerp(-slideDistancePx * _pendingDirection, 0f, t),
                    _contentGroupRT.anchoredPosition.y);

                if (t >= 1f)
                {
                    _contentCanvasGroup.alpha = 1f;
                    _contentGroupRT.anchoredPosition = new Vector2(0f, _contentGroupRT.anchoredPosition.y);
                    _transitionPhase = TransitionPhase.Idle;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Float animation
        // ─────────────────────────────────────────────────────────────────────

        private void UpdateFloatAnimation()
        {
            if (_topPanelRT == null || _bottomPanelRT == null) return;

            float topY = Mathf.Sin(Time.time * (Mathf.PI * 2f / floatPeriodTop)) * floatAmplitudeTop;
            float botY = Mathf.Sin((Time.time + floatPhaseBottom) * (Mathf.PI * 2f / floatPeriodBottom)) * floatAmplitudeBottom;

            _topPanelRT.anchoredPosition    = new Vector2(_topPanelBase.x,    _topPanelBase.y    + topY);
            _bottomPanelRT.anchoredPosition = new Vector2(_bottomPanelBase.x, _bottomPanelBase.y + botY);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Meta SDK / PointableCanvas setup
        //  (mirrors the reference MetaRayIntentCardMenu exactly)
        // ─────────────────────────────────────────────────────────────────────

        private void GrabComponents()
        {
            _canvas           = GetComponent<Canvas>();
            _canvasScaler     = GetComponent<CanvasScaler>();
            _graphicRaycaster = GetComponent<GraphicRaycaster>();
            _rectTransform    = GetComponent<RectTransform>();
        }

        private void ComputeBottomPanelHeight()
        {
            int  specCount = GetMaxSpecCount();
            int  rows      = Mathf.CeilToInt((float)specCount / Mathf.Max(1, specChipColumns));
            float gridH    = rows * specChipHeight + Mathf.Max(0, rows - 1) * specGridGap;
            float hdrH     = specsHeaderFontSize * 2.2f;
            float computed = 16f * 2f + hdrH + 12f + gridH;
            _computedBottomPanelHeight = Mathf.Max(bottomPanelMinHeight, computed);
        }

        private void ConfigureCanvas()
        {
            if (_canvas == null) return;

            float topH    = imageAreaHeight + dotsRowHeight + nameBlockHeight;
            float totalH  = topH + panelGap + _computedBottomPanelHeight;
            float totalW  = cardWidth + 2f * (arrowGap + arrowButtonSize);

            _canvas.renderMode  = RenderMode.WorldSpace;
            _canvas.worldCamera = eventCamera != null ? eventCamera : Camera.main;
            transform.localScale = Vector3.one * worldScale;

            if (_rectTransform != null)
            {
                _rectTransform.sizeDelta = new Vector2(totalW + 20f, totalH + 40f);
                _rectTransform.pivot     = new Vector2(0.5f, 0.5f);
            }

            if (_canvasScaler != null)
            {
                _canvasScaler.uiScaleMode           = CanvasScaler.ScaleMode.ConstantPixelSize;
                _canvasScaler.referencePixelsPerUnit = 100f;
                _canvasScaler.dynamicPixelsPerUnit   = 10f;
            }

            if (_graphicRaycaster != null)
            {
                _graphicRaycaster.ignoreReversedGraphics = true;
                _graphicRaycaster.blockingObjects        = GraphicRaycaster.BlockingObjects.None;
            }
        }

        private void EnsurePointableCanvas()
        {
            _pointableCanvas = GetComponent<PointableCanvas>() ?? gameObject.AddComponent<PointableCanvas>();
            if (_canvas != null) _pointableCanvas.InjectAllPointableCanvas(_canvas);
        }

        private void EnsureEventSystem()
        {
            if (!autoCreateEventSystem) return;
            var es = FindAny<EventSystem>();
            if (es == null)
            {
                es = new GameObject("EventSystem – Meta Pointable Canvas").AddComponent<EventSystem>();
                if (debugLogs) UDebug.Log("<b>[MetaRayProductCardMenu]</b> Created EventSystem.");
            }
            if (es.GetComponent<PointableCanvasModule>() == null)
                es.gameObject.AddComponent<PointableCanvasModule>();
        }

        private void BuildRayInteractionSurface()
        {
            _raySurfaceRoot = MakeUI("ISDK_RayInteractionSurface", transform);
            StretchFull(_raySurfaceRoot);
            AddOrGet<LayoutElement>(_raySurfaceRoot).ignoreLayout = true;

            var plane  = AddOrGet<PlaneSurface>(_raySurfaceRoot);
            plane.Facing = raySurfaceFacing;

            var boundsClipper  = AddOrGet<BoundsClipper>(_raySurfaceRoot);
            var clipperDriver  = AddOrGet<RectTransformBoundsClipperDriver>(_raySurfaceRoot);
            ConfigureClipperDriver(clipperDriver, boundsClipper);

            var clipped = AddOrGet<ClippedPlaneSurface>(_raySurfaceRoot);
            clipped.InjectAllClippedPlaneSurface(plane, new List<IBoundsClipper> { boundsClipper });

            _canvasRayInteractable = AddOrGet<RayInteractable>(_raySurfaceRoot);
            _canvasRayInteractable.InjectAllRayInteractable(clipped);
            _canvasRayInteractable.InjectOptionalSelectSurface(plane);
        }

        // Reflection-based setup keeps this compatible across SDK versions
        private static void ConfigureClipperDriver(RectTransformBoundsClipperDriver driver, BoundsClipper clipper)
        {
            if (driver == null || clipper == null) return;
            var rt   = driver.GetComponent<RectTransform>();
            var type = typeof(RectTransformBoundsClipperDriver);

            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!method.Name.StartsWith("Inject", StringComparison.Ordinal)) continue;
                var parms = method.GetParameters();
                var args  = new object[parms.Length];
                bool ok   = true;
                for (int i = 0; i < parms.Length; i++)
                {
                    var p = parms[i].ParameterType;
                    if      (p.IsAssignableFrom(typeof(BoundsClipper)))  args[i] = clipper;
                    else if (p.IsAssignableFrom(typeof(RectTransform)))  args[i] = rt;
                    else if (p.IsAssignableFrom(typeof(Transform)))      args[i] = rt;
                    else { ok = false; break; }
                }
                if (!ok) continue;
                try { method.Invoke(driver, args); break; } catch { /* version mismatch, fall through */ }
            }

            TrySetField(driver, "_boundsClipper", clipper);
            TrySetField(driver, "_rectTransform",  rt);
            try { type.GetMethod("Resize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(driver, null); } catch { }
        }

        private static void TrySetField(object target, string name, object value)
        {
            var f = target?.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            try { f?.SetValue(target, value); } catch { }
        }

        private void SubscribeRayState()
        {
            if (_canvasRayInteractable == null || _subscribedToRayState) return;
            _canvasRayInteractable.WhenStateChanged += OnRayStateChanged;
            _subscribedToRayState = true;
        }

        private void UnsubscribeRayState()
        {
            if (_canvasRayInteractable == null || !_subscribedToRayState) return;
            _canvasRayInteractable.WhenStateChanged -= OnRayStateChanged;
            _subscribedToRayState = false;
        }

        private void OnRayStateChanged(InteractableStateChangeArgs args)
        {
            if (!debugLogs) return;
            if (args.NewState == InteractableState.Hover && args.PreviousState == InteractableState.Normal)
                UDebug.Log("<color=#89CFF0><b>[MetaRayProductCardMenu]</b></color> Ray hovering card menu.");
            else if (args.NewState == InteractableState.Select)
                UDebug.Log("<color=yellow><b>[MetaRayProductCardMenu]</b></color> Ray selected on card menu.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Keyboard debug
        // ─────────────────────────────────────────────────────────────────────

        private void HandleKeyboardDebug()
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow))  NavigateLeft();
            if (Input.GetKeyDown(KeyCode.RightArrow)) NavigateRight();
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) ConfirmCurrent();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Data helpers
        // ─────────────────────────────────────────────────────────────────────

        private int GetCount() =>
            (products != null && products.Count > 0) ? products.Count : Mathf.Max(1, fallbackProductCount);

        private int GetMaxSpecCount()
        {
            int max = 4;
            if (products == null) return max;
            foreach (var p in products)
                if (p?.specs != null) max = Mathf.Max(max, p.specs.Count);
            return max;
        }

        private ProductCardData GetData(int i)
        {
            if (products == null || i < 0 || i >= products.Count) return null;
            return products[i];
        }

        private string GetName(int i)
        {
            var d = GetData(i);
            if (d != null && !string.IsNullOrWhiteSpace(d.productName)) return d.productName;
            return i switch { 0 => "Kombi 1PPAKB", 1 => "Metod VAXHULT", 2 => "Sektion AXSTAD", _ => $"Product {i + 1}" };
        }

        private string GetCategory(int i)
        {
            var d = GetData(i);
            if (d != null && !string.IsNullOrWhiteSpace(d.category)) return d.category;
            return i switch { 0 => "Kitchen combination", 1 => "Base cabinet with shelves", 2 => "Wall cabinet combination", _ => "Category" };
        }

        private Color GetImageBg(int i)
        {
            var d = GetData(i);
            if (d != null) return d.imageBgColor;
            return (i % 3) switch { 0 => fallbackImageBg0, 1 => fallbackImageBg1, _ => fallbackImageBg2 };
        }

        private Color GetAccent(int i)
        {
            var d = GetData(i);
            if (d != null) return d.accentColor;
            return (i % 3) switch { 0 => fallbackAccent0, 1 => fallbackAccent1, _ => fallbackAccent2 };
        }

        // ─────────────────────────────────────────────────────────────────────
        //  UI factory helpers
        // ─────────────────────────────────────────────────────────────────────

        private void ClearGeneratedObjects()
        {
            DestroyNamed("Generated_ProductCardMenu");
            DestroyNamed("ISDK_RayInteractionSurface");
            if (_generatedRoot  != null) { DestroyS(_generatedRoot);  _generatedRoot  = null; }
            if (_raySurfaceRoot != null) { DestroyS(_raySurfaceRoot); _raySurfaceRoot = null; }
            _canvasRayInteractable = null;
            _topPanelRT = _bottomPanelRT = _contentGroupRT = null;
            _contentCanvasGroup = null;
            _imageAreaBg = null; _productNameTMP = _categoryTMP = _specsHeaderTMP = null;
        }

        private void DestroyNamed(string n)
        {
            var child = transform.Find(n);
            if (child != null) DestroyS(child.gameObject);
        }

        private GameObject MakeUI(string n, Transform parent)
        {
            var go = new GameObject(n, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = gameObject.layer;
            return go;
        }

        private static TextMeshProUGUI AddTMP(string n, Transform parent)
        {
            var go  = new GameObject(n, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.enableAutoSizing = false;
            return tmp;
        }

        private static void StretchFull(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void StretchInset(GameObject go, float inset)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        private static void SetMinHeight(GameObject go, float h)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = h; le.flexibleHeight = 0f;
        }

        private static void SetMinWidth(GameObject go, float w)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = w; le.flexibleWidth = 0f;
        }

        private static T AddOrGet<T>(GameObject go) where T : Component =>
            go.GetComponent<T>() ?? go.AddComponent<T>();

        private static void DestroyS(GameObject go)
        {
            if (go == null) return;
            if (UApp.isPlaying) Destroy(go); else DestroyImmediate(go);
        }

        private static T FindAny<T>() where T : UnityEngine.Object
        {
#if UNITY_2022_2_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<T>();
#else
            return UnityEngine.Object.FindObjectOfType<T>();
#endif
        }
    }

    // =========================================================================
    //  Helper: Arrow hover + press scale
    // =========================================================================

    /// <summary>
    /// Attached to each arrow button.  Animates the RectTransform scale on
    /// hover and press — works with Meta PointableCanvasModule pointer events.
    /// </summary>
    public class ProductCardArrowHitbox : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform _rt;
        private float _hoverScale;
        private float _pressScale;
        private float _lerpSpeed;
        private bool  _hovered;
        private bool  _pressed;

        public void Init(RectTransform rt, float hover, float press, float speed)
        {
            _rt = rt; _hoverScale = hover; _pressScale = press; _lerpSpeed = speed;
        }

        private void Update()
        {
            if (_rt == null) return;
            float target = _pressed ? _pressScale : _hovered ? _hoverScale : 1f;
            _rt.localScale = Vector3.Lerp(_rt.localScale, Vector3.one * target, Time.deltaTime * _lerpSpeed);
        }

        public void OnPointerEnter(PointerEventData e) => _hovered = true;
        public void OnPointerExit (PointerEventData e) { _hovered = false; _pressed = false; }
        public void OnPointerDown (PointerEventData e) => _pressed = true;
        public void OnPointerUp   (PointerEventData e) => _pressed = false;
    }

    // =========================================================================
    //  Helper: Spec chip hover tint
    // =========================================================================

    /// <summary>
    /// Attached to each spec chip.  Smoothly blends the background colour
    /// between normal and hover on pointer enter / exit.
    /// </summary>
    public class ProductCardSpecChipHitbox : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        private UIImage _bg;
        private Color   _normal;
        private Color   _hover;
        private float   _t;
        private bool    _hovered;

        public void Init(UIImage bg, Color normal, Color hover)
        {
            _bg = bg; _normal = normal; _hover = hover;
        }

        private void Update()
        {
            if (_bg == null) return;
            _t = Mathf.MoveTowards(_t, _hovered ? 1f : 0f, Time.deltaTime / 0.2f);
            _bg.color = Color.Lerp(_normal, _hover, _t);
        }

        public void OnPointerEnter(PointerEventData e) => _hovered = true;
        public void OnPointerExit (PointerEventData e) => _hovered = false;
    }
}
