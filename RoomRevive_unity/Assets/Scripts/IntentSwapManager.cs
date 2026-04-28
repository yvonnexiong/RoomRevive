using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Manages the "Choose how you want to live" lifestyle selection UI.
///
/// Setup:
///   1. Add this component to an empty GameObject.
///   2. Add IntentData ScriptableObjects to the Intents list.
///   3. Right-click the component and select "Generate UI Cards".
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
[RequireComponent(typeof(GraphicRaycaster))]
public class IntentSwapManager : MonoBehaviour
{
    // ── Intent Data ──────────────────────────────────────────────────────────
    [Header("Intents")]
    [Tooltip("Drag IntentData ScriptableObjects here.")]
    public List<IntentData> intents = new List<IntentData>();

    // ── Canvas ───────────────────────────────────────────────────────────────
    [Header("Canvas (World Space)")]
    [Tooltip("Meters per pixel. 0.001 → 1000px = 1m. Resize to taste.")]
    public float worldScale = 0.001f;

    // ── Card Layout ──────────────────────────────────────────────────────────
    [Header("Card Layout")]
    public float cardWidth   = 260f;
    public float cardHeight  = 347f;    
    public float cardSpacing = 36f;
    [Tooltip("Extra horizontal padding added to the canvas on each side.")]
    public float canvasPadX  = 120f;
    [Tooltip("Extra vertical padding (for header + next button).")]
    public float canvasPadY  = 200f;

    // ── Card Scale ───────────────────────────────────────────────────────────
    [Header("Card Scale")]
    [Range(0.80f, 0.99f)] public float deselectedScale = 0.93f;
    [Range(1.00f, 1.25f)] public float selectedScale   = 1.08f;

    // ── Colors ───────────────────────────────────────────────────────────────
    [Header("Colors")]
    public Color borderColorSelected   = new Color(1f,  1f,  1f,  0.85f);
    public Color borderColorUnselected = new Color(0.1f, 0.15f, 0.2f, 1f);
    [Range(1f, 10f)] public float borderThickness = 3f;

    public Color labelBgSelected   = new Color(1f,  1f,  1f,  0.25f);
    public Color labelBgUnselected = new Color(0.15f, 0.2f, 0.28f, 0.55f);

    public Color gradientColor = new Color(0f, 0f, 0f, 0.65f);
    public Color headerBgColor = new Color(0f, 0f, 0f, 0.35f);
    public Color nextBtnColor  = new Color(1f, 1f, 1f, 0.88f);

    // ── Typography ───────────────────────────────────────────────────────────
    [Header("Typography")]
    public float headerFontSize   = 30f;
    public float titleFontSize    = 19f;
    public float subtitleFontSize = 12f;
    public float nextBtnFontSize  = 20f;

    // ── VR Input ─────────────────────────────────────────────────────────────
    [Header("VR Input")]
    [Tooltip("Left OVRHand for pinch detection. Auto-found if left blank.")]
    public OVRHand   leftHand;
    [Tooltip("Ray origin for the left hand (HandRayInteractor). Auto-found if left blank.")]
    public Transform leftRayOrigin;

    [Tooltip("Right OVRHand for pinch detection. Auto-found if left blank.")]
    public OVRHand   rightHand;
    [Tooltip("Ray origin for the right hand (HandRayInteractor). Auto-found if left blank.")]
    public Transform rightRayOrigin;

    public float     rayDistance = 5f;
    public LayerMask interactableLayer = -1;

    // ── Debug ─────────────────────────────────────────────────────────────────
    [Header("Debug")]
    public bool debugLogs = true;

    // ── Serialized internal refs (survive domain reload) ─────────────────────
    [HideInInspector, SerializeField] private GameObject _uiRoot;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private int  _selectedIndex   = -1;
    private bool _wasLeftPinching;
    private bool _wasRightPinching;

    private Canvas       _canvas;
    private CanvasScaler _scaler;

    [SerializeField, HideInInspector]
    private List<CardUI> _cardUIs = new List<CardUI>();

