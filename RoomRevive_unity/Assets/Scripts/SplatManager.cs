using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using GaussianSplatting.Runtime;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SplatManager : MonoBehaviour
{
    public static SplatManager Instance { get; private set; }

    public enum SplatRoom
    {
        None,

        CalmRoom,
        FastRoom,
        HostRoom,

        HostKitchenLightingCold,
        HostKitchenLightingWarm,
        HostKitchenNewCabinetA,
        HostKitchenNewCabinetB
    }

    public enum SplatSwitchMode
    {
        SwapAssetOnTargetRenderer,
        ToggleRendererObjects
    }

    [Header("Singleton")]
    public bool useSingleton = true;
    public bool disableDuplicateManagers = true;
    public bool dontDestroyOnLoad = false;

    [Header("Main Splat Reference")]
    public GaussianSplatRenderer MainSplat;
    public bool keepMainSplatSyncedToActiveRenderer = true;

    [Header("Feedback Managers")]
    public ParticlesManager particlesManager;
    public AudioManager audioManager;
    public bool autoFindFeedbackManagers = true;
    public bool playParticleTransitionOnRoomChange = true;
    public bool playRoomParticleOnRoomChange = true;
    public bool playAudioTransitionOnRoomChange = true;
    public bool playRoomMusicOnRoomChange = true;
    public bool playTransitionOnFirstRoomApply = false;
    public bool triggerFeedbackWhenSettingSameRoom = false;

    [Tooltip("If true, lighting/cabinet Host variants reuse HostRoom particles/music instead of needing separate feedback cases.")]
    public bool useHostRoomFeedbackForHostVariants = true;

    [Header("Switch Mode")]
    public SplatSwitchMode switchMode = SplatSwitchMode.SwapAssetOnTargetRenderer;

    [Header("Start Room")]
    public bool applyRoomOnStart = true;
    public SplatRoom startRoom = SplatRoom.CalmRoom;

    [Header("MODE 1 — Single Renderer Asset Swapping")]
    public GaussianSplatRenderer targetRenderer;

    [Header("Base Room Assets")]
    public GaussianSplatAsset calmRoomAsset;
    public GaussianSplatAsset fastRoomAsset;
    public GaussianSplatAsset hostRoomAsset;

    [Header("Host / Gather Kitchen Variant Assets")]
    [Tooltip("Asset name example: Host and Gather Kitchen_lighting_cold")]
    public GaussianSplatAsset hostKitchenLightingColdAsset;

    [Tooltip("Asset name example: Host and Gather Kitchen_lighting_warm")]
    public GaussianSplatAsset hostKitchenLightingWarmAsset;

    [Tooltip("Asset name example: Host and Gather Kitchen_new_Cabinet_A")]
    public GaussianSplatAsset hostKitchenNewCabinetAAsset;

    [Tooltip("Asset name example: Host and Gather Kitchen_new_Cabinet_B")]
    public GaussianSplatAsset hostKitchenNewCabinetBAsset;

    [Header("MODE 2 — Toggle Existing Renderers")]
    [Header("Base Room Renderers")]
    public GaussianSplatRenderer calmRoomRenderer;
    public GaussianSplatRenderer fastRoomRenderer;
    public GaussianSplatRenderer hostRoomRenderer;

    [Header("Host / Gather Kitchen Variant Renderers")]
    public GaussianSplatRenderer hostKitchenLightingColdRenderer;
    public GaussianSplatRenderer hostKitchenLightingWarmRenderer;
    public GaussianSplatRenderer hostKitchenNewCabinetARenderer;
    public GaussianSplatRenderer hostKitchenNewCabinetBRenderer;

    public bool disableInactiveGameObjects = true;

    [Header("Optional Transform Sync")]
    public bool forceActiveSplatToManagerTransform = false;
    public bool forceTargetRendererToManagerTransform = false;

    [Header("Cutout Sphere Transition")]
    public bool useCutoutSphereTransition = true;

    [Tooltip("The visible blue circle/sphere surface effect.")]
    public XRCCSphereSurfaceIntersectionEffect circleEffect;

    [Tooltip("Old world renderer. Its GaussianCutout should have Invert enabled.")]
    public GaussianSplatRenderer oldWorldTransitionRenderer;

    [Tooltip("New world renderer. Its GaussianCutout should have Invert disabled.")]
    public GaussianSplatRenderer newWorldTransitionRenderer;

    [Tooltip("The Effect/GaussianCutout child under SplatRendererEffect1.")]
    public Transform oldWorldCutoutTransform;

    [Tooltip("The Effect/GaussianCutout child under SplatRendererEffect2.")]
    public Transform newWorldCutoutTransform;

    public bool autoFindCutoutTransforms = true;

    [Header("Transition Origin")]
    [Tooltip("If true, the transition origin is taken from the camera/current viewer position.")]
    public bool useCameraOriginForTransition = true;

    [Tooltip("Recommended: assign CenterEyeAnchor or the main camera transform here.")]
    public Transform cameraOriginReference;

    [Tooltip("Optional camera reference. Used if Camera Origin Reference is empty.")]
    public Camera cameraReference;

    [Tooltip("If true and no camera reference is assigned, Camera.main is used.")]
    public bool autoUseMainCamera = true;

    [Tooltip("If true, the camera position is captured once when the transition starts, so the effect does not follow the head while animating.")]
    public bool lockOriginWhenTransitionStarts = true;

    [Tooltip("Moves the CircleEffect GameObject so its sphere center matches the transition origin.")]
    public bool moveCircleEffectToTransitionOrigin = true;

    [Tooltip("Moves both GaussianCutout Effect objects so their seam is centered at the same origin as the CircleEffect.")]
    public bool moveGaussianCutoutEffectsToTransitionOrigin = true;

    [Tooltip("Optional explicit center. Used only if camera origin is disabled or missing.")]
    public Transform transitionCenter;

    public bool copySourceSplatTransformToTransitionRenderers = true;
    public bool disableTransitionRenderersWhenIdle = true;
    public bool hideMainSplatDuringTransition = true;

    [Header("Cutout / Circle Alignment")]
    [Tooltip("This should stay true. It makes the GaussianCutout seam use the same world center as the CircleEffect.")]
    public bool forceCutoutCenterToCircleEffect = true;

    [Tooltip("Small manual world-space offset if the seam still needs tiny adjustment.")]
    public Vector3 cutoutCenterWorldOffset = Vector3.zero;

    [Tooltip("This should stay true. It makes the GaussianCutout seam scale from the same radius as the visible CircleEffect.")]
    public bool useCircleRadiusForCutoutScale = true;

    [Tooltip("Multiplier from visible circle radius to GaussianCutout local scale.")]
    public float gaussianCutoutRadiusScaleMultiplier = 1.25f;

    [Tooltip("Extra axis shaping. Keep this at 1,1,1 unless you need an ellipsoid correction.")]
    public Vector3 gaussianCutoutAxisMultiplier = Vector3.one;

    [Tooltip("Used only if Use Circle Radius For Cutout Scale is false.")]
    public float gaussianCutoutAnimationScaleMultiplier = 1.25f;

    [Tooltip("If true and Use Circle Radius For Cutout Scale is false, the cutout keeps its initial ellipsoid shape.")]
    public bool preserveInitialCutoutShape = true;

    [Tooltip("Used only if Preserve Initial Cutout Shape is false and Use Circle Radius For Cutout Scale is false.")]
    public float uniformCutoutScaleMultiplier = 2f;

    [Header("Cutout Transition Animation")]
    [Min(0.01f)] public float cutoutTransitionDuration = 1.1f;
    [Min(0f)] public float transitionStartRadius = 0f;
    [Min(0.01f)] public float transitionEndRadius = 6f;
    public AnimationCurve transitionRadiusCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Initial State")]
    [Tooltip("If true, CircleEffect is forced to radius 0 and hidden when this manager enables/starts.")]
    public bool resetCircleEffectToZeroOnEnable = true;

    [Tooltip("If true, transition renderers are disabled when idle.")]
    public bool disableTransitionEffectsOnEnable = true;

    [Header("Inspector Trigger / Editor Preview")]
    public bool triggerCutoutTransitionFromInspector = false;
    public SplatRoom inspectorTransitionTargetRoom = SplatRoom.FastRoom;

    public bool previewCutoutTransitionInEditor = false;

    [Range(0f, 1f)]
    public float editorPreviewTransitionT = 0.5f;

    public SplatRoom editorPreviewOldRoom = SplatRoom.CalmRoom;
    public SplatRoom editorPreviewNewRoom = SplatRoom.FastRoom;

    [Header("Debug Alignment")]
    public bool drawCutoutAlignmentGizmos = true;
    public Color cutoutCenterGizmoColor = Color.magenta;

    [SerializeField] private Vector3 lockedTransitionOriginWorld;
    [SerializeField] private bool hasLockedTransitionOrigin;
    [SerializeField] private Vector3 lastTransitionCenterWorld;
    [SerializeField] private Vector3 lastOldCutoutWorldPosition;
    [SerializeField] private Vector3 lastNewCutoutWorldPosition;
    [SerializeField] private float lastTransitionRadius;

    [Header("Debug")]
    public bool debugLogs = true;

    [Header("Events")]
    public SplatRoomEvent onRoomChanged = new SplatRoomEvent();

    [Serializable]
    public class SplatRoomEvent : UnityEvent<SplatRoom> { }

    public SplatRoom CurrentRoom { get; private set; } = SplatRoom.None;

    public GaussianSplatRenderer CurrentRenderer
    {
        get
        {
            if (MainSplat != null)
                return MainSplat;

            if (switchMode == SplatSwitchMode.SwapAssetOnTargetRenderer)
                return targetRenderer;

            return GetRenderer(CurrentRoom);
        }
    }

    private Coroutine activeTransitionRoutine;
    private bool isTransitioning;

    private Vector3 oldCutoutInitialScale = Vector3.one;
    private Vector3 newCutoutInitialScale = Vector3.one;
    private bool hasCapturedInitialCutoutScales;

    public static SplatManager GetOrFindInstance()
    {
        if (Instance != null)
            return Instance;

#if UNITY_2023_1_OR_NEWER
        Instance = FindFirstObjectByType<SplatManager>(FindObjectsInactive.Include);
#else
        Instance = FindObjectOfType<SplatManager>(true);
#endif

        return Instance;
    }

    private void RegisterSingleton()
    {
        if (!useSingleton)
            return;

        if (Instance == null || Instance == this)
        {
            Instance = this;

            if (dontDestroyOnLoad && Application.isPlaying)
                DontDestroyOnLoad(gameObject);

            return;
        }

        if (Instance != this)
        {
            if (debugLogs)
            {
                Debug.LogWarning(
                    $"<b>[SplatManager]</b> Duplicate SplatManager found on {name}. Existing instance is {Instance.name}.",
                    this
                );
            }

            if (disableDuplicateManagers && Application.isPlaying)
                gameObject.SetActive(false);
        }
    }

    private void UnregisterSingleton()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Reset()
    {
        TryAutoAssignReferences();
        TryAutoAssignTransitionReferences();
        CaptureInitialCutoutScales(true);
        ResetTransitionVisualsToIdle();
    }

    private void Awake()
    {
        RegisterSingleton();
        TryAutoAssignReferences();
        TryAutoAssignTransitionReferences();
        CaptureInitialCutoutScales(false);

        if (resetCircleEffectToZeroOnEnable)
            ResetTransitionVisualsToIdle();
    }

    private void OnEnable()
    {
        RegisterSingleton();
        TryAutoAssignReferences();
        TryAutoAssignTransitionReferences();
        CaptureInitialCutoutScales(false);

        if (resetCircleEffectToZeroOnEnable)
            ResetTransitionVisualsToIdle();

        if (disableTransitionEffectsOnEnable && disableTransitionRenderersWhenIdle && !previewCutoutTransitionInEditor)
            SetTransitionRenderersVisible(false);
    }

    private void OnDestroy()
    {
        UnregisterSingleton();
    }

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        ResetTransitionVisualsToIdle();

        if (applyRoomOnStart)
            SetRoom(startRoom);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        cutoutTransitionDuration = Mathf.Max(0.01f, cutoutTransitionDuration);
        transitionStartRadius = Mathf.Max(0f, transitionStartRadius);
        transitionEndRadius = Mathf.Max(0.01f, transitionEndRadius);

        gaussianCutoutRadiusScaleMultiplier = Mathf.Max(0.0001f, gaussianCutoutRadiusScaleMultiplier);
        gaussianCutoutAnimationScaleMultiplier = Mathf.Max(0.0001f, gaussianCutoutAnimationScaleMultiplier);
        uniformCutoutScaleMultiplier = Mathf.Max(0.0001f, uniformCutoutScaleMultiplier);

        gaussianCutoutAxisMultiplier.x = Mathf.Max(0.0001f, gaussianCutoutAxisMultiplier.x);
        gaussianCutoutAxisMultiplier.y = Mathf.Max(0.0001f, gaussianCutoutAxisMultiplier.y);
        gaussianCutoutAxisMultiplier.z = Mathf.Max(0.0001f, gaussianCutoutAxisMultiplier.z);

        TryAutoAssignReferences();
        TryAutoAssignTransitionReferences();
        CaptureInitialCutoutScales(false);

        if (forceTargetRendererToManagerTransform && targetRenderer != null)
        {
            CopyManagerTransformTo(targetRenderer.transform);
            EditorUtility.SetDirty(targetRenderer.transform);
        }

        if (triggerCutoutTransitionFromInspector)
        {
            triggerCutoutTransitionFromInspector = false;

            if (Application.isPlaying)
            {
                SetRoom(inspectorTransitionTargetRoom);
            }
            else
            {
                previewCutoutTransitionInEditor = true;

                if (CurrentRoom != SplatRoom.None)
                    editorPreviewOldRoom = CurrentRoom;
                else
                    editorPreviewOldRoom = startRoom;

                editorPreviewNewRoom = inspectorTransitionTargetRoom;
                editorPreviewTransitionT = 0f;

                CaptureTransitionOriginNow();
                PreviewCutoutTransitionInEditor();
            }
        }

        if (!Application.isPlaying)
        {
            if (previewCutoutTransitionInEditor)
            {
                if (!hasLockedTransitionOrigin)
                    CaptureTransitionOriginNow();

                PreviewCutoutTransitionInEditor();
            }
            else
            {
                hasLockedTransitionOrigin = false;

                if (resetCircleEffectToZeroOnEnable)
                    ResetTransitionVisualsToIdle();

                if (disableTransitionRenderersWhenIdle)
                    SetTransitionRenderersVisible(false);
            }
        }

        EditorUtility.SetDirty(this);
        SceneView.RepaintAll();
    }
