#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RoomRevive.ProductBrowser.EditorTools
{
    /// <summary>
    /// Editor wizard for adding a new fridge product in one step.
    ///
    /// What it does:
    ///   1. Creates a ProductData asset with the entered details.
    ///   2. Appends it to the Fridges ProductCatalog.
    ///   3. Creates a scene GameObject under the Fridges root.
    ///   4. Appends that GameObject to ProductVariantRouter.fridgeVariants[].
    ///
    /// Menu: Tools → RoomRevive → Product Browser → Add New Fridge
    /// </summary>
    public class ProductBrowserFridgeWizard : EditorWindow
    {
        const string FridgeCatalogPath   = "Assets/RoomRevive/UI/ProductBrowser/Data/Product/Category_Fridges.asset";
        const string FridgeDataFolder    = "Assets/RoomRevive/Data/NewProducts";
        const string FridgesRootName     = "Fridges";

        // ── Form fields ───────────────────────────────────────────────────────

        string     _id          = "";
        string     _brand       = "";
        string     _productName = "";
        string     _subtitle    = "";
        string     _emotional   = "";
        string     _description = "";
        string     _specsText   = "";
        string     _price       = "";
        Sprite     _image;
        GameObject _prefab;

        // ── Scene / asset refs (auto-resolved) ────────────────────────────────

        ProductCategoryData _fridgeCategory;
        ProductVariantRouter _router;
        Transform _fridgesRoot;

        Vector2 _scroll;

        // ── Open ──────────────────────────────────────────────────────────────

        [MenuItem("Tools/RoomRevive/Product Browser/Add New Fridge")]
        public static void Open()
        {
            var win = GetWindow<ProductBrowserFridgeWizard>(true, "Add New Fridge", true);
            win.minSize = new Vector2(420, 560);
            win.Resolve();
        }

        void OnEnable() => Resolve();

        // ── GUI ───────────────────────────────────────────────────────────────

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawStatusBox();
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("New Fridge", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _id          = EditorGUILayout.TextField(new GUIContent("ID",           "Stable unique id, e.g. 'miele_white'. Used by PlayerPrefs favorites key."), _id);
            _brand       = EditorGUILayout.TextField(new GUIContent("Brand",        "e.g. Miele"), _brand);
            _productName = EditorGUILayout.TextField(new GUIContent("Product Name", "Headline shown on the card, e.g. 'Modern Fridge'"), _productName);
            _subtitle    = EditorGUILayout.TextField(new GUIContent("Subtitle",     "Small line under the name, e.g. 'Freestanding refrigerator · white'"), _subtitle);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(new GUIContent("Emotional Line / Headline", "Shown as the large feature line on the Swap panel."), EditorStyles.miniLabel);
            _emotional = EditorGUILayout.TextArea(_emotional, GUILayout.MinHeight(40));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Short Description", EditorStyles.miniLabel);
            _description = EditorGUILayout.TextArea(_description, GUILayout.MinHeight(60));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(new GUIContent("Specs (one per line)", "Each line becomes a chip, e.g. '141 L', '73 kWh/yr', '34 dB'."), EditorStyles.miniLabel);
            _specsText = EditorGUILayout.TextArea(_specsText, GUILayout.MinHeight(54));

            EditorGUILayout.Space(4);
            _price  = EditorGUILayout.TextField(new GUIContent("Price",  "Optional, e.g. 'From $1,299'. Leave blank to hide."), _price);
            _image  = (Sprite)EditorGUILayout.ObjectField(new GUIContent("Product Image", "Sprite shown on the Discover and Swap panels."), _image, typeof(Sprite), false);
            _prefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Prefab", "Optional. Instantiated under the Fridges root. Leave blank to create an empty placeholder."), _prefab, typeof(GameObject), false);

            EditorGUILayout.Space(12);

            bool canCreate = IsValid(out string reason);
            using (new EditorGUI.DisabledScope(!canCreate))
            {
                if (GUILayout.Button("Add Fridge", GUILayout.Height(32)))
                    CreateFridge();
            }

            if (!canCreate)
                EditorGUILayout.HelpBox(reason, MessageType.Warning);

            EditorGUILayout.EndScrollView();
        }

        // ── Status box ────────────────────────────────────────────────────────

        void DrawStatusBox()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Scene / Asset Targets", EditorStyles.boldLabel);

            DrawStatusRow("Fridge Catalog",  _fridgeCategory != null, _fridgeCategory?.name ?? "Not found — run Sync first");
            DrawStatusRow("Fridges Root GO", _fridgesRoot != null,    _fridgesRoot != null ? $"\"{_fridgesRoot.name}\" in scene" : $"No child named \"{FridgesRootName}\" under router");
            DrawStatusRow("Variant Router",  _router != null,         _router != null ? $"on \"{_router.gameObject.name}\"" : "Not found in scene");

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Re-scan Scene", EditorStyles.miniButton, GUILayout.Width(90)))
                Resolve();

            EditorGUILayout.EndVertical();
        }

        static void DrawStatusRow(string label, bool ok, string detail)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(ok ? "✓" : "✗", GUILayout.Width(16));
            EditorGUILayout.LabelField(label, GUILayout.Width(130));
            EditorGUILayout.LabelField(detail, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        // ── Validation ────────────────────────────────────────────────────────

        bool IsValid(out string reason)
        {
            if (string.IsNullOrWhiteSpace(_productName)) { reason = "Product Name is required."; return false; }
            if (string.IsNullOrWhiteSpace(_id))          { reason = "ID is required (used as favorites key — must be unique)."; return false; }
            if (_fridgeCategory == null)                  { reason = "Fridge catalog not found. Run Tools → Sync first."; return false; }
            if (_fridgesRoot == null)                     { reason = $"No \"{FridgesRootName}\" GameObject found under the router. Create it in the scene."; return false; }
            if (_router == null)                          { reason = "ProductVariantRouter not found in scene."; return false; }
            reason = null;
            return true;
        }

        // ── Creation ─────────────────────────────────────────────────────────

        void CreateFridge()
        {
            // 1. Save ProductData asset.
            if (!Directory.Exists(FridgeDataFolder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(FridgeDataFolder), Path.GetFileName(FridgeDataFolder));

            string safeName = string.IsNullOrWhiteSpace(_id)
                ? _productName.Replace(" ", "_")
                : _id.Replace(" ", "_");

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{FridgeDataFolder}/Product_{safeName}.asset");

            ProductData data = CreateInstance<ProductData>();
            data.id              = _id.Trim();
            data.brandName       = _brand.Trim();
            data.productName     = _productName.Trim();
            data.subtitle        = _subtitle.Trim();
            data.emotionalLine   = _emotional.Trim();
            data.shortDescription = _description.Trim();
            data.specs           = ParseSpecs(_specsText);
            data.fromPrice       = _price.Trim();
            data.productImage    = _image;

            AssetDatabase.CreateAsset(data, assetPath);

            // 2. Append to catalog.
            ProductCatalog catalog = _fridgeCategory.catalog;
            if (catalog == null)
            {
                Debug.LogError("[FridgeWizard] Fridge category has no catalog assigned.", _fridgeCategory);
                return;
            }

            Undo.RecordObject(catalog, "Add Fridge to Catalog");
            int newIndex = catalog.Count;

            SerializedObject soCatalog = new SerializedObject(catalog);
            SerializedProperty itemsProp = soCatalog.FindProperty("products");

            itemsProp.arraySize++;
            itemsProp.GetArrayElementAtIndex(itemsProp.arraySize - 1).objectReferenceValue = data;
            soCatalog.ApplyModifiedProperties();

            EditorUtility.SetDirty(catalog);

            // 3. Create scene GameObject under fridges root (instantiate prefab if provided).
            GameObject fridgeGO;
            if (_prefab != null)
            {
                fridgeGO = (GameObject)PrefabUtility.InstantiatePrefab(_prefab, _fridgesRoot);
                fridgeGO.name = _productName.Trim();
                Undo.RegisterCreatedObjectUndo(fridgeGO, "Create Fridge GO");
            }
            else
            {
                fridgeGO = new GameObject(_productName.Trim());
                Undo.RegisterCreatedObjectUndo(fridgeGO, "Create Fridge GO");
                fridgeGO.transform.SetParent(_fridgesRoot, false);
            }
            fridgeGO.SetActive(false);

            // 4. Append to objectVariants[] on router.
            SerializedObject soRouter = new SerializedObject(_router);
            SerializedProperty variantsProp = soRouter.FindProperty("objectVariants");

            variantsProp.arraySize++;
            variantsProp.GetArrayElementAtIndex(variantsProp.arraySize - 1).objectReferenceValue = fridgeGO;
            soRouter.ApplyModifiedProperties();
            EditorUtility.SetDirty(_router);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[FridgeWizard] Added '{data.productName}' at catalog index {newIndex}. " +
                      $"Asset: {assetPath}  |  Scene GO: {fridgeGO.name}", fridgeGO);

            EditorGUIUtility.PingObject(data);

            // Reset form for next entry.
            _id = _brand = _productName = _subtitle = _emotional = _description = _specsText = _price = "";
            _image = null;
            _prefab = null;

            Repaint();
        }

        /// <summary>Splits a multi-line specs box into one trimmed chip string per non-empty line.</summary>
        static string[] ParseSpecs(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new string[0];
            var list = new System.Collections.Generic.List<string>();
            foreach (string line in raw.Split('\n'))
            {
                string t = line.Trim();
                if (t.Length > 0) list.Add(t);
            }
            return list.ToArray();
        }

        // ── Scene resolution ─────────────────────────────────────────────────

        void Resolve()
        {
            _fridgeCategory = AssetDatabase.LoadAssetAtPath<ProductCategoryData>(FridgeCatalogPath);

#if UNITY_2022_2_OR_NEWER
            _router = Object.FindFirstObjectByType<ProductVariantRouter>();
#else
            _router = Object.FindObjectOfType<ProductVariantRouter>();
#endif

            _fridgesRoot = null;
            if (_router != null)
            {
                Transform t = _router.transform.Find(FridgesRootName);
                if (t == null)
                {
                    // Search children one level deep.
                    foreach (Transform child in _router.transform)
                    {
                        if (child.name == FridgesRootName) { t = child; break; }
                    }
                }
                if (t == null)
                {
                    // Broader search in scene.
                    GameObject found = GameObject.Find(FridgesRootName);
                    if (found != null) t = found.transform;
                }
                _fridgesRoot = t;
            }

            Repaint();
        }
    }
}
#endif
