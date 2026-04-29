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
using UApplication = UnityEngine.Application;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Drop this on an empty GameObject.
/// It generates a world-space Meta Interaction SDK ray-interactable card menu.
///
/// FEATURES:
/// - Room mood card selection
/// - Optional SplatManager integration
/// - Optional AudioManager integration
/// - Product UI / Cabinet UI / Fridges / Cabinets visibility based on Host Room selection
///
/// UPDATED:
/// - Next button removed completely
/// - Cabinet UI added and follows Product UI visibility
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
[RequireComponent(typeof(GraphicRaycaster))]
public class MetaRayIntentCardMenu : MonoBehaviour
{
    public enum SplatCardAction
    {
        AutoByIndex,
        None,
        CalmRoom,
        FastRoom,
        HostRoom
    }

    public enum IntentSelection
    {
        CalmRoom = 0,
        FastRoom = 1,
        HostRoom = 2
    }

    [Header("Intent Enum Selection")]
    public IntentSelection selectedIntent = IntentSelection.CalmRoom;
    public bool useIntentEnumSelection = true;
    public bool syncIntentEnumWhenSelectingCards = true;
    public bool previewSplatManagerInEditorFromSelectedIntent = true;

    [Header("Host Room Only Objects")]
    public GameObject productUI;
    public GameObject cabinetUI;
    public GameObject fridgesGO;
    public GameObject cabinetsGO;
    public bool onlyShowProductUIAndFridgesInHostRoom = true;
    public bool updateHostRoomObjectsInEditor = true;

    [Header("Splat Manager Hook")]
    public SplatManager splatManager;
    public bool autoFindSplatManager = true;
    public bool callSplatManagerOnSelect = true;
    public bool callSplatManagerOnConfirm = false;
    public bool reapplySameSplatIfClickedAgain = false;

    [Header("Audio Manager Hook")]
    [Tooltip("Optional. If empty, the script will auto-find AudioManager.")]
    public AudioManager audioManager;

    [Tooltip("When enabled, this script will auto-find AudioManager.")]
    public bool autoFindAudioManager = true;

    [Tooltip("When this Intent UI is enabled in play mode, it starts the room music for the current selectedIntent.")]
    public bool playRoomMusicWhenIntentUIIsEnabled = true;

    [Tooltip("When a card is selected, play the matching room atmosphere music.")]
    public bool playRoomMusicOnSelect = true;

    [Tooltip("When true, choosing the same selected card again can re-apply the music. AudioManager can still avoid restarting the same clip.")]
    public bool reapplySameMusicIfClickedAgain = false;

    [Header("Start Selection")]
    public bool selectCardOnStart = true;
    [Range(0, 7)] public int startCardIndex = 0;
    public bool callSplatManagerForStartSelection = true;

    [Header("Card Data")]
    public List<MetaRayIntentCardData> cards = new List<MetaRayIntentCardData>();
    [Range(1, 8)] public int fallbackCardCount = 3;

    [Serializable] public class IntentCardEvent : UnityEvent<int, MetaRayIntentCardData> { }
    [Serializable] public class SplatActionEvent : UnityEvent<SplatCardAction> { }

    [Header("Events")]
    public IntentCardEvent onSelectionChanged = new IntentCardEvent();
    public IntentCardEvent onConfirmed = new IntentCardEvent();
    public SplatActionEvent onSplatActionInvoked = new SplatActionEvent();

    [Header("World Space Canvas")]
    public float worldScale = 0.001f;
    public Camera eventCamera;
    public bool autoCreateEventSystem = true;
    public bool autoRebuildInEditor = true;
    public PlaneSurface.NormalFacing raySurfaceFacing = PlaneSurface.NormalFacing.Backward;

    [Header("Ray / Pointer Select")]
    public bool addButtonComponentToCards = true;
    public bool selectCardOnPointerDown = true;
    public bool selectCardOnPointerClick = true;

    [Header("Keyboard Simulation")]
    public bool keyboardDebug = true;
    public bool arrowKeysWrapAround = true;
    public bool arrowKeysInvokeSelection = true;
    public KeyCode previousCardKey = KeyCode.LeftArrow;
    public KeyCode nextCardKey = KeyCode.RightArrow;
    public KeyCode confirmKey = KeyCode.Return;
    public KeyCode confirmKeyAlt = KeyCode.KeypadEnter;

    [Header("Layout")]
    public float cardWidth = 260f;
    public float cardHeight = 347f;
    public float cardSpacing = 36f;
    public float canvasPadX = 120f;
    public float canvasPadY = 140f;
    public float headerHeight = 56f;
    public float headerTopOffset = 28f;
    public float cardsVerticalOffset = -8f;

    [Header("Card Scale")]
    [Range(0.75f, 1.00f)] public float normalScale = 0.93f;
    [Range(0.90f, 1.15f)] public float hoverScale = 1.00f;
    [Range(1.00f, 1.25f)] public float selectedScale = 1.08f;
    public float scaleLerpSpeed = 14f;

    [Header("Rounded Corners")]
    public bool useGeneratedRoundedCorners = true;
    public bool clipCardContentToRoundedCorners = true;
    [Range(1, 64)] public int headerCornerRadius = 22;
    [Range(1, 64)] public int cardBorderCornerRadius = 28;
    [Range(1, 64)] public int cardContentCornerRadius = 24;
    [Range(1, 64)] public int labelPanelCornerRadius = 18;
    [Range(1, 64)] public int checkmarkCornerRadius = 32;

