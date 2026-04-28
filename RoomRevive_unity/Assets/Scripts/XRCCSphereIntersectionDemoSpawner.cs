using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class XRCCSphereIntersectionDemoSpawner : MonoBehaviour
{
    private const string GeneratedRootName = "__XRCC_SphereIntersectionDemo";
    private const string CubeName = "__XRCC_TestCube";
    private const string OverlayName = "__XRCC_TestCube_IntersectionOverlay";
    private const string RingsRootName = "__XRCC_DebugSphereRings";
    private const string EnergyRootName = "__XRCC_EnergyDots";
    private const string CenterOrbName = "__XRCC_CenterOrb";
    private const string LightName = "__XRCC_EffectLight";

    private const string ShaderName = "XRCC/Sphere Intersection Demo Surface URP";

#if UNITY_EDITOR
    private const string ShaderFolder = "Assets/XRCC/GeneratedShaders";
    private const string ShaderPath = ShaderFolder + "/XRCC_SphereIntersectionDemoSurfaceURP.shader";
    private static bool isWritingShader;
#endif

    [Header("One Click Setup")]
    public bool initializeInOnValidate = true;
    public bool autoWriteShaderAsset = true;
    public bool forceRewriteShaderAsset = false;
    public bool rebuildDemoObjects = false;

    [Header("Generated Objects")]
    public bool createCube = true;
    public bool createOverlay = true;
    public bool createDebugRings = true;
    public bool createEnergyDots = true;
    public bool createCenterOrb = true;
    public bool createEffectLight = true;

    [Header("Animation")]
    public bool animateInPlayMode = true;
    public bool animateInEditMode = true;

    [Tooltip("When enabled, the sphere radius pulses automatically so you can see the effect immediately.")]
    public bool autoAnimateRadius = true;

    [Min(0.01f)] public float minAnimatedRadius = 0.25f;
    [Min(0.01f)] public float maxAnimatedRadius = 3.25f;
    [Range(0.01f, 10f)] public float animationSpeed = 1.4f;

    [Header("Manual Sphere")]
    [Tooltip("Used when Auto Animate Radius is off.")]
    [Min(0.01f)] public float manualRadius = 1.35f;

    [Tooltip("Optional offset for the sphere center from this GameObject.")]
    public Vector3 sphereCenterLocalOffset = Vector3.zero;

    [Header("Test Cube")]
    public Vector3 cubeLocalPosition = new Vector3(1.15f, 0f, 0f);
    public Vector3 cubeLocalEuler = new Vector3(0f, 12f, 0f);
    public Vector3 cubeSize = new Vector3(2f, 2f, 2f);
    public Color cubeColor = new Color(0.12f, 0.14f, 0.16f, 1f);

    [Header("Intersection Look")]
    public Color effectColor = new Color(0.25f, 0.95f, 1f, 1f);

    [Tooltip("Width of the visible band on the cube surface.")]
    [Range(0.005f, 2f)] public float bandThickness = 0.14f;

    [Tooltip("Softness around the edge of the visible band.")]
    [Range(0.001f, 1f)] public float edgeSoftness = 0.08f;

    [Tooltip("Opacity/brightness multiplier.")]
    [Range(0f, 12f)] public float intensity = 4.5f;

    [Header("Shader Detail")]
    [Range(0f, 25f)] public float noiseScale = 8f;
    [Range(0f, 1f)] public float noiseStrength = 0.28f;
    [Range(1f, 80f)] public float gridScale = 18f;
    [Range(0f, 1f)] public float gridStrength = 0.45f;
    [Range(0.1f, 8f)] public float rimPower = 1.5f;

    [Header("Debug Rings")]
    public Color ringColor = new Color(0.25f, 0.95f, 1f, 0.85f);
    [Range(8, 256)] public int ringSegments = 128;
    [Range(0.001f, 0.08f)] public float ringWidth = 0.015f;

    [Header("Energy Dots")]
    [Range(0, 128)] public int energyDotCount = 36;
    [Range(0.01f, 0.25f)] public float energyDotSize = 0.055f;
    public Color energyDotColor = new Color(0.5f, 1f, 1f, 1f);

    [Header("Runtime Info")]
    [SerializeField] private float currentRadius;
    [SerializeField] private string shaderStatus;
    [SerializeField] private GameObject generatedRoot;
    [SerializeField] private GameObject testCube;
    [SerializeField] private GameObject overlayObject;

    private Material cubeMaterial;
    private Material overlayMaterial;
    private Material ringMaterial;
    private Material energyDotMaterial;
    private Material orbMaterial;

    private Transform ringsRoot;
    private readonly List<LineRenderer> rings = new List<LineRenderer>();

    private Transform energyRoot;
    private readonly List<Transform> energyDots = new List<Transform>();

    private GameObject centerOrb;
    private Light effectLight;

    private double editorStartTime;

    private void Reset()
    {
        editorStartTime = GetTime();
        Initialize(true);
    }

    private void Awake()
    {
        editorStartTime = GetTime();
        Initialize(false);
    }

    private void OnEnable()
    {
        editorStartTime = GetTime();
        Initialize(false);
    }

    private void Update()
    {
        bool shouldAnimate =
            Application.isPlaying && animateInPlayMode ||
            !Application.isPlaying && animateInEditMode;

        if (!shouldAnimate)
            return;

        Initialize(false);
        UpdateEffect();
    }

    private void OnValidate()
    {
        if (!initializeInOnValidate)
            return;

        minAnimatedRadius = Mathf.Max(0.01f, minAnimatedRadius);
        maxAnimatedRadius = Mathf.Max(minAnimatedRadius + 0.01f, maxAnimatedRadius);
        manualRadius = Mathf.Max(0.01f, manualRadius);

        bandThickness = Mathf.Max(0.005f, bandThickness);
        edgeSoftness = Mathf.Max(0.001f, edgeSoftness);

        cubeSize.x = Mathf.Max(0.01f, cubeSize.x);
        cubeSize.y = Mathf.Max(0.01f, cubeSize.y);
        cubeSize.z = Mathf.Max(0.01f, cubeSize.z);

        ringSegments = Mathf.Clamp(ringSegments, 8, 256);

        bool forceRebuild = rebuildDemoObjects;
        rebuildDemoObjects = false;

        Initialize(forceRebuild);
        UpdateEffect();

#if UNITY_EDITOR
        SceneView.RepaintAll();
#endif
    }

    [ContextMenu("XRCC / Rebuild Full Demo")]
    public void RebuildFullDemo()
    {
        Initialize(true);
        UpdateEffect();
    }

    [ContextMenu("XRCC / Delete Generated Demo Objects")]
    public void DeleteGeneratedDemoObjects()
    {
        Transform existing = transform.Find(GeneratedRootName);

        if (existing != null)
            DestroySmart(existing.gameObject);

        generatedRoot = null;
        testCube = null;
        overlayObject = null;
        ringsRoot = null;
        energyRoot = null;
        centerOrb = null;
        effectLight = null;

        rings.Clear();
        energyDots.Clear();
    }

    private void Initialize(bool forceRebuild)
    {
#if UNITY_EDITOR
        if (autoWriteShaderAsset)
            WriteShaderAssetIfNeeded(forceRewriteShaderAsset);
#endif

        CreateMaterials();

        if (forceRebuild)
            DeleteGeneratedDemoObjects();

        CreateGeneratedRoot();

        if (createCube)
            CreateOrUpdateCube();

        if (createOverlay)
            CreateOrUpdateOverlay();

        if (createDebugRings)
            CreateOrUpdateRings();
        else
            DeleteChildByName(RingsRootName);

        if (createEnergyDots)
            CreateOrUpdateEnergyDots();
        else
            DeleteChildByName(EnergyRootName);

        if (createCenterOrb)
            CreateOrUpdateCenterOrb();
        else
            DeleteChildByName(CenterOrbName);

        if (createEffectLight)
            CreateOrUpdateEffectLight();
        else
            DeleteChildByName(LightName);

        UpdateEffect();
    }

    private void CreateGeneratedRoot()
    {
        if (generatedRoot != null)
            return;

        Transform existing = transform.Find(GeneratedRootName);

        if (existing != null)
        {
            generatedRoot = existing.gameObject;
            return;
        }

        generatedRoot = new GameObject(GeneratedRootName);
        generatedRoot.hideFlags = HideFlags.DontSaveInBuild;
        generatedRoot.transform.SetParent(transform, false);
        generatedRoot.transform.localPosition = Vector3.zero;
        generatedRoot.transform.localRotation = Quaternion.identity;
        generatedRoot.transform.localScale = Vector3.one;
    }

    private void CreateMaterials()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null)
            litShader = Shader.Find("Standard");

        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null)
            unlitShader = Shader.Find("Unlit/Color");

        Shader overlayShader = Shader.Find(ShaderName);

        shaderStatus = overlayShader != null
            ? "Shader found: " + ShaderName
            : "Shader missing. It should be generated at Assets/XRCC/GeneratedShaders/";

        if (cubeMaterial == null)
        {
            cubeMaterial = new Material(litShader);
            cubeMaterial.name = "__XRCC_TestCube_Material";
            cubeMaterial.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
        }

        SetMaterialColor(cubeMaterial, cubeColor);

        if (overlayShader != null)
        {
            if (overlayMaterial == null || overlayMaterial.shader != overlayShader)
            {
                overlayMaterial = new Material(overlayShader);
                overlayMaterial.name = "__XRCC_IntersectionOverlay_Material";
                overlayMaterial.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
                overlayMaterial.renderQueue = 3100;
            }
        }

        if (ringMaterial == null)
        {
            ringMaterial = new Material(unlitShader);
            ringMaterial.name = "__XRCC_DebugRing_Material";
            ringMaterial.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
        }

        SetMaterialColor(ringMaterial, ringColor);

        if (energyDotMaterial == null)
        {
            energyDotMaterial = new Material(unlitShader);
            energyDotMaterial.name = "__XRCC_EnergyDot_Material";
            energyDotMaterial.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
        }

        SetMaterialColor(energyDotMaterial, energyDotColor);

        if (orbMaterial == null)
        {
            orbMaterial = new Material(unlitShader);
            orbMaterial.name = "__XRCC_CenterOrb_Material";
            orbMaterial.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
        }

        SetMaterialColor(orbMaterial, new Color(effectColor.r, effectColor.g, effectColor.b, 0.85f));
    }

    private void CreateOrUpdateCube()
    {
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
        testCube.transform.localRotation = Quaternion.Euler(cubeLocalEuler);
        testCube.transform.localScale = cubeSize;

        MeshRenderer renderer = testCube.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = testCube.AddComponent<MeshRenderer>();

        MeshFilter filter = testCube.GetComponent<MeshFilter>();
        if (filter == null)
            filter = testCube.AddComponent<MeshFilter>();

        BoxCollider collider = testCube.GetComponent<BoxCollider>();
        if (collider == null)
            collider = testCube.AddComponent<BoxCollider>();

        collider.center = Vector3.zero;
        collider.size = Vector3.one;

        renderer.sharedMaterial = cubeMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = true;
    }

    private void CreateOrUpdateOverlay()
    {
        if (testCube == null)
            return;

        if (overlayMaterial == null)
            return;

        Transform existing = testCube.transform.Find(OverlayName);

        if (existing != null)
            overlayObject = existing.gameObject;

        if (overlayObject == null)
        {
            overlayObject = new GameObject(OverlayName);
            overlayObject.hideFlags = HideFlags.DontSaveInBuild;
            overlayObject.transform.SetParent(testCube.transform, false);
            overlayObject.transform.localPosition = Vector3.zero;
            overlayObject.transform.localRotation = Quaternion.identity;
            overlayObject.transform.localScale = Vector3.one;
        }

        MeshFilter sourceFilter = testCube.GetComponent<MeshFilter>();

        MeshFilter overlayFilter = overlayObject.GetComponent<MeshFilter>();
        if (overlayFilter == null)
            overlayFilter = overlayObject.AddComponent<MeshFilter>();

        MeshRenderer overlayRenderer = overlayObject.GetComponent<MeshRenderer>();
        if (overlayRenderer == null)
            overlayRenderer = overlayObject.AddComponent<MeshRenderer>();

        overlayFilter.sharedMesh = sourceFilter != null ? sourceFilter.sharedMesh : null;
        overlayRenderer.sharedMaterial = overlayMaterial;
        overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        overlayRenderer.receiveShadows = false;
        overlayRenderer.allowOcclusionWhenDynamic = false;
        overlayRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private void CreateOrUpdateRings()
    {
        if (ringsRoot == null)
        {
            Transform existing = generatedRoot.transform.Find(RingsRootName);

            if (existing != null)
                ringsRoot = existing;
        }

        if (ringsRoot == null)
        {
            GameObject root = new GameObject(RingsRootName);
            root.hideFlags = HideFlags.DontSaveInBuild;
            root.transform.SetParent(generatedRoot.transform, false);
            ringsRoot = root.transform;
        }

        while (rings.Count < 3)
        {
            GameObject ringObject = new GameObject("__XRCC_Ring_" + rings.Count);
            ringObject.hideFlags = HideFlags.DontSaveInBuild;
            ringObject.transform.SetParent(ringsRoot, false);

            LineRenderer line = ringObject.AddComponent<LineRenderer>();
            line.sharedMaterial = ringMaterial;
            line.useWorldSpace = true;
            line.loop = true;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            rings.Add(line);
        }

        for (int i = 0; i < rings.Count; i++)
        {
            if (rings[i] == null)
                continue;

            rings[i].sharedMaterial = ringMaterial;
            rings[i].startColor = ringColor;
            rings[i].endColor = ringColor;
            rings[i].startWidth = ringWidth;
            rings[i].endWidth = ringWidth;
        }
    }

    private void CreateOrUpdateEnergyDots()
    {
        if (energyRoot == null)
        {
            Transform existing = generatedRoot.transform.Find(EnergyRootName);

            if (existing != null)
                energyRoot = existing;
        }

        if (energyRoot == null)
        {
            GameObject root = new GameObject(EnergyRootName);
            root.hideFlags = HideFlags.DontSaveInBuild;
            root.transform.SetParent(generatedRoot.transform, false);
            energyRoot = root.transform;
        }

        while (energyDots.Count < energyDotCount)
        {
            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "__XRCC_EnergyDot_" + energyDots.Count;
            dot.hideFlags = HideFlags.DontSaveInBuild;
            dot.transform.SetParent(energyRoot, false);

            Collider col = dot.GetComponent<Collider>();
            if (col != null)
                DestroySmart(col);

            MeshRenderer renderer = dot.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = energyDotMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            energyDots.Add(dot.transform);
        }

        for (int i = energyDots.Count - 1; i >= energyDotCount; i--)
        {
            if (energyDots[i] != null)
                DestroySmart(energyDots[i].gameObject);

            energyDots.RemoveAt(i);
        }
    }

    private void CreateOrUpdateCenterOrb()
    {
        if (centerOrb == null)
        {
            Transform existing = generatedRoot.transform.Find(CenterOrbName);

            if (existing != null)
                centerOrb = existing.gameObject;
        }

        if (centerOrb == null)
        {
            centerOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            centerOrb.name = CenterOrbName;
            centerOrb.hideFlags = HideFlags.DontSaveInBuild;
            centerOrb.transform.SetParent(generatedRoot.transform, false);

            Collider col = centerOrb.GetComponent<Collider>();
            if (col != null)
                DestroySmart(col);
        }

        MeshRenderer renderer = centerOrb.GetComponent<MeshRenderer>();

        if (renderer != null)
        {
            renderer.sharedMaterial = orbMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private void CreateOrUpdateEffectLight()
    {
        if (effectLight == null)
        {
            Transform existing = generatedRoot.transform.Find(LightName);

            if (existing != null)
                effectLight = existing.GetComponent<Light>();
        }

        if (effectLight == null)
        {
            GameObject lightObject = new GameObject(LightName);
            lightObject.hideFlags = HideFlags.DontSaveInBuild;
            lightObject.transform.SetParent(generatedRoot.transform, false);
            effectLight = lightObject.AddComponent<Light>();
        }

        effectLight.type = LightType.Point;
        effectLight.color = effectColor;
        effectLight.intensity = 2.2f;
        effectLight.range = 5.5f;
        effectLight.shadows = LightShadows.None;
    }

    private void UpdateEffect()
    {
        Vector3 center = GetSphereCenterWorld();
        currentRadius = GetCurrentRadius();
        float time = (float)GetTime();

        if (overlayMaterial != null)
        {
            overlayMaterial.SetVector("_SphereCenterWS", center);
            overlayMaterial.SetFloat("_SphereRadiusWS", currentRadius);
            overlayMaterial.SetColor("_EffectColor", effectColor);
            overlayMaterial.SetFloat("_BandThickness", bandThickness);
            overlayMaterial.SetFloat("_EdgeSoftness", edgeSoftness);
            overlayMaterial.SetFloat("_Intensity", intensity);
            overlayMaterial.SetFloat("_CustomTime", time);
            overlayMaterial.SetFloat("_NoiseScale", noiseScale);
            overlayMaterial.SetFloat("_NoiseStrength", noiseStrength);
            overlayMaterial.SetFloat("_GridScale", gridScale);
            overlayMaterial.SetFloat("_GridStrength", gridStrength);
            overlayMaterial.SetFloat("_RimPower", rimPower);
        }

        UpdateRings(center, currentRadius);
        UpdateEnergyDots(center, currentRadius, time);
        UpdateCenterOrb(center, time);
        UpdateEffectLight(center);
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
        float normalized = Mathf.Sin(t * animationSpeed) * 0.5f + 0.5f;
        normalized = Mathf.SmoothStep(0f, 1f, normalized);

        return Mathf.Lerp(minAnimatedRadius, maxAnimatedRadius, normalized);
    }

    private double GetTime()
    {
        if (Application.isPlaying)
            return Time.time;

#if UNITY_EDITOR
        return EditorApplication.timeSinceStartup;
#else
        return Time.realtimeSinceStartup;
#endif
    }

    private void UpdateRings(Vector3 center, float radius)
    {
        if (!createDebugRings || rings.Count < 3)
            return;

        for (int i = 0; i < rings.Count; i++)
        {
            LineRenderer line = rings[i];

            if (line == null)
                continue;

            line.positionCount = ringSegments;
            line.startWidth = ringWidth;
            line.endWidth = ringWidth;
            line.startColor = ringColor;
            line.endColor = ringColor;

            for (int p = 0; p < ringSegments; p++)
            {
                float a = (p / (float)ringSegments) * Mathf.PI * 2f;
                float x = Mathf.Cos(a) * radius;
                float y = Mathf.Sin(a) * radius;

                Vector3 pos;

                if (i == 0)
                    pos = center + new Vector3(x, y, 0f);
                else if (i == 1)
                    pos = center + new Vector3(x, 0f, y);
                else
                    pos = center + new Vector3(0f, x, y);

                line.SetPosition(p, pos);
            }
        }
    }

    private void UpdateEnergyDots(Vector3 center, float radius, float time)
    {
        if (!createEnergyDots || energyDots.Count == 0)
            return;

        float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));

        for (int i = 0; i < energyDots.Count; i++)
        {
            Transform dot = energyDots[i];

            if (dot == null)
                continue;

            float index = i + 0.5f;
            float y = 1f - index / energyDots.Count * 2f;
            float r = Mathf.Sqrt(1f - y * y);
            float theta = goldenAngle * i + time * 0.45f;

            Vector3 dir = new Vector3(
                Mathf.Cos(theta) * r,
                y,
                Mathf.Sin(theta) * r
            ).normalized;

            float wave = Mathf.Sin(time * 3f + i * 0.57f) * 0.06f;
            Vector3 pos = center + dir * (radius + wave);

            dot.position = pos;

            float pulse = Mathf.Sin(time * 4.5f + i * 0.73f) * 0.5f + 0.5f;
            float size = energyDotSize * Mathf.Lerp(0.55f, 1.45f, pulse);

            dot.localScale = Vector3.one * size;

            MeshRenderer renderer = dot.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = energyDotMaterial;
        }
    }

    private void UpdateCenterOrb(Vector3 center, float time)
    {
        if (!createCenterOrb || centerOrb == null)
            return;

        centerOrb.transform.position = center;

        float pulse = Mathf.Sin(time * 3f) * 0.5f + 0.5f;
        float size = Mathf.Lerp(0.09f, 0.16f, pulse);

        centerOrb.transform.localScale = Vector3.one * size;

        if (orbMaterial != null)
        {
            Color c = new Color(effectColor.r, effectColor.g, effectColor.b, Mathf.Lerp(0.45f, 1f, pulse));
            SetMaterialColor(orbMaterial, c);
        }
    }

    private void UpdateEffectLight(Vector3 center)
    {
        if (!createEffectLight || effectLight == null)
            return;

        effectLight.transform.position = center;
        effectLight.color = effectColor;
        effectLight.intensity = 1.2f + currentRadius * 0.35f;
        effectLight.range = Mathf.Max(2f, currentRadius * 2.2f);
    }

    private void SetMaterialColor(Material mat, Color color)
    {
        if (mat == null)
            return;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);

        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", color * 1.5f);
    }

    private void DeleteChildByName(string childName)
    {
        if (generatedRoot == null)
            return;

        Transform child = generatedRoot.transform.Find(childName);

        if (child != null)
            DestroySmart(child.gameObject);
    }

    private void DestroySmart(Object obj)
    {
        if (obj == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(obj);      
        else
            Destroy(obj);
#else
        Destroy(obj);
#endif
    }

    private void OnDrawGizmos()
    {
        Vector3 center = GetSphereCenterWorld();
        float radius = Application.isPlaying || autoAnimateRadius ? currentRadius : manualRadius;

        Gizmos.color = new Color(effectColor.r, effectColor.g, effectColor.b, 0.85f);
        Gizmos.DrawWireSphere(center, radius);

        Gizmos.color = new Color(effectColor.r, effectColor.g, effectColor.b, 0.25f);
        Gizmos.DrawSphere(center, 0.06f);
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
        }
        finally
        {
            isWritingShader = false;
        }
    }

    private void EnsureFolder(string parent, string child)
    {
        string fullPath = parent + "/" + child;

        if (!AssetDatabase.IsValidFolder(fullPath))
            AssetDatabase.CreateFolder(parent, child);
    }
