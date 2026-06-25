using UnityEngine;

namespace RoomRevive
{
    /// <summary>
    /// Drives this GameObject's transform so a <see cref="GaussianSplatting.Runtime.GaussianCutout"/>
    /// box (or ellipsoid) exactly covers a referenced <see cref="BoxCollider"/>.
    ///
    /// A GaussianCutout's region is a unit shape in local space (the box spans ±1, the ellipsoid is
    /// radius 1), so its world half-extents equal this transform's lossy scale. We therefore set:
    ///   • position = the collider's world-space centre,
    ///   • rotation = the collider's world rotation,
    ///   • scale    = the collider's world half-extents.
    ///
    /// Put this on the cutout object (e.g. FridgeCutOut) and assign the target collider.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class GaussianCutoutMatchCollider : MonoBehaviour
    {
        [Tooltip("The collider this cutout should cover. The cutout's transform is driven to match it exactly.")]
        public BoxCollider target;

        [Tooltip("Re-match every frame so the cutout follows the collider if it moves. " +
                 "Turn off for a one-time snap (then you can edit the transform freely).")]
        public bool matchEveryFrame = true;

        [Tooltip("Per-axis padding as a fraction of the collider half-extent. " +
                 "0 = exact match, 0.3 = +30% larger, -0.3 = 30% smaller (inset).")]
        public Vector3 paddingPercent = Vector3.zero;

        void OnEnable() => Match();

        void Update()
        {
            // [ExecuteAlways] → this also runs in edit mode.
            if (matchEveryFrame) Match();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null) Match();
            };
        }
#endif

        /// <summary>Snap this transform to cover <see cref="target"/> right now.</summary>
        [ContextMenu("Match Now")]
        public void Match()
        {
            if (target == null) return;

            Transform ct = target.transform;

            Vector3 worldCenter = ct.TransformPoint(target.center);
            Quaternion worldRot = ct.rotation;
            // Collider world half-extents = (size * 0.5) scaled by the collider transform's lossy scale.
            // The cutout's world half-extents equal its own lossy scale, so this is our target world scale.
            Vector3 worldHalf = Vector3.Scale(target.size * 0.5f, ct.lossyScale);

            // Grow / shrink the matched region by a per-axis fraction (0.3 = +30%, -0.3 = inset 30%).
            worldHalf = new Vector3(
                worldHalf.x * (1f + paddingPercent.x),
                worldHalf.y * (1f + paddingPercent.y),
                worldHalf.z * (1f + paddingPercent.z));

            transform.SetPositionAndRotation(worldCenter, worldRot);

            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(
                SafeDiv(worldHalf.x, parentScale.x),
                SafeDiv(worldHalf.y, parentScale.y),
                SafeDiv(worldHalf.z, parentScale.z));
        }

        static float SafeDiv(float a, float b) => Mathf.Approximately(b, 0f) ? a : a / b;
    }
}
