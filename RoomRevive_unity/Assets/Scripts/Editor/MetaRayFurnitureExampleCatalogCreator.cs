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
public class MetaRayFurnitureExampleCatalogCreator : MonoBehaviour
{
#if UNITY_EDITOR
    private const string CatalogFolder = "Assets/RoomRevive/Data/NewProducts";
    private const string ImageFolder = "Assets/RoomRevive/Data/NewProducts/ProductImages";
    private const string CatalogAssetPath = CatalogFolder + "/FridgesCatalog.asset";

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

        sb.AppendLine($"<b>[MetaRayFurnitureExampleCatalogCreator]</b> This script is attached to a GameObject.");
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

    [MenuItem("XRCC/Furniture/Find Attached MetaRayFurnitureExampleCatalogCreator In Loaded Objects")]
    public static void FindAttachedInstancesInLoadedObjects()
    {
        MetaRayFurnitureExampleCatalogCreator[] instances =
            Resources.FindObjectsOfTypeAll<MetaRayFurnitureExampleCatalogCreator>();

        if (instances == null || instances.Length == 0)
        {
            Debug.LogWarning("[MetaRayFurnitureExampleCatalogCreator] No loaded instances found. It may be inside a prefab/scene that is not currently open.");
            return;
        }

        Debug.Log($"[MetaRayFurnitureExampleCatalogCreator] Found {instances.Length} loaded instance(s).");

        foreach (MetaRayFurnitureExampleCatalogCreator instance in instances)
        {
            if (instance == null)
            {
                continue;
            }

            instance.LogAttachedObjectAndParents("Manual finder");
        }
    }

