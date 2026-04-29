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
using UComponent = UnityEngine.Component;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Drop this on an empty GameObject named BeforeAfterUI.
/// It automatically builds a world-space Meta Interaction SDK compatible Before / After slider UI.
///
/// Generated hierarchy:
/// - Background
/// - Label
/// - Slider
///   - Background
///   - Fill Area
///   - Handle Slide Area
/// - ISDK_RayCanvasInteraction
///   - Surface
/// - ISDK_PokeCanvasInteraction
///   - Surface
///
/// Notes:
/// - Runs in OnValidate so you can tweak it in the editor.
/// - Does NOT reset/move the GameObject position or rotation.
/// - Only sets the Canvas size and local scale.
/// - Slider can optionally blend two renderer roots: Before root fades out, After root fades in.
/// - Also exposes onSliderValueChanged so you can hook it into your own splat opacity / reveal scripts.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
[RequireComponent(typeof(GraphicRaycaster))]
public class MetaRayBeforeAfterSliderUI : MonoBehaviour
{
    [Serializable]
    public class SliderFloatEvent : UnityEvent<float> { }

    [Header("OnValidate / Editor")]
    public bool rebuildOnValidate = true;

    [Tooltip("If true, the slider value also updates target renderers while editing in the Unity editor.")]
    public bool previewBlendInEditMode = true;

    [Tooltip("Creates an EventSystem + PointableCanvasModule if none exists.")]
    public bool autoCreateEventSystem = true;

    [Header("World Space Canvas")]
    public Camera eventCamera;

    [Tooltip("Local scale applied to this UI object. Position and rotation are left untouched.")]
    public float worldScale = 0.001f;

    [Tooltip("Matches the screenshot surface size.")]
    public float canvasWidth = 380f;

    [Tooltip("Matches the screenshot surface size.")]
    public float canvasHeight = 100f;

    [Header("Interaction Surfaces")]
    public PlaneSurface.NormalFacing raySurfaceFacing = PlaneSurface.NormalFacing.Backward;
    public PlaneSurface.NormalFacing pokeSurfaceFacing = PlaneSurface.NormalFacing.Backward;

    [Header("Slider Value")]
    [Range(0f, 1f)]
    public float sliderValue = 0.5f;

    public float minValue = 0f;
    public float maxValue = 1f;
    public bool wholeNumbers = false;

    [Tooltip("Called when the UI slider changes. Value is normalized 0-1.")]
    public SliderFloatEvent onSliderValueChanged = new SliderFloatEvent();

    [Header("Before / After Target Roots")]
    [Tooltip("Optional. Renderers under this object fade OUT as the slider moves right.")]
    public GameObject beforeRoot;

    [Tooltip("Optional. Renderers under this object fade IN as the slider moves right.")]
    public GameObject afterRoot;

    [Tooltip("If true, renderers are collected from beforeRoot and afterRoot automatically.")]
    public bool collectRenderersFromRoots = true;

    [Tooltip("Extra before renderers if you do not want to use a full root object.")]
    public List<Renderer> extraBeforeRenderers = new List<Renderer>();

    [Tooltip("Extra after renderers if you do not want to use a full root object.")]
    public List<Renderer> extraAfterRenderers = new List<Renderer>();

    [Header("Target Blend Settings")]
    public bool applyBlendToRenderers = true;

    [Tooltip("Uses MaterialPropertyBlock so materials are not duplicated.")]
    public bool useMaterialPropertyBlocks = true;

    [Tooltip("Good for Gaussian splat / custom shaders that expose _Opacity.")]
    public string opacityPropertyName = "_Opacity";

    [Tooltip("Secondary fallback opacity property.")]
    public string secondaryOpacityPropertyName = "_Alpha";

    [Tooltip("Also tries to set alpha on _BaseColor / _Color.")]
    public bool alsoSetColorAlpha = true;

    public string baseColorPropertyName = "_BaseColor";
    public string colorPropertyName = "_Color";

    [Tooltip("Optional. If true, roots are disabled when their alpha becomes almost zero.")]
    public bool hideRootsWhenFullyTransparent = false;

    [Range(0f, 0.2f)]
    public float hideAlphaThreshold = 0.01f;

