// =====================================================================
//  IntentSelectMenu.cs
//
//  World-space VR menu for choosing a "lifestyle intent" wired up to
//  Meta's Interaction SDK (Interactables Rig → HandRayInteractor).
//
//  USAGE
//  -----
//   1. Create an empty GameObject in your scene (e.g. "IntentMenu").
//   2. Add this component. Canvas / CanvasScaler / GraphicRaycaster /
//      RectTransform are added automatically.
//   3. Drop your IntentData ScriptableObjects into the `intents` list
//      in the inspector.
//   4. Position the GameObject in front of the player (a typical
//      starting transform: position (0, 1.4, 1.6), rotation (0, 0, 0)).
//   5. Press Play. Your existing Meta Interactables Rig will discover
//      the cards on its own — there is nothing to wire up by hand.
//
//  HOW THE ISDK WIRING WORKS
//  -------------------------
//  Each interactive element (cards + Next button) has its own
//  RayInteractable. The HandRayInteractor on each hand finds them
//  automatically through the InteractorRegistry. We listen to
//  RayInteractable.WhenStateChanged to drive Hover / Selected visuals.
//  No PointableCanvas, no PointableCanvasModule, no UGUI Button, no
//  physics colliders, no manual OVRHand pinch detection.
//
//  Per-element hierarchy:
//
//      Card_<i>                           RectTransform (layout slot)
//       ├── Visual                        RectTransform (animates)
//       │    ├── Border  (Image)
//       │    └── Content
//       │         ├── BgImage    card photo
//       │         ├── Gradient   legibility fade
//       │         ├── LabelPanel title + subtitle
//       │         └── Checkmark  ✓ on select
//       └── RayInteraction                RectTransform (stretched to card)
//            • PlaneSurface
//            • BoundsClipper
//            • RectTransformBoundsClipperDriver  (drives the clipper from the RectTransform)
//            • ClippedPlaneSurface  = PlaneSurface ∩ BoundsClipper
//            • RayInteractable      ← subscribe to WhenStateChanged
//
// =====================================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
[RequireComponent(typeof(GraphicRaycaster))]
[AddComponentMenu("VR/Intent Select Menu")]
public class IntentSelectMenu : MonoBehaviour
{
    // -----------------------------------------------------------------
    // Public configuration
    // -----------------------------------------------------------------

    [Header("Intents")]
    [Tooltip("Drag IntentData ScriptableObjects here. One card is built per entry.")]
    public List<IntentData> intents = new List<IntentData>();

    [Header("Canvas (World Space)")]
    [Tooltip("Meters per pixel. 0.001 → 1000 px = 1 m. Resize to taste.")]
    public float worldScale = 0.001f;

    [Header("Card Layout (px)")]
    public float cardWidth = 260f;
    public float cardHeight = 347f;
    public float cardSpacing = 36f;
    [Tooltip("Extra horizontal padding added to the canvas on each side.")]
    public float canvasPadX = 120f;
    [Tooltip("Extra vertical padding (for header + next button).")]
    public float canvasPadY = 220f;

    [Header("Card Visual States")]
    [Range(0.80f, 1.00f)] public float normalScale = 0.93f;
    [Range(0.90f, 1.10f)] public float hoverScale = 1.00f;
    [Range(1.00f, 1.30f)] public float selectedScale = 1.08f;
    [Tooltip("Speed of the scale/color tween. Higher = snappier.")]
    [Range(1f, 30f)] public float visualLerpSpeed = 14f;

    [Header("Border Colors")]
    public Color borderColorNormal = new Color(0.10f, 0.15f, 0.20f, 1.00f);
    public Color borderColorHover = new Color(0.55f, 0.80f, 1.00f, 0.85f);
    public Color borderColorSelected = new Color(1.00f, 1.00f, 1.00f, 0.95f);
    [Range(1f, 10f)] public float borderThickness = 3f;

    [Header("Label Panel Colors")]
    public Color labelBgNormal = new Color(0.15f, 0.20f, 0.28f, 0.55f);
    public Color labelBgHover = new Color(0.10f, 0.30f, 0.50f, 0.55f);
    public Color labelBgSelected = new Color(1.00f, 1.00f, 1.00f, 0.25f);

