using System.IO;
using System.Text;
using UnityEngine;
using RoomRevive.SplatEditorBridge;

namespace RoomRevive.ProductBrowser
{
    /// <summary>
    /// Bridges the cabinet product browser to the live splat editor: when the selected kitchen
    /// changes, it asks the editor to swap the cabinet-front + worktop materials via
    /// <see cref="SplatMaterialSwapClient"/>.
    ///
    /// The exact editor filenames live on each kitchen's <see cref="ProductData"/>
    /// (<c>splatCabMaterial</c> / <c>splatWtMaterial</c>), baked there by the catalog sync from the
    /// kitchen's design-element references. This router only forwards them — no catalog parsing at runtime.
    ///
    /// Setup: put this on the Cabinet <c>ProductBrowserUI</c> (next to the controller) and assign /
    /// auto-find a <see cref="SplatMaterialSwapClient"/>. Harmless on the fridge UI — fridge products
    /// have empty splat-material fields, so nothing is sent.
    /// </summary>
    [DisallowMultipleComponent]
    public class CabinetMaterialSwapRouter : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Controller to listen to. Auto-found on this GameObject if empty.")]
        public ProductBrowserController controller;

        [Tooltip("HTTP client that talks to the splat editor. Auto-found on this GameObject / scene if empty.")]
        public SplatMaterialSwapClient swapClient;

        [Header("Behavior")]
        [Tooltip("Swap as soon as the displayed product changes (preview). If false, only an explicit " +
                 "SwapForSelected() call triggers a swap — wire that to the CTA / confirm instead.")]
        public bool swapOnProductChange = true;

        [Tooltip("Log each swap request.")]
        public bool debugLogs = true;

        [Header("Prerendered Cabinets")]
        [Tooltip("If a prerendered .spz exists for the combo, load it directly (smooth, no editor/server round-trip) " +
                 "instead of asking the web editor to recolor. Falls back to the editor swap when none exists. " +
                 "OFF = always use the HTML editor.")]
        public bool preferPrerendered = true;
        [Tooltip("Folder holding the prerendered .spz files, relative to the Unity project root.")]
        public string prerenderedFolderRelative = "../HTML_Editor/Prerendered Cabinets";
        [Tooltip("The live .spz LiveSplatLoader watches, relative to the Unity project root. A prerendered " +
                 "combo is copied here so the existing transition pipeline plays it (via the file watcher).")]
        public string liveSpzRelativePath = "LiveSplat/kitchen-copy.spz";

        [Header("Demo: first-N swaps use prerendered files")]
        [Tooltip("For the first N UI swaps, ignore the selected product and just cycle through the .spz " +
                 "files in the prerendered folder (smooth, no server). After N, normal behavior resumes.")]
        public bool useFirstNPrerendered = true;
        [Tooltip("How many opening swaps play from the prerendered folder in order.")]
        public int firstNPrerenderedCount = 5;
        int _uiSwapIndex;

        [Header("Status (read-only)")]
        [Tooltip("Number of onProductChanged events received since enable.")]
        public int productChangeCount;
        [Tooltip("Number of swaps actually forwarded to the editor.")]
        public int swapCount;
        [Tooltip("Most recent product + materials handled.")]
        public string lastStatus = "idle";

        void OnEnable()
        {
            ResolveRefs();
            if (controller != null)
            {
                controller.onProductChanged.AddListener(OnProductChanged);
                if (debugLogs) Debug.Log("[CabinetMaterialSwapRouter] Subscribed to onProductChanged.", this);
            }
            else
            {
                Debug.LogWarning("[CabinetMaterialSwapRouter] No ProductBrowserController found — swaps won't fire.", this);
            }
        }

        void OnDisable()
        {
            if (controller != null) controller.onProductChanged.RemoveListener(OnProductChanged);
        }

        void ResolveRefs()
        {
            if (controller == null) controller = GetComponent<ProductBrowserController>();
            if (swapClient == null) swapClient = GetComponent<SplatMaterialSwapClient>();
            if (swapClient == null)
#if UNITY_2022_2_OR_NEWER
                swapClient = FindFirstObjectByType<SplatMaterialSwapClient>();
#else
                swapClient = FindObjectOfType<SplatMaterialSwapClient>();
#endif
        }

        void OnProductChanged(ProductData product)
        {
            productChangeCount++;
            if (debugLogs)
                Debug.Log($"[CabinetMaterialSwapRouter] onProductChanged #{productChangeCount} → {SafeName(product)} " +
                          $"(cab='{product?.splatCabMaterial}', wt='{product?.splatWtMaterial}')", this);
            if (swapOnProductChange) ApplySwap(product);
        }

        /// <summary>Swaps materials for the controller's currently selected product (wire to a button / confirm).</summary>
        public void SwapForSelected() =>
            ApplySwap(controller != null ? controller.SelectedProduct : null);

