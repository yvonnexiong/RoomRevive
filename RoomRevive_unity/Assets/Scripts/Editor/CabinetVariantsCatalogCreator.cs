using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class CabinetVariantsCatalogCreator : MonoBehaviour
{
#if UNITY_EDITOR
    private const string CatalogFolder = "Assets/RoomRevive/Data/NewCabinets";
    private const string ImageFolder = "Assets/RoomRevive/Data/NewCabinets/CabinetImages";
    private const string CatalogAssetPath = CatalogFolder + "/CabinetVariantsCatalog.asset";

    private static readonly string[] SupportedImageExtensions =
    {
        ".png",
        ".jpg",
        ".jpeg"
    };

    private bool queuedPathLog;

    private void Reset()
    {
        QueueLogAttachedObjectAndParents("Reset");
    }

    private void OnEnable()
    {
        QueueLogAttachedObjectAndParents("OnEnable");
    }

    private void OnValidate()
    {
        QueueLogAttachedObjectAndParents("OnValidate");
    }

    [ContextMenu("XRCC/Log Object And Parents")]
    private void LogObjectAndParentsFromContextMenu()
    {
        LogAttachedObjectAndParents("Context Menu");
    }

    [ContextMenu("XRCC/Create Cabinet Variants Catalog")]
    private void CreateCabinetVariantsCatalogFromContextMenu()
    {
        CreateExampleCabinetVariantsCatalog();
    }

    private void QueueLogAttachedObjectAndParents(string reason)
    {
        if (queuedPathLog)
        {
            return;
        }

        queuedPathLog = true;

        EditorApplication.delayCall += () =>
        {
            queuedPathLog = false;

            if (this == null)
            {
                return;
            }

            LogAttachedObjectAndParents(reason);
        };
    }

    private void LogAttachedObjectAndParents(string reason)
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine($"<b>[CabinetVariantsCatalogCreator]</b> This script is attached to a GameObject.");
        sb.AppendLine($"Reason: {reason}");
        sb.AppendLine();
        sb.AppendLine($"GameObject: {gameObject.name}");
        sb.AppendLine($"Full hierarchy path: {GetTransformPath(transform)}");
        sb.AppendLine();

        if (gameObject.scene.IsValid())
        {
            sb.AppendLine($"Scene name: {gameObject.scene.name}");
            sb.AppendLine($"Scene path: {gameObject.scene.path}");
        }
        else
        {
            sb.AppendLine("Scene: Not part of a normal open scene.");
        }

        sb.AppendLine();

        string prefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
        if (!string.IsNullOrWhiteSpace(prefabAssetPath))
        {
            sb.AppendLine($"Prefab asset path: {prefabAssetPath}");
        }

        string directAssetPath = AssetDatabase.GetAssetPath(gameObject);
        if (!string.IsNullOrWhiteSpace(directAssetPath))
        {
            sb.AppendLine($"Direct asset path: {directAssetPath}");
        }

        sb.AppendLine();
        sb.AppendLine("Object and parents, child to root:");

        Transform current = transform;
        int depth = 0;

        while (current != null)
        {
            sb.AppendLine($"{depth}: {current.name}");
            current = current.parent;
            depth++;
        }

        Debug.Log(sb.ToString(), gameObject);

        Selection.activeGameObject = gameObject;
        EditorGUIUtility.PingObject(gameObject);
    }

    private static string GetTransformPath(Transform target)
    {
        if (target == null)
        {
            return "<null>";
        }

        List<string> names = new List<string>();
        Transform current = target;

        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    [MenuItem("XRCC/Furniture/Find Attached CabinetVariantsCatalogCreator In Loaded Objects")]
    public static void FindAttachedInstancesInLoadedObjects()
    {
        CabinetVariantsCatalogCreator[] instances =
            Resources.FindObjectsOfTypeAll<CabinetVariantsCatalogCreator>();

        if (instances == null || instances.Length == 0)
        {
            Debug.LogWarning("[CabinetVariantsCatalogCreator] No loaded instances found. It may be inside a prefab/scene that is not currently open.");
            return;
        }

        Debug.Log($"[CabinetVariantsCatalogCreator] Found {instances.Length} loaded instance(s).");

        foreach (CabinetVariantsCatalogCreator instance in instances)
        {
            if (instance == null)
            {
                continue;
            }

            instance.LogAttachedObjectAndParents("Manual finder");
        }
    }

    [MenuItem("XRCC/Furniture/Find References To CabinetVariantsCatalogCreator GUID")]
    public static void FindReferencesToThisScriptGuid()
    {
        string[] scriptGuids = AssetDatabase.FindAssets("CabinetVariantsCatalogCreator t:MonoScript");

        if (scriptGuids == null || scriptGuids.Length == 0)
        {
            Debug.LogError("[CabinetVariantsCatalogCreator] Could not find this script as a MonoScript asset.");
            return;
        }

        int totalMatches = 0;

        foreach (string scriptGuid in scriptGuids)
        {
            string scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuid);

            if (!scriptPath.EndsWith("CabinetVariantsCatalogCreator.cs"))
            {
                continue;
            }

            Debug.Log($"[CabinetVariantsCatalogCreator] Searching for GUID references:\nGUID: {scriptGuid}\nScript: {scriptPath}");

            string[] allFiles = Directory.GetFiles(Application.dataPath, "*.*", SearchOption.AllDirectories);

            foreach (string fullPath in allFiles)
            {
                string extension = Path.GetExtension(fullPath).ToLowerInvariant();

                if (extension != ".unity" && extension != ".prefab" && extension != ".asset")
                {
                    continue;
                }

                string fileText;

                try
                {
                    fileText = File.ReadAllText(fullPath);
                }
                catch
                {
                    continue;
                }

                if (!fileText.Contains(scriptGuid))
                {
                    continue;
                }

                string assetPath = FullPathToAssetPath(fullPath);
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);

                totalMatches++;

                Debug.LogWarning(
                    $"<b>[CabinetVariantsCatalogCreator]</b> Found reference to this script GUID in:\n{assetPath}",
                    asset
                );

                if (asset != null)
                {
                    EditorGUIUtility.PingObject(asset);
                }
            }
        }

        if (totalMatches == 0)
        {
            Debug.LogWarning("[CabinetVariantsCatalogCreator] No .unity, .prefab, or .asset files contained this script GUID.");
        }
        else
        {
            Debug.LogWarning($"[CabinetVariantsCatalogCreator] Total GUID references found: {totalMatches}");
        }
    }

    private static string FullPathToAssetPath(string fullPath)
    {
        string normalizedFullPath = fullPath.Replace("\\", "/");
        string normalizedDataPath = Application.dataPath.Replace("\\", "/");

        if (normalizedFullPath.StartsWith(normalizedDataPath))
        {
            return "Assets" + normalizedFullPath.Substring(normalizedDataPath.Length);
        }

        return normalizedFullPath;
    }

    [MenuItem("XRCC/Furniture/Create Cabinet Variants Catalog")]
    public static void CreateExampleCabinetVariantsCatalog()
    {
        EnsureFolder("Assets", "RoomRevive");
        EnsureFolder("Assets/RoomRevive", "Data");
        EnsureFolder("Assets/RoomRevive/Data", "NewCabinets");
        EnsureFolder("Assets/RoomRevive/Data/NewCabinets", "CabinetImages");

        MetaRayFurnitureProductCatalog catalog =
            AssetDatabase.LoadAssetAtPath<MetaRayFurnitureProductCatalog>(CatalogAssetPath);

        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<MetaRayFurnitureProductCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        }

        catalog.products = new List<MetaRayFurnitureProductVariant>
        {
            CreateWarmWalnutCabinets(),
            CreateSoftCreamStoneCabinets(),
            CreateSageOakCabinets()
        };

        for (int i = 0; i < catalog.products.Count; i++)
        {
            MetaRayFurnitureProductVariant product = catalog.products[i];

            if (product == null)
            {
                continue;
            }

            Sprite sprite = TryLoadExistingProductImage(i, product.productName);

            if (sprite == null && !string.IsNullOrWhiteSpace(product.sourceImageUrl))
            {
                string safeName = MakeSafeFileName(product.productName);
                string imageAssetPath = $"{ImageFolder}/{safeName}.jpg";

                sprite = DownloadImageAsSprite(product.sourceImageUrl, imageAssetPath);
            }

            if (sprite != null)
            {
                product.productImage = sprite;
            }
            else
            {
                Debug.LogWarning(
                    $"[CabinetVariantsCatalogCreator] No image found for '{product.productName}'.\n" +
                    $"Place an image in:\n{ImageFolder}\n" +
                    $"Suggested filename:\n{MakeSafeFileName(product.productName)}.png"
                );
            }
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = catalog;
        EditorGUIUtility.PingObject(catalog);

        Debug.Log($"<b>[CabinetVariantsCatalogCreator]</b> Created/updated cabinet variants catalog:\n{CatalogAssetPath}");
    }

    private static MetaRayFurnitureProductVariant CreateWarmWalnutCabinets()
    {
        return new MetaRayFurnitureProductVariant
        {
            id = 101,
            productName = "Warm Walnut Cabinets",
            subtitle = "Full-height walnut kitchen with dark stone",
            badgeText = "Natural Warmth",
            priceText = "$8,900",

            sourceImageUrl = "",

            calloutText = "A warm walnut cabinet style was found for this kitchen. The layout keeps the original room proportions while applying a rich wood finish and darker stone surfaces.",
            shortDescription = "A premium walnut cabinet system with full-height panels, warm grain and a dark stone backsplash.",
            description = "A rich cabinet concept built around natural walnut, vertical wood grain and deep stone contrast. It gives the kitchen a calm, grounded and high-end interior feeling.",
            longDescription = "A rich cabinet concept built around natural walnut, vertical wood grain and deep stone contrast. It gives the kitchen a calm, grounded and high-end interior feeling while preserving the scanned kitchen dimensions. The products shown are presented as a cabinet style package that matches the concrete size and layout of the existing kitchen area.",

            widthText = "Room matched",
            heightText = "Floor to ceiling",
            depthText = "60 cm base / 35 cm wall",
            weightText = "Project based",

            features = new List<string>
            {
                "Full-height cabinet fronts",
                "Continuous vertical walnut grain",
                "Dark stone backsplash and worktop",
                "Integrated handle-free look",
                "Warm natural material expression",
                "Designed to fit the scanned cabinet volume"
            },

            materialsText = "Walnut veneer fronts, dark stone-effect countertop, dark stone backsplash, matte black shadow gaps, soft-close fittings",
            finish = "Warm Walnut / Dark Stone",

            finishColors = new List<Color>
            {
                HexToColor("#8A552E"),
                HexToColor("#3F4642"),
                HexToColor("#2C2119")
            },

            finishColorLabels = new List<string>
            {
                "Warm Walnut",
                "Dark Stone",
                "Deep Shadow Trim"
            },

            storageText = "Tall integrated cabinet wall, upper storage, base cabinets, open shelf zone and island front panels",

            includedParts = new List<string>
            {
                "Tall cabinet fronts",
                "Upper cabinet fronts",
                "Base cabinet fronts",
                "Island front panels",
                "Stone-effect backsplash",
                "Stone-effect countertop",
                "Open shelf detail",
                "Soft-close hinge and drawer hardware"
            },

            useCustomFallbackColor = true,
            fallbackColor = HexToColor("#8A552E")
        };
    }

    private static MetaRayFurnitureProductVariant CreateSoftCreamStoneCabinets()
    {
        return new MetaRayFurnitureProductVariant
        {
            id = 102,
            productName = "Soft Cream Stone Cabinets",
            subtitle = "Cream upper cabinets with grey lower fronts",
            badgeText = "Minimal Calm",
            priceText = "$7,400",

            sourceImageUrl = "",

            calloutText = "A soft cream and stone cabinet style was found for this kitchen. It keeps the same kitchen footprint while creating a brighter, quieter and more minimal expression.",
            shortDescription = "A light modern cabinet system with cream upper fronts, grey base units and white marble-effect surfaces.",
            description = "A calm and minimal kitchen concept with soft cream cabinetry, muted grey base cabinets and white stone surfaces. The style makes the space feel brighter and more open.",
            longDescription = "A calm and minimal kitchen concept with soft cream cabinetry, muted grey base cabinets and white stone surfaces. The style makes the space feel brighter and more open while preserving the exact cabinet zones from the scanned kitchen. The products shown are selected to match the concrete size of the existing kitchen and demonstrate a realistic replacement direction.",

            widthText = "Room matched",
            heightText = "Floor to ceiling",
            depthText = "60 cm base / 35 cm wall",
            weightText = "Project based",

            features = new List<string>
            {
                "Soft cream full-height fronts",
                "Muted grey lower cabinets",
                "White marble-effect backsplash",
                "Integrated clean-line cabinet seams",
                "Bright minimal Scandinavian expression",
                "Designed to fit the existing cabinet layout"
            },

            materialsText = "Matte cream laminate fronts, muted grey painted fronts, white marble-effect countertop, white stone backsplash, brushed metal details",
            finish = "Soft Cream / Stone Grey / White Marble",

            finishColors = new List<Color>
            {
                HexToColor("#DDD7C2"),
                HexToColor("#8E9290"),
                HexToColor("#F1EEE6")
            },

            finishColorLabels = new List<string>
            {
                "Soft Cream",
                "Stone Grey",
                "White Marble"
            },

            storageText = "Tall hidden storage, upper display shelf, base drawers, sink area cabinets and island cladding",

            includedParts = new List<string>
            {
                "Tall cabinet fronts",
                "Upper cabinet fronts",
                "Base cabinet fronts",
                "Island cladding panels",
                "White stone-effect backsplash",
                "White stone-effect countertop",
                "Integrated shelf detail",
                "Soft-close hinge and drawer hardware"
            },

            useCustomFallbackColor = true,
            fallbackColor = HexToColor("#DDD7C2")
        };
    }

    private static MetaRayFurnitureProductVariant CreateSageOakCabinets()
    {
        return new MetaRayFurnitureProductVariant
        {
            id = 103,
            productName = "Sage Oak Cabinets",
            subtitle = "Soft sage lower cabinets with oak details",
            badgeText = "Scandi Fresh",
            priceText = "$7,900",

            sourceImageUrl = "",

            calloutText = "A sage and oak cabinet style was found for this kitchen. It preserves the current kitchen proportions while adding a lighter, warmer and more playful Scandinavian finish.",
            shortDescription = "A soft sage cabinet system with cream upper fronts, oak shelving and a natural wood countertop.",
            description = "A fresh Scandinavian cabinet concept combining soft sage green, warm oak and light cream surfaces. The design feels calm, natural and welcoming.",
            longDescription = "A fresh Scandinavian cabinet concept combining soft sage green, warm oak and light cream surfaces. The design feels calm, natural and welcoming while keeping the same cabinet scale and kitchen geometry. The products shown are matched to the concrete size of the existing kitchen and visualized as a realistic replacement package.",

            widthText = "Room matched",
            heightText = "Floor to ceiling",
            depthText = "60 cm base / 35 cm wall",
            weightText = "Project based",

            features = new List<string>
            {
                "Soft sage green lower cabinets",
                "Cream upper cabinet fronts",
                "Warm oak shelves and countertop",
                "Light speckled backsplash surface",
                "Minimal handles and calm panel rhythm",
                "Designed around the scanned cabinet footprint"
            },

            materialsText = "Matte sage painted fronts, cream laminate fronts, oak veneer shelves, oak countertop, speckled stone-effect backsplash",
            finish = "Soft Sage / Cream / Natural Oak",

            finishColors = new List<Color>
            {
                HexToColor("#9EAD9A"),
                HexToColor("#DCD5BE"),
                HexToColor("#B88952")
            },

            finishColorLabels = new List<string>
            {
                "Soft Sage",
                "Warm Cream",
                "Natural Oak"
            },

            storageText = "Tall storage wall, upper open shelves, sage base drawers, island panels and fitted cabinet fronts",

            includedParts = new List<string>
            {
                "Tall cabinet fronts",
                "Upper cabinet fronts",
                "Sage base cabinet fronts",
                "Island side panels",
                "Oak open shelves",
                "Oak countertop",
                "Speckled backsplash panels",
                "Soft-close hinge and drawer hardware"
            },

            useCustomFallbackColor = true,
            fallbackColor = HexToColor("#9EAD9A")
        };
    }

    private static Sprite TryLoadExistingProductImage(int productIndex, string productName)
    {
        List<string> candidateNames = GetImageNameCandidates(productIndex, productName);

        foreach (string candidateName in candidateNames)
        {
            foreach (string extension in SupportedImageExtensions)
            {
                string imageAssetPath = $"{ImageFolder}/{candidateName}{extension}";

                if (File.Exists(imageAssetPath))
                {
                    return ImportImageAsSprite(imageAssetPath);
                }
            }
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ImageFolder });

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(assetPath);

            foreach (string candidateName in candidateNames)
            {
                if (string.Equals(fileNameWithoutExtension, candidateName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return ImportImageAsSprite(assetPath);
                }
            }
        }

        return null;
    }

    private static List<string> GetImageNameCandidates(int productIndex, string productName)
    {
        List<string> candidates = new List<string>
        {
            MakeSafeFileName(productName)
        };

        switch (productIndex)
        {
            case 0:
                candidates.Add("Warm_Walnut_Cabinets");
                candidates.Add("warm_walnut_cabinets");
                candidates.Add("Walnut_Cabinets");
                candidates.Add("walnut_cabinets");
                candidates.Add("Cabinet_1");
                candidates.Add("cabinet_1");
                candidates.Add("cabinet1");
                candidates.Add("Kitchen_1");
                candidates.Add("kitchen_1");
                break;

            case 1:
                candidates.Add("Soft_Cream_Stone_Cabinets");
                candidates.Add("soft_cream_stone_cabinets");
                candidates.Add("Cream_Grey_Cabinets");
                candidates.Add("cream_grey_cabinets");
                candidates.Add("Cream_Stone_Cabinets");
                candidates.Add("cream_stone_cabinets");
                candidates.Add("Cabinet_2");
                candidates.Add("cabinet_2");
                candidates.Add("cabinet2");
                candidates.Add("Kitchen_2");
                candidates.Add("kitchen_2");
                break;

            case 2:
                candidates.Add("Sage_Oak_Cabinets");
                candidates.Add("sage_oak_cabinets");
                candidates.Add("Green_Oak_Cabinets");
                candidates.Add("green_oak_cabinets");
                candidates.Add("Sage_Cabinets");
                candidates.Add("sage_cabinets");
                candidates.Add("Cabinet_3");
                candidates.Add("cabinet_3");
                candidates.Add("cabinet3");
                candidates.Add("Kitchen_3");
                candidates.Add("kitchen_3");
                break;
        }

        return candidates;
    }

    private static Sprite DownloadImageAsSprite(string imageUrl, string imageAssetPath)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        if (File.Exists(imageAssetPath))
        {
            return ImportImageAsSprite(imageAssetPath);
        }

        Debug.Log($"[CabinetVariantsCatalogCreator] Downloading image:\n{imageUrl}");

        using UnityWebRequest request = UnityWebRequest.Get(imageUrl);
        UnityWebRequestAsyncOperation operation = request.SendWebRequest();

        while (!operation.isDone)
        {
            // Editor blocking download. Fine for a small one-click setup tool.
        }

