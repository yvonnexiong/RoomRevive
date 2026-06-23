using System.Collections.Generic;
using GaussianSplatting.Runtime;
using UnityEngine;

namespace RoomRevive.Capture
{
    /// <summary>
    /// Sits on a capture <see cref="Camera"/>. You choose which layers this camera renders via
    /// <see cref="renderLayers"/>; put everything you want captured on those layers (e.g. an
    /// "Objects" layer) and nothing else will appear.
    ///
    /// Normal meshes and UI are handled purely by the camera's culling mask — robust, because a
    /// per-camera mask can't be overridden by UI controller scripts. Gaussian splats ignore the
    /// culling mask (custom render pass), so any splat whose layer is NOT in <see cref="renderLayers"/>
    /// is disabled for the shot and restored afterwards.
    ///
    /// Two ways to use it:
    ///   • <b>Preview</b> (<see cref="BeginIsolation"/>/<see cref="EndIsolation"/> or the buttons):
    ///     holds the layer mask on so you can frame the shot live. Restore brings the scene back.
    ///   • <b>Capture</b> (<see cref="CaptureToTexture"/>/Capture PNG): apply → render → restore.
    /// </summary>
    [AddComponentMenu("RoomRevive/Capture/Camera Render Set")]
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class CameraRenderSet : MonoBehaviour
    {
        [Tooltip("The layers this camera will render. Put everything you want captured on one of " +
                 "these layers. Splats not on these layers are hidden for the shot.")]
        public LayerMask renderLayers = ~0;

        [Header("Capture output")]
        [Tooltip("Single file (under Assets/) that every capture overwrites.")]
        public string outputPath = "Assets/Captures/RoomCapture.png";
        [Tooltip("Capture resolution.")]
        public int captureWidth = 1920;
        public int captureHeight = 1080;

        // ── Restore bookkeeping (only ever holds what WE changed) ─────────────────
        readonly List<GaussianSplatRenderer> _disabledSplats = new();
        int _savedCullingMask;
        bool _isolated;

        Camera _cam;
        Camera Cam => _cam != null ? _cam : (_cam = GetComponent<Camera>());

        /// <summary>True while this camera is currently restricted to <see cref="renderLayers"/>.</summary>
        public bool IsIsolated => _isolated;

        // ── Preview (held) ────────────────────────────────────────────────────────

        /// <summary>
        /// Restricts the camera to <see cref="renderLayers"/> and disables off-layer splats, holding
        /// it until <see cref="EndIsolation"/>. No-op when already applied.
        /// </summary>
        [ContextMenu("Isolate (Preview)")]
        public void BeginIsolation()
        {
            if (_isolated) return;
            if (renderLayers.value == 0)
                Debug.LogWarning("[CameraRenderSet] 'Render Layers' is empty — the capture will be blank.", this);

            _savedCullingMask = Cam.cullingMask;
            Cam.cullingMask = renderLayers.value;

            // Splats ignore the culling mask, so hide the ones whose layer isn't being rendered.
            foreach (GaussianSplatRenderer s in FindObjectsByType<GaussianSplatRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!s.enabled || LayerIncluded(s.gameObject.layer)) continue;
                s.enabled = false;
                _disabledSplats.Add(s);
            }

            _isolated = true;
        }

        /// <summary>Restores the camera's culling mask and any splats this component disabled.</summary>
        [ContextMenu("Restore Scene")]
        public void EndIsolation()
        {
            if (!_isolated) return;

            foreach (GaussianSplatRenderer s in _disabledSplats) if (s != null) s.enabled = true;
            _disabledSplats.Clear();

            Cam.cullingMask = _savedCullingMask;
            _isolated = false;
        }

        // ── Capture ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Applies the render layers, renders this camera into a new <see cref="Texture2D"/> (RGBA32),
        /// and restores. If isolation is already held (preview), it is left held. Caller owns the
        /// returned texture.
        /// </summary>
        public Texture2D CaptureToTexture(int width, int height)
        {
            bool startedHere = !_isolated;
            BeginIsolation();

            RenderTexture prevTarget = Cam.targetTexture;
            RenderTexture prevActive = RenderTexture.active;

            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                Cam.targetTexture = rt;
                Cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
            }
            finally
            {
                Cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
                if (startedHere) EndIsolation();
            }
            return tex;
        }

        // ── Internals ───────────────────────────────────────────────────────────

        bool LayerIncluded(int layer) => (renderLayers.value & (1 << layer)) != 0;

        // Safety net: never leave the camera isolated if disabled/destroyed mid-preview.
        void OnDisable() => EndIsolation();
    }
}