#endif

    public void SetCalmRoom()
    {
        SetRoom(SplatRoom.CalmRoom);
    }

    public void SetFastRoom()
    {
        SetRoom(SplatRoom.FastRoom);
    }

    public void SetHostRoom()
    {
        SetRoom(SplatRoom.HostRoom);
    }

    public void SetHostKitchenLightingCold()
    {
        SetRoom(SplatRoom.HostKitchenLightingCold);
    }

    public void SetHostKitchenLightingWarm()
    {
        SetRoom(SplatRoom.HostKitchenLightingWarm);
    }

    public void SetHostKitchenNewCabinetA()
    {
        SetRoom(SplatRoom.HostKitchenNewCabinetA);
    }

    public void SetHostKitchenNewCabinetB()
    {
        SetRoom(SplatRoom.HostKitchenNewCabinetB);
    }

    public void HideAllRooms()
    {
        if (activeTransitionRoutine != null)
        {
            StopCoroutine(activeTransitionRoutine);
            activeTransitionRoutine = null;
        }

        isTransitioning = false;
        hasLockedTransitionOrigin = false;

        ResetTransitionVisualsToIdle();
        SetTransitionRenderersVisible(false);

        if (switchMode == SplatSwitchMode.ToggleRendererObjects)
        {
            SetAllRoomRenderersActive(false);
        }
        else if (targetRenderer != null)
        {
            targetRenderer.enabled = false;
        }

        CurrentRoom = SplatRoom.None;

        if (keepMainSplatSyncedToActiveRenderer)
            MainSplat = null;

        if (particlesManager != null)
            particlesManager.StopAllIntentParticles();

        if (audioManager != null)
            audioManager.StopAtmosphereMusic();

        onRoomChanged?.Invoke(CurrentRoom);

        if (debugLogs)
            Debug.Log("<b>[SplatManager]</b> Hid all splat rooms.", this);
    }

    public void SetRoom(SplatRoom room)
    {
        if (room == SplatRoom.None)
        {
            HideAllRooms();
            return;
        }

        SplatRoom previousRoom = CurrentRoom;
        bool isSameRoom = previousRoom == room;
        bool isFirstRoomApply = previousRoom == SplatRoom.None;

        if (isSameRoom && !triggerFeedbackWhenSettingSameRoom)
        {
            if (debugLogs)
                Debug.Log($"<b>[SplatManager]</b> {room} is already active. Skipping switch.", this);

            return;
        }

        bool shouldTriggerFeedback = !isSameRoom || triggerFeedbackWhenSettingSameRoom;
        bool shouldPlayTransitionFeedback = shouldTriggerFeedback && (!isFirstRoomApply || playTransitionOnFirstRoomApply);

        if (Application.isPlaying &&
            useCutoutSphereTransition &&
            !isFirstRoomApply &&
            CanUseCutoutTransition(previousRoom, room))
        {
            if (activeTransitionRoutine != null)
                StopCoroutine(activeTransitionRoutine);

            activeTransitionRoutine = StartCoroutine(
                CutoutTransitionRoutine(previousRoom, room, shouldPlayTransitionFeedback, shouldTriggerFeedback)
            );

            return;
        }

        bool switchedSuccessfully = ApplyRoomImmediate(room);

        if (!switchedSuccessfully)
            return;

        ResetTransitionVisualsToIdle();
        TriggerFeedbackForRoom(room, shouldPlayTransitionFeedback, shouldTriggerFeedback);
    }

    private IEnumerator CutoutTransitionRoutine(
        SplatRoom previousRoom,
        SplatRoom nextRoom,
        bool playTransitionFeedback,
        bool playRoomFeedback
    )
    {
        isTransitioning = true;

        TryAutoAssignReferences();
        TryAutoAssignTransitionReferences();
        CaptureInitialCutoutScales(false);
        CaptureTransitionOriginNow();

        GaussianSplatRenderer sourceRenderer = GetActiveRendererForRoom(previousRoom);
        GaussianSplatAsset oldAsset = GetAssetFromRendererOrRoom(sourceRenderer, previousRoom);
        GaussianSplatAsset newAsset = GetAssetForRoom(nextRoom);

        if (oldAsset == null || newAsset == null)
        {
            Debug.LogWarning("[SplatManager] Cutout transition could not find old/new splat assets. Falling back to immediate switch.", this);

            ApplyRoomImmediate(nextRoom);
            ResetTransitionVisualsToIdle();
            TriggerFeedbackForRoom(nextRoom, playTransitionFeedback, playRoomFeedback);

            isTransitioning = false;
            activeTransitionRoutine = null;
            yield break;
        }

        ConfigureTransitionRenderers(sourceRenderer, oldAsset, newAsset);

        if (hideMainSplatDuringTransition)
            HideMainRoomRenderersForTransition();

        ApplyTransitionVisual(0f, 0f);
        TriggerFeedbackForRoom(nextRoom, playTransitionFeedback, playRoomFeedback);

        float elapsed = 0f;

        while (elapsed < cutoutTransitionDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / cutoutTransitionDuration);
            float curvedT = transitionRadiusCurve != null ? transitionRadiusCurve.Evaluate(t) : t;
            float radius = Mathf.Lerp(transitionStartRadius, transitionEndRadius, curvedT);

            ApplyTransitionVisual(radius, curvedT);

            yield return null;
        }

        ApplyTransitionVisual(transitionEndRadius, 1f);

        bool switched = ApplyRoomImmediate(nextRoom);

        if (!switched)
            Debug.LogWarning($"[SplatManager] Transition finished, but final switch to {nextRoom} failed.", this);

        ResetTransitionVisualsToIdle();

        if (disableTransitionRenderersWhenIdle)
            SetTransitionRenderersVisible(false);

        ResetCutoutScalesToInitial();

        hasLockedTransitionOrigin = false;
        isTransitioning = false;
        activeTransitionRoutine = null;
    }

    private void ResetTransitionVisualsToIdle()
    {
        Vector3 origin = ResolveLiveTransitionOriginWorld();

        if (moveCircleEffectToTransitionOrigin && circleEffect != null)
            MoveCircleEffectCenterToWorld(origin);

        if (circleEffect != null)
            circleEffect.SetExternalRadius(0f, false);

        lastTransitionRadius = 0f;
        lastTransitionCenterWorld = origin;
    }

    private void CaptureTransitionOriginNow()
    {
        Vector3 origin = ResolveLiveTransitionOriginWorld();

        lockedTransitionOriginWorld = origin;
        hasLockedTransitionOrigin = true;

        if (moveCircleEffectToTransitionOrigin && circleEffect != null)
            MoveCircleEffectCenterToWorld(origin);

        lastTransitionCenterWorld = origin;
    }

    private Vector3 ResolveLiveTransitionOriginWorld()
    {
        if (useCameraOriginForTransition)
        {
            if (cameraOriginReference != null)
                return cameraOriginReference.position;

            if (cameraReference != null)
                return cameraReference.transform.position;

            if (autoUseMainCamera && Camera.main != null)
                return Camera.main.transform.position;
        }

        if (transitionCenter != null)
            return transitionCenter.position;

        if (circleEffect != null)
            return circleEffect.SphereCenterWorld;

        return transform.position;
    }

    private bool CanUseCutoutTransition(SplatRoom previousRoom, SplatRoom nextRoom)
    {
        if (circleEffect == null)
            return false;

        if (oldWorldTransitionRenderer == null || newWorldTransitionRenderer == null)
            return false;

        if (previousRoom == SplatRoom.None || nextRoom == SplatRoom.None)
            return false;

        GaussianSplatRenderer sourceRenderer = GetActiveRendererForRoom(previousRoom);
        GaussianSplatAsset oldAsset = GetAssetFromRendererOrRoom(sourceRenderer, previousRoom);
        GaussianSplatAsset newAsset = GetAssetForRoom(nextRoom);

        return oldAsset != null && newAsset != null;
    }

    private bool ApplyRoomImmediate(SplatRoom room)
    {
        bool switchedSuccessfully = false;

        switch (switchMode)
        {
            case SplatSwitchMode.SwapAssetOnTargetRenderer:
                switchedSuccessfully = SwapAsset(room);
                break;

            case SplatSwitchMode.ToggleRendererObjects:
                switchedSuccessfully = ToggleRenderer(room);
                break;

            default:
                Debug.LogError($"[SplatManager] Unknown switch mode: {switchMode}", this);
                break;
        }

        return switchedSuccessfully;
    }

    private bool SwapAsset(SplatRoom room)
    {
        if (targetRenderer == null)
        {
            Debug.LogError("[SplatManager] Cannot swap splat. targetRenderer is missing.", this);
            return false;
        }

        GaussianSplatAsset targetAsset = GetAsset(room);

        if (targetAsset == null)
        {
            Debug.LogError($"[SplatManager] Cannot switch to {room}. The matching GaussianSplatAsset is missing.", this);
            return false;
        }

        if (forceTargetRendererToManagerTransform)
            CopyManagerTransformTo(targetRenderer.transform);

        targetRenderer.gameObject.SetActive(true);
        targetRenderer.enabled = true;

        targetRenderer.m_Asset = targetAsset;
        targetRenderer.UpdateRessources();

        CurrentRoom = room;

        if (keepMainSplatSyncedToActiveRenderer || MainSplat == null)
            MainSplat = targetRenderer;

        onRoomChanged?.Invoke(CurrentRoom);

        if (debugLogs)
            Debug.Log($"<color=lime><b>[SplatManager]</b></color> Swapped target renderer to {room}: {targetAsset.name}", this);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(targetRenderer);
            EditorUtility.SetDirty(this);
        }
