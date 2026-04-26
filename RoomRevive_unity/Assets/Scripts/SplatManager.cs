using System;
using UnityEngine;
using UnityEngine.Events;
using GaussianSplatting.Runtime;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Manages switching between different Gaussian Splat rooms.
///
/// Put this script on an empty GameObject.
/// Then either:
///
/// MODE 1 — Swap Asset On Target Renderer:
///     Assign one active GaussianSplatRenderer to targetRenderer.
///     Assign calmRoomAsset, fastRoomAsset, and hostRoomAsset.
///     Calling SetCalmRoom / SetFastRoom / SetHostRoom swaps the asset on that renderer.
///
/// MODE 2 — Toggle Renderer Objects:
///     Assign three existing GaussianSplatRenderer objects:
///         calmRoomRenderer
///         fastRoomRenderer
///         hostRoomRenderer
///     Calling SetCalmRoom / SetFastRoom / SetHostRoom enables one and disables the others.
///
/// UI setup:
///     Your UI Card / Button / Meta Interactable UnityEvent can call:
///         SplatManager.SetCalmRoom()
///         SplatManager.SetFastRoom()
///         SplatManager.SetHostRoom()
/// </summary>
public class SplatManager : MonoBehaviour
{
    public enum SplatRoom
    {
        None,
        CalmRoom,
        FastRoom,
        HostRoom
    }

    public enum SplatSwitchMode
    {
        SwapAssetOnTargetRenderer,
        ToggleRendererObjects
    }

    [Header("Switch Mode")]
    [Tooltip("Swap asset on one GaussianSplatRenderer, or toggle between three renderer GameObjects.")]
    public SplatSwitchMode switchMode = SplatSwitchMode.SwapAssetOnTargetRenderer;

    [Header("Start Room")]
    public bool applyRoomOnStart = true;
    public SplatRoom startRoom = SplatRoom.CalmRoom;

    [Header("MODE 1 — Single Renderer Asset Swapping")]
    [Tooltip("The one active GaussianSplatRenderer that will change between calm/fast/host splat assets.")]
    public GaussianSplatRenderer targetRenderer;

    [Tooltip("Gaussian splat asset for the Calm room.")]
    public GaussianSplatAsset calmRoomAsset;

    [Tooltip("Gaussian splat asset for the Fast room.")]
    public GaussianSplatAsset fastRoomAsset;

    [Tooltip("Gaussian splat asset for the Host room.")]
    public GaussianSplatAsset hostRoomAsset;

    [Header("MODE 2 — Toggle Existing Renderers")]
    [Tooltip("Existing GaussianSplatRenderer object for Calm room.")]
    public GaussianSplatRenderer calmRoomRenderer;

    [Tooltip("Existing GaussianSplatRenderer object for Fast room.")]
    public GaussianSplatRenderer fastRoomRenderer;

    [Tooltip("Existing GaussianSplatRenderer object for Host room.")]
    public GaussianSplatRenderer hostRoomRenderer;

    [Tooltip("If true, inactive splat renderer GameObjects are disabled. If false, only the GaussianSplatRenderer component is disabled.")]
    public bool disableInactiveGameObjects = true;

    [Header("Optional Transform Sync")]
    [Tooltip("Useful in Toggle Renderer mode. When switching, copy this transform's pose to the active splat.")]
    public bool forceActiveSplatToManagerTransform = false;

    [Tooltip("Useful in Asset Swap mode. Keeps the target renderer at this manager's transform when switching.")]
    public bool forceTargetRendererToManagerTransform = false;

    [Header("Debug")]
    public bool debugLogs = true;

    [Header("Events")]
    public SplatRoomEvent onRoomChanged = new SplatRoomEvent();

    [Serializable]
    public class SplatRoomEvent : UnityEvent<SplatRoom> { }

    public SplatRoom CurrentRoom { get; private set; } = SplatRoom.None;

    private void Reset()
    {
        TryAutoAssignTargetRenderer();
    }

    private void Awake()
    {
        TryAutoAssignTargetRenderer();
    }

