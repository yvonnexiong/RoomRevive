using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using UIImage = UnityEngine.UI.Image;
using UDebug = UnityEngine.Debug;
using UApplication = UnityEngine.Application;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
[RequireComponent(typeof(GraphicRaycaster))]
public class MetaRayFurnitureBrowser : MonoBehaviour
{
    public enum FurnitureBrowserUIState
    {
        Discover,
        Product,
        Details
    }

    [Serializable]
    public class ProductVariantEvent : UnityEvent<int, MetaRayFurnitureProductVariant> { }

    [Header("Product Catalog ScriptableObject")]
    public MetaRayFurnitureProductCatalog productCatalog;

    [Header("UI State")]
    [Tooltip("Controls which UI is visible. Enter advances Discover -> Product -> Details -> Product.")]
    public FurnitureBrowserUIState uiState = FurnitureBrowserUIState.Discover;

    [Header("Runtime Image URLs")]
    public bool loadImageUrlsAtRuntime = true;

    [Header("Initial Product")]
    [Range(0, 50)]
    public int startProductIndex = 0;

    [Header("Variant GameObjects")]
    [Tooltip("Enabled when product variant index 0 is active.")]
    public GameObject variant0GameObject;

    [Tooltip("Enabled when product variant index 1 is active.")]
    public GameObject variant1GameObject;

    [Tooltip("Enabled when product variant index 2 is active.")]
    public GameObject variant2GameObject;

    [Header("OnValidate / Editor Preview")]
    public bool rebuildOnValidate = true;

    [Header("Interaction")]
    public bool cardClickUnlocksDetails = true;
    public bool resetDetailsWhenChangingProduct = true;
    public bool wrapAroundProducts = true;

    [Header("Keyboard Debug Add-on")]
    public bool keyboardDebug = true;
    public bool enterKeyPressesPrimaryAction = true;
    public bool spaceKeyPressesPrimaryAction = true;
    public bool arrowKeysChangeProduct = true;
    public bool rKeyResetsToCallout = true;

    [Header("Events")]
    public ProductVariantEvent onProductChanged = new ProductVariantEvent();
    public ProductVariantEvent onDiscovered = new ProductVariantEvent();
    public ProductVariantEvent onDetailsUnlocked = new ProductVariantEvent();

    [Header("World Space Canvas")]
    public float worldScale = 0.001f;
    public Camera eventCamera;
    public bool autoCreateEventSystem = true;
    public PlaneSurface.NormalFacing raySurfaceFacing = PlaneSurface.NormalFacing.Backward;

    [Header("Canvas Size")]
    public float canvasWidth = 760f;
    public float canvasHeight = 1650f;

    [Header("Generated Background")]
    public bool showGeneratedBackground = false;

    [Header("Product Card Layout")]
    public float productTopOffset = 54f;
    public float cardWidth = 540f;
    public float cardHeight = 610f;
    public float cardBorderThickness = 3f;
    public float imageHeight = 320f;
    public float detailsGap = 32f;
    public float detailsHeight = 980f;

    [Header("Product Image Area")]
    public bool roundImageArea = true;
    public float imageAreaInset = 6f;

    [Tooltip("Keeps product images proportional. The image zooms to cover the frame and is cropped by the ImageClip mask.")]
    public bool productImageAspectFill = true;

    [Tooltip("Extra zoom on top of aspect-fill. 1 = normal cover crop, 1.1 = slightly zoomed.")]
    [Min(1f)]
    public float productImageZoom = 1f;

    [Header("Product Text Layout")]
    public float subtitleBodyExtraGap = 10f;

    [Header("Details Card Transform")]
    public Vector3 detailsCardPositionOffset = new Vector3(-600.1f, 621.3f, 0f);
    public Vector3 detailsCardRotationEuler = new Vector3(0f, -32.36f, 0f);
    public Vector3 detailsCardScale = new Vector3(1f, 1f, 1f);

    [Header("Clean Details UI Layout")]
    public float detailsHeaderHeight = 112f;
    public float detailsBodyPaddingX = 28f;
    public float detailsBodyPaddingY = 24f;
    public float detailsBodySectionSpacing = 24f;
    public float detailsSectionTitleHeight = 24f;
    public float detailsSectionTitleGap = 10f;

    public float detailsDimensionsSectionHeight = 112f;
    public float detailsDimensionCardHeight = 72f;
    public float detailsDimensionCardSpacing = 10f;

    public float detailsMaterialsSectionHeight = 86f;
    public float detailsMaterialsTextHeight = 50f;

    public float detailsFeatureLineHeight = 24f;

    public float detailsFinishSectionHeight = 120f;
    public float detailsFinishSwatchHeight = 82f;
    public float detailsFinishSwatchSpacing = 12f;

    public float detailsStorageSectionHeight = 76f;
    public float detailsStorageTextHeight = 34f;

    [Header("Details UI Style")]
    public Color detailsPanelColor = new Color(0.955f, 0.958f, 0.965f, 0.98f);
    public Color detailsHeaderColor = new Color(1f, 1f, 1f, 0.52f);
    public Color detailsMutedCardColor = new Color(1f, 1f, 1f, 0.50f);
    public Color detailsHeaderTextColor = new Color(0.46f, 0.49f, 0.57f, 1f);
    public Color detailsDividerColor = new Color(0.80f, 0.82f, 0.86f, 0.75f);

    public Sprite detailsPanelSprite;
    public Sprite detailsHeaderSprite;
    public Sprite detailsMutedCardSprite;

    [Header("Finish Swatch Style")]
    [Tooltip("Used for FinishSwatch_0, FinishSwatch_1, etc. Hex: #726E6E")]
    public Color finishSwatchBackgroundColor = new Color32(0x72, 0x6E, 0x6E, 0xFF);

    [Tooltip("Label color inside the darker finish swatch cards.")]
    public Color finishSwatchLabelColor = new Color(1f, 1f, 1f, 0.92f);

    [Tooltip("Optional override sprite for the finish swatch background cards.")]
    public Sprite finishSwatchBackgroundSprite;

    [Tooltip("Sprite used for the finish color dots. This script tries to auto-assign RoundCorners_4.")]
    public Sprite finishColorDotSprite;

    [Header("Callout Layout")]
    public float calloutWidth = 590f;
    public float calloutHeight = 380f;
    public float calloutPreviewSize = 140f;

    [Header("Arrow Buttons")]
    public float arrowButtonSize = 62f;
    public float nextArrowSizeMultiplier = 2f;
    public float previousArrowFontSize = 34f;
    public float nextArrowFontSize = 68f;

    [Tooltip("Actual anchored position of PreviousButton. Applied in Build, Update, and OnValidate.")]
    public Vector2 previousButtonAnchoredPosition = new Vector2(18f, 0f);

    [Tooltip("Actual anchored position of NextButton. Applied in Build, Update, and OnValidate.")]
    public Vector2 nextButtonAnchoredPosition = new Vector2(-18f, 0f);

    [Header("Finish Color Chips")]
    public Vector3 finishColorChipScale = new Vector3(0.2f, 0.2f, 0.2f);
    public float detailsFinishDotSize = 32f;

    [Header("Rounded Corners")]
    public bool useGeneratedRoundedCorners = true;
    [Range(1, 64)] public int missingCatalogCornerRadius = 28;
    [Range(1, 64)] public int calloutCornerRadius = 32;
    [Range(1, 64)] public int previewImageCornerRadius = 18;
    [Range(1, 64)] public int productCardCornerRadius = 34;
    [Range(1, 64)] public int productInnerCornerRadius = 30;
    [Range(1, 64)] public int imageAreaCornerRadius = 26;
    [Range(1, 64)] public int pillCornerRadius = 18;
    [Range(1, 64)] public int buttonCornerRadius = 20;
    [Range(1, 64)] public int detailsPanelCornerRadius = 36;
    [Range(1, 64)] public int detailsHeaderCornerRadius = 28;
    [Range(1, 64)] public int detailsMutedCardCornerRadius = 18;
    [Range(1, 64)] public int colorDotCornerRadius = 64;

    [Header("Animation")]
    public float transitionDuration = 0.22f;
    public float slideDistancePx = 90f;
    public float hoverScale = 1.018f;
    public float scaleLerpSpeed = 14f;

    [Header("Colors")]
    public Color backgroundColor = new Color(0.93f, 0.94f, 0.97f, 1f);
    public Color ambientTopColor = new Color(0.78f, 0.76f, 0.95f, 0.32f);
    public Color ambientBottomColor = new Color(0.84f, 0.86f, 0.90f, 0.45f);

    public Color cardColor = new Color(0.98f, 0.985f, 0.995f, 1f);
    public Color cardBorderColor = new Color(0.78f, 0.80f, 0.86f, 1f);
    public Color cardBorderHoverColor = new Color(0.50f, 0.55f, 0.72f, 1f);

    public Color darkTextColor = new Color(0.09f, 0.10f, 0.14f, 1f);
    public Color mutedTextColor = new Color(0.36f, 0.39f, 0.47f, 1f);
    public Color softTextColor = new Color(0.52f, 0.55f, 0.62f, 1f);

    public Color primaryButtonColor = new Color(0.12f, 0.14f, 0.20f, 1f);
    public Color primaryButtonTextColor = Color.white;
    public Color secondaryButtonColor = new Color(1f, 1f, 1f, 0.9f);
    public Color secondaryButtonTextColor = new Color(0.10f, 0.11f, 0.16f, 1f);

    public Color overlayColor = new Color(0f, 0f, 0f, 0.20f);
    public Color fallbackImageColorA = new Color(0.78f, 0.76f, 0.92f, 1f);
    public Color fallbackImageColorB = new Color(0.88f, 0.82f, 0.72f, 1f);
    public Color fallbackImageColorC = new Color(0.76f, 0.83f, 0.84f, 1f);

    [Header("Typography")]
    public string discoverHeadline = "New room match discovered";
    public string discoverButtonText = "Discover";
    public string detailsButtonText = "See details";
    public string hideDetailsButtonText = "Hide details";
    public string defaultBadgeText = "Room fit";

    public float eyebrowFontSize = 15f;
    public float titleFontSize = 32f;
    public float subtitleFontSize = 17f;
    public float bodyFontSize = 15f;
    public float buttonFontSize = 18f;
    public float detailTitleFontSize = 26f;
    public float detailBodyFontSize = 14f;

    [Header("Debug")]
    public bool debugLogs = true;

    [SerializeField, HideInInspector] private GameObject _generatedRoot;
    [SerializeField, HideInInspector] private GameObject _raySurfaceRoot;
    [SerializeField, HideInInspector] private PointableCanvas _pointableCanvas;
    [SerializeField, HideInInspector] private RayInteractable _canvasRayInteractable;