#endif

        return true;
    }

    private bool ToggleRenderer(SplatRoom room)
    {
        GaussianSplatRenderer activeRenderer = GetRenderer(room);

        if (activeRenderer == null)
        {
            Debug.LogError($"[SplatManager] Cannot switch to {room}. The matching GaussianSplatRenderer is missing.", this);
            return false;
        }

        SetAllRoomRenderersActive(false);
        SetRendererActive(activeRenderer, true);

        if (forceActiveSplatToManagerTransform)
            CopyManagerTransformTo(activeRenderer.transform);

        if (activeRenderer.HasValidAsset)
            activeRenderer.UpdateRessources();

        CurrentRoom = room;

        if (keepMainSplatSyncedToActiveRenderer || MainSplat == null)
            MainSplat = activeRenderer;

        onRoomChanged?.Invoke(CurrentRoom);

        if (debugLogs)
            Debug.Log($"<color=lime><b>[SplatManager]</b></color> Activated renderer for {room}: {activeRenderer.name}", this);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(activeRenderer);
            EditorUtility.SetDirty(this);
        }
#endif

        return true;
    }

    private void TriggerFeedbackForRoom(SplatRoom room, bool playTransition, bool playRoomFeedback)
    {
        TryAutoAssignFeedbackManagers();

        SplatRoom feedbackRoom = GetFeedbackRoom(room);

        if (particlesManager != null)
        {
            if (playTransition && playParticleTransitionOnRoomChange)
                particlesManager.PlayRoomChangeParticles();

            if (playRoomFeedback && playRoomParticleOnRoomChange)
                particlesManager.PlayIntentParticles(feedbackRoom);
        }

        if (audioManager != null)
        {
            if (playTransition && playAudioTransitionOnRoomChange)
                audioManager.PlayRoomChangeSfx();

            if (playRoomFeedback && playRoomMusicOnRoomChange)
                audioManager.PlayAtmosphereForRoom(feedbackRoom);
        }
    }

    private SplatRoom GetFeedbackRoom(SplatRoom room)
    {
        if (!useHostRoomFeedbackForHostVariants)
            return room;

        if (IsHostVariantRoom(room))
            return SplatRoom.HostRoom;

        return room;
    }

    private bool IsHostVariantRoom(SplatRoom room)
    {
        return room == SplatRoom.HostKitchenLightingCold ||
               room == SplatRoom.HostKitchenLightingWarm ||
               room == SplatRoom.HostKitchenNewCabinetA ||
               room == SplatRoom.HostKitchenNewCabinetB;
    }

    private void PreviewCutoutTransitionInEditor()
    {
        if (Application.isPlaying)
            return;

        if (!previewCutoutTransitionInEditor)
            return;

        TryAutoAssignTransitionReferences();
        CaptureInitialCutoutScales(false);

        if (!hasLockedTransitionOrigin)
            CaptureTransitionOriginNow();

        GaussianSplatRenderer oldRenderer = GetActiveRendererForRoom(editorPreviewOldRoom);
        GaussianSplatAsset oldAsset = GetAssetFromRendererOrRoom(oldRenderer, editorPreviewOldRoom);
        GaussianSplatAsset newAsset = GetAssetForRoom(editorPreviewNewRoom);

        if (oldAsset != null && newAsset != null)
            ConfigureTransitionRenderers(oldRenderer, oldAsset, newAsset);

        float t = Mathf.Clamp01(editorPreviewTransitionT);
        float curvedT = transitionRadiusCurve != null ? transitionRadiusCurve.Evaluate(t) : t;
        float radius = Mathf.Lerp(transitionStartRadius, transitionEndRadius, curvedT);

        ApplyTransitionVisual(radius, curvedT);
    }

    private void ConfigureTransitionRenderers(
        GaussianSplatRenderer sourceRenderer,
        GaussianSplatAsset oldAsset,
        GaussianSplatAsset newAsset
    )
    {
        if (oldWorldTransitionRenderer == null || newWorldTransitionRenderer == null)
            return;

        SetTransitionRenderersVisible(true);

        if (copySourceSplatTransformToTransitionRenderers && sourceRenderer != null)
        {
            CopyTransform(sourceRenderer.transform, oldWorldTransitionRenderer.transform);
            CopyTransform(sourceRenderer.transform, newWorldTransitionRenderer.transform);
        }

        SetRendererAsset(oldWorldTransitionRenderer, oldAsset);
        SetRendererAsset(newWorldTransitionRenderer, newAsset);

        TryAutoAssignTransitionReferences();
        CaptureInitialCutoutScales(false);
    }

    private void SetRendererAsset(GaussianSplatRenderer renderer, GaussianSplatAsset asset)
    {
        if (renderer == null || asset == null)
            return;

        renderer.gameObject.SetActive(true);
        renderer.enabled = true;
        renderer.m_Asset = asset;
        renderer.UpdateRessources();

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorUtility.SetDirty(renderer);
#endif
    }

    private void ApplyTransitionVisual(float radius, float normalizedT)
    {
        Vector3 center = GetTransitionCenterWorld() + cutoutCenterWorldOffset;

        if (moveCircleEffectToTransitionOrigin && circleEffect != null)
        {
            MoveCircleEffectCenterToWorld(center);
            center = circleEffect.SphereCenterWorld + cutoutCenterWorldOffset;
        }

        lastTransitionCenterWorld = center;
        lastTransitionRadius = radius;

        if (circleEffect != null)
            circleEffect.SetExternalRadius(radius, radius > 0.0001f);

        ApplyCutoutTransform(
            oldWorldCutoutTransform,
            oldWorldTransitionRenderer,
            oldCutoutInitialScale,
            center,
            radius,
            normalizedT,
            true
        );

        ApplyCutoutTransform(
            newWorldCutoutTransform,
            newWorldTransitionRenderer,
            newCutoutInitialScale,
            center,
            radius,
            normalizedT,
            false
        );
    }

    private Vector3 GetTransitionCenterWorld()
    {
        if (lockOriginWhenTransitionStarts && hasLockedTransitionOrigin)
            return lockedTransitionOriginWorld;

        if (useCameraOriginForTransition)
            return ResolveLiveTransitionOriginWorld();

        if (forceCutoutCenterToCircleEffect && circleEffect != null)
            return circleEffect.SphereCenterWorld;

        if (transitionCenter != null)
            return transitionCenter.position;

        if (circleEffect != null)
            return circleEffect.SphereCenterWorld;

        return transform.position;
    }

    private void MoveCircleEffectCenterToWorld(Vector3 desiredCenterWorld)
    {
        if (circleEffect == null)
            return;

        Transform circleTransform = circleEffect.transform;
        Vector3 worldOffset = circleTransform.TransformVector(circleEffect.sphereCenterLocalOffset);

        circleTransform.position = desiredCenterWorld - worldOffset;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorUtility.SetDirty(circleTransform);
#endif
    }

    private void ApplyCutoutTransform(
        Transform cutout,
        GaussianSplatRenderer owningRenderer,
        Vector3 initialScale,
        Vector3 centerWorld,
        float radius,
        float normalizedT,
        bool isOldCutout
    )
    {
        if (cutout == null)
            return;

        Quaternion targetRotation;

        if (transitionCenter != null && !useCameraOriginForTransition)
            targetRotation = transitionCenter.rotation;
        else if (circleEffect != null)
            targetRotation = circleEffect.transform.rotation;
        else
            targetRotation = transform.rotation;

        Transform parent = cutout.parent;

        if (moveGaussianCutoutEffectsToTransitionOrigin)
        {
            if (parent != null)
            {
                cutout.localPosition = parent.InverseTransformPoint(centerWorld);
                cutout.localRotation = Quaternion.Inverse(parent.rotation) * targetRotation;
            }
            else
            {
                cutout.position = centerWorld;
                cutout.rotation = targetRotation;
            }
        }

        cutout.localScale = CalculateCutoutLocalScale(initialScale, radius, normalizedT);

        if (isOldCutout)
            lastOldCutoutWorldPosition = cutout.position;
        else
            lastNewCutoutWorldPosition = cutout.position;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorUtility.SetDirty(cutout);
#endif
    }

    private Vector3 CalculateCutoutLocalScale(Vector3 initialScale, float radius, float normalizedT)
    {
        if (useCircleRadiusForCutoutScale)
        {
            float scaleFromRadius = Mathf.Max(0.0001f, radius * gaussianCutoutRadiusScaleMultiplier);

            Vector3 shape = NormalizeShape(initialScale);
            shape = Vector3.Scale(shape, gaussianCutoutAxisMultiplier);

            return shape * scaleFromRadius;
        }

        if (preserveInitialCutoutShape)
        {
            float scaleT = Mathf.Clamp01(normalizedT);
            float safeScaleT = Mathf.Max(0.0001f, scaleT);

            return initialScale * safeScaleT * gaussianCutoutAnimationScaleMultiplier;
        }

        float s = Mathf.Max(
            0.0001f,
            radius * uniformCutoutScaleMultiplier * gaussianCutoutAnimationScaleMultiplier
        );

        return Vector3.one * s;
    }

    private Vector3 NormalizeShape(Vector3 scale)
    {
        float max = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

        if (max < 0.0001f)
            return Vector3.one;

        return new Vector3(
            Mathf.Abs(scale.x) / max,
            Mathf.Abs(scale.y) / max,
            Mathf.Abs(scale.z) / max
        );
    }

    private void ResetCutoutScalesToInitial()
    {
        if (oldWorldCutoutTransform != null)
            oldWorldCutoutTransform.localScale = oldCutoutInitialScale;

        if (newWorldCutoutTransform != null)
            newWorldCutoutTransform.localScale = newCutoutInitialScale;
    }

    [ContextMenu("XRCC / Recapture Initial Cutout Scales")]
    public void RecaptureInitialCutoutScales()
    {
        CaptureInitialCutoutScales(true);
    }

    private void CaptureInitialCutoutScales(bool force)
    {
        if (hasCapturedInitialCutoutScales && !force)
            return;

        if (oldWorldCutoutTransform != null)
            oldCutoutInitialScale = oldWorldCutoutTransform.localScale;

        if (newWorldCutoutTransform != null)
            newCutoutInitialScale = newWorldCutoutTransform.localScale;

        hasCapturedInitialCutoutScales = true;
    }

    private void HideMainRoomRenderersForTransition()
    {
        if (switchMode == SplatSwitchMode.SwapAssetOnTargetRenderer)
        {
            if (targetRenderer != null)
                targetRenderer.enabled = false;

            return;
        }

        SetAllRoomRenderersActive(false);
    }

    private void SetTransitionRenderersVisible(bool visible)
    {
        SetTransitionRendererVisible(oldWorldTransitionRenderer, visible);
        SetTransitionRendererVisible(newWorldTransitionRenderer, visible);
    }

    private void SetTransitionRendererVisible(GaussianSplatRenderer renderer, bool visible)
    {
        if (renderer == null)
            return;

        renderer.gameObject.SetActive(visible);
        renderer.enabled = visible;
    }

    private GaussianSplatRenderer GetActiveRendererForRoom(SplatRoom room)
    {
        if (switchMode == SplatSwitchMode.SwapAssetOnTargetRenderer)
            return targetRenderer;

        return GetRenderer(room);
    }

    private GaussianSplatAsset GetAssetForRoom(SplatRoom room)
    {
        if (switchMode == SplatSwitchMode.SwapAssetOnTargetRenderer)
            return GetAsset(room);

        GaussianSplatRenderer renderer = GetRenderer(room);
        return renderer != null ? renderer.m_Asset : null;
    }

    private GaussianSplatAsset GetAssetFromRendererOrRoom(GaussianSplatRenderer renderer, SplatRoom room)
    {
        if (renderer != null && renderer.m_Asset != null)
            return renderer.m_Asset;

        return GetAssetForRoom(room);
    }

    private GaussianSplatAsset GetAsset(SplatRoom room)
    {
        switch (room)
        {
            case SplatRoom.CalmRoom:
                return calmRoomAsset;

            case SplatRoom.FastRoom:
                return fastRoomAsset;

            case SplatRoom.HostRoom:
                return hostRoomAsset;

            case SplatRoom.HostKitchenLightingCold:
                return hostKitchenLightingColdAsset;

            case SplatRoom.HostKitchenLightingWarm:
                return hostKitchenLightingWarmAsset;

            case SplatRoom.HostKitchenNewCabinetA:
                return hostKitchenNewCabinetAAsset;

            case SplatRoom.HostKitchenNewCabinetB:
                return hostKitchenNewCabinetBAsset;

            default:
                return null;
        }
    }

    private GaussianSplatRenderer GetRenderer(SplatRoom room)
    {
        switch (room)
        {
            case SplatRoom.CalmRoom:
                return calmRoomRenderer;

            case SplatRoom.FastRoom:
                return fastRoomRenderer;

            case SplatRoom.HostRoom:
                return hostRoomRenderer;

            case SplatRoom.HostKitchenLightingCold:
                return hostKitchenLightingColdRenderer;

            case SplatRoom.HostKitchenLightingWarm:
                return hostKitchenLightingWarmRenderer;

            case SplatRoom.HostKitchenNewCabinetA:
                return hostKitchenNewCabinetARenderer;

            case SplatRoom.HostKitchenNewCabinetB:
                return hostKitchenNewCabinetBRenderer;

            default:
                return null;
        }
    }

    private void SetAllRoomRenderersActive(bool active)
    {
        SetRendererActive(calmRoomRenderer, active);
        SetRendererActive(fastRoomRenderer, active);
        SetRendererActive(hostRoomRenderer, active);

        SetRendererActive(hostKitchenLightingColdRenderer, active);
        SetRendererActive(hostKitchenLightingWarmRenderer, active);
        SetRendererActive(hostKitchenNewCabinetARenderer, active);
        SetRendererActive(hostKitchenNewCabinetBRenderer, active);
    }

    private void SetRendererActive(GaussianSplatRenderer renderer, bool active)
    {
        if (renderer == null)
            return;

        if (disableInactiveGameObjects)
            renderer.gameObject.SetActive(active);
        else
            renderer.enabled = active;
    }

    private void CopyManagerTransformTo(Transform target)
    {
        if (target == null)
            return;

        target.position = transform.position;
        target.rotation = transform.rotation;
        target.localScale = transform.localScale;
    }

    private void CopyTransform(Transform source, Transform target)
    {
        if (source == null || target == null)
            return;

        target.position = source.position;
        target.rotation = source.rotation;
        target.localScale = source.localScale;
    }

    private void TryAutoAssignReferences()
    {
        TryAutoAssignMainSplat();
        TryAutoAssignTargetRenderer();
        TryAutoAssignFeedbackManagers();
    }

    private void TryAutoAssignFeedbackManagers()
    {
        if (!autoFindFeedbackManagers)
            return;

        if (particlesManager == null)
            particlesManager = ParticlesManager.GetOrFindInstance();

        if (audioManager == null)
            audioManager = AudioManager.GetOrFindInstance();
    }

    private void TryAutoAssignMainSplat()
    {
        if (MainSplat != null)
            return;

        if (targetRenderer != null)
        {
            MainSplat = targetRenderer;
            return;
        }

        GaussianSplatRenderer localRenderer = GetComponent<GaussianSplatRenderer>();

        if (localRenderer != null)
        {
            MainSplat = localRenderer;
            return;
        }

        GaussianSplatRenderer childRenderer = GetComponentInChildren<GaussianSplatRenderer>(true);

        if (childRenderer != null)
        {
            MainSplat = childRenderer;
            return;
        }

        GaussianSplatRenderer namedMainSplat = FindRendererNamed("MainSplat");

        if (namedMainSplat != null)
            MainSplat = namedMainSplat;
    }

    private void TryAutoAssignTargetRenderer()
    {
        if (targetRenderer != null)
            return;

        if (MainSplat != null)
        {
            targetRenderer = MainSplat;
            return;
        }

        targetRenderer = GetComponent<GaussianSplatRenderer>();

        if (targetRenderer != null)
            return;

        targetRenderer = GetComponentInChildren<GaussianSplatRenderer>(true);

        if (targetRenderer != null && MainSplat == null)
            MainSplat = targetRenderer;
    }

    private void TryAutoAssignTransitionReferences()
    {
        if (!autoFindCutoutTransforms)
            return;

        if (oldWorldCutoutTransform == null && oldWorldTransitionRenderer != null)
            oldWorldCutoutTransform = FindTransitionCutoutTransform(oldWorldTransitionRenderer.transform);

        if (newWorldCutoutTransform == null && newWorldTransitionRenderer != null)
            newWorldCutoutTransform = FindTransitionCutoutTransform(newWorldTransitionRenderer.transform);
    }

    private Transform FindTransitionCutoutTransform(Transform root)
    {
        if (root == null)
            return null;

        Transform namedEffect = FindChildRecursive(root, "Effect");

        if (namedEffect != null)
            return namedEffect;

        Component[] allComponents = root.GetComponentsInChildren<Component>(true);

        foreach (Component component in allComponents)
        {
            if (component == null)
                continue;

            string typeName = component.GetType().Name;

            if (typeName.Contains("GaussianCutout"))
                return component.transform;
        }

        return null;
    }

    private Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), childName);

            if (found != null)
                return found;
        }

        return null;
    }

    private GaussianSplatRenderer FindRendererNamed(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        GaussianSplatRenderer[] renderers = Resources.FindObjectsOfTypeAll<GaussianSplatRenderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            GaussianSplatRenderer renderer = renderers[i];

            if (renderer == null)
                continue;

            if (!renderer.gameObject.scene.IsValid())
                continue;

            if (renderer.gameObject.name == objectName)
                return renderer;
        }

        return null;
    }

    private void OnDrawGizmos()
    {
        if (!drawCutoutAlignmentGizmos)
            return;

        Gizmos.color = cutoutCenterGizmoColor;
        Gizmos.DrawWireSphere(lastTransitionCenterWorld, 0.08f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(lastOldCutoutWorldPosition, 0.055f);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(lastNewCutoutWorldPosition, 0.055f);

        Gizmos.color = new Color(cutoutCenterGizmoColor.r, cutoutCenterGizmoColor.g, cutoutCenterGizmoColor.b, 0.35f);
        Gizmos.DrawWireSphere(lastTransitionCenterWorld, lastTransitionRadius);
    }
}