    [Header("Generated Background")]
    public bool showBackground = true;
    public Color backgroundColor = new Color(0.98f, 0.985f, 0.995f, 0.92f);
    public Color backgroundBorderColor = new Color(0.78f, 0.80f, 0.86f, 0.65f);
    public float backgroundBorderThickness = 2f;

    [Header("Label")]
    public bool showLabel = true;
    public string beforeLabel = "BEFORE";
    public string afterLabel = "AFTER";
    public bool showPercentInLabel = true;
    public float labelFontSize = 13f;
    public Color labelColor = new Color(0.10f, 0.11f, 0.16f, 0.94f);
    public Color labelMutedColor = new Color(0.42f, 0.45f, 0.52f, 0.9f);
    public float labelTopOffset = 12f;
    public float labelHeight = 24f;

    [Header("Slider Layout")]
    public float sliderWidth = 320f;
    public float sliderHeight = 30f;
    public Vector2 sliderAnchoredPosition = new Vector2(0f, -20f);

    [Tooltip("Visual track height inside the slider rect.")]
    public float sliderTrackHeight = 12f;

    [Tooltip("Handle size in pixels.")]
    public float handleSize = 34f;

    [Header("Slider Style")]
    public Color sliderTrackColor = new Color(0.76f, 0.77f, 0.80f, 0.88f);
    public Color sliderFillColor = new Color(0.11f, 0.12f, 0.16f, 0.96f);
    public Color sliderHandleColor = new Color(1f, 1f, 1f, 0.98f);
    public Color sliderHandlePressedColor = new Color(0.88f, 0.89f, 0.92f, 1f);
    public Color sliderHandleHighlightColor = new Color(1f, 1f, 1f, 1f);
    public Color sliderDisabledColor = new Color(0.55f, 0.55f, 0.58f, 0.4f);

    [Header("Rounded Corners")]
    public bool useGeneratedRoundedCorners = true;
    [Range(1, 64)] public int backgroundCornerRadius = 26;
    [Range(1, 64)] public int trackCornerRadius = 12;
    [Range(1, 64)] public int fillCornerRadius = 12;
    [Range(1, 64)] public int handleCornerRadius = 64;

    [Header("Debug")]
    public bool debugLogs = false;

    [SerializeField, HideInInspector] private GameObject _backgroundGO;
    [SerializeField, HideInInspector] private GameObject _labelGO;
    [SerializeField, HideInInspector] private GameObject _sliderGO;
    [SerializeField, HideInInspector] private GameObject _rayInteractionGO;
    [SerializeField, HideInInspector] private GameObject _pokeInteractionGO;

    private Canvas _canvas;
    private CanvasScaler _canvasScaler;
    private GraphicRaycaster _graphicRaycaster;
    private RectTransform _rectTransform;

    private Slider _slider;
    private TextMeshProUGUI _labelText;
    private UIImage _fillImage;
    private UIImage _handleImage;

    private PointableCanvas _rayPointableCanvas;
    private PointableCanvas _pokePointableCanvas;
    private RayInteractable _rayInteractable;
    private PokeInteractable _pokeInteractable;

    private bool _suppressSliderCallback;
    private bool _isBuilding;

    private MaterialPropertyBlock _propertyBlock;

    private static readonly Dictionary<int, Sprite> RoundedSpriteCache = new Dictionary<int, Sprite>();

#if UNITY_EDITOR
    private bool _editorRebuildQueued;
#endif

    private const string BackgroundName = "Background";
    private const string LabelName = "Label";
    private const string SliderName = "Slider";
    private const string RayInteractionName = "ISDK_RayCanvasInteraction";
    private const string PokeInteractionName = "ISDK_PokeCanvasInteraction";
    private const string SurfaceName = "Surface";

    private void Reset()
    {
        GrabRequiredComponents();
        ClampValues();
        ConfigureCanvas();

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
        ClampValues();
        ConfigureCanvas();

        if (UApplication.isPlaying)
        {
            RebuildUI();
        }
    }

    private void Start()
    {
        if (!UApplication.isPlaying) return;

        GrabRequiredComponents();
        ClampValues();
        ConfigureCanvas();
        RebuildUI();
        ApplyCurrentBlend(false);
    }