#endif

    private const string ShaderSource = @"
Shader ""XRCC/Sphere Intersection Demo Surface URP""
{
    Properties
    {
        _EffectColor (""Effect Color"", Color) = (0.25, 0.95, 1, 1)
        _SphereCenterWS (""Sphere Center WS"", Vector) = (0, 0, 0, 0)
        _SphereRadiusWS (""Sphere Radius WS"", Float) = 1
        _BandThickness (""Band Thickness"", Float) = 0.14
        _EdgeSoftness (""Edge Softness"", Float) = 0.08
        _Intensity (""Intensity"", Float) = 4.5

        _CustomTime (""Custom Time"", Float) = 0
        _NoiseScale (""Noise Scale"", Float) = 8
        _NoiseStrength (""Noise Strength"", Float) = 0.28
        _GridScale (""Grid Scale"", Float) = 18
        _GridStrength (""Grid Strength"", Float) = 0.45
        _RimPower (""Rim Power"", Float) = 1.5
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
            Name ""XRCC Sphere Surface Contact""

            Tags
            {
                ""LightMode"" = ""UniversalForward""
            }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -8, -8

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""

            CBUFFER_START(UnityPerMaterial)
                float4 _EffectColor;
                float4 _SphereCenterWS;
                float _SphereRadiusWS;
                float _BandThickness;
                float _EdgeSoftness;
                float _Intensity;

                float _CustomTime;
                float _NoiseScale;
                float _NoiseStrength;
                float _GridScale;
                float _GridStrength;
                float _RimPower;
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

            float GridMask(float2 uv, float scale)
            {
                float2 gridUV = abs(frac(uv * scale) - 0.5);
                float line = 1.0 - smoothstep(0.0, 0.035, min(gridUV.x, gridUV.y));
                return line;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
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

                float innerSpark = 1.0 - smoothstep(
                    _BandThickness * 0.18,
                    _BandThickness * 0.55,
                    shellDistance
                );

                float noise = ValueNoise(input.positionWS * _NoiseScale + _CustomTime * 0.45);
                float noiseMask = lerp(1.0, noise, _NoiseStrength);

                float scan = sin((distanceToCenter * 22.0) - (_CustomTime * 8.0)) * 0.5 + 0.5;
                scan = pow(scan, 4.0);

                float grid = GridMask(input.uv, _GridScale) * _GridStrength;

                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(1.0 - saturate(dot(viewDir, normalize(input.normalWS))), _RimPower);

                float alpha = band;
                alpha *= noiseMask;
                alpha *= 1.0 + scan * 0.35;
                alpha *= _EffectColor.a * _Intensity;
                alpha = saturate(alpha);

                clip(alpha - 0.005);

                float energy = band * 1.4 + innerSpark * 2.4 + fresnel * 1.2 + grid * band;
                float3 color = _EffectColor.rgb * energy;

                color += _EffectColor.rgb * scan * band * 0.65;
                color += float3(1.0, 1.0, 1.0) * innerSpark * 0.45;

                return half4(color, alpha);
            }

            ENDHLSL
        }
    }

    FallBack Off
}
";
}