    [SerializeField, HideInInspector] private int _currentIndex;

    [SerializeField, HideInInspector] private RectTransform _previousButtonRect;
    [SerializeField, HideInInspector] private RectTransform _nextButtonRect;

    private Canvas _canvas;
    private CanvasScaler _canvasScaler;
    private GraphicRaycaster _graphicRaycaster;
    private RectTransform _rectTransform;

    private RectTransform _animatedProductRoot;
    private CanvasGroup _animatedProductCanvasGroup;
    private RectTransform _productCardVisual;
    private UIImage _productCardBorder;

    private bool _subscribedToRayState;
    private bool _cardHovered;

    private bool _isTransitioning;
    private float _transitionTimer;
    private int _transitionDirection = 1;
    private Vector2 _transitionBasePosition;

    private readonly Dictionary<string, Sprite> _runtimeSpriteCache = new Dictionary<string, Sprite>();

#if UNITY_EDITOR
    private bool _editorRebuildQueued;
#endif

    private void Reset()
    {
        GrabRequiredComponents();
        ClampInspectorValues();
        TryAutoAssignDefaultSprites();
        ConfigureCanvas();
        ApplyInitialProductIndex();
        ApplyVariantGameObjects();

#if UNITY_EDITOR
        if (!UApplication.isPlaying)
        {
            QueueEditorRebuild();
        }
#endif
    }

    private void Awake()
    {
        GrabRequiredComponents();
        ClampInspectorValues();
        TryAutoAssignDefaultSprites();
        ConfigureCanvas();

        if (!UApplication.isPlaying)
        {
            ApplyInitialProductIndex();
        }

        ApplyVariantGameObjects();
    }

    private void Start()
    {
        if (!UApplication.isPlaying) return;

        ApplyInitialProductIndex();
        ApplyVariantGameObjects();
        RebuildBrowser(false, 1);
        SubscribeRayState();

        if (uiState == FurnitureBrowserUIState.Product || uiState == FurnitureBrowserUIState.Details)
        {
            InvokeProductShownEvents();
        }
    }

    private void Update()
    {
        ApplyArrowButtonTransforms();

        if (UApplication.isPlaying)
        {
            AnimateTransition();
            AnimateCardHover();

            if (keyboardDebug)
            {
                HandleKeyboardDebug();
            }
        }
        else
        {
            AnimateCardHover();
        }
    }

    private void OnDisable()
    {
        UnsubscribeRayState();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        GrabRequiredComponents();
        ClampInspectorValues();
        TryAutoAssignDefaultSprites();
        ConfigureCanvas();
        ApplyInitialProductIndex();
        ApplyVariantGameObjects();
        ApplyArrowButtonTransforms();

        if (!rebuildOnValidate) return;

        QueueEditorRebuild();
    }

    private void QueueEditorRebuild()
    {
        if (_editorRebuildQueued) return;

        _editorRebuildQueued = true;

        EditorApplication.delayCall += () =>
        {
            _editorRebuildQueued = false;

            if (this == null) return;
            if (gameObject == null) return;
            if (UApplication.isPlaying) return;
            if (!rebuildOnValidate) return;

            GrabRequiredComponents();
            ClampInspectorValues();
            TryAutoAssignDefaultSprites();
            ConfigureCanvas();
            ApplyInitialProductIndex();
            ApplyVariantGameObjects();

            RebuildBrowser(false, 1);
            ApplyArrowButtonTransforms();

            EditorUtility.SetDirty(this);
        };
    }
#endif

    [ContextMenu("Rebuild Meta Ray Furniture Browser")]
    public void RebuildBrowser()
    {
        RebuildBrowser(false, 1);
    }

    public void PressPrimaryAction()
    {
        switch (uiState)
        {
            case FurnitureBrowserUIState.Discover:
                Discover();
                break;

            case FurnitureBrowserUIState.Product:
                UnlockDetails();
                break;

            case FurnitureBrowserUIState.Details:
                CloseDetails();
                break;
        }
    }

    public void Discover()
    {
        if (GetProductCount() <= 0)
        {
            WarnMissingCatalog();
            return;
        }

        uiState = FurnitureBrowserUIState.Product;
        ApplyVariantGameObjects();
        RebuildBrowser(true, 1);

        MetaRayFurnitureProductVariant product = GetProduct(_currentIndex);
        product?.onDiscovered?.Invoke();

        onDiscovered?.Invoke(_currentIndex, product);
        InvokeProductShownEvents();

        if (debugLogs)
        {
            UDebug.Log($"<color=#89CFF0><b>[MetaRayFurnitureBrowser]</b></color> Discover -> Product: {_currentIndex}: {GetTitle(_currentIndex)}");
        }
    }

    public void GoNext()
    {
        int count = GetProductCount();

        if (count <= 0)
        {
            WarnMissingCatalog();
            return;
        }

        int next = _currentIndex + 1;

        if (next >= count)
        {
            next = wrapAroundProducts ? 0 : count - 1;
        }

        SetProductIndex(next, 1);
    }

    public void GoPrevious()
    {
        int count = GetProductCount();

        if (count <= 0)
        {
            WarnMissingCatalog();
            return;
        }

        int next = _currentIndex - 1;

        if (next < 0)
        {
            next = wrapAroundProducts ? count - 1 : 0;
        }

        SetProductIndex(next, -1);
    }

    public void SetProductIndex(int index)
    {
        SetProductIndex(index, 1);
    }

    public void SetProductIndex(int index, int direction)
    {
        int count = GetProductCount();

        if (count <= 0)
        {
            WarnMissingCatalog();
            return;
        }

        index = Mathf.Clamp(index, 0, count - 1);

        if (index == _currentIndex && uiState != FurnitureBrowserUIState.Discover)
        {
            ApplyVariantGameObjects();
            return;
        }

        _currentIndex = index;

        if (uiState == FurnitureBrowserUIState.Discover)
        {
            uiState = FurnitureBrowserUIState.Product;
        }

        if (resetDetailsWhenChangingProduct && uiState == FurnitureBrowserUIState.Details)
        {
            uiState = FurnitureBrowserUIState.Product;
        }

        ApplyVariantGameObjects();

        RebuildBrowser(true, direction >= 0 ? 1 : -1);

        MetaRayFurnitureProductVariant product = GetProduct(_currentIndex);
        product?.onShown?.Invoke();

        onProductChanged?.Invoke(_currentIndex, product);

        if (debugLogs)
        {
            UDebug.Log($"<color=yellow><b>[MetaRayFurnitureBrowser]</b></color> Product changed to {_currentIndex}: {GetTitle(_currentIndex)} | State: {uiState}");
        }
    }

    public void UnlockDetails()
    {
        if (uiState == FurnitureBrowserUIState.Discover)
        {
            return;
        }

        bool wasAlreadyDetails = uiState == FurnitureBrowserUIState.Details;
        uiState = FurnitureBrowserUIState.Details;

        RebuildBrowser(false, 1);

        if (!wasAlreadyDetails)
        {
            MetaRayFurnitureProductVariant product = GetProduct(_currentIndex);
            product?.onDetailsUnlocked?.Invoke();

            onDetailsUnlocked?.Invoke(_currentIndex, product);

            if (debugLogs)
            {
                UDebug.Log($"<color=lime><b>[MetaRayFurnitureBrowser]</b></color> Product -> Details for {_currentIndex}: {GetTitle(_currentIndex)}");
            }
        }
    }

    public void ToggleDetails()
    {
        if (uiState == FurnitureBrowserUIState.Details)
        {
            CloseDetails();
        }
        else
        {
            UnlockDetails();
        }
    }

    public void CloseDetails()
    {
        if (uiState != FurnitureBrowserUIState.Details)
        {
            return;
        }

        uiState = FurnitureBrowserUIState.Product;
        RebuildBrowser(false, 1);

        if (debugLogs)
        {
            UDebug.Log($"<color=#89CFF0><b>[MetaRayFurnitureBrowser]</b></color> Details closed for {_currentIndex}: {GetTitle(_currentIndex)}");
        }
    }

    public void ResetToCallout()
    {
        uiState = FurnitureBrowserUIState.Discover;
        ApplyVariantGameObjects();
        RebuildBrowser(false, 1);
    }

    public void SetCardHovered(bool hovered)
    {
        _cardHovered = hovered;

        if (_productCardBorder != null)
        {
            ApplyRoundedImage(
                _productCardBorder,
                hovered ? cardBorderHoverColor : cardBorderColor,
                productCardCornerRadius
            );

            _productCardBorder.raycastTarget = true;
        }
    }

    private void ApplyVariantGameObjects()
    {
        int activeVariantIndex = GetActiveVariantGameObjectIndex();

        SetVariantGameObjectActive(variant0GameObject, activeVariantIndex == 0);
        SetVariantGameObjectActive(variant1GameObject, activeVariantIndex == 1);
        SetVariantGameObjectActive(variant2GameObject, activeVariantIndex == 2);
    }

    private int GetActiveVariantGameObjectIndex()
    {
        if (GetProductCount() <= 0)
        {
            return -1;
        }

        if (_currentIndex < 0 || _currentIndex > 2)
        {
            return -1;
        }

        return _currentIndex;
    }