    private void Update()
    {
        if (_slider == null) return;

        float normalized = Mathf.InverseLerp(minValue, maxValue, _slider.value);

        if (!Mathf.Approximately(sliderValue, normalized))
        {
            sliderValue = Mathf.Clamp01(normalized);
            UpdateLabelText();

            if (UApplication.isPlaying || previewBlendInEditMode)
            {
                ApplyCurrentBlend(false);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        GrabRequiredComponents();
        ClampValues();
        ConfigureCanvas();
        PushInspectorValueToSlider();

        if (previewBlendInEditMode)
        {
            ApplyCurrentBlend(false);
        }

        if (!rebuildOnValidate) return;
        if (_isBuilding) return;

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
            ClampValues();
            ConfigureCanvas();
            RebuildUI();

            if (previewBlendInEditMode)
            {
                ApplyCurrentBlend(false);
            }

            EditorUtility.SetDirty(this);
        };
    }
#endif

    [ContextMenu("Rebuild Before After Slider UI")]
    public void RebuildUI()
    {
        if (_isBuilding) return;

        _isBuilding = true;

        try
        {
            GrabRequiredComponents();
            ClampValues();
            ConfigureCanvas();

            ClearGeneratedObjects();

            if (showBackground)
            {
                BuildBackground();
            }

            if (showLabel)
            {
                BuildLabel();
            }

            BuildSlider();
            BuildRayCanvasInteraction();
            BuildPokeCanvasInteraction();

            EnsureEventSystemAndPointableModule();

            PushInspectorValueToSlider();
            UpdateLabelText();
            ApplyCurrentBlend(false);

            if (debugLogs)
            {
                UDebug.Log($"<b>[MetaRayBeforeAfterSliderUI]</b> Rebuilt UI. Value: {sliderValue:0.00}");
            }
        }
        finally
        {
            _isBuilding = false;
        }
    }

    public void SetSliderValue(float normalizedValue)
    {
        normalizedValue = Mathf.Clamp01(normalizedValue);

        if (Mathf.Approximately(sliderValue, normalizedValue))
        {
            PushInspectorValueToSlider();
            return;
        }

        sliderValue = normalizedValue;
        PushInspectorValueToSlider();
        UpdateLabelText();
        ApplyCurrentBlend(true);
    }

    public void SetBefore()
    {
        SetSliderValue(0f);
    }

    public void SetAfter()
    {
        SetSliderValue(1f);
    }

    public void SetHalfway()
    {
        SetSliderValue(0.5f);
    }

    private void HandleSliderChanged(float rawValue)
    {
        if (_suppressSliderCallback) return;

        sliderValue = Mathf.Clamp01(Mathf.InverseLerp(minValue, maxValue, rawValue));
        UpdateLabelText();
        ApplyCurrentBlend(true);
    }

    private void ApplyCurrentBlend(bool invokeEvent)
    {
        sliderValue = Mathf.Clamp01(sliderValue);

        float beforeAlpha = 1f - sliderValue;
        float afterAlpha = sliderValue;

        if (hideRootsWhenFullyTransparent)
        {
            SetRootActiveIfValid(beforeRoot, beforeAlpha > hideAlphaThreshold);
            SetRootActiveIfValid(afterRoot, afterAlpha > hideAlphaThreshold);
        }

        if (applyBlendToRenderers)
        {
            ApplyAlphaToRendererSet(beforeRoot, extraBeforeRenderers, beforeAlpha);
            ApplyAlphaToRendererSet(afterRoot, extraAfterRenderers, afterAlpha);
        }

        if (invokeEvent && (UApplication.isPlaying || previewBlendInEditMode))
        {
            onSliderValueChanged?.Invoke(sliderValue);
        }
    }

    private void SetRootActiveIfValid(GameObject root, bool active)
    {
        if (root == null) return;
        if (root == gameObject) return;

        if (root.activeSelf != active)
        {
            root.SetActive(active);
        }
    }

    private void ApplyAlphaToRendererSet(GameObject root, List<Renderer> extraRenderers, float alpha)
    {
        HashSet<Renderer> renderers = new HashSet<Renderer>();

        if (collectRenderersFromRoots && root != null)
        {
            Renderer[] found = root.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                {
                    renderers.Add(found[i]);
                }
            }
        }

        if (extraRenderers != null)
        {
            for (int i = 0; i < extraRenderers.Count; i++)
            {
                if (extraRenderers[i] != null)
                {
                    renderers.Add(extraRenderers[i]);
                }
            }
        }

        foreach (Renderer rendererTarget in renderers)
        {
            ApplyAlphaToRenderer(rendererTarget, alpha);
        }
    }

