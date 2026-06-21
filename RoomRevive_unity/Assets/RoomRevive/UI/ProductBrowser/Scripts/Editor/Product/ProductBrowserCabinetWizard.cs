#if UNITY_EDITOR
using System.IO;
using GaussianSplatting.Editor;
using GaussianSplatting.Runtime;
using UnityEditor;
using UnityEngine;

namespace RoomRevive.ProductBrowser.EditorTools
{
    /// <summary>
    /// Editor wizard for adding a new cabinet product in one step.
    ///
    /// What it does:
    ///   1. Imports the .spz / .ply file as a GaussianSplatAsset via GaussianSplatAssetCreatorAPI.
    ///   2. Creates a ProductData asset with the entered details.
    ///   3. Appends it to the Cabinets ProductCatalog.
    ///   4. Appends the GaussianSplatAsset to ProductVariantRouter.splatAssets[].
    ///
    /// Menu: Tools → RoomRevive → Product Browser → Add New Cabinet
    /// </summary>
    public class ProductBrowserCabinetWizard : EditorWindow
    {
        const string CabinetCatalogPath  = "Assets/RoomRevive/UI/ProductBrowser/Data/Product/Category_Cabinets.asset";
        const string CabinetDataFolder   = "Assets/RoomRevive/Data/NewCabinets";
        const string SplatOutputFolder   = "Assets/RoomRevive/Data/NewCabinets/Splats";

        // ── Form fields ───────────────────────────────────────────────────────

        string _id          = "";
        string _brand       = "";
        string _productName = "";
        string _subtitle    = "";
        string _emotional   = "";
        string _description = "";
        string _specsText   = "";
        string _price       = "";
        Sprite _image;
        string _splatFilePath = "";   // absolute path to .spz or .ply

        // ── Asset refs (auto-resolved) ───────────────────────────────────────

        ProductCategoryData _cabinetCategory;

        Vector2 _scroll;

        // ── Open ──────────────────────────────────────────────────────────────

        [MenuItem("Tools/RoomRevive/Product Browser/Add New Cabinet")]
        public static void Open()
        {
            var win = GetWindow<ProductBrowserCabinetWizard>(true, "Add New Cabinet", true);
            win.minSize = new Vector2(440, 540);
            win.Resolve();
        }

        void OnEnable() => Resolve();

        // ── GUI ───────────────────────────────────────────────────────────────

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawStatusBox();
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("New Cabinet", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _id          = EditorGUILayout.TextField(new GUIContent("ID",           "Stable unique id, e.g. 'nobilia_sage'. Used by PlayerPrefs favorites key."), _id);
            _brand       = EditorGUILayout.TextField(new GUIContent("Brand",        "e.g. Nobilia"), _brand);
            _productName = EditorGUILayout.TextField(new GUIContent("Product Name", "Headline shown on the card, e.g. 'Warm Walnut'"), _productName);
            _subtitle    = EditorGUILayout.TextField(new GUIContent("Subtitle",     "Small line under the name, e.g. 'Upper cabinets · matte sage'"), _subtitle);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(new GUIContent("Emotional Line / Headline", "Shown as the large feature line on the Swap panel."), EditorStyles.miniLabel);
            _emotional = EditorGUILayout.TextArea(_emotional, GUILayout.MinHeight(40));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Short Description", EditorStyles.miniLabel);
            _description = EditorGUILayout.TextArea(_description, GUILayout.MinHeight(60));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(new GUIContent("Specs (one per line)", "Each line becomes a chip, e.g. 'Soft-close', 'Matte finish'."), EditorStyles.miniLabel);
            _specsText = EditorGUILayout.TextArea(_specsText, GUILayout.MinHeight(54));

            EditorGUILayout.Space(4);
            _price = EditorGUILayout.TextField(new GUIContent("Price", "Optional, e.g. 'From $2,499'. Leave blank to hide."), _price);
            _image = (Sprite)EditorGUILayout.ObjectField(new GUIContent("Product Image", "Sprite shown on the Discover and Swap panels."), _image, typeof(Sprite), false);

            EditorGUILayout.Space(6);
            DrawSplatFilePicker();

            EditorGUILayout.Space(12);

            bool canCreate = IsValid(out string reason);
            using (new EditorGUI.DisabledScope(!canCreate))
            {
                if (GUILayout.Button("Add Cabinet", GUILayout.Height(32)))
                    CreateCabinet();
            }

            if (!canCreate)
                EditorGUILayout.HelpBox(reason, MessageType.Warning);

            EditorGUILayout.EndScrollView();
        }

