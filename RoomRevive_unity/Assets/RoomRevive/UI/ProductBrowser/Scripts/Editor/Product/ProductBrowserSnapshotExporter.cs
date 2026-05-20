#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RoomRevive.ProductBrowser.EditorTools
{
    /// <summary>
    /// Exports a human- and AI-readable Markdown snapshot of the current ProductBrowser
    /// prefab and data asset state to Snapshots/ProductBrowserUI.md.
    ///
    /// Run this before making any changes so the snapshot reflects current reality.
    /// The snapshot is the ground truth — code defaults may differ from what's in the assets.
    ///
    /// Menu: Tools → RoomRevive → Product Browser → Export Snapshot
    /// </summary>
    public static class ProductBrowserSnapshotExporter
    {
        const string SnapshotPath       = "Assets/RoomRevive/UI/ProductBrowser/Snapshots/ProductBrowserUI.md";
        const string BrowserUIPrefabPath = "Assets/RoomRevive/UI/ProductBrowser/Prefabs/Product/ProductBrowserUI.prefab";
        const string DataRoot           = "Assets/RoomRevive/UI/ProductBrowser/Data/Product";

        [MenuItem("Tools/RoomRevive/Product Browser/Export Snapshot")]
        public static void ExportSnapshot()
        {
            EnsureFolder("Assets/RoomRevive/UI/ProductBrowser/Snapshots");

            var sb = new StringBuilder();
            sb.AppendLine("# ProductBrowser Snapshot");
            sb.AppendLine($"> Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine();
            sb.AppendLine("Use this snapshot as the ground truth for AI-assisted edits.");
            sb.AppendLine("Run Export Snapshot again after any change to keep it current.");
            sb.AppendLine();

            AppendPrefabSection(sb);
            AppendDataSection(sb);

            System.IO.File.WriteAllText(
                System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath)!, SnapshotPath),
                sb.ToString());

            AssetDatabase.Refresh();
            Debug.Log($"[ProductBrowserSnapshotExporter] Snapshot written to {SnapshotPath}");
            EditorUtility.RevealInFinder(SnapshotPath);
        }

        // ── Prefab section ────────────────────────────────────────────────────

        static void AppendPrefabSection(StringBuilder sb)
        {
            sb.AppendLine("## Prefabs");
            sb.AppendLine();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BrowserUIPrefabPath);
            if (prefab == null)
            {
                sb.AppendLine("⚠ ProductBrowserUI.prefab not found. Run **Rebuild All Prefabs** first.");
                sb.AppendLine();
                return;
            }

            sb.AppendLine($"### {prefab.name}");
            sb.AppendLine($"- Path: `{BrowserUIPrefabPath}`");

            ProductBrowserController ctrl = prefab.GetComponent<ProductBrowserController>();
            if (ctrl != null)
            {
                sb.AppendLine($"- Controller: `ProductBrowserController`");
                sb.AppendLine($"  - view wired: {ctrl.view != null}");
                sb.AppendLine($"  - wrapAroundProducts: {ctrl.wrapAroundProducts}");
                sb.AppendLine($"  - keyboardDebug: {ctrl.keyboardDebug}");
            }

            ProductBrowserView view = prefab.GetComponent<ProductBrowserView>();
            if (view != null)
            {
                sb.AppendLine($"- View: `ProductBrowserView`");
                sb.AppendLine($"  - discoverPanel wired: {view.discoverPanel != null}");
                sb.AppendLine($"  - swapPanel wired: {view.swapPanel != null}");
            }

            AppendChildPrefabs(sb);
            sb.AppendLine();
        }

        static void AppendChildPrefabs(StringBuilder sb)
        {
            string[] childPaths = new[]
            {
                "Assets/RoomRevive/UI/ProductBrowser/Prefabs/Product/ProductDiscoverPanel.prefab",
                "Assets/RoomRevive/UI/ProductBrowser/Prefabs/Product/ProductSwapPanel.prefab"
            };

            foreach (string path in childPaths)
            {
                GameObject child = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (child == null)
                {
                    sb.AppendLine($"- ⚠ `{System.IO.Path.GetFileName(path)}` not found");
                    continue;
                }

                sb.AppendLine($"- `{child.name}` (`{path}`)");
                DescribeComponents(sb, child.GetComponents<MonoBehaviour>());
                DescribeHierarchy(sb, child.transform, depth: 2, maxDepth: 4);
            }
        }

        static void DescribeComponents(StringBuilder sb, MonoBehaviour[] comps)
        {
            foreach (MonoBehaviour mb in comps)
            {
                if (mb == null) continue;
                sb.AppendLine($"  - `{mb.GetType().Name}`");
            }
        }

        static void DescribeHierarchy(StringBuilder sb, Transform t, int depth, int maxDepth)
        {
            if (depth > maxDepth) return;
            string indent = new string(' ', depth * 2);
            foreach (Transform child in t)
            {
                sb.Append($"{indent}- `{child.name}`");
                var comps = child.GetComponents<Component>();
                foreach (Component c in comps)
                {
                    if (c == null || c is Transform) continue;
                    sb.Append($" [{c.GetType().Name}]");
                }
                sb.AppendLine();
                DescribeHierarchy(sb, child, depth + 1, maxDepth);
            }
        }

        // ── Data section ──────────────────────────────────────────────────────

        static void AppendDataSection(StringBuilder sb)
        {
            sb.AppendLine("## Data Assets");
            sb.AppendLine();

            string[] guids = AssetDatabase.FindAssets("t:ProductCategoryData", new[] { DataRoot });
            if (guids.Length == 0)
            {
                sb.AppendLine("No `ProductCategoryData` assets found. Run **Create Default Data Assets**.");
                sb.AppendLine();
                return;
            }

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ProductCategoryData cat = AssetDatabase.LoadAssetAtPath<ProductCategoryData>(path);
                if (cat == null) continue;

                sb.AppendLine($"### {cat.displayName} (`{cat.id}`)");
                sb.AppendLine($"- Path: `{path}`");
                sb.AppendLine($"- SwapType: `{cat.swapType}`");
                sb.AppendLine($"- AccentColor: #{ColorToHex(cat.accentColor)}");

                ProductCatalog catalog = cat.catalog;
                if (catalog == null)
                {
                    sb.AppendLine("- Catalog: ⚠ not assigned");
                }
                else
                {
                    sb.AppendLine($"- Catalog: `{catalog.name}` ({catalog.Count} products)");
                    for (int i = 0; i < catalog.Count; i++)
                    {
                        ProductData p = catalog.GetProduct(i);
                        if (p == null) continue;
                        sb.AppendLine($"  - [{i}] `{p.productName}` ({p.brandName}) — {p.fromPrice}");
                    }
                }
                sb.AppendLine();
            }
        }

        // ── Utilities ─────────────────────────────────────────────────────────

        static string ColorToHex(Color c)
        {
            Color32 c32 = c;
            return $"{c32.r:X2}{c32.g:X2}{c32.b:X2}";
        }

        static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