    // ─────────────────────────────────────────────────────────────────────────
    // Card UI descriptor
    // ─────────────────────────────────────────────────────────────────────────
    [System.Serializable]
    private class CardUI
    {
        public GameObject       root;             // fixed-size layout slot — never scaled
        public GameObject       visual;           // child of root — scales on select (pivot bottom-center)
        public Image            borderImg;        // border color on visual
        public Image            bgImage;          // card photo
        public Image            gradOverlay;      // dark overlay at bottom
        public Image            labelBg;          // frosted-glass label panel
        public TextMeshProUGUI  titleTMP;
        public TextMeshProUGUI  subtitleTMP;
        public GameObject       checkmark;        // ✓ circle top-right
        public RayInteractable  rayInteractable;  // NATIVE ISDK Interaction component
        public int              index;
        public IntentData       data;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        GrabComponents();
        ConfigureCanvas();
        AutoFindVRReferences();
    }

    void Start()
    {
        if (Application.isPlaying)
        {
            RebuildUI();
            SubscribeToInteractables();
        }
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        HandleKeyboard();
        HandleOVRInput();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        EditorApplication.delayCall += () =>
        {
            if (this == null || gameObject == null) return;
            GrabComponents();
            ConfigureCanvas();
            AutoFindVRReferences();
            RebuildUI();
        };
    }
#endif

    // ─────────────────────────────────────────────────────────────────────────
    // Canvas configuration
    // ─────────────────────────────────────────────────────────────────────────
    void GrabComponents()
    {
        _canvas = GetComponent<Canvas>();
        _scaler = GetComponent<CanvasScaler>();
    }

    void AutoFindVRReferences()
    {
        bool anyMissing = leftHand == null || leftRayOrigin == null
                       || rightHand == null || rightRayOrigin == null;
        if (!anyMissing) return;

        var allHands = FindObjectsByType<OVRHand>(FindObjectsSortMode.None);
        foreach (var h in allHands)
        {
            string path = GetTransformPath(h.transform).ToLower();
            if (leftHand  == null && path.Contains("left"))  leftHand  = h;
            if (rightHand == null && path.Contains("right")) rightHand = h;
        }

        if (leftRayOrigin  == null) leftRayOrigin  = FindHandRayInteractor("LeftInteractions");
        if (rightRayOrigin == null) rightRayOrigin = FindHandRayInteractor("RightInteractions");
    }

    static Transform FindHandRayInteractor(string parentName)
    {
        var allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        Transform parent  = null;
        foreach (var t in allTransforms)
        {
            if (t.name == parentName) { parent = t; break; }
        }
        return parent != null ? FindInChildren(parent, "HandRayInteractor") : null;
    }