    [Header("Card Colors")]
    public Color cardFallbackColorA = new Color(0.13f, 0.20f, 0.24f, 1f);
    public Color cardFallbackColorB = new Color(0.24f, 0.13f, 0.18f, 1f);
    public Color cardFallbackColorC = new Color(0.24f, 0.18f, 0.11f, 1f);
    public Color borderNormal = new Color(0.08f, 0.11f, 0.18f, 1f);
    public Color borderHover = new Color(0.55f, 0.75f, 1f, 1f);
    public Color borderSelected = new Color(1f, 1f, 1f, 0.95f);
    [Range(1f, 12f)] public float borderThickness = 4f;
    public Color labelNormal = new Color(0.08f, 0.10f, 0.16f, 0.72f);
    public Color labelHover = new Color(0.13f, 0.18f, 0.26f, 0.82f);
    public Color labelSelected = new Color(1f, 1f, 1f, 0.25f);
    public Color overlayColor = new Color(0f, 0f, 0f, 0.58f);
    public Color headerBgColor = new Color(0f, 0f, 0f, 0.36f);

    [Header("Typography")]
    public string headerText = "Choose room mood";
    public float headerFontSize = 30f;
    public float titleFontSize = 19f;
    public float subtitleFontSize = 12f;

    [Header("Debug")]
    public bool debugLogs = true;

    [SerializeField, HideInInspector] private GameObject _generatedRoot;
    [SerializeField, HideInInspector] private GameObject _raySurfaceRoot;
    [SerializeField, HideInInspector] private PointableCanvas _pointableCanvas;
    [SerializeField, HideInInspector] private RayInteractable _canvasRayInteractable;

    private Canvas _canvas;
    private CanvasScaler _canvasScaler;
    private GraphicRaycaster _graphicRaycaster;
    private RectTransform _rectTransform;

    private readonly List<CardRuntimeUI> _runtimeCards = new List<CardRuntimeUI>();
    private int _selectedIndex = -1;
    private int _hoveredIndex = -1;
    private bool _subscribedToRayState;

#if UNITY_EDITOR
    private bool _editorRebuildQueued;
#endif

    [Serializable]
    private class CardRuntimeUI
    {
        public int index;
        public GameObject root;
        public RectTransform visual;
        public UIImage hitImage;
        public Button button;
        public UIImage borderImage;
        public UIImage backgroundImage;
        public UIImage overlayImage;
        public UIImage labelImage;
        public UIImage checkCircleImage;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI subtitleText;
        public GameObject checkmarkRoot;
        public Shadow shadow;
        public float targetScale;
    }

    private void Reset()
    {
        GrabRequiredComponents();
        TryAutoFindSplatManager();
        TryAutoFindAudioManager();
        SyncStartIndexFromIntentEnum();
        ConfigureCanvas();
        ApplyHostRoomObjectVisibility();
    }

    private void Awake()
    {
        GrabRequiredComponents();
        TryAutoFindSplatManager();
        TryAutoFindAudioManager();
        SyncStartIndexFromIntentEnum();
        ConfigureCanvas();
        ApplyHostRoomObjectVisibility();
    }

    private void OnEnable()
    {
        GrabRequiredComponents();
        TryAutoFindSplatManager();
        TryAutoFindAudioManager();
        SyncStartIndexFromIntentEnum();
        ApplyHostRoomObjectVisibility();

        if (UApplication.isPlaying && playRoomMusicWhenIntentUIIsEnabled)
        {
            PlayAudioForCurrentIntent();
        }
    }

    private void Start()
    {
        if (!UApplication.isPlaying) return;

        TryAutoFindSplatManager();
        TryAutoFindAudioManager();
        SyncStartIndexFromIntentEnum();
        ApplyHostRoomObjectVisibility();

        RebuildMenu();
        SubscribeRayState();

        int startIndex = GetStartSelectionIndex();

        if (selectCardOnStart && IsValidCardIndex(startIndex))
        {
            ApplySelection(startIndex, callSplatManagerForStartSelection);
        }
        else
        {
            ApplyHostRoomObjectVisibility();

            if (playRoomMusicWhenIntentUIIsEnabled)
                PlayAudioForCurrentIntent();
        }
    }

    private void Update()
    {
        if (!UApplication.isPlaying) return;

        AnimateScales();

        if (keyboardDebug)
            HandleKeyboardDebug();
    }

    private void OnDisable()
    {
        UnsubscribeRayState();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (UApplication.isPlaying) return;

        TryAutoFindSplatManager();
        TryAutoFindAudioManager();
        SyncStartIndexFromIntentEnum();

        if (updateHostRoomObjectsInEditor)
            ApplyHostRoomObjectVisibility();

        if (!autoRebuildInEditor)
        {
            ApplyIntentEnumSelectionInEditor(previewSplatManagerInEditorFromSelectedIntent);
            return;
        }

        QueueEditorRebuild();
    }

    private void QueueEditorRebuild()
    {
        if (_editorRebuildQueued) return;

        _editorRebuildQueued = true;

        EditorApplication.delayCall += () =>
        {
            _editorRebuildQueued = false;

            if (this == null || gameObject == null) return;
            if (UApplication.isPlaying) return;

            RebuildMenu();
            ApplyIntentEnumSelectionInEditor(previewSplatManagerInEditorFromSelectedIntent);
            ApplyHostRoomObjectVisibility();
        };
    }
#endif

