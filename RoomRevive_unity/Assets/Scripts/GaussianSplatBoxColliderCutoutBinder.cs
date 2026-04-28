using System;
using GaussianSplatting.Runtime;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class GaussianSplatBoxColliderCutoutBinder : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Automatically uses SplatManager.Instance.MainSplat as the target GaussianSplatRenderer.")]
    public bool useSplatManagerMainSplat = true;

    [Tooltip("The SplatManager used to find MainSplat. This is auto-filled from SplatManager.Instance / scene search.")]
    public SplatManager splatManager;

    [Tooltip("Auto-filled from SplatManager.MainSplat when useSplatManagerMainSplat is true.")]
    public GaussianSplatRenderer splatRenderer;

    [Tooltip("The BoxCollider on this same GameObject. This is auto-filled in OnValidate.")]
    public BoxCollider sourceBoxCollider;

    [Header("Generated Cutout")]
    public string generatedCutoutName = "__Generated_Gaussian_BoxCollider_Cutout";
    public GaussianCutout generatedCutout;

    [Tooltip("Creates a GaussianCutout GameObject automatically if none is assigned.")]
    public bool createCutoutIfMissing = true;

    [Tooltip("Default ON. The generated cutout becomes a child of the BoxCollider object.")]
    public bool parentCutoutToBoxCollider = true;

    [Tooltip("Keep other cutouts already assigned on the GaussianSplatRenderer.")]
    public bool preserveExistingCutouts = true;

    [Tooltip("Default ON.")]
    public bool invertCutout = true;

    [Header("Cutout Border / Padding")]
    [Range(0f, 0.001f)]
    [Tooltip("Extra border around the BoxCollider. Added on every side. Default is 0.001.")]
    public float cutoutBorder = 0.001f;

    [Header("Enable / Disable Behaviour")]
    [Tooltip("When this component or GameObject is disabled, the generated cutout is removed from the renderer's m_Cutouts array.")]
    public bool removeCutoutFromRendererWhenDisabled = true;

    [Tooltip("When this component or GameObject is disabled, the generated GaussianCutout component is also disabled.")]
    public bool disableCutoutComponentWhenThisIsDisabled = true;

    [Tooltip("When this component or GameObject is enabled again, the generated GaussianCutout component is enabled again.")]
    public bool enableCutoutComponentWhenThisIsEnabled = true;

    [Header("Sync")]
    public bool syncOnEnable = true;
    public bool syncEveryFrame = true;
    public bool syncInEditMode = true;

#if UNITY_EDITOR
    private bool _editorSyncQueued;
#endif

    private void Reset()
    {
        ApplyDefaultSettings();
        ResolveReferences();
        SyncCutout();
    }

    [ContextMenu("Apply Default Settings")]
    public void ApplyDefaultSettings()
    {
        useSplatManagerMainSplat = true;

        createCutoutIfMissing = true;
        parentCutoutToBoxCollider = true;
        preserveExistingCutouts = true;
        invertCutout = true;

        cutoutBorder = 0.001f;

        removeCutoutFromRendererWhenDisabled = true;
        disableCutoutComponentWhenThisIsDisabled = true;
        enableCutoutComponentWhenThisIsEnabled = true;

        syncOnEnable = true;
        syncEveryFrame = true;
        syncInEditMode = true;

        ResolveReferences();
        MarkDirty();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (enableCutoutComponentWhenThisIsEnabled)
            SetGeneratedCutoutEnabled(true);

        if (syncOnEnable)
            SyncCutout();
    }

    private void OnDisable()
    {
        ResolveReferences();

        if (removeCutoutFromRendererWhenDisabled)
            RemoveGeneratedCutoutFromRenderer(false);

        if (disableCutoutComponentWhenThisIsDisabled)
            SetGeneratedCutoutEnabled(false);
    }

    private void LateUpdate()
    {
        if (!syncEveryFrame)
            return;

        if (!Application.isPlaying && !syncInEditMode)
            return;

        SyncCutout();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        cutoutBorder = Mathf.Clamp(cutoutBorder, 0f, 0.001f);

        ResolveReferences();

        if (!syncInEditMode)
            return;

        QueueEditorSync();
#endif
    }

#if UNITY_EDITOR
    private void QueueEditorSync()
    {
        if (_editorSyncQueued)
            return;

        _editorSyncQueued = true;

        EditorApplication.delayCall += () =>
        {
            _editorSyncQueued = false;

            if (this == null)
                return;

            ResolveReferences();

            if (!isActiveAndEnabled)
            {
                if (removeCutoutFromRendererWhenDisabled)
                    RemoveGeneratedCutoutFromRenderer(false);

                if (disableCutoutComponentWhenThisIsDisabled)
                    SetGeneratedCutoutEnabled(false);

                return;
            }

            SyncCutout();
        };
    }