    private void SetVariantGameObjectActive(GameObject target, bool active)
    {
        if (target == null) return;

        if (target == gameObject)
        {
            if (debugLogs)
            {
                UDebug.LogWarning("[MetaRayFurnitureBrowser] Variant GameObject cannot be the same GameObject as the browser itself.");
            }

            return;
        }

        if (target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private float GetResolvedDetailsHeight()
    {
        return Mathf.Max(detailsHeight, GetCleanDetailsPanelHeight());
    }

    private float GetCleanDetailsPanelHeight()
    {
        float featuresHeight = GetCleanFeaturesSectionHeight();

        float bodyHeight =
            detailsBodyPaddingY * 2f +
            detailsDimensionsSectionHeight +
            detailsMaterialsSectionHeight +
            featuresHeight +
            detailsFinishSectionHeight +
            detailsStorageSectionHeight +
            detailsBodySectionSpacing * 4f;

        return detailsHeaderHeight + bodyHeight;
    }

    private float GetCleanFeaturesSectionHeight()
    {
        List<string> features = GetProduct(_currentIndex)?.features;
        int featureCount = features == null ? 1 : Mathf.Max(1, features.Count);

        return detailsSectionTitleHeight +
               detailsSectionTitleGap +
               featureCount * detailsFeatureLineHeight +
               Mathf.Max(0, featureCount - 1) * 4f;
    }

    private Vector3 GetDetailsAnchoredPosition3D()
    {
        return new Vector3(
            detailsCardPositionOffset.x,
            -(cardHeight + detailsGap) + detailsCardPositionOffset.y,
            detailsCardPositionOffset.z
        );
    }

    private void ClampInspectorValues()
    {
        startProductIndex = Mathf.Clamp(startProductIndex, 0, Mathf.Max(0, GetProductCount() - 1));
        worldScale = Mathf.Max(0.0001f, worldScale);

        canvasWidth = Mathf.Max(200f, canvasWidth);
        canvasHeight = Mathf.Max(400f, canvasHeight);

        cardWidth = Mathf.Max(160f, cardWidth);
        cardHeight = Mathf.Max(220f, cardHeight);
        cardBorderThickness = Mathf.Clamp(cardBorderThickness, 0f, 24f);
        imageHeight = Mathf.Clamp(imageHeight, 80f, cardHeight - 170f);
        imageAreaInset = Mathf.Max(0f, imageAreaInset);
        productImageZoom = Mathf.Max(1f, productImageZoom);

        detailsGap = Mathf.Max(0f, detailsGap);
        detailsHeight = Mathf.Max(420f, detailsHeight);

        detailsHeaderHeight = Mathf.Max(72f, detailsHeaderHeight);
        detailsBodyPaddingX = Mathf.Max(0f, detailsBodyPaddingX);
        detailsBodyPaddingY = Mathf.Max(0f, detailsBodyPaddingY);
        detailsBodySectionSpacing = Mathf.Max(0f, detailsBodySectionSpacing);
        detailsSectionTitleHeight = Mathf.Max(16f, detailsSectionTitleHeight);
        detailsSectionTitleGap = Mathf.Max(0f, detailsSectionTitleGap);

        detailsDimensionsSectionHeight = Mathf.Max(90f, detailsDimensionsSectionHeight);
        detailsDimensionCardHeight = Mathf.Max(40f, detailsDimensionCardHeight);
        detailsDimensionCardSpacing = Mathf.Max(0f, detailsDimensionCardSpacing);

        detailsMaterialsSectionHeight = Mathf.Max(60f, detailsMaterialsSectionHeight);
        detailsMaterialsTextHeight = Mathf.Max(24f, detailsMaterialsTextHeight);

        detailsFeatureLineHeight = Mathf.Max(18f, detailsFeatureLineHeight);

        detailsFinishSectionHeight = Mathf.Max(90f, detailsFinishSectionHeight);
        detailsFinishSwatchHeight = Mathf.Max(50f, detailsFinishSwatchHeight);
        detailsFinishSwatchSpacing = Mathf.Max(0f, detailsFinishSwatchSpacing);
        detailsFinishDotSize = Mathf.Max(12f, detailsFinishDotSize);

        detailsStorageSectionHeight = Mathf.Max(56f, detailsStorageSectionHeight);
        detailsStorageTextHeight = Mathf.Max(20f, detailsStorageTextHeight);

        calloutWidth = Mathf.Max(180f, calloutWidth);
        calloutHeight = Mathf.Max(160f, calloutHeight);
        calloutPreviewSize = Mathf.Max(40f, calloutPreviewSize);

        arrowButtonSize = Mathf.Max(24f, arrowButtonSize);
        nextArrowSizeMultiplier = Mathf.Max(1f, nextArrowSizeMultiplier);
        previousArrowFontSize = Mathf.Max(8f, previousArrowFontSize);
        nextArrowFontSize = Mathf.Max(8f, nextArrowFontSize);

        transitionDuration = Mathf.Max(0.01f, transitionDuration);
        slideDistancePx = Mathf.Max(0f, slideDistancePx);
        hoverScale = Mathf.Max(0.5f, hoverScale);
        scaleLerpSpeed = Mathf.Max(0.1f, scaleLerpSpeed);

        eyebrowFontSize = Mathf.Max(4f, eyebrowFontSize);
        titleFontSize = Mathf.Max(6f, titleFontSize);
        subtitleFontSize = Mathf.Max(6f, subtitleFontSize);
        bodyFontSize = Mathf.Max(6f, bodyFontSize);
        buttonFontSize = Mathf.Max(6f, buttonFontSize);
        detailTitleFontSize = Mathf.Max(6f, detailTitleFontSize);
        detailBodyFontSize = Mathf.Max(6f, detailBodyFontSize);

        detailsCardScale.x = Mathf.Max(0.001f, detailsCardScale.x);
        detailsCardScale.y = Mathf.Max(0.001f, detailsCardScale.y);
        detailsCardScale.z = Mathf.Max(0.001f, detailsCardScale.z);

        finishColorChipScale.x = Mathf.Max(0.001f, finishColorChipScale.x);
        finishColorChipScale.y = Mathf.Max(0.001f, finishColorChipScale.y);
        finishColorChipScale.z = Mathf.Max(0.001f, finishColorChipScale.z);
    }

    private void ApplyInitialProductIndex()
    {
        _currentIndex = Mathf.Clamp(startProductIndex, 0, Mathf.Max(0, GetProductCount() - 1));
    }

    private void InvokeProductShownEvents()
    {
        if (!UApplication.isPlaying) return;

        MetaRayFurnitureProductVariant product = GetProduct(_currentIndex);
        product?.onShown?.Invoke();
        onProductChanged?.Invoke(_currentIndex, product);
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

        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.worldCamera = eventCamera != null ? eventCamera : Camera.main;

        transform.localScale = Vector3.one * worldScale;

        if (_rectTransform != null)
        {
            _rectTransform.sizeDelta = new Vector2(canvasWidth, canvasHeight);
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
        {
            _pointableCanvas = gameObject.AddComponent<PointableCanvas>();
        }

        if (_canvas != null)
        {
            _pointableCanvas.InjectAllPointableCanvas(_canvas);
        }
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
            {
                UDebug.Log("<b>[MetaRayFurnitureBrowser]</b> Created EventSystem.");
            }
        }

        PointableCanvasModule module = eventSystem.GetComponent<PointableCanvasModule>();

        if (module == null)
        {
            module = eventSystem.gameObject.AddComponent<PointableCanvasModule>();

            if (debugLogs)
            {
                UDebug.Log("<b>[MetaRayFurnitureBrowser]</b> Added PointableCanvasModule to EventSystem.");
            }
        }
    }

    private void RebuildBrowser(bool animateProduct, int direction)
    {
        UnsubscribeRayState();

        GrabRequiredComponents();
        ClampInspectorValues();
        TryAutoAssignDefaultSprites();
        ConfigureCanvas();
        EnsurePointableCanvas();
        EnsureEventSystemAndPointableModule();
        ApplyVariantGameObjects();

        ClearGeneratedObjects();

        _cardHovered = false;

        _generatedRoot = MakeUIObject("Generated_MetaRayFurnitureBrowser", transform);
        StretchFull(_generatedRoot);

        BuildRayInteractionSurface();

        if (showGeneratedBackground)
        {
            BuildAmbientBackground(_generatedRoot.transform);
        }

        if (GetProductCount() <= 0)
        {
            BuildMissingCatalogMessage(_generatedRoot.transform);
        }
        else if (uiState == FurnitureBrowserUIState.Discover)
        {
            BuildDiscoverCallout(_generatedRoot.transform);
        }
        else
        {
            BuildProductExperience(_generatedRoot.transform);

            if (animateProduct && UApplication.isPlaying)
            {
                StartProductTransition(direction);
            }
        }

        ApplyArrowButtonTransforms();

        if (UApplication.isPlaying)
        {
            SubscribeRayState();
        }

        if (debugLogs)
        {
            UDebug.Log($"<b>[MetaRayFurnitureBrowser]</b> Built UI. Product index: {_currentIndex}, State: {uiState}");
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
        clippedPlaneSurface.InjectAllClippedPlaneSurface(
            planeSurface,
            new List<IBoundsClipper> { boundsClipper }
        );

        _canvasRayInteractable = AddOrGet<RayInteractable>(_raySurfaceRoot);
        _canvasRayInteractable.InjectAllRayInteractable(clippedPlaneSurface);

        if (_pointableCanvas != null)
        {
            _canvasRayInteractable.InjectOptionalPointableElement(_pointableCanvas);
        }

        _canvasRayInteractable.InjectOptionalSelectSurface(planeSurface);
    }

    private void BuildAmbientBackground(Transform parent)
    {
        GameObject bg = MakeUIObject("Background", parent);
        StretchFull(bg);

        UIImage bgImage = bg.AddComponent<UIImage>();
        ApplyRoundedImage(bgImage, backgroundColor, 1);
        bgImage.raycastTarget = false;

        GameObject top = MakeUIObject("AmbientTop", parent);
        RectTransform topRT = top.GetComponent<RectTransform>();
        topRT.anchorMin = new Vector2(0f, 0.62f);
        topRT.anchorMax = new Vector2(1f, 1f);
        topRT.offsetMin = Vector2.zero;
        topRT.offsetMax = Vector2.zero;

        UIImage topImage = top.AddComponent<UIImage>();
        topImage.color = ambientTopColor;
        topImage.raycastTarget = false;

        GameObject bottom = MakeUIObject("AmbientBottom", parent);
        RectTransform bottomRT = bottom.GetComponent<RectTransform>();
        bottomRT.anchorMin = new Vector2(0f, 0f);
        bottomRT.anchorMax = new Vector2(1f, 0.26f);
        bottomRT.offsetMin = Vector2.zero;
        bottomRT.offsetMax = Vector2.zero;

        UIImage bottomImage = bottom.AddComponent<UIImage>();
        bottomImage.color = ambientBottomColor;
        bottomImage.raycastTarget = false;
    }

    private void BuildMissingCatalogMessage(Transform parent)
    {
        GameObject root = MakeUIObject("MissingCatalogMessage", parent);
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(560f, 220f);

        UIImage bg = root.AddComponent<UIImage>();
        ApplyRoundedImage(bg, cardColor, missingCatalogCornerRadius);
        bg.raycastTarget = false;

        TextMeshProUGUI text = AddTMP("Message", root.transform);
        StretchInset(text.gameObject, 28f);
        text.text = "No Product Catalog assigned\n\nCreate one via:\nAssets > Create > XRCC > Furniture Product Catalog\n\nThen add product variants to the catalog list and drag it onto this component.";
        text.fontSize = 20f;
        text.fontStyle = FontStyles.Bold;
        text.color = darkTextColor;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
    }

    private void BuildDiscoverCallout(Transform parent)
    {
        GameObject root = MakeUIObject("DiscoverCallout", parent);
        RectTransform rt = root.GetComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 20f);
        rt.sizeDelta = new Vector2(calloutWidth, calloutHeight);

        UIImage card = root.AddComponent<UIImage>();
        ApplyRoundedImage(card, cardColor, calloutCornerRadius);
        card.raycastTarget = false;

        Shadow shadow = root.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(0f, -14f);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.22f);

        GameObject content = MakeUIObject("Content", root.transform);
        StretchInset(content, 28f);

        TextMeshProUGUI eyebrow = AddTMP("Eyebrow", content.transform);
        RectTransform eyebrowRT = eyebrow.GetComponent<RectTransform>();
        eyebrowRT.anchorMin = new Vector2(0f, 1f);
        eyebrowRT.anchorMax = new Vector2(1f, 1f);
        eyebrowRT.pivot = new Vector2(0.5f, 1f);
        eyebrowRT.anchoredPosition = Vector2.zero;
        eyebrowRT.sizeDelta = new Vector2(0f, 28f);

        eyebrow.text = GetBadge(_currentIndex).ToUpperInvariant();
        eyebrow.fontSize = eyebrowFontSize;
        eyebrow.fontStyle = FontStyles.Bold;
        eyebrow.color = softTextColor;
        eyebrow.alignment = TextAlignmentOptions.Left;
        eyebrow.raycastTarget = false;

        GameObject preview = MakeUIObject("PreviewImage", content.transform);
        RectTransform previewRT = preview.GetComponent<RectTransform>();
        previewRT.anchorMin = new Vector2(0f, 1f);
        previewRT.anchorMax = new Vector2(0f, 1f);
        previewRT.pivot = new Vector2(0f, 1f);
        previewRT.anchoredPosition = new Vector2(0f, -48f);
        previewRT.sizeDelta = new Vector2(calloutPreviewSize, calloutPreviewSize);

        UIImage previewImage = preview.AddComponent<UIImage>();
        ApplyRoundedImage(previewImage, GetFallbackImageColor(_currentIndex), previewImageCornerRadius);
        previewImage.raycastTarget = false;
        previewImage.preserveAspect = true;
        ApplyProductSpriteOrRuntimeUrl(previewImage, _currentIndex, false);

        TextMeshProUGUI title = AddTMP("Title", content.transform);
        RectTransform titleRT = title.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 1f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.pivot = new Vector2(0f, 1f);
        titleRT.anchoredPosition = new Vector2(calloutPreviewSize + 24f, -42f);
        titleRT.sizeDelta = new Vector2(-(calloutPreviewSize + 24f), 92f);

        title.text = discoverHeadline;
        title.fontSize = titleFontSize * 0.78f;
        title.fontStyle = FontStyles.Bold;
        title.color = darkTextColor;
        title.alignment = TextAlignmentOptions.Left;
        title.enableWordWrapping = true;
        title.raycastTarget = false;

        TextMeshProUGUI subtitle = AddTMP("Subtitle", content.transform);
        RectTransform subtitleRT = subtitle.GetComponent<RectTransform>();
        subtitleRT.anchorMin = new Vector2(0f, 1f);
        subtitleRT.anchorMax = new Vector2(1f, 1f);
        subtitleRT.pivot = new Vector2(0f, 1f);
        subtitleRT.anchoredPosition = new Vector2(calloutPreviewSize + 24f, -132f);
        subtitleRT.sizeDelta = new Vector2(-(calloutPreviewSize + 24f), 92f);

        subtitle.text = GetCalloutText(_currentIndex);
        subtitle.fontSize = subtitleFontSize;
        subtitle.fontStyle = FontStyles.Normal;
        subtitle.color = mutedTextColor;
        subtitle.alignment = TextAlignmentOptions.Left;
        subtitle.enableWordWrapping = true;
        subtitle.raycastTarget = false;

        GameObject button = BuildButton(
            "DiscoverButton",
            content.transform,
            discoverButtonText,
            primaryButtonColor,
            primaryButtonTextColor,
            buttonFontSize,
            Discover
        );

        RectTransform buttonRT = button.GetComponent<RectTransform>();
        buttonRT.anchorMin = new Vector2(0f, 0f);
        buttonRT.anchorMax = new Vector2(1f, 0f);
        buttonRT.pivot = new Vector2(0.5f, 0f);
        buttonRT.anchoredPosition = Vector2.zero;
        buttonRT.sizeDelta = new Vector2(0f, 58f);
    }

    private void BuildProductExperience(Transform parent)
    {
        float resolvedDetailsHeight = uiState == FurnitureBrowserUIState.Details ? GetResolvedDetailsHeight() : 0f;

        GameObject root = MakeUIObject("ProductExperience", parent);
        _animatedProductRoot = root.GetComponent<RectTransform>();
        _animatedProductRoot.anchorMin = new Vector2(0.5f, 1f);
        _animatedProductRoot.anchorMax = new Vector2(0.5f, 1f);
        _animatedProductRoot.pivot = new Vector2(0.5f, 1f);
        _animatedProductRoot.anchoredPosition = new Vector2(0f, -productTopOffset);
        _animatedProductRoot.sizeDelta = new Vector2(canvasWidth, cardHeight + detailsGap + resolvedDetailsHeight);

        _animatedProductCanvasGroup = root.AddComponent<CanvasGroup>();
        _animatedProductCanvasGroup.alpha = 1f;

        BuildProductCard(root.transform);

        if (uiState == FurnitureBrowserUIState.Details)
        {
            BuildProductDetails(root.transform);
        }
    }

    private void BuildProductCard(Transform parent)
    {
        GameObject cardOuter = MakeUIObject("ProductCard", parent);
        _productCardVisual = cardOuter.GetComponent<RectTransform>();
        _productCardVisual.anchorMin = new Vector2(0.5f, 1f);
        _productCardVisual.anchorMax = new Vector2(0.5f, 1f);
        _productCardVisual.pivot = new Vector2(0.5f, 1f);
        _productCardVisual.anchoredPosition = Vector2.zero;
        _productCardVisual.sizeDelta = new Vector2(cardWidth, cardHeight);

        _productCardBorder = cardOuter.AddComponent<UIImage>();
        ApplyRoundedImage(_productCardBorder, cardBorderColor, productCardCornerRadius);
        _productCardBorder.raycastTarget = true;

        Shadow shadow = cardOuter.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(0f, -16f);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.20f);

        MetaRayFurnitureCardHitbox hitbox = cardOuter.AddComponent<MetaRayFurnitureCardHitbox>();
        hitbox.Initialize(this);

        GameObject cardInner = MakeUIObject("CardInner", cardOuter.transform);
        StretchInset(cardInner, cardBorderThickness);

        UIImage innerImage = cardInner.AddComponent<UIImage>();
        ApplyRoundedImage(innerImage, cardColor, productInnerCornerRadius);
        innerImage.raycastTarget = false;

        BuildProductImageArea(cardInner.transform);
        BuildProductTextArea(cardInner.transform);
    }