    [ContextMenu("Rebuild Meta Ray Intent Menu")]
    public void RebuildMenu()
    {
        UnsubscribeRayState();

        GrabRequiredComponents();
        TryAutoFindSplatManager();
        TryAutoFindAudioManager();
        SyncStartIndexFromIntentEnum();
        ConfigureCanvas();
        EnsurePointableCanvas();
        EnsureEventSystemAndPointableModule();

        ClearGeneratedObjects();

        _runtimeCards.Clear();
        _hoveredIndex = -1;
        _selectedIndex = -1;

        _generatedRoot = MakeUIObject("Generated_MetaRayIntentMenu", transform);
        StretchFull(_generatedRoot);

        BuildRayInteractionSurface();
        BuildHeader(_generatedRoot.transform);
        BuildCardsRow(_generatedRoot.transform);

        RefreshVisuals(true);

        if (!UApplication.isPlaying)
            ApplyIntentEnumSelectionVisualOnlyInEditor();

        ApplyHostRoomObjectVisibility();

        if (UApplication.isPlaying)
            SubscribeRayState();

        if (debugLogs)
            UDebug.Log($"<b>[MetaRayIntentCardMenu]</b> Built {_runtimeCards.Count} ray-interactable card(s).");
    }

    public void SelectCard(int index) => ApplySelection(index, true);

    public void SelectPreviousCard() => SelectRelativeCard(-1, arrowKeysInvokeSelection);

    public void SelectNextCard() => SelectRelativeCard(1, arrowKeysInvokeSelection);

    public void SetIntent(IntentSelection intent)
    {
        selectedIntent = intent;
        SyncStartIndexFromIntentEnum();
        ApplyHostRoomObjectVisibility();

        int index = GetStartSelectionIndex();

        if (UApplication.isPlaying)
            ApplySelection(index, true);
        else
            ApplyIntentEnumSelectionInEditor(previewSplatManagerInEditorFromSelectedIntent);
    }

    public void PlayAudioForCurrentIntent()
    {
        PlayAudioForCard(GetStartSelectionIndex());
    }

    public void SetHoveredCard(int index, bool isHovered)
    {
        if (isHovered)
            _hoveredIndex = index;
        else if (_hoveredIndex == index)
            _hoveredIndex = -1;

        RefreshVisuals(false);
    }

    public void ConfirmSelection()
    {
        if (!IsValidCardIndex(_selectedIndex))
        {
            if (debugLogs)
                UDebug.LogWarning("[MetaRayIntentCardMenu] Nothing selected. Hover/pinch-select a card first.");
            return;
        }

        MetaRayIntentCardData data = GetData(_selectedIndex);

        if (callSplatManagerOnConfirm)
            InvokeSplatManagerForCard(_selectedIndex);

        data?.onConfirmed?.Invoke();
        onConfirmed?.Invoke(_selectedIndex, data);

        if (debugLogs)
            UDebug.Log($"<color=lime><b>[MetaRayIntentCardMenu]</b></color> Confirmed card {_selectedIndex}: {GetTitle(_selectedIndex)}");
    }

    public void InvokeSelectedSplatRoom()
    {
        if (IsValidCardIndex(_selectedIndex))
            InvokeSplatManagerForCard(_selectedIndex);
    }

    public void SelectCalmRoomCard() => SetIntent(IntentSelection.CalmRoom);
    public void SelectFastRoomCard() => SetIntent(IntentSelection.FastRoom);
    public void SelectHostRoomCard() => SetIntent(IntentSelection.HostRoom);

    private void SelectRelativeCard(int direction, bool invokeEvents)
    {
        int count = _runtimeCards.Count;
        if (count <= 0) return;

        int current = IsValidCardIndex(_selectedIndex)
            ? _selectedIndex
            : Mathf.Clamp(GetStartSelectionIndex(), 0, count - 1);

        int next = current + direction;

        if (arrowKeysWrapAround)
        {
            if (next < 0) next = count - 1;
            else if (next >= count) next = 0;
        }
        else
        {
            next = Mathf.Clamp(next, 0, count - 1);
        }

        ApplySelection(next, invokeEvents);
    }

    private void TryAutoFindSplatManager()
    {
        if (!autoFindSplatManager || splatManager != null) return;
        splatManager = FindAny<SplatManager>();
    }

    private void TryAutoFindAudioManager()
    {
        if (!autoFindAudioManager || audioManager != null) return;

        audioManager = AudioManager.GetOrFindInstance();

        if (audioManager == null)
            audioManager = FindAny<AudioManager>();
    }

    private void SyncStartIndexFromIntentEnum()
    {
        if (useIntentEnumSelection)
            startCardIndex = IntentToCardIndex(selectedIntent);
    }

    private int GetStartSelectionIndex()
    {
        return useIntentEnumSelection ? IntentToCardIndex(selectedIntent) : startCardIndex;
    }

    private int IntentToCardIndex(IntentSelection intent) => (int)intent;

    private void SyncIntentEnumFromCardIndex(int index)
    {
        if (!syncIntentEnumWhenSelectingCards) return;

        switch (index)
        {
            case 0: selectedIntent = IntentSelection.CalmRoom; break;
            case 1: selectedIntent = IntentSelection.FastRoom; break;
            case 2: selectedIntent = IntentSelection.HostRoom; break;
        }

        startCardIndex = index;
    }

    private void ApplyIntentEnumSelectionVisualOnlyInEditor()
    {
        if (UApplication.isPlaying || !useIntentEnumSelection) return;

        int index = GetStartSelectionIndex();
        _selectedIndex = index;
        _hoveredIndex = -1;
        ApplyHostRoomObjectVisibility();

        if (IsValidCardIndex(index))
            RefreshVisuals(true);
    }

