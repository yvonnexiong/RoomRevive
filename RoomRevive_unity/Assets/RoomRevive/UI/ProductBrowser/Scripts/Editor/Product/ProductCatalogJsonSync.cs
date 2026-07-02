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
            // When true, each synced asset file is named "Product_<id> <productName>" — the hash id is
            // KEPT and the product name appended for readability (e.g. "Product_9a39b52d… NOVALUX 519").
            // Safe: sync re-finds assets by catalogKey and the catalog references them by GUID — both
            // survive a rename.
            public bool nameByProductName;
        }

        static readonly SyncTarget[] Targets =
        {
            new SyncTarget {
                jsonCategory      = "Fridges",
                catalogPath       = DataRoot + "/ProductCatalog_Fridges.asset",
                fromCatalogFolder = DataRoot + "/Fridges/FromCatalog",
                apply             = ApplyFridge,
                // Fridge assets keep their modelKey name (matches the 3D model / scene object).
            },
            new SyncTarget {
                jsonCategory      = "Kitchens",
                catalogPath       = DataRoot + "/ProductCatalog_Cabinets.asset",
                fromCatalogFolder = DataRoot + "/Cabinets/FromCatalog",
                apply             = ApplyKitchen,
                nameByProductName = true,   // Kitchens have hash ids → append the product name for readability.
            },
            ApplianceTarget("Hoods",          ApplyHood),
            ApplianceTarget("Cooktops",       ApplyCooktop),
            ApplianceTarget("CoffeeMachines", ApplyCoffeeMachine),
            ApplianceTarget("Microwaves",     ApplyMicrowave),
            ApplianceTarget("Dishwashers",    ApplyDishwasher),
        };

        // Miele appliance categories — same pattern as Fridges; asset name = "Product_<id> <name>".
        static SyncTarget ApplianceTarget(string category, Action<ProductData, CatalogProduct, string> apply) =>
            new SyncTarget
            {
                jsonCategory      = category,
                catalogPath       = DataRoot + "/ProductCatalog_" + category + ".asset",
                fromCatalogFolder = DataRoot + "/" + category + "/FromCatalog",
                apply             = apply,
                nameByProductName = true,
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

                if (tgt.nameByProductName) RenameAssetWithProductName(pd, key);

                if (!catalog.products.Contains(pd)) catalog.products.Add(pd);
            }

            catalog.SortPinnedFirst();   // keep pinned products at the front after a sync
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
            if (p.annualEnergy > 0)   specs.Add($"{Num(p.annualEnergy)} kWh/yr");
            if (p.noise > 0)          specs.Add($"{p.noise} dB");
            if (!string.IsNullOrEmpty(p.energyClass)) specs.Add($"Class {p.energyClass}");
            return specs.ToArray();
        }

        // ── Mapping: Miele appliances (Hoods, Cooktops, Coffee, Microwaves, Dishwashers) ──────
        static void ApplyApplianceCommon(ProductData pd, CatalogProduct p, string key, string[] specs)
        {
            pd.catalogKey       = key;
            pd.id               = key;
            pd.brandName        = p.brand;
            pd.productName      = p.name;
            pd.subtitle         = p.subtitle;
            pd.emotionalLine    = !string.IsNullOrEmpty(p.headline) ? p.headline : p.emotionalLine;
            pd.shortDescription = p.description;
            pd.specs            = specs;
            pd.fromPrice        = BuildPrice(p);
        }

        static void ApplyHood(ProductData pd, CatalogProduct p, string key)
        {
            var s = new List<string>();
            if (p.airflow > 0)      s.Add($"{Num(p.airflow)} m³/h");
            if (p.noise > 0)        s.Add($"{p.noise} dB");
            if (!string.IsNullOrEmpty(p.energyClass)) s.Add($"Class {p.energyClass}");
            if (p.annualEnergy > 0) s.Add($"{Num(p.annualEnergy)} kWh/yr");
            ApplyApplianceCommon(pd, p, key, s.ToArray());
        }

        static void ApplyCooktop(ProductData pd, CatalogProduct p, string key)
        {
            var s = new List<string>();
            if (p.zones > 0)        s.Add($"{p.zones} zones");
            if (p.induction)        s.Add("Induction");
            if (p.totalPowerKw > 0) s.Add($"{Num(p.totalPowerKw)} kW");
            if (!string.IsNullOrEmpty(p.energyClass)) s.Add($"Class {p.energyClass}");
            ApplyApplianceCommon(pd, p, key, s.ToArray());
        }

        static void ApplyCoffeeMachine(ProductData pd, CatalogProduct p, string key)
        {
            var s = new List<string>();
            if (!string.IsNullOrEmpty(p.color))      s.Add(p.color);
            if (!string.IsNullOrEmpty(p.dimensions)) s.Add(p.dimensions);
            ApplyApplianceCommon(pd, p, key, s.ToArray());
        }

        static void ApplyMicrowave(ProductData pd, CatalogProduct p, string key)
        {
            var s = new List<string>();
            if (p.capacityL > 0)       s.Add($"{p.capacityL} L");
            if (p.microwavePowerW > 0) s.Add($"{p.microwavePowerW} W");
            if (p.grill)               s.Add("Grill");
            ApplyApplianceCommon(pd, p, key, s.ToArray());
        }

        static void ApplyDishwasher(ProductData pd, CatalogProduct p, string key)
        {
            var s = new List<string>();
            if (p.placeSettings > 0) s.Add($"{p.placeSettings} settings");
            if (!string.IsNullOrEmpty(p.energyClass)) s.Add($"Class {p.energyClass}");
            if (p.waterPerCycle > 0) s.Add($"{Num(p.waterPerCycle)} L/cycle");
            if (!string.IsNullOrEmpty(p.noiseClass)) s.Add($"Noise {p.noiseClass}");
            ApplyApplianceCommon(pd, p, key, s.ToArray());
        }

        // Whole numbers render without a decimal; fractional keep one place (invariant → always a dot).
        static string Num(float v) =>
            Mathf.Approximately(v, Mathf.Round(v))
                ? Mathf.RoundToInt(v).ToString()
                : v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

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

        // Names the asset "Product_<id> <productName>" — keeps the hash id, appends the readable name.
        // Idempotent (recomputed from id + name each sync), and unique because the id is unique.
        static void RenameAssetWithProductName(ProductData pd, string key)
        {
            string path = AssetDatabase.GetAssetPath(pd);
            if (string.IsNullOrEmpty(path)) return;

            string name = SafeAssetName(pd.productName);
            string desired = string.IsNullOrEmpty(name) ? $"Product_{Safe(key)}" : $"Product_{Safe(key)} {name}";
            if (Path.GetFileNameWithoutExtension(path) == desired) return;

            string err = AssetDatabase.RenameAsset(path, desired);
            if (!string.IsNullOrEmpty(err))
                Debug.LogWarning($"[CatalogJsonSync] Could not rename '{path}' → '{desired}': {err}");
        }

        // Asset names allow spaces; only strip characters illegal in a filename.
        static string SafeAssetName(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Trim();
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
            public float  annualEnergy;     // float: fridges are whole (239), hoods fractional (91.6)
            public int    noise;
            public string energyClass;
            public float  price;
            public string currency;
            public string modelKey;
            // Appliances (Hoods / Cooktops / CoffeeMachines / Microwaves / Dishwashers)
            public string dimensions;
            public float  airflow;          // hoods (m³/h)
            public int    zones;            // cooktops
            public bool   induction;        // cooktops
            public float  totalPowerKw;     // cooktops
            public int    capacityL;        // microwaves
            public int    microwavePowerW;  // microwaves
            public bool   grill;            // microwaves
            public int    placeSettings;    // dishwashers
            public float  waterPerCycle;    // dishwashers
            public string noiseClass;       // dishwashers
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