    private void Start()
    {
        if (!Application.isPlaying) return;

        if (applyRoomOnStart)
        {
            SetRoom(startRoom);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        TryAutoAssignTargetRenderer();

        if (forceTargetRendererToManagerTransform && targetRenderer != null)
        {
            CopyManagerTransformTo(targetRenderer.transform);
            EditorUtility.SetDirty(targetRenderer.transform);
        }
    }
#endif

    // ─────────────────────────────────────────────────────────────
    // Public UI methods
    // ─────────────────────────────────────────────────────────────

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

    public void HideAllRooms()
    {
        if (switchMode == SplatSwitchMode.ToggleRendererObjects)
        {
            SetRendererActive(calmRoomRenderer, false);
            SetRendererActive(fastRoomRenderer, false);
            SetRendererActive(hostRoomRenderer, false);
        }
        else if (targetRenderer != null)
        {
            targetRenderer.enabled = false;
        }

        CurrentRoom = SplatRoom.None;
        onRoomChanged?.Invoke(CurrentRoom);

        if (debugLogs)
        {
            Debug.Log("<b>[SplatManager]</b> Hid all splat rooms.");
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Main switch logic
    // ─────────────────────────────────────────────────────────────

    public void SetRoom(SplatRoom room)
    {
        if (room == SplatRoom.None)
        {
            HideAllRooms();
            return;
        }

        switch (switchMode)
        {
            case SplatSwitchMode.SwapAssetOnTargetRenderer:
                SwapAsset(room);
                break;

            case SplatSwitchMode.ToggleRendererObjects:
                ToggleRenderer(room);
                break;

            default:
                Debug.LogError($"[SplatManager] Unknown switch mode: {switchMode}");
                break;
        }
    }

    private void SwapAsset(SplatRoom room)
    {
        if (targetRenderer == null)
        {
            Debug.LogError("[SplatManager] Cannot swap splat. targetRenderer is missing.");
            return;
        }

        GaussianSplatAsset targetAsset = GetAsset(room);

        if (targetAsset == null)
        {
            Debug.LogError($"[SplatManager] Cannot switch to {room}. The matching GaussianSplatAsset is missing.");
            return;
        }

        if (forceTargetRendererToManagerTransform)
        {
            CopyManagerTransformTo(targetRenderer.transform);
        }

        targetRenderer.gameObject.SetActive(true);
        targetRenderer.enabled = true;

        // Main important line:
        // This changes which GaussianSplatAsset the renderer uses.
        targetRenderer.m_Asset = targetAsset;

        // Important:
        // The renderer script you pasted exposes UpdateRessources().
        // Calling it forces GPU buffers/material data to rebuild immediately.
        targetRenderer.UpdateRessources();

        CurrentRoom = room;
        onRoomChanged?.Invoke(CurrentRoom);

        if (debugLogs)
        {
            Debug.Log($"<color=lime><b>[SplatManager]</b></color> Swapped target renderer to {room}: {targetAsset.name}");
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(targetRenderer);
            EditorUtility.SetDirty(this);
        }
#endif
    }

    private void ToggleRenderer(SplatRoom room)
    {
        GaussianSplatRenderer activeRenderer = GetRenderer(room);

        if (activeRenderer == null)
        {
            Debug.LogError($"[SplatManager] Cannot switch to {room}. The matching GaussianSplatRenderer is missing.");
            return;
        }

        SetRendererActive(calmRoomRenderer, room == SplatRoom.CalmRoom);
        SetRendererActive(fastRoomRenderer, room == SplatRoom.FastRoom);
        SetRendererActive(hostRoomRenderer, room == SplatRoom.HostRoom);

        if (forceActiveSplatToManagerTransform)
        {
            CopyManagerTransformTo(activeRenderer.transform);
        }

        // Make sure the selected renderer refreshes.
        if (activeRenderer.HasValidAsset)
        {
            activeRenderer.UpdateRessources();
        }

        CurrentRoom = room;
        onRoomChanged?.Invoke(CurrentRoom);

        if (debugLogs)
        {
            Debug.Log($"<color=lime><b>[SplatManager]</b></color> Activated renderer for {room}: {activeRenderer.name}");
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(activeRenderer);
            EditorUtility.SetDirty(this);
        }
#endif
    }

    // ─────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────

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

            default:
                return null;
        }
    }

    private void SetRendererActive(GaussianSplatRenderer renderer, bool active)
    {
        if (renderer == null) return;

        if (disableInactiveGameObjects)
        {
            renderer.gameObject.SetActive(active);
        }
        else
        {
            renderer.enabled = active;
        }
    }

    private void CopyManagerTransformTo(Transform target)
    {
        if (target == null) return;

        target.position = transform.position;
        target.rotation = transform.rotation;
        target.localScale = transform.localScale;
    }

    private void TryAutoAssignTargetRenderer()
    {
        if (targetRenderer != null) return;

        targetRenderer = GetComponent<GaussianSplatRenderer>();

        if (targetRenderer != null) return;

        targetRenderer = GetComponentInChildren<GaussianSplatRenderer>(true);
    }
}