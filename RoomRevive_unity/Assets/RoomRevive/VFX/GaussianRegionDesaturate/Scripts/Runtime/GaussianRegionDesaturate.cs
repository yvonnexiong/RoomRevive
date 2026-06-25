using UnityEngine;

namespace RoomRevive.VFX
{
    /// <summary>
    /// Drives the "Gaussian Splatting/Render Splats (Region Desaturate)" shader so that splats whose
    /// center falls inside (or outside) a collider volume are pushed to grayscale — the same region
    /// idea as a <c>GaussianCutout</c>, but it recolors instead of culling. The Gaussian-splatting
    /// package is not modified.
    ///
    /// SETUP
    ///   1. Assign the "…(Region Desaturate)" shader to the target GaussianSplatRenderer's
    ///      "Render Shader" (m_ShaderSplats) field — that's the only renderer change needed.
    ///   2. Add this component anywhere, assign a Box/Sphere collider as the Region.
    ///
    /// • BoxCollider / SphereCollider → exact volume (honours position, rotation, scale).
    /// • Any other collider → its world bounding box.
    ///
    /// The volume is published via GLOBAL shader properties, so one component affects every splat
    /// renderer using the desaturate shader. [ExecuteAlways] for live edit-mode preview.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("RoomRevive/Gaussian Region Desaturate")]
    public class GaussianRegionDesaturate : MonoBehaviour
    {
        [Header("Region")]
        [Tooltip("Volume that defines the desaturated region. Box & Sphere are exact; others use bounds.")]
        public Collider region;

        [Tooltip("On: splats INSIDE the region go grayscale. Off: splats OUTSIDE the region do.")]
        public bool desaturateInside = true;

        [Header("Look")]
        [Tooltip("0 = no change, 1 = fully black & white.")]
        [Range(0f, 1f)] public float amount = 1f;

        [Tooltip("Boundary feather (in volume-relative units). Small = crisp edge.")]
        [Range(0.0001f, 0.5f)] public float softness = 0.02f;

        [Tooltip("Master switch. When off, splats render with their original color.")]
        public bool effectEnabled = true;

        static readonly int WorldToLocalID = Shader.PropertyToID("_GSDesatWorldToLocal");
        static readonly int ShapeID        = Shader.PropertyToID("_GSDesatShape");
        static readonly int EnabledID      = Shader.PropertyToID("_GSDesatEnabled");
        static readonly int InsideID       = Shader.PropertyToID("_GSDesatInside");
        static readonly int AmountID       = Shader.PropertyToID("_GSDesatAmount");
        static readonly int SoftnessID     = Shader.PropertyToID("_GSDesatSoftness");

        void OnEnable()  => Push();
        void Update()    => Push();   // track the collider as it moves / resizes
        void OnDisable() => Shader.SetGlobalFloat(EnabledID, 0f);

#if UNITY_EDITOR
        void OnValidate() => Push();
#endif

        void Push()
        {
            if (!effectEnabled || region == null)
            {
                Shader.SetGlobalFloat(EnabledID, 0f);
                return;
            }

            Matrix4x4 m;
            float shape;

            if (region is BoxCollider box)
            {
                Vector3 s = box.size;
                var inv = new Vector3(1f / Safe(s.x), 1f / Safe(s.y), 1f / Safe(s.z));
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
                Bounds b = region.bounds;
                Vector3 s = b.size;
                var inv = new Vector3(1f / Safe(s.x), 1f / Safe(s.y), 1f / Safe(s.z));
                m = Matrix4x4.Scale(inv) * Matrix4x4.Translate(-b.center);
                shape = 0f;
            }

            Shader.SetGlobalMatrix(WorldToLocalID, m);
            Shader.SetGlobalFloat(ShapeID, shape);
            Shader.SetGlobalFloat(InsideID, desaturateInside ? 1f : 0f);
            Shader.SetGlobalFloat(AmountID, amount);
            Shader.SetGlobalFloat(SoftnessID, softness);
            Shader.SetGlobalFloat(EnabledID, 1f);
        }

        static float Safe(float v) => Mathf.Abs(v) < 1e-5f ? 1e-5f : v;
    }
}
