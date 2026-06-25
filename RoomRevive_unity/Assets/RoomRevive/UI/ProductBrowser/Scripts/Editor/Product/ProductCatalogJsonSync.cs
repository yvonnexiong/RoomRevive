#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RoomRevive.ProductBrowser.EditorTools
{
    /// <summary>
    /// Syncs products from <c>HTML_Editor/admin/catalog.json</c> into ProductData assets, per category.
    ///
    /// Targets (JSON category → Unity catalog):
    ///   • "Fridges"  → ProductCatalog_Fridges    (Miele fridges)
    ///   • "Kitchens" → ProductCatalog_Cabinets    (Nobilia kitchens / cabinets)
    ///
    /// Ownership model:
    ///   • JSON-owned assets carry a <see cref="ProductData.catalogKey"/> (= the JSON item's modelKey,
    ///     or its item id when the item has no modelKey — Kitchens have none). They live under each
    ///     target's <c>FromCatalog</c> folder and are created/overwritten by this tool.
    ///   • Hand-authored assets have an EMPTY catalogKey and are never touched or removed.
    ///
    /// Triggers:
    ///   • Menu: Tools → RoomRevive → Product Browser → Sync from catalog.json
    ///   • Auto: an editor poll re-syncs whenever catalog.json's last-write time changes.
    /// </summary>
    [InitializeOnLoad]
    public static class ProductCatalogJsonSync
    {
        const string CatalogJsonRelative = "../../HTML_Editor/admin/catalog.json";
        const string DataRoot = "Assets/RoomRevive/UI/ProductBrowser/Data/Product";

        // ── Sync targets ─────────────────────────────────────────────────────────
        class SyncTarget
        {
            public string jsonCategory;
            public string catalogPath;
            public string fromCatalogFolder;
            public Action<ProductData, CatalogProduct, string> apply;
        }

        static readonly SyncTarget[] Targets =
        {
            new SyncTarget {
                jsonCategory      = "Fridges",
                catalogPath       = DataRoot + "/ProductCatalog_Fridges.asset",
                fromCatalogFolder = DataRoot + "/Fridges/FromCatalog",
                apply             = ApplyFridge,
            },
            new SyncTarget {
                jsonCategory      = "Kitchens",
                catalogPath       = DataRoot + "/ProductCatalog_Cabinets.asset",
                fromCatalogFolder = DataRoot + "/Cabinets/FromCatalog",
                apply             = ApplyKitchen,
            },
        };

        // ── Auto-watch ────────────────────────────────────────────────────────
        static double _nextCheck;
        static long   _lastWriteTicks;

        static ProductCatalogJsonSync()
        {
            _lastWriteTicks = GetJsonWriteTicks();
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.timeSinceStartup < _nextCheck) return;
            _nextCheck = EditorApplication.timeSinceStartup + 1.5;

            long ticks = GetJsonWriteTicks();
            if (ticks != 0 && ticks != _lastWriteTicks)
            {
                _lastWriteTicks = ticks;
                Debug.Log("[CatalogJsonSync] catalog.json changed — syncing products…");
                SyncAll();
            }
        }

        static long GetJsonWriteTicks()
        {
            string path = GetJsonPath();
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0;
        }

        static string GetJsonPath() =>
            Path.GetFullPath(Path.Combine(Application.dataPath, CatalogJsonRelative));

        // ── Manual trigger ──────────────────────────────────────────────────────
        [MenuItem("Tools/RoomRevive/Product Browser/Sync from catalog.json")]
        public static void SyncAllMenu()
        {
            SyncAll();
            _lastWriteTicks = GetJsonWriteTicks();
        }

        // ── Core sync ───────────────────────────────────────────────────────────
        public static void SyncAll()
        {
            string path = GetJsonPath();
            if (!File.Exists(path)) { Debug.LogWarning($"[CatalogJsonSync] catalog.json not found at {path}"); return; }

            string json;
            try { json = File.ReadAllText(path); }
            catch (Exception e) { Debug.LogError($"[CatalogJsonSync] Failed to read catalog.json: {e.Message}"); return; }

            CatalogRoot root;
            try { root = JsonUtility.FromJson<CatalogRoot>(json); }
            catch (Exception e) { Debug.LogError($"[CatalogJsonSync] Failed to parse catalog.json: {e.Message}"); return; }
            if (root?.items == null) { Debug.LogWarning("[CatalogJsonSync] No 'items' array in catalog.json."); return; }

            foreach (SyncTarget tgt in Targets)
                SyncSingleTarget(tgt, root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void SyncSingleTarget(SyncTarget tgt, CatalogRoot root)
        {
            ProductCatalog catalog = AssetDatabase.LoadAssetAtPath<ProductCatalog>(tgt.catalogPath);
            if (catalog == null) { Debug.LogError($"[CatalogJsonSync] Missing catalog asset: {tgt.catalogPath}"); return; }

            EnsureFolder(tgt.fromCatalogFolder);

            // Index existing JSON-owned assets by catalogKey.
            var existing = new Dictionary<string, ProductData>();
            foreach (string guid in AssetDatabase.FindAssets("t:ProductData", new[] { tgt.fromCatalogFolder }))
            {
                var pd = AssetDatabase.LoadAssetAtPath<ProductData>(AssetDatabase.GUIDToAssetPath(guid));
                if (pd != null && !string.IsNullOrEmpty(pd.catalogKey)) existing[pd.catalogKey] = pd;
            }

            int created = 0, updated = 0;
            foreach (CatalogItem item in root.items)
            {
                if (item == null || item.category != tgt.jsonCategory || item.product == null) continue;

                CatalogProduct p = item.product;
                string key = !string.IsNullOrEmpty(p.modelKey) ? p.modelKey : item.id;
                if (string.IsNullOrEmpty(key)) continue;

                bool isNew = !existing.TryGetValue(key, out ProductData pd) || pd == null;
                if (isNew)
                {
                    pd = ScriptableObject.CreateInstance<ProductData>();
                    string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{tgt.fromCatalogFolder}/Product_{Safe(key)}.asset");
                    AssetDatabase.CreateAsset(pd, assetPath);
                    existing[key] = pd;
                    created++;
                }
                else updated++;

                tgt.apply(pd, p, key);
                EditorUtility.SetDirty(pd);

                if (!catalog.products.Contains(pd)) catalog.products.Add(pd);
            }

            EditorUtility.SetDirty(catalog);
            Debug.Log($"[CatalogJsonSync] {tgt.jsonCategory} → {Path.GetFileName(tgt.catalogPath)}: " +
                      $"{created} created, {updated} updated. Catalog now has {catalog.Count} products.");
        }

        // ── Mapping: Fridges (Miele) ───────────────────────────────────────────
        static void ApplyFridge(ProductData pd, CatalogProduct p, string key)
        {
            pd.catalogKey       = key;
            pd.id               = key;
            pd.brandName        = p.brand;
            pd.productName      = p.name;
            pd.subtitle         = p.subtitle;
            pd.emotionalLine    = !string.IsNullOrEmpty(p.headline) ? p.headline : p.emotionalLine;
            pd.shortDescription = p.description;
            pd.specs            = BuildFridgeSpecs(p);
            pd.fromPrice        = BuildPrice(p);
            // productImage / splatAsset are NOT touched.
        }

        static string[] BuildFridgeSpecs(CatalogProduct p)
        {
            var specs = new List<string>();
            if (p.fridgeCapacity > 0) specs.Add($"{p.fridgeCapacity} L");
            if (p.annualEnergy > 0)   specs.Add($"{p.annualEnergy} kWh/yr");
            if (p.noise > 0)          specs.Add($"{p.noise} dB");
            if (!string.IsNullOrEmpty(p.energyClass)) specs.Add($"Class {p.energyClass}");
            return specs.ToArray();
        }

        // ── Mapping: Kitchens (Nobilia) → Cabinets ──────────────────────────────
        static void ApplyKitchen(ProductData pd, CatalogProduct p, string key)
        {
            pd.catalogKey       = key;
            pd.id               = key;
            pd.brandName        = p.brand;
            pd.productName      = p.name;
            pd.subtitle         = BuildKitchenSubtitle(p);
            pd.emotionalLine    = p.headline;
            pd.shortDescription = p.description;
            pd.specs            = BuildKitchenSpecs(p);
            // Kitchens carry a text priceLabel ("Price on request") rather than a number.
            pd.fromPrice        = !string.IsNullOrEmpty(p.priceLabel) ? p.priceLabel : BuildPrice(p);

            // Resolve the live-splat-editor material filenames for this kitchen.
            // The join key is the material NUMBER, shared by the design element ("fronts-337")
            // and the editor's material file ("337_Aqua_supermatt.jpg").
            //   front   → cabinet ('cab')  → HTML_Editor/CabinetMaterials
            //   worktop → ('wt')           → HTML_Editor/WorktopMaterials
            string frontNum = NumberFromRef(p.designElementRefs?.front) ?? NumberFromText(p.front);
            string wtNum    = NumberFromRef(p.designElementRefs?.worktop) ?? NumberFromText(p.worktop);
            pd.splatCabMaterial = ResolveMaterialFile("CabinetMaterials", frontNum);
            pd.splatWtMaterial  = ResolveMaterialFile("WorktopMaterials", wtNum);
        }

        // ── Material-file resolution (design element number → editor filename) ─────

        /// <summary>"fronts-337" → "337"; null/empty → null.</summary>
        static string NumberFromRef(string reference)
        {
            if (string.IsNullOrEmpty(reference)) return null;
            int dash = reference.LastIndexOf('-');
            return (dash >= 0 && dash < reference.Length - 1) ? reference.Substring(dash + 1) : null;
        }

        /// <summary>"Front 337, Aqua supermatt" → "337"; falls back when no design-element ref exists.</summary>
        static string NumberFromText(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var m = System.Text.RegularExpressions.Regex.Match(text, @"\d+");
            return m.Success ? m.Value : null;
        }

        /// <summary>
        /// Finds the editor material file in HTML_Editor/&lt;folder&gt; whose name starts with
        /// "&lt;number&gt;_" (e.g. number "337" → "337_Aqua_supermatt.jpg"). Returns "" if none.
        /// </summary>
        static string ResolveMaterialFile(string folder, string number)
        {
            if (string.IsNullOrEmpty(number)) return "";
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../HTML_Editor", folder));
            if (!Directory.Exists(dir)) return "";

            string prefix = number + "_";
            string fallback = null;
            foreach (string path in Directory.GetFiles(dir))
            {
                string fn = Path.GetFileName(path);
                if (!fn.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                if (!IsImageFile(fn) || fn.IndexOf("_atlas", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                // Prefer a "normal" file over high-res test variants.
                if (fn.IndexOf("highres", StringComparison.OrdinalIgnoreCase) >= 0) { fallback ??= fn; continue; }
                return fn;
            }
            return fallback ?? "";
        }

        static bool IsImageFile(string fn)
        {
            string l = fn.ToLowerInvariant();
            return l.EndsWith(".jpg") || l.EndsWith(".jpeg") || l.EndsWith(".png") ||
                   l.EndsWith(".webp") || l.EndsWith(".avif");
        }

        static string BuildKitchenSubtitle(CatalogProduct p)
        {
            string type = Cap(p.kitchenType);
            return string.IsNullOrEmpty(type) ? "Kitchen" : $"{type} kitchen";
        }

        static string[] BuildKitchenSpecs(CatalogProduct p)
        {
            var specs = new List<string>();
            if (!string.IsNullOrEmpty(p.color)) specs.Add(p.color);
            if (!string.IsNullOrEmpty(p.handle) && p.handle.IndexOf("handleless", StringComparison.OrdinalIgnoreCase) >= 0)
                specs.Add("Handleless");
            return specs.ToArray();
        }

        // ── Shared helpers ────────────────────────────────────────────────────────
        static string BuildPrice(CatalogProduct p)
        {
            if (p.price <= 0f) return "";
            string cur = string.IsNullOrEmpty(p.currency) ? "" : p.currency + " ";
            string val = Mathf.Approximately(p.price, Mathf.Round(p.price))
                ? Mathf.RoundToInt(p.price).ToString("N0")
                : p.price.ToString("0.##");
            return $"From {cur}{val}";
        }

        static string Cap(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        static string Safe(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
            string leaf   = Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        // ── JSON DTOs (subset; JsonUtility ignores unlisted fields) ─────────────
        [Serializable] class CatalogRoot  { public CatalogItem[] items; }
        [Serializable] class CatalogItem  { public string id; public string category; public CatalogProduct product; }

        [Serializable]
        public class CatalogProduct
        {
            public string brand;
            public string name;
            public string subtitle;
            public string emotionalLine;
            public string headline;
            public string description;
            // Fridges
            public int    fridgeCapacity;
            public int    annualEnergy;
            public int    noise;
            public string energyClass;
            public float  price;
            public string currency;
            public string modelKey;
            // Kitchens (Nobilia)
            public string color;
            public string kitchenType;
            public string handle;
            public string priceLabel;
            public string front;      // e.g. "Front 337, Aqua supermatt"
            public string worktop;    // e.g. "Worktop 198, Sierra oak reproduction"
            public DesignElementRefs designElementRefs;
        }

        [Serializable]
        public class DesignElementRefs
        {
            public string front;      // e.g. "fronts-337"
            public string carcase;    // e.g. "carcaseColours-193"
            public string worktop;    // e.g. "worktops-198"  (may be null/empty)
            public string handle;
        }
    }
}
#endif