        void DrawSplatFilePicker()
        {
            EditorGUILayout.LabelField("Gaussian Splat File", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Upload a .spz (WorldLabs compressed) or .ply (standard Gaussian Splat) file.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            string display = string.IsNullOrEmpty(_splatFilePath) ? "No file selected" : Path.GetFileName(_splatFilePath);
            EditorGUILayout.LabelField(display, EditorStyles.miniLabel);

            if (GUILayout.Button("Browse…", GUILayout.Width(70)))
            {
                string path = EditorUtility.OpenFilePanel("Select .spz or .ply Gaussian Splat file", "", "spz,ply");
                if (!string.IsNullOrEmpty(path))
                    _splatFilePath = path;
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_splatFilePath))
                EditorGUILayout.HelpBox($"Will import: {Path.GetFileName(_splatFilePath)}\nOutput: {SplatOutputFolder}/", MessageType.None);
        }

        // ── Status box ────────────────────────────────────────────────────────

        void DrawStatusBox()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Asset Targets", EditorStyles.boldLabel);

            DrawStatusRow("Cabinet Catalog", _cabinetCategory != null, _cabinetCategory?.name ?? "Not found");

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Re-scan", EditorStyles.miniButton, GUILayout.Width(60)))
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
            if (string.IsNullOrWhiteSpace(_productName))    { reason = "Product Name is required."; return false; }
            if (string.IsNullOrWhiteSpace(_id))             { reason = "ID is required (used as favorites key — must be unique)."; return false; }
            if (_cabinetCategory == null)                   { reason = "Cabinet catalog not found."; return false; }
            if (string.IsNullOrEmpty(_splatFilePath))       { reason = "Select a .spz or .ply file to import."; return false; }
            if (!File.Exists(_splatFilePath))               { reason = "Selected file no longer exists. Re-browse."; return false; }
            reason = null;
            return true;
        }

        // ── Creation ─────────────────────────────────────────────────────────

        void CreateCabinet()
        {
            string safeName = string.IsNullOrWhiteSpace(_id)
                ? _productName.Replace(" ", "_")
                : _id.Replace(" ", "_");

            // 1. Import splat file → GaussianSplatAsset.
            GaussianSplatAsset splatAsset = GaussianSplatAssetCreatorAPI.CreateAsset(
                inputFilePath: _splatFilePath,
                outputFolder:  SplatOutputFolder,
                baseName:      safeName,
                formatPos:     GaussianSplatAsset.VectorFormat.Norm11,
                formatScale:   GaussianSplatAsset.VectorFormat.Norm11,
                formatColor:   GaussianSplatAsset.ColorFormat.Norm8x4,
                formatSH:      GaussianSplatAsset.SHFormat.Cluster16k);

            if (splatAsset == null)
            {
                Debug.LogError("[CabinetWizard] Splat import failed — aborting.", this);
                return;
            }

            // 2. Create ProductData asset.
            if (!Directory.Exists(CabinetDataFolder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(CabinetDataFolder), Path.GetFileName(CabinetDataFolder));

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{CabinetDataFolder}/Product_{safeName}.asset");

            ProductData data = CreateInstance<ProductData>();
            data.id               = _id.Trim();
            data.brandName        = _brand.Trim();
            data.productName      = _productName.Trim();
            data.subtitle         = _subtitle.Trim();
            data.emotionalLine    = _emotional.Trim();
            data.shortDescription = _description.Trim();
            data.specs            = ParseSpecs(_specsText);
            data.fromPrice        = _price.Trim();
            data.productImage     = _image;

            AssetDatabase.CreateAsset(data, assetPath);

            // 3. Append to catalog.
            ProductCatalog catalog = _cabinetCategory.catalog;
            if (catalog == null)
            {
                Debug.LogError("[CabinetWizard] Cabinet category has no catalog assigned.", _cabinetCategory);
                return;
            }

            Undo.RecordObject(catalog, "Add Cabinet to Catalog");
            int newIndex = catalog.Count;

            SerializedObject soCatalog = new SerializedObject(catalog);
            SerializedProperty itemsProp = soCatalog.FindProperty("products");
            itemsProp.arraySize++;
            itemsProp.GetArrayElementAtIndex(itemsProp.arraySize - 1).objectReferenceValue = data;
            soCatalog.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);

            // 4. Assign the GaussianSplatAsset directly on the ProductData asset.
            SerializedObject soData = new SerializedObject(data);
            soData.FindProperty("splatAsset").objectReferenceValue = splatAsset;
            soData.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CabinetWizard] Added '{data.productName}' at catalog index {newIndex}. " +
                      $"Asset: {assetPath}  |  Splat: {splatAsset.name}", data);

            EditorGUIUtility.PingObject(data);

            // Reset form for next entry.
            _id = _brand = _productName = _subtitle = _emotional = _description = _specsText = _price = "";
            _image = null;
            _splatFilePath = "";

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
            _cabinetCategory = AssetDatabase.LoadAssetAtPath<ProductCategoryData>(CabinetCatalogPath);
            Repaint();
        }
    }
}
#endif