    private void BuildProductImageArea(Transform parent)
    {
        GameObject imageArea = MakeUIObject("ImageArea", parent);
        RectTransform areaRT = imageArea.GetComponent<RectTransform>();
        areaRT.anchorMin = new Vector2(0f, 1f);
        areaRT.anchorMax = new Vector2(1f, 1f);
        areaRT.pivot = new Vector2(0.5f, 1f);
        areaRT.anchoredPosition = Vector2.zero;
        areaRT.sizeDelta = new Vector2(0f, imageHeight);

        GameObject imageClip = MakeUIObject("ImageClip", imageArea.transform);
        StretchFull(imageClip);

        UIImage clipImage = imageClip.AddComponent<UIImage>();
        ApplyRoundedImage(clipImage, Color.white, imageAreaCornerRadius);
        clipImage.raycastTarget = false;

        if (roundImageArea)
        {
            Mask mask = imageClip.AddComponent<Mask>();
            mask.showMaskGraphic = false;
        }

        GameObject productImageGO = MakeUIObject("ProductImage", imageClip.transform);
        RectTransform productImageRT = productImageGO.GetComponent<RectTransform>();
        productImageRT.anchorMin = new Vector2(0.5f, 0.5f);
        productImageRT.anchorMax = new Vector2(0.5f, 0.5f);
        productImageRT.pivot = new Vector2(0.5f, 0.5f);
        productImageRT.anchoredPosition = Vector2.zero;
        productImageRT.localScale = Vector3.one;

        Vector2 fallbackFillSize = GetProductImageAspectFillSize();
        productImageRT.sizeDelta = fallbackFillSize;

        UIImage productImage = productImageGO.AddComponent<UIImage>();
        productImage.color = GetFallbackImageColor(_currentIndex);
        productImage.raycastTarget = false;
        productImage.preserveAspect = false;
        productImage.type = UIImage.Type.Simple;

        ApplyProductSpriteOrRuntimeUrl(
            productImage,
            _currentIndex,
            productImageAspectFill,
            fallbackFillSize
        );

        GameObject overlay = MakeUIObject("SoftOverlay", imageClip.transform);
        RectTransform overlayRT = overlay.GetComponent<RectTransform>();
        overlayRT.anchorMin = new Vector2(0f, 0f);
        overlayRT.anchorMax = new Vector2(1f, 0.32f);
        overlayRT.offsetMin = new Vector2(imageAreaInset, imageAreaInset);
        overlayRT.offsetMax = new Vector2(-imageAreaInset, 0f);

        UIImage overlayImage = overlay.AddComponent<UIImage>();
        overlayImage.color = overlayColor;
        overlayImage.raycastTarget = false;

        BuildImagePills(imageArea.transform);
        BuildArrowButtons(imageArea.transform);
    }

    private void BuildImagePills(Transform parent)
    {
        string badgeValue = GetBadge(_currentIndex);

        if (!string.IsNullOrWhiteSpace(badgeValue))
        {
            GameObject badge = MakeUIObject("Badge", parent);
            RectTransform badgeRT = badge.GetComponent<RectTransform>();
            badgeRT.anchorMin = new Vector2(0f, 1f);
            badgeRT.anchorMax = new Vector2(0f, 1f);
            badgeRT.pivot = new Vector2(0f, 1f);
            badgeRT.anchoredPosition = new Vector2(18f, -18f);
            badgeRT.sizeDelta = new Vector2(166f, 36f);

            UIImage badgeBg = badge.AddComponent<UIImage>();
            ApplyRoundedImage(badgeBg, new Color(1f, 1f, 1f, 0.86f), pillCornerRadius);
            badgeBg.raycastTarget = false;

            TextMeshProUGUI badgeText = AddTMP("BadgeText", badge.transform);
            StretchFull(badgeText.gameObject);
            badgeText.text = badgeValue;
            badgeText.fontSize = eyebrowFontSize;
            badgeText.fontStyle = FontStyles.Bold;
            badgeText.color = darkTextColor;
            badgeText.alignment = TextAlignmentOptions.Center;
            badgeText.raycastTarget = false;
        }

        GameObject counter = MakeUIObject("Counter", parent);
        RectTransform counterRT = counter.GetComponent<RectTransform>();
        counterRT.anchorMin = new Vector2(1f, 1f);
        counterRT.anchorMax = new Vector2(1f, 1f);
        counterRT.pivot = new Vector2(1f, 1f);
        counterRT.anchoredPosition = new Vector2(-18f, -18f);
        counterRT.sizeDelta = new Vector2(86f, 36f);

        UIImage counterBg = counter.AddComponent<UIImage>();
        ApplyRoundedImage(counterBg, new Color(0.08f, 0.09f, 0.12f, 0.62f), pillCornerRadius);
        counterBg.raycastTarget = false;

        TextMeshProUGUI counterText = AddTMP("CounterText", counter.transform);
        StretchFull(counterText.gameObject);
        counterText.text = $"{_currentIndex + 1} / {GetProductCount()}";
        counterText.fontSize = eyebrowFontSize;
        counterText.fontStyle = FontStyles.Bold;
        counterText.color = Color.white;
        counterText.alignment = TextAlignmentOptions.Center;
        counterText.raycastTarget = false;
    }

