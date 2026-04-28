using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class XRCCCyaniluxForcefieldDemo : MonoBehaviour
{
    private const string RootName = "__XRCC_CyaniluxForcefield_DemoRoot";
    private const string SphereName = "__XRCC_ForcefieldSphere";
    private const string CubeName = "__XRCC_DepthTestCube";
    private const string LightName = "__XRCC_ForcefieldLight";

    private const string ShaderName = "XRCC/Cyanilux Style Forcefield URP";

#if UNITY_EDITOR
    private const string ShaderFolder = "Assets/XRCC/GeneratedShaders";
    private const string ShaderPath = ShaderFolder + "/XRCC_CyaniluxStyleForcefieldURP.shader";
    private static bool isWritingShader;
#endif

    private const int MaxRipples = 6;
    private const int RippleFloatCount = MaxRipples * 4;

    [Header("One Click Setup")]
    public bool initializeInOnValidate = true;
    public bool autoWriteShaderAsset = true;
    public bool forceRewriteShaderAsset = false;
    public bool rebuildGeneratedObjects = false;

    [Header("URP Camera Setup")]
    public bool tryEnableDepthTexture = true;
    public bool tryEnableOpaqueTexture = true;

    [Header("Generated Demo Objects")]
    public bool createTestCube = true;
    public bool createForcefieldSphere = true;
    public bool createPointLight = true;

    [Header("Animation")]
    public bool animateInPlayMode = true;
    public bool animateInEditMode = true;
    public bool autoAnimateRadius = true;

    [Range(0.05f, 10f)] public float minRadius = 0.2f;
    [Range(0.05f, 10f)] public float maxRadius = 3.1f;
    [Range(0.01f, 10f)] public float radiusAnimationSpeed = 1.15f;

    [Header("Manual Radius")]
    [Tooltip("Used when Auto Animate Radius is disabled.")]
    [Range(0.05f, 10f)] public float manualRadius = 1.5f;

    public Vector3 sphereCenterLocalOffset = Vector3.zero;

    [Header("Test Cube")]
    public Vector3 cubeLocalPosition = new Vector3(1.05f, 0f, 0f);
    public Vector3 cubeLocalRotation = new Vector3(0f, 15f, 0f);
    public Vector3 cubeSize = new Vector3(2f, 2f, 2f);
    public Color cubeColor = new Color(0.13f, 0.15f, 0.18f, 1f);

    [Header("Forcefield Color")]
    [ColorUsage(true, true)]
    public Color forcefieldColor = new Color(0.15f, 0.85f, 1f, 1f);

    [Header("Fresnel Edge")]
    [Range(0f, 1f)] public float surfaceAlpha = 0.025f;
    [Range(0.1f, 16f)] public float fresnelPower = 6f;
    [Range(0f, 10f)] public float fresnelIntensity = 1.5f;

    [Header("Intersection")]
    [Tooltip("How far from the sphere shell an object surface can be before the glow disappears.")]
    [Range(0.001f, 2f)] public float intersectionDistance = 0.18f;

    [Tooltip("Soft fade around the intersection band.")]
    [Range(0.001f, 2f)] public float intersectionSoftness = 0.16f;

    [Range(0f, 15f)] public float intersectionIntensity = 6f;

    [Header("Scan / Noise Detail")]
    [Range(0f, 30f)] public float noiseScale = 9f;
    [Range(0f, 1f)] public float noiseStrength = 0.22f;
    [Range(0f, 30f)] public float scanlineScale = 14f;
    [Range(0f, 2f)] public float scanlineStrength = 0.45f;

    [Header("Ripples")]
    public bool autoSpawnRipples = true;
    [Range(0.05f, 5f)] public float rippleSpawnInterval = 0.45f;
    [Range(0.05f, 5f)] public float rippleLifetime = 1.2f;
    [Range(0.01f, 2f)] public float rippleWidth = 0.22f;
    [Range(0f, 5f)] public float rippleWorldRadius = 1.4f;
    [Range(0f, 10f)] public float rippleIntensity = 3f;

    [Header("Runtime Info")]
    [SerializeField] private float currentRadius;
    [SerializeField] private string shaderStatus;
    [SerializeField] private GameObject generatedRoot;
    [SerializeField] private GameObject forcefieldSphere;
    [SerializeField] private GameObject testCube;
    [SerializeField] private Light forcefieldLight;

    private Material forcefieldMaterial;
    private Material cubeMaterial;

    private readonly float[] ripplePoints = new float[RippleFloatCount];
    private int nextRippleIndex;
    private double lastTime;
    private float rippleTimer;

#if UNITY_EDITOR
    private bool editorUpdateRegistered;
#endif

    private void Reset()
    {
        ResetRipples();
        lastTime = GetTime();
        Initialize(true);
    }

    private void Awake()
    {
        ResetRipples();
        lastTime = GetTime();
        Initialize(false);
    }

    private void OnEnable()
    {
        ResetRipples();
        lastTime = GetTime();

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
        if (!Application.isPlaying)
            return;

        if (!animateInPlayMode)
            return;

        Tick();
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

        if (!animateInEditMode)
            return;

        Tick();
        SceneView.RepaintAll();
    }
#endif

    private void OnValidate()
    {
        if (!initializeInOnValidate)
            return;

        minRadius = Mathf.Max(0.05f, minRadius);
        maxRadius = Mathf.Max(minRadius + 0.01f, maxRadius);
        manualRadius = Mathf.Max(0.05f, manualRadius);

        intersectionDistance = Mathf.Max(0.001f, intersectionDistance);
        intersectionSoftness = Mathf.Max(0.001f, intersectionSoftness);

        cubeSize.x = Mathf.Max(0.01f, cubeSize.x);
        cubeSize.y = Mathf.Max(0.01f, cubeSize.y);
        cubeSize.z = Mathf.Max(0.01f, cubeSize.z);

        bool shouldRebuild = rebuildGeneratedObjects;
        rebuildGeneratedObjects = false;

        Initialize(shouldRebuild);
        UpdateForcefield();

#if UNITY_EDITOR
        SceneView.RepaintAll();
#endif
    }

    [ContextMenu("XRCC / Rebuild Full Demo")]
    public void RebuildFullDemo()
    {
        Initialize(true);
        UpdateForcefield();
    }

    [ContextMenu("XRCC / Add Ripple Now")]
    public void AddRippleNow()
    {
        AddRipple(UnityEngine.Random.onUnitSphere);
    }

    [ContextMenu("XRCC / Delete Generated Demo")]
    public void DeleteGeneratedDemo()
    {
        Transform existingRoot = transform.Find(RootName);

        if (existingRoot != null)
            DestroySmart(existingRoot.gameObject);

        generatedRoot = null;
        forcefieldSphere = null;
        testCube = null;
        forcefieldLight = null;
    }

    private void Tick()
    {
        Initialize(false);

        double now = GetTime();
        float deltaTime = Mathf.Max(0f, (float)(now - lastTime));
        lastTime = now;

        UpdateRipples(deltaTime);
        UpdateForcefield();
    }

    private void Initialize(bool forceRebuild)
    {
#if UNITY_EDITOR
        if (autoWriteShaderAsset)
            WriteShaderAssetIfNeeded(forceRewriteShaderAsset);
#endif

        if (tryEnableDepthTexture || tryEnableOpaqueTexture)
            TryEnableCameraTextures();

        CreateMaterials();

        if (forceRebuild)
            DeleteGeneratedDemo();

        CreateGeneratedRoot();

        if (createTestCube)
            CreateOrUpdateCube();
        else
            DeleteChild(CubeName);

        if (createForcefieldSphere)
            CreateOrUpdateSphere();
        else
            DeleteChild(SphereName);

        if (createPointLight)
            CreateOrUpdateLight();
        else
            DeleteChild(LightName);

        UpdateForcefield();
    }

    private void CreateGeneratedRoot()
    {
        if (generatedRoot != null)
            return;

        Transform existing = transform.Find(RootName);

        if (existing != null)
        {
            generatedRoot = existing.gameObject;
            return;
        }

        generatedRoot = new GameObject(RootName);
        generatedRoot.hideFlags = HideFlags.DontSaveInBuild;
        generatedRoot.transform.SetParent(transform, false);
        generatedRoot.transform.localPosition = Vector3.zero;
        generatedRoot.transform.localRotation = Quaternion.identity;
        generatedRoot.transform.localScale = Vector3.one;
    }

    private void CreateMaterials()
    {
        Shader forcefieldShader = Shader.Find(ShaderName);

        shaderStatus = forcefieldShader != null
            ? "Shader found: " + ShaderName
            : "Shader missing. It should be generated at Assets/XRCC/GeneratedShaders/";

        if (forcefieldShader != null)
        {
            if (forcefieldMaterial == null || forcefieldMaterial.shader != forcefieldShader)
            {
                forcefieldMaterial = new Material(forcefieldShader);
                forcefieldMaterial.name = "__XRCC_CyaniluxForcefield_Material";
                forcefieldMaterial.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
                forcefieldMaterial.renderQueue = 3100;
            }
        }

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null)
            litShader = Shader.Find("Standard");

        if (cubeMaterial == null)
        {
            cubeMaterial = new Material(litShader);
            cubeMaterial.name = "__XRCC_DepthTestCube_Material";
            cubeMaterial.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
        }

        SetMaterialColor(cubeMaterial, cubeColor);
    }

    private void CreateOrUpdateCube()
    {
        if (generatedRoot == null)
            return;

        if (testCube == null)
        {
            Transform existing = generatedRoot.transform.Find(CubeName);

            if (existing != null)
                testCube = existing.gameObject;
        }

        if (testCube == null)
        {
            testCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testCube.name = CubeName;
            testCube.hideFlags = HideFlags.DontSaveInBuild;
            testCube.transform.SetParent(generatedRoot.transform, false);
        }

        testCube.transform.localPosition = cubeLocalPosition;
        testCube.transform.localRotation = Quaternion.Euler(cubeLocalRotation);
        testCube.transform.localScale = cubeSize;

        MeshRenderer renderer = testCube.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = testCube.AddComponent<MeshRenderer>();

        MeshFilter filter = testCube.GetComponent<MeshFilter>();
        if (filter == null)
            filter = testCube.AddComponent<MeshFilter>();

        BoxCollider boxCollider = testCube.GetComponent<BoxCollider>();
        if (boxCollider == null)
            boxCollider = testCube.AddComponent<BoxCollider>();

        boxCollider.center = Vector3.zero;
        boxCollider.size = Vector3.one;

        renderer.sharedMaterial = cubeMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = true;
    }

    private void CreateOrUpdateSphere()
    {
        if (generatedRoot == null)
            return;

        if (forcefieldSphere == null)
        {
            Transform existing = generatedRoot.transform.Find(SphereName);

            if (existing != null)
                forcefieldSphere = existing.gameObject;
        }

        if (forcefieldSphere == null)
        {
            forcefieldSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            forcefieldSphere.name = SphereName;
            forcefieldSphere.hideFlags = HideFlags.DontSaveInBuild;
            forcefieldSphere.transform.SetParent(generatedRoot.transform, false);
        }

        SphereCollider sphereCollider = forcefieldSphere.GetComponent<SphereCollider>();
        if (sphereCollider != null)
        {
            sphereCollider.isTrigger = true;
        }

        MeshRenderer renderer = forcefieldSphere.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = forcefieldSphere.AddComponent<MeshRenderer>();

        renderer.sharedMaterial = forcefieldMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.allowOcclusionWhenDynamic = false;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private void CreateOrUpdateLight()
    {
        if (generatedRoot == null)
            return;

        if (forcefieldLight == null)
        {
            Transform existing = generatedRoot.transform.Find(LightName);

            if (existing != null)
                forcefieldLight = existing.GetComponent<Light>();
        }

        if (forcefieldLight == null)
        {
            GameObject lightObject = new GameObject(LightName);
            lightObject.name = LightName;
            lightObject.hideFlags = HideFlags.DontSaveInBuild;
            lightObject.transform.SetParent(generatedRoot.transform, false);
            forcefieldLight = lightObject.AddComponent<Light>();
        }

        forcefieldLight.type = LightType.Point;
        forcefieldLight.color = forcefieldColor;
        forcefieldLight.shadows = LightShadows.None;
    }

    private void UpdateForcefield()
    {
        currentRadius = GetCurrentRadius();
        Vector3 center = GetSphereCenterWorld();
        float time = (float)GetTime();

        if (forcefieldSphere != null)
        {
            forcefieldSphere.transform.position = center;
            forcefieldSphere.transform.rotation = Quaternion.identity;

            // Unity primitive sphere has radius 0.5 at scale 1.
            forcefieldSphere.transform.localScale = Vector3.one * currentRadius * 2f;
        }

        if (forcefieldLight != null)
        {
            forcefieldLight.transform.position = center;
            forcefieldLight.color = forcefieldColor;
            forcefieldLight.intensity = 1.2f + currentRadius * 0.45f;
            forcefieldLight.range = Mathf.Max(2f, currentRadius * 2.4f);
        }

        if (forcefieldMaterial != null)
        {
            forcefieldMaterial.SetVector("_SphereCenterWS", center);
            forcefieldMaterial.SetFloat("_SphereRadiusWS", currentRadius);

            forcefieldMaterial.SetColor("_ForcefieldColor", forcefieldColor);

            forcefieldMaterial.SetFloat("_SurfaceAlpha", surfaceAlpha);
            forcefieldMaterial.SetFloat("_FresnelPower", fresnelPower);
            forcefieldMaterial.SetFloat("_FresnelIntensity", fresnelIntensity);

            forcefieldMaterial.SetFloat("_IntersectionDistance", intersectionDistance);
            forcefieldMaterial.SetFloat("_IntersectionSoftness", intersectionSoftness);
            forcefieldMaterial.SetFloat("_IntersectionIntensity", intersectionIntensity);

            forcefieldMaterial.SetFloat("_NoiseScale", noiseScale);
            forcefieldMaterial.SetFloat("_NoiseStrength", noiseStrength);
            forcefieldMaterial.SetFloat("_ScanlineScale", scanlineScale);
            forcefieldMaterial.SetFloat("_ScanlineStrength", scanlineStrength);

            forcefieldMaterial.SetFloat("_RippleWidth", rippleWidth);
            forcefieldMaterial.SetFloat("_RippleWorldRadius", rippleWorldRadius);
            forcefieldMaterial.SetFloat("_RippleIntensity", rippleIntensity);

            forcefieldMaterial.SetFloat("_CustomTime", time);
            forcefieldMaterial.SetFloatArray("_Points", ripplePoints);
        }
    }

    private Vector3 GetSphereCenterWorld()
    {
        return transform.TransformPoint(sphereCenterLocalOffset);
    }

    private float GetCurrentRadius()
    {
        if (!autoAnimateRadius)
            return manualRadius;

        float t = (float)GetTime();
        float n = Mathf.Sin(t * radiusAnimationSpeed) * 0.5f + 0.5f;
        n = Mathf.SmoothStep(0f, 1f, n);

        return Mathf.Lerp(minRadius, maxRadius, n);
    }

    private void ResetRipples()
    {
        for (int i = 0; i < MaxRipples; i++)
        {
            int p = i * 4;
            ripplePoints[p + 0] = 0f;
            ripplePoints[p + 1] = 0f;
            ripplePoints[p + 2] = 0f;
            ripplePoints[p + 3] = 2f;
        }

        nextRippleIndex = 0;
        rippleTimer = 0f;
    }

    private void UpdateRipples(float deltaTime)
    {
        for (int i = 0; i < MaxRipples; i++)
        {
            int p = i * 4;
            float lifetime = ripplePoints[p + 3];

            if (lifetime <= 1f)
            {
                lifetime += deltaTime / Mathf.Max(0.01f, rippleLifetime);

                if (lifetime > 1f)
                    lifetime = 2f;

                ripplePoints[p + 3] = lifetime;
            }
        }

        if (!autoSpawnRipples)
            return;

        rippleTimer += deltaTime;

        if (rippleTimer >= rippleSpawnInterval)
        {
            rippleTimer = 0f;
            AddRipple(UnityEngine.Random.onUnitSphere);
        }
    }

    private void AddRipple(Vector3 direction)
    {
        direction = direction.sqrMagnitude < 0.0001f
            ? Vector3.up
            : direction.normalized;

        Vector3 center = GetSphereCenterWorld();
        Vector3 point = center + direction * currentRadius;

        int p = nextRippleIndex * 4;

        ripplePoints[p + 0] = point.x;
        ripplePoints[p + 1] = point.y;
        ripplePoints[p + 2] = point.z;
        ripplePoints[p + 3] = 0f;

        nextRippleIndex++;
        if (nextRippleIndex >= MaxRipples)
            nextRippleIndex = 0;
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

    private void TryEnableCameraTextures()
    {
        Camera[] cameras = Camera.allCameras;

        foreach (Camera camera in cameras)
        {
            if (camera == null)
                continue;

            if (tryEnableDepthTexture)
                camera.depthTextureMode |= DepthTextureMode.Depth;

            Component urpCameraData = camera.GetComponent("UniversalAdditionalCameraData");

            if (urpCameraData != null)
            {
                if (tryEnableDepthTexture)
                    TrySetBoolProperty(urpCameraData, "requiresDepthTexture", true);

                if (tryEnableOpaqueTexture)
                    TrySetBoolProperty(urpCameraData, "requiresColorTexture", true);
            }
        }

        RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;

        if (asset != null)
        {
            if (tryEnableDepthTexture)
                TrySetBoolProperty(asset, "supportsCameraDepthTexture", true);

            if (tryEnableOpaqueTexture)
                TrySetBoolProperty(asset, "supportsCameraOpaqueTexture", true);

#if UNITY_EDITOR
            EditorUtility.SetDirty(asset);
#endif
        }
    }

    private void TrySetBoolProperty(object target, string propertyName, bool value)
    {
        if (target == null)
            return;

        Type type = target.GetType();

        PropertyInfo property = type.GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (property == null)
            return;

        if (!property.CanWrite)
            return;

        if (property.PropertyType != typeof(bool))
            return;

        property.SetValue(target, value);
    }

    private void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private void DeleteChild(string childName)
    {
        if (generatedRoot == null)
            return;

        Transform child = generatedRoot.transform.Find(childName);

        if (child != null)
            DestroySmart(child.gameObject);
    }

    private void DestroySmart(UnityEngine.Object target)
    {
        if (target == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(target);
        else
            Destroy(target);
#else
        Destroy(target);
#endif
    }

    private void OnDrawGizmos()
    {
        Vector3 center = GetSphereCenterWorld();
        float radius = currentRadius > 0f ? currentRadius : manualRadius;

        Gizmos.color = new Color(forcefieldColor.r, forcefieldColor.g, forcefieldColor.b, 0.85f);
        Gizmos.DrawWireSphere(center, radius);

        Gizmos.color = new Color(forcefieldColor.r, forcefieldColor.g, forcefieldColor.b, 0.25f);
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
Shader ""XRCC/Cyanilux Style Forcefield URP""
{
    Properties
    {
        _ForcefieldColor (""Forcefield Color"", Color) = (0.15, 0.85, 1, 1)

        _SphereCenterWS (""Sphere Center WS"", Vector) = (0, 0, 0, 0)
        _SphereRadiusWS (""Sphere Radius WS"", Float) = 1

        _SurfaceAlpha (""Surface Alpha"", Range(0, 1)) = 0.025
        _FresnelPower (""Fresnel Power"", Float) = 6
        _FresnelIntensity (""Fresnel Intensity"", Float) = 1.5

        _IntersectionDistance (""Intersection Distance"", Float) = 0.18
        _IntersectionSoftness (""Intersection Softness"", Float) = 0.16
        _IntersectionIntensity (""Intersection Intensity"", Float) = 6

        _NoiseScale (""Noise Scale"", Float) = 9
        _NoiseStrength (""Noise Strength"", Range(0, 1)) = 0.22
        _ScanlineScale (""Scanline Scale"", Float) = 14
        _ScanlineStrength (""Scanline Strength"", Range(0, 2)) = 0.45

        _RippleWidth (""Ripple Width"", Float) = 0.22
        _RippleWorldRadius (""Ripple World Radius"", Float) = 1.4
        _RippleIntensity (""Ripple Intensity"", Float) = 3

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
            Name ""XRCC Forcefield Depth Intersection""

            Tags
            {
                ""LightMode"" = ""UniversalForward""
            }

            Blend SrcAlpha One
            ZWrite Off
            ZTest Always
            Cull Off
            Offset -8, -8

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl""

            CBUFFER_START(UnityPerMaterial)
                float4 _ForcefieldColor;

                float4 _SphereCenterWS;
                float _SphereRadiusWS;

                float _SurfaceAlpha;
                float _FresnelPower;
                float _FresnelIntensity;

                float _IntersectionDistance;
                float _IntersectionSoftness;
                float _IntersectionIntensity;

                float _NoiseScale;
                float _NoiseStrength;
                float _ScanlineScale;
                float _ScanlineStrength;

                float _RippleWidth;
                float _RippleWorldRadius;
                float _RippleIntensity;

                float _CustomTime;
            CBUFFER_END

            uniform float _Points[24];

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
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

            float GetRipple(float3 scenePositionWS)
            {
                float rippleOutput = 0.0;

                [unroll]
                for (int i = 0; i < 24; i += 4)
                {
                    float3 pointWS = float3(_Points[i + 0], _Points[i + 1], _Points[i + 2]);
                    float lifetime = _Points[i + 3];

                    if (lifetime <= 1.0)
                    {
                        float expandingRadius = lifetime * _RippleWorldRadius;
                        float distanceToRipple = distance(scenePositionWS, pointWS);
                        float ringDistance = abs(distanceToRipple - expandingRadius);

                        float ring = 1.0 - smoothstep(0.0, _RippleWidth, ringDistance);
                        float fade = saturate(1.0 - lifetime);

                        rippleOutput += ring * fade;
                    }
                }

                return saturate(rippleOutput);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalize(normalInputs.normalWS);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;

                float rawSceneDepth = SampleSceneDepth(screenUV);

                #if UNITY_REVERSED_Z
                    float depth = rawSceneDepth;
                    float sceneValid = step(0.00001, rawSceneDepth);
                #else
                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawSceneDepth);
                    float sceneValid = step(rawSceneDepth, 0.99999);
                #endif

                float3 scenePositionWS = ComputeWorldSpacePosition(screenUV, depth, UNITY_MATRIX_I_VP);

                float sceneDistanceFromSphereCenter = distance(scenePositionWS, _SphereCenterWS.xyz);
                float sceneDistanceFromSphereShell = abs(sceneDistanceFromSphereCenter - _SphereRadiusWS);

                float intersection = 1.0 - smoothstep(
                    _IntersectionDistance,
                    _IntersectionDistance + _IntersectionSoftness,
                    sceneDistanceFromSphereShell
                );

                intersection *= sceneValid;

                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float normalDot = abs(dot(viewDir, normalize(input.normalWS)));
                float fresnel = pow(1.0 - saturate(normalDot), _FresnelPower);

                float noise = ValueNoise(scenePositionWS * _NoiseScale + _CustomTime * 0.35);
                float noiseMask = lerp(1.0, noise, _NoiseStrength);

                float scan = sin(sceneDistanceFromSphereCenter * _ScanlineScale - _CustomTime * 6.0) * 0.5 + 0.5;
                scan = pow(scan, 5.0) * _ScanlineStrength;

                float ripple = GetRipple(scenePositionWS);

                float surfaceGlow = fresnel * _FresnelIntensity * _SurfaceAlpha;
                float intersectionGlow = intersection * _IntersectionIntensity * noiseMask; 
                float rippleGlow = ripple * _RippleIntensity;

                float alpha = surfaceGlow + intersectionGlow + rippleGlow;
                alpha = saturate(alpha);

                clip(alpha - 0.001);

                float energy = surfaceGlow + intersectionGlow + rippleGlow + scan * intersection;
                float3 color = _ForcefieldColor.rgb * energy;

                color += float3(1.0, 1.0, 1.0) * intersection * 0.35;
                color += float3(1.0, 1.0, 1.0) * ripple * 0.25;

                return half4(color, alpha);
            }

            ENDHLSL
        }
    }

    FallBack Off
}
";
}