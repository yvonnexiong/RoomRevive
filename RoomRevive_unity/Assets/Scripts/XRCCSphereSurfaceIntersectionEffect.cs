using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class XRCCSphereSurfaceIntersectionEffect : MonoBehaviour
{
    private const string ShaderName = "XRCC/Yinan Visual Cube Sphere Intersection URP";
    private const string OverlayPrefix = "__XRCC_YinanSphereIntersection_Overlay_";

#if UNITY_EDITOR
    private const string ShaderFolder = "Assets/XRCC/GeneratedShaders";
    private const string ShaderPath = ShaderFolder + "/XRCC_YinanVisualCubeSphereIntersectionURP.shader";
    private static bool isWritingShader;
#endif

    [Header("Yinan Kitchen Reference")]
    public Transform yinanKitchenRoot;
    public string visualCubeObjectName = "Visual_Cube";
    public bool includeInactiveVisualCubes = true;

    [Tooltip("Disables the original Visual_Cube MeshRenderers, but keeps the generated effect overlays enabled.")]
    public bool hideOriginalVisualCubeRenderers = true;

    [Header("OnValidate")]
    public bool initializeInOnValidate = true;
    public bool rebuildOverlaysOnValidate = true;

    [Header("External Transition Control")]
    [Tooltip("Used by SplatManager during splat transitions.")]
    public bool useExternalRadiusOverride = false;

    [Min(0f)]
    public float externalRadius = 0f;

    public bool externalEffectVisible = false;

    [Tooltip("Turn this on in the inspector to preview the effect in edit mode.")]
    public bool triggerPreviewFromInspector = false;

    [Range(0f, 1f)]
    public float editorPreviewT = 0.5f;

    [Min(0.01f)]
    public float editorPreviewMaxRadius = 6f;

    [Header("Sphere")]
    public bool useTransformScaleAsRadius = true;

    [Min(0.01f)]
    public float baseRadius = 0.5f;

    [Min(0.01f)]
    public float manualRadius = 1.5f;

    public Vector3 sphereCenterLocalOffset = Vector3.zero;

    [Header("Optional Radius Animation")]
    public bool animateRadiusInEditor = false;
    public bool animateRadiusInPlayMode = false;

    [Min(0.01f)] public float minAnimatedRadius = 0.2f;
    [Min(0.01f)] public float maxAnimatedRadius = 5f;
    [Range(0.01f, 10f)] public float animationSpeed = 1.2f;

    [Header("Intersection Look")]
    [ColorUsage(true, true)]
    public Color effectColor = new Color(0.15f, 0.9f, 1f, 1f);

    [Range(0.001f, 2f)]
    public float bandThickness = 0.18f;

    [Range(0.001f, 2f)]
    public float edgeSoftness = 0.08f;

    [Range(0f, 20f)]
    public float intensity = 6f;

    [Header("Visual Detail")]
    [Range(0f, 30f)] public float noiseScale = 8f;
    [Range(0f, 1f)] public float noiseStrength = 0.22f;
    [Range(0f, 40f)] public float scanlineScale = 18f;
    [Range(0f, 2f)] public float scanlineStrength = 0.35f;
    [Range(0.1f, 8f)] public float fresnelPower = 1.5f;
    [Range(0f, 5f)] public float fresnelStrength = 1.2f;

    [Header("Shader Asset")]
    public bool autoWriteShaderAsset = true;
    public bool forceRewriteShaderAsset = false;

    [Header("Debug")]
    public bool drawSphereGizmo = true;
    public Color gizmoColor = new Color(0.15f, 0.9f, 1f, 0.85f);

    [Header("Runtime Info")]
    [SerializeField] private float currentRadius;
    [SerializeField] private int foundVisualCubeCount;
    [SerializeField] private int overlayCount;
    [SerializeField] private int disabledOverlayCount;
    [SerializeField] private string shaderStatus;

    private Material effectMaterial;

    private readonly List<OverlayEntry> overlays = new List<OverlayEntry>();

#if UNITY_EDITOR
    private bool editorUpdateRegistered;
#endif

    public Vector3 SphereCenterWorld => GetSphereCenterWorld();
    public float CurrentRadius => currentRadius;

    private class OverlayEntry
    {
        public MeshRenderer sourceRenderer;
        public MeshFilter sourceMeshFilter;
        public GameObject overlayObject;
        public MeshRenderer overlayRenderer;
        public MeshFilter overlayMeshFilter;
    }

    private void Reset()
    {
        Initialize(true);
    }

    private void Awake()
    {
        Initialize(false);
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        RegisterEditorUpdate();
#endif

        Initialize(false);
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnregisterEditorUpdate();
#endif
    }

    private void Update()
    {
        bool shouldAnimate =
            Application.isPlaying && animateRadiusInPlayMode ||
            !Application.isPlaying && animateRadiusInEditor;

        UpdateEffectMaterial();

        if (shouldAnimate || useExternalRadiusOverride)
            SyncOverlays();
    }

#if UNITY_EDITOR
    private void RegisterEditorUpdate()
    {
        if (editorUpdateRegistered)
            return;

        EditorApplication.update += EditorTick;
        editorUpdateRegistered = true;
    }

    private void UnregisterEditorUpdate()
    {
        if (!editorUpdateRegistered)
            return;

        EditorApplication.update -= EditorTick;
        editorUpdateRegistered = false;
    }

    private void EditorTick()
    {
        if (Application.isPlaying)
            return;

        if (!animateRadiusInEditor && !useExternalRadiusOverride)
            return;

        UpdateEffectMaterial();
        SyncOverlays();
        SceneView.RepaintAll();
    }
#endif

    private void OnValidate()
    {
        if (!initializeInOnValidate)
            return;

        baseRadius = Mathf.Max(0.01f, baseRadius);
        manualRadius = Mathf.Max(0.01f, manualRadius);
        minAnimatedRadius = Mathf.Max(0.01f, minAnimatedRadius);
        maxAnimatedRadius = Mathf.Max(minAnimatedRadius + 0.01f, maxAnimatedRadius);

        bandThickness = Mathf.Max(0.001f, bandThickness);
        edgeSoftness = Mathf.Max(0.001f, edgeSoftness);
        editorPreviewMaxRadius = Mathf.Max(0.01f, editorPreviewMaxRadius);

        if (triggerPreviewFromInspector)
        {
            triggerPreviewFromInspector = false;
            useExternalRadiusOverride = true;
            externalEffectVisible = true;
            externalRadius = Mathf.Lerp(0f, editorPreviewMaxRadius, editorPreviewT);
        }

        Initialize(rebuildOverlaysOnValidate);
        UpdateEffectMaterial();
        SyncOverlays();

#if UNITY_EDITOR
        SceneView.RepaintAll();
#endif
    }

    public void SetExternalRadius(float radius, bool visible)
    {
        useExternalRadiusOverride = true;
        externalRadius = Mathf.Max(0f, radius);
        externalEffectVisible = visible;

        UpdateEffectMaterial();
        SyncOverlays();
    }

    public void ClearExternalRadiusOverride()
    {
        useExternalRadiusOverride = false;
        externalEffectVisible = true;

        UpdateEffectMaterial();
        SyncOverlays();
    }

    [ContextMenu("XRCC / Rebuild Visual Cube Overlays")]
    public void RebuildVisualCubeOverlays()
    {
        Initialize(true);
        UpdateEffectMaterial();
        SyncOverlays();
    }

    [ContextMenu("XRCC / Disable Generated Visual Cube Overlays")]
    public void DisableGeneratedVisualCubeOverlays()
    {
        DisableExistingGeneratedOverlaysUnderYinanKitchen();
        overlays.Clear();
        overlayCount = 0;
        foundVisualCubeCount = 0;
    }

    [ContextMenu("XRCC / Show Original Visual Cubes")]
    public void ShowOriginalVisualCubes()
    {
        SetOriginalVisualCubeRenderersVisible(true);
    }

    [ContextMenu("XRCC / Hide Original Visual Cubes")]
    public void HideOriginalVisualCubes()
    {
        SetOriginalVisualCubeRenderersVisible(false);
    }

    private void Initialize(bool rebuild)
    {
#if UNITY_EDITOR
        if (autoWriteShaderAsset)
            WriteShaderAssetIfNeeded(forceRewriteShaderAsset);
#endif

        CreateMaterial();

        if (rebuild)
        {
            DisableExistingGeneratedOverlaysUnderYinanKitchen();
            BuildOverlaysForAllVisualCubes();
        }

        if (overlays.Count == 0)
            BuildOverlaysForAllVisualCubes();

        UpdateEffectMaterial();
        SyncOverlays();
    }

    private void CreateMaterial()
    {
        Shader shader = Shader.Find(ShaderName);

        shaderStatus = shader != null
            ? "Shader found: " + ShaderName
            : "Shader missing. It should generate at Assets/XRCC/GeneratedShaders/";

        if (shader == null)
            return;

        if (effectMaterial == null || effectMaterial.shader != shader)
        {
            effectMaterial = new Material(shader);
            effectMaterial.name = "__XRCC_YinanVisualCubeSphereIntersection_Material";
            effectMaterial.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            effectMaterial.renderQueue = 3100;
        }
    }

    private void BuildOverlaysForAllVisualCubes()
    {
        overlays.Clear();
        foundVisualCubeCount = 0;

        if (yinanKitchenRoot == null)
        {
            overlayCount = 0;
            return;
        }

        MeshRenderer[] renderers = yinanKitchenRoot.GetComponentsInChildren<MeshRenderer>(includeInactiveVisualCubes);

        foreach (MeshRenderer renderer in renderers)
        {
            if (!IsValidVisualCubeRenderer(renderer))
                continue;

            foundVisualCubeCount++;
            CreateOrEnableOverlayFor(renderer);
        }

        overlayCount = overlays.Count;
    }

    private bool IsValidVisualCubeRenderer(MeshRenderer renderer)
    {
        if (renderer == null)
            return false;

        GameObject go = renderer.gameObject;

        if (go == null)
            return false;

        if (go.name != visualCubeObjectName)
            return false;

        if (go.name.StartsWith(OverlayPrefix))
            return false;

        if (go.transform.IsChildOf(transform))
            return false;

        MeshFilter meshFilter = go.GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
            return false;

        return true;
    }

    private void CreateOrEnableOverlayFor(MeshRenderer sourceRenderer)
    {
        if (sourceRenderer == null || effectMaterial == null)
            return;

        MeshFilter sourceMeshFilter = sourceRenderer.GetComponent<MeshFilter>();

        if (sourceMeshFilter == null || sourceMeshFilter.sharedMesh == null)
            return;

        Transform existingOverlay = sourceRenderer.transform.Find(OverlayPrefix + sourceRenderer.gameObject.name);

        GameObject overlayObject;

        if (existingOverlay != null)
        {
            overlayObject = existingOverlay.gameObject;
            overlayObject.SetActive(true);
        }
        else
        {
            overlayObject = new GameObject(OverlayPrefix + sourceRenderer.gameObject.name);
            overlayObject.hideFlags = HideFlags.DontSaveInBuild;
            overlayObject.layer = sourceRenderer.gameObject.layer;

            overlayObject.transform.SetParent(sourceRenderer.transform, false);
            overlayObject.transform.localPosition = Vector3.zero;
            overlayObject.transform.localRotation = Quaternion.identity;
            overlayObject.transform.localScale = Vector3.one;
        }

        MeshFilter overlayMeshFilter = overlayObject.GetComponent<MeshFilter>();
        if (overlayMeshFilter == null)
            overlayMeshFilter = overlayObject.AddComponent<MeshFilter>();

        MeshRenderer overlayRenderer = overlayObject.GetComponent<MeshRenderer>();
        if (overlayRenderer == null)
            overlayRenderer = overlayObject.AddComponent<MeshRenderer>();

        overlayMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;

        overlayRenderer.sharedMaterial = effectMaterial;
        overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        overlayRenderer.receiveShadows = false;
        overlayRenderer.allowOcclusionWhenDynamic = false;
        overlayRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        overlayRenderer.enabled = true;

        if (hideOriginalVisualCubeRenderers)
            sourceRenderer.enabled = false;

        overlays.Add(new OverlayEntry
        {
            sourceRenderer = sourceRenderer,
            sourceMeshFilter = sourceMeshFilter,
            overlayObject = overlayObject,
            overlayRenderer = overlayRenderer,
            overlayMeshFilter = overlayMeshFilter
        });
    }

    private void SyncOverlays()
    {
        disabledOverlayCount = 0;

        for (int i = overlays.Count - 1; i >= 0; i--)
        {
            OverlayEntry entry = overlays[i];

            if (entry == null || entry.sourceRenderer == null || entry.overlayObject == null)
            {
                overlays.RemoveAt(i);
                disabledOverlayCount++;
                continue;
            }

            entry.overlayObject.SetActive(true);

            if (entry.sourceMeshFilter != null && entry.overlayMeshFilter != null)
                entry.overlayMeshFilter.sharedMesh = entry.sourceMeshFilter.sharedMesh;

            if (entry.overlayRenderer != null)
            {
                entry.overlayRenderer.sharedMaterial = effectMaterial;
                entry.overlayRenderer.enabled = true;
            }

            if (hideOriginalVisualCubeRenderers)
                entry.sourceRenderer.enabled = false;
        }

        overlayCount = overlays.Count;
    }

    private void DisableExistingGeneratedOverlaysUnderYinanKitchen()
    {
        disabledOverlayCount = 0;

        if (yinanKitchenRoot == null)
            return;

        Transform[] allChildren = yinanKitchenRoot.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in allChildren)
        {
            if (child == null)
                continue;

            if (!child.name.StartsWith(OverlayPrefix))
                continue;

            child.gameObject.SetActive(false);
            disabledOverlayCount++;
        }

        overlays.Clear();
    }

    private void SetOriginalVisualCubeRenderersVisible(bool visible)
    {
        if (yinanKitchenRoot == null)
            return;

        MeshRenderer[] renderers = yinanKitchenRoot.GetComponentsInChildren<MeshRenderer>(true);

        foreach (MeshRenderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (renderer.gameObject.name != visualCubeObjectName)
                continue;

            renderer.enabled = visible;
        }
    }

    private void UpdateEffectMaterial()
    {
        currentRadius = GetCurrentRadius();
        Vector3 center = GetSphereCenterWorld();

        if (effectMaterial == null)
            return;

        float visible = externalEffectVisible ? 1f : 0f;

        if (!useExternalRadiusOverride)
            visible = 1f;

        effectMaterial.SetVector("_SphereCenterWS", center);
        effectMaterial.SetFloat("_SphereRadiusWS", currentRadius);
        effectMaterial.SetFloat("_EffectVisible", visible);

        effectMaterial.SetColor("_EffectColor", effectColor);
        effectMaterial.SetFloat("_BandThickness", bandThickness);
        effectMaterial.SetFloat("_EdgeSoftness", edgeSoftness);
        effectMaterial.SetFloat("_Intensity", intensity);

        effectMaterial.SetFloat("_NoiseScale", noiseScale);
        effectMaterial.SetFloat("_NoiseStrength", noiseStrength);
        effectMaterial.SetFloat("_ScanlineScale", scanlineScale);
        effectMaterial.SetFloat("_ScanlineStrength", scanlineStrength);

        effectMaterial.SetFloat("_FresnelPower", fresnelPower);
        effectMaterial.SetFloat("_FresnelStrength", fresnelStrength);

        effectMaterial.SetFloat("_CustomTime", (float)GetTime());
    }

    private Vector3 GetSphereCenterWorld()
    {
        return transform.TransformPoint(sphereCenterLocalOffset);
    }

    private float GetCurrentRadius()
    {
        if (useExternalRadiusOverride)
            return Mathf.Max(0f, externalRadius);

        bool shouldAnimate =
            Application.isPlaying && animateRadiusInPlayMode ||
            !Application.isPlaying && animateRadiusInEditor;

        if (shouldAnimate)
        {
            float t = (float)GetTime();
            float n = Mathf.Sin(t * animationSpeed) * 0.5f + 0.5f;
            n = Mathf.SmoothStep(0f, 1f, n);
            return Mathf.Lerp(minAnimatedRadius, maxAnimatedRadius, n);
        }

        if (!useTransformScaleAsRadius)
            return manualRadius;

        Vector3 s = transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        return Mathf.Max(0.01f, baseRadius * maxScale);
    }

    private double GetTime()
    {
        if (Application.isPlaying)
            return Time.timeAsDouble;

#if UNITY_EDITOR
        return EditorApplication.timeSinceStartup;
#else
        return Time.realtimeSinceStartupAsDouble;
#endif
    }

    private void OnDrawGizmos()
    {
        if (!drawSphereGizmo)
            return;

        Vector3 center = GetSphereCenterWorld();
        float radius = currentRadius > 0.001f ? currentRadius : GetCurrentRadius();

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(center, radius);

        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.25f);
        Gizmos.DrawSphere(center, 0.055f);
    }