    private void BuildArrowButtons(Transform parent)
    {
        RemoveExistingChildImmediately(parent, "PreviousButton");
        RemoveExistingChildImmediately(parent, "NextButton");

        GameObject previous = BuildButton(
            "PreviousButton",
            parent,
            "‹",
            new Color(1f, 1f, 1f, 0.88f),
            secondaryButtonTextColor,
            previousArrowFontSize,
            GoPrevious
        );

        _previousButtonRect = previous.GetComponent<RectTransform>();
        _previousButtonRect.anchorMin = new Vector2(0f, 0.5f);
        _previousButtonRect.anchorMax = new Vector2(0f, 0.5f);
        _previousButtonRect.pivot = new Vector2(0f, 0.5f);
        _previousButtonRect.sizeDelta = new Vector2(arrowButtonSize, arrowButtonSize);

        GameObject next = BuildButton(
            "NextButton",
            parent,
            "›",
            new Color(1f, 1f, 1f, 0.88f),
            secondaryButtonTextColor,
            nextArrowFontSize,
            GoNext
        );

        _nextButtonRect = next.GetComponent<RectTransform>();
        _nextButtonRect.anchorMin = new Vector2(1f, 0.5f);
        _nextButtonRect.anchorMax = new Vector2(1f, 0.5f);
        _nextButtonRect.pivot = new Vector2(1f, 0.5f);
        _nextButtonRect.sizeDelta = new Vector2(arrowButtonSize * nextArrowSizeMultiplier, arrowButtonSize * nextArrowSizeMultiplier);

        ApplyArrowButtonTransforms();
    }

    private void ApplyArrowButtonTransforms()
    {
        if (_previousButtonRect != null)
        {
            _previousButtonRect.anchoredPosition = previousButtonAnchoredPosition;
            _previousButtonRect.sizeDelta = new Vector2(arrowButtonSize, arrowButtonSize);
        }

        if (_nextButtonRect != null)
        {
            _nextButtonRect.anchoredPosition = nextButtonAnchoredPosition;
            _nextButtonRect.sizeDelta = new Vector2(
                arrowButtonSize * nextArrowSizeMultiplier,
                arrowButtonSize * nextArrowSizeMultiplier
            );
        }
    }

    private void BuildProductTextArea(Transform parent)
    {
        float textTop = imageHeight + 22f;

        TextMeshProUGUI title = AddTMP("ProductTitle", parent);
        RectTransform titleRT = title.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 1f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -textTop);
        titleRT.sizeDelta = new Vector2(-44f, 44f);

        title.text = GetTitle(_currentIndex);
        title.fontSize = titleFontSize;
        title.fontStyle = FontStyles.Bold;
        title.color = darkTextColor;
        title.alignment = TextAlignmentOptions.Left;
        title.enableWordWrapping = false;
        title.overflowMode = TextOverflowModes.Ellipsis;
        title.raycastTarget = false;

        TextMeshProUGUI subtitle = AddTMP("ProductSubtitle", parent);
        RectTransform subtitleRT = subtitle.GetComponent<RectTransform>();
        subtitleRT.anchorMin = new Vector2(0f, 1f);
        subtitleRT.anchorMax = new Vector2(1f, 1f);
        subtitleRT.pivot = new Vector2(0.5f, 1f);
        subtitleRT.anchoredPosition = new Vector2(0f, -(textTop + 44f));
        subtitleRT.sizeDelta = new Vector2(-44f, 30f);

        subtitle.text = $"{GetSubtitle(_currentIndex)}  •  {GetPrice(_currentIndex)}";
        subtitle.fontSize = subtitleFontSize;
        subtitle.fontStyle = FontStyles.Bold;
        subtitle.color = mutedTextColor;
        subtitle.alignment = TextAlignmentOptions.Left;
        subtitle.enableWordWrapping = false;
        subtitle.overflowMode = TextOverflowModes.Ellipsis;
        subtitle.raycastTarget = false;

        TextMeshProUGUI body = AddTMP("ProductBody", parent);
        RectTransform bodyRT = body.GetComponent<RectTransform>();
        bodyRT.anchorMin = new Vector2(0f, 1f);
        bodyRT.anchorMax = new Vector2(1f, 1f);
        bodyRT.pivot = new Vector2(0.5f, 1f);
        bodyRT.anchoredPosition = new Vector2(0f, -(textTop + 82f + subtitleBodyExtraGap));
        bodyRT.sizeDelta = new Vector2(-44f, 92f);

        body.text = GetShortDescription(_currentIndex);
        body.fontSize = bodyFontSize;
        body.fontStyle = FontStyles.Normal;
        body.color = softTextColor;
        body.alignment = TextAlignmentOptions.Left;
        body.enableWordWrapping = true;
        body.raycastTarget = false;

        BuildFinishSwatches(parent, new Vector2(22f, 112f));

        GameObject button = BuildButton(
            "DetailsButton",
            parent,
            uiState == FurnitureBrowserUIState.Details ? hideDetailsButtonText : detailsButtonText,
            uiState == FurnitureBrowserUIState.Details ? secondaryButtonColor : primaryButtonColor,
            uiState == FurnitureBrowserUIState.Details ? secondaryButtonTextColor : primaryButtonTextColor,
            buttonFontSize,
            ToggleDetails
        );

