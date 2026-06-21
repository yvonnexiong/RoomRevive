#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RoomRevive.ProductBrowser.EditorTools
{
    /// <summary>
    /// Syncs Fridge products from <c>HTML_Editor/admin/catalog.json</c> into ProductData assets.
    ///
    /// Ownership model:
    ///   • JSON-owned assets carry a <see cref="ProductData.catalogKey"/> (= the JSON item's modelKey).
    ///     They live under <see cref="FromCatalogFolder"/> and are created/overwritten by this tool.
    ///   • Hand-authored assets have an EMPTY catalogKey and are never touched or removed.
    ///
    /// Triggers:
    ///   • Menu: Tools → RoomRevive → Product Browser → Sync Fridges from catalog.json
    ///   • Auto: an editor poll re-syncs whenever catalog.json's last-write time changes.
    /// </summary>
    [InitializeOnLoad]
    public static class ProductCatalogJsonSync
    {
        // catalog.json lives in the repo root's HTML_Editor (two levels up from Assets:
        // .../RoomRevive_unity/Assets → .../RoomRevive_unity → .../RoomRevive/HTML_Editor/...).
        const string CatalogJsonRelative = "../../HTML_Editor/admin/catalog.json";
        const string FridgeCatalogPath   = "Assets/RoomRevive/UI/ProductBrowser/Data/Product/ProductCatalog_Fridges.asset";
        const string FromCatalogFolder   = "Assets/RoomRevive/UI/ProductBrowser/Data/Product/Fridges/FromCatalog";
        const string JsonCategoryFridges = "Fridges";

        // ── Auto-watch ────────────────────────────────────────────────────────
        static double _nextCheck;
        static long   _lastWriteTicks;

        static ProductCatalogJsonSync()
        {
            // Baseline now so we only sync on real changes, not on every domain reload.
            _lastWriteTicks = GetJsonWriteTicks();
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.timeSinceStartup < _nextCheck) return;
            _nextCheck = EditorApplication.timeSinceStartup + 1.5; // throttle

            long ticks = GetJsonWriteTicks();
            if (ticks != 0 && ticks != _lastWriteTicks)
            {
                _lastWriteTicks = ticks;
                Debug.Log("[CatalogJsonSync] catalog.json changed — syncing fridges…");
                SyncFridges();
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
        [MenuItem("Tools/RoomRevive/Product Browser/Sync Fridges from catalog.json")]
        public static void SyncFridgesMenu()
        {
            SyncFridges();
            _lastWriteTicks = GetJsonWriteTicks(); // don't immediately auto re-run
        }

        // ── Core sync ───────────────────────────────────────────────────────────
        public static void SyncFridges()
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

            ProductCatalog catalog = AssetDatabase.LoadAssetAtPath<ProductCatalog>(FridgeCatalogPath);
            if (catalog == null) { Debug.LogError($"[CatalogJsonSync] Missing catalog asset: {FridgeCatalogPath}"); return; }

            EnsureFolder(FromCatalogFolder);

            // Index existing JSON-owned assets by catalogKey.
            var existing = new Dictionary<string, ProductData>();
            foreach (string guid in AssetDatabase.FindAssets("t:ProductData", new[] { FromCatalogFolder }))
            {
                var pd = AssetDatabase.LoadAssetAtPath<ProductData>(AssetDatabase.GUIDToAssetPath(guid));
                if (pd != null && !string.IsNullOrEmpty(pd.catalogKey)) existing[pd.catalogKey] = pd;
            }

            int created = 0, updated = 0;
            foreach (CatalogItem item in root.items)
            {
                if (item == null || item.category != JsonCategoryFridges || item.product == null) continue;

                CatalogProduct p = item.product;
                string key = !string.IsNullOrEmpty(p.modelKey) ? p.modelKey : item.id;
                if (string.IsNullOrEmpty(key)) continue;

                bool isNew = !existing.TryGetValue(key, out ProductData pd) || pd == null;
                if (isNew)
                {
                    pd = ScriptableObject.CreateInstance<ProductData>();
                    string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{FromCatalogFolder}/Product_{Safe(key)}.asset");
                    AssetDatabase.CreateAsset(pd, assetPath);
                    existing[key] = pd;
                    created++;
                }
                else updated++;

                ApplyProduct(pd, p, key);
                EditorUtility.SetDirty(pd);

                if (!catalog.products.Contains(pd)) catalog.products.Add(pd);
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CatalogJsonSync] Fridges synced — {created} created, {updated} updated. " +
                      $"ProductCatalog_Fridges now has {catalog.Count} products.");
        }

        // ── Mapping: catalog.json → ProductData ─────────────────────────────────
        static void ApplyProduct(ProductData pd, CatalogProduct p, string key)
        {
            pd.catalogKey       = key;
            pd.id               = key;
            pd.brandName        = p.brand;
            pd.productName      = p.name;
            pd.subtitle         = p.subtitle;
            pd.emotionalLine    = !string.IsNullOrEmpty(p.headline) ? p.headline : p.emotionalLine; // big feature line
            pd.shortDescription = p.description;
            pd.specs            = BuildSpecs(p);
            pd.fromPrice        = BuildPrice(p);
            // productImage / splatAsset are NOT touched — catalog.json has no UI sprite; keep any manual assignment.
        }

        static string[] BuildSpecs(CatalogProduct p)
        {
            var specs = new List<string>();
            if (p.fridgeCapacity > 0) specs.Add($"{p.fridgeCapacity} L");
            if (p.annualEnergy > 0)   specs.Add($"{p.annualEnergy} kWh/yr");
            if (p.noise > 0)          specs.Add($"{p.noise} dB");
            if (!string.IsNullOrEmpty(p.energyClass)) specs.Add($"Class {p.energyClass}");
            return specs.ToArray();
        }

        static string BuildPrice(CatalogProduct p)
        {
            if (p.price <= 0f) return "";
            string cur = string.IsNullOrEmpty(p.currency) ? "" : p.currency + " ";
            string val = Mathf.Approximately(p.price, Mathf.Round(p.price))
                ? Mathf.RoundToInt(p.price).ToString("N0")
                : p.price.ToString("0.##");
            return $"From {cur}{val}";
        }

        // ── Helpers ─────────────────────────────────────────────────────────────
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
        class CatalogProduct
        {
            public string brand;
            public string name;
            public string subtitle;
            public string emotionalLine;
            public string headline;
            public string description;
            public int    fridgeCapacity;
            public int    freezerCapacity;
            public int    annualEnergy;
            public int    noise;
            public string energyClass;
            public string dimensions;
            public string color;
            public float  price;
            public string currency;
            public string modelKey;
        }
    }
}
#endif