    private void ApplyIntentEnumSelectionInEditor(bool invokeSplatManager)
    {
        if (UApplication.isPlaying || !useIntentEnumSelection) return;

        int index = GetStartSelectionIndex();
        _selectedIndex = index;
        _hoveredIndex = -1;
        ApplyHostRoomObjectVisibility();

        if (IsValidCardIndex(index))
            RefreshVisuals(true);

        if (invokeSplatManager && IsValidCardIndex(index))
            InvokeSplatManagerForCard(index);

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    private void ApplyHostRoomObjectVisibility()
    {
        if (!onlyShowProductUIAndFridgesInHostRoom) return;

        bool shouldBeActive = ShouldHostRoomObjectsBeActive();

        SetActiveIfNeeded(productUI, shouldBeActive);
        SetActiveIfNeeded(cabinetUI, shouldBeActive);
        SetActiveIfNeeded(fridgesGO, shouldBeActive);
        SetActiveIfNeeded(cabinetsGO, shouldBeActive);
    }

    private bool ShouldHostRoomObjectsBeActive()
    {
        if (useIntentEnumSelection)
            return selectedIntent == IntentSelection.HostRoom;

        if (_selectedIndex >= 0)
            return _selectedIndex == IntentToCardIndex(IntentSelection.HostRoom);

        return startCardIndex == IntentToCardIndex(IntentSelection.HostRoom);
    }

    private static void SetActiveIfNeeded(GameObject target, bool active)
    {
        if (target == null || target.activeSelf == active) return;

        target.SetActive(active);

#if UNITY_EDITOR
        if (!UApplication.isPlaying)
            EditorUtility.SetDirty(target);
#endif
    }

    private void InvokeSplatManagerForCard(int index)
    {
        SplatCardAction action = GetSplatAction(index);

        if (action == SplatCardAction.None || action == SplatCardAction.AutoByIndex)
        {
            if (debugLogs)
                UDebug.LogWarning($"[MetaRayIntentCardMenu] Card {index} has no valid SplatManager action.");
            return;
        }

        if (splatManager == null)
            TryAutoFindSplatManager();

        if (splatManager == null)
        {
            UDebug.LogError($"[MetaRayIntentCardMenu] Cannot invoke {action}. No SplatManager assigned or found in scene.");
            return;
        }

        switch (action)
        {
            case SplatCardAction.CalmRoom: splatManager.SetCalmRoom(); break;
            case SplatCardAction.FastRoom: splatManager.SetFastRoom(); break;
            case SplatCardAction.HostRoom: splatManager.SetHostRoom(); break;
        }

        onSplatActionInvoked?.Invoke(action);

        if (debugLogs)
            UDebug.Log($"<color=lime><b>[MetaRayIntentCardMenu]</b></color> Invoked SplatManager action: {action}");
    }

    private void PlayAudioForCard(int index)
    {
        if (!playRoomMusicOnSelect && UApplication.isPlaying)
            return;

        if (audioManager == null)
            TryAutoFindAudioManager();

        if (audioManager == null)
        {
            if (debugLogs)
                UDebug.LogWarning("[MetaRayIntentCardMenu] No AudioManager assigned or found. Room music not changed.", this);
            return;
        }

        SplatManager.SplatRoom room = GetSplatRoomForCard(index);

        if (room == SplatManager.SplatRoom.None)
            return;

        audioManager.PlayAtmosphereForRoom(room);
    }

    private SplatManager.SplatRoom GetSplatRoomForCard(int index)
    {
        SplatCardAction action = GetSplatAction(index);

        switch (action)
        {
            case SplatCardAction.CalmRoom: return SplatManager.SplatRoom.CalmRoom;
            case SplatCardAction.FastRoom: return SplatManager.SplatRoom.FastRoom;
            case SplatCardAction.HostRoom: return SplatManager.SplatRoom.HostRoom;
            default: return SplatManager.SplatRoom.None;
        }
    }

    private SplatCardAction GetSplatAction(int index)
    {
        MetaRayIntentCardData data = GetData(index);

        if (data != null && data.splatAction != SplatCardAction.AutoByIndex)
            return data.splatAction;

        switch (index)
        {
            case 0: return SplatCardAction.CalmRoom;
            case 1: return SplatCardAction.FastRoom;
            case 2: return SplatCardAction.HostRoom;
            default: return SplatCardAction.None;
        }
    }

    private void GrabRequiredComponents()
    {
        _canvas = GetComponent<Canvas>();
        _canvasScaler = GetComponent<CanvasScaler>();
        _graphicRaycaster = GetComponent<GraphicRaycaster>();
        _rectTransform = GetComponent<RectTransform>();
    }

    private void ConfigureCanvas()
    {
        if (_canvas == null) return;

        int count = GetCardCount();
        float totalCardWidth = count * cardWidth + Mathf.Max(0, count - 1) * cardSpacing;
        float totalWidth = totalCardWidth + canvasPadX * 2f;
        float totalHeight = cardHeight + canvasPadY;

        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.worldCamera = eventCamera != null ? eventCamera : Camera.main;

        transform.localScale = Vector3.one * worldScale;

        if (_rectTransform != null)
        {
            _rectTransform.sizeDelta = new Vector2(totalWidth, totalHeight);
            _rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        if (_canvasScaler != null)
        {
            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            _canvasScaler.referencePixelsPerUnit = 100f;
            _canvasScaler.dynamicPixelsPerUnit = 10f;
        }

        if (_graphicRaycaster != null)
        {
            _graphicRaycaster.ignoreReversedGraphics = true;
            _graphicRaycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
        }
    }

    private void EnsurePointableCanvas()
    {
        _pointableCanvas = GetComponent<PointableCanvas>();

        if (_pointableCanvas == null)
            _pointableCanvas = gameObject.AddComponent<PointableCanvas>();

        if (_canvas != null)
            _pointableCanvas.InjectAllPointableCanvas(_canvas);
    }

    private void EnsureEventSystemAndPointableModule()
    {
        if (!autoCreateEventSystem) return;

        EventSystem eventSystem = FindAny<EventSystem>();

        if (eventSystem == null)
        {
            GameObject eventSystemGO = new GameObject("EventSystem - Meta Pointable Canvas");
            eventSystem = eventSystemGO.AddComponent<EventSystem>();

            if (debugLogs)
                UDebug.Log("<b>[MetaRayIntentCardMenu]</b> Created EventSystem.");
        }

        PointableCanvasModule module = eventSystem.GetComponent<PointableCanvasModule>();

        if (module == null)
        {
            module = eventSystem.gameObject.AddComponent<PointableCanvasModule>();

            if (debugLogs)
                UDebug.Log("<b>[MetaRayIntentCardMenu]</b> Added PointableCanvasModule to EventSystem.");
        }
    }

    private void BuildRayInteractionSurface()
    {
        _raySurfaceRoot = MakeUIObject("ISDK_RayInteractionSurface", transform);
        StretchFull(_raySurfaceRoot);

        LayoutElement layoutElement = AddOrGet<LayoutElement>(_raySurfaceRoot);
        layoutElement.ignoreLayout = true;

        PlaneSurface planeSurface = AddOrGet<PlaneSurface>(_raySurfaceRoot);
        planeSurface.Facing = raySurfaceFacing;

        BoundsClipper boundsClipper = AddOrGet<BoundsClipper>(_raySurfaceRoot);

        RectTransformBoundsClipperDriver clipperDriver = AddOrGet<RectTransformBoundsClipperDriver>(_raySurfaceRoot);
        TryConfigureRectTransformBoundsClipperDriver(clipperDriver, boundsClipper);

        ClippedPlaneSurface clippedPlaneSurface = AddOrGet<ClippedPlaneSurface>(_raySurfaceRoot);
        clippedPlaneSurface.InjectAllClippedPlaneSurface(planeSurface, new List<IBoundsClipper> { boundsClipper });

        _canvasRayInteractable = AddOrGet<RayInteractable>(_raySurfaceRoot);
        _canvasRayInteractable.InjectAllRayInteractable(clippedPlaneSurface);

        if (_pointableCanvas != null)
            _canvasRayInteractable.InjectOptionalPointableElement(_pointableCanvas);

        _canvasRayInteractable.InjectOptionalSelectSurface(planeSurface);
    }

    private void BuildHeader(Transform parent)
    {
        GameObject header = MakeUIObject("Header", parent);
        RectTransform rt = header.GetComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.05f, 1f);
        rt.anchorMax = new Vector2(0.95f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -headerTopOffset);
        rt.sizeDelta = new Vector2(0f, headerHeight);

        UIImage bg = header.AddComponent<UIImage>();
        ApplyRoundedImage(bg, headerBgColor, headerCornerRadius);
        bg.raycastTarget = false;

        TextMeshProUGUI text = AddTMP("HeaderText", header.transform);
        StretchFull(text.gameObject);
        text.text = headerText;
        text.fontSize = headerFontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Normal;
        text.raycastTarget = false;
    }

    private void BuildCardsRow(Transform parent)
    {
        int count = GetCardCount();

        GameObject row = MakeUIObject("CardsRow", parent);
        RectTransform rowRT = row.GetComponent<RectTransform>();

        float rowWidth = count * cardWidth + Mathf.Max(0, count - 1) * cardSpacing;

        rowRT.anchorMin = new Vector2(0.5f, 0.5f);
        rowRT.anchorMax = new Vector2(0.5f, 0.5f);
        rowRT.pivot = new Vector2(0.5f, 0.5f);
        rowRT.anchoredPosition = new Vector2(0f, cardsVerticalOffset);
        rowRT.sizeDelta = new Vector2(rowWidth, cardHeight);

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = cardSpacing;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        for (int i = 0; i < count; i++)
            BuildCard(row.transform, i);
    }

    private void BuildCard(Transform parent, int index)
    {
        MetaRayIntentCardData data = GetData(index);

        CardRuntimeUI ui = new CardRuntimeUI();
        ui.index = index;
        ui.targetScale = normalScale;

        ui.root = MakeUIObject($"Card_{index}_{CleanName(GetTitle(index))}", parent);
        RectTransform rootRT = ui.root.GetComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(cardWidth, cardHeight);

        LayoutElement le = ui.root.AddComponent<LayoutElement>();
        le.minWidth = cardWidth;
        le.preferredWidth = cardWidth;
        le.flexibleWidth = 0f;
        le.minHeight = cardHeight;
        le.preferredHeight = cardHeight;
        le.flexibleHeight = 0f;

        ui.hitImage = ui.root.AddComponent<UIImage>();
        ui.hitImage.color = new Color(1f, 1f, 1f, 0.001f);
        ui.hitImage.raycastTarget = true;

        if (addButtonComponentToCards)
        {
            ui.button = ui.root.AddComponent<Button>();
            ui.button.targetGraphic = ui.hitImage;
            ui.button.transition = Selectable.Transition.None;
            ui.button.navigation = new Navigation { mode = Navigation.Mode.None };

            int capturedIndex = index;
            ui.button.onClick.RemoveAllListeners();
            ui.button.onClick.AddListener(() => SelectCard(capturedIndex));
        }

        MetaRayIntentCardHitbox hitbox = ui.root.AddComponent<MetaRayIntentCardHitbox>();
        hitbox.Initialize(this, index);

        ui.visual = MakeUIObject("Visual", ui.root.transform).GetComponent<RectTransform>();
        ui.visual.anchorMin = new Vector2(0.5f, 0f);
        ui.visual.anchorMax = new Vector2(0.5f, 0f);
        ui.visual.pivot = new Vector2(0.5f, 0f);
        ui.visual.anchoredPosition = Vector2.zero;
        ui.visual.sizeDelta = new Vector2(cardWidth, cardHeight);
        ui.visual.localScale = Vector3.one * normalScale;

        ui.borderImage = ui.visual.gameObject.AddComponent<UIImage>();
        ApplyRoundedImage(ui.borderImage, borderNormal, cardBorderCornerRadius);
        ui.borderImage.raycastTarget = false;

        ui.shadow = ui.visual.gameObject.AddComponent<Shadow>();
        ui.shadow.effectDistance = new Vector2(0f, -10f);
        ui.shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);

        GameObject content = MakeUIObject("Content", ui.visual);
        StretchInset(content, borderThickness);

        if (clipCardContentToRoundedCorners)
        {
            UIImage maskImage = content.AddComponent<UIImage>();
            ApplyRoundedImage(maskImage, Color.white, cardContentCornerRadius);
            maskImage.raycastTarget = false;

            Mask mask = content.AddComponent<Mask>();
            mask.showMaskGraphic = false;
        }

        GameObject bgGO = MakeUIObject("Background", content.transform);
        StretchFull(bgGO);

        ui.backgroundImage = bgGO.AddComponent<UIImage>();
        ui.backgroundImage.raycastTarget = false;
        ui.backgroundImage.preserveAspect = false;

        if (data != null && data.cardImage != null)
        {
            ui.backgroundImage.sprite = data.cardImage;
            ui.backgroundImage.color = Color.white;
            ui.backgroundImage.type = UIImage.Type.Simple;
        }
        else
        {
            ui.backgroundImage.sprite = null;
            ui.backgroundImage.type = UIImage.Type.Simple;
            ui.backgroundImage.color = GetFallbackBackgroundColor(index);
        }

        GameObject overlay = MakeUIObject("BottomOverlay", content.transform);
        RectTransform overlayRT = overlay.GetComponent<RectTransform>();
        overlayRT.anchorMin = new Vector2(0f, 0f);
        overlayRT.anchorMax = new Vector2(1f, 0.65f);
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;

        ui.overlayImage = overlay.AddComponent<UIImage>();
        ui.overlayImage.color = overlayColor;
        ui.overlayImage.raycastTarget = false;

        GameObject label = MakeUIObject("LabelPanel", content.transform);
        RectTransform labelRT = label.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0.05f, 0f);
        labelRT.anchorMax = new Vector2(0.95f, 0f);
        labelRT.pivot = new Vector2(0.5f, 0f);
        labelRT.anchoredPosition = new Vector2(0f, 12f);
        labelRT.sizeDelta = new Vector2(0f, cardHeight * 0.36f);