        void ApplySwap(ProductData product)
        {
            // The client only issues requests in Play mode; skip silently otherwise so editor
            // navigation doesn't spam warnings.
            if (!Application.isPlaying || product == null) return;

            string cab = product.splatCabMaterial;
            string wt  = product.splatWtMaterial;
            if (string.IsNullOrEmpty(cab) && string.IsNullOrEmpty(wt))
            {
                lastStatus = $"skipped {SafeName(product)} — no splat materials (not a kitchen?)";
                return; // not a kitchen / nothing to swap
            }

            // Demo: play the first N UI swaps straight from the prerendered folder (in order), so the
            // opening transitions are smooth regardless of whether that exact combo was baked.
            if (useFirstNPrerendered && TryApplyFirstNPrerendered(product))
                return;

            // Prefer a prerendered .spz — instant + smooth, no web-editor recolor.
            if (preferPrerendered && TryApplyPrerendered(cab, wt, product))
                return;

            if (swapClient == null) ResolveRefs();
            if (swapClient == null)
            {
                lastStatus = "no SplatMaterialSwapClient";
                Debug.LogWarning("[CabinetMaterialSwapRouter] No SplatMaterialSwapClient found — add one to the scene.", this);
                return;
            }

            swapCount++;
            lastStatus = $"#{swapCount} {SafeName(product)} → cab='{cab}', wt='{wt}' (editor)";
            if (debugLogs) Debug.Log($"[CabinetMaterialSwapRouter] {lastStatus}", this);

            swapClient.SwapMaterials(cab, wt);
        }

        // Demo mode: for the first N swaps, use the prerendered folder's files in filename order,
        // ignoring which product was selected. Returns false once past N or if the folder is empty.
        bool TryApplyFirstNPrerendered(ProductData product)
        {
            if (_uiSwapIndex >= Mathf.Max(0, firstNPrerenderedCount)) return false;

            string dir = ResolveProjectPath(prerenderedFolderRelative);
            if (!Directory.Exists(dir)) return false;

            string[] files = Directory.GetFiles(dir, "*.spz");
            if (files.Length == 0) return false;
            System.Array.Sort(files, System.StringComparer.OrdinalIgnoreCase);

            string src = files[_uiSwapIndex % files.Length];
            if (!CopyToLiveSpz(src)) return false;

            _uiSwapIndex++;
            swapCount++;
            lastStatus = $"#{swapCount} {SafeName(product)} → {Path.GetFileName(src)} (prerendered {_uiSwapIndex}/{firstNPrerenderedCount})";
            if (debugLogs) Debug.Log($"[CabinetMaterialSwapRouter] {lastStatus}", this);
            return true;
        }

        // Copies a .spz over the live file so the LiveSplatLoader watcher drives the reveal.
        bool CopyToLiveSpz(string src)
        {
            try
            {
                string dst = ResolveProjectPath(liveSpzRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.Copy(src, dst, true);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CabinetMaterialSwapRouter] copy to live .spz failed ({e.Message}).", this);
                return false;
            }
        }

        // Copies the prerendered .spz for this combo over the live file so LiveSplatLoader + SplatManager
        // play the normal transition. Returns false (→ editor fallback) if no prerendered file exists.
        bool TryApplyPrerendered(string cab, string wt, ProductData product)
        {
            string key = PrerenderKey(cab, wt);
            if (string.IsNullOrEmpty(key)) return false;

            string src = ResolveProjectPath(Path.Combine(prerenderedFolderRelative, key + ".spz"));
            if (!File.Exists(src)) return false;

            if (CopyToLiveSpz(src))
            {
                // The LiveSplatLoader that watches this file (Drive Transition on) detects the change
                // and drives the reveal, exactly like the editor auto-save.
                swapCount++;
                lastStatus = $"#{swapCount} {SafeName(product)} → {key}.spz (prerendered)";
                if (debugLogs) Debug.Log($"[CabinetMaterialSwapRouter] {lastStatus}", this);
                return true;
            }
            return false;
        }

        static string ResolveProjectPath(string relative) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));

        // Must match the editor's safeName() and gen_prerender_combos.py: strip the extension, replace
        // any run of chars outside [A-Za-z0-9._-] with '_', and join cab + "__" + wt (wt omitted if empty).
        static string PrerenderKey(string cab, string wt)
        {
            string c = Sanitize(cab);
            if (string.IsNullOrEmpty(c)) return null;
            string w = Sanitize(wt);
            return string.IsNullOrEmpty(w) ? c : c + "__" + w;
        }

        static string Sanitize(string file)
        {
            if (string.IsNullOrEmpty(file)) return "";
            int dot = file.LastIndexOf('.');
            string s = dot > 0 ? file.Substring(0, dot) : file;
            var sb = new StringBuilder(s.Length);
            bool lastUnderscore = false;
            foreach (char ch in s)
            {
                bool ok = (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') ||
                          (ch >= '0' && ch <= '9') || ch == '.' || ch == '_' || ch == '-';
                if (ok) { sb.Append(ch); lastUnderscore = false; }
                else if (!lastUnderscore) { sb.Append('_'); lastUnderscore = true; }
            }
            return sb.ToString();
        }

        static string SafeName(ProductData p) =>
            p == null ? "<null>" : (!string.IsNullOrEmpty(p.productName) ? p.productName : p.name);
    }
}
