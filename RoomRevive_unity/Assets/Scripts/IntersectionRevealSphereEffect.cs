using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(SphereCollider))]
public class IntersectionRevealSphereEffect : MonoBehaviour
{
    private const string ShaderName = "XRCC/Intersection Reveal Sphere URP";
    private const string GeneratedMeshName = "__XRCC_IntersectionRevealSphere_Mesh";
    private const string GeneratedMaterialName = "__XRCC_IntersectionRevealSphere_Material";

    [Header("Auto Setup")]
    public bool initializeInOnValidate = true;
    public bool forceCameraDepthTexture = true;
    public bool addKinematicRigidbodyForTriggerEvents = true;

    [Header("Sphere")]
    [Tooltip("Unity standard sphere size. Radius 0.5 means scale 1 = diameter 1 meter.")]
    [Min(0.01f)] public float sphereMeshRadius = 0.5f;

    [Tooltip("How detailed the generated sphere mesh should be.")]
    [Range(16, 128)] public int sphereSegments = 64;

    [Range(8, 64)] public int sphereRings = 32;

    [Header("Intersection Look")]
    public Color intersectionColor = new Color(0.45f, 0.95f, 1f, 1f);

    [Tooltip("How thick the visible contact band is in world/depth units.")]
    [Range(0.001f, 2f)] public float intersectionThickness = 0.16f;

    [Tooltip("Soft fade at the edge of the contact band.")]
    [Range(0.001f, 2f)] public float edgeSoftness = 0.08f;

    [Tooltip("Overall brightness/opacity of the intersection effect.")]
    [Range(0f, 5f)] public float intensity = 1.6f;

    [Header("Optional Empty Sphere Shell")]
    [Tooltip("Keep this at 0 if you only want the surface visible where it touches/collides with the room.")]
    [Range(0f, 1f)] public float invisibleShellAlpha = 0f;

    [Tooltip("Adds a soft fresnel glow to the sphere shell if invisibleShellAlpha is above 0.")]
    [Range(0.1f, 8f)] public float fresnelPower = 3f;

    [Header("Animated Energy")]
    [Range(0f, 10f)] public float pulseSpeed = 1.5f;
    [Range(0f, 2f)] public float pulseStrength = 0.18f;
    [Range(0f, 20f)] public float noiseScale = 6f;
    [Range(0f, 1f)] public float noiseStrength = 0.25f;

    [Header("Collision / Trigger")]
    public bool useTriggerCollider = true;
    public LayerMask collisionLayers = ~0;

    [Tooltip("Runtime list of colliders currently inside the sphere trigger.")]
    [SerializeField] private List<Collider> currentOverlaps = new List<Collider>();

    [Header("Debug")]
    public bool showGizmo = true;
    public Color gizmoColor = new Color(0.45f, 0.95f, 1f, 0.2f);

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private SphereCollider sphereCollider;
    private Rigidbody sphereRigidbody;

    [SerializeField, HideInInspector] private Mesh generatedMesh;
    [SerializeField, HideInInspector] private Material generatedMaterial;

    public IReadOnlyList<Collider> CurrentOverlaps => currentOverlaps;

    private void Reset()
    {
        Initialize();
        ApplySettings();
    }

    private void Awake()
    {
        Initialize();
        ApplySettings();
    }

    private void OnEnable()
    {
        Initialize();
        ApplySettings();
    }

    private void Update()
    {
        ApplyMaterialSettings();

        if (forceCameraDepthTexture)
            EnsureCameraDepthTextures();
    }

    private void OnValidate()
    {
        if (!initializeInOnValidate)
            return;

        sphereMeshRadius = Mathf.Max(0.01f, sphereMeshRadius);
        sphereSegments = Mathf.Clamp(sphereSegments, 16, 128);
        sphereRings = Mathf.Clamp(sphereRings, 8, 64);

        Initialize();
        ApplySettings();

#if UNITY_EDITOR
        SceneView.RepaintAll();
#endif
    }

    private void Initialize()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        sphereCollider = GetComponent<SphereCollider>();

        if (generatedMesh == null || generatedMesh.name != GeneratedMeshName)
        {
            generatedMesh = CreateSphereMesh(sphereMeshRadius, sphereSegments, sphereRings);
            generatedMesh.name = GeneratedMeshName;
            generatedMesh.hideFlags = HideFlags.DontSaveInBuild;
        }

        if (meshFilter.sharedMesh != generatedMesh)
            meshFilter.sharedMesh = generatedMesh;

        Shader shader = Shader.Find(ShaderName);

        if (generatedMaterial == null)
        {
            generatedMaterial = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit"));
            generatedMaterial.name = GeneratedMaterialName;
            generatedMaterial.hideFlags = HideFlags.DontSaveInBuild;
        }

        if (shader != null && generatedMaterial.shader != shader)
            generatedMaterial.shader = shader;

        if (meshRenderer.sharedMaterial != generatedMaterial)
            meshRenderer.sharedMaterial = generatedMaterial;

