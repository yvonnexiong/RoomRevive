using System;
using System.Collections;
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

namespace RoomRevive
{
    /// <summary>
    /// Intent selector panel — 3 cards:
    /// 0 = Calm & Unwind
    /// 1 = Host & Gather
    /// 2 = Fast & Focused
    ///
    /// Keeps the original IntentCardSelectorUI layout/style:
    /// - Header pill
    /// - Three cards
    /// - Image area
    /// - Label strip
    /// - Icon slot
    /// - Original gradients
    /// - Original typography and spacing
    ///
    /// Adds functionality from MetaRayIntentCardMenu:
    /// - Meta Interaction SDK ray support
    /// - PointableCanvas
    /// - RayInteractable surface
    /// - Hover / press / selection animation
    /// - Keyboard debug navigation
    /// - SplatManager hook
    /// - AudioManager hook
    /// - Host-only object visibility
    ///
    /// Selection visual:
    /// - Selected card stays fully visible
    /// - Non-selected cards become greyed out
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public class IntentCardSelectorUI : MonoBehaviour
    {
        public enum SplatCardAction
        {
            AutoByIndex,
            None,
            CalmRoom,
            HostRoom,
            FastRoom
        }

        [Serializable] public class IntentIndexEvent : UnityEvent<int> { }
        [Serializable] public class SplatActionEvent : UnityEvent<SplatCardAction> { }

        // ─────────────────────────────────────────────────────────────────────
        // Original design tokens
        // ─────────────────────────────────────────────────────────────────────

        static readonly Color PanelBgA = Hex(0x6B7080);
        static readonly Color PanelBgB = Hex(0x8B8E9A);
        static readonly Color PanelBgC = Hex(0x5C6270);

        static readonly Color HeaderBg = new Color(0x73 / 255f, 0x7E / 255f, 0x9C / 255f, 195 / 255f);
        static readonly Color HeaderBorder = new Color(1f, 1f, 1f, 0.25f);

        static readonly Color TitleColor = Hex(0x2D3245);
        static readonly Color SubColor = Hex(0x4A5068);

        static readonly Color LabelBg = new Color(0x73 / 255f, 0x7E / 255f, 0x9C / 255f, 0.55f);
        static readonly Color CardText = Color.white;
        static readonly Color CardTextDim = new Color(1f, 1f, 1f, 0.85f);
        static readonly Color IconBorder = Hex(0x737E9C);

        static readonly Color[] GradCalm = { Hex(0xE8DFCE), Hex(0xD4C8AC), Hex(0xB8A582) };
        static readonly Color[] GradHost = { Hex(0x5A4030), Hex(0x3D2A1C), Hex(0x2A1C12) };
        static readonly Color[] GradFast = { Hex(0xC8CDD5), Hex(0xA8AEB8), Hex(0x88909C) };
        static readonly float[] GradStops = { 0f, 0.6f, 1f };

        const int CardCount = 3;

        /// <summary>
        /// Compatibility event from original IntentCardSelectorUI.
        /// 0 = Calm, 1 = Host, 2 = Fast
        /// </summary>
        public static Action<int> OnIntentSelected;

        // ─────────────────────────────────────────────────────────────────────
        // Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("Selected Card")]
        [CardIndexDropdown]
        [Tooltip("Index into the Cards list. Dropdown is populated dynamically from the card titles.")]
        public int selectedCardIndex = 0;
        public bool previewSplatManagerInEditorFromSelectedIntent = true;

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
        public bool callSplatManagerForStartSelection = true;

        [Header("Cards")]
        [Tooltip("ScriptableObject card data assets. Element order is the visual order. Leave empty to use the fallback Calm / Host / Fast defaults.")]
        public List<MetaRayIntentCardData> cards = new List<MetaRayIntentCardData>();

        [Header("Events")]
        public IntentIndexEvent onSelectionChanged = new IntentIndexEvent();
        public IntentIndexEvent onConfirmed = new IntentIndexEvent();
        public SplatActionEvent onSplatActionInvoked = new SplatActionEvent();

        [Header("Canvas")]
        public float worldScale = 0.001f;
        public Camera eventCamera;

        [Header("Meta Ray Interaction")]
        public bool autoCreateEventSystem = true;
        public PlaneSurface.NormalFacing raySurfaceFacing = PlaneSurface.NormalFacing.Backward;

        [Header("Head Follow")]
        public bool followHeadInPlayMode = true;
        public float distance = 1.4f;
        public float rightOffset = 0f;
        public float upOffset = -0.1f;

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

        [Header("Layout (px)")]
        public float cardWidth = 260f;
        public float cardGap = 24f;
        public float panelPadH = 56f;
        public float panelPadSide = 48f;
        public float imageAspect = 1.1f;
        public float labelAreaH = 86f;
        public float cardRadius = 8f;

        [Header("Card Scale Animation")]
        [Range(0.75f, 1.00f)] public float normalScale = 1.00f;
        [Range(0.90f, 1.15f)] public float hoverScale = 1.03f;
        [Range(1.00f, 1.25f)] public float selectedScale = 1.08f;
        public float scaleLerpSpeed = 14f;

        [Header("Selection Grey-Out Effect")]
        [Tooltip("When a card is selected, every non-selected card receives this grey overlay.")]
        public Color greyedOutOverlayColor = new Color(0.25f, 0.25f, 0.25f, 1f);

        [Range(0f, 1f)]
        public float greyedOutOverlayAlpha = 0.48f;

        [Tooltip("Optional: make a non-selected card slightly less grey when hovering it.")]
        public bool reduceGreyOnHoveredUnselectedCard = true;

        [Range(0f, 1f)]
        public float greyedOutHoveredAlpha = 0.36f;

        [Range(0f, 1f)]
        public float greyedOutPressedAlpha = 0.56f;

        [Header("Hover / Press Overlay Animation Before Selection")]
        [Tooltip("Only used before a card has been selected.")]
        public Color preSelectionHoverOverlayColor = Color.white;

        [Range(0f, 0.5f)]
        public float hoverOverlayAlpha = 0.18f;

        [Range(0f, 0.5f)]
        public float pressedOverlayAlpha = 0.32f;

        public float overlayLerpSpeed = 18f;

        [Header("Font")]
        public TMP_FontAsset fontAsset;

        [Header("Card Images (optional — overrides gradient)")]
        public Sprite imageCalm;
        public Sprite imageHost;
        public Sprite imageFast;

        [Header("Card Icons (optional — replaces text placeholder)")]
        public Sprite iconCalm;
        public Sprite iconHost;
        public Sprite iconFast;

        [Header("Image Behaviour")]
        [Tooltip("False keeps the exact original behaviour. True prevents stretching by preserving the sprite aspect ratio.")]
        public bool preserveCardImageAspect = false;

        [Header("Debug")]
        public bool debugLogs = true;

        [SerializeField, HideInInspector] private GameObject _generatedRoot;
        [SerializeField, HideInInspector] private GameObject _raySurfaceRoot;
        [SerializeField, HideInInspector] private PointableCanvas _pointableCanvas;
        [SerializeField, HideInInspector] private RayInteractable _canvasRayInteractable;

        Canvas _canvas;
        CanvasScaler _scaler;
        GraphicRaycaster _graphicRaycaster;
        CanvasGroup _cg;
        RectTransform _rectTransform;

        Transform _cam;
        Coroutine _fade;

        readonly List<CardRuntimeUI> _runtimeCards = new List<CardRuntimeUI>();
        readonly List<Texture2D> _generatedTextures = new List<Texture2D>();

        int _selectedIndex = -1;
        int _hoveredIndex = -1;
        int _pressedIndex = -1;

        bool _subscribedToRayState;

#if UNITY_EDITOR
        bool _rebuildQueued;
#endif

        [Serializable]
        private class CardRuntimeUI
        {
            public int index;
            public GameObject root;
            public RectTransform visual;
            public UIImage hitImage;
            public Button button;
            public UIImage maskImage;
            public UIImage imageArea;
            public UIImage labelImage;
            public UIImage stateOverlay;
            public TextMeshProUGUI titleText;
            public TextMeshProUGUI taglineText;
            public float targetScale;
            public float targetOverlayAlpha;
            public Color targetOverlayColor;
        }

        private class IntentCardPointerProxy : MonoBehaviour,
            IPointerEnterHandler,
            IPointerExitHandler,
            IPointerDownHandler,
            IPointerUpHandler,
            IPointerClickHandler
        {
            IntentCardSelectorUI _owner;
            int _index;

            public void Initialize(IntentCardSelectorUI owner, int index)
            {
                _owner = owner;
                _index = index;
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                if (_owner != null)
                    _owner.SetHoveredCard(_index, true);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                if (_owner != null)
                {
                    _owner.SetHoveredCard(_index, false);
                    _owner.SetPressedCard(_index, false);
                }
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                if (_owner == null) return;

                _owner.SetPressedCard(_index, true);

                if (_owner.selectCardOnPointerDown)
                    _owner.SelectCard(_index);
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                if (_owner != null)
                    _owner.SetPressedCard(_index, false);
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (_owner != null && _owner.selectCardOnPointerClick)
                    _owner.SelectCard(_index);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Reset()
        {
            GrabRequiredComponents();
            TryAutoFindSplatManager();
            TryAutoFindAudioManager();
            SyncStartIndexFromIntentEnum();
            ConfigureCanvas();
        }

        void Awake()
        {
            GrabRequiredComponents();
            TryAutoFindSplatManager();
            TryAutoFindAudioManager();
            SyncStartIndexFromIntentEnum();
            ConfigureCanvas();
        }

        void OnEnable()
        {
            GrabRequiredComponents();
            TryAutoFindSplatManager();
            TryAutoFindAudioManager();
            SyncStartIndexFromIntentEnum();

            if (UApplication.isPlaying)
            {
                FindHeadCamera();

                if (playRoomMusicWhenIntentUIIsEnabled)
                    PlayAudioForCurrentIntent();
            }
        }

        void Start()
        {
            if (!UApplication.isPlaying) return;

            FindHeadCamera();
            TryAutoFindSplatManager();
            TryAutoFindAudioManager();
            SyncStartIndexFromIntentEnum();

            Rebuild();
            SubscribeRayState();

            int startIndex = GetStartSelectionIndex();

            if (selectCardOnStart && IsValidCardIndex(startIndex))
            {
                ApplySelection(startIndex, callSplatManagerForStartSelection);
            }
            else
            {
                if (playRoomMusicWhenIntentUIIsEnabled)
                    PlayAudioForCurrentIntent();
            }
        }

        void Update()
        {
            if (!UApplication.isPlaying) return;

            AnimateCards();

            if (keyboardDebug)
                HandleKeyboardDebug();
        }

        void LateUpdate()
        {
            if (!UApplication.isPlaying || !followHeadInPlayMode || _cam == null) return;
            if (_cg != null && _cg.alpha < 0.01f) return;

            transform.position =
                _cam.position +
                _cam.forward * distance +
                _cam.right * rightOffset +
                _cam.up * upOffset;

            transform.rotation = Quaternion.LookRotation(_cam.forward);
        }

        void OnDisable()
        {
            UnsubscribeRayState();
        }

        void OnDestroy()
        {
            UnsubscribeRayState();
            ClearGeneratedTextures();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (UApplication.isPlaying) return;

            TryAutoFindSplatManager();
            TryAutoFindAudioManager();
            SyncStartIndexFromIntentEnum();

            ApplyIntentEnumSelectionInEditor(previewSplatManagerInEditorFromSelectedIntent);
        }

        internal void QueueEditorRebuild()
        {
            if (_rebuildQueued) return;

            _rebuildQueued = true;

            EditorApplication.delayCall += () =>
            {
                _rebuildQueued = false;

                if (this == null || gameObject == null) return;
                if (UApplication.isPlaying) return;

                Rebuild();
                ApplyIntentEnumSelectionInEditor(previewSplatManagerInEditorFromSelectedIntent);
            };
        }
#endif

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        [ContextMenu("Rebuild UI")]
        public void Rebuild()
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
            _pressedIndex = -1;
            _selectedIndex = -1;

            _generatedRoot = MakeUIObject("Generated_IntentSelector", transform);
            StretchFull(_generatedRoot);

            BuildRayInteractionSurface();
            BuildPanel(_generatedRoot.transform);

            ApplyFontToGeneratedUI();
            RefreshVisuals(true);

            if (!UApplication.isPlaying)
                ApplyIntentEnumSelectionVisualOnlyInEditor();

            if (UApplication.isPlaying)
                SubscribeRayState();

            if (debugLogs)
                UDebug.Log($"<b>[IntentCardSelectorUI]</b> Built {_runtimeCards.Count} ray-interactable intent card(s).", this);
        }

        public void Show()
        {
            if (_generatedRoot == null) Rebuild();

            if (_fade != null)
                StopCoroutine(_fade);

            _fade = StartCoroutine(FadeRoutine(true));
        }

        public void Hide()
        {
            if (_fade != null)
                StopCoroutine(_fade);

            _fade = StartCoroutine(FadeRoutine(false));
        }

        public void SelectCard(int index)
        {
            ApplySelection(index, true);
        }

        public void SelectPreviousCard()
        {
            SelectRelativeCard(-1, arrowKeysInvokeSelection);
        }

        public void SelectNextCard()
        {
            SelectRelativeCard(1, arrowKeysInvokeSelection);
        }

        public void SetIntent(int index)
        {
            selectedCardIndex = index;

            if (UApplication.isPlaying)
                ApplySelection(index, true);
            else
                ApplyIntentEnumSelectionInEditor(previewSplatManagerInEditorFromSelectedIntent);
        }

        public void PlayAudioForCurrentIntent()
        {
            PlayAudioForCard(GetStartSelectionIndex(), true);
        }

        public void ConfirmSelection()
        {
            if (!IsValidCardIndex(_selectedIndex))
            {
                if (debugLogs)
                    UDebug.LogWarning("[IntentCardSelectorUI] Nothing selected. Hover/pinch-select a card first.", this);

                return;
            }

            if (callSplatManagerOnConfirm)
                InvokeSplatManagerForCard(_selectedIndex);

            MetaRayIntentCardData data = GetCardConfig(_selectedIndex);
            data?.onConfirmed?.Invoke();

            onConfirmed?.Invoke(_selectedIndex);

            if (debugLogs)
                UDebug.Log($"<color=lime><b>[IntentCardSelectorUI]</b></color> Confirmed card {_selectedIndex}: {GetTitle(_selectedIndex)}", this);
        }

        public void InvokeSelectedSplatRoom()
        {
            if (IsValidCardIndex(_selectedIndex))
                InvokeSplatManagerForCard(_selectedIndex);
        }

        public void SetHoveredCard(int index, bool isHovered)
        {
            if (isHovered)
                _hoveredIndex = index;
            else if (_hoveredIndex == index)
                _hoveredIndex = -1;

            RefreshVisuals(false);
        }

        public void SetPressedCard(int index, bool isPressed)
        {
            if (isPressed)
                _pressedIndex = index;
            else if (_pressedIndex == index)
                _pressedIndex = -1;

            RefreshVisuals(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Build — original visual layout
        // ─────────────────────────────────────────────────────────────────────

        void BuildPanel(Transform root)
        {
            float imageH = cardWidth / imageAspect;
            float cardH = imageH + labelAreaH;

            float headerPillH = 22f + 36f + 10f + 22f + 22f;
            float panelW = cardWidth * 3f + cardGap * 2f + panelPadSide * 2f;
            float panelH = panelPadH + headerPillH + 40f + cardH + panelPadH;

            GameObject panel = MakeUIObject("Panel", root);
            RectTransform pRT = panel.GetComponent<RectTransform>();
            pRT.anchorMin = pRT.anchorMax = pRT.pivot = new Vector2(0.5f, 0.5f);
            pRT.sizeDelta = new Vector2(panelW, panelH);
            pRT.anchoredPosition = Vector2.zero;

            float y = -panelPadH;

            // Header pill
            float pillW = panelW - panelPadSide * 2f;

            GameObject pillGO = MakeUIObject("HeaderPill", panel.transform);
            RectTransform pillRT = pillGO.GetComponent<RectTransform>();
            pillRT.anchorMin = pillRT.anchorMax = new Vector2(0.5f, 1f);
            pillRT.pivot = new Vector2(0.5f, 1f);
            pillRT.sizeDelta = new Vector2(pillW, headerPillH);
            pillRT.anchoredPosition = new Vector2(0f, y);

            UIImage pillImg = pillGO.AddComponent<UIImage>();
            pillImg.color = HeaderBg;
            pillImg.raycastTarget = false;
            Round(pillImg, 8f);

            GameObject pillBorder = MakeUIObject("PillBorder", pillGO.transform);
            StretchFull(pillBorder);

            UIImage pbImg = pillBorder.AddComponent<UIImage>();
            pbImg.color = HeaderBorder;
            pbImg.raycastTarget = false;
            Round(pbImg, 8f);

            // Kept from original script. Disabled because the original did not show it.
            pbImg.enabled = false;

            float pillInnerW = pillW - 56f * 2f;

            TextMeshProUGUI title = AbsText(
                "Choose how you want to live",
                pillGO.transform,
                new Vector2(pillInnerW, 40f),
                new Vector2(0f, -22f),
                30f,
                Color.white
            );

            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            title.enableWordWrapping = false;

            TextMeshProUGUI sub = AbsText(
                "Pick the feeling. We'll shape your kitchen around it.",
                pillGO.transform,
                new Vector2(pillInnerW, 24f),
                new Vector2(0f, -22f - 36f - 10f),
                14f,
                Color.white
            );

            sub.alignment = TextAlignmentOptions.Center;
            sub.enableWordWrapping = true;

            y -= headerPillH + 40f;

            float totalCardsW = cardWidth * 3f + cardGap * 2f;

            for (int i = 0; i < CardCount; i++)
            {
                float x = -totalCardsW * 0.5f + i * (cardWidth + cardGap) + cardWidth * 0.5f;
                BuildCard(panel.transform, i, new Vector2(x, y), cardH);
            }
        }

        void BuildCard(Transform parent, int index, Vector2 pos, float cardH)
        {
            float imageH = cardWidth / imageAspect;

            CardRuntimeUI ui = new CardRuntimeUI();
            ui.index = index;
            ui.targetScale = normalScale;
            ui.targetOverlayAlpha = 0f;
            ui.targetOverlayColor = preSelectionHoverOverlayColor;

            GameObject cardGO = MakeUIObject($"Card_{index}_{CleanName(GetTitle(index))}", parent);
            RectTransform cRT = cardGO.GetComponent<RectTransform>();
            cRT.anchorMin = cRT.anchorMax = new Vector2(0.5f, 1f);
            cRT.pivot = new Vector2(0.5f, 1f);
            cRT.sizeDelta = new Vector2(cardWidth, cardH);
            cRT.anchoredPosition = pos;

            ui.root = cardGO;

            ui.hitImage = cardGO.AddComponent<UIImage>();
            ui.hitImage.color = new Color(1f, 1f, 1f, 0.001f);
            ui.hitImage.raycastTarget = true;

            if (addButtonComponentToCards)
            {
                ui.button = cardGO.AddComponent<Button>();
                ui.button.targetGraphic = ui.hitImage;
                ui.button.transition = Selectable.Transition.None;
                ui.button.navigation = new Navigation { mode = Navigation.Mode.None };
            }

            IntentCardPointerProxy proxy = cardGO.AddComponent<IntentCardPointerProxy>();
            proxy.Initialize(this, index);

            // Visual is scaled while root keeps stable hit area.
            GameObject visualGO = MakeUIObject("Visual", cardGO.transform);
            RectTransform vRT = visualGO.GetComponent<RectTransform>();
            vRT.anchorMin = vRT.anchorMax = new Vector2(0.5f, 1f);
            vRT.pivot = new Vector2(0.5f, 1f);
            vRT.sizeDelta = new Vector2(cardWidth, cardH);
            vRT.anchoredPosition = Vector2.zero;
            vRT.localScale = Vector3.one * normalScale;

            ui.visual = vRT;

            ui.maskImage = visualGO.AddComponent<UIImage>();
            ui.maskImage.color = Color.white;
            ui.maskImage.raycastTarget = false;
            Round(ui.maskImage, cardRadius);

            Mask mask = visualGO.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // Image area
            GameObject imgGO = MakeUIObject("ImageArea", visualGO.transform);
            RectTransform iRT = imgGO.GetComponent<RectTransform>();
            iRT.anchorMin = iRT.anchorMax = new Vector2(0.5f, 1f);
            iRT.pivot = new Vector2(0.5f, 1f);
            iRT.sizeDelta = new Vector2(cardWidth, imageH);
            iRT.anchoredPosition = Vector2.zero;

            UIImage imgBg = imgGO.AddComponent<UIImage>();
            imgBg.color = Color.white;
            imgBg.raycastTarget = false;
            imgBg.type = Image.Type.Simple;
            imgBg.preserveAspect = preserveCardImageAspect;

            Sprite sprite = GetImage(index);
            imgBg.sprite = sprite != null
                ? sprite
                : LinearGradientSprite(
                    Mathf.RoundToInt(cardWidth),
                    Mathf.RoundToInt(imageH),
                    160f,
                    GetGradient(index),
                    GradStops,
                    0f
                );

            ui.imageArea = imgBg;

            // Label area
            GameObject lblGO = MakeUIObject("LabelArea", visualGO.transform);
            RectTransform lRT = lblGO.GetComponent<RectTransform>();
            lRT.anchorMin = lRT.anchorMax = new Vector2(0.5f, 1f);
            lRT.pivot = new Vector2(0.5f, 1f);
            lRT.sizeDelta = new Vector2(cardWidth, labelAreaH);
            lRT.anchoredPosition = new Vector2(0f, -imageH);

            UIImage lblBg = lblGO.AddComponent<UIImage>();
            lblBg.color = LabelBg;
            lblBg.raycastTarget = false;
            ui.labelImage = lblBg;

            float padH = 16f;
            float innerW = cardWidth - padH * 2f;

            // Icon circle
            const float iconSize = 28f;

            GameObject iconGO = MakeUIObject("IconCircle", lblGO.transform);
            RectTransform iconRT = iconGO.GetComponent<RectTransform>();
            iconRT.anchorMin = iconRT.anchorMax = new Vector2(0f, 1f);
            iconRT.pivot = new Vector2(0f, 1f);
            iconRT.sizeDelta = new Vector2(iconSize, iconSize);
            iconRT.anchoredPosition = new Vector2(padH, -padH);

            UIImage iconBg = iconGO.AddComponent<UIImage>();
            iconBg.color = IconBorder;
            iconBg.raycastTarget = false;
            Round(iconBg, iconSize * 0.5f);

            // Kept from original script.
            iconBg.enabled = false;

            GameObject iconContent = MakeUIObject("IconContent", iconGO.transform);
            StretchFull(iconContent);

            UIImage icImg = iconContent.AddComponent<UIImage>();
            icImg.raycastTarget = false;

            Sprite iconSprite = GetIcon(index);

            if (iconSprite != null)
            {
                icImg.sprite = iconSprite;
                icImg.color = Color.white;
                icImg.type = Image.Type.Simple;
                icImg.preserveAspect = true;
            }
            else
            {
                icImg.color = new Color(0f, 0f, 0f, 0f);
            }

            // Title
            float titleX = 35f;
            float titleW = cardWidth - titleX - padH;

            TextMeshProUGUI titleText = AbsText(
                GetTitle(index),
                lblGO.transform,
                new Vector2(titleW, iconSize),
                new Vector2(titleX, -padH),
                17f,
                CardText
            );

            titleText.fontStyle = FontStyles.Bold;
            titleText.verticalAlignment = VerticalAlignmentOptions.Middle;
            titleText.overflowMode = TextOverflowModes.Ellipsis;
            ui.titleText = titleText;

            // Tagline
            TextMeshProUGUI tagText = AbsText(
                GetTagline(index),
                lblGO.transform,
                new Vector2(innerW, 26f),
                new Vector2(padH + iconSize + 10f - 10f, -padH - iconSize - 6f),
                12f,
                CardTextDim
            );

            tagText.enableWordWrapping = true;
            tagText.overflowMode = TextOverflowModes.Ellipsis;
            ui.taglineText = tagText;

            // State overlay:
            // - Before selection: hover/press feedback
            // - After selection: non-selected cards greyed out
            // - Selected card stays fully visible
            GameObject overlayGO = MakeUIObject("StateOverlay_GreyOutHoverPress", visualGO.transform);
            StretchFull(overlayGO);

            UIImage overlayImg = overlayGO.AddComponent<UIImage>();
            overlayImg.color = new Color(preSelectionHoverOverlayColor.r, preSelectionHoverOverlayColor.g, preSelectionHoverOverlayColor.b, 0f);
            overlayImg.raycastTarget = false;

            ui.stateOverlay = overlayImg;

            _runtimeCards.Add(ui);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Selection / visuals / animation
        // ─────────────────────────────────────────────────────────────────────

        void ApplySelection(int index, bool invokeEvents)
        {
            if (!IsValidCardIndex(index)) return;

            bool changed = _selectedIndex != index;

            _selectedIndex = index;

            SyncIntentEnumFromCardIndex(index);
            RefreshVisuals(false);

            if (debugLogs)
                UDebug.Log($"<color=yellow><b>[IntentCardSelectorUI]</b></color> Selected card {index}: {GetTitle(index)}", this);

            if (!invokeEvents) return;

            if (!changed && !reapplySameSplatIfClickedAgain && !reapplySameMusicIfClickedAgain)
                return;

            if (callSplatManagerOnSelect && (changed || reapplySameSplatIfClickedAgain))
                InvokeSplatManagerForCard(index);

            if (playRoomMusicOnSelect && (changed || reapplySameMusicIfClickedAgain))
                PlayAudioForCard(index, false);

            MetaRayIntentCardData data = GetCardConfig(index);
            data?.onSelected?.Invoke();

            OnIntentSelected?.Invoke(index);
            onSelectionChanged?.Invoke(index);
        }

        void RefreshVisuals(bool instant)
        {
            bool hasSelection = IsValidCardIndex(_selectedIndex);

            for (int i = 0; i < _runtimeCards.Count; i++)
            {
                CardRuntimeUI ui = _runtimeCards[i];
                if (ui == null || ui.root == null) continue;

                bool selected = hasSelection && i == _selectedIndex;
                bool hovered = i == _hoveredIndex;
                bool pressed = i == _pressedIndex;

                ui.targetScale = selected ? selectedScale : hovered ? hoverScale : normalScale;

                if (hasSelection)
                {
                    if (selected)
                    {
                        // Inverted selection effect:
                        // selected card is fully visible with no grey/white overlay.
                        ui.targetOverlayColor = greyedOutOverlayColor;
                        ui.targetOverlayAlpha = 0f;
                    }
                    else
                    {
                        // Non-selected cards are greyed out.
                        ui.targetOverlayColor = greyedOutOverlayColor;

                        if (pressed)
                        {
                            ui.targetOverlayAlpha = greyedOutPressedAlpha;
                        }
                        else if (hovered && reduceGreyOnHoveredUnselectedCard)
                        {
                            ui.targetOverlayAlpha = greyedOutHoveredAlpha;
                        }
                        else
                        {
                            ui.targetOverlayAlpha = greyedOutOverlayAlpha;
                        }
                    }
                }
                else
                {
                    // Before any card has been selected, keep the original simple hover/press feedback.
                    ui.targetOverlayColor = preSelectionHoverOverlayColor;

                    if (pressed)
                        ui.targetOverlayAlpha = pressedOverlayAlpha;
                    else if (hovered)
                        ui.targetOverlayAlpha = hoverOverlayAlpha;
                    else
                        ui.targetOverlayAlpha = 0f;
                }

                if (instant || !UApplication.isPlaying)
                {
                    if (ui.visual != null)
                        ui.visual.localScale = Vector3.one * ui.targetScale;

                    if (ui.stateOverlay != null)
                    {
                        Color c = ui.targetOverlayColor;
                        c.a = ui.targetOverlayAlpha;
                        ui.stateOverlay.color = c;
                    }
                }
            }
        }

        void AnimateCards()
        {
            for (int i = 0; i < _runtimeCards.Count; i++)
            {
                CardRuntimeUI ui = _runtimeCards[i];
                if (ui == null) continue;

                if (ui.visual != null)
                {
                    Vector3 current = ui.visual.localScale;
                    Vector3 target = Vector3.one * ui.targetScale;
                    ui.visual.localScale = Vector3.Lerp(current, target, Time.deltaTime * scaleLerpSpeed);
                }

                if (ui.stateOverlay != null)
                {
                    Color current = ui.stateOverlay.color;
                    Color target = ui.targetOverlayColor;
                    target.a = ui.targetOverlayAlpha;

                    current.r = Mathf.Lerp(current.r, target.r, Time.deltaTime * overlayLerpSpeed);
                    current.g = Mathf.Lerp(current.g, target.g, Time.deltaTime * overlayLerpSpeed);
                    current.b = Mathf.Lerp(current.b, target.b, Time.deltaTime * overlayLerpSpeed);
                    current.a = Mathf.Lerp(current.a, target.a, Time.deltaTime * overlayLerpSpeed);

                    ui.stateOverlay.color = current;
                }
            }
        }

        void SelectRelativeCard(int direction, bool invokeEvents)
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

        // ─────────────────────────────────────────────────────────────────────
        // Splat / audio / host visibility
        // ─────────────────────────────────────────────────────────────────────

        void TryAutoFindSplatManager()
        {
            if (!autoFindSplatManager || splatManager != null) return;
            splatManager = FindAny<SplatManager>();
        }

        void TryAutoFindAudioManager()
        {
            if (!autoFindAudioManager || audioManager != null) return;
            audioManager = FindAny<AudioManager>();
        }

        void SyncStartIndexFromIntentEnum()
        {
            // selectedCardIndex IS the start index now — nothing to sync.
        }

        int GetStartSelectionIndex()
        {
            return selectedCardIndex;
        }

        void SyncIntentEnumFromCardIndex(int index)
        {
            selectedCardIndex = index;
        }

        void ApplyIntentEnumSelectionVisualOnlyInEditor()
        {
            if (UApplication.isPlaying) return;

            int index = GetStartSelectionIndex();

            _selectedIndex = index;
            _hoveredIndex = -1;
            _pressedIndex = -1;

            if (IsValidCardIndex(index))
                RefreshVisuals(true);
        }

        void ApplyIntentEnumSelectionInEditor(bool invokeSplatManager)
        {
            if (UApplication.isPlaying) return;

            int index = GetStartSelectionIndex();

            _selectedIndex = index;
            _hoveredIndex = -1;
            _pressedIndex = -1;

            if (IsValidCardIndex(index))
                RefreshVisuals(true);

            if (invokeSplatManager && IsValidCardIndex(index))
                InvokeSplatManagerForCard(index);

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        void InvokeSplatManagerForCard(int index)
        {
            SplatCardAction action = GetSplatAction(index);

            if (action == SplatCardAction.None || action == SplatCardAction.AutoByIndex)
            {
                if (debugLogs)
                    UDebug.LogWarning($"[IntentCardSelectorUI] Card {index} has no valid SplatManager action.", this);

                return;
            }

            if (splatManager == null)
                TryAutoFindSplatManager();

            if (splatManager == null)
            {
                UDebug.LogError($"[IntentCardSelectorUI] Cannot invoke {action}. No SplatManager assigned or found in scene.", this);
                return;
            }

            switch (action)
            {
                case SplatCardAction.CalmRoom:
                    splatManager.SetCalmRoom();
                    break;

                case SplatCardAction.HostRoom:
                    splatManager.SetHostRoom();
                    break;

                case SplatCardAction.FastRoom:
                    splatManager.SetFastRoom();
                    break;
            }

            onSplatActionInvoked?.Invoke(action);

            if (debugLogs)
                UDebug.Log($"<color=lime><b>[IntentCardSelectorUI]</b></color> Invoked SplatManager action: {action}", this);
        }

        void PlayAudioForCard(int index, bool ignoreSelectFlag)
        {
            if (!ignoreSelectFlag && !playRoomMusicOnSelect && UApplication.isPlaying)
                return;

            if (audioManager == null)
                TryAutoFindAudioManager();

            if (audioManager == null)
            {
                if (debugLogs)
                    UDebug.LogWarning("[IntentCardSelectorUI] No AudioManager assigned or found. Room music not changed.", this);

                return;
            }

            SplatManager.SplatRoom room = GetSplatRoomForCard(index);

            if (room == SplatManager.SplatRoom.None)
                return;

            audioManager.PlayAtmosphereForRoom(room);
        }

        SplatManager.SplatRoom GetSplatRoomForCard(int index)
        {
            SplatCardAction action = GetSplatAction(index);

            switch (action)
            {
                case SplatCardAction.CalmRoom:
                    return SplatManager.SplatRoom.CalmRoom;

                case SplatCardAction.HostRoom:
                    return SplatManager.SplatRoom.HostRoom;

                case SplatCardAction.FastRoom:
                    return SplatManager.SplatRoom.FastRoom;

                default:
                    return SplatManager.SplatRoom.None;
            }
        }

        SplatCardAction GetSplatAction(int index)
        {
            MetaRayIntentCardData data = GetCardConfig(index);

            if (data != null && data.splatAction != MetaRayIntentCardMenu.SplatCardAction.AutoByIndex)
                return ConvertSplatAction(data.splatAction);

            switch (index)
            {
                case 0:
                    return SplatCardAction.CalmRoom;

                case 1:
                    return SplatCardAction.HostRoom;

                case 2:
                    return SplatCardAction.FastRoom;

                default:
                    return SplatCardAction.None;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Canvas / Meta ray setup
        // ─────────────────────────────────────────────────────────────────────

        void GrabRequiredComponents()
        {
            _canvas = GetComponent<Canvas>();
            _scaler = GetComponent<CanvasScaler>();
            _graphicRaycaster = GetComponent<GraphicRaycaster>();
            _rectTransform = GetComponent<RectTransform>();

            _cg = GetComponent<CanvasGroup>();

            if (_cg == null)
                _cg = gameObject.AddComponent<CanvasGroup>();
        }

        void ConfigureCanvas()
        {
            if (_canvas == null) return;

            float imageH = cardWidth / imageAspect;
            float cardH = imageH + labelAreaH;
            float pillH = 22f + 36f + 10f + 22f + 22f;

            float panelW = cardWidth * 3f + cardGap * 2f + panelPadSide * 2f;
            float panelH = panelPadH + pillH + 40f + cardH + panelPadH;

            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = eventCamera != null ? eventCamera : Camera.main;

            transform.localScale = Vector3.one * worldScale;

            if (_rectTransform != null)
            {
                _rectTransform.sizeDelta = new Vector2(panelW + 40f, panelH + 40f);
                _rectTransform.pivot = Vector2.one * 0.5f;
            }

            if (_scaler != null)
            {
                _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                _scaler.referencePixelsPerUnit = 100f;
                _scaler.dynamicPixelsPerUnit = 10f;
            }

            if (_graphicRaycaster != null)
            {
                _graphicRaycaster.ignoreReversedGraphics = true;
                _graphicRaycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
            }
        }

        void EnsurePointableCanvas()
        {
            _pointableCanvas = GetComponent<PointableCanvas>();

            if (_pointableCanvas == null)
                _pointableCanvas = gameObject.AddComponent<PointableCanvas>();

            if (_canvas != null)
                _pointableCanvas.InjectAllPointableCanvas(_canvas);
        }

        void EnsureEventSystemAndPointableModule()
        {
            if (!autoCreateEventSystem) return;

            EventSystem eventSystem = FindAny<EventSystem>();

            if (eventSystem == null)
            {
                GameObject eventSystemGO = new GameObject("EventSystem - Meta Pointable Canvas");
                eventSystem = eventSystemGO.AddComponent<EventSystem>();

                if (debugLogs)
                    UDebug.Log("<b>[IntentCardSelectorUI]</b> Created EventSystem.", this);
            }

            PointableCanvasModule module = eventSystem.GetComponent<PointableCanvasModule>();

            if (module == null)
            {
                module = eventSystem.gameObject.AddComponent<PointableCanvasModule>();

                if (debugLogs)
                    UDebug.Log("<b>[IntentCardSelectorUI]</b> Added PointableCanvasModule to EventSystem.", this);
            }
        }

        void BuildRayInteractionSurface()
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

        void SubscribeRayState()
        {
            if (_canvasRayInteractable == null || _subscribedToRayState) return;

            _canvasRayInteractable.WhenStateChanged += HandleCanvasRayStateChanged;
            _subscribedToRayState = true;
        }

        void UnsubscribeRayState()
        {
            if (_canvasRayInteractable == null || !_subscribedToRayState) return;

            _canvasRayInteractable.WhenStateChanged -= HandleCanvasRayStateChanged;
            _subscribedToRayState = false;
        }

        void HandleCanvasRayStateChanged(InteractableStateChangeArgs args)
        {
            if (!debugLogs) return;

            if (args.NewState == InteractableState.Hover && args.PreviousState == InteractableState.Normal)
            {
                UDebug.Log("<color=#89CFF0><b>[IntentCardSelectorUI]</b></color> HandRayInteractor is hovering the menu canvas.", this);
            }
            else if (args.NewState == InteractableState.Select)
            {
                UDebug.Log("<color=yellow><b>[IntentCardSelectorUI]</b></color> HandRayInteractor selected on the menu canvas.", this);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Data helpers
        // ─────────────────────────────────────────────────────────────────────

        bool IsValidCardIndex(int index)
        {
            return index >= 0 && index < _runtimeCards.Count;
        }

        MetaRayIntentCardData GetCardConfig(int index)
        {
            if (cards == null) return null;
            if (index < 0 || index >= cards.Count) return null;
            return cards[index];
        }

        static SplatCardAction ConvertSplatAction(MetaRayIntentCardMenu.SplatCardAction action)
        {
            switch (action)
            {
                case MetaRayIntentCardMenu.SplatCardAction.CalmRoom: return SplatCardAction.CalmRoom;
                case MetaRayIntentCardMenu.SplatCardAction.HostRoom: return SplatCardAction.HostRoom;
                case MetaRayIntentCardMenu.SplatCardAction.FastRoom: return SplatCardAction.FastRoom;
                case MetaRayIntentCardMenu.SplatCardAction.None: return SplatCardAction.None;
                default: return SplatCardAction.AutoByIndex;
            }
        }

        string GetTitle(int index)
        {
            MetaRayIntentCardData data = GetCardConfig(index);

            if (data != null && !string.IsNullOrWhiteSpace(data.title))
                return data.title;

            switch (index)
            {
                case 0:
                    return "Calm & Unwind";

                case 1:
                    return "Host & Gather";

                case 2:
                    return "Fast & Focused";

                default:
                    return $"Room {index + 1}";
            }
        }

        string GetTagline(int index)
        {
            MetaRayIntentCardData data = GetCardConfig(index);

            if (data != null && !string.IsNullOrWhiteSpace(data.subtitle))
                return data.subtitle;

            switch (index)
            {
                case 0:
                    return "I want to relax and take my time";

                case 1:
                    return "I want to gather, host, and share";

                case 2:
                    return "I want it simple and efficient";

                default:
                    return "Select this room mood.";
            }
        }

        Sprite GetImage(int index)
        {
            MetaRayIntentCardData data = GetCardConfig(index);

            if (data != null && data.cardImage != null)
                return data.cardImage;

            switch (index)
            {
                case 0:
                    return imageCalm;

                case 1:
                    return imageHost;

                case 2:
                    return imageFast;

                default:
                    return null;
            }
        }

        Sprite GetIcon(int index)
        {
            switch (index)
            {
                case 0:
                    return iconCalm;

                case 1:
                    return iconHost;

                case 2:
                    return iconFast;

                default:
                    return null;
            }
        }

        Color[] GetGradient(int index)
        {
            switch (index)
            {
                case 0:
                    return GradCalm;

                case 1:
                    return GradHost;

                case 2:
                    return GradFast;

                default:
                    return GradCalm;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Runtime controls
        // ─────────────────────────────────────────────────────────────────────

        void HandleKeyboardDebug()
        {
            if (Input.GetKeyDown(previousCardKey)) SelectPreviousCard();
            if (Input.GetKeyDown(nextCardKey)) SelectNextCard();

            if (Input.GetKeyDown(KeyCode.Alpha1)) SetIntent(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetIntent(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetIntent(2);

            if (Input.GetKeyDown(confirmKey) || Input.GetKeyDown(confirmKeyAlt))
                ConfirmSelection();
        }

        void FindHeadCamera()
        {
            GameObject eye = GameObject.Find("CenterEyeAnchor");
            _cam = eye != null ? eye.transform : Camera.main != null ? Camera.main.transform : null;
        }

        IEnumerator FadeRoutine(bool fadeIn)
        {
            if (_cg == null)
                yield break;

            if (fadeIn)
            {
                _cg.interactable = true;
                _cg.blocksRaycasts = true;
            }

            float start = _cg.alpha;
            float end = fadeIn ? 1f : 0f;
            float t = 0f;

            while (t < 1f)
            {
                t = Mathf.Min(t + Time.deltaTime / 0.25f, 1f);
                _cg.alpha = Mathf.Lerp(start, end, t);
                yield return null;
            }

            if (!fadeIn)
            {
                _cg.interactable = false;
                _cg.blocksRaycasts = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Cleanup
        // ─────────────────────────────────────────────────────────────────────

        void ClearGeneratedObjects()
        {
            ClearGeneratedTextures();

            DestroyChildByName("Generated_IntentSelector");
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

        void DestroyChildByName(string childName)
        {
            Transform child = transform.Find(childName);

            if (child != null)
                DestroySmart(child.gameObject);
        }

        void ClearGeneratedTextures()
        {
            foreach (Texture2D tex in _generatedTextures)
            {
                if (tex == null) continue;

                if (UApplication.isPlaying)
                    Destroy(tex);
                else
                    DestroyImmediate(tex);
            }

            _generatedTextures.Clear();
        }

        // ─────────────────────────────────────────────────────────────────────
        // UI helpers
        // ─────────────────────────────────────────────────────────────────────

        TextMeshProUGUI AbsText(string label, Transform parent, Vector2 size, Vector2 pos, float fontSize, Color color)
        {
            GameObject go = new GameObject(label.Length > 24 ? label.Substring(0, 24) : label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = gameObject.layer;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.enableAutoSizing = false;
            tmp.raycastTarget = false;

            if (fontAsset != null)
                tmp.font = fontAsset;

            return tmp;
        }

        GameObject MakeUIObject(string objectName, Transform parent)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = gameObject.layer;
            return go;
        }

        void ApplyFontToGeneratedUI()
        {
            if (fontAsset == null || _generatedRoot == null) return;

            foreach (TextMeshProUGUI tmp in _generatedRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
                tmp.font = fontAsset;
        }

        static void StretchFull(GameObject go)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void StretchInset(GameObject go, float inset)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        static T AddOrGet<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();

            if (component == null)
                component = go.AddComponent<T>();

            return component;
        }

        static void DestroySmart(GameObject go)
        {
            if (go == null) return;

            if (UApplication.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }

        static string CleanName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "Untitled";

            foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
                input = input.Replace(invalid, '_');

            return input.Replace(" ", "_");
        }

        static T FindAny<T>() where T : UnityEngine.Object
        {
#if UNITY_2022_2_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<T>();
#else
            return UnityEngine.Object.FindObjectOfType<T>();
#endif
        }

        // ─────────────────────────────────────────────────────────────────────
        // Generated graphics
        // ─────────────────────────────────────────────────────────────────────

        Sprite LinearGradientSprite(int w, int h, float angleDeg, Color[] colors, float[] stops, float cornerRadius)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color32[] pixels = new Color32[w * h];

            float rad = angleDeg * Mathf.Deg2Rad;
            float dx = Mathf.Sin(rad);
            float dy = -Mathf.Cos(rad);

            float[] c =
            {
                0 * dx + 0 * dy,
                1 * dx + 0 * dy,
                0 * dx + 1 * dy,
                1 * dx + 1 * dy
            };

            float minT = Mathf.Min(c[0], c[1], c[2], c[3]);
            float maxT = Mathf.Max(c[0], c[1], c[2], c[3]);
            float range = maxT - minT;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float v = (y + 0.5f) / h;
                    float t = ((u * dx + v * dy) - minT) / range;

                    Color col = colors[0];

                    for (int i = 0; i < stops.Length - 1; i++)
                    {
                        if (t >= stops[i] && t <= stops[i + 1])
                        {
                            col = Color.Lerp(
                                colors[i],
                                colors[i + 1],
                                (t - stops[i]) / (stops[i + 1] - stops[i])
                            );

                            break;
                        }
                    }

                    if (t > stops[stops.Length - 1])
                        col = colors[colors.Length - 1];

                    float a = 1f;

                    if (cornerRadius > 0f)
                    {
                        float px = x + 0.5f;
                        float py = y + 0.5f;
                        float rPx = cornerRadius;
                        float ex = Mathf.Max(rPx - px, px - (w - rPx), 0f);
                        float ey = Mathf.Max(rPx - py, py - (h - rPx), 0f);
                        a = Mathf.Clamp01(rPx - Mathf.Sqrt(ex * ex + ey * ey) + 0.5f);
                    }

                    pixels[y * w + x] = new Color32(
                        (byte)(col.r * 255 + 0.5f),
                        (byte)(col.g * 255 + 0.5f),
                        (byte)(col.b * 255 + 0.5f),
                        (byte)(a * 255 + 0.5f)
                    );
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            _generatedTextures.Add(tex);

            return Sprite.Create(
                tex,
                new Rect(0, 0, w, h),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect
            );
        }

        static void Round(UIImage img, float radius)
        {
            if (img == null) return;

            img.sprite = RoundedSprite(radius);
            img.type = Image.Type.Sliced;
        }

        static Sprite RoundedSprite(float radius)
        {
            const int size = 64;

            float r = Mathf.Clamp(radius, 0f, size * 0.5f);

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = x + 0.5f;
                    float cy = y + 0.5f;

                    float ex = Mathf.Max(r - cx, cx - (size - r), 0f);
                    float ey = Mathf.Max(r - cy, cy - (size - r), 0f);

                    float a = Mathf.Clamp01(r - Mathf.Sqrt(ex * ex + ey * ey) + 0.5f);

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            return Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                Vector2.one * 0.5f,
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(r, r, r, r)
            );
        }

        static Color Hex(uint rgb)
        {
            return new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f
            );
        }

        // ─────────────────────────────────────────────────────────────────────
        // Meta SDK version compatibility
        // ─────────────────────────────────────────────────────────────────────

        static void TryConfigureRectTransformBoundsClipperDriver(
            RectTransformBoundsClipperDriver driver,
            BoundsClipper boundsClipper
        )
        {
            if (driver == null || boundsClipper == null) return;

            RectTransform rectTransform = driver.GetComponent<RectTransform>();
            Type driverType = typeof(RectTransformBoundsClipperDriver);

            MethodInfo[] methods = driverType.GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );

            foreach (MethodInfo method in methods)
            {
                if (!method.Name.StartsWith("Inject", StringComparison.Ordinal))
                    continue;

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

                if (!canUseMethod)
                    continue;

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

            MethodInfo resizeMethod = driverType.GetMethod(
                "Resize",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );

            try
            {
                resizeMethod?.Invoke(driver, null);
            }
            catch
            {
                // Non-fatal.
            }
        }

        static void SetFieldIfExists(object target, string fieldName, object value)
        {
            if (target == null) return;

            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );

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

    public class CardIndexDropdownAttribute : PropertyAttribute { }

#if UNITY_EDITOR
    [CustomEditor(typeof(IntentCardSelectorUI))]
    public class IntentCardSelectorUIEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            IntentCardSelectorUI ui = (IntentCardSelectorUI)target;

            GUILayout.Space(4);

            using (new EditorGUI.DisabledScope(UnityEngine.Application.isPlaying))
            {
                if (GUILayout.Button("Rebuild UI", GUILayout.Height(28)))
                {
                    ui.QueueEditorRebuild();
                }
            }

            GUILayout.Space(6);

            DrawDefaultInspector();
        }
    }

    [CustomPropertyDrawer(typeof(CardIndexDropdownAttribute))]
    public class CardIndexDropdownDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Integer)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            SerializedProperty cardsProp = property.serializedObject.FindProperty("cards");

            if (cardsProp == null || !cardsProp.isArray || cardsProp.arraySize == 0)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            string[] names = new string[cardsProp.arraySize];

            for (int i = 0; i < cardsProp.arraySize; i++)
            {
                SerializedProperty element = cardsProp.GetArrayElementAtIndex(i);
                MetaRayIntentCardData data = element.objectReferenceValue as MetaRayIntentCardData;

                string displayName;

                if (data == null)
                    displayName = $"{i}: <empty>";
                else if (!string.IsNullOrWhiteSpace(data.title))
                    displayName = $"{i}: {data.title}";
                else
                    displayName = $"{i}: {data.name}";

                names[i] = displayName;
            }

            int current = Mathf.Clamp(property.intValue, 0, cardsProp.arraySize - 1);

            EditorGUI.BeginChangeCheck();
            int newVal = EditorGUI.Popup(position, label.text, current, names);

            if (EditorGUI.EndChangeCheck() || current != property.intValue)
                property.intValue = newVal;
        }
    }
#endif
}
