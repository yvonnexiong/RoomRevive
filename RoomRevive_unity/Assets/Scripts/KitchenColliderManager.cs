using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Put this script on the root kitchen GameObject, for example "YinanKitchen".
///
/// It searches through all child objects named "Visual_Cube".
/// For each Visual_Cube it can:
/// - Assign an MRUKLit material / shader
/// - Enable or disable the MeshRenderer using a public bool
///
/// Useful for MRUK / scene collider objects where the collider should remain active,
/// but the debug cube mesh can be shown or hidden from the inspector.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class KitchenColliderManager : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The GameObject name that should be processed.")]
    public string visualCubeObjectName = "Visual_Cube";

    [Header("Material / Shader")]
    [Tooltip("If true, every Visual_Cube will get the MRUKLit material/shader assigned.")]
    public bool assignMRUKLitMaterial = true;

    [Tooltip("Optional material to assign to every Visual_Cube. If empty, the script will create/find a material using the shader below.")]
    public Material visualCubeMaterial;

    [Tooltip("Shader path used for the Visual_Cube material.")]
    public string mrukLitShaderName = "Meta/MRUK/MixedReality/MRUKLit";

    [Tooltip("Name used for the auto-created material if no material is assigned.")]
    public string autoMaterialName = "Auto_MRUKLit_VisualCube_Material";

    [Header("Renderer Visibility")]
    [Tooltip("If true, all Visual_Cube MeshRenderers will be enabled. If false, they will be disabled.")]
    public bool visualCubeMeshRenderersEnabled = false;

    [Tooltip("If true, this script will apply the renderer enabled/disabled state to all Visual_Cube objects.")]
    public bool applyRendererVisibility = true;

    [Header("Search Settings")]
    [Tooltip("If true, inactive children will also be searched.")]
    public bool includeInactiveChildren = true;

    [Tooltip("If true, the script will automatically apply changes in the editor when values change.")]
    public bool applyOnValidate = true;

    [Tooltip("If true, the script also applies when the scene starts.")]
    public bool applyOnStart = true;

    [Header("Debug")]
    [SerializeField] private int processedVisualCubeCount;
    [SerializeField] private int enabledRendererCount;
    [SerializeField] private int disabledRendererCount;
    [SerializeField] private int materialAssignedCount;

    [SerializeField, HideInInspector] private Material autoCreatedMaterial;

    private void Start()
    {
        if (applyOnStart)
        {
            ApplyToVisualCubes();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!applyOnValidate)
            return;

        EditorApplication.delayCall -= DelayedValidate;
        EditorApplication.delayCall += DelayedValidate;
    }

    private void DelayedValidate()
    {
        if (this == null)
            return;

        ApplyToVisualCubes();
    }
#endif

    [ContextMenu("Apply To Visual Cubes")]
    public void ApplyToVisualCubes()
    {
        processedVisualCubeCount = 0;
        enabledRendererCount = 0;
        disabledRendererCount = 0;
        materialAssignedCount = 0;

        Transform[] allChildren = GetComponentsInChildren<Transform>(includeInactiveChildren);

        Material materialToAssign = null;

        if (assignMRUKLitMaterial)
        {
            materialToAssign = GetOrCreateMRUKLitMaterial();
        }

        foreach (Transform child in allChildren)
        {
            if (child == null)
                continue;

            if (child.gameObject == gameObject)
                continue;

            if (child.name != visualCubeObjectName)
                continue;

            processedVisualCubeCount++;

            MeshRenderer meshRenderer = child.GetComponent<MeshRenderer>();

            if (meshRenderer == null)
                continue;

            if (assignMRUKLitMaterial && materialToAssign != null)
            {
                AssignMaterial(meshRenderer, materialToAssign);
                materialAssignedCount++;
            }

            if (applyRendererVisibility)
            {
                meshRenderer.enabled = visualCubeMeshRenderersEnabled;

                if (visualCubeMeshRenderersEnabled)
                    enabledRendererCount++;
                else
                    disabledRendererCount++;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(meshRenderer);
                EditorUtility.SetDirty(child.gameObject);
            }
#endif
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
        }
#endif
    }

    [ContextMenu("Enable Visual Cube Mesh Renderers")]
    public void EnableVisualCubeRenderers()
    {
        visualCubeMeshRenderersEnabled = true;
        applyRendererVisibility = true;
        ApplyToVisualCubes();
    }

    [ContextMenu("Disable Visual Cube Mesh Renderers")]
    public void DisableVisualCubeRenderers()
    {
        visualCubeMeshRenderersEnabled = false;
        applyRendererVisibility = true;
        ApplyToVisualCubes();
    }

    [ContextMenu("Assign MRUKLit Material Only")]
    public void AssignMRUKLitMaterialOnly()
    {
        materialAssignedCount = 0;
        processedVisualCubeCount = 0;

        Material materialToAssign = GetOrCreateMRUKLitMaterial();

        if (materialToAssign == null)
        {
            Debug.LogWarning(
                $"[{nameof(KitchenColliderManager)}] Could not assign material because shader was not found: {mrukLitShaderName}",
                this
            );
            return;
        }

        Transform[] allChildren = GetComponentsInChildren<Transform>(includeInactiveChildren);

        foreach (Transform child in allChildren)
        {
            if (child == null)
                continue;

            if (child.gameObject == gameObject)
                continue;

            if (child.name != visualCubeObjectName)
                continue;

            processedVisualCubeCount++;

            MeshRenderer meshRenderer = child.GetComponent<MeshRenderer>();

            if (meshRenderer == null)
                continue;

            AssignMaterial(meshRenderer, materialToAssign);
            materialAssignedCount++;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(meshRenderer);
                EditorUtility.SetDirty(child.gameObject);
            }
#endif
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
        }
#endif
    }

    private void AssignMaterial(MeshRenderer meshRenderer, Material materialToAssign)
    {
        if (meshRenderer == null || materialToAssign == null)
            return;

        Material[] sharedMaterials = meshRenderer.sharedMaterials;

        if (sharedMaterials == null || sharedMaterials.Length == 0)
        {
            meshRenderer.sharedMaterial = materialToAssign;
            return;
        }

        for (int i = 0; i < sharedMaterials.Length; i++)
        {
            sharedMaterials[i] = materialToAssign;
        }

        meshRenderer.sharedMaterials = sharedMaterials;
    }

    private Material GetOrCreateMRUKLitMaterial()
    {
        if (visualCubeMaterial != null)
            return visualCubeMaterial;

        Shader shader = Shader.Find(mrukLitShaderName);

        if (shader == null)
        {
            Debug.LogWarning(
                $"[{nameof(KitchenColliderManager)}] Shader not found: {mrukLitShaderName}. Make sure MRUK is installed and the shader path is correct.",
                this
            );

            return null;
        }

        if (autoCreatedMaterial == null || autoCreatedMaterial.shader != shader)
        {
            autoCreatedMaterial = new Material(shader)
            {
                name = autoMaterialName
            };

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                autoCreatedMaterial.hideFlags = HideFlags.DontSaveInBuild;
                EditorUtility.SetDirty(this);
            }
#endif
        }

        return autoCreatedMaterial;
    }
}