    private void ApplyAlphaToRenderer(Renderer rendererTarget, float alpha)
    {
        if (rendererTarget == null) return;

        alpha = Mathf.Clamp01(alpha);

        Material[] sharedMaterials = rendererTarget.sharedMaterials;
        if (sharedMaterials == null || sharedMaterials.Length == 0) return;

        if (useMaterialPropertyBlocks)
        {
            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            rendererTarget.GetPropertyBlock(_propertyBlock);

            bool wroteAnyProperty = false;

            if (!string.IsNullOrWhiteSpace(opacityPropertyName) &&
                AnyMaterialHasProperty(sharedMaterials, opacityPropertyName))
            {
                _propertyBlock.SetFloat(opacityPropertyName, alpha);
                wroteAnyProperty = true;
            }

            if (!string.IsNullOrWhiteSpace(secondaryOpacityPropertyName) &&
                AnyMaterialHasProperty(sharedMaterials, secondaryOpacityPropertyName))
            {
                _propertyBlock.SetFloat(secondaryOpacityPropertyName, alpha);
                wroteAnyProperty = true;
            }

            if (alsoSetColorAlpha)
            {
                if (!string.IsNullOrWhiteSpace(baseColorPropertyName) &&
                    TryGetFirstMaterialColor(sharedMaterials, baseColorPropertyName, out Color baseColor))
                {
                    baseColor.a = alpha;
                    _propertyBlock.SetColor(baseColorPropertyName, baseColor);
                    wroteAnyProperty = true;
                }

                if (!string.IsNullOrWhiteSpace(colorPropertyName) &&
                    TryGetFirstMaterialColor(sharedMaterials, colorPropertyName, out Color color))
                {
                    color.a = alpha;
                    _propertyBlock.SetColor(colorPropertyName, color);
                    wroteAnyProperty = true;
                }
            }

            if (wroteAnyProperty)
            {
                rendererTarget.SetPropertyBlock(_propertyBlock);
            }

            return;
        }

        Material[] runtimeMaterials = rendererTarget.materials;
        if (runtimeMaterials == null) return;

        for (int i = 0; i < runtimeMaterials.Length; i++)
        {
            Material mat = runtimeMaterials[i];
            if (mat == null) continue;

            if (!string.IsNullOrWhiteSpace(opacityPropertyName) && mat.HasProperty(opacityPropertyName))
            {
                mat.SetFloat(opacityPropertyName, alpha);
            }

            if (!string.IsNullOrWhiteSpace(secondaryOpacityPropertyName) && mat.HasProperty(secondaryOpacityPropertyName))
            {
                mat.SetFloat(secondaryOpacityPropertyName, alpha);
            }

            if (alsoSetColorAlpha)
            {
                if (!string.IsNullOrWhiteSpace(baseColorPropertyName) && mat.HasProperty(baseColorPropertyName))
                {
                    Color c = mat.GetColor(baseColorPropertyName);
                    c.a = alpha;
                    mat.SetColor(baseColorPropertyName, c);
                }

                if (!string.IsNullOrWhiteSpace(colorPropertyName) && mat.HasProperty(colorPropertyName))
                {
                    Color c = mat.GetColor(colorPropertyName);
                    c.a = alpha;
                    mat.SetColor(colorPropertyName, c);
                }
            }
        }
    }

