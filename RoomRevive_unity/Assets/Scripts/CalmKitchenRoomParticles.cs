using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Calm, detailed room-atmosphere particle effect for a kitchen.
///
/// IMPORTANT:
/// - Does NOT set the transform pose of generated particle children after creation.
/// - You can move/rotate/scale generated particle children manually.
/// - Steam wisps have been removed and old generated steam children are auto-deleted.
/// - All particles use Mesh render mode.
/// - All particles use Facing render alignment.
/// - All particles can collide with 3D world colliders.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class CalmKitchenRoomParticles : MonoBehaviour
{
    private const string Prefix = "__CalmKitchen_";
    private const string AmbientName = Prefix + "AmbientDustMotes";
    private const string SunbeamName = Prefix + "SoftSunbeamDust";
    private const string GlowName = Prefix + "SlowFloatingGlow";
    private const string CounterName = Prefix + "CounterShimmer";
    private const string SteamPrefix = Prefix + "SteamWisp_";

    [Header("Main")]
    [Range(0f, 2f)]
    public float intensity = 1f;

    public bool rebuildOnValidate = true;
    public bool playInEditMode = true;

    [Header("Pose Safety")]
    [Tooltip("OFF = the script does not offset particle shapes internally. Best when you want to move each generated particle child manually.")]
    public bool useShapeOffsetsInsteadOfTransformPose = false;

    [Header("Particle Rendering")]
    public bool useMeshParticles = true;

    [Tooltip("Optional mesh override. If empty, the script generates a cube mesh automatically.")]
    public Mesh particleMeshOverride;

    public bool enableMeshGPUInstancing = true;

    [Tooltip("Facing makes the mesh particles face the player/camera.")]
    public ParticleSystemRenderSpace particleRenderAlignment = ParticleSystemRenderSpace.Facing;

    [Header("Particle Collision")]
    public bool enableParticleCollision = true;

    [Tooltip("Default is Everything.")]
    public LayerMask particleCollisionLayers = ~0;

    [Range(0.01f, 3f)]
    public float collisionRadiusScale = 1f;

    [Range(0f, 2f)]
    public float collisionBounce = 1f;

    [Range(0f, 1f)]
    public float collisionDampen = 0f;

    [Range(0f, 1f)]
    public float collisionLifetimeLoss = 0f;

    public float minKillSpeed = 0f;
    public float maxKillSpeed = 10000f;

    public ParticleSystemCollisionQuality collisionQuality = ParticleSystemCollisionQuality.High;

    [Range(1, 256)]
    public int maxCollisionShapes = 256;

    public bool enableDynamicColliders = true;
    public float colliderForce = 0f;
    public bool multiplyColliderForceByCollisionAngle = true;
    public bool multiplyColliderForceByParticleSpeed = false;
    public bool multiplyColliderForceByParticleSize = false;
    public bool sendCollisionMessages = false;

    [Header("Particle Material")]
    [Tooltip("ON = use Unity's built-in Default-ParticleSystem Material from Resources/unity_builtin_extra.")]
    public bool useUnityDefaultParticleMaterial = true;

    [Tooltip("Optional override. If assigned and Use Unity Default Particle Material is OFF, all particles use this material.")]
    public Material particleMaterialOverride;

    [Header("Room Volume")]
    [Tooltip("Only used as ParticleSystem Shape offset when Use Shape Offsets Instead Of Transform Pose is ON. It does NOT move the GameObject.")]
    public Vector3 roomCenterOffset = new Vector3(0f, 1.35f, 0f);

    public Vector3 roomSize = new Vector3(4.5f, 2.7f, 4.5f);

    [Header("Effect Colors — Ambient Dust")]
    public Color ambientDustColorA = new Color(1.0f, 0.82f, 0.55f, 1f);
    public Color ambientDustColorB = new Color(0.78f, 0.86f, 1.0f, 1f);

    [Header("Effect Colors — Sunbeam Dust")]
    public Color sunbeamDustColor = new Color(1.0f, 0.82f, 0.55f, 1f);

    [Header("Effect Colors — Slow Floating Glow")]
    public Color slowGlowColor = new Color(1.0f, 0.74f, 0.38f, 1f);

    [Header("Effect Colors — Counter Shimmer")]
    public Color counterShimmerColor = new Color(1.0f, 0.82f, 0.55f, 1f);

    [Header("Sunbeam")]
    [Tooltip("Only used as ParticleSystem Shape offset when Use Shape Offsets Instead Of Transform Pose is ON. It does NOT move the GameObject.")]
    public Vector3 sunbeamLocalPosition = new Vector3(-1.6f, 1.65f, -1.2f);

    [Tooltip("Only used as ParticleSystem Shape rotation when Use Shape Offsets Instead Of Transform Pose is ON. It does NOT rotate the GameObject.")]
    public Vector3 sunbeamLocalEuler = new Vector3(0f, 35f, 0f);

    public Vector3 sunbeamBoxSize = new Vector3(1.4f, 1.7f, 0.35f);

    [Header("Counter Shimmer")]
    [Tooltip("Only used as ParticleSystem Shape offset when Use Shape Offsets Instead Of Transform Pose is ON. It does NOT move the GameObject.")]
    public Vector3 counterAreaLocalPosition = new Vector3(0f, 1.02f, 0.4f);

    public Vector3 counterAreaSize = new Vector3(4.35f, 2.66f, 5.6f);

    private Material _cachedUnityDefaultParticleMaterial;
    private Material _runtimeFallbackMaterial;
    private Texture2D _runtimeFallbackTexture;
    private Mesh _generatedCubeMesh;

#if UNITY_EDITOR
    private bool _queuedEditorRebuild;
#endif

    private void OnEnable()
    {
        BuildEffect();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        roomSize.x = Mathf.Max(0.1f, roomSize.x);
        roomSize.y = Mathf.Max(0.1f, roomSize.y);
        roomSize.z = Mathf.Max(0.1f, roomSize.z);

        sunbeamBoxSize.x = Mathf.Max(0.01f, sunbeamBoxSize.x);
        sunbeamBoxSize.y = Mathf.Max(0.01f, sunbeamBoxSize.y);
        sunbeamBoxSize.z = Mathf.Max(0.01f, sunbeamBoxSize.z);

        counterAreaSize.x = Mathf.Max(0.01f, counterAreaSize.x);
        counterAreaSize.y = Mathf.Max(0.01f, counterAreaSize.y);
        counterAreaSize.z = Mathf.Max(0.01f, counterAreaSize.z);

        intensity = Mathf.Max(0f, intensity);
        collisionRadiusScale = Mathf.Max(0.01f, collisionRadiusScale);
        maxCollisionShapes = Mathf.Clamp(maxCollisionShapes, 1, 256);
        maxKillSpeed = Mathf.Max(minKillSpeed, maxKillSpeed);

        if (!rebuildOnValidate)
            return;

        QueueEditorRebuild();
    }

    private void QueueEditorRebuild()
    {
        if (Application.isPlaying)
        {
            BuildEffect();
            return;
        }

        if (_queuedEditorRebuild)
            return;

        _queuedEditorRebuild = true;

        EditorApplication.delayCall += () =>
        {
            if (this == null)
                return;

            _queuedEditorRebuild = false;
            BuildEffect();
        };
    }
#endif

    [ContextMenu("Rebuild Calm Kitchen Particles")]
    public void BuildEffect()
    {
        ConfigureAmbientDust(GetOrCreateParticleSystem(AmbientName));
        ConfigureSunbeamDust(GetOrCreateParticleSystem(SunbeamName));
        ConfigureGlowParticles(GetOrCreateParticleSystem(GlowName));
        ConfigureCounterShimmer(GetOrCreateParticleSystem(CounterName));

        DeleteGeneratedSteamWisps();
    }

    [ContextMenu("Delete Old Steam Wisp Children")]
    public void DeleteGeneratedSteamWisps()
    {
        List<GameObject> toDelete = new List<GameObject>();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);

            if (child.name.StartsWith(SteamPrefix))
                toDelete.Add(child.gameObject);
        }

        foreach (GameObject go in toDelete)
            DestroySafe(go);
    }

    [ContextMenu("Clear Generated Particle Children")]
    public void ClearGeneratedChildren()
    {
        List<GameObject> toDelete = new List<GameObject>();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);

            if (child.name.StartsWith(Prefix))
                toDelete.Add(child.gameObject);
        }

        foreach (GameObject go in toDelete)
            DestroySafe(go);
    }

    private ParticleSystem GetOrCreateParticleSystem(string childName)
    {
        Transform child = transform.Find(childName);

        if (child == null)
        {
            GameObject go = new GameObject(childName);

            // Only parent the object. Existing child poses are never overwritten.
            go.transform.SetParent(transform, false);

            child = go.transform;
        }

        ParticleSystem ps = child.GetComponent<ParticleSystem>();
        if (ps == null)
            ps = child.gameObject.AddComponent<ParticleSystem>();

        ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
            renderer = child.gameObject.AddComponent<ParticleSystemRenderer>();

        ConfigureRenderer(renderer);

        return ps;
    }

    private void ConfigureRenderer(ParticleSystemRenderer renderer)
    {
        renderer.sharedMaterial = GetParticleMaterial();

        if (useMeshParticles)
        {
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = GetParticleMesh();
            renderer.enableGPUInstancing = enableMeshGPUInstancing;
        }
        else
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        renderer.alignment = particleRenderAlignment;

        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.allowRoll = true;
        renderer.sortingFudge = 1f;
        renderer.minParticleSize = 0.0001f;
        renderer.maxParticleSize = 0.45f;
    }

    private void ConfigureCollision(ParticleSystem ps)
    {
        var collision = ps.collision;
        collision.enabled = enableParticleCollision;

        if (!enableParticleCollision)
            return;

        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;

        collision.dampen = new ParticleSystem.MinMaxCurve(collisionDampen);
        collision.bounce = new ParticleSystem.MinMaxCurve(collisionBounce);
        collision.lifetimeLoss = new ParticleSystem.MinMaxCurve(collisionLifetimeLoss);

        collision.minKillSpeed = minKillSpeed;
        collision.maxKillSpeed = maxKillSpeed;
        collision.radiusScale = collisionRadiusScale;

        collision.quality = collisionQuality;
        collision.collidesWith = particleCollisionLayers;
        collision.maxCollisionShapes = maxCollisionShapes;
        collision.enableDynamicColliders = enableDynamicColliders;

        collision.colliderForce = colliderForce;
        collision.multiplyColliderForceByCollisionAngle = multiplyColliderForceByCollisionAngle;
        collision.multiplyColliderForceByParticleSpeed = multiplyColliderForceByParticleSpeed;
        collision.multiplyColliderForceByParticleSize = multiplyColliderForceByParticleSize;
        collision.sendCollisionMessages = sendCollisionMessages;
    }

    private Mesh GetParticleMesh()
    {
        if (particleMeshOverride != null)
            return particleMeshOverride;

        if (_generatedCubeMesh != null)
            return _generatedCubeMesh;

        _generatedCubeMesh = CreateCubeMesh();
        _generatedCubeMesh.name = "Generated Calm Kitchen Particle Cube Mesh";
        _generatedCubeMesh.hideFlags = HideFlags.HideAndDontSave;

        return _generatedCubeMesh;
    }

    private Mesh CreateCubeMesh()
    {
        Mesh mesh = new Mesh();

        Vector3[] vertices =
        {
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3( 0.5f, -0.5f, -0.5f),
            new Vector3( 0.5f,  0.5f, -0.5f),
            new Vector3(-0.5f,  0.5f, -0.5f),

            new Vector3(-0.5f, -0.5f,  0.5f),
            new Vector3( 0.5f, -0.5f,  0.5f),
            new Vector3( 0.5f,  0.5f,  0.5f),
            new Vector3(-0.5f,  0.5f,  0.5f)
        };

        int[] triangles =
        {
            0, 2, 1, 0, 3, 2,
            5, 6, 4, 6, 7, 4,
            3, 7, 2, 7, 6, 2,
            0, 1, 4, 1, 5, 4,
            1, 2, 5, 2, 6, 5,
            4, 7, 0, 7, 3, 0
        };

        Vector2[] uvs =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),

            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private Material GetParticleMaterial()
    {
        if (!useUnityDefaultParticleMaterial && particleMaterialOverride != null)
            return particleMaterialOverride;

        if (useUnityDefaultParticleMaterial)
        {
            Material unityDefault = GetUnityDefaultParticleSystemMaterial();

            if (unityDefault != null)
                return unityDefault;
        }

        return GetRuntimeFallbackSoftParticleMaterial();
    }

    private Material GetUnityDefaultParticleSystemMaterial()
    {
        if (_cachedUnityDefaultParticleMaterial != null)
            return _cachedUnityDefaultParticleMaterial;

        _cachedUnityDefaultParticleMaterial =
            Resources.GetBuiltinResource<Material>("Default-ParticleSystem.mat");

#if UNITY_EDITOR
        if (_cachedUnityDefaultParticleMaterial == null)
        {
            _cachedUnityDefaultParticleMaterial =
                AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
        }
#endif

        return _cachedUnityDefaultParticleMaterial;
    }

    private Material GetRuntimeFallbackSoftParticleMaterial()
    {
        if (_runtimeFallbackMaterial != null)
            return _runtimeFallbackMaterial;

        Shader shader =
            Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
            Shader.Find("Particles/Standard Unlit") ??
            Shader.Find("Sprites/Default");

        _runtimeFallbackMaterial = new Material(shader);
        _runtimeFallbackMaterial.name = "Runtime Calm Kitchen Fallback Particle Material";
        _runtimeFallbackMaterial.hideFlags = HideFlags.HideAndDontSave;

        _runtimeFallbackTexture = CreateSoftCircleTexture(96);
        _runtimeFallbackTexture.hideFlags = HideFlags.HideAndDontSave;

        if (_runtimeFallbackMaterial.HasProperty("_BaseMap"))
            _runtimeFallbackMaterial.SetTexture("_BaseMap", _runtimeFallbackTexture);

        if (_runtimeFallbackMaterial.HasProperty("_MainTex"))
            _runtimeFallbackMaterial.SetTexture("_MainTex", _runtimeFallbackTexture);

        if (_runtimeFallbackMaterial.HasProperty("_BaseColor"))
            _runtimeFallbackMaterial.SetColor("_BaseColor", Color.white);

        if (_runtimeFallbackMaterial.HasProperty("_Color"))
            _runtimeFallbackMaterial.SetColor("_Color", Color.white);

        _runtimeFallbackMaterial.renderQueue = 3000;

        return _runtimeFallbackMaterial;
    }

    private Texture2D CreateSoftCircleTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Runtime Soft Particle Circle";

        float center = (size - 1) * 0.5f;
        float radius = center;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / radius;
                float dy = (y - center) / radius;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = Mathf.Clamp01(1f - distance);
                alpha = Mathf.Pow(alpha, 2.6f);

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.Apply();

        return texture;
    }

    private void ConfigureAmbientDust(ParticleSystem ps)
    {
        ResetParticleSystem(ps);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.duration = 60f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.Max(1, Mathf.RoundToInt(520 * intensity));
        main.startLifetime = new ParticleSystem.MinMaxCurve(12f, 24f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.005f, 0.035f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.022f);
        main.startColor = new ParticleSystem.MinMaxGradient(ambientDustColorA, ambientDustColorB);
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(120.3f * intensity);
        emission.rateOverDistance = new ParticleSystem.MinMaxCurve(0f);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = GetOptionalShapeOffset(roomCenterOffset);
        shape.rotation = Vector3.zero;
        shape.scale = roomSize;
        shape.randomDirectionAmount = 0.15f;
        shape.sphericalDirectionAmount = 0f;
        shape.randomPositionAmount = 0f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.012f, 0.012f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.002f, 0.018f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.012f, 0.012f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.High;
        noise.strength = new ParticleSystem.MinMaxCurve(0.035f);
        noise.frequency = 0.18f;
        noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.025f);
        noise.damping = true;

        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(CreateAlphaGradient(ambientDustColorA, 0.22f));

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.15f, 1f),
                new Keyframe(0.85f, 1f),
                new Keyframe(1f, 0f)
            )
        );

        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

        ConfigureCollision(ps);
        ConfigureRenderer(ps.GetComponent<ParticleSystemRenderer>());

        PlayIfAllowed(ps);
    }

    private void ConfigureSunbeamDust(ParticleSystem ps)
    {
        ResetParticleSystem(ps);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.duration = 45f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.Max(1, Mathf.RoundToInt(180 * intensity));
        main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 16f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.008f, 0.035f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.028f);
        main.startColor = new ParticleSystem.MinMaxGradient(sunbeamDustColor);
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(12f * intensity);
        emission.rateOverDistance = new ParticleSystem.MinMaxCurve(0f);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = GetOptionalShapeOffset(sunbeamLocalPosition);
        shape.rotation = useShapeOffsetsInsteadOfTransformPose ? sunbeamLocalEuler : Vector3.zero;
        shape.scale = sunbeamBoxSize;
        shape.randomDirectionAmount = 0.08f;
        shape.sphericalDirectionAmount = 0f;
        shape.randomPositionAmount = 0f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0.012f, 0.04f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.005f, 0.018f);
        velocity.z = new ParticleSystem.MinMaxCurve(0.005f, 0.025f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.High;
        noise.strength = new ParticleSystem.MinMaxCurve(0.025f);
        noise.frequency = 0.12f;
        noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.015f);
        noise.damping = true;

        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(CreateAlphaGradient(sunbeamDustColor, 0.26f));

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.2f, 1f),
                new Keyframe(0.8f, 0.75f),
                new Keyframe(1f, 0f)
            )
        );

        ConfigureCollision(ps);
        ConfigureRenderer(ps.GetComponent<ParticleSystemRenderer>());

        PlayIfAllowed(ps);
    }

    private void ConfigureGlowParticles(ParticleSystem ps)
    {
        ResetParticleSystem(ps);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.duration = 70f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.Max(1, Mathf.RoundToInt(24 * intensity));
        main.startLifetime = new ParticleSystem.MinMaxCurve(18f, 36f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.004f, 0.018f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.36f);
        main.startColor = new ParticleSystem.MinMaxGradient(slowGlowColor);
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(0.32f * intensity);
        emission.rateOverDistance = new ParticleSystem.MinMaxCurve(0f);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = GetOptionalShapeOffset(roomCenterOffset);
        shape.rotation = Vector3.zero;
        shape.scale = roomSize;
        shape.randomDirectionAmount = 0f;
        shape.sphericalDirectionAmount = 0f;
        shape.randomPositionAmount = 0f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.006f, 0.006f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.002f, 0.012f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.006f, 0.006f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.High;
        noise.strength = new ParticleSystem.MinMaxCurve(0.02f);
        noise.frequency = 0.1f;
        noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.01f);
        noise.damping = true;

        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(CreateAlphaGradient(slowGlowColor, 0.045f));

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.25f, 1f),
                new Keyframe(0.65f, 0.85f),
                new Keyframe(1f, 0f)
            )
        );

        ConfigureCollision(ps);
        ConfigureRenderer(ps.GetComponent<ParticleSystemRenderer>());

        PlayIfAllowed(ps);
    }

    private void ConfigureCounterShimmer(ParticleSystem ps)
    {
        ResetParticleSystem(ps);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.duration = 40f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.Max(1, Mathf.RoundToInt(32 * intensity));
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.012f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.055f);
        main.startColor = new ParticleSystem.MinMaxGradient(counterShimmerColor);
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(21.99f * intensity);
        emission.rateOverDistance = new ParticleSystem.MinMaxCurve(0f);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = GetOptionalShapeOffset(counterAreaLocalPosition);
        shape.rotation = Vector3.zero;
        shape.scale = counterAreaSize;
        shape.randomDirectionAmount = 0.08f;
        shape.sphericalDirectionAmount = 0f;
        shape.randomPositionAmount = 0f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.01f, 0.01f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.002f, 0.018f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.01f, 0.01f);

        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(CreateAlphaGradient(counterShimmerColor, 0.24f));

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.35f, 1f),
                new Keyframe(0.7f, 0.65f),
                new Keyframe(1f, 0f)
            )
        );

        ConfigureCollision(ps);
        ConfigureRenderer(ps.GetComponent<ParticleSystemRenderer>());

        PlayIfAllowed(ps);
    }

    private void ResetParticleSystem(ParticleSystem ps)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.useAutoRandomSeed = false;
        ps.randomSeed = unchecked((uint)ps.name.GetHashCode());
    }

    private void PlayIfAllowed(ParticleSystem ps)
    {
        if (Application.isPlaying || playInEditMode)
            ps.Play();
    }

    private Vector3 GetOptionalShapeOffset(Vector3 offset)
    {
        return useShapeOffsetsInsteadOfTransformPose ? offset : Vector3.zero;
    }

    private Gradient CreateAlphaGradient(Color baseColor, float maxAlpha)
    {
        Gradient gradient = new Gradient();

        Color c = baseColor;
        c.a = 1f;

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(c, 0f),
                new GradientColorKey(c, 0.5f),
                new GradientColorKey(c, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(maxAlpha, 0.18f),
                new GradientAlphaKey(maxAlpha * 0.7f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        return gradient;
    }

    private void DestroySafe(GameObject go)
    {
        if (go == null)
            return;

        if (Application.isPlaying)
            Destroy(go);
        else
            DestroyImmediate(go);
    }
}