        ui.labelImage = label.AddComponent<UIImage>();
        ApplyRoundedImage(ui.labelImage, labelNormal, labelPanelCornerRadius);
        ui.labelImage.raycastTarget = false;

        VerticalLayoutGroup vlg = label.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 10, 10);
        vlg.spacing = 4f;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ui.titleText = AddTMP("Title", label.transform);
        ui.titleText.text = GetTitle(index);
        ui.titleText.fontSize = titleFontSize;
        ui.titleText.fontStyle = FontStyles.Bold;
        ui.titleText.color = Color.white;
        ui.titleText.alignment = TextAlignmentOptions.Left;
        ui.titleText.enableWordWrapping = true;
        ui.titleText.overflowMode = TextOverflowModes.Ellipsis;
        ui.titleText.raycastTarget = false;
        SetPreferredHeight(ui.titleText.gameObject, titleFontSize * 1.45f);

        ui.subtitleText = AddTMP("Subtitle", label.transform);
        ui.subtitleText.text = GetSubtitle(index);
        ui.subtitleText.fontSize = subtitleFontSize;
        ui.subtitleText.fontStyle = FontStyles.Normal;
        ui.subtitleText.color = new Color(1f, 1f, 1f, 0.78f);
        ui.subtitleText.alignment = TextAlignmentOptions.Left;
        ui.subtitleText.enableWordWrapping = true;
        ui.subtitleText.overflowMode = TextOverflowModes.Ellipsis;
        ui.subtitleText.raycastTarget = false;
        SetPreferredHeight(ui.subtitleText.gameObject, subtitleFontSize * 3.1f);

        ui.checkmarkRoot = MakeUIObject("SelectedCheckmark", content.transform);
        RectTransform ckRT = ui.checkmarkRoot.GetComponent<RectTransform>();
        ckRT.anchorMin = new Vector2(1f, 1f);
        ckRT.anchorMax = new Vector2(1f, 1f);
        ckRT.pivot = new Vector2(1f, 1f);
        ckRT.anchoredPosition = new Vector2(-12f, -12f);
        ckRT.sizeDelta = new Vector2(30f, 30f);

        ui.checkCircleImage = ui.checkmarkRoot.AddComponent<UIImage>();
        ApplyRoundedImage(ui.checkCircleImage, Color.white, checkmarkCornerRadius);
        ui.checkCircleImage.raycastTarget = false;

        TextMeshProUGUI checkText = AddTMP("Check", ui.checkmarkRoot.transform);
        StretchFull(checkText.gameObject);
        checkText.text = "✓";
        checkText.fontSize = 17f;
        checkText.fontStyle = FontStyles.Bold;
        checkText.color = new Color(0.08f, 0.28f, 0.85f, 1f);
        checkText.alignment = TextAlignmentOptions.Center;
        checkText.raycastTarget = false;

        ui.checkmarkRoot.SetActive(false);
        _runtimeCards.Add(ui);
    }

    private void ApplySelection(int index, bool invokeEvents)
    {
        if (!IsValidCardIndex(index)) return;

        bool changed = _selectedIndex != index;
        _selectedIndex = index;

        SyncIntentEnumFromCardIndex(index);
        ApplyHostRoomObjectVisibility();
        RefreshVisuals(false);

        if (debugLogs)
            UDebug.Log($"<color=yellow><b>[MetaRayIntentCardMenu]</b></color> Selected card {index}: {GetTitle(index)}");

        if (!invokeEvents) return;

        if (!changed && !reapplySameSplatIfClickedAgain && !reapplySameMusicIfClickedAgain)
            return;

        MetaRayIntentCardData data = GetData(index);

        if (callSplatManagerOnSelect && (changed || reapplySameSplatIfClickedAgain))
            InvokeSplatManagerForCard(index);

        if (playRoomMusicOnSelect && (changed || reapplySameMusicIfClickedAgain))
            PlayAudioForCard(index);

        data?.onSelected?.Invoke();
        onSelectionChanged?.Invoke(index, data);
    }

    private void RefreshVisuals(bool instant)
    {
        for (int i = 0; i < _runtimeCards.Count; i++)
        {
            CardRuntimeUI ui = _runtimeCards[i];
            if (ui == null || ui.root == null) continue;

            bool selected = i == _selectedIndex;
            bool hovered = i == _hoveredIndex;

            float targetScale = selected ? selectedScale : hovered ? hoverScale : normalScale;
            ui.targetScale = targetScale;

            if (instant || !UApplication.isPlaying)
            {
                if (ui.visual != null)
                    ui.visual.localScale = Vector3.one * targetScale;
            }

            if (ui.borderImage != null)
                ui.borderImage.color = selected ? borderSelected : hovered ? borderHover : borderNormal;

            if (ui.labelImage != null)
                ui.labelImage.color = selected ? labelSelected : hovered ? labelHover : labelNormal;

            if (ui.checkmarkRoot != null)
                ui.checkmarkRoot.SetActive(selected);

            if (ui.shadow != null)
            {
                ui.shadow.effectDistance = selected ? new Vector2(0f, -16f) : hovered ? new Vector2(0f, -12f) : new Vector2(0f, -8f);
                ui.shadow.effectColor = selected ? new Color(0f, 0f, 0f, 0.55f) : hovered ? new Color(0f, 0f, 0f, 0.45f) : new Color(0f, 0f, 0f, 0.30f);
            }
        }
    }

    private void AnimateScales()
    {
        for (int i = 0; i < _runtimeCards.Count; i++)
        {
            CardRuntimeUI ui = _runtimeCards[i];
            if (ui == null || ui.visual == null) continue;

            Vector3 current = ui.visual.localScale;
            Vector3 target = Vector3.one * ui.targetScale;
            ui.visual.localScale = Vector3.Lerp(current, target, Time.deltaTime * scaleLerpSpeed);
        }
    }

    private void SubscribeRayState()
    {
        if (_canvasRayInteractable == null || _subscribedToRayState) return;
        _canvasRayInteractable.WhenStateChanged += HandleCanvasRayStateChanged;
        _subscribedToRayState = true;
    }

    private void UnsubscribeRayState()
    {
        if (_canvasRayInteractable == null || !_subscribedToRayState) return;
        _canvasRayInteractable.WhenStateChanged -= HandleCanvasRayStateChanged;
        _subscribedToRayState = false;
    }

    private void HandleCanvasRayStateChanged(InteractableStateChangeArgs args)
    {
        if (!debugLogs) return;

        if (args.NewState == InteractableState.Hover && args.PreviousState == InteractableState.Normal)
            UDebug.Log("<color=#89CFF0><b>[MetaRayIntentCardMenu]</b></color> HandRayInteractor is hovering the menu canvas.");
        else if (args.NewState == InteractableState.Select)
            UDebug.Log("<color=yellow><b>[MetaRayIntentCardMenu]</b></color> HandRayInteractor selected on the menu canvas.");
    }

    private void HandleKeyboardDebug()
    {
        if (Input.GetKeyDown(previousCardKey)) SelectPreviousCard();
        if (Input.GetKeyDown(nextCardKey)) SelectNextCard();
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetIntent(IntentSelection.CalmRoom);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetIntent(IntentSelection.FastRoom);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetIntent(IntentSelection.HostRoom);
        if (Input.GetKeyDown(confirmKey) || Input.GetKeyDown(confirmKeyAlt)) ConfirmSelection();
    }

    private int GetCardCount()
    {
        if (cards != null && cards.Count > 0)
            return cards.Count;

        return Mathf.Max(1, fallbackCardCount);
    }

    private bool IsValidCardIndex(int index) => index >= 0 && index < _runtimeCards.Count;

    private MetaRayIntentCardData GetData(int index)
    {
        if (cards == null) return null;
        if (index < 0 || index >= cards.Count) return null;
        return cards[index];
    }

    private string GetTitle(int index)
    {
        MetaRayIntentCardData data = GetData(index);

        if (data != null && !string.IsNullOrWhiteSpace(data.title))
            return data.title;

        switch (index)
        {
            case 0: return "Calm Room";
            case 1: return "Fast Room";
            case 2: return "Host Room";
            default: return $"Room {index + 1}";
        }
    }

    private string GetSubtitle(int index)
    {
        MetaRayIntentCardData data = GetData(index);

        if (data != null && !string.IsNullOrWhiteSpace(data.subtitle))
            return data.subtitle;

        switch (index)
        {
            case 0: return "A quiet, soft and focused room mood.";
            case 1: return "A more energetic and dynamic room mood.";
            case 2: return "A warm room mood for guests and shared moments.";
            default: return "Select this room mood.";
        }
    }

    private Color GetFallbackBackgroundColor(int index)
    {
        MetaRayIntentCardData data = GetData(index);

        if (data != null && data.useCustomFallbackColor)
            return data.fallbackColor;

        switch (index % 3)
        {
            case 0: return cardFallbackColorA;
            case 1: return cardFallbackColorB;
            default: return cardFallbackColorC;
        }
    }

    private void ClearGeneratedObjects()
    {
        DestroyChildByName("Generated_MetaRayIntentMenu");
        DestroyChildByName("ISDK_RayInteractionSurface");

        if (_generatedRoot != null)
        {
            DestroySmart(_generatedRoot);
            _generatedRoot = null;
        }

        if (_raySurfaceRoot != null)
        {
            DestroySmart(_raySurfaceRoot);
            _raySurfaceRoot = null;
        }

        _canvasRayInteractable = null;
    }

    private void DestroyChildByName(string childName)
    {
        Transform child = transform.Find(childName);
        if (child != null)
            DestroySmart(child.gameObject);
    }

    private GameObject MakeUIObject(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = gameObject.layer;
        return go;
    }

    private static TextMeshProUGUI AddTMP(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        if (parent != null)
            go.layer = parent.gameObject.layer;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.enableAutoSizing = false;
        return tmp;
    }

    private static void StretchFull(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void StretchInset(GameObject go, float inset)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static void SetPreferredHeight(GameObject go, float height)
    {
        LayoutElement le = go.GetComponent<LayoutElement>();

        if (le == null)
            le = go.AddComponent<LayoutElement>();

        le.minHeight = height;
        le.preferredHeight = height;
        le.flexibleHeight = 0f;
    }

    private static T AddOrGet<T>(GameObject go) where T : UnityEngine.Component
    {
        T component = go.GetComponent<T>();

        if (component == null)
            component = go.AddComponent<T>();

        return component;
    }

    private static void DestroySmart(GameObject go)
    {
        if (go == null) return;

        if (UApplication.isPlaying)
            Destroy(go);
        else
            DestroyImmediate(go);
    }

    private static string CleanName(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "Untitled";

        foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
            input = input.Replace(invalid, '_');

        return input.Replace(" ", "_");
    }

    private static T FindAny<T>() where T : UnityEngine.Object
    {
#if UNITY_2022_2_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<T>();
#else
        return UnityEngine.Object.FindObjectOfType<T>();
#endif
    }

    private void ApplyRoundedImage(UIImage image, Color color, int radius)
    {
        if (image == null) return;

        if (useGeneratedRoundedCorners)
        {
            RoundedUISpriteUtility.ApplyRoundedCorners(image, color, Mathf.Clamp(radius, 1, 64));
        }
        else
        {
            image.sprite = null;
            image.type = UIImage.Type.Simple;
            image.color = color;
        }
    }

    private static void TryConfigureRectTransformBoundsClipperDriver(RectTransformBoundsClipperDriver driver, BoundsClipper boundsClipper)
    {
        if (driver == null || boundsClipper == null) return;

        RectTransform rectTransform = driver.GetComponent<RectTransform>();
        Type driverType = typeof(RectTransformBoundsClipperDriver);

        MethodInfo[] methods = driverType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (MethodInfo method in methods)
        {
            if (!method.Name.StartsWith("Inject", StringComparison.Ordinal)) continue;

            ParameterInfo[] parameters = method.GetParameters();
            object[] args = new object[parameters.Length];
            bool canUseMethod = true;

            for (int i = 0; i < parameters.Length; i++)
            {
                Type p = parameters[i].ParameterType;

                if (p.IsAssignableFrom(typeof(BoundsClipper)))
                    args[i] = boundsClipper;
                else if (p.IsAssignableFrom(typeof(RectTransform)))
                    args[i] = rectTransform;
                else if (p.IsAssignableFrom(typeof(Transform)))
                    args[i] = rectTransform;
                else
                {
                    canUseMethod = false;
                    break;
                }
            }

            if (!canUseMethod) continue;

            try
            {
                method.Invoke(driver, args);
                break;
            }
            catch
            {
                // Version-specific SDK differences are handled by field fallback below.
            }
        }

        SetFieldIfExists(driver, "_boundsClipper", boundsClipper);
        SetFieldIfExists(driver, "_rectTransform", rectTransform);

        MethodInfo resizeMethod = driverType.GetMethod("Resize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        try
        {
            resizeMethod?.Invoke(driver, null);
        }
        catch
        {
            // Non-fatal.
        }
    }

    private static void SetFieldIfExists(object target, string fieldName, object value)
    {
        if (target == null) return;

        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field == null) return;

        try
        {
            field.SetValue(target, value);
        }
        catch
        {
            // Ignore version-specific field restrictions.
        }
    }
}

/// <summary>
/// This is included in the same file so Unity can always find it.
/// It handles hover and click events for each generated card.
/// </summary>
public class MetaRayIntentCardHitbox : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerClickHandler
{
    [SerializeField] private MetaRayIntentCardMenu menu;
    [SerializeField] private int cardIndex;

    public void Initialize(MetaRayIntentCardMenu owner, int index)
    {
        menu = owner;
        cardIndex = index;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (menu == null) return;
        menu.SetHoveredCard(cardIndex, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (menu == null) return;
        menu.SetHoveredCard(cardIndex, false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (menu == null) return;

        if (menu.selectCardOnPointerDown)
            menu.SelectCard(cardIndex);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (menu == null) return;

        if (menu.selectCardOnPointerClick)
            menu.SelectCard(cardIndex);
    }
}