    private static bool AnyMaterialHasProperty(Material[] materials, string propertyName)
    {
        if (materials == null) return false;
        if (string.IsNullOrWhiteSpace(propertyName)) return false;

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null && materials[i].HasProperty(propertyName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetFirstMaterialColor(Material[] materials, string propertyName, out Color color)
    {
        color = Color.white;

        if (materials == null) return false;
        if (string.IsNullOrWhiteSpace(propertyName)) return false;

        for (int i = 0; i < materials.Length; i++)
        {
            Material mat = materials[i];

            if (mat != null && mat.HasProperty(propertyName))
            {
                color = mat.GetColor(propertyName);
                return true;
            }
        }

        return false;
    }

    private void GrabRequiredComponents()
    {
        _canvas = GetComponent<Canvas>();
        _canvasScaler = GetComponent<CanvasScaler>();
        _graphicRaycaster = GetComponent<GraphicRaycaster>();
        _rectTransform = GetComponent<RectTransform>();
    }

    private void ClampValues()
    {
        worldScale = Mathf.Max(0.0001f, worldScale);
        canvasWidth = Mathf.Max(100f, canvasWidth);
        canvasHeight = Mathf.Max(60f, canvasHeight);

        if (maxValue <= minValue)
        {
            maxValue = minValue + 1f;
        }

        sliderValue = Mathf.Clamp01(sliderValue);

        sliderWidth = Mathf.Clamp(sliderWidth, 40f, canvasWidth);
        sliderHeight = Mathf.Max(12f, sliderHeight);
        sliderTrackHeight = Mathf.Clamp(sliderTrackHeight, 2f, sliderHeight);
        handleSize = Mathf.Max(8f, handleSize);

        labelFontSize = Mathf.Max(4f, labelFontSize);
        labelHeight = Mathf.Max(8f, labelHeight);

        backgroundBorderThickness = Mathf.Clamp(backgroundBorderThickness, 0f, 12f);
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

    private void BuildBackground()
    {
        _backgroundGO = MakeUIObject(BackgroundName, transform);
        StretchFull(_backgroundGO);

        UIImage borderImage = _backgroundGO.AddComponent<UIImage>();
        ApplyRoundedImage(borderImage, backgroundBorderColor, backgroundCornerRadius);
        borderImage.raycastTarget = false;

        GameObject inner = MakeUIObject("Inner", _backgroundGO.transform);
        StretchInset(inner, backgroundBorderThickness);

        UIImage innerImage = inner.AddComponent<UIImage>();
        ApplyRoundedImage(innerImage, backgroundColor, Mathf.Max(1, backgroundCornerRadius - 2));
        innerImage.raycastTarget = false;

        Shadow shadow = _backgroundGO.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(0f, -4f);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.16f);
    }

    private void BuildLabel()
    {
        _labelGO = MakeUIObject(LabelName, transform);

        RectTransform rt = _labelGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -labelTopOffset);
        rt.sizeDelta = new Vector2(canvasWidth - 34f, labelHeight);

        _labelText = _labelGO.AddComponent<TextMeshProUGUI>();
        _labelText.enableAutoSizing = false;
        _labelText.fontSize = labelFontSize;
        _labelText.fontStyle = FontStyles.Bold;
        _labelText.characterSpacing = 6f;
        _labelText.color = labelColor;
        _labelText.alignment = TextAlignmentOptions.Center;
        _labelText.enableWordWrapping = false;
        _labelText.overflowMode = TextOverflowModes.Ellipsis;
        _labelText.raycastTarget = false;

        UpdateLabelText();
    }

    private void BuildSlider()
    {
        _sliderGO = MakeUIObject(SliderName, transform);

        RectTransform sliderRT = _sliderGO.GetComponent<RectTransform>();
        sliderRT.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRT.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRT.pivot = new Vector2(0.5f, 0.5f);
        sliderRT.anchoredPosition = sliderAnchoredPosition;
        sliderRT.sizeDelta = new Vector2(sliderWidth, sliderHeight);

        _slider = _sliderGO.AddComponent<Slider>();
        _slider.transition = Selectable.Transition.ColorTint;
        _slider.navigation = new Navigation { mode = Navigation.Mode.None };
        _slider.direction = Slider.Direction.LeftToRight;
        _slider.minValue = minValue;
        _slider.maxValue = maxValue;
        _slider.wholeNumbers = wholeNumbers;
        _slider.interactable = true;

        GameObject track = MakeUIObject("Background", _sliderGO.transform);
        RectTransform trackRT = track.GetComponent<RectTransform>();
        trackRT.anchorMin = new Vector2(0f, 0.5f);
        trackRT.anchorMax = new Vector2(1f, 0.5f);
        trackRT.pivot = new Vector2(0.5f, 0.5f);
        trackRT.offsetMin = new Vector2(handleSize * 0.5f, -sliderTrackHeight * 0.5f);
        trackRT.offsetMax = new Vector2(-handleSize * 0.5f, sliderTrackHeight * 0.5f);

        UIImage trackImage = track.AddComponent<UIImage>();
        ApplyRoundedImage(trackImage, sliderTrackColor, trackCornerRadius);
        trackImage.raycastTarget = false;

        GameObject fillArea = MakeUIObject("Fill Area", _sliderGO.transform);
        RectTransform fillAreaRT = fillArea.GetComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0f, 0.5f);
        fillAreaRT.anchorMax = new Vector2(1f, 0.5f);
        fillAreaRT.pivot = new Vector2(0.5f, 0.5f);
        fillAreaRT.offsetMin = new Vector2(handleSize * 0.5f, -sliderTrackHeight * 0.5f);
        fillAreaRT.offsetMax = new Vector2(-handleSize * 0.5f, sliderTrackHeight * 0.5f);

        GameObject fill = MakeUIObject("Fill", fillArea.transform);
        RectTransform fillRT = fill.GetComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0f, 0f);
        fillRT.anchorMax = new Vector2(1f, 1f);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        _fillImage = fill.AddComponent<UIImage>();
        ApplyRoundedImage(_fillImage, sliderFillColor, fillCornerRadius);
        _fillImage.raycastTarget = false;

