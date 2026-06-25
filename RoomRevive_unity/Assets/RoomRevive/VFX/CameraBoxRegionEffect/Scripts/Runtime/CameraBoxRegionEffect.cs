using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering; // RenderPipelineManager, ScriptableRenderContext (core RP — auto-referenced)

namespace RoomRevive.VFX
{
    /// <summary>
    /// Projects a list of 3D BoxColliders onto the assigned camera and feeds the convex-hull silhouette
    /// of each box (a dynamic polygon that morphs as the box rotates) to the
    /// "RoomRevive/Box Region Blur Gray" full-screen shader, so whatever the camera sees inside that
    /// silhouette is rendered blurry + black & white.
    ///
    /// Objects on <see cref="excludeLayers"/> stay sharp + colored: a companion RenderObjects feature
    /// ("BoxRegionExcludeRedraw") re-draws them AFTER the blur, so they're painted crisp on top — which
    /// also fixes the jagged splat↔mesh edge (the product is composited over the splats, not fighting
    /// them per-billboard). This component just keeps that feature's layer mask in sync.
    ///
    /// SETUP
    ///   1. Material from "RoomRevive/Box Region Blur Gray".
    ///   2. URP Renderer → Full Screen Pass Renderer Feature → that material, After Rendering Post Processing.
    ///   3. (Auto-added) RenderObjects "BoxRegionExcludeRedraw" runs just after the blur.
    ///   4. Put this component anywhere; assign Camera, Boxes, and Exclude Layers (the product layer).
    ///
    /// Driven per-camera in beginCameraRendering and only for the assigned camera, so the Scene view /
    /// previews are unaffected.
    ///
    /// NOTE (Quest passthrough): a post-process only touches the camera color buffer; on Quest,
    /// passthrough is composited by the OS and is NOT in that buffer.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("RoomRevive/Camera Box Region Effect")]
    public class CameraBoxRegionEffect : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("Camera the boxes are projected onto. Defaults to Camera.main. Effect ONLY applies to this camera.")]
        public Camera cam;

        [Tooltip("Boxes whose projected silhouette gets the blur + B&W effect. Up to 4 are used.")]
        public List<BoxCollider> boxes = new List<BoxCollider>();

        [Header("Region")]
        [Tooltip("Soft feather on the silhouette edge (UV units). Keep tiny for a crisp edge that hugs the box.")]
        [Range(0.0001f, 0.1f)] public float edgeSoftness = 0.002f;

        [Header("Look")]
        [Tooltip("Blur tap distance in UV units. 0 = no blur.")]
        [Range(0f, 0.02f)] public float blurSize = 0.004f;

        [Tooltip("0 = full color, 1 = full black & white.")]
        [Range(0f, 1f)] public float grayAmount = 1f;

        [Tooltip("Master switch.")]
        public bool effectEnabled = true;

        [Header("Exclusions")]
        [Tooltip("Objects on these layers stay sharp + colored and are drawn crisply on top of the blur " +
                 "(via the BoxRegionExcludeRedraw renderer feature). Set this to the product model's layer.")]
        public LayerMask excludeLayers = 0;

        [Header("Debug")]
        [Tooltip("Draw the computed silhouette vertices + count in the Game view.")]
        public bool debugOverlay = false;

        const int MaxPolys = 4;
        const int MaxVerts = 8;

        static readonly int VertsID = Shader.PropertyToID("_PolyVerts");
        static readonly int InfoID  = Shader.PropertyToID("_PolyInfo");
        static readonly int CountID = Shader.PropertyToID("_PolyCount");
        static readonly int BlurID  = Shader.PropertyToID("_BlurSize");
        static readonly int GrayID  = Shader.PropertyToID("_GrayAmount");
        static readonly int SoftID  = Shader.PropertyToID("_Softness");

        readonly Vector4[] _polyVerts = new Vector4[MaxPolys * MaxVerts];
        readonly Vector4[] _polyInfo  = new Vector4[MaxPolys];
        int _polyCount;

        // Reused per-frame buffers (no GC).
        readonly List<Vector2> _pts  = new List<Vector2>(8);
        readonly List<Vector2> _hull = new List<Vector2>(8);
        readonly Vector2[] _chain    = new Vector2[16];

        void OnEnable()
        {
            if (cam == null) cam = Camera.main;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            Shader.SetGlobalFloat(CountID, 0f);
        }

        // Drive the effect ONLY for the target camera; other cameras (Scene view, previews) get 0 polys.
        void OnBeginCamera(ScriptableRenderContext ctx, Camera camera)
        {
            if (!effectEnabled || cam == null || camera != cam)
            {
                Shader.SetGlobalFloat(CountID, 0f);
                if (camera == cam) _polyCount = 0;
                return;
            }
            PushPolys();
        }

        void PushPolys()
        {
            int p = 0;
            for (int b = 0; b < boxes.Count && p < MaxPolys; b++)
            {
                if (boxes[b] == null) continue;
                if (!TryProjectHull(boxes[b], _hull)) continue;

                int vc = Mathf.Min(_hull.Count, MaxVerts);
                for (int i = 0; i < vc; i++)
                    _polyVerts[p * MaxVerts + i] = new Vector4(_hull[i].x, _hull[i].y, 0f, 0f);
                _polyInfo[p] = new Vector4(vc, 0f, 0f, 0f);
                p++;
            }
            _polyCount = p;

            Shader.SetGlobalVectorArray(VertsID, _polyVerts);
            Shader.SetGlobalVectorArray(InfoID, _polyInfo);
            Shader.SetGlobalFloat(CountID, p);
            Shader.SetGlobalFloat(BlurID, blurSize);
            Shader.SetGlobalFloat(GrayID, grayAmount);
            Shader.SetGlobalFloat(SoftID, edgeSoftness);
        }

        // Convex hull of the box's 8 projected corners (those in front of the camera), CCW in viewport UV.
        bool TryProjectHull(BoxCollider box, List<Vector2> hull)
        {
            _pts.Clear();
            Transform t = box.transform;
            Vector3 c = box.center, e = box.size * 0.5f;

            for (int i = 0; i < 8; i++)
            {
                var corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x,
                                             (i & 2) == 0 ? -e.y : e.y,
                                             (i & 4) == 0 ? -e.z : e.z);
                Vector3 vp = cam.WorldToViewportPoint(t.TransformPoint(corner));
                if (vp.z <= 0f) continue; // behind the camera
                _pts.Add(new Vector2(vp.x, vp.y));
            }

            if (_pts.Count < 3) return false;
            ConvexHull(_pts, hull, _chain);
            return hull.Count >= 3;
        }

        static void ConvexHull(List<Vector2> pts, List<Vector2> hull, Vector2[] chain)
        {
            hull.Clear();
            int n = pts.Count;
            if (n < 3) { hull.AddRange(pts); return; }

            pts.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            int k = 0;
            for (int i = 0; i < n; i++)
            {
                while (k >= 2 && Cross(chain[k - 2], chain[k - 1], pts[i]) <= 0f) k--;
                chain[k++] = pts[i];
            }
            int lower = k + 1;
            for (int i = n - 2; i >= 0; i--)
            {
                while (k >= lower && Cross(chain[k - 2], chain[k - 1], pts[i]) <= 0f) k--;
                chain[k++] = pts[i];
            }
            for (int i = 0; i < k - 1; i++) hull.Add(chain[i]); // CCW
        }

        static float Cross(Vector2 o, Vector2 a, Vector2 b) =>
            (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        void OnGUI()
        {
            if (!debugOverlay) return;
            GUI.color = Color.yellow;
            GUI.Label(new Rect(10, 10, 480, 20),
                $"CameraBoxRegionEffect: {_polyCount} polygon(s)  cam={(cam ? cam.name : "<none>")}");
            for (int p = 0; p < _polyCount; p++)
            {
                int vc = (int)_polyInfo[p].x;
                for (int i = 0; i < vc; i++)
                {
                    Vector4 v = _polyVerts[p * MaxVerts + i];
                    float x = v.x * Screen.width;
                    float y = (1f - v.y) * Screen.height;
                    GUI.Box(new Rect(x - 5, y - 5, 10, 10), i.ToString());
                }
            }
            GUI.color = Color.white;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (cam == null) cam = Camera.main;
            UnityEditor.EditorApplication.delayCall += SyncRedrawLayersDeferred;
        }

        // Mirror excludeLayers into the "BoxRegionExcludeRedraw" RenderObjects feature's LayerMask, via
        // reflection (this runtime assembly can't reference the URP runtime assembly directly).
        void SyncRedrawLayersDeferred()
        {
            UnityEditor.EditorApplication.delayCall -= SyncRedrawLayersDeferred;
            if (this == null) return;

            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var rd = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (rd == null) continue;

                if (rd.GetType().GetProperty("rendererFeatures")?.GetValue(rd) is not System.Collections.IEnumerable feats)
                    continue;

                foreach (var f in feats)
                {
                    if (f is not Object uf || uf.GetType().Name != "RenderObjects" || uf.name != "BoxRegionExcludeRedraw")
                        continue;

                    var settings = uf.GetType().GetField("settings")?.GetValue(uf);
                    var filter   = settings?.GetType().GetField("filterSettings")?.GetValue(settings);
                    var lmField  = filter?.GetType().GetField("LayerMask");
                    if (lmField == null) continue;

                    if (((LayerMask)lmField.GetValue(filter)).value != excludeLayers.value)
                    {
                        lmField.SetValue(filter, excludeLayers);
                        UnityEditor.EditorUtility.SetDirty(uf);
                        UnityEditor.EditorUtility.SetDirty(rd);
                    }
                }
            }
        }
#endif
    }
}