    [Header("Other Colors")]
    public Color gradientColor = new Color(0f, 0f, 0f, 0.65f);
    public Color headerBgColor = new Color(0f, 0f, 0f, 0.35f);
    public Color nextBtnNormal = new Color(1f, 1f, 1f, 0.40f);
    public Color nextBtnHover = new Color(1f, 1f, 1f, 0.85f);
    public Color nextBtnEnabledHover = new Color(1f, 1f, 1f, 1.00f);

    [Header("Typography (px)")]
    public float headerFontSize = 30f;
    public float titleFontSize = 19f;
    public float subtitleFontSize = 12f;
    public float nextBtnFontSize = 20f;
    public string headerText = "Choose how you want to live";

    [Header("Behavior")]
    [Tooltip("If true, pinching a card again will deselect it.")]
    public bool allowDeselect = false;

    [Header("Events")]
    public UnityEvent<IntentData> onIntentHovered;     // fires when a card enters hover
    public UnityEvent<IntentData> onIntentSelected;    // fires when a card is selected (single-click)
    public UnityEvent<IntentData> onIntentConfirmed;   // fires when Next is pressed with a selection

    [Header("Debug")]
    public bool debugLogs = true;
    public bool keyboardFallbackEnabled = true;

    // -----------------------------------------------------------------
    // Internal state
    // -----------------------------------------------------------------

    private Canvas _canvas;
    private CanvasScaler _scaler;
    private RectTransform _rt;

    [HideInInspector, SerializeField] private GameObject _uiRoot;
    [SerializeField, HideInInspector] private List<CardUI> _cards = new List<CardUI>();
    [NonSerialized] private NextButtonUI _nextButton;

    [NonSerialized] private int _selectedIndex = -1;
    [NonSerialized] private bool _subscribed = false;

    // We keep all WhenStateChanged handlers here so we can unsubscribe
    // cleanly on rebuild / disable.
    [NonSerialized]
    private readonly Dictionary<RayInteractable, Action<InteractableStateChangeArgs>>
        _stateHandlers = new Dictionary<RayInteractable, Action<InteractableStateChangeArgs>>();

    // -----------------------------------------------------------------
    // Per-element descriptors
    // -----------------------------------------------------------------

    [Serializable]
    private class CardUI
    {
        public int index;
        public IntentData data;
        public GameObject root;
        public GameObject visual;
        public Image borderImg;
        public Image bgImage;
        public Image gradOverlay;
        public Image labelBg;
        public TextMeshProUGUI titleTMP;
        public TextMeshProUGUI subtitleTMP;
        public GameObject checkmark;
        public RayInteractable rayInteractable;

        // runtime-only — do NOT serialize
        [NonSerialized] public bool isHovered;
        [NonSerialized] public bool isSelected;
        [NonSerialized] public Vector3 currentLocalScale;
        [NonSerialized] public Color currentBorderColor;
        [NonSerialized] public Color currentLabelColor;
    }

    private class NextButtonUI
    {
        public GameObject root;
        public GameObject visual;
        public Image bgImage;
        public TextMeshProUGUI labelTMP;
        public RayInteractable rayInteractable;

        public bool isHovered;
        public Color currentColor;
    }

    // =================================================================
    // UNITY LIFECYCLE
    // =================================================================

    void Awake()
    {
        GrabComponents();
        ConfigureCanvas();
        EnsureEventSystem();
    }

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (_uiRoot == null || _cards.Count == 0) RebuildUI();
        SubscribeAllInteractables();
    }

    void OnDisable()
    {
        UnsubscribeAllInteractables();
    }

    void Start()
    {
        if (Application.isPlaying)
        {
            // First-time build at runtime. RebuildUI handles re-subscription.
            RebuildUI();
        }
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        UpdateVisuals(Time.deltaTime);
        if (keyboardFallbackEnabled) HandleKeyboardFallback();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Defer to avoid running during serialization.
        EditorApplication.delayCall += () =>
        {
            if (this == null || gameObject == null) return;
            GrabComponents();
            ConfigureCanvas();
            if (Application.isPlaying) return;
            // Editor-time live preview
            RebuildUI();
        };
    }