        GameObject handleArea = MakeUIObject("Handle Slide Area", _sliderGO.transform);
        RectTransform handleAreaRT = handleArea.GetComponent<RectTransform>();
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.offsetMin = new Vector2(handleSize * 0.5f, 0f);
        handleAreaRT.offsetMax = new Vector2(-handleSize * 0.5f, 0f);

        GameObject handle = MakeUIObject("Handle", handleArea.transform);
        RectTransform handleRT = handle.GetComponent<RectTransform>();
        handleRT.anchorMin = new Vector2(0.5f, 0.5f);
        handleRT.anchorMax = new Vector2(0.5f, 0.5f);
        handleRT.pivot = new Vector2(0.5f, 0.5f);
        handleRT.sizeDelta = new Vector2(handleSize, handleSize);

        _handleImage = handle.AddComponent<UIImage>();
        ApplyRoundedImage(_handleImage, sliderHandleColor, handleCornerRadius);
        _handleImage.raycastTarget = true;

        Shadow handleShadow = handle.AddComponent<Shadow>();
        handleShadow.effectDistance = new Vector2(0f, -3f);
        handleShadow.effectColor = new Color(0f, 0f, 0f, 0.26f);

        _slider.fillRect = fillRT;
        _slider.handleRect = handleRT;
        _slider.targetGraphic = _handleImage;

        ColorBlock colors = _slider.colors;
        colors.normalColor = sliderHandleColor;
        colors.highlightedColor = sliderHandleHighlightColor;
        colors.pressedColor = sliderHandlePressedColor;
        colors.selectedColor = sliderHandleHighlightColor;
        colors.disabledColor = sliderDisabledColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        _slider.colors = colors;

