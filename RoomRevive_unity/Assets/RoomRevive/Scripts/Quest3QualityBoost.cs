using UnityEngine;
using UnityEngine.XR;

namespace RoomRevive
{
    /// <summary>
    /// Sharpens the whole view on Quest 3 by supersampling the VR eye buffer at startup — UI, 3D
    /// models, and the Gaussian splat all get crisper. Supersampling renders larger then downsamples,
    /// so (unlike MSAA) it does NOT tear the splats and is safe to raise.
    ///
    /// Higher = sharper but heavier. If the framerate drops, lower <see cref="renderScale"/>. Pair this
    /// with a high GPU level + Foveated Rendering on OVRManager so there's budget for the extra pixels.
    /// Put this on any always-on GameObject (e.g. the rig or a bootstrap object).
    /// </summary>
    [DisallowMultipleComponent]
    public class Quest3QualityBoost : MonoBehaviour
    {
        [Tooltip("Eye-buffer supersampling. 1 = native; 1.2–1.3 is noticeably sharper on Quest 3 when you have GPU headroom.")]
        [Range(0.6f, 1.6f)] public float renderScale = 1.25f;

        [Tooltip("Target display refresh. 72 leaves the most GPU budget per frame for resolution; 90/120 are smoother but tighter.")]
        public int targetFrameRate = 72;

        [Tooltip("Force the highest anisotropic-filtering setting (sharper textures at grazing angles).")]
        public bool forceAnisotropic = true;

        [Tooltip("Re-apply whenever this component is enabled — handy while tuning renderScale in Play mode.")]
        public bool applyOnEnable = true;

        void Start() => Apply();

        void OnEnable() { if (applyOnEnable) Apply(); }

        public void Apply()
        {
            // Supersample the XR eye textures. Safe for splats (no MSAA-style tearing).
            XRSettings.eyeTextureResolutionScale = Mathf.Clamp(renderScale, 0.5f, 2f);
            XRSettings.renderViewportScale = 1f;

            if (targetFrameRate > 0)
                Application.targetFrameRate = targetFrameRate;

            if (forceAnisotropic)
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;

            Debug.Log($"[Quest3QualityBoost] eyeTextureResolutionScale={XRSettings.eyeTextureResolutionScale:0.00}, " +
                      $"targetFPS={targetFrameRate}, aniso={QualitySettings.anisotropicFiltering}");
        }

#if UNITY_EDITOR
        // Live-tune renderScale in Play mode: edit the value and it re-applies.
        void OnValidate()
        {
            renderScale = Mathf.Clamp(renderScale, 0.6f, 1.6f);
            if (Application.isPlaying && isActiveAndEnabled) Apply();
        }
#endif
    }
}