#if UNITY_2020_2_OR_NEWER
        bool hasError = request.result != UnityWebRequest.Result.Success;
#else
        bool hasError = request.isNetworkError || request.isHttpError;
#endif

        if (hasError)
        {
            Debug.LogWarning($"[CabinetVariantsCatalogCreator] Could not download image:\n{imageUrl}\n{request.error}");
            return null;
        }

        byte[] data = request.downloadHandler.data;

        if (data == null || data.Length == 0)
        {
            Debug.LogWarning($"[CabinetVariantsCatalogCreator] Downloaded image was empty:\n{imageUrl}");
            return null;
        }

        File.WriteAllBytes(imageAssetPath, data);
        AssetDatabase.ImportAsset(imageAssetPath, ImportAssetOptions.ForceUpdate);

        return ImportImageAsSprite(imageAssetPath);
    }

    private static Sprite ImportImageAsSprite(string imageAssetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(imageAssetPath) as TextureImporter;

        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        AssetDatabase.ImportAsset(imageAssetPath, ImportAssetOptions.ForceUpdate);

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imageAssetPath);

        if (sprite == null)
        {
            Debug.LogWarning($"[CabinetVariantsCatalogCreator] Could not import image as Sprite:\n{imageAssetPath}");
        }

        return sprite;
    }

    private static void EnsureFolder(string parent, string folderName)
    {
        string path = $"{parent}/{folderName}";

        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    private static string MakeSafeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "CabinetImage";
        }

        foreach (char c in Path.GetInvalidFileNameChars())
        {
            input = input.Replace(c, '_');
        }

        return input.Replace(" ", "_");
    }

    private static Color HexToColor(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out Color color))
        {
            return color;
        }

        return Color.white;
    }
#endif
}