        _slider.onValueChanged.RemoveAllListeners();
        _slider.onValueChanged.AddListener(HandleSliderChanged);
    }

    private void BuildRayCanvasInteraction()
    {
        _rayInteractionGO = MakeUIObject(RayInteractionName, transform);
        StretchFull(_rayInteractionGO);

        LayoutElement layoutElement = AddOrGet<LayoutElement>(_rayInteractionGO);
        layoutElement.ignoreLayout = true;

        _rayPointableCanvas = AddOrGet<PointableCanvas>(_rayInteractionGO);
        TryInvokeSingleArg(_rayPointableCanvas, "InjectAllPointableCanvas", _canvas);

        BuildSurfaceChild(
            _rayInteractionGO.transform,
            raySurfaceFacing,
            out PlaneSurface planeSurface,
            out ClippedPlaneSurface clippedPlaneSurface
        );

        _rayInteractable = AddOrGet<RayInteractable>(_rayInteractionGO);
        TryInvokeSingleArg(_rayInteractable, "InjectAllRayInteractable", clippedPlaneSurface);
        TryInvokeSingleArg(_rayInteractable, "InjectOptionalPointableElement", _rayPointableCanvas);
        TryInvokeSingleArg(_rayInteractable, "InjectOptionalSelectSurface", planeSurface);
    }

    private void BuildPokeCanvasInteraction()
    {
        _pokeInteractionGO = MakeUIObject(PokeInteractionName, transform);
        StretchFull(_pokeInteractionGO);

        LayoutElement layoutElement = AddOrGet<LayoutElement>(_pokeInteractionGO);
        layoutElement.ignoreLayout = true;

        _pokePointableCanvas = AddOrGet<PointableCanvas>(_pokeInteractionGO);
        TryInvokeSingleArg(_pokePointableCanvas, "InjectAllPointableCanvas", _canvas);

        BuildSurfaceChild(
            _pokeInteractionGO.transform,
            pokeSurfaceFacing,
            out PlaneSurface planeSurface,
            out ClippedPlaneSurface clippedPlaneSurface
        );

        _pokeInteractable = AddOrGet<PokeInteractable>(_pokeInteractionGO);

        TryInvokeSingleArg(_pokeInteractable, "InjectAllPokeInteractable", clippedPlaneSurface);
        TryInvokeSingleArg(_pokeInteractable, "InjectOptionalPointableElement", _pokePointableCanvas);

        SetMemberIfExists(_pokeInteractable, "_surfacePatch", clippedPlaneSurface);
        SetMemberIfExists(_pokeInteractable, "SurfacePatch", clippedPlaneSurface);
        SetMemberIfExists(_pokeInteractable, "_pointableElement", _pokePointableCanvas);
        SetMemberIfExists(_pokeInteractable, "PointableElement", _pokePointableCanvas);
    }

    private void BuildSurfaceChild(
        Transform parent,
        PlaneSurface.NormalFacing facing,
        out PlaneSurface planeSurface,
        out ClippedPlaneSurface clippedPlaneSurface)
    {
        GameObject surfaceGO = MakeUIObject(SurfaceName, parent);
        StretchFull(surfaceGO);

        planeSurface = AddOrGet<PlaneSurface>(surfaceGO);
        planeSurface.Facing = facing;

        BoundsClipper boundsClipper = AddOrGet<BoundsClipper>(surfaceGO);

        RectTransformBoundsClipperDriver clipperDriver = AddOrGet<RectTransformBoundsClipperDriver>(surfaceGO);
        TryConfigureRectTransformBoundsClipperDriver(clipperDriver, boundsClipper);

        clippedPlaneSurface = AddOrGet<ClippedPlaneSurface>(surfaceGO);
        clippedPlaneSurface.InjectAllClippedPlaneSurface(
            planeSurface,
            new List<IBoundsClipper> { boundsClipper }
        );
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
                UDebug.Log("<b>[MetaRayBeforeAfterSliderUI]</b> Created EventSystem.");
            }
        }

        PointableCanvasModule module = eventSystem.GetComponent<PointableCanvasModule>();

        if (module == null)
        {
            module = eventSystem.gameObject.AddComponent<PointableCanvasModule>();

            if (debugLogs)
            {
                UDebug.Log("<b>[MetaRayBeforeAfterSliderUI]</b> Added PointableCanvasModule.");
            }
        }
    }

    private void PushInspectorValueToSlider()
    {
        if (_slider == null) return;

        _suppressSliderCallback = true;

        _slider.minValue = minValue;
        _slider.maxValue = maxValue;
        _slider.wholeNumbers = wholeNumbers;
        _slider.SetValueWithoutNotify(Mathf.Lerp(minValue, maxValue, sliderValue));

        _suppressSliderCallback = false;
    }

    private void UpdateLabelText()
    {
        if (_labelText == null) return;

        if (showPercentInLabel)
        {
            int beforePercent = Mathf.RoundToInt((1f - sliderValue) * 100f);
            int afterPercent = Mathf.RoundToInt(sliderValue * 100f);
            _labelText.text = $"{beforeLabel} {beforePercent}%   /   {afterLabel} {afterPercent}%";
        }
        else
        {
            _labelText.text = $"{beforeLabel}   /   {afterLabel}";
        }

        _labelText.color = labelColor;
    }

    private void ClearGeneratedObjects()
    {
        DestroyChildByName(BackgroundName);
        DestroyChildByName(LabelName);
        DestroyChildByName(SliderName);
        DestroyChildByName(RayInteractionName);
        DestroyChildByName(PokeInteractionName);

        _backgroundGO = null;
        _labelGO = null;
        _sliderGO = null;
        _rayInteractionGO = null;
        _pokeInteractionGO = null;

        _slider = null;
        _labelText = null;
        _fillImage = null;
        _handleImage = null;

        _rayPointableCanvas = null;
        _pokePointableCanvas = null;
        _rayInteractable = null;
        _pokeInteractable = null;
    }

    private void DestroyChildByName(string childName)
    {
        Transform child = transform.Find(childName);
        if (child == null) return;

        DestroySmart(child.gameObject);
    }

    private GameObject MakeUIObject(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = parent != null ? parent.gameObject.layer : gameObject.layer;
        return go;
    }

    private static void StretchFull(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void StretchInset(GameObject go, float inset)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private static T AddOrGet<T>(GameObject go) where T : UComponent
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
            image.sprite = GetRoundedSprite(Mathf.Clamp(radius, 1, 64));
            image.type = UIImage.Type.Sliced;
            image.color = color;
            image.preserveAspect = false;
        }
        else
        {
            image.sprite = null;
            image.type = UIImage.Type.Simple;
            image.color = color;
        }
    }

    private static Sprite GetRoundedSprite(int radius)
    {
        radius = Mathf.Clamp(radius, 1, 64);

        if (RoundedSpriteCache.TryGetValue(radius, out Sprite cached) && cached != null)
        {
            return cached;
        }

        int size = Mathf.Max(32, radius * 2 + 4);
        Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
        texture.name = $"__GeneratedRoundedUISprite_R{radius}";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color white = Color.white;

        float r = radius;
        float left = r;
        float right = size - 1 - r;
        float bottom = r;
        float top = size - 1 - r;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x, left, right);
                float cy = Mathf.Clamp(y, bottom, top);
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                texture.SetPixel(x, y, distance <= r ? white : clear);
            }
        }

        texture.Apply(false, true);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius)
        );

        sprite.name = $"__RoundedUISprite_R{radius}";
        sprite.hideFlags = HideFlags.HideAndDontSave;

        RoundedSpriteCache[radius] = sprite;
        return sprite;
    }

    private static bool TryInvokeSingleArg(object target, string methodName, object arg)
    {
        if (target == null) return false;
        if (string.IsNullOrWhiteSpace(methodName)) return false;

        Type type = target.GetType();

        MethodInfo[] methods = type.GetMethods(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];

            if (method.Name != methodName) continue;

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 1) continue;

            Type parameterType = parameters[0].ParameterType;

            if (arg != null && !parameterType.IsAssignableFrom(arg.GetType()))
            {
                continue;
            }

            try
            {
                method.Invoke(target, new[] { arg });
                return true;
            }
            catch
            {
                // Ignore SDK-version differences.
            }
        }

        return false;
    }

    private static void SetMemberIfExists(object target, string memberName, object value)
    {
        if (target == null) return;
        if (string.IsNullOrWhiteSpace(memberName)) return;

        Type type = target.GetType();

        FieldInfo field = type.GetField(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (field != null)
        {
            try
            {
                if (value == null || field.FieldType.IsAssignableFrom(value.GetType()))
                {
                    field.SetValue(target, value);
                }
            }
            catch
            {
                // Ignore incompatible SDK fields.
            }

            return;
        }

        PropertyInfo property = type.GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (property == null) return;
        if (!property.CanWrite) return;

        try
        {
            if (value == null || property.PropertyType.IsAssignableFrom(value.GetType()))
            {
                property.SetValue(target, value);
            }
        }
        catch
        {
            // Ignore incompatible SDK properties.
        }
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

        for (int m = 0; m < methods.Length; m++)
        {
            MethodInfo method = methods[m];

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