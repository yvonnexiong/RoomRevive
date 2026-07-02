using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
    [Tooltip("Keep this false if the video should begin with no splats visible.")]
    public bool applyRoomOnStart = false;

    [Tooltip("Use None if the video should begin with no splats visible.")]
    public SplatRoom startRoom = SplatRoom.None;

    [Tooltip("Splat loaded immediately on Start before any intent or product selection. Replaces the removed per-room asset fields for the initial view.")]
    public GaussianSplatAsset defaultSplatAsset;

    [Header("Startup Hidden State")]
    [Tooltip("Forces all splat renderers off in Awake, before the first rendered frame.")]
    public bool forceHideAllSplatsOnAwake = true;

    [Tooltip("Forces all splat renderers off again in Start, unless Apply Room On Start is enabled.")]
    public bool forceHideAllSplatsOnStart = true;

    [Tooltip("Also disables the old/new transition splat renderers at startup.")]
    public bool hideTransitionRenderersOnStartup = true;

    [Tooltip("If true, CurrentRoom becomes None when the startup hide runs.")]
    public bool clearCurrentRoomOnStartupHide = true;

    [Header("MODE 1 — Single Renderer Asset Swapping")]
    [Tooltip("Target renderer for asset swapping. Callers (Intent UI, Cabinet UI) pass their own GaussianSplatAsset references to SetAsset().")]
    public GaussianSplatRenderer targetRenderer;

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

    [Tooltip("3rd renderer. When a transition finishes, the OLD transition renderer (SplatRenderer 1) " +
             "is set to this renderer's splat, so it's primed with that content for the next cycle.")]
    public GaussianSplatRenderer postTransitionSourceRenderer;

    [Tooltip("Its GaussianSplatRenderer COMPONENT is disabled while a live SPZ swap reveal plays, then " +
             "re-enabled (e.g. NewKitchenRenderer) so it doesn't double up over the transition. " +
             "Only the renderer component is toggled — the GameObject stays active.")]
    public GaussianSplatRenderer hideDuringSpzSwap;

    [Header("SPZ File Transition (reference only)")]
    [Tooltip("Reference note only — the script does NOT load this. Marks which .spz belongs on " +
             "'Old World Transition Renderer' (the previous splat). Populate it via a LiveSplatLoader.")]
    public string oldSplatSpzPath = "LiveSplat/kitchenLastSelected.spz";

    [Tooltip("Reference note only — the script does NOT load this. Marks which .spz belongs on " +
             "'New World Transition Renderer' (the current splat). Populate it via a LiveSplatLoader.")]
    public string newSplatSpzPath = "LiveSplat/kitchen-copy.spz";

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

    [Tooltip("GameObjects deactivated for the duration of the transition effect, then restored to " +
             "whatever active state they had when the transition started.")]
    public List<GameObject> objectsToDisableDuringTransition = new List<GameObject>();

    [Tooltip("GameObjects activated for the duration of the transition effect (e.g. a loading screen), " +
             "then restored to whatever active state they had when the transition started.")]
    public List<GameObject> objectsToEnableDuringTransition = new List<GameObject>();

    [Header("SPZ Snapshots (paths relative to project root, one folder above Assets/)")]
    [Tooltip("If true, copy the live .spz to the snapshot files below. Editor / Quest-Link dev workflow.")]
    public bool persistSpzSnapshots = true;
    [Tooltip("The live working file LiveSplatLoader watches and the HTML editor writes.")]
    public string liveSpzRelativePath = "LiveSplat/kitchen-copy.spz";
    [Tooltip("Snapshot taken each time a new splat is delivered (start of SwapToSpz).")]
    public string currentSelectedRelativePath = "LiveSplat/kitchenCurrentSelected.spz";

    [Tooltip("Optional renderer that displays the 'current selected' snapshot. The new splat is loaded " +
             "onto it (runtime data — its Asset field stays bypassed).")]
    public GaussianSplatRenderer currentSelectedRenderer;

    [Header("Last Selected — Baked Asset (Editor only)")]
    [Tooltip("After each transition the live splat is re-baked into a real GaussianSplatAsset here and " +
             "assigned to Last Selected Renderer's Data Asset. Editor-only (needs AssetDatabase); stripped from builds.")]
    public bool bakeLastSelectedAsset = true;
    [Tooltip("Output folder for the baked asset + its .bytes (must start with 'Assets/').")]
    public string lastSelectedAssetFolder = "Assets/WorldLabsWorlds/kitchenLastSelected";
    [Tooltip("Base name for the baked .asset and its side .bytes files.")]
    public string lastSelectedAssetBaseName = "kitchenLastSelected";
    [Tooltip("Optional renderer that uses the baked 'last selected' asset as its Data Asset (set after the transition finishes).")]
    public GaussianSplatRenderer lastSelectedRenderer;

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
    [Tooltip("Wait this long after a new live splat is loaded before the reveal starts (the old splat stays visible during the wait).")]
    [Min(0f)] public float preTransitionDelay = 0.5f;
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

    [Tooltip("Tick to replay the SPZ ping-pong reveal over whatever is currently on the two transition renderers " +
             "(does NOT load a new splat). In Play mode it animates; in edit mode it shows a static preview at editorPreviewTransitionT.")]
    public bool triggerSpzTransitionFromInspector = false;

    [Tooltip("If true, automatically plays the SPZ ping-pong reveal once on Start (Play mode). Waits one frame " +
             "so any LiveSplatLoader has populated the renderers first.")]
    public bool playSpzTransitionOnStart = false;

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

    /// <summary>World center of the current transition reveal sphere.</summary>
    public Vector3 LastTransitionCenterWorld => lastTransitionCenterWorld;
    /// <summary>World radius of the current transition reveal sphere.</summary>
    public float LastTransitionRadius => lastTransitionRadius;
    /// <summary>True while a reveal transition is animating.</summary>
    public bool IsTransitioning => isTransitioning;

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

    private bool hasAppliedStartupHide;

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
        applyRoomOnStart = false;
        startRoom = SplatRoom.None;

        TryAutoAssignReferences();
        TryAutoAssignTransitionReferences();
        CaptureInitialCutoutScales(true);
        ResetTransitionVisualsToIdle();

        ForceAllSplatsHiddenForStartup("Reset");
    }

    private void Awake()
    {
        RegisterSingleton();
        TryAutoAssignReferences();
        TryAutoAssignTransitionReferences();
        CaptureInitialCutoutScales(false);

        if (resetCircleEffectToZeroOnEnable)
            ResetTransitionVisualsToIdle();

        if (Application.isPlaying && forceHideAllSplatsOnAwake)
            ForceAllSplatsHiddenForStartup("Awake");
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

        if (Application.isPlaying && forceHideAllSplatsOnAwake && !hasAppliedStartupHide)
            ForceAllSplatsHiddenForStartup("OnEnable");
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

        if (defaultSplatAsset != null)
        {
            ApplyAssetImmediate(defaultSplatAsset, targetRenderer);
        }
        else
        {
            if (forceHideAllSplatsOnStart && !applyRoomOnStart)
                ForceAllSplatsHiddenForStartup("Start");

            if (applyRoomOnStart && startRoom != SplatRoom.None)
                SetRoom(startRoom);
        }

        if (playSpzTransitionOnStart)
            StartCoroutine(PlaySpzTransitionNextFrame());
    }

    // Waits one frame so LiveSplatLoaders (which load in their own Start) have populated the
    // transition renderers, then plays the reveal over their current content.
    private IEnumerator PlaySpzTransitionNextFrame()
    {
        yield return null;
        PlaySpzTransition();
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

        if (triggerSpzTransitionFromInspector)
        {
            triggerSpzTransitionFromInspector = false;

            if (Application.isPlaying)
            {
                // Animate the reveal over whatever the two renderers already hold.
                PlaySpzTransition();
            }
            else
            {
                // Coroutines don't animate in edit mode — show a static frame of the reveal instead.
                previewCutoutTransitionInEditor = true;
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

    [ContextMenu("XRCC / Hide All Rooms")]
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

    private void ForceAllSplatsHiddenForStartup(string reason)
    {
        if (activeTransitionRoutine != null)
        {
            StopCoroutine(activeTransitionRoutine);
            activeTransitionRoutine = null;
        }

        isTransitioning = false;
        hasLockedTransitionOrigin = false;

        ResetTransitionVisualsToIdle();

        if (hideTransitionRenderersOnStartup)
            SetTransitionRenderersVisible(false);

        DisableRendererComponentOnly(MainSplat);
        DisableRendererComponentOnly(targetRenderer);

        SetAllRoomRenderersActive(false);

        if (oldWorldTransitionRenderer != null)
            SetTransitionRendererVisible(oldWorldTransitionRenderer, false);

        if (newWorldTransitionRenderer != null)
            SetTransitionRendererVisible(newWorldTransitionRenderer, false);

        if (clearCurrentRoomOnStartupHide)
            CurrentRoom = SplatRoom.None;

        hasAppliedStartupHide = true;

        if (debugLogs)
            Debug.Log($"<b>[SplatManager]</b> Startup hide completed from {reason}. No splats should be visible at the beginning.", this);
    }

    private void DisableRendererComponentOnly(GaussianSplatRenderer renderer)
    {
        if (renderer == null)
            return;

        renderer.enabled = false;
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

    /// <summary>
    /// Directly swaps the asset on the target renderer without going through the SplatRoom enum.
    /// Used by ProductVariantRouter in GaussianSplat mode for data-driven cabinet swapping.
    /// Uses the cutout sphere transition when available in play mode.
    /// Pass an explicit <paramref name="renderer"/> to target a specific renderer (e.g. one on the Cabinet UI);
    /// pass null to fall back to <see cref="targetRenderer"/>.
    /// </summary>
    public void SetAsset(GaussianSplatAsset asset, GaussianSplatRenderer renderer = null)
    {
        GaussianSplatRenderer resolvedRenderer = renderer != null ? renderer : targetRenderer;

        if (resolvedRenderer == null)
        {
            Debug.LogWarning("[SplatManager] SetAsset called but no renderer is available.", this);
            return;
        }
        if (asset == null)
        {
            Debug.LogWarning("[SplatManager] SetAsset called with a null asset.", this);
            return;
        }

        bool canTransition = Application.isPlaying &&
                             useCutoutSphereTransition &&
                             circleEffect != null &&
                             oldWorldTransitionRenderer != null &&
                             newWorldTransitionRenderer != null &&
                             resolvedRenderer.m_Asset != null &&
                             resolvedRenderer.m_Asset != asset;

        if (canTransition)
        {
            if (activeTransitionRoutine != null)
                StopCoroutine(activeTransitionRoutine);

            activeTransitionRoutine = StartCoroutine(CutoutTransitionRoutineForAsset(asset, resolvedRenderer));
            return;
        }

        ApplyAssetImmediate(asset, resolvedRenderer);
    }

    private void ApplyAssetImmediate(GaussianSplatAsset asset, GaussianSplatRenderer renderer)
    {
        renderer.gameObject.SetActive(true);
        renderer.enabled = true;
        renderer.m_Asset = asset;
        renderer.UpdateRessources();

        if (keepMainSplatSyncedToActiveRenderer || MainSplat == null)
            MainSplat = renderer;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(this);
        }
#endif
    }

    private IEnumerator CutoutTransitionRoutineForAsset(GaussianSplatAsset newAsset, GaussianSplatRenderer renderer)
    {
        isTransitioning = true;

        TryAutoAssignReferences();
        TryAutoAssignTransitionReferences();
        CaptureInitialCutoutScales(false);
        CaptureTransitionOriginNow();

        GaussianSplatAsset oldAsset = renderer.m_Asset;

        if (oldAsset == null || newAsset == null)
        {
            Debug.LogWarning("[SplatManager] Asset transition could not find old/new splat assets. Falling back to immediate switch.", this);
            ApplyAssetImmediate(newAsset, renderer);
            ResetTransitionVisualsToIdle();
            isTransitioning = false;
            activeTransitionRoutine = null;
            yield break;
        }

        ConfigureTransitionRenderers(renderer);

        if (hideMainSplatDuringTransition)
            HideMainRoomRenderersForTransition();

        ApplyTransitionVisual(0f, 0f);

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

        ApplyAssetImmediate(newAsset, renderer);
        ResetTransitionVisualsToIdle();

        if (disableTransitionRenderersWhenIdle)
            SetTransitionRenderersVisible(false);

        ResetCutoutScalesToInitial();

        hasLockedTransitionOrigin = false;
        isTransitioning = false;
        activeTransitionRoutine = null;
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

        ConfigureTransitionRenderers(sourceRenderer);

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

        // SwapAssetOnTargetRenderer: transition is driven by SetAsset(); SetRoom() is state/feedback only.
        if (switchMode == SplatSwitchMode.SwapAssetOnTargetRenderer)
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
        // In SwapAssetOnTargetRenderer mode visuals are driven externally via SetAsset().
        // This only updates room-state tracking so feedback and events fire correctly.
        CurrentRoom = room;

        if (keepMainSplatSyncedToActiveRenderer || MainSplat == null)
            MainSplat = targetRenderer;

        onRoomChanged?.Invoke(CurrentRoom);

        if (debugLogs)
            Debug.Log($"<color=lime><b>[SplatManager]</b></color> Room state → {room}. Visual swap handled via SetAsset().", this);

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

        ConfigureTransitionRenderers(oldRenderer);

        float t = Mathf.Clamp01(editorPreviewTransitionT);
        float curvedT = transitionRadiusCurve != null ? transitionRadiusCurve.Evaluate(t) : t;
        float radius = Mathf.Lerp(transitionStartRadius, transitionEndRadius, curvedT);

        ApplyTransitionVisual(radius, curvedT);
    }

    // Prepares the two transition renderers for the reveal WITHOUT touching their splats — it only
    // makes them visible, matches their transform to the source, and refreshes the cutout refs.
    // Whatever splat each renderer already holds (e.g. from a LiveSplatLoader) is what gets revealed.
    private void ConfigureTransitionRenderers(GaussianSplatRenderer sourceRenderer)
    {
        if (oldWorldTransitionRenderer == null || newWorldTransitionRenderer == null)
            return;

        SetTransitionRenderersVisible(true);

        if (copySourceSplatTransformToTransitionRenderers && sourceRenderer != null)
        {
            CopyTransform(sourceRenderer.transform, oldWorldTransitionRenderer.transform);
            CopyTransform(sourceRenderer.transform, newWorldTransitionRenderer.transform);
        }

        TryAutoAssignTransitionReferences();
        CaptureInitialCutoutScales(false);
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

    // ── Splat transition test ─────────────────────────────────────────────
    // Plays the cutout-sphere reveal between WHATEVER is currently on the two transition renderers.
    // It never loads, applies, or changes the splat on either renderer — it only animates the cutout
    // and toggles visibility for the reveal. Populate the renderers yourself (e.g. a LiveSplatLoader
    // on each, pointed at the .spz paths above, which are kept only as a reference).

    [ContextMenu("SPZ Transition / Play Transition (Play mode)")]
    public void PlaySpzTransition()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[SplatManager] PlaySpzTransition only animates in Play mode.", this);
            return;
        }
        if (oldWorldTransitionRenderer == null || newWorldTransitionRenderer == null)
        {
            Debug.LogWarning("[SplatManager] Assign Old/New World Transition Renderer first.", this);
            return;
        }
        if (circleEffect == null)
            Debug.LogWarning("[SplatManager] No Circle Effect assigned — the sphere reveal won't be visible.", this);

        if (activeTransitionRoutine != null) StopCoroutine(activeTransitionRoutine);
        activeTransitionRoutine = StartCoroutine(SpzCutoutTransitionRoutine());
    }

    IEnumerator SpzCutoutTransitionRoutine()
    {
        isTransitioning = true;

        TryAutoAssignTransitionReferences();

        // We do NOT load/apply any splats here — the transition animates whatever is already on
        // oldWorldTransitionRenderer / newWorldTransitionRenderer. Populate them yourself first.

        SetTransitionRenderersVisible(true);
        CaptureInitialCutoutScales(false);
        CaptureTransitionOriginNow();

        if (hideMainSplatDuringTransition)
            HideMainRoomRenderersForTransition();
        ApplyTransitionObjectStates();

        ApplyTransitionVisual(0f, 0f);

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

        RestoreTransitionObjects();

        // Transition done: prime the OLD transition renderer (1) with the 3rd renderer's splat,
        // so it carries that content into the next cycle.
        SyncOldRendererFromPostTransitionSource();

        // The new splat is fully revealed. Keep the NEW renderer showing (it holds the current .spz),
        // reset its cutout to full visibility, and hide the OLD one. No ApplyAssetImmediate here:
        // the result already lives on newWorldTransitionRenderer, not on a main asset renderer.
        ResetCutoutScalesToInitial();
        ResetTransitionVisualsToIdle();
        SetTransitionRendererVisible(oldWorldTransitionRenderer, false);
        SetTransitionRendererVisible(newWorldTransitionRenderer, true);

        hasLockedTransitionOrigin = false;
        isTransitioning = false;
        activeTransitionRoutine = null;
    }

    // Copies the 3rd renderer's splat asset onto the OLD transition renderer (SplatRenderer 1),
    // called after a transition so renderer 1 matches that source for the next reveal.
    // NOTE: copies the asset reference (m_Asset). If the source is driven by a LiveSplatLoader
    // (runtime .spz, no asset), point a LiveSplatLoader at renderer 1 instead — m_Asset won't carry it.
    void SyncOldRendererFromPostTransitionSource()
    {
        if (oldWorldTransitionRenderer == null || postTransitionSourceRenderer == null)
            return;

        oldWorldTransitionRenderer.m_Asset = postTransitionSourceRenderer.m_Asset;
        oldWorldTransitionRenderer.UpdateRessources();
    }

    // ── Live SPZ swap: ping-pong reveal fed by LiveSplatLoader ─────────────
    // Renderer 1 = the persistent CURRENT splat (and the OLD layer during a reveal).
    // Renderer 2 = the incoming NEW splat. Per swap: new → load into 2 → reveal 2 over 1 → copy 2 → 1.
    bool _spzDisplaySeeded;

    /// <summary>
    /// Feed a freshly-loaded splat (from LiveSplatLoader / the HTML editor). The first call seeds
    /// renderer 1 directly (no transition); later calls load it into renderer 2, play the reveal over
    /// renderer 1, then copy renderer 2 → 1 so 1 holds the new current for the next swap.
    /// </summary>
    public void SwapToSpz(RuntimeSplatData newRsd)
    {
        if (oldWorldTransitionRenderer == null || newWorldTransitionRenderer == null)
        {
            Debug.LogWarning("[SplatManager] SwapToSpz: assign Old/New World Transition Renderer.", this);
            return;
        }

        if (debugLogs)
            Debug.Log($"<color=cyan><b>[SplatManager]</b></color> SwapToSpz called (playing={Application.isPlaying}, " +
                      $"seeded={_spzDisplaySeeded}) → {(!_spzDisplaySeeded || !Application.isPlaying ? "SEED (no reveal)" : "TRANSITION")}", this);

        // New data just returned from the live file → snapshot it as the current selection
        // AND load that content onto the current-selected renderer so it shows it.
        CopySpzSnapshot(liveSpzRelativePath, currentSelectedRelativePath, "current selected");
        if (currentSelectedRenderer != null)
            LoadRsdInto(currentSelectedRenderer, newRsd);

        // First splat (or edit mode, where coroutines don't animate): just show it, no reveal.
        if (!_spzDisplaySeeded || !Application.isPlaying)
        {
            LoadRsdInto(oldWorldTransitionRenderer, newRsd);                       // renderer 1 = current (old layer for the next swap)
            if (hideDuringSpzSwap != null) LoadRsdInto(hideDuringSpzSwap, newRsd); // standalone display shows it
            SetTransitionRendererVisible(oldWorldTransitionRenderer, hideDuringSpzSwap == null);
            SetTransitionRendererVisible(newWorldTransitionRenderer, false);
            _spzDisplaySeeded = true;
            return;
        }

        // New splat → load into renderer 2 (before the transition), then reveal + copy.
        LoadRsdInto(newWorldTransitionRenderer, newRsd);
        if (activeTransitionRoutine != null) StopCoroutine(activeTransitionRoutine);
        activeTransitionRoutine = StartCoroutine(SpzSwapRoutine(newRsd));
    }

    IEnumerator SpzSwapRoutine(RuntimeSplatData newRsd)
    {
        isTransitioning = true;
        TryAutoAssignTransitionReferences();

        // Show the loading screen / hide the panels for the WHOLE transition, including the pre-delay.
        ApplyTransitionObjectStates();

        // The new splat is already loaded on renderer 2; hold on the old splat briefly before revealing.
        if (preTransitionDelay > 0f)
            yield return new WaitForSeconds(preTransitionDelay);

        // Give the standalone display the NEW splat (copy of renderer 2) but keep it hidden for the
        // reveal, so when it's re-shown at the end it already holds the new — no flash.
        if (hideDuringSpzSwap != null)
        {
            LoadRsdInto(hideDuringSpzSwap, newRsd);
            hideDuringSpzSwap.enabled = false;
        }

        SetTransitionRenderersVisible(true);          // both visible for the reveal
        CaptureInitialCutoutScales(false);
        CaptureTransitionOriginNow();
        if (hideMainSplatDuringTransition) HideMainRoomRenderersForTransition();

        ApplyTransitionVisual(0f, 0f);
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

        // Reveal done — copy renderer 2 (new) into renderer 1 so 1 becomes the current for next time.
        LoadRsdInto(oldWorldTransitionRenderer, newRsd);

        RestoreTransitionObjects();
        ResetCutoutScalesToInitial();
        ResetTransitionVisualsToIdle();

        if (hideDuringSpzSwap != null)
        {
            // Standalone display takes over showing the new — hide both transition renderers.
            hideDuringSpzSwap.enabled = true;
            SetTransitionRendererVisible(oldWorldTransitionRenderer, false);
            SetTransitionRendererVisible(newWorldTransitionRenderer, false);
        }
        else
        {
            // No standalone display assigned — renderer 1 stays as the idle display.
            SetTransitionRendererVisible(oldWorldTransitionRenderer, true);
            SetTransitionRendererVisible(newWorldTransitionRenderer, false);
        }

        // Transition fully finished → bake the live splat into the persistent last-selected
        // GaussianSplatAsset and assign it to the last-selected renderer's Data Asset.
#if UNITY_EDITOR
        BakeLastSelectedAsset();
#endif

        hasLockedTransitionOrigin = false;
        isTransitioning = false;
        activeTransitionRoutine = null;
    }

    static void LoadRsdInto(GaussianSplatRenderer renderer, RuntimeSplatData rsd)
    {
        if (renderer == null) return;
        renderer.gameObject.SetActive(true);
        renderer.enabled = true;
        renderer.LoadFromRuntimeData(rsd);
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

    // Remembers which listed objects we toggled, so we restore only those to their prior state.
    private readonly List<GameObject> _disabledForTransition = new List<GameObject>();
    private readonly List<GameObject> _enabledForTransition = new List<GameObject>();

    // Turn OFF the disable-list and ON the enable-list (e.g. a loading screen) for the transition.
    private void ApplyTransitionObjectStates()
    {
        _disabledForTransition.Clear();
        if (objectsToDisableDuringTransition != null)
            foreach (GameObject go in objectsToDisableDuringTransition)
            {
                if (go == null || !go.activeSelf) continue;
                go.SetActive(false);
                _disabledForTransition.Add(go);
            }

        _enabledForTransition.Clear();
        if (objectsToEnableDuringTransition != null)
            foreach (GameObject go in objectsToEnableDuringTransition)
            {
                if (go == null || go.activeSelf) continue;
                go.SetActive(true);
                _enabledForTransition.Add(go);
            }
    }

    private void RestoreTransitionObjects()
    {
        foreach (GameObject go in _disabledForTransition)
            if (go != null) go.SetActive(true);
        _disabledForTransition.Clear();

        foreach (GameObject go in _enabledForTransition)
            if (go != null) go.SetActive(false);
        _enabledForTransition.Clear();
    }

    private static string ResolveProjectRelativePath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
    }

    // Copies the live .spz to a snapshot file. Best-effort: logs and continues on any failure
    // (e.g. in a standalone build where the LiveSplat folder doesn't exist).
    private void CopySpzSnapshot(string srcRelative, string dstRelative, string reason)
    {
        if (!persistSpzSnapshots) return;
        if (string.IsNullOrEmpty(srcRelative) || string.IsNullOrEmpty(dstRelative)) return;

        try
        {
            string src = ResolveProjectRelativePath(srcRelative);
            string dst = ResolveProjectRelativePath(dstRelative);

            if (!File.Exists(src))
            {
                if (debugLogs)
                    Debug.LogWarning($"[SplatManager] {reason}: source .spz not found: {src}", this);
                return;
            }

            if (string.Equals(src, dst, StringComparison.OrdinalIgnoreCase))
                return;

            File.Copy(src, dst, true);

            if (debugLogs)
                Debug.Log($"<color=lime><b>[SplatManager]</b></color> {reason}: {srcRelative} → {dstRelative}", this);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SplatManager] {reason}: snapshot copy failed — {e.Message}", this);
        }
    }

#if UNITY_EDITOR
    // Re-bakes the live .spz into a real GaussianSplatAsset (matching the existing Float32/lossless
    // formats) at lastSelectedAssetFolder, replacing the asset in place (same GUID, so any renderer
    // referencing it stays valid), then assigns it to lastSelectedRenderer's Data Asset.
    // Editor-only: uses the package's editor API via reflection (Assembly-CSharp can't reference it directly).
    private void BakeLastSelectedAsset()
    {
        if (!bakeLastSelectedAsset) return;

        try
        {
            string spzFull = ResolveProjectRelativePath(liveSpzRelativePath);
            if (!File.Exists(spzFull))
            {
                if (debugLogs)
                    Debug.LogWarning($"[SplatManager] last-selected bake: live .spz not found: {spzFull}", this);
                return;
            }

            Type api = FindTypeInLoadedAssemblies("GaussianSplatting.Editor.GaussianSplatAssetCreatorAPI");
            MethodInfo create = api?.GetMethod("CreateAsset", BindingFlags.Public | BindingFlags.Static);
            if (create == null)
            {
                Debug.LogWarning("[SplatManager] last-selected bake: GaussianSplatAssetCreatorAPI.CreateAsset not found.", this);
                return;
            }

            // Match the existing asset's formats (all 0 = lossless Float32).
            object baked = create.Invoke(null, new object[]
            {
                spzFull,
                lastSelectedAssetFolder,
                lastSelectedAssetBaseName,
                GaussianSplatAsset.VectorFormat.Float32,   // position
                GaussianSplatAsset.VectorFormat.Float32,   // scale
                GaussianSplatAsset.ColorFormat.Float32x4,  // color
                GaussianSplatAsset.SHFormat.Float32,       // spherical harmonics
                false                                       // importCameras
            });

            if (baked is GaussianSplatAsset asset && lastSelectedRenderer != null)
            {
                lastSelectedRenderer.gameObject.SetActive(true);
                lastSelectedRenderer.enabled = true;
                lastSelectedRenderer.m_Asset = asset;
                lastSelectedRenderer.UpdateRessources();
            }

            if (debugLogs)
                Debug.Log($"<color=lime><b>[SplatManager]</b></color> baked last-selected asset → " +
                          $"{lastSelectedAssetFolder}/{lastSelectedAssetBaseName}.asset", this);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SplatManager] last-selected bake failed — {e.Message}", this);
        }
    }

    private static Type FindTypeInLoadedAssemblies(string fullName)
    {
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = a.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }
#endif

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
        // SwapAssetOnTargetRenderer: assets are owned by callers; read the live asset from the renderer.
        if (switchMode == SplatSwitchMode.SwapAssetOnTargetRenderer)
            return targetRenderer != null ? targetRenderer.m_Asset : null;

        GaussianSplatRenderer renderer = GetRenderer(room);
        return renderer != null ? renderer.m_Asset : null;
    }

    private GaussianSplatAsset GetAssetFromRendererOrRoom(GaussianSplatRenderer renderer, SplatRoom room)
    {
        if (renderer != null && renderer.m_Asset != null)
            return renderer.m_Asset;

        return GetAssetForRoom(room);
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

        if (!active)
            renderer.enabled = false;
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