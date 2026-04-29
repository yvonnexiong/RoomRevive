using UnityEngine;
using UDebug = UnityEngine.Debug;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Put this script on a root GameObject.
/// It assigns an MRUKLit material to all child Renderers.
///
/// Default shader:
/// Meta/MRUK/MixedReality/MRUKLit
///
/// Works with:
/// - MeshRenderer
/// - SkinnedMeshRenderer
/// - Any Renderer component on children
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class AssignMaterialToChildren : MonoBehaviour
{
    [Header("Material / Shader")]
    [Tooltip("Optional material to assign to every child Renderer. If empty, the script will create/find a material using the shader below.")]
    public Material materialToAssign;

    [Tooltip("Shader path used for the auto-created material.")]
    public string mrukLitShaderName = "Meta/MRUK/MixedReality/MRUKLit";

    [Tooltip("Name used for the auto-created material if no material is assigned.")]
    public string autoMaterialName = "Auto_MRUKLit_Children_Material";

    [Header("Material Slot Settings")]
    [Tooltip("If true, every material slot on each Renderer will be replaced.")]
    public bool replaceAllMaterialSlots = true;

    [Tooltip("If true, only the first material slot will be replaced.")]
    public bool assignToFirstSlotOnly = false;

    [Header("Search Settings")]
    [Tooltip("If true, inactive children will also be included.")]
    public bool includeInactiveChildren = true;

    [Tooltip("If true, the Renderer on this GameObject will also be included.")]
    public bool includeThisGameObject = false;

    [Header("Apply Settings")]
    [Tooltip("If true, this script applies automatically when values change in the Inspector.")]
    public bool applyOnValidate = true;

    [Tooltip("If true, this script applies automatically when the scene starts.")]
    public bool applyOnStart = true;

    [Header("Debug")]
    [SerializeField] private int processedRendererCount;
    [SerializeField] private int materialAssignedCount;
    [SerializeField] private int skippedRendererCount;

    [SerializeField, HideInInspector] private Material autoCreatedMaterial;

    private void Start()
    {
        if (applyOnStart)
        {
            ApplyMaterialToChildren();
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

        ApplyMaterialToChildren();
    }
#endif

    [ContextMenu("Apply Material To Children")]
    public void ApplyMaterialToChildren()
    {
        processedRendererCount = 0;
        materialAssignedCount = 0;
        skippedRendererCount = 0;

        Material resolvedMaterial = GetMaterialToAssign();

        if (resolvedMaterial == null)
        {
            UDebug.LogWarning(
                $"[{nameof(AssignMaterialToChildren)}] Could not assign material. Shader not found: {mrukLitShaderName}",
                this
            );

            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(includeInactiveChildren);

        foreach (Renderer childRenderer in renderers)
        {
            if (childRenderer == null)
                continue;

            if (!includeThisGameObject && childRenderer.gameObject == gameObject)
                continue;

            processedRendererCount++;

            AssignMaterial(childRenderer, resolvedMaterial);
            materialAssignedCount++;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(childRenderer);
                EditorUtility.SetDirty(childRenderer.gameObject);
            }
#endif
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
        }
#endif

        UDebug.Log(
            $"[{nameof(AssignMaterialToChildren)}] Assigned '{resolvedMaterial.name}' to {materialAssignedCount} child Renderer(s).",
            this
        );
    }

    [ContextMenu("Clear Debug Counts")]
    public void ClearDebugCounts()
    {
        processedRendererCount = 0;
        materialAssignedCount = 0;
        skippedRendererCount = 0;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
        }
#endif
    }

    private Material GetMaterialToAssign()
    {
        if (materialToAssign != null)
            return materialToAssign;

        Shader shader = Shader.Find(mrukLitShaderName);

        if (shader == null)
        {
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

    private void AssignMaterial(Renderer targetRenderer, Material material)
    {
        if (targetRenderer == null || material == null)
        {
            skippedRendererCount++;
            return;
        }

        Material[] sharedMaterials = targetRenderer.sharedMaterials;

        if (sharedMaterials == null || sharedMaterials.Length == 0)
        {
            targetRenderer.sharedMaterial = material;
            return;
        }

        if (replaceAllMaterialSlots && !assignToFirstSlotOnly)
        {
            for (int i = 0; i < sharedMaterials.Length; i++)
            {
                sharedMaterials[i] = material;
            }

            targetRenderer.sharedMaterials = sharedMaterials;
            return;
        }

        sharedMaterials[0] = material;
        targetRenderer.sharedMaterials = sharedMaterials;
    }
}