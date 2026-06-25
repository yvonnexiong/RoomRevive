using UnityEngine;

namespace RoomRevive.VFX
{
    /// <summary>
    /// Feeds a collider volume to the "RoomRevive/Outside Collider Highlight" shader so that any
    /// mesh using that shader is tinted + made transparent OUTSIDE the volume, and rendered
    /// normally inside it.
    ///
    /// • BoxCollider   → exact oriented box (honours position, rotation, non-uniform scale).
    /// • SphereCollider→ exact sphere.
    /// • Any other collider (mesh/capsule) → falls back to its world axis-aligned bounding box.
    ///
    /// The volume is published via GLOBAL shader properties, so a SINGLE component drives every
    /// material that uses the shader. (Per-object volumes would need a MaterialPropertyBlock variant.)
    ///
    /// Tweak the LOOK on the material (Outside Color / Outside Alpha / Edge Softness). Tweak the
    /// REGION here (assign the collider). [ExecuteAlways] so it previews + tracks the collider in edit mode.
    ///
    /// Upgrade note: this ships with an Unlit shader. For lit shading inside the volume, port the
    /// frag's last few lines into a URP Lit shader's surface output.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("RoomRevive/Outside Collider Clip")]
    public class OutsideColliderClip : MonoBehaviour
    {
        [Tooltip("The volume that defines 'inside'. BoxCollider & SphereCollider are exact; " +
                 "any other collider falls back to its world bounding box.")]
        public Collider region;

        [Tooltip("Master switch. When off, meshes render normally (no tint / no transparency).")]
        public bool clipEnabled = true;

        static readonly int ClipWorldToLocalID = Shader.PropertyToID("_ClipWorldToLocal");
        static readonly int ClipShapeID        = Shader.PropertyToID("_ClipShape");
        static readonly int ClipEnabledID      = Shader.PropertyToID("_ClipEnabled");

        void OnEnable()  => Push();
        void Update()    => Push();   // keep in sync as the collider moves / resizes
        void OnDisable() => Shader.SetGlobalFloat(ClipEnabledID, 0f);

#if UNITY_EDITOR
        void OnValidate() => Push();
#endif

        void Push()
        {
            if (!clipEnabled || region == null)
            {
                Shader.SetGlobalFloat(ClipEnabledID, 0f);
                return;
            }

            Matrix4x4 m;
            float shape;

            if (region is BoxCollider box)
            {
                Vector3 s = box.size;
                var inv = new Vector3(1f / Safe(s.x), 1f / Safe(s.y), 1f / Safe(s.z));
                // world → collider-local → centered on box → scaled so the box spans [-0.5, 0.5].
                m = Matrix4x4.Scale(inv) * Matrix4x4.Translate(-box.center) * region.transform.worldToLocalMatrix;
                shape = 0f;
            }
            else if (region is SphereCollider sphere)
            {
                float inv = 1f / Safe(2f * sphere.radius);
                m = Matrix4x4.Scale(new Vector3(inv, inv, inv)) *
                    Matrix4x4.Translate(-sphere.center) * region.transform.worldToLocalMatrix;
                shape = 1f;
            }
            else
            {
                // Fallback: world axis-aligned bounding box (no rotation).
                Bounds b = region.bounds;
                Vector3 s = b.size;
                var inv = new Vector3(1f / Safe(s.x), 1f / Safe(s.y), 1f / Safe(s.z));
                m = Matrix4x4.Scale(inv) * Matrix4x4.Translate(-b.center);
                shape = 0f;
            }

            Shader.SetGlobalMatrix(ClipWorldToLocalID, m);
            Shader.SetGlobalFloat(ClipShapeID, shape);
            Shader.SetGlobalFloat(ClipEnabledID, 1f);
        }

        static float Safe(float v) => Mathf.Abs(v) < 1e-5f ? 1e-5f : v;
    }
}