    static Transform FindInChildren(Transform root, string childName)
    {
        foreach (Transform child in root)
        {
            if (child.name == childName) return child;
            var found = FindInChildren(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    static string GetTransformPath(Transform t)
    {
        var path = t.name;
        while (t.parent != null)
        {
            t    = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    void ConfigureCanvas()
    {
        if (_canvas == null) return;

        _canvas.renderMode   = RenderMode.WorldSpace;
        transform.localScale = Vector3.one * worldScale;

        int   n      = (intents != null) ? intents.Count : 0;
        float totalW = n * cardWidth + Mathf.Max(0, n - 1) * cardSpacing + canvasPadX * 2f;
        float totalH = cardHeight + canvasPadY;

        var rt       = GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(totalW, totalH);
        rt.pivot     = new Vector2(0.5f, 0.5f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UI Build
    // ─────────────────────────────────────────────────────────────────────────
    [ContextMenu("Generate UI Cards")]
    public void RebuildUI()
    {
        ConfigureCanvas();
        ClearUI();

        if (intents == null || intents.Count == 0)
        {
            if (debugLogs) Debug.Log("[IntentSwapManager] No intents assigned — nothing to build.");
            return;
        }

        _uiRoot = MakeGO("IntentUI", transform);
        StretchFull(_uiRoot);

        BuildHeader(_uiRoot.transform);
        BuildCardsRow(_uiRoot.transform);
        BuildNextButton(_uiRoot.transform);

        if (debugLogs) Debug.Log($"[IntentSwapManager] Built {_cardUIs.Count} intent cards.");
    }

    void ClearUI()
    {
        _cardUIs.Clear();
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
    }

    void BuildHeader(Transform parent)
    {
        var go = MakeGO("Header", parent);
        var rt = RT(go);
        rt.anchorMin        = new Vector2(0.05f, 1f);
        rt.anchorMax        = new Vector2(0.95f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -28f);
        rt.sizeDelta        = new Vector2(0f, 52f);

        var bg   = go.AddComponent<Image>();
        bg.color = headerBgColor;

        var tmp = AddTMP("Title", go.transform);
        StretchFull(tmp.gameObject);
        tmp.text      = "Choose how you want to live";
        tmp.fontSize  = headerFontSize;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Normal;
    }

    void BuildCardsRow(Transform parent)
    {
        var go = MakeGO("CardsRow", parent);
        var rt = RT(go);

        int   n    = intents.Count;
        float rowW = n * cardWidth + (n - 1) * cardSpacing;

        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -10f); 
        rt.sizeDelta        = new Vector2(rowW, cardHeight);

        var hlg                    = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = cardSpacing;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.childAlignment         = TextAnchor.MiddleCenter;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;

        for (int i = 0; i < intents.Count; i++)
        {
            if (intents[i] == null) continue;
            BuildCard(go.transform, i, intents[i]);
        }
    }

    void BuildCard(Transform parent, int index, IntentData data)
    {
        var ui = new CardUI { index = index, data = data };

        // ── Root — Fixed layout slot ──────────────────────────────────────────
        ui.root          = MakeGO($"Card_{index}_{data.name}", parent);
        var rootRT       = RT(ui.root);
        rootRT.sizeDelta = new Vector2(cardWidth, cardHeight);

        var le             = ui.root.AddComponent<LayoutElement>();
        le.minWidth        = cardWidth;
        le.minHeight       = cardHeight;
        le.preferredWidth  = cardWidth;
        le.preferredHeight = cardHeight;

        // Fallback BoxCollider for Physics Raycasts
        var boxCol = ui.root.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(cardWidth, cardHeight, 0.01f);
        boxCol.center = Vector3.zero;

        // Nested Canvas and Raycaster on root
        var nestedCanvas = ui.root.AddComponent<Canvas>();
        nestedCanvas.overrideSorting = true;
        ui.root.AddComponent<GraphicRaycaster>();

        // PointableCanvas on root
        var rootPointable = ui.root.AddComponent<PointableCanvas>();
        rootPointable.InjectAllPointableCanvas(nestedCanvas);

        // ── ISDK Ray Canvas Interaction Child ─────────────────────────────────
        var isdkGO = MakeGO("ISDK_RayCanvasInteraction", ui.root.transform);
        StretchFull(isdkGO);
        
        var isdkLE = isdkGO.AddComponent<LayoutElement>();
        isdkLE.ignoreLayout = true;

        var isdkPointable = isdkGO.AddComponent<PointableCanvas>();
        isdkPointable.InjectAllPointableCanvas(nestedCanvas);

        // ── Surface Child ─────────────────────────────────────────────────────
        var surfaceGO = MakeGO("Surface", isdkGO.transform);
        StretchFull(surfaceGO);

        var planeSurface = surfaceGO.AddComponent<PlaneSurface>();
        planeSurface.Facing = PlaneSurface.NormalFacing.Backward;

        var boundsClipper = surfaceGO.AddComponent<BoundsClipper>();
        var clipDriver = surfaceGO.AddComponent<RectTransformBoundsClipperDriver>();
        
        var clipperField = typeof(RectTransformBoundsClipperDriver).GetField("_boundsClipper",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        clipperField?.SetValue(clipDriver, boundsClipper);

        var clippedSurface = surfaceGO.AddComponent<ClippedPlaneSurface>();
        clippedSurface.InjectAllClippedPlaneSurface(planeSurface, new List<IBoundsClipper> { boundsClipper });

        // ── Connect NATIVE Interactable (Removed EventWrapper) ────────────────
        ui.rayInteractable = isdkGO.AddComponent<RayInteractable>();
        ui.rayInteractable.InjectAllRayInteractable(clippedSurface);
        ui.rayInteractable.InjectOptionalPointableElement(isdkPointable);
        ui.rayInteractable.InjectOptionalSelectSurface(planeSurface);

        // ── Visual child (scales on hover/select) ─────────────────────────────
        ui.visual = MakeGO("Visual", ui.root.transform);
        var visRT         = RT(ui.visual);
        visRT.pivot       = new Vector2(0.5f, 0f); 
        visRT.anchorMin   = new Vector2(0.5f, 0f);
        visRT.anchorMax   = new Vector2(0.5f, 0f);
        visRT.sizeDelta   = new Vector2(cardWidth, cardHeight);
        visRT.anchoredPosition = Vector2.zero;

        ui.borderImg       = ui.visual.AddComponent<Image>();
        ui.borderImg.color = borderColorUnselected;

        var content  = MakeGO("Content", ui.visual.transform);
        StretchInset(content, borderThickness);

        var bgGO     = MakeGO("BgImage", content.transform);
        StretchFull(bgGO);
        ui.bgImage   = bgGO.AddComponent<Image>();
        ui.bgImage.preserveAspect = false;
        if (data.cardImage != null)
        {
            ui.bgImage.sprite = data.cardImage;
            ui.bgImage.type   = Image.Type.Simple;
        }
        else
        {
            ui.bgImage.color = new Color(0.2f, 0.25f, 0.35f, 1f);
        }

        var gradGO      = MakeGO("GradientOverlay", content.transform);
        var gradRT      = RT(gradGO);
        gradRT.anchorMin = new Vector2(0f, 0f);
        gradRT.anchorMax = new Vector2(1f, 0.65f);
        gradRT.offsetMin = Vector2.zero;
        gradRT.offsetMax = Vector2.zero;
        ui.gradOverlay   = gradGO.AddComponent<Image>();
        ui.gradOverlay.color = gradientColor;

        var labelGO = MakeGO("LabelPanel", content.transform);
        var labelRT = RT(labelGO);
        labelRT.anchorMin        = new Vector2(0.05f, 0f);
        labelRT.anchorMax        = new Vector2(0.95f, 0f);
        labelRT.pivot            = new Vector2(0.5f, 0f);
        labelRT.anchoredPosition = new Vector2(0f, 12f);
        labelRT.sizeDelta        = new Vector2(0f, cardHeight * 0.36f);

        ui.labelBg       = labelGO.AddComponent<Image>();
        ui.labelBg.color = labelBgUnselected;

        var vlg                    = labelGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding                = new RectOffset(12, 12, 10, 10);
        vlg.spacing                = 4f;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlHeight     = false;
        vlg.childAlignment         = TextAnchor.UpperLeft;

        ui.titleTMP          = AddTMP("Title", labelGO.transform);
        ui.titleTMP.text     = data.title;
        ui.titleTMP.fontSize = titleFontSize;
        ui.titleTMP.fontStyle = FontStyles.Bold;
        ui.titleTMP.color    = Color.white;
        ui.titleTMP.enableWordWrapping = true;
        ui.titleTMP.overflowMode = TextOverflowModes.Ellipsis;
        SetLayoutHeight(ui.titleTMP.gameObject, titleFontSize * 1.4f);

        ui.subtitleTMP          = AddTMP("Subtitle", labelGO.transform);
        ui.subtitleTMP.text     = data.subtitle;
        ui.subtitleTMP.fontSize = subtitleFontSize;
        ui.subtitleTMP.color    = new Color(1f, 1f, 1f, 0.75f);
        ui.subtitleTMP.enableWordWrapping = true;
        ui.subtitleTMP.overflowMode = TextOverflowModes.Ellipsis;
        SetLayoutHeight(ui.subtitleTMP.gameObject, subtitleFontSize * 2.8f);

        var ckGO  = MakeGO("Checkmark", content.transform);
        var ckRT  = RT(ckGO);
        ckRT.anchorMin        = new Vector2(1f, 1f);
        ckRT.anchorMax        = new Vector2(1f, 1f);
        ckRT.pivot            = new Vector2(1f, 1f);
        ckRT.anchoredPosition = new Vector2(-10f, -10f);
        ckRT.sizeDelta        = new Vector2(28f, 28f);

        var ckImg  = ckGO.AddComponent<Image>();
        ckImg.color = Color.white;

        var ckTMP = AddTMP("Check", ckGO.transform);
        StretchFull(ckTMP.gameObject);
        ckTMP.text      = "✓";
        ckTMP.fontSize  = 16f;
        ckTMP.color     = new Color(0.1f, 0.35f, 0.85f, 1f);
        ckTMP.alignment = TextAlignmentOptions.Center;
        ckTMP.fontStyle = FontStyles.Bold;

        ui.checkmark = ckGO;
        ckGO.SetActive(false);

        ui.visual.transform.localScale = Vector3.one * deselectedScale;

        _cardUIs.Add(ui);
    }

    void BuildNextButton(Transform parent)
    {
        var go = MakeGO("NextButton", parent);
        var rt = RT(go);
        rt.anchorMin        = new Vector2(0.5f, 0f);
        rt.anchorMax        = new Vector2(0.5f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 28f);
        rt.sizeDelta        = new Vector2(210f, 54f);

        var img   = go.AddComponent<Image>();
        img.color = nextBtnColor;

        var btn   = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var colors          = btn.colors;
        colors.normalColor  = nextBtnColor;
        colors.highlightedColor = Color.white;
        btn.colors          = colors;

        var tmp   = AddTMP("NextLabel", go.transform);
        StretchFull(tmp.gameObject);
        tmp.text      = "Next";
        tmp.fontSize  = nextBtnFontSize;
        tmp.color     = new Color(0.08f, 0.08f, 0.12f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Normal;

        if (Application.isPlaying)
            btn.onClick.AddListener(ConfirmSelection);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Selection Logic
    // ─────────────────────────────────────────────────────────────────────────
    void ApplySelection(int index)
    {
        if (index < 0 || index >= _cardUIs.Count) return;

        _selectedIndex = index;
        RefreshVisuals();
    }

    void RefreshVisuals()
    {
        for (int i = 0; i < _cardUIs.Count; i++)
        {
            var  ui  = _cardUIs[i];
            bool sel = (i == _selectedIndex);

            if (ui.root == null) continue;

            if (ui.visual != null)
                ui.visual.transform.localScale = Vector3.one * (sel ? selectedScale : deselectedScale);

            ui.borderImg.color           = sel ? borderColorSelected : borderColorUnselected;
            ui.labelBg.color             = sel ? labelBgSelected : labelBgUnselected;
            ui.checkmark.SetActive(sel);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Keyboard Input  (Play Mode only)
    // ─────────────────────────────────────────────────────────────────────────
    void HandleKeyboard()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            int next = Mathf.Max(0, _selectedIndex - 1);
            ApplySelection(next);
            if (debugLogs) Debug.Log($"[IntentSwapManager] ← → Card {next}: {intents[next].title}");
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            int next = Mathf.Min(_cardUIs.Count - 1, _selectedIndex + 1);
            ApplySelection(next);
            if (debugLogs) Debug.Log($"[IntentSwapManager] → Card {next}: {intents[next].title}");
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            ConfirmSelection();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OVR Native ISDK Event Listening (No EventWrapper required!)
    // ─────────────────────────────────────────────────────────────────────────
    
    private bool _issdkSubscribed = false;

    void SubscribeToInteractables()
    {
        int subscribeCount = 0;
        for (int i = 0; i < _cardUIs.Count; i++)
        {
            var card = _cardUIs[i];
            if (card.rayInteractable == null) continue;

            int indexCapture = i;
            
            // Subscribing directly to the NATIVE state machine of the RayInteractable
            card.rayInteractable.WhenStateChanged += (InteractableStateChangeArgs args) =>
            {
                // Triggered when the Ray hits the canvas collider
                if (args.NewState == InteractableState.Hover && args.PreviousState == InteractableState.Normal)
                {
                    if (debugLogs) Debug.Log($"<color=lime><b>[ISDK NATIVE CODE]</b></color> Hover started on Card {indexCapture}: {intents[indexCapture].title}");
                }
                // Triggered when the user Pinches while hovering
                else if (args.NewState == InteractableState.Select && args.PreviousState == InteractableState.Hover)
                {
                    if (debugLogs) Debug.Log($"<color=yellow><b>[ISDK NATIVE CODE]</b></color> Pinched/Selected Card {indexCapture}: {intents[indexCapture].title}");
                    ApplySelection(indexCapture);
                }
            };

            subscribeCount++;
        }

        _issdkSubscribed = subscribeCount > 0;
        if (!_issdkSubscribed && debugLogs)
            Debug.LogWarning("[IntentSwapManager] No RayInteractables found on cards — check that ISDK is set up correctly.");
    }

    void HandleOVRInput()
    {
        HandleHand(leftHand,  leftRayOrigin,  ref _wasLeftPinching,  Color.cyan);
        HandleHand(rightHand, rightRayOrigin, ref _wasRightPinching, Color.yellow);
    }

    void HandleHand(OVRHand hand, Transform rayOrigin, ref bool wasPinching, Color debugColor)
    {
        if (hand == null || rayOrigin == null) return;
        if (!hand.IsTracked) return;

        var ray = new Ray(rayOrigin.position, rayOrigin.forward);
        if (debugLogs) Debug.DrawRay(ray.origin, ray.direction * rayDistance, debugColor);

        int hoveredIndex = -1;

        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, interactableLayer))
        {
            for (int i = 0; i < _cardUIs.Count; i++)
            {
                if (_cardUIs[i].root != null && hit.collider.gameObject == _cardUIs[i].root)
                {
                    hoveredIndex = i;
                    break;
                }
            }
        }

        bool pinching = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
        
        // Raw fallback logic in case native ISDK events fail
        if (pinching && !wasPinching && hoveredIndex != -1)
        {
            if (_selectedIndex != hoveredIndex)
            {
                ApplySelection(hoveredIndex);
                if (debugLogs) Debug.Log($"[IntentSwapManager] [{hand.name}] Raw Pinch Fallback → Card {hoveredIndex} Selected.");
            }
        }
        
        wasPinching = pinching;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Confirm
    // ─────────────────────────────────────────────────────────────────────────
    public void HandleKeyboardTest(int direction)
    {
        int next = Mathf.Clamp(_selectedIndex + direction, 0, _cardUIs.Count - 1);
        ApplySelection(next);
        if (debugLogs) Debug.Log($"[IntentSwapManager] Editor nav → Card {next}: {intents[next].title}");
    }

    public void ConfirmSelection()
    {
        if (_selectedIndex < 0 || _selectedIndex >= intents.Count)
        {
            if (debugLogs) Debug.LogWarning("[IntentSwapManager] Nothing selected — choose a card first.");
            return;
        }

        var chosen = intents[_selectedIndex];
        Debug.Log($"<color=green><b>[IntentSwapManager]</b></color> ✅ Intent confirmed: \"{chosen.title}\" | subtitle: \"{chosen.subtitle}\"");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────
    static GameObject MakeGO(string goName, Transform parent)
    {
        var go = new GameObject(goName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static RectTransform RT(GameObject go) => (RectTransform)go.transform;

    static void StretchFull(GameObject go)
    {
        var rt       = RT(go);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void StretchInset(GameObject go, float inset)
    {
        var rt       = RT(go);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2( inset,  inset);
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
        var le             = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight       = height;
    }
}