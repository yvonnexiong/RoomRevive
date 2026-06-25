using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RoomRevive
{
    /// <summary>
    /// Adds or subtracts uniform padding to a <see cref="GaussianSplatting.Runtime.GaussianCutout"/> box
    /// (or ellipsoid) without hand-editing the Transform scale.
    ///
    /// A GaussianCutout's region is a unit shape in local space (the box spans ±1), so its world half-extents
    /// equal this transform's scale — i.e. +1 on a scale axis = +1 m of padding on that side. This component
    /// remembers the box's zero-padding size (<see cref="baseScale"/>) and drives
    ///     localScale = baseScale + padding      (per-axis, corrected for any parent scaling)
    /// so 'Padding' grows/shrinks every face by that many metres; set it back to 0 for the original box.
    /// Centre and rotation are untouched.
    ///
    /// Workflow: size/position the box as usual, then Add Component → "Gaussian Cutout Padding" (it captures the
    /// current size as the base). Then just change 'Padding'. If you later resize the Transform by hand,
    /// right-click the component header → "Capture current scale as base size".
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GaussianSplatting.Runtime.GaussianCutout))]
    public class GaussianCutoutPadding : MonoBehaviour
    {
        [Tooltip("Metres added to every side of the box (negative shrinks it). Centre & rotation stay put.")]
        public float padding = 0f;

        [Tooltip("Extra padding per local axis (metres), added on top of 'Padding'. Leave at 0 for uniform.")]
        public Vector3 paddingPerAxis = Vector3.zero;

        [Tooltip("The box size with zero padding (world half-extents). Captured when the component is added; " +
                 "re-capture from the context menu if you resize the Transform by hand.")]
        public Vector3 baseScale = Vector3.zero;

        [Tooltip("Re-apply every frame. Only needed if something else also changes the scale at runtime.")]
        public bool continuous = false;

        // Called by Unity when the component is first added → snapshot the existing box as the base size.
        void Reset()
        {
            baseScale = transform.localScale;
            padding = 0f;
            paddingPerAxis = Vector3.zero;
        }

        void OnEnable()
        {
            if (baseScale == Vector3.zero) baseScale = transform.localScale;
            Apply();
        }

        void Update()
        {
            if (continuous) Apply();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (baseScale == Vector3.zero) baseScale = transform.localScale;
            // Defer: writing transform.localScale directly inside OnValidate can trip Unity warnings.
            EditorApplication.delayCall -= DeferredApply;
            EditorApplication.delayCall += DeferredApply;
        }

        void DeferredApply()
        {
            EditorApplication.delayCall -= DeferredApply;
            if (this == null) return;
            Apply();
        }
#endif

        /// <summary>Drive the transform scale to baseScale + padding (in metres per side).</summary>
        public void Apply()
        {
            Vector3 ps = transform.parent ? transform.parent.lossyScale : Vector3.one;   // metres → local scale through parent
            Vector3 s = baseScale;
            s.x += (padding + paddingPerAxis.x) / Mathf.Max(1e-6f, ps.x);
            s.y += (padding + paddingPerAxis.y) / Mathf.Max(1e-6f, ps.y);
            s.z += (padding + paddingPerAxis.z) / Mathf.Max(1e-6f, ps.z);
            transform.localScale = s;
        }

        /// <summary>Re-baseline: treat the current (padding-adjusted) scale as the new zero-padding size.</summary>
        [ContextMenu("Capture current scale as base size")]
        public void CaptureBase()
        {
            Vector3 ps = transform.parent ? transform.parent.lossyScale : Vector3.one;
            Vector3 s = transform.localScale;
            s.x -= (padding + paddingPerAxis.x) / Mathf.Max(1e-6f, ps.x);   // strip current padding so re-applying is stable
            s.y -= (padding + paddingPerAxis.y) / Mathf.Max(1e-6f, ps.y);
            s.z -= (padding + paddingPerAxis.z) / Mathf.Max(1e-6f, ps.z);
            baseScale = s;
            Apply();
        }
    }
}