        RectTransform buttonRT = button.GetComponent<RectTransform>();
        buttonRT.anchorMin = new Vector2(0f, 0f);
        buttonRT.anchorMax = new Vector2(1f, 0f);
        buttonRT.pivot = new Vector2(0.5f, 0f);
        buttonRT.anchoredPosition = new Vector2(0f, 22f);
        buttonRT.sizeDelta = new Vector2(-44f, 58f);
    }

    private void BuildFinishSwatches(Transform parent, Vector2 anchoredPosition)
    {
        MetaRayFurnitureProductVariant product = GetProduct(_currentIndex);
        if (product == null) return;

        GameObject row = MakeUIObject("FinishSwatches", parent);
        RectTransform rowRT = row.GetComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0f, 0f);
        rowRT.anchorMax = new Vector2(1f, 0f);
        rowRT.pivot = new Vector2(0f, 0f);
        rowRT.anchoredPosition = anchoredPosition;
        rowRT.sizeDelta = new Vector2(-44f, 28f);

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        TextMeshProUGUI label = AddTMP("FinishLabel", row.transform);
        label.text = string.IsNullOrWhiteSpace(product.finish) ? "Finish" : product.finish;
        label.fontSize = 12f;
        label.fontStyle = FontStyles.Bold;
        label.color = mutedTextColor;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        SetPreferredSize(label.gameObject, 260f, 28f);

        if (product.finishColors != null)
        {
            for (int i = 0; i < product.finishColors.Count; i++)
            {
                GameObject dot = MakeUIObject($"FinishColor_{i}", row.transform);
                RectTransform dotRT = dot.GetComponent<RectTransform>();
                dotRT.localScale = finishColorChipScale;

                LayoutElement le = dot.AddComponent<LayoutElement>();
                le.preferredWidth = 24f;
                le.preferredHeight = 24f;
                le.minWidth = 24f;
                le.minHeight = 24f;

                UIImage img = dot.AddComponent<UIImage>();
                ApplyColorDotImage(img, product.finishColors[i]);
                img.raycastTarget = false;
            }
        }
    }

    private void BuildProductDetails(Transform parent)
    {
        float panelHeight = GetResolvedDetailsHeight();

        GameObject panel = MakeUIObject("ProductDetails", parent);
        RectTransform panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 1f);
        panelRT.anchorMax = new Vector2(0.5f, 1f);
        panelRT.pivot = new Vector2(0.5f, 1f);
        panelRT.anchoredPosition3D = GetDetailsAnchoredPosition3D();
        panelRT.localEulerAngles = detailsCardRotationEuler;
        panelRT.localScale = detailsCardScale;
        panelRT.sizeDelta = new Vector2(cardWidth, panelHeight);

        UIImage bg = panel.AddComponent<UIImage>();
        ApplyRoundedImageOrSprite(bg, detailsPanelColor, detailsPanelCornerRadius, detailsPanelSprite);
        bg.raycastTarget = false;

        Shadow shadow = panel.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(0f, -14f);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.16f);

        VerticalLayoutGroup panelLayout = panel.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(0, 0, 0, 0);
        panelLayout.spacing = 0f;
        panelLayout.childAlignment = TextAnchor.UpperLeft;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        BuildDetailsHeader(panel.transform);
        BuildDetailsBody(panel.transform);

        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRT);
    }

    private void BuildDetailsHeader(Transform parent)
    {
        GameObject header = MakeLayoutChild("DetailsHeader", parent, detailsHeaderHeight);
        UIImage headerBg = header.AddComponent<UIImage>();
        ApplyRoundedImageOrSprite(headerBg, detailsHeaderColor, detailsHeaderCornerRadius, detailsHeaderSprite);
        headerBg.raycastTarget = false;

        VerticalLayoutGroup layout = header.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 20, 16);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI eyebrow = AddTMP("Eyebrow", header.transform);
        eyebrow.text = "PRODUCT DETAILS";
        eyebrow.fontSize = 12f;
        eyebrow.fontStyle = FontStyles.Bold;
        eyebrow.characterSpacing = 8f;
        eyebrow.color = detailsHeaderTextColor;
        eyebrow.alignment = TextAlignmentOptions.Left;
        eyebrow.enableWordWrapping = false;
        eyebrow.overflowMode = TextOverflowModes.Ellipsis;
        SetPreferredHeight(eyebrow.gameObject, 24f);

        TextMeshProUGUI name = AddTMP("ProductName", header.transform);
        name.text = GetTitle(_currentIndex);
        name.fontSize = detailTitleFontSize + 4f;
        name.fontStyle = FontStyles.Bold;
        name.color = darkTextColor;
        name.alignment = TextAlignmentOptions.Left;
        name.enableWordWrapping = false;
        name.overflowMode = TextOverflowModes.Ellipsis;
        SetPreferredHeight(name.gameObject, 42f);
    }

    private void BuildDetailsBody(Transform parent)
    {
        float bodyHeight = GetResolvedDetailsHeight() - detailsHeaderHeight;

        GameObject body = MakeLayoutChild("DetailsBody", parent, bodyHeight);
        VerticalLayoutGroup layout = body.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(
            Mathf.RoundToInt(detailsBodyPaddingX),
            Mathf.RoundToInt(detailsBodyPaddingX),
            Mathf.RoundToInt(detailsBodyPaddingY),
            Mathf.RoundToInt(detailsBodyPaddingY)
        );
        layout.spacing = detailsBodySectionSpacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        BuildDetailsDimensionsSection(body.transform);
        BuildDetailsMaterialsSection(body.transform);
        BuildDetailsFeaturesSection(body.transform);
        BuildDetailsFinishSection(body.transform);
        BuildDetailsStorageSection(body.transform);
    }

    private void BuildDetailsDimensionsSection(Transform parent)
    {
        GameObject section = MakeCleanSection("DimensionsSection", parent, detailsDimensionsSectionHeight);
        BuildSectionTitle(section.transform, "DIMENSIONS");

        GameObject row = MakeLayoutChild("DimensionsRow", section.transform, detailsDimensionCardHeight);

        HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.padding = new RectOffset(0, 0, 0, 0);
        rowLayout.spacing = detailsDimensionCardSpacing;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;

        BuildDimensionItem(row.transform, "WIDTH", GetWidth(_currentIndex));
        BuildDimensionItem(row.transform, "HEIGHT", GetHeight(_currentIndex));
        BuildDimensionItem(row.transform, "DEPTH", GetDepth(_currentIndex));
        BuildDimensionItem(row.transform, "WEIGHT", GetWeight(_currentIndex));
    }

    private void BuildDimensionItem(Transform parent, string label, string value)
    {
        GameObject item = MakeUIObject(label + "Item", parent);

        LayoutElement le = item.AddComponent<LayoutElement>();
        le.minHeight = detailsDimensionCardHeight;
        le.preferredHeight = detailsDimensionCardHeight;
        le.flexibleHeight = 0f;
        le.minWidth = 0f;
        le.preferredWidth = 0f;
        le.flexibleWidth = 1f;

        UIImage bg = item.AddComponent<UIImage>();
        ApplyRoundedImageOrSprite(bg, detailsMutedCardColor, detailsMutedCardCornerRadius, detailsMutedCardSprite);
        bg.raycastTarget = false;

        VerticalLayoutGroup layout = item.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 11, 9);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI labelText = AddTMP("Label", item.transform);
        labelText.text = label;
        labelText.fontSize = 10f;
        labelText.fontStyle = FontStyles.Bold;
        labelText.characterSpacing = 2.4f;
        labelText.color = detailsHeaderTextColor;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.enableWordWrapping = false;
        labelText.overflowMode = TextOverflowModes.Ellipsis;
        SetPreferredHeight(labelText.gameObject, 17f);

        TextMeshProUGUI valueText = AddTMP("Value", item.transform);
        valueText.text = value;
        valueText.fontSize = detailBodyFontSize + 5f;
        valueText.fontStyle = FontStyles.Bold;
        valueText.color = darkTextColor;
        valueText.alignment = TextAlignmentOptions.Center;
        valueText.enableWordWrapping = false;
        valueText.overflowMode = TextOverflowModes.Ellipsis;
        SetPreferredHeight(valueText.gameObject, 28f);
    }

    private void BuildDetailsMaterialsSection(Transform parent)
    {
        GameObject section = MakeCleanSection("MaterialsSection", parent, detailsMaterialsSectionHeight);
        BuildSectionTitle(section.transform, "MATERIALS");

        TextMeshProUGUI text = AddTMP("MaterialsText", section.transform);
        text.text = GetMaterials(_currentIndex);
        text.fontSize = detailBodyFontSize + 1f;
        text.fontStyle = FontStyles.Normal;
        text.color = mutedTextColor;
        text.alignment = TextAlignmentOptions.Left;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        SetPreferredHeight(text.gameObject, detailsMaterialsTextHeight);
    }

    private void BuildDetailsFeaturesSection(Transform parent)
    {
        List<string> features = GetProduct(_currentIndex)?.features;
        float sectionHeight = GetCleanFeaturesSectionHeight();

        GameObject section = MakeCleanSection("FeaturesSection", parent, sectionHeight);
        BuildSectionTitle(section.transform, "FEATURES");

        if (features == null || features.Count == 0)
        {
            BuildFeatureText(section.transform, "—");
            return;
        }

        for (int i = 0; i < features.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(features[i])) continue;
            BuildFeatureText(section.transform, features[i]);
        }
    }

    private void BuildFeatureText(Transform parent, string value)
    {
        TextMeshProUGUI text = AddTMP("FeatureText", parent);
        text.text = value;
        text.fontSize = detailBodyFontSize;
        text.fontStyle = FontStyles.Normal;
        text.color = mutedTextColor;
        text.alignment = TextAlignmentOptions.Left;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        SetPreferredHeight(text.gameObject, detailsFeatureLineHeight);
    }

    private void BuildDetailsFinishSection(Transform parent)
    {
        GameObject section = MakeCleanSection("FinishSection", parent, detailsFinishSectionHeight);
        BuildSectionTitle(section.transform, "COLOR / FINISH");

        MetaRayFurnitureProductVariant product = GetProduct(_currentIndex);
        List<string> labels = GetFinishLabels(product);

        int count = Mathf.Max(
            labels.Count,
            product != null && product.finishColors != null ? product.finishColors.Count : 0
        );

        count = Mathf.Max(1, count);

        GameObject row = MakeLayoutChild("FinishRow", section.transform, detailsFinishSwatchHeight);

        HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.padding = new RectOffset(0, 0, 0, 0);
        rowLayout.spacing = detailsFinishSwatchSpacing;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;

        for (int i = 0; i < count; i++)
        {
            string label = i < labels.Count ? labels[i] : $"Finish {i + 1}";
            Color color = GetFinishColor(product, i);
            BuildFinishSwatchItem(row.transform, label, color, i);
        }
    }

    private void BuildFinishSwatchItem(Transform parent, string label, Color color, int index)
    {
        GameObject item = MakeUIObject($"FinishSwatch_{index}", parent);

        LayoutElement le = item.AddComponent<LayoutElement>();
        le.minHeight = detailsFinishSwatchHeight;
        le.preferredHeight = detailsFinishSwatchHeight;
        le.flexibleHeight = 0f;
        le.minWidth = 0f;
        le.preferredWidth = 0f;
        le.flexibleWidth = 1f;

        UIImage bg = item.AddComponent<UIImage>();
        ApplyRoundedImageOrSprite(
            bg,
            finishSwatchBackgroundColor,
            detailsMutedCardCornerRadius,
            finishSwatchBackgroundSprite != null ? finishSwatchBackgroundSprite : detailsMutedCardSprite
        );
        bg.raycastTarget = false;

        VerticalLayoutGroup layout = item.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 10, 10);
        layout.spacing = 7f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI labelText = AddTMP("Label", item.transform);
        labelText.text = label;
        labelText.fontSize = 11f;
        labelText.fontStyle = FontStyles.Bold;
        labelText.color = finishSwatchLabelColor;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.enableWordWrapping = false;
        labelText.overflowMode = TextOverflowModes.Ellipsis;
        SetPreferredHeight(labelText.gameObject, 18f);

        GameObject dot = MakeUIObject("ColorDot", item.transform);

        LayoutElement dotLE = dot.AddComponent<LayoutElement>();
        dotLE.minWidth = detailsFinishDotSize;
        dotLE.preferredWidth = detailsFinishDotSize;
        dotLE.minHeight = detailsFinishDotSize;
        dotLE.preferredHeight = detailsFinishDotSize;
        dotLE.flexibleWidth = 0f;
        dotLE.flexibleHeight = 0f;

        UIImage dotImage = dot.AddComponent<UIImage>();
        ApplyColorDotImage(dotImage, color);
        dotImage.raycastTarget = false;
    }

    private void BuildDetailsStorageSection(Transform parent)
    {
        GameObject section = MakeCleanSection("StorageSection", parent, detailsStorageSectionHeight);
        BuildSectionTitle(section.transform, "STORAGE");

        TextMeshProUGUI text = AddTMP("StorageText", section.transform);
        text.text = GetStorage(_currentIndex);
        text.fontSize = detailBodyFontSize + 1f;
        text.fontStyle = FontStyles.Normal;
        text.color = mutedTextColor;
        text.alignment = TextAlignmentOptions.Left;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        SetPreferredHeight(text.gameObject, detailsStorageTextHeight);
    }

    private GameObject MakeCleanSection(string objectName, Transform parent, float height)
    {
        GameObject section = MakeLayoutChild(objectName, parent, height);

        VerticalLayoutGroup layout = section.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = detailsSectionTitleGap;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        return section;
    }

    private void BuildSectionTitle(Transform parent, string label)
    {
        TextMeshProUGUI title = AddTMP("SectionTitle", parent);
        title.text = label;
        title.fontSize = 12f;
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 7f;
        title.color = detailsHeaderTextColor;
        title.alignment = TextAlignmentOptions.Left;
        title.enableWordWrapping = false;
        title.overflowMode = TextOverflowModes.Ellipsis;
        SetPreferredHeight(title.gameObject, detailsSectionTitleHeight);
    }

    private GameObject MakeLayoutChild(string objectName, Transform parent, float preferredHeight)
    {
        GameObject go = MakeUIObject(objectName, parent);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minHeight = preferredHeight;
        le.preferredHeight = preferredHeight;
        le.flexibleHeight = 0f;
        le.minWidth = 0f;
        le.preferredWidth = 0f;
        le.flexibleWidth = 1f;

        return go;
    }

    private GameObject BuildButton(
        string objectName,
        Transform parent,
        string label,
        Color bgColor,
        Color textColor,
        float fontSize,
        UnityAction onClick)
    {
        GameObject go = MakeUIObject(objectName, parent);

        UIImage image = go.AddComponent<UIImage>();
        ApplyRoundedImage(image, bgColor, buttonCornerRadius);
        image.raycastTarget = true;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        ColorBlock colors = button.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = Color.Lerp(bgColor, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(bgColor, Color.black, 0.08f);
        colors.selectedColor = Color.Lerp(bgColor, Color.white, 0.12f);
        colors.disabledColor = new Color(bgColor.r, bgColor.g, bgColor.b, 0.45f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);

        TextMeshProUGUI text = AddTMP("Label", go.transform);
        StretchFull(text.gameObject);
        text.text = label;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = textColor;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        return go;
    }

    private void StartProductTransition(int direction)
    {
        if (_animatedProductRoot == null || _animatedProductCanvasGroup == null) return;

        _transitionDirection = direction >= 0 ? 1 : -1;
        _transitionTimer = 0f;
        _isTransitioning = true;
        _transitionBasePosition = _animatedProductRoot.anchoredPosition;

        _animatedProductRoot.anchoredPosition =
            _transitionBasePosition + new Vector2(_transitionDirection * slideDistancePx, 0f);

        _animatedProductCanvasGroup.alpha = 0f;
    }

    private void AnimateTransition()
    {
        if (!_isTransitioning) return;
        if (_animatedProductRoot == null || _animatedProductCanvasGroup == null)
        {
            _isTransitioning = false;
            return;
        }

        _transitionTimer += Time.deltaTime;

        float duration = Mathf.Max(0.01f, transitionDuration);
        float t = Mathf.Clamp01(_transitionTimer / duration);
        float eased = 1f - Mathf.Pow(1f - t, 3f);

        Vector2 start = _transitionBasePosition + new Vector2(_transitionDirection * slideDistancePx, 0f);
        _animatedProductRoot.anchoredPosition = Vector2.Lerp(start, _transitionBasePosition, eased);
        _animatedProductCanvasGroup.alpha = eased;

        if (t >= 1f)
        {
            _animatedProductRoot.anchoredPosition = _transitionBasePosition;
            _animatedProductCanvasGroup.alpha = 1f;
            _isTransitioning = false;
        }
    }

    private void AnimateCardHover()
    {
        if (_productCardVisual == null) return;

        float target = _cardHovered ? hoverScale : 1f;
        Vector3 current = _productCardVisual.localScale;
        Vector3 desired = Vector3.one * target;

        float lerp = UApplication.isPlaying ? Time.deltaTime * scaleLerpSpeed : 1f;
        _productCardVisual.localScale = Vector3.Lerp(current, desired, lerp);
    }

    private void SubscribeRayState()
    {
        if (_canvasRayInteractable == null) return;
        if (_subscribedToRayState) return;

        _canvasRayInteractable.WhenStateChanged += HandleCanvasRayStateChanged;
        _subscribedToRayState = true;
    }

    private void UnsubscribeRayState()
    {
        if (_canvasRayInteractable == null) return;
        if (!_subscribedToRayState) return;

        _canvasRayInteractable.WhenStateChanged -= HandleCanvasRayStateChanged;
        _subscribedToRayState = false;
    }

    private void HandleCanvasRayStateChanged(InteractableStateChangeArgs args)
    {
        if (!debugLogs) return;

        if (args.NewState == InteractableState.Hover && args.PreviousState == InteractableState.Normal)
        {
            UDebug.Log("<color=#89CFF0><b>[MetaRayFurnitureBrowser]</b></color> HandRayInteractor is hovering the browser canvas.");
        }
        else if (args.NewState == InteractableState.Select)
        {
            UDebug.Log("<color=yellow><b>[MetaRayFurnitureBrowser]</b></color> HandRayInteractor selected on the browser canvas.");
        }
    }

    private void HandleKeyboardDebug()
    {
        if (arrowKeysChangeProduct)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                GoPrevious();
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                GoNext();
            }
        }

        if (enterKeyPressesPrimaryAction)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                PressPrimaryAction();
            }
        }

        if (spaceKeyPressesPrimaryAction)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                PressPrimaryAction();
            }
        }

        if (rKeyResetsToCallout)
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetToCallout();
            }
        }
    }

    private int GetProductCount()
    {
        if (productCatalog == null) return 0;
        if (productCatalog.products == null) return 0;
        return productCatalog.products.Count;
    }

    private MetaRayFurnitureProductVariant GetProduct(int index)
    {
        if (productCatalog == null) return null;
        if (productCatalog.products == null) return null;
        if (index < 0 || index >= productCatalog.products.Count) return null;
        return productCatalog.products[index];
    }

    private string GetTitle(int index)
    {
        MetaRayFurnitureProductVariant product = GetProduct(index);
        if (product != null && !string.IsNullOrWhiteSpace(product.productName)) return product.productName;
        return $"Product {index + 1}";
    }

    private string GetSubtitle(int index)
    {
        MetaRayFurnitureProductVariant product = GetProduct(index);
        if (product != null && !string.IsNullOrWhiteSpace(product.subtitle)) return product.subtitle;
        return "Furniture object variant";
    }

    private string GetPrice(int index)
    {
        MetaRayFurnitureProductVariant product = GetProduct(index);
        if (product != null && !string.IsNullOrWhiteSpace(product.priceText)) return product.priceText;
        return "—";
    }

    private string GetShortDescription(int index)
    {
        MetaRayFurnitureProductVariant product = GetProduct(index);
        if (product == null) return "";
        if (!string.IsNullOrWhiteSpace(product.shortDescription)) return product.shortDescription;
        return product.description;
    }

    private string GetLongDescription(int index)
    {
        MetaRayFurnitureProductVariant product = GetProduct(index);
        if (product == null) return "";
        if (!string.IsNullOrWhiteSpace(product.longDescription)) return product.longDescription;
        return product.description;
    }

    private string GetCalloutText(int index)
    {
        MetaRayFurnitureProductVariant product = GetProduct(index);
        if (product != null && !string.IsNullOrWhiteSpace(product.calloutText)) return product.calloutText;
        return $"I found a possible match for your room: {GetTitle(index)}. Open the card to compare style, size and details.";
    }

    private string GetBadge(int index)
    {
        MetaRayFurnitureProductVariant product = GetProduct(index);
        if (product != null && !string.IsNullOrWhiteSpace(product.badgeText)) return product.badgeText;
        return defaultBadgeText;
    }

    private string GetWidth(int index)
    {
        MetaRayFurnitureProductVariant product = GetProduct(index);
        return product != null && !string.IsNullOrWhiteSpace(product.widthText) ? product.widthText : "—";
    }

    private string GetHeight(int index)
    {
        MetaRayFurnitureProductVariant product = GetProduct(index);
        return product != null && !string.IsNullOrWhiteSpace(product.heightText) ? product.heightText : "—";
    }

    private string GetDepth(int index)
    {
        MetaRayFurnitureProductVariant product = GetProduct(index);
        return product != null && !string.IsNullOrWhiteSpace(product.depthText) ? product.depthText : "—";
    }

    private string GetWeight(int index)
    {
        MetaRayFurnitureProductVariant product = GetProduct(index);
        return product != null && !string.IsNullOrWhiteSpace(product.weightText) ? product.weightText : "—";
    }

    private string GetMaterials(int index)
    {
        MetaRayFurnitureProductVariant product = GetProduct(index);
        return product != null && !string.IsNullOrWhiteSpace(product.materialsText) ? product.materialsText : "—";
    }

    private string GetStorage(int index)
    {
        MetaRayFurnitureProductVariant product = GetProduct(index);
        return product != null && !string.IsNullOrWhiteSpace(product.storageText) ? product.storageText : "—";
    }

    private List<string> GetFinishLabels(MetaRayFurnitureProductVariant product)
    {
        List<string> labels = new List<string>();

        if (product == null) return labels;

        if (product.finishColorLabels != null && product.finishColorLabels.Count > 0)
        {
            for (int i = 0; i < product.finishColorLabels.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(product.finishColorLabels[i]))
                {
                    labels.Add(product.finishColorLabels[i]);
                }
            }
        }

        if (labels.Count > 0) return labels;

        if (!string.IsNullOrWhiteSpace(product.finish))
        {
            string[] split = product.finish.Split(
                new[] { "/", ",", "|" },
                StringSplitOptions.RemoveEmptyEntries
            );

            for (int i = 0; i < split.Length; i++)
            {
                string cleaned = split[i].Trim();

                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    labels.Add(cleaned);
                }
            }
        }

        return labels;
    }

    private Color GetFinishColor(MetaRayFurnitureProductVariant product, int index)
    {
        if (product != null &&
            product.finishColors != null &&
            index >= 0 &&
            index < product.finishColors.Count)
        {
            return product.finishColors[index];
        }

        switch (index % 3)
        {
            case 0:
                return new Color(0.78f, 0.66f, 0.47f, 1f);
            case 1:
                return new Color(0.95f, 0.95f, 0.92f, 1f);
            default:
                return new Color(0.16f, 0.17f, 0.18f, 1f);
        }
    }

    private Color GetFallbackImageColor(int index)
    {
        MetaRayFurnitureProductVariant product = GetProduct(index);

        if (product != null && product.useCustomFallbackColor)
        {
            return product.fallbackColor;
        }

        switch (index % 3)
        {
            case 0: return fallbackImageColorA;
            case 1: return fallbackImageColorB;
            default: return fallbackImageColorC;
        }
    }

    private void ApplyProductSpriteOrRuntimeUrl(
        UIImage image,
        int index,
        bool useAspectFill,
        Vector2 aspectFillSize = default)
    {
        if (image == null) return;

        MetaRayFurnitureProductVariant product = GetProduct(index);
        if (product == null) return;

        if (product.productImage != null)
        {
            image.sprite = product.productImage;
            image.color = Color.white;
            image.type = UIImage.Type.Simple;

            if (useAspectFill)
            {
                ApplyAspectFillToImage(image, aspectFillSize);
            }
            else
            {
                image.preserveAspect = true;
            }

            return;
        }

        if (!UApplication.isPlaying) return;
        if (!loadImageUrlsAtRuntime) return;
        if (string.IsNullOrWhiteSpace(product.sourceImageUrl)) return;

        string url = product.sourceImageUrl.Trim();

        if (_runtimeSpriteCache.TryGetValue(url, out Sprite cached) && cached != null)
        {
            image.sprite = cached;
            image.color = Color.white;
            image.type = UIImage.Type.Simple;

            if (useAspectFill)
            {
                ApplyAspectFillToImage(image, aspectFillSize);
            }
            else
            {
                image.preserveAspect = true;
            }

            return;
        }

        StartCoroutine(LoadSpriteFromUrl(url, image, useAspectFill, aspectFillSize));
    }

    private IEnumerator LoadSpriteFromUrl(
        string url,
        UIImage targetImage,
        bool useAspectFill,
        Vector2 aspectFillSize)
    {
        if (string.IsNullOrWhiteSpace(url)) yield break;
        if (_runtimeSpriteCache.ContainsKey(url)) yield break;

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            bool hasError = request.result != UnityWebRequest.Result.Success;
#else
            bool hasError = request.isNetworkError || request.isHttpError;
#endif

            if (hasError)
            {
                if (debugLogs)
                {
                    UDebug.LogWarning($"[MetaRayFurnitureBrowser] Could not load image URL: {url}\n{request.error}");
                }

                yield break;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            if (texture == null) yield break;

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );

            _runtimeSpriteCache[url] = sprite;

            if (targetImage != null)
            {
                targetImage.sprite = sprite;
                targetImage.color = Color.white;
                targetImage.type = UIImage.Type.Simple;

                if (useAspectFill)
                {
                    ApplyAspectFillToImage(targetImage, aspectFillSize);
                }
                else
                {
                    targetImage.preserveAspect = true;
                }
            }
        }
    }

    private Vector2 GetProductImageAspectFillSize()
    {
        float width = Mathf.Max(
            1f,
            cardWidth - cardBorderThickness * 2f - imageAreaInset * 2f
        );

        float height = Mathf.Max(
            1f,
            imageHeight - imageAreaInset * 2f
        );

        return new Vector2(width, height);
    }

    private void ApplyAspectFillToImage(UIImage image, Vector2 targetSize)
    {
        if (image == null) return;
        if (image.sprite == null) return;

        RectTransform imageRT = image.rectTransform;
        if (imageRT == null) return;

        if (targetSize.x <= 0f || targetSize.y <= 0f)
        {
            targetSize = GetProductImageAspectFillSize();
        }

        float spriteWidth = Mathf.Max(1f, image.sprite.rect.width);
        float spriteHeight = Mathf.Max(1f, image.sprite.rect.height);

        float spriteAspect = spriteWidth / spriteHeight;
        float targetAspect = targetSize.x / targetSize.y;

        float finalWidth;
        float finalHeight;

        if (spriteAspect > targetAspect)
        {
            finalHeight = targetSize.y * productImageZoom;
            finalWidth = finalHeight * spriteAspect;
        }
        else
        {
            finalWidth = targetSize.x * productImageZoom;
            finalHeight = finalWidth / spriteAspect;
        }

        image.preserveAspect = false;
        image.type = UIImage.Type.Simple;

        imageRT.anchorMin = new Vector2(0.5f, 0.5f);
        imageRT.anchorMax = new Vector2(0.5f, 0.5f);
        imageRT.pivot = new Vector2(0.5f, 0.5f);
        imageRT.anchoredPosition = Vector2.zero;
        imageRT.localScale = Vector3.one;
        imageRT.sizeDelta = new Vector2(finalWidth, finalHeight);
    }

    private void WarnMissingCatalog()
    {
        if (!debugLogs) return;
        UDebug.LogWarning("[MetaRayFurnitureBrowser] No Product Catalog assigned, or the catalog product list is empty.");
    }

