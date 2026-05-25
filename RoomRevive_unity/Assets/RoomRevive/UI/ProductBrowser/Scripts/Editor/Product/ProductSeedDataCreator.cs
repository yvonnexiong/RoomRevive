#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace RoomRevive.ProductBrowser.EditorTools
{
    /// <summary>
    /// Seeds ProductData and ProductCatalog assets from the legacy MetaRay catalogs,
    /// then wires the catalogs into the existing ProductCategoryData assets and
    /// refreshes the ProductBrowserController categories list on the scene prefab.
    ///
    /// Menu: Tools → RoomRevive → Product Browser → Seed Product Data
    /// </summary>
    public static class ProductSeedDataCreator
    {
        const string Base       = "Assets/RoomRevive/UI/ProductBrowser/Data/Product";
        const string PrefabPath = "Assets/RoomRevive/UI/ProductBrowser/Prefabs/Product/ProductBrowserUI.prefab";
        const string FridgeDir = Base + "/Fridges";
        const string CabDir    = Base + "/Cabinets";
        const string LightDir  = Base + "/Lights";

        const string CatFridgesPath  = Base + "/Category_Fridges.asset";
        const string CatCabinetsPath = Base + "/Category_Cabinets.asset";
        const string CatLightsPath   = Base + "/Category_Lights.asset";

        const string CatalogFridgesPath  = Base + "/ProductCatalog_Fridges.asset";
        const string CatalogCabinetsPath = Base + "/ProductCatalog_Cabinets.asset";
        const string CatalogLightsPath   = Base + "/ProductCatalog_Lights.asset";

        [MenuItem("Tools/RoomRevive/Product Browser/Seed Product Data")]
        public static void SeedAll()
        {
            EnsureFolder(FridgeDir);
            EnsureFolder(CabDir);
            EnsureFolder(LightDir);

            ProductCatalog fridgeCatalog  = SeedFridges();
            ProductCatalog cabCatalog     = SeedCabinets();
            ProductCatalog lightCatalog   = SeedLights();

            WireCategoryAsset(CatFridgesPath,  fridgeCatalog,
                accentColor:      new Color(0.96f, 0.65f, 0.14f),
                discoverHeadline: "Your fridge, reimagined.",
                discoverSubline:  "See how a new fridge changes the whole feeling of the room.");

            WireCategoryAsset(CatCabinetsPath, cabCatalog,
                accentColor:      new Color(0.55f, 0.78f, 0.56f),
                discoverHeadline: "Same kitchen. Different soul.",
                discoverSubline:  "Swap the cabinet style without touching the layout.");

            WireCategoryAsset(CatLightsPath,   lightCatalog,
                accentColor:      new Color(0.90f, 0.75f, 0.40f),
                discoverHeadline: "The right light changes everything.",
                discoverSubline:  "Set the mood before a single piece of furniture moves.");

            WirePrefabController(fridgeCatalog, cabCatalog, lightCatalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ProductSeedDataCreator] Done — 9 products, 3 catalogs seeded and wired.");
        }

        // ── Fridges ───────────────────────────────────────────────────────────

        static ProductCatalog SeedFridges()
        {
            ProductData modern = GetOrCreate<ProductData>(FridgeDir + "/Product_FridgeModern.asset", p =>
            {
                p.id            = "fridge_modern";
                p.brandName     = "Nobilia";
                p.productName   = "Modern Fridge";
                p.emotionalLine = "A clean, confident fridge that fits right into a modern kitchen without asking for attention.";
                p.shortDescription = "Four-door stainless fridge with brushed steel surfaces and a high-end modern kitchen look.";
                p.fromPrice     = "From $1,299";
                p.variants      = new[]
                {
                    new ProductVariantData { label = "Brushed Steel",   swatchColor = new Color(0.72f, 0.71f, 0.68f) },
                    new ProductVariantData { label = "Graphite Trim",   swatchColor = new Color(0.16f, 0.16f, 0.16f) },
                };
                p.startsSelectedByDefault = true;
            });

            ProductData nordic = GetOrCreate<ProductData>(FridgeDir + "/Product_FridgeNordic.asset", p =>
            {
                p.id            = "fridge_nordic";
                p.brandName     = "Nobilia";
                p.productName   = "Nordic Tall Fridge";
                p.emotionalLine = "Slim, quiet and tall — a fridge that disappears into a calm Scandinavian kitchen.";
                p.shortDescription = "Bottom-freezer refrigerator with a minimal vertical profile and soft brushed metal finish.";
                p.fromPrice     = "From $899";
                p.variants      = new[]
                {
                    new ProductVariantData { label = "Soft Steel",       swatchColor = new Color(0.76f, 0.75f, 0.72f) },
                    new ProductVariantData { label = "Deep Cabinet Grey", swatchColor = new Color(0.15f, 0.15f, 0.13f) },
                };
            });

            ProductData aurora = GetOrCreate<ProductData>(FridgeDir + "/Product_FridgeAurora.asset", p =>
            {
                p.id            = "fridge_aurora";
                p.brandName     = "Nobilia";
                p.productName   = "Aurora French Door";
                p.emotionalLine = "A statement piece that earns its place — polished, capacious and unmistakably premium.";
                p.shortDescription = "French-door fridge with reflective stainless steel, vertical handles and an integrated water dispenser.";
                p.fromPrice     = "From $1,799";
                p.variants      = new[]
                {
                    new ProductVariantData { label = "Mirror Steel",  swatchColor = new Color(0.78f, 0.77f, 0.74f) },
                    new ProductVariantData { label = "Black Inset",   swatchColor = new Color(0.08f, 0.08f, 0.08f) },
                };
            });

            return GetOrCreateCatalog(CatalogFridgesPath, modern, nordic, aurora);
        }

        // ── Cabinets ──────────────────────────────────────────────────────────

        static ProductCatalog SeedCabinets()
        {
            ProductData walnut = GetOrCreate<ProductData>(CabDir + "/Product_CabinetWalnut.asset", p =>
            {
                p.id            = "cabinet_walnut";
                p.brandName     = "Nobilia";
                p.productName   = "Warm Walnut";
                p.emotionalLine = "Rich, grounded, warm — a kitchen that feels like it has always been there.";
                p.shortDescription = "Full-height walnut cabinet system with continuous vertical grain and a dark stone countertop.";
                p.fromPrice     = "From $8,900";
                p.variants      = new[]
                {
                    new ProductVariantData { label = "Warm Walnut",      swatchColor = new Color(0.54f, 0.33f, 0.18f) },
                    new ProductVariantData { label = "Dark Stone",        swatchColor = new Color(0.25f, 0.27f, 0.26f) },
                    new ProductVariantData { label = "Deep Shadow Trim",  swatchColor = new Color(0.17f, 0.13f, 0.10f) },
                };
                p.startsSelectedByDefault = true;
            });

            ProductData cream = GetOrCreate<ProductData>(CabDir + "/Product_CabinetCream.asset", p =>
            {
                p.id            = "cabinet_cream";
                p.brandName     = "Nobilia";
                p.productName   = "Soft Cream Stone";
                p.emotionalLine = "Light, open and calm — a kitchen that breathes.";
                p.shortDescription = "Cream upper cabinets with grey base units and white marble-effect surfaces for a brighter, quieter feel.";
                p.fromPrice     = "From $7,400";
                p.variants      = new[]
                {
                    new ProductVariantData { label = "Soft Cream",   swatchColor = new Color(0.87f, 0.84f, 0.76f) },
                    new ProductVariantData { label = "Stone Grey",   swatchColor = new Color(0.56f, 0.57f, 0.56f) },
                    new ProductVariantData { label = "White Marble", swatchColor = new Color(0.95f, 0.93f, 0.90f) },
                };
            });

            ProductData sage = GetOrCreate<ProductData>(CabDir + "/Product_CabinetSage.asset", p =>
            {
                p.id            = "cabinet_sage";
                p.brandName     = "Nobilia";
                p.productName   = "Sage Oak";
                p.emotionalLine = "Soft, natural and full of life — a kitchen that feels like a breath of fresh air.";
                p.shortDescription = "Soft sage lower cabinets with cream uppers, oak shelving and a natural wood countertop.";
                p.fromPrice     = "From $7,900";
                p.variants      = new[]
                {
                    new ProductVariantData { label = "Soft Sage",    swatchColor = new Color(0.62f, 0.68f, 0.60f) },
                    new ProductVariantData { label = "Warm Cream",   swatchColor = new Color(0.86f, 0.84f, 0.75f) },
                    new ProductVariantData { label = "Natural Oak",  swatchColor = new Color(0.72f, 0.54f, 0.32f) },
                };
            });

            return GetOrCreateCatalog(CatalogCabinetsPath, walnut, cream, sage);
        }

        // ── Lights ────────────────────────────────────────────────────────────

        static ProductCatalog SeedLights()
        {
            ProductData calm = GetOrCreate<ProductData>(LightDir + "/Product_LightCalm.asset", p =>
            {
                p.id            = "light_calm";
                p.brandName     = "Neuhaus";
                p.productName   = "Ambient Warmth";
                p.emotionalLine = "Soft, warm light that invites you to slow down and actually be in the room.";
                p.shortDescription = "2700K warm pendant with a diffused glow — designed for end-of-day calm and decompression.";
                p.fromPrice     = "From $490";
                p.variants      = new[]
                {
                    new ProductVariantData { label = "Matte Black",  swatchColor = new Color(0.12f, 0.12f, 0.12f) },
                    new ProductVariantData { label = "Brushed Brass", swatchColor = new Color(0.80f, 0.68f, 0.40f) },
                };
                p.startsSelectedByDefault = true;
            });

            ProductData host = GetOrCreate<ProductData>(LightDir + "/Product_LightHost.asset", p =>
            {
                p.id            = "light_host";
                p.brandName     = "Neuhaus";
                p.productName   = "Social Glow";
                p.emotionalLine = "Warm pools of light that make every meal feel like an occasion worth sharing.";
                p.shortDescription = "3000K layered warm lighting with adjustable pendants — built for gathering and connection.";
                p.fromPrice     = "From $620";
                p.variants      = new[]
                {
                    new ProductVariantData { label = "Warm White",  swatchColor = new Color(0.95f, 0.88f, 0.70f) },
                    new ProductVariantData { label = "Smoked Glass", swatchColor = new Color(0.45f, 0.42f, 0.38f) },
                };
            });

            ProductData focus = GetOrCreate<ProductData>(LightDir + "/Product_LightFocus.asset", p =>
            {
                p.id            = "light_focus";
                p.brandName     = "Neuhaus";
                p.productName   = "Sharp Focus";
                p.emotionalLine = "Clean, precise light that sharpens the counter and gets you out of the kitchen faster.";
                p.shortDescription = "4000K neutral task lighting with directional strips — engineered for efficiency and clarity.";
                p.fromPrice     = "From $380";
                p.variants      = new[]
                {
                    new ProductVariantData { label = "Cool White",  swatchColor = new Color(0.88f, 0.92f, 0.98f) },
                    new ProductVariantData { label = "Matte White",  swatchColor = new Color(0.94f, 0.94f, 0.94f) },
                };
            });

            return GetOrCreateCatalog(CatalogLightsPath, calm, host, focus);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        static T GetOrCreate<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            T asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static ProductCatalog GetOrCreateCatalog(string path, params ProductData[] products)
        {
            ProductCatalog existing = AssetDatabase.LoadAssetAtPath<ProductCatalog>(path);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<ProductCatalog>();
                AssetDatabase.CreateAsset(existing, path);
            }
            existing.products.Clear();
            foreach (ProductData p in products)
                if (p != null) existing.products.Add(p);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static void WireCategoryAsset(string path, ProductCatalog catalog,
            Color accentColor, string discoverHeadline, string discoverSubline)
        {
            ProductCategoryData cat = AssetDatabase.LoadAssetAtPath<ProductCategoryData>(path);
            if (cat == null) { Debug.LogWarning($"[ProductSeedDataCreator] Category not found: {path}"); return; }
            cat.catalog          = catalog;
            cat.accentColor      = accentColor;
            cat.discoverHeadline = discoverHeadline;
            cat.discoverSubline  = discoverSubline;
            EditorUtility.SetDirty(cat);
        }

        static void WirePrefabController(ProductCatalog fridgeCatalog, ProductCatalog cabCatalog, ProductCatalog lightCatalog)
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogWarning("[ProductSeedDataCreator] ProductBrowserUI.prefab not found — skipping prefab wiring.");
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ProductBrowserController ctrl = contents.GetComponent<ProductBrowserController>();
                if (ctrl == null) { Debug.LogWarning("[ProductSeedDataCreator] No ProductBrowserController on prefab root."); return; }

                ctrl.fridgesCategory  = AssetDatabase.LoadAssetAtPath<ProductCategoryData>(CatFridgesPath);
                ctrl.cabinetsCategory = AssetDatabase.LoadAssetAtPath<ProductCategoryData>(CatCabinetsPath);
                ctrl.lightsCategory   = AssetDatabase.LoadAssetAtPath<ProductCategoryData>(CatLightsPath);
                EditorUtility.SetDirty(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                Debug.Log("[ProductSeedDataCreator] Prefab controller wired with 3 categories.");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf   = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf))
                AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