    [MenuItem("XRCC/Furniture/Find References To MetaRayFurnitureExampleCatalogCreator GUID")]
    public static void FindReferencesToThisScriptGuid()
    {
        string[] scriptGuids = AssetDatabase.FindAssets("MetaRayFurnitureExampleCatalogCreator t:MonoScript");

        if (scriptGuids == null || scriptGuids.Length == 0)
        {
            Debug.LogError("[MetaRayFurnitureExampleCatalogCreator] Could not find this script as a MonoScript asset.");
            return;
        }

        int totalMatches = 0;

        foreach (string scriptGuid in scriptGuids)
        {
            string scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuid);

            if (!scriptPath.EndsWith("MetaRayFurnitureExampleCatalogCreator.cs"))
            {
                continue;
            }

            Debug.Log($"[MetaRayFurnitureExampleCatalogCreator] Searching for GUID references:\nGUID: {scriptGuid}\nScript: {scriptPath}");

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
                    $"<b>[MetaRayFurnitureExampleCatalogCreator]</b> Found reference to this script GUID in:\n{assetPath}",
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
            Debug.LogWarning("[MetaRayFurnitureExampleCatalogCreator] No .unity, .prefab, or .asset files contained this script GUID.");
        }
        else
        {
            Debug.LogWarning($"[MetaRayFurnitureExampleCatalogCreator] Total GUID references found: {totalMatches}");
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

    [MenuItem("XRCC/Furniture/Create Fridges Catalog")]
    public static void CreateExampleProductCatalog()
    {
        EnsureFolder("Assets", "RoomRevive");
        EnsureFolder("Assets/RoomRevive", "Data");
        EnsureFolder("Assets/RoomRevive/Data", "NewProducts");
        EnsureFolder("Assets/RoomRevive/Data/NewProducts", "ProductImages");

        MetaRayFurnitureProductCatalog catalog =
            AssetDatabase.LoadAssetAtPath<MetaRayFurnitureProductCatalog>(CatalogAssetPath);

        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<MetaRayFurnitureProductCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        }

        catalog.products = new List<MetaRayFurnitureProductVariant>
        {
            CreateModernFridge(),
            CreateScandiTallFridge(),
            CreateAuroraFrenchDoorFridge()
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
                    $"[MetaRayFurnitureExampleCatalogCreator] No image found for '{product.productName}'.\n" +
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

        Debug.Log($"<b>[MetaRayFurnitureExampleCatalogCreator]</b> Created/updated fridge catalog:\n{CatalogAssetPath}");
    }

    private static MetaRayFurnitureProductVariant CreateModernFridge()
    {
        return new MetaRayFurnitureProductVariant
        {
            id = 1,
            productName = "Modern Fridge",
            subtitle = "Premium four-door stainless fridge",
            badgeText = "Modern",
            priceText = "$1,299",

            sourceImageUrl = "",

            calloutText = "A modern stainless fridge was found for this kitchen. Open it to compare storage zones, finish and fit.",
            shortDescription = "A clean four-door fridge with brushed steel surfaces and a high-end modern kitchen look.",
            description = "A premium four-door fridge designed for open kitchens, loft apartments and modern interiors. The brushed stainless finish reflects the room softly while keeping a calm, professional look.",
            longDescription = "A premium four-door fridge designed for open kitchens, loft apartments and modern interiors. The brushed stainless finish reflects the room softly while keeping a calm, professional look. Ideal for spaces that mix industrial materials, warm wood, stone surfaces and black metal details.",

            widthText = "91 cm",
            heightText = "183 cm",
            depthText = "70 cm",
            weightText = "118 kg",

            features = new List<string>
            {
                "Four-door flexible storage layout",
                "External touch display",
                "Brushed stainless steel finish",
                "Separate cooling zones",
                "Quiet inverter compressor",
                "Large family-size capacity"
            },

            materialsText = "Brushed stainless steel doors, graphite trim, black glass touch panel, tempered glass shelves",
            finish = "Brushed Steel / Graphite Trim",

            finishColors = new List<Color>
            {
                HexToColor("#B8B5AD"),
                HexToColor("#2A2A2A")
            },

            finishColorLabels = new List<string>
            {
                "Brushed Steel",
                "Graphite Trim"
            },

            storageText = "4 main doors, flexible fridge/freezer zones, wide interior shelves",

            includedParts = new List<string>
            {
                "Fridge body",
                "Adjustable glass shelves",
                "Door bins",
                "Humidity drawer",
                "User guide"
            },

            useCustomFallbackColor = true,
            fallbackColor = HexToColor("#B8B5AD")
        };
    }

    private static MetaRayFurnitureProductVariant CreateScandiTallFridge()
    {
        return new MetaRayFurnitureProductVariant
        {
            id = 2,
            productName = "Nordic Tall Fridge",
            subtitle = "Minimal bottom-freezer refrigerator",
            badgeText = "Clean Fit",
            priceText = "$899",

            sourceImageUrl = "",

            calloutText = "A slim Scandinavian-style fridge was found for this space. Open it to inspect the height, freezer layout and quiet daily-use features.",
            shortDescription = "A calm, minimal fridge with a tall vertical profile and soft brushed metal finish.",
            description = "A slim bottom-freezer refrigerator made for quiet, modern kitchens. Its simple vertical shape works well beside dark cabinets, white counters and soft natural daylight.",
            longDescription = "A slim bottom-freezer refrigerator made for quiet, modern kitchens. Its simple vertical shape works well beside dark cabinets, white counters and soft natural daylight. The layout keeps fresh food at eye level and places frozen storage in the lower section for a cleaner everyday workflow.",

            widthText = "60 cm",
            heightText = "190 cm",
            depthText = "66 cm",
            weightText = "72 kg",

            features = new List<string>
            {
                "Bottom-freezer layout",
                "Minimal integrated handle",
                "Bright interior LED lighting",
                "Low-noise kitchen mode",
                "Reversible door setup",
                "Compact footprint for smaller kitchens"
            },

            materialsText = "Soft brushed steel front, integrated handle profile, white interior liner, tempered glass shelves",
            finish = "Soft Steel / Deep Cabinet Grey",

            finishColors = new List<Color>
            {
                HexToColor("#C2C0B8"),
                HexToColor("#262621")
            },

            finishColorLabels = new List<string>
            {
                "Soft Steel",
                "Deep Cabinet Grey"
            },

            storageText = "1 tall fridge section, 1 lower freezer section, adjustable shelves and door bins",

            includedParts = new List<string>
            {
                "Fridge body",
                "Glass shelves",
                "Freezer drawers",
                "Door bins",
                "User guide"
            },

            useCustomFallbackColor = true,
            fallbackColor = HexToColor("#C2C0B8")
        };
    }

    private static MetaRayFurnitureProductVariant CreateAuroraFrenchDoorFridge()
    {
        return new MetaRayFurnitureProductVariant
        {
            id = 3,
            productName = "Aurora French Door Fridge",
            subtitle = "French-door fridge with dispenser",
            badgeText = "Premium",
            priceText = "$1,799",

            sourceImageUrl = "",

            calloutText = "A premium French-door fridge was found for this kitchen. Open it to compare the dispenser, handle design and storage capacity.",
            shortDescription = "A large French-door fridge with reflective stainless steel, vertical handles and an integrated dispenser.",
            description = "A premium French-door fridge designed for larger kitchens and high-end interiors. The reflective stainless surface, tall handles and built-in water dispenser create a polished professional look.",
            longDescription = "A premium French-door fridge designed for larger kitchens and high-end interiors. The reflective stainless surface, tall handles and built-in water dispenser create a polished professional look. It is a strong fit for dark cabinetry, marble backsplashes, copper lighting and spacious open-plan kitchens.",

            widthText = "91 cm",
            heightText = "178 cm",
            depthText = "73 cm",
            weightText = "135 kg",

            features = new List<string>
            {
                "French-door refrigerator layout",
                "Filtered water dispenser",
                "Tall vertical metal handles",
                "Fingerprint-resistant steel finish",
                "Twin cooling zones",
                "Large-capacity interior storage"
            },

            materialsText = "Fingerprint-resistant stainless steel, black dispenser inset, aluminium handles, tempered glass shelves",
            finish = "Mirror Steel / Black Inset",

            finishColors = new List<Color>
            {
                HexToColor("#C6C4BC"),
                HexToColor("#151515")
            },

            finishColorLabels = new List<string>
            {
                "Mirror Steel",
                "Black Inset"
            },

            storageText = "2 French doors, wide fridge compartment, large freezer storage, dispenser area",

            includedParts = new List<string>
            {
                "Fridge body",
                "Water filter",
                "Glass shelves",
                "Door bins",
                "Ice tray",
                "User guide"
            },

            useCustomFallbackColor = true,
            fallbackColor = HexToColor("#C6C4BC")
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
                candidates.Add("Modern_Fridge");
                candidates.Add("modern_fridge");
                candidates.Add("Fridge_1");
                candidates.Add("fridge1");
                candidates.Add("fridge_1");
                break;

            case 1:
                candidates.Add("Nordic_Tall_Fridge");
                candidates.Add("nordic_tall_fridge");
                candidates.Add("Scandi_Tall_Fridge");
                candidates.Add("scandi_tall_fridge");
                candidates.Add("Ikea_Fridge");
                candidates.Add("ikea_fridge");
                candidates.Add("Fridge_2");
                candidates.Add("fridge2");
                candidates.Add("fridge_2");
                break;

            case 2:
                candidates.Add("Aurora_French_Door_Fridge");
                candidates.Add("aurora_french_door_fridge");
                candidates.Add("Samsung_French_Door_Fridge");
                candidates.Add("samsung_french_door_fridge");
                candidates.Add("French_Door_Fridge");
                candidates.Add("french_door_fridge");
                candidates.Add("Fridge_3");
                candidates.Add("fridge3");
                candidates.Add("fridge_3");
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

        Debug.Log($"[MetaRayFurnitureExampleCatalogCreator] Downloading image:\n{imageUrl}");

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
            Debug.LogWarning($"[MetaRayFurnitureExampleCatalogCreator] Could not download image:\n{imageUrl}\n{request.error}");
            return null;
        }

        byte[] data = request.downloadHandler.data;

        if (data == null || data.Length == 0)
        {
            Debug.LogWarning($"[MetaRayFurnitureExampleCatalogCreator] Downloaded image was empty:\n{imageUrl}");
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
            Debug.LogWarning($"[MetaRayFurnitureExampleCatalogCreator] Could not import image as Sprite:\n{imageAssetPath}");
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
            return "ProductImage";
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