#if UNITY_EDITOR
    private void TryAutoAssignDefaultSprites()
    {
        if (finishColorDotSprite == null)
        {
            string roundCornersPath = "Packages/com.meta.xr.mrutilitykit/Core/Textures/UI/RoundCorners.png";
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(roundCornersPath);

            if (assets != null)
            {
                for (int i = 0; i < assets.Length; i++)
                {
                    if (assets[i] is Sprite sprite && sprite.name == "RoundCorners_4")
                    {
                        finishColorDotSprite = sprite;
                        EditorUtility.SetDirty(this);
                        break;
                    }
                }
            }
        }
    }
#else
    private void TryAutoAssignDefaultSprites()
    {
    }
#endif

    private void ClearGeneratedObjects()
    {
        DestroyChildByName("Generated_MetaRayFurnitureBrowser");
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
        _animatedProductRoot = null;
        _animatedProductCanvasGroup = null;
        _productCardVisual = null;
        _productCardBorder = null;
        _previousButtonRect = null;
        _nextButtonRect = null;
    }

    private void DestroyChildByName(string childName)
    {
        Transform child = transform.Find(childName);

        if (child != null)
        {
            DestroySmart(child.gameObject);
        }
    }

    private static void RemoveExistingChildImmediately(Transform parent, string childName)
    {
        if (parent == null) return;

        Transform existing = parent.Find(childName);
        if (existing == null) return;

        GameObject go = existing.gameObject;

        if (UApplication.isPlaying)
        {
            go.SetActive(false);
            Destroy(go);
        }
        else
        {
            DestroyImmediate(go);
        }
    }

    private GameObject MakeUIObject(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = parent != null ? parent.gameObject.layer : gameObject.layer;
        return go;
    }

    private static TextMeshProUGUI AddTMP(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        if (parent != null)
        {
            go.layer = parent.gameObject.layer;
        }

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.enableAutoSizing = false;
        tmp.raycastTarget = false;
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
        {
            le = go.AddComponent<LayoutElement>();
        }

        le.minHeight = height;
        le.preferredHeight = height;
        le.flexibleHeight = 0f;
    }

    private static void SetPreferredSize(GameObject go, float width, float height)
    {
        LayoutElement le = go.GetComponent<LayoutElement>();

        if (le == null)
        {
            le = go.AddComponent<LayoutElement>();
        }

        le.minWidth = width;
        le.preferredWidth = width;
        le.minHeight = height;
        le.preferredHeight = height;
        le.flexibleWidth = 0f;
        le.flexibleHeight = 0f;
    }

    private static T AddOrGet<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();

        if (component == null)
        {
            component = go.AddComponent<T>();
        }

        return component;
    }

    private static void DestroySmart(GameObject go)
    {
        if (go == null) return;

        if (UApplication.isPlaying)
        {
            go.SetActive(false);
            Destroy(go);
        }
        else
        {
            DestroyImmediate(go);
        }
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

    private void ApplyRoundedImageOrSprite(UIImage image, Color color, int radius, Sprite overrideSprite)
    {
        if (image == null) return;

        if (overrideSprite != null)
        {
            image.sprite = overrideSprite;
            image.type = UIImage.Type.Simple;
            image.color = color;
            return;
        }

        ApplyRoundedImage(image, color, radius);
    }

    private void ApplyColorDotImage(UIImage image, Color color)
    {
        if (image == null) return;

        if (finishColorDotSprite != null)
        {
            image.sprite = finishColorDotSprite;
            image.type = UIImage.Type.Simple;
            image.color = color;
            image.preserveAspect = false;
            return;
        }

        ApplyRoundedImage(image, color, colorDotCornerRadius);
    }

    private static void TryConfigureRectTransformBoundsClipperDriver(
        RectTransformBoundsClipperDriver driver,
        BoundsClipper boundsClipper)
    {
        if (driver == null || boundsClipper == null) return;

        RectTransform rectTransform = driver.GetComponent<RectTransform>();
        Type driverType = typeof(RectTransformBoundsClipperDriver);

        MethodInfo[] methods = driverType.GetMethods(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

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
                {
                    args[i] = boundsClipper;
                }
                else if (p.IsAssignableFrom(typeof(RectTransform)))
                {
                    args[i] = rectTransform;
                }
                else if (p.IsAssignableFrom(typeof(Transform)))
                {
                    args[i] = rectTransform;
                }
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
                // SDK-version-safe fallback below.
            }
        }

        SetFieldIfExists(driver, "_boundsClipper", boundsClipper);
        SetFieldIfExists(driver, "_rectTransform", rectTransform);

        MethodInfo resizeMethod = driverType.GetMethod(
            "Resize",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
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

    private static void SetFieldIfExists(object target, string fieldName, object value)
    {
        if (target == null) return;

        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (field == null) return;

        try
        {
            field.SetValue(target, value);
        }
        catch
        {
            // Ignore SDK-version-specific field restrictions.
        }
    }
}

[CreateAssetMenu(
    fileName = "Furniture Product Catalog",
    menuName = "XRCC/Furniture Product Catalog"
)]
public class MetaRayFurnitureProductCatalog : ScriptableObject
{
    [Header("Product Variants")]
    public List<MetaRayFurnitureProductVariant> products = new List<MetaRayFurnitureProductVariant>();
}

[Serializable]
public class MetaRayFurnitureProductVariant
{
    [Header("Identity")]
    public int id;
    public string productName = "Natural Birch Cabinet";
    public string subtitle = "Storage cabinet";
    public string badgeText = "Room fit";
    public string priceText = "$649";

    [Header("Image")]
    public Sprite productImage;
    public string sourceImageUrl;

    [Header("Copy")]
    [TextArea(2, 4)]
    public string calloutText = "A clean storage cabinet was found for this room. Open it to compare dimensions, materials, finish and storage.";

    [TextArea(2, 4)]
    public string shortDescription = "A refined storage cabinet with a natural birch finish and clean vertical proportions.";

    [TextArea(3, 7)]
    public string description = "A refined storage cabinet with a natural birch finish, matte panel doors and practical adjustable shelving.";

    [TextArea(3, 7)]
    public string longDescription = "A refined storage cabinet with a natural birch finish, matte panel doors and practical adjustable shelving.";

    [Header("Dimensions")]
    public string widthText = "201 cm";
    public string heightText = "93 cm";
    public string depthText = "64 cm";
    public string weightText = "38 kg";

    [Header("Features")]
    public List<string> features = new List<string>
    {
        "Soft-close hinges",
        "Adjustable interior shelving",
        "Cable management cutout",
        "Anti-tip wall anchor included"
    };

    [Header("Materials / Finish")]
    [TextArea(2, 4)]
    public string materialsText = "Solid birch frame, MDF panel doors, steel hardware";

    public string finish = "Natural Birch / Matte White / Anthracite";

    public List<Color> finishColors = new List<Color>
    {
        new Color(0.78f, 0.66f, 0.47f, 1f),
        new Color(0.95f, 0.95f, 0.92f, 1f),
        new Color(0.16f, 0.17f, 0.18f, 1f)
    };

    public List<string> finishColorLabels = new List<string>
    {
        "Natural Birch",
        "Matte White",
        "Anthracite"
    };

    [Header("Storage / Included")]
    [TextArea(2, 4)]
    public string storageText = "3 large doors, 4 adjustable shelves";

    public List<string> includedParts = new List<string>();

    [Header("Fallback Visual")]
    public bool useCustomFallbackColor = true;
    public Color fallbackColor = new Color(0.78f, 0.66f, 0.47f, 1f);

    [Header("Events")]
    public UnityEvent onShown;
    public UnityEvent onDiscovered;
    public UnityEvent onDetailsUnlocked;
}

public class MetaRayFurnitureCardHitbox : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    [SerializeField] private MetaRayFurnitureBrowser browser;

    public void Initialize(MetaRayFurnitureBrowser owner)
    {
        browser = owner;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (browser == null) return;
        browser.SetCardHovered(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (browser == null) return;
        browser.SetCardHovered(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (browser == null) return;

        if (browser.cardClickUnlocksDetails)
        {
            browser.UnlockDetails();
        }
    }
}