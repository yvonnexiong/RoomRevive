using UnityEngine;
using UnityEngine.Rendering;

namespace RoomRevive
{
    /// <summary>
    /// Procedural 3D donut (torus). Adjust the outer radius, hole radius, and transparency in the
    /// inspector — the mesh + material rebuild live in edit mode and Play mode. Lies flat in the XZ
    /// plane (hole faces local +Y).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class DonutMesh : MonoBehaviour
    {
        [Header("Shape")]
        [Tooltip("Outer edge radius of the donut (center → outer rim).")]
        [Min(0.001f)] public float outerRadius = 0.5f;

        [Tooltip("Hole radius (center → inner rim). Kept below the outer radius.")]
        [Min(0f)] public float holeRadius = 0.25f;

        [Header("Smoothness")]
        [Tooltip("Segments around the main ring.")]
        [Range(3, 128)] public int ringSegments = 48;
        [Tooltip("Segments around the tube cross-section.")]
        [Range(3, 64)] public int tubeSegments = 24;

        [Header("Appearance")]
        public Color color = new Color(0.37f, 0.59f, 0.72f, 1f);

        [Tooltip("0 = fully opaque, 1 = fully see-through.")]
        [Range(0f, 1f)] public float transparency = 0.5f;

        MeshFilter _filter;
        MeshRenderer _renderer;
        Mesh _mesh;
        Material _material;

        void OnEnable() => Rebuild();

#if UNITY_EDITOR
        void OnValidate()
        {
            // Defer: OnValidate can fire during serialization where mesh/material writes are unsafe.
            UnityEditor.EditorApplication.delayCall -= RebuildDeferred;
            UnityEditor.EditorApplication.delayCall += RebuildDeferred;
        }

        void RebuildDeferred()
        {
            UnityEditor.EditorApplication.delayCall -= RebuildDeferred;
            if (this == null) return;
            Rebuild();
        }
#endif

        [ContextMenu("Rebuild Donut")]
        public void Rebuild()
        {
            GrabComponents();
            RebuildMesh();
            RebuildMaterial();
        }

        void GrabComponents()
        {
            if (_filter == null) _filter = GetComponent<MeshFilter>();
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
        }

        void RebuildMesh()
        {
            // Convert the user-facing outer/hole radii into torus major (R) + tube (r) radii.
            float hole = Mathf.Clamp(holeRadius, 0f, outerRadius - 0.001f);
            float R = (outerRadius + hole) * 0.5f;   // center of the tube
            float r = (outerRadius - hole) * 0.5f;   // tube thickness

            int ring = Mathf.Max(3, ringSegments);
            int tube = Mathf.Max(3, tubeSegments);

            int vCount = (ring + 1) * (tube + 1);
            Vector3[] verts = new Vector3[vCount];
            Vector3[] normals = new Vector3[vCount];
            Vector2[] uvs = new Vector2[vCount];
            int[] tris = new int[ring * tube * 6];

            int vi = 0;
            for (int i = 0; i <= ring; i++)
            {
                float u = (i / (float)ring) * Mathf.PI * 2f;
                float cu = Mathf.Cos(u), su = Mathf.Sin(u);
                for (int j = 0; j <= tube; j++, vi++)
                {
                    float v = (j / (float)tube) * Mathf.PI * 2f;
                    float cv = Mathf.Cos(v), sv = Mathf.Sin(v);

                    verts[vi] = new Vector3((R + r * cv) * cu, r * sv, (R + r * cv) * su);
                    normals[vi] = new Vector3(cv * cu, sv, cv * su);
                    uvs[vi] = new Vector2(i / (float)ring, j / (float)tube);
                }
            }

            int ti = 0;
            int stride = tube + 1;
            for (int i = 0; i < ring; i++)
            for (int j = 0; j < tube; j++)
            {
                int a = i * stride + j;
                int b = (i + 1) * stride + j;
                tris[ti++] = a;     tris[ti++] = a + 1; tris[ti++] = b;
                tris[ti++] = b;     tris[ti++] = a + 1; tris[ti++] = b + 1;
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "DonutMesh" };
                _mesh.hideFlags = HideFlags.DontSave;
            }
            _mesh.Clear();
            _mesh.indexFormat = vCount > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            _mesh.vertices = verts;
            _mesh.normals = normals;
            _mesh.uv = uvs;
            _mesh.triangles = tris;
            _mesh.RecalculateBounds();

            _filter.sharedMesh = _mesh;
        }

        void RebuildMaterial()
        {
            float alpha = 1f - transparency;
            bool transparent = alpha < 0.999f;

            if (_material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                _material = new Material(shader) { name = "DonutMaterial", hideFlags = HideFlags.DontSave };
            }

            Color c = color; c.a = alpha;
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", c);
            if (_material.HasProperty("_Color")) _material.SetColor("_Color", c);

            SetSurfaceMode(_material, transparent);
            _renderer.sharedMaterial = _material;
        }

        // Switch a URP/Lit (or Standard) material between opaque and alpha-blended transparency.
        static void SetSurfaceMode(Material m, bool transparent)
        {
            if (transparent)
            {
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // 0 opaque, 1 transparent
                if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);     // alpha blend
                if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
                if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)BlendMode.One);
                if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)BlendMode.Zero);
                if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 1f);
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Opaque");
                m.renderQueue = (int)RenderQueue.Geometry;
            }
        }
    }
}