#if UNITY_EDITOR
    private void WriteShaderAssetIfNeeded(bool force)
    {
        if (isWritingShader)
            return;

        if (!force && File.Exists(ShaderPath))
            return;

        isWritingShader = true;

        try
        {
            EnsureFolder("Assets", "XRCC");
            EnsureFolder("Assets/XRCC", "GeneratedShaders");

            File.WriteAllText(ShaderPath, ShaderSource);
            AssetDatabase.ImportAsset(ShaderPath);
            AssetDatabase.Refresh();

            shaderStatus = "Generated shader: " + ShaderPath;
            forceRewriteShaderAsset = false;
        }
        finally
        {
            isWritingShader = false;
        }
    }

    private void EnsureFolder(string parent, string folder)
    {
        string fullPath = parent + "/" + folder;

        if (!AssetDatabase.IsValidFolder(fullPath))
            AssetDatabase.CreateFolder(parent, folder);
    }
#endif

    private const string ShaderSource = @"
Shader ""XRCC/Yinan Visual Cube Sphere Intersection URP""
{
    Properties
    {
        _EffectColor (""Effect Color"", Color) = (0.15, 0.9, 1, 1)

        _SphereCenterWS (""Sphere Center WS"", Vector) = (0, 0, 0, 0)
        _SphereRadiusWS (""Sphere Radius WS"", Float) = 1
        _EffectVisible (""Effect Visible"", Float) = 1

        _BandThickness (""Band Thickness"", Float) = 0.18
        _EdgeSoftness (""Edge Softness"", Float) = 0.08
        _Intensity (""Intensity"", Float) = 6

        _NoiseScale (""Noise Scale"", Float) = 8
        _NoiseStrength (""Noise Strength"", Range(0, 1)) = 0.22
        _ScanlineScale (""Scanline Scale"", Float) = 18
        _ScanlineStrength (""Scanline Strength"", Range(0, 2)) = 0.35

        _FresnelPower (""Fresnel Power"", Float) = 1.5
        _FresnelStrength (""Fresnel Strength"", Float) = 1.2

        _CustomTime (""Custom Time"", Float) = 0
    }

    SubShader
    {
        Tags
        {
            ""RenderPipeline"" = ""UniversalPipeline""
            ""Queue"" = ""Transparent+100""
            ""RenderType"" = ""Transparent""
            ""IgnoreProjector"" = ""True""
        }

        Pass
        {
            Name ""Yinan Visual Cube Sphere Intersection""

            Tags
            {
                ""LightMode"" = ""UniversalForward""
            }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""

            CBUFFER_START(UnityPerMaterial)
                float4 _EffectColor;

                float4 _SphereCenterWS;
                float _SphereRadiusWS;
                float _EffectVisible;

                float _BandThickness;
                float _EdgeSoftness;
                float _Intensity;

                float _NoiseScale;
                float _NoiseStrength;
                float _ScanlineScale;
                float _ScanlineStrength;

                float _FresnelPower;
                float _FresnelStrength;

                float _CustomTime;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);

                f = f * f * (3.0 - 2.0 * f);

                float n000 = Hash31(i + float3(0, 0, 0));
                float n100 = Hash31(i + float3(1, 0, 0));
                float n010 = Hash31(i + float3(0, 1, 0));
                float n110 = Hash31(i + float3(1, 1, 0));

                float n001 = Hash31(i + float3(0, 0, 1));
                float n101 = Hash31(i + float3(1, 0, 1));
                float n011 = Hash31(i + float3(0, 1, 1));
                float n111 = Hash31(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);

                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);

                return lerp(nxy0, nxy1, f.z);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS);

                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = normalize(normal.normalWS);
                output.uv = input.uv;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 center = _SphereCenterWS.xyz;

                float distanceToCenter = distance(input.positionWS, center);
                float shellDistance = abs(distanceToCenter - _SphereRadiusWS);

                float band = 1.0 - smoothstep(
                    _BandThickness,
                    _BandThickness + _EdgeSoftness,
                    shellDistance
                );

                band *= saturate(_EffectVisible);

                clip(band - 0.002);

                float noise = ValueNoise(input.positionWS * _NoiseScale + _CustomTime * 0.35);
                float noiseMask = lerp(1.0, noise, _NoiseStrength);

                float scan = sin(distanceToCenter * _ScanlineScale - _CustomTime * 5.0) * 0.5 + 0.5;
                scan = pow(scan, 5.0) * _ScanlineStrength;

                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float ndotv = abs(dot(normalize(input.normalWS), viewDir));
                float fresnel = pow(1.0 - saturate(ndotv), _FresnelPower) * _FresnelStrength;

                float alpha = band * noiseMask * _EffectColor.a * _Intensity;
                alpha = saturate(alpha);

                float energy = band * (1.0 + scan + fresnel);
                float3 color = _EffectColor.rgb * energy * _Intensity;
                color += float3(1.0, 1.0, 1.0) * band * 0.25;

                return half4(color, alpha);
            }

            ENDHLSL
        }
    }

    FallBack Off
}
";
}