#endif

    // =================================================================
    // CANVAS / EVENT SYSTEM
    // =================================================================

    void GrabComponents()
    {
        _canvas = GetComponent<Canvas>();
        _scaler = GetComponent<CanvasScaler>();
        _rt = GetComponent<RectTransform>();
    }

    void ConfigureCanvas()
    {
        if (_canvas == null) return;
        _canvas.renderMode = RenderMode.WorldSpace;
        transform.localScale = Vector3.one * Mathf.Max(0.0001f, worldScale);

        int n = (intents != null) ? intents.Count : 0;
        float totalW = n * cardWidth + Mathf.Max(0, n - 1) * cardSpacing + canvasPadX * 2f;
        float totalH = cardHeight + canvasPadY;
        if (_rt == null) _rt = GetComponent<RectTransform>();
        _rt.sizeDelta = new Vector2(Mathf.Max(totalW, cardWidth + canvasPadX * 2f), totalH);
        _rt.pivot = new Vector2(0.5f, 0.5f);
    }

    /// <summary>
    /// ISDK doesn't strictly need an EventSystem for direct RayInteractable
    /// subscriptions, but creating one prevents warnings and keeps any other
    /// UGUI in your scene functional.
    /// </summary>
    void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        if (FindAnyObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();

        // Try to add the ISDK PointableCanvasModule via reflection so this
        // script compiles even if your project doesn't include that file.
        var moduleType = Type.GetType("Oculus.Interaction.PointableCanvasModule, Oculus.Interaction");
        if (moduleType != null) go.AddComponent(moduleType);
        else go.AddComponent<StandaloneInputModule>();
    }

    // =================================================================
    // BUILD UI
    // =================================================================

    [ContextMenu("Rebuild UI")]
    public void RebuildUI()
    {
        UnsubscribeAllInteractables();
        ClearUI();
        ConfigureCanvas();

        if (intents == null || intents.Count == 0)
        {
            if (debugLogs) Debug.Log("[IntentSelectMenu] No intents — nothing to build.");
            return;
        }

        _uiRoot = MakeChild("IntentUI", transform);
        StretchFull(_uiRoot);

        BuildHeader(_uiRoot.transform);
        BuildCardsRow(_uiRoot.transform);
        BuildNextButton(_uiRoot.transform);

        // Initialize live visual state
        foreach (var c in _cards)
        {
            if (c == null || c.visual == null) continue;
            c.currentLocalScale = Vector3.one * normalScale;
            c.currentBorderColor = borderColorNormal;
            c.currentLabelColor = labelBgNormal;
            c.visual.transform.localScale = c.currentLocalScale;
            if (c.borderImg) c.borderImg.color = c.currentBorderColor;
            if (c.labelBg) c.labelBg.color = c.currentLabelColor;
            if (c.checkmark) c.checkmark.SetActive(false);
        }

        if (Application.isPlaying) SubscribeAllInteractables();

        if (debugLogs) Debug.Log($"[IntentSelectMenu] Built {_cards.Count} card(s) + Next button.");
    }

    void ClearUI()
    {
        _cards.Clear();
        _nextButton = null;
        _selectedIndex = -1;

        if (_uiRoot != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(_uiRoot);
            else                        Destroy(_uiRoot);
#else
            Destroy(_uiRoot);
#endif
            _uiRoot = null;
        }

        // Also nuke any orphaned IntentUI children — useful after script reloads.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var ch = transform.GetChild(i);
            if (ch && ch.name == "IntentUI")
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(ch.gameObject);
                else                        Destroy(ch.gameObject);
#else
                Destroy(ch.gameObject);
#endif
            }
        }
    }

    // -----------------------------------------------------------------
    // Header
    // -----------------------------------------------------------------

    void BuildHeader(Transform parent)
    {
        var go = MakeChild("Header", parent);
        var rt = RT(go);
        rt.anchorMin = new Vector2(0.05f, 1f);
        rt.anchorMax = new Vector2(0.95f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -28f);
        rt.sizeDelta = new Vector2(0f, 52f);

        var bg = go.AddComponent<Image>();
        bg.color = headerBgColor;
        bg.raycastTarget = false;

        var tmp = AddTMP("Title", go.transform);
        StretchFull(tmp.gameObject);
        tmp.text = string.IsNullOrEmpty(headerText) ? "Choose how you want to live" : headerText;
        tmp.fontSize = headerFontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
    }

    // -----------------------------------------------------------------
    // Cards
    // -----------------------------------------------------------------

    void BuildCardsRow(Transform parent)
    {
        var go = MakeChild("CardsRow", parent);
        var rt = RT(go);

        int n = intents.Count;
        float rowW = n * cardWidth + Mathf.Max(0, n - 1) * cardSpacing;

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -10f);
        rt.sizeDelta = new Vector2(rowW, cardHeight);

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = cardSpacing;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        for (int i = 0; i < intents.Count; i++)
        {
            if (intents[i] == null) continue;
            BuildCard(go.transform, i, intents[i]);
        }
    }

    void BuildCard(Transform parent, int index, IntentData data)
    {
        var ui = new CardUI { index = index, data = data };

        // ─── Card root: fixed-size layout slot (NEVER scaled) ─────────
        ui.root = MakeChild($"Card_{index}_{SafeName(data)}", parent);
        var rootRT = RT(ui.root);
        rootRT.sizeDelta = new Vector2(cardWidth, cardHeight);

        var le = ui.root.AddComponent<LayoutElement>();
        le.minWidth = cardWidth;
        le.minHeight = cardHeight;
        le.preferredWidth = cardWidth;
        le.preferredHeight = cardHeight;

        // ─── Visual child: animates on hover/select ───────────────────
        // Pivot at bottom-center so scale grows upward, not from middle —
        // matches typical card-picker UX where the card "lifts".
        ui.visual = MakeChild("Visual", ui.root.transform);
        var visRT = RT(ui.visual);
        visRT.pivot = new Vector2(0.5f, 0f);
        visRT.anchorMin = new Vector2(0.5f, 0f);
        visRT.anchorMax = new Vector2(0.5f, 0f);
        visRT.sizeDelta = new Vector2(cardWidth, cardHeight);
        visRT.anchoredPosition = Vector2.zero;

        // Border (just a flat-color image; content sits inset inside)
        ui.borderImg = ui.visual.AddComponent<Image>();
        ui.borderImg.color = borderColorNormal;
        ui.borderImg.raycastTarget = false;

        var content = MakeChild("Content", ui.visual.transform);
        StretchInset(content, borderThickness);

        // Background image
        var bgGO = MakeChild("BgImage", content.transform);
        StretchFull(bgGO);
        ui.bgImage = bgGO.AddComponent<Image>();
        ui.bgImage.preserveAspect = false;
        ui.bgImage.raycastTarget = false;
        if (data != null && data.cardImage != null)
        {
            ui.bgImage.sprite = data.cardImage;
            ui.bgImage.type = Image.Type.Simple;
        }
        else
        {
            ui.bgImage.color = new Color(0.20f, 0.25f, 0.35f, 1f);
        }

        // Bottom gradient for label legibility
        var gradGO = MakeChild("GradientOverlay", content.transform);
        var gradRT = RT(gradGO);
        gradRT.anchorMin = new Vector2(0f, 0f);
        gradRT.anchorMax = new Vector2(1f, 0.65f);
        gradRT.offsetMin = Vector2.zero;
        gradRT.offsetMax = Vector2.zero;
        ui.gradOverlay = gradGO.AddComponent<Image>();
        ui.gradOverlay.color = gradientColor;
        ui.gradOverlay.raycastTarget = false;

        // Label panel (frosted-glass style)
        var labelGO = MakeChild("LabelPanel", content.transform);
        var labelRT = RT(labelGO);
        labelRT.anchorMin = new Vector2(0.05f, 0f);
        labelRT.anchorMax = new Vector2(0.95f, 0f);
        labelRT.pivot = new Vector2(0.5f, 0f);
        labelRT.anchoredPosition = new Vector2(0f, 12f);
        labelRT.sizeDelta = new Vector2(0f, cardHeight * 0.36f);
        ui.labelBg = labelGO.AddComponent<Image>();
        ui.labelBg.color = labelBgNormal;
        ui.labelBg.raycastTarget = false;

        var vlg = labelGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 10, 10);
        vlg.spacing = 4f;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlHeight = false;
        vlg.childAlignment = TextAnchor.UpperLeft;

        // Title
        ui.titleTMP = AddTMP("Title", labelGO.transform);
        ui.titleTMP.text = data != null ? data.title : "(no title)";
        ui.titleTMP.fontSize = titleFontSize;
        ui.titleTMP.fontStyle = FontStyles.Bold;
        ui.titleTMP.color = Color.white;
        ui.titleTMP.enableWordWrapping = true;
        ui.titleTMP.overflowMode = TextOverflowModes.Ellipsis;
        ui.titleTMP.raycastTarget = false;
        SetLayoutHeight(ui.titleTMP.gameObject, titleFontSize * 1.4f);

        // Subtitle
        ui.subtitleTMP = AddTMP("Subtitle", labelGO.transform);
        ui.subtitleTMP.text = data != null ? data.subtitle : "";
        ui.subtitleTMP.fontSize = subtitleFontSize;
        ui.subtitleTMP.color = new Color(1f, 1f, 1f, 0.78f);
        ui.subtitleTMP.enableWordWrapping = true;
        ui.subtitleTMP.overflowMode = TextOverflowModes.Ellipsis;
        ui.subtitleTMP.raycastTarget = false;
        SetLayoutHeight(ui.subtitleTMP.gameObject, subtitleFontSize * 2.8f);

        // Checkmark badge (top-right)
        var ckGO = MakeChild("Checkmark", content.transform);
        var ckRT = RT(ckGO);
        ckRT.anchorMin = new Vector2(1f, 1f);
        ckRT.anchorMax = new Vector2(1f, 1f);
        ckRT.pivot = new Vector2(1f, 1f);
        ckRT.anchoredPosition = new Vector2(-10f, -10f);
        ckRT.sizeDelta = new Vector2(28f, 28f);
        var ckBg = ckGO.AddComponent<Image>();
        ckBg.color = Color.white;
        ckBg.raycastTarget = false;

        var ckTMP = AddTMP("Check", ckGO.transform);
        StretchFull(ckTMP.gameObject);
        ckTMP.text = "\u2713";   // ✓
        ckTMP.fontSize = 16f;
        ckTMP.color = new Color(0.10f, 0.35f, 0.85f, 1f);
        ckTMP.alignment = TextAlignmentOptions.Center;
        ckTMP.fontStyle = FontStyles.Bold;
        ckTMP.raycastTarget = false;
        ui.checkmark = ckGO;
        ckGO.SetActive(false);

        ui.visual.transform.localScale = Vector3.one * normalScale;

        // ─── ISDK RayInteraction (sibling of Visual on the card root) ─
        BuildRayInteraction(ui.root.transform, "RayInteraction",
                            cardWidth, cardHeight,
                            out var cardInteractable);
        ui.rayInteractable = cardInteractable;

        _cards.Add(ui);
    }

    // -----------------------------------------------------------------
    // Next button
    // -----------------------------------------------------------------

    void BuildNextButton(Transform parent)
    {
        _nextButton = new NextButtonUI();

        // Layout slot (fixed)
        _nextButton.root = MakeChild("NextButton", parent);
        var rootRT = RT(_nextButton.root);
        rootRT.anchorMin = new Vector2(0.5f, 0f);
        rootRT.anchorMax = new Vector2(0.5f, 0f);
        rootRT.pivot = new Vector2(0.5f, 0f);
        rootRT.anchoredPosition = new Vector2(0f, 28f);
        rootRT.sizeDelta = new Vector2(210f, 54f);

        // Visual that animates
        _nextButton.visual = MakeChild("Visual", _nextButton.root.transform);
        StretchFull(_nextButton.visual);

        _nextButton.bgImage = _nextButton.visual.AddComponent<Image>();
        _nextButton.bgImage.color = nextBtnNormal;
        _nextButton.bgImage.raycastTarget = false;
        _nextButton.currentColor = nextBtnNormal;

        _nextButton.labelTMP = AddTMP("NextLabel", _nextButton.visual.transform);
        StretchFull(_nextButton.labelTMP.gameObject);
        _nextButton.labelTMP.text = "Next";
        _nextButton.labelTMP.fontSize = nextBtnFontSize;
        _nextButton.labelTMP.color = new Color(0.08f, 0.08f, 0.12f, 1f);
        _nextButton.labelTMP.alignment = TextAlignmentOptions.Center;
        _nextButton.labelTMP.raycastTarget = false;

        // ISDK
        BuildRayInteraction(_nextButton.root.transform, "RayInteraction",
                            210f, 54f,
                            out var btnInteractable);
        _nextButton.rayInteractable = btnInteractable;
    }

    // -----------------------------------------------------------------
    // Generic ISDK RayInteraction builder
    //
    // Creates a child GameObject stretched over the parent rect that
    // owns the full PlaneSurface / BoundsClipper / ClippedPlaneSurface
    // / RayInteractable stack. The clipper is driven from this same
    // RectTransform so the bounds always match the visible rect — even
    // if you change cardWidth/cardHeight at runtime.
    // -----------------------------------------------------------------

    void BuildRayInteraction(Transform parent,
                             string childName,
                             float width,
                             float height,
                             out RayInteractable interactable)
    {
        var go = MakeChild(childName, parent);
        StretchFull(go);

        // Don't let LayoutGroups push it around — it's an ISDK helper, not visual.
        var le = go.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        // Make sure size is locked (StretchFull already does this, but be defensive)
        var rt = RT(go);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // 1) PlaneSurface — infinite plane along the GO's local Z=0.
        //    NormalFacing.Backward = surface normal points along -Z, which
        //    is towards the user when the canvas faces forward (default).
        var planeSurface = go.AddComponent<PlaneSurface>();
        planeSurface.Facing = PlaneSurface.NormalFacing.Backward;

        // 2) BoundsClipper — owns the volume that limits the surface
        //    (so a ray hitting outside the rect doesn't register).
        var clipper = go.AddComponent<BoundsClipper>();

        // 3) Driver — auto-syncs the clipper to this RectTransform.
        //    `_boundsClipper` is private with no public Inject method in
        //    most ISDK versions, so we set it via reflection.
        var driver = go.AddComponent<RectTransformBoundsClipperDriver>();
        WireClipperDriver(driver, clipper);

        // 4) ClippedPlaneSurface — composes plane × clipper.
        var clipped = go.AddComponent<ClippedPlaneSurface>();
        clipped.InjectAllClippedPlaneSurface(planeSurface, new List<IBoundsClipper> { clipper });

        // 5) RayInteractable — what the HandRayInteractor talks to.
        //    Surface = the clipped surface (used for both hover + select).
        //    We DO NOT inject an optional select surface, because we want
        //    selection to cancel if the user drags off the card.
        interactable = go.AddComponent<RayInteractable>();
        interactable.InjectAllRayInteractable(clipped);
    }

    /// <summary>
    /// RectTransformBoundsClipperDriver typically exposes an `InjectAll…`
    /// method on newer ISDK versions and a private `_boundsClipper` field
    /// on older ones. Try the public path first, fall back to reflection.
    /// </summary>
    static void WireClipperDriver(RectTransformBoundsClipperDriver driver, BoundsClipper clipper)
    {
        if (driver == null || clipper == null) return;

        var t = typeof(RectTransformBoundsClipperDriver);
        const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Try a few likely public method names.
        string[] candidates = {
            "InjectAllRectTransformBoundsClipperDriver",
            "InjectBoundsClipper",
            "InjectAll"
        };
        foreach (var name in candidates)
        {
            var m = t.GetMethod(name, FLAGS, null, new[] { typeof(BoundsClipper) }, null);
            if (m != null)
            {
                m.Invoke(driver, new object[] { clipper });
                return;
            }
        }

        // Reflection fallback: set the private field directly.
        var f = t.GetField("_boundsClipper", FLAGS);
        if (f != null) f.SetValue(driver, clipper);
        else if (Application.isEditor)
            Debug.LogWarning("[IntentSelectMenu] Could not wire RectTransformBoundsClipperDriver to its BoundsClipper. " +
                             "Hover hit-testing may be miss-bounded.");
    }

    // =================================================================
    // ISDK SUBSCRIPTIONS
    // =================================================================

    void SubscribeAllInteractables()
    {
        if (_subscribed) return;

        // Cards
        for (int i = 0; i < _cards.Count; i++)
        {
            var card = _cards[i];
            if (card == null || card.rayInteractable == null) continue;
            int captured = i;

            Action<InteractableStateChangeArgs> handler = (args) => OnCardStateChanged(captured, args);
            card.rayInteractable.WhenStateChanged += handler;
            _stateHandlers[card.rayInteractable] = handler;
        }

        // Next button
        if (_nextButton != null && _nextButton.rayInteractable != null)
        {
            Action<InteractableStateChangeArgs> handler = OnNextButtonStateChanged;
            _nextButton.rayInteractable.WhenStateChanged += handler;
            _stateHandlers[_nextButton.rayInteractable] = handler;
        }

        _subscribed = true;
        if (debugLogs) Debug.Log($"[IntentSelectMenu] Subscribed {_stateHandlers.Count} RayInteractable(s).");
    }

    void UnsubscribeAllInteractables()
    {
        foreach (var kv in _stateHandlers)
        {
            if (kv.Key != null && kv.Value != null)
                kv.Key.WhenStateChanged -= kv.Value;
        }
        _stateHandlers.Clear();
        _subscribed = false;
    }

    // -----------------------------------------------------------------
    // State change handlers
    // -----------------------------------------------------------------

    void OnCardStateChanged(int cardIndex, InteractableStateChangeArgs args)
    {
        if (cardIndex < 0 || cardIndex >= _cards.Count) return;
        var card = _cards[cardIndex];
        if (card == null) return;

        // Hover enter
        if (args.NewState == InteractableState.Hover &&
            args.PreviousState == InteractableState.Normal)
        {
            card.isHovered = true;
            onIntentHovered?.Invoke(card.data);
            if (debugLogs) Debug.Log($"<color=cyan>[ISDK]</color> Hover ▶ Card {cardIndex} ({Title(card.data)})");
        }
        // Hover exit (back to Normal)
        else if (args.NewState == InteractableState.Normal &&
                 args.PreviousState == InteractableState.Hover)
        {
            card.isHovered = false;
        }
        // Pinch (Hover → Select)
        else if (args.NewState == InteractableState.Select &&
                 args.PreviousState == InteractableState.Hover)
        {
            // Deselection toggle (optional)
            if (allowDeselect && _selectedIndex == cardIndex)
            {
                ApplySelection(-1);
                if (debugLogs) Debug.Log($"<color=orange>[ISDK]</color> Deselected Card {cardIndex}");
            }
            else
            {
                ApplySelection(cardIndex);
                if (debugLogs) Debug.Log($"<color=yellow>[ISDK]</color> Selected ▶ Card {cardIndex} ({Title(card.data)})");
            }
        }
        // Pinch released back into Hover — we don't act here; selection is "sticky".
        else if (args.NewState == InteractableState.Hover &&
                 args.PreviousState == InteractableState.Select)
        {
            card.isHovered = true;
        }
        // Pinch released straight to Normal (drag-off cancel) — also nothing to do
        // because we never consumed a selection on press-down beyond marking it.
    }

    void OnNextButtonStateChanged(InteractableStateChangeArgs args)
    {
        if (_nextButton == null) return;

        if (args.NewState == InteractableState.Hover &&
            args.PreviousState == InteractableState.Normal)
        {
            _nextButton.isHovered = true;
            if (debugLogs) Debug.Log("<color=cyan>[ISDK]</color> Hover ▶ Next");
        }
        else if (args.NewState == InteractableState.Normal &&
                 args.PreviousState == InteractableState.Hover)
        {
            _nextButton.isHovered = false;
        }
        else if (args.NewState == InteractableState.Select &&
                 args.PreviousState == InteractableState.Hover)
        {
            ConfirmSelection();
        }
        else if (args.NewState == InteractableState.Hover &&
                 args.PreviousState == InteractableState.Select)
        {
            _nextButton.isHovered = true;
        }
    }

    // =================================================================
    // SELECTION / CONFIRM
    // =================================================================

    void ApplySelection(int newIndex)
    {
        // Clamp
        if (newIndex < -1 || newIndex >= _cards.Count) return;

        // Update flags on every card
        for (int i = 0; i < _cards.Count; i++)
        {
            if (_cards[i] != null) _cards[i].isSelected = (i == newIndex);
        }
        _selectedIndex = newIndex;

        // Fire event with the chosen data (or null if cleared)
        IntentData chosen = (newIndex >= 0 && newIndex < intents.Count) ? intents[newIndex] : null;
        onIntentSelected?.Invoke(chosen);
    }

    public void ConfirmSelection()
    {
        if (_selectedIndex < 0 || _selectedIndex >= intents.Count)
        {
            if (debugLogs) Debug.LogWarning("[IntentSelectMenu] Confirm pressed but nothing is selected.");
            return;
        }

        var chosen = intents[_selectedIndex];
        Debug.Log($"<color=lime><b>[IntentSelectMenu]</b></color> ✅ Confirmed: \"{chosen.title}\"  —  \"{chosen.subtitle}\"");
        onIntentConfirmed?.Invoke(chosen);
    }

    // =================================================================
    // VISUAL ANIMATION
    // =================================================================

    void UpdateVisuals(float dt)
    {
        float k = 1f - Mathf.Exp(-visualLerpSpeed * dt);  // frame-rate independent lerp

        // Cards
        for (int i = 0; i < _cards.Count; i++)
        {
            var c = _cards[i];
            if (c == null || c.visual == null) continue;

            // Priority: selected > hovered > normal
            float targetScale;
            Color targetBorder;
            Color targetLabel;

            if (c.isSelected) { targetScale = selectedScale; targetBorder = borderColorSelected; targetLabel = labelBgSelected; }
            else if (c.isHovered) { targetScale = hoverScale; targetBorder = borderColorHover; targetLabel = labelBgHover; }
            else { targetScale = normalScale; targetBorder = borderColorNormal; targetLabel = labelBgNormal; }

            c.currentLocalScale = Vector3.Lerp(c.currentLocalScale, Vector3.one * targetScale, k);
            c.currentBorderColor = Color.Lerp(c.currentBorderColor, targetBorder, k);
            c.currentLabelColor = Color.Lerp(c.currentLabelColor, targetLabel, k);

            c.visual.transform.localScale = c.currentLocalScale;
            if (c.borderImg) c.borderImg.color = c.currentBorderColor;
            if (c.labelBg) c.labelBg.color = c.currentLabelColor;
            if (c.checkmark && c.checkmark.activeSelf != c.isSelected) c.checkmark.SetActive(c.isSelected);
        }

        // Next button
        if (_nextButton != null && _nextButton.bgImage != null)
        {
            bool hasSelection = _selectedIndex >= 0;
            Color target;
            if (_nextButton.isHovered) target = hasSelection ? nextBtnEnabledHover : nextBtnHover;
            else target = hasSelection ? nextBtnHover : nextBtnNormal;

            _nextButton.currentColor = Color.Lerp(_nextButton.currentColor, target, k);
            _nextButton.bgImage.color = _nextButton.currentColor;
        }
    }

    // =================================================================
    // KEYBOARD FALLBACK (editor / desktop testing)
    // =================================================================

    void HandleKeyboardFallback()
    {
        if (_cards.Count == 0) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            int next = (_selectedIndex < 0) ? 0 : Mathf.Max(0, _selectedIndex - 1);
            ApplySelection(next);
            if (debugLogs) Debug.Log($"[IntentSelectMenu] (kbd) ← Card {next}: {Title(intents[next])}");
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            int next = (_selectedIndex < 0) ? 0 : Mathf.Min(_cards.Count - 1, _selectedIndex + 1);
            ApplySelection(next);
            if (debugLogs) Debug.Log($"[IntentSelectMenu] (kbd) → Card {next}: {Title(intents[next])}");
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            ConfirmSelection();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (allowDeselect) ApplySelection(-1);
        }
    }

    // =================================================================
    // PUBLIC API
    // =================================================================

    /// <summary> Programmatically select a card by index. </summary>
    public void SelectIndex(int index)
    {
        if (index < 0 || index >= _cards.Count)
        {
            if (debugLogs) Debug.LogWarning($"[IntentSelectMenu] SelectIndex({index}) out of range.");
            return;
        }
        ApplySelection(index);
    }

    /// <summary> Programmatically clear any selection. </summary>
    public void ClearSelection() => ApplySelection(-1);

    /// <summary> The currently selected IntentData, or null. </summary>
    public IntentData CurrentSelection
        => (_selectedIndex >= 0 && _selectedIndex < intents.Count) ? intents[_selectedIndex] : null;

    /// <summary> The currently selected card index, or -1. </summary>
    public int CurrentSelectedIndex => _selectedIndex;

    // =================================================================
    // Helpers
    // =================================================================

    static GameObject MakeChild(string goName, Transform parent)
    {
        var go = new GameObject(goName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static RectTransform RT(GameObject go) => (RectTransform)go.transform;

    static void StretchFull(GameObject go)
    {
        var rt = RT(go);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void StretchInset(GameObject go, float inset)
    {
        var rt = RT(go);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static TextMeshProUGUI AddTMP(string goName, Transform parent)
    {
        var go = new GameObject(goName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.AddComponent<TextMeshProUGUI>();
    }

    static void SetLayoutHeight(GameObject go, float height)
    {
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
    }

    static string SafeName(IntentData d)
    {
        if (d == null) return "null";
        return string.IsNullOrEmpty(d.name) ? (string.IsNullOrEmpty(d.title) ? "Intent" : d.title) : d.name;
    }

    static string Title(IntentData d)
    {
        if (d == null) return "(null)";
        return string.IsNullOrEmpty(d.title) ? d.name : d.title;
    }
}