#endif

    [ContextMenu("Create / Sync Cutout From Local BoxCollider")]
    public void SyncCutout()
    {
        ResolveReferences();

        if (splatRenderer == null)
        {
            Debug.LogWarning(
                $"{nameof(GaussianSplatBoxColliderCutoutBinder)}: Missing GaussianSplatRenderer. Make sure SplatManager has MainSplat assigned.",
                this
            );
            return;
        }

        if (sourceBoxCollider == null)
        {
            Debug.LogWarning(
                $"{nameof(GaussianSplatBoxColliderCutoutBinder)}: Missing BoxCollider on this GameObject.",
                this
            );
            return;
        }

        if (!EnsureCutoutExists())
            return;

        MatchCutoutToBoxCollider();
        AssignCutoutToRenderer();

        if (isActiveAndEnabled && enableCutoutComponentWhenThisIsEnabled)
            SetGeneratedCutoutEnabled(true);

        MarkDirty();
    }

    [ContextMenu("Disable Generated Cutout")]
    public void DisableGeneratedCutout()
    {
        SetGeneratedCutoutEnabled(false);
    }

    [ContextMenu("Enable Generated Cutout")]
    public void EnableGeneratedCutout()
    {
        SetGeneratedCutoutEnabled(true);
    }

    [ContextMenu("Remove Generated Cutout From Renderer")]
    public void RemoveGeneratedCutoutFromRendererContext()
    {
        RemoveGeneratedCutoutFromRenderer(true);
    }

    public void RemoveGeneratedCutoutFromRenderer(bool markDirty)
    {
        ResolveReferences();

        if (splatRenderer == null || generatedCutout == null || splatRenderer.m_Cutouts == null)
            return;

        GaussianCutout[] oldCutouts = splatRenderer.m_Cutouts;
        int count = 0;

        for (int i = 0; i < oldCutouts.Length; i++)
        {
            if (oldCutouts[i] != null && oldCutouts[i] != generatedCutout)
                count++;
        }

        GaussianCutout[] newCutouts = new GaussianCutout[count];
        int index = 0;

        for (int i = 0; i < oldCutouts.Length; i++)
        {
            if (oldCutouts[i] != null && oldCutouts[i] != generatedCutout)
            {
                newCutouts[index] = oldCutouts[i];
                index++;
            }
        }

        splatRenderer.m_Cutouts = newCutouts;

        if (markDirty)
            MarkDirty();
    }

    private void ResolveReferences()
    {
        sourceBoxCollider = GetComponent<BoxCollider>();

        if (useSplatManagerMainSplat)
        {
            if (splatManager == null)
                splatManager = SplatManager.GetOrFindInstance();

            if (splatManager != null)
                splatRenderer = splatManager.MainSplat;
        }

        if (generatedCutout == null)
            generatedCutout = FindExistingGeneratedCutout();
    }

    private bool EnsureCutoutExists()
    {
        if (generatedCutout != null)
            return true;

        generatedCutout = FindExistingGeneratedCutout();

        if (generatedCutout != null)
            return true;

        if (!createCutoutIfMissing)
            return false;

        GameObject cutoutGO = new GameObject(generatedCutoutName);

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Undo.RegisterCreatedObjectUndo(cutoutGO, "Create Gaussian Splat Cutout");
#endif

        generatedCutout = cutoutGO.AddComponent<GaussianCutout>();
        return generatedCutout != null;
    }

    private GaussianCutout FindExistingGeneratedCutout()
    {
        GaussianCutout[] childCutouts = GetComponentsInChildren<GaussianCutout>(true);

        for (int i = 0; i < childCutouts.Length; i++)
        {
            if (childCutouts[i] != null && childCutouts[i].name == generatedCutoutName)
                return childCutouts[i];
        }

        return null;
    }

    private void MatchCutoutToBoxCollider()
    {
        Transform cutoutTransform = generatedCutout.transform;

        generatedCutout.name = generatedCutoutName;
        generatedCutout.m_Type = GaussianCutout.Type.Box;
        generatedCutout.m_Invert = invertCutout;

        if (parentCutoutToBoxCollider)
            MatchCutoutAsChild(cutoutTransform);
        else
            MatchCutoutInWorldSpace(cutoutTransform);
    }

    private void MatchCutoutAsChild(Transform cutoutTransform)
    {
        Transform colliderTransform = sourceBoxCollider.transform;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Undo.SetTransformParent(cutoutTransform, colliderTransform, "Parent Gaussian Cutout To BoxCollider");
        else
            cutoutTransform.SetParent(colliderTransform, false);
#else
        cutoutTransform.SetParent(colliderTransform, false);
#endif

        cutoutTransform.localPosition = sourceBoxCollider.center;
        cutoutTransform.localRotation = Quaternion.identity;

        Vector3 paddedLocalSize = GetPaddedColliderLocalSize();

        // GaussianCutout box volume expects half-size scale.
        cutoutTransform.localScale = HalfAbs(paddedLocalSize);
    }

    private void MatchCutoutInWorldSpace(Transform cutoutTransform)
    {
        Transform colliderTransform = sourceBoxCollider.transform;

        cutoutTransform.SetParent(null, true);

        cutoutTransform.position = colliderTransform.TransformPoint(sourceBoxCollider.center);
        cutoutTransform.rotation = colliderTransform.rotation;

        Vector3 worldSize = GetColliderWorldSize();

        Vector3 paddedWorldSize = new Vector3(
            worldSize.x + cutoutBorder * 2f,
            worldSize.y + cutoutBorder * 2f,
            worldSize.z + cutoutBorder * 2f
        );

        // GaussianCutout box volume expects half-size scale.
        cutoutTransform.localScale = HalfAbs(paddedWorldSize);
    }

    private Vector3 GetPaddedColliderLocalSize()
    {
        float paddingTotal = cutoutBorder * 2f;
        Vector3 size = sourceBoxCollider.size;

        return new Vector3(
            Mathf.Max(size.x + paddingTotal, 0.0001f),
            Mathf.Max(size.y + paddingTotal, 0.0001f),
            Mathf.Max(size.z + paddingTotal, 0.0001f)
        );
    }

    private Vector3 GetColliderWorldSize()
    {
        Transform colliderTransform = sourceBoxCollider.transform;
        Vector3 localSize = sourceBoxCollider.size;
        Vector3 lossyScale = colliderTransform.lossyScale;

        return new Vector3(
            Mathf.Abs(localSize.x * lossyScale.x),
            Mathf.Abs(localSize.y * lossyScale.y),
            Mathf.Abs(localSize.z * lossyScale.z)
        );
    }

    private void AssignCutoutToRenderer()
    {
        if (splatRenderer == null || generatedCutout == null)
            return;

        if (!preserveExistingCutouts)
        {
            splatRenderer.m_Cutouts = new[] { generatedCutout };
            return;
        }

        GaussianCutout[] current = splatRenderer.m_Cutouts;

        if (current == null || current.Length == 0)
        {
            splatRenderer.m_Cutouts = new[] { generatedCutout };
            return;
        }

        for (int i = 0; i < current.Length; i++)
        {
            if (current[i] == generatedCutout)
                return;
        }

        for (int i = 0; i < current.Length; i++)
        {
            if (current[i] == null)
            {
                current[i] = generatedCutout;
                splatRenderer.m_Cutouts = current;
                return;
            }
        }

        Array.Resize(ref current, current.Length + 1);
        current[current.Length - 1] = generatedCutout;
        splatRenderer.m_Cutouts = current;
    }

    private void SetGeneratedCutoutEnabled(bool enabled)
    {
        if (generatedCutout == null)
            return;

        generatedCutout.enabled = enabled;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorUtility.SetDirty(generatedCutout);
#endif
    }

    private static Vector3 HalfAbs(Vector3 size)
    {
        return ClampTiny(new Vector3(
            Mathf.Abs(size.x) * 0.5f,
            Mathf.Abs(size.y) * 0.5f,
            Mathf.Abs(size.z) * 0.5f
        ));
    }

    private static Vector3 ClampTiny(Vector3 value)
    {
        const float min = 0.0001f;

        return new Vector3(
            Mathf.Max(value.x, min),
            Mathf.Max(value.y, min),
            Mathf.Max(value.z, min)
        );
    }

    private void MarkDirty()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (generatedCutout != null)
                EditorUtility.SetDirty(generatedCutout);

            if (splatRenderer != null)
                EditorUtility.SetDirty(splatRenderer);

            if (splatManager != null)
                EditorUtility.SetDirty(splatManager);

            EditorUtility.SetDirty(this);   
        }
#endif
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (sourceBoxCollider == null)
            sourceBoxCollider = GetComponent<BoxCollider>();

        if (sourceBoxCollider == null)
            return;

        Vector3 localPaddedSize = GetPaddedColliderLocalSize();

        Gizmos.matrix = sourceBoxCollider.transform.localToWorldMatrix;

        // Original BoxCollider.
        Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
        Gizmos.DrawWireCube(sourceBoxCollider.center, sourceBoxCollider.size);

        // Actual padded cutout.
        Gizmos.color = new Color(1f, 0f, 1f, 0.55f);
        Gizmos.DrawWireCube(sourceBoxCollider.center, localPaddedSize);
    }
#endif
}