        if (addKinematicRigidbodyForTriggerEvents)
        {
            sphereRigidbody = GetComponent<Rigidbody>();

            if (sphereRigidbody == null)
                sphereRigidbody = gameObject.AddComponent<Rigidbody>();

            sphereRigidbody.isKinematic = true;
            sphereRigidbody.useGravity = false;
            sphereRigidbody.interpolation = RigidbodyInterpolation.None;
            sphereRigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }

        gameObject.name = string.IsNullOrWhiteSpace(gameObject.name)
            ? "Intersection Reveal Sphere"
            : gameObject.name;
    }

    private void ApplySettings()
    {
        ApplyColliderSettings();
        ApplyRendererSettings();
        ApplyMaterialSettings();

        if (forceCameraDepthTexture)
            EnsureCameraDepthTextures();
    }

    private void ApplyColliderSettings()
    {
        if (sphereCollider == null)
            return;

        sphereCollider.radius = sphereMeshRadius;
        sphereCollider.center = Vector3.zero;
        sphereCollider.isTrigger = useTriggerCollider;
    }

    private void ApplyRendererSettings()
    {
        if (meshRenderer == null)
            return;

        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.allowOcclusionWhenDynamic = false;
        meshRenderer.enabled = true;
    }

    private void ApplyMaterialSettings()
    {
        if (generatedMaterial == null)
            return;

        generatedMaterial.SetColor("_IntersectionColor", intersectionColor);
        generatedMaterial.SetFloat("_IntersectionThickness", Mathf.Max(0.001f, intersectionThickness));
        generatedMaterial.SetFloat("_EdgeSoftness", Mathf.Max(0.001f, edgeSoftness));
        generatedMaterial.SetFloat("_Intensity", intensity);

        generatedMaterial.SetFloat("_ShellAlpha", invisibleShellAlpha);
        generatedMaterial.SetFloat("_FresnelPower", fresnelPower);

        generatedMaterial.SetFloat("_PulseSpeed", pulseSpeed);
        generatedMaterial.SetFloat("_PulseStrength", pulseStrength);
        generatedMaterial.SetFloat("_NoiseScale", noiseScale);
        generatedMaterial.SetFloat("_NoiseStrength", noiseStrength);

        generatedMaterial.renderQueue = 3000;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsInCollisionLayer(other.gameObject.layer))
            return;

        if (!currentOverlaps.Contains(other))
            currentOverlaps.Add(other);
    }

    private void OnTriggerExit(Collider other)
    {
        currentOverlaps.Remove(other);
    }

    private bool IsInCollisionLayer(int layer)
    {
        return (collisionLayers.value & (1 << layer)) != 0;
    }

    private void EnsureCameraDepthTextures()
    {
        Camera[] cameras = Camera.allCameras;

        for (int i = 0; i < cameras.Length; i++)
            EnableDepthTextureOnCamera(cameras[i]);

#if UNITY_EDITOR
        SceneView[] sceneViews = SceneView.sceneViews.ToArray() as SceneView[];

        if (sceneViews != null)
        {
            foreach (SceneView view in sceneViews)
            {
                if (view != null && view.camera != null)
                    EnableDepthTextureOnCamera(view.camera);
            }
        }
#endif
    }

    private void EnableDepthTextureOnCamera(Camera cam)
    {
        if (cam == null)
            return;

        cam.depthTextureMode |= DepthTextureMode.Depth;

        Component urpCameraData = cam.GetComponent("UniversalAdditionalCameraData");

        if (urpCameraData != null)
        {
            Type type = urpCameraData.GetType();

            PropertyInfo requiresDepthTextureProperty = type.GetProperty(
                "requiresDepthTexture",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (requiresDepthTextureProperty != null && requiresDepthTextureProperty.CanWrite)
                requiresDepthTextureProperty.SetValue(urpCameraData, true);
        }
    }

    private static Mesh CreateSphereMesh(float radius, int segments, int rings)
    {
        Mesh mesh = new Mesh();
        mesh.name = GeneratedMeshName;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        for (int y = 0; y <= rings; y++)
        {
            float v = y / (float)rings;
            float phi = v * Mathf.PI;

            float sinPhi = Mathf.Sin(phi);
            float cosPhi = Mathf.Cos(phi);

            for (int x = 0; x <= segments; x++)
            {
                float u = x / (float)segments;
                float theta = u * Mathf.PI * 2f;

                float sinTheta = Mathf.Sin(theta);
                float cosTheta = Mathf.Cos(theta);

                Vector3 normal = new Vector3(
                    sinPhi * cosTheta,
                    cosPhi,
                    sinPhi * sinTheta
                ).normalized;

                vertices.Add(normal * radius);
                normals.Add(normal);
                uvs.Add(new Vector2(u, v));
            }
        }

        for (int y = 0; y < rings; y++)
        {
            for (int x = 0; x < segments; x++)
            {
                int a = y * (segments + 1) + x;
                int b = a + segments + 1;
                int c = a + 1;
                int d = b + 1;

                if (y != 0)
                {
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                }

                if (y != rings - 1)
                {
                    triangles.Add(c);
                    triangles.Add(d);
                    triangles.Add(b);
                }
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();

        return mesh;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmo)
            return;

        Gizmos.color = gizmoColor;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireSphere(Vector3.zero, sphereMeshRadius);
    }
}