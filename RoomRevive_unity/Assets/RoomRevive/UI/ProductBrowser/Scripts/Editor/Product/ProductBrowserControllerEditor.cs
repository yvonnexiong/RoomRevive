#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace RoomRevive.ProductBrowser.EditorTools
{
    /// <summary>
    /// Custom inspector for <see cref="ProductBrowserController"/>.
    ///
    /// Shows a setup box with:
    ///   📸 Export Snapshot — reads current state to a Markdown file.
    ///   🔗 Sync            — creates missing assets/prefabs non-destructively.
    ///   ⚠  Rebuild         — full prefab rebuild (destructive, confirmation required).
    ///
    /// Also provides quick test buttons to open Discover / Swap / Close at edit-time.
    /// </summary>
    [CustomEditor(typeof(ProductBrowserController))]
    public class ProductBrowserControllerEditor : Editor
    {
        const string DefaultFridgeCategoryPath  = "Assets/RoomRevive/UI/ProductBrowser/Data/Product/Category_Fridges.asset";
        const string DefaultCabinetCategoryPath = "Assets/RoomRevive/UI/ProductBrowser/Data/Product/Category_Cabinets.asset";
        const string DefaultLightsCategoryPath  = "Assets/RoomRevive/UI/ProductBrowser/Data/Product/Category_Lights.asset";

        public override void OnInspectorGUI()
        {
            ProductBrowserController controller = (ProductBrowserController)target;

            DrawSetupBox(controller);
            EditorGUILayout.Space(6);

            if (Application.isPlaying)
                DrawPlayModeControls(controller);

            EditorGUILayout.Space(4);
            DrawInspectorWithProductPopup(controller);
        }

        void DrawInspectorWithProductPopup(ProductBrowserController controller)
        {
            serializedObject.Update();

            SerializedProperty prop = serializedObject.GetIterator();
            prop.NextVisible(true); // skip m_Script header

            while (prop.NextVisible(false))
            {
                if (prop.name == "initialProductIndex")
                    DrawProductIndexPopup(controller, prop);
                else
                    EditorGUILayout.PropertyField(prop, true);
            }

            serializedObject.ApplyModifiedProperties();
        }

        void DrawProductIndexPopup(ProductBrowserController controller, SerializedProperty indexProp)
        {
            ProductCategoryData cat = controller.initialCategory
                ?? controller.fridgesCategory
                ?? controller.cabinetsCategory
                ?? controller.lightsCategory;

            if (cat?.catalog == null || cat.catalog.Count == 0)
            {
                EditorGUILayout.PropertyField(indexProp);
                return;
            }

            int count = cat.catalog.Count;
            string[] options = new string[count];
            for (int i = 0; i < count; i++)
            {
                ProductData p = cat.catalog.GetProduct(i);
                options[i] = p != null
                    ? $"{i + 1}. {p.productName}"
                    : $"{i}: (empty)";
            }

            int current = Mathf.Clamp(indexProp.intValue, 0, count - 1);
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUILayout.Popup(
                new GUIContent("Initial Product", $"Catalog: {cat.displayName} ({count} items)"),
                current,
                options);
            if (EditorGUI.EndChangeCheck())
            {
                indexProp.intValue = selected;
                serializedObject.ApplyModifiedProperties();
                // Trigger live preview in the editor.
                if (!Application.isPlaying)
                    controller.SendMessage("OnValidate", null, SendMessageOptions.DontRequireReceiver);
            }
        }

        // ── Setup box ─────────────────────────────────────────────────────────

        void DrawSetupBox(ProductBrowserController controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Product Browser — Setup", EditorStyles.boldLabel);

            bool hasView = controller.view != null;
            EditorGUILayout.LabelField($"View: {(hasView ? "✓ wired" : "⚠ missing — run Auto-Bind")}");

            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("📸  Export Snapshot (read-only)", GUILayout.Height(24)))
                    ProductBrowserSnapshotExporter.ExportSnapshot();

                if (GUILayout.Button("🔗  Sync (non-destructive)", GUILayout.Height(24)))
                {
                    ProductBrowserPrefabBinder.SyncAll();
                    AutoBindReferences(controller);
                }

                if (GUILayout.Button("🔧  Auto-Bind References on this controller", GUILayout.Height(22)))
                    AutoBindReferences(controller);

                EditorGUILayout.Space(4);

                Color prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.85f, 0.5f, 0.5f);
                if (GUILayout.Button("⚠  Rebuild Prefabs (destructive)", GUILayout.Height(28)))
                {
                    if (EditorUtility.DisplayDialog(
                        "Rebuild Product Browser Prefabs",
                        "This will regenerate the ProductBrowserUI, ProductDiscoverPanel and ProductSwapPanel prefabs " +
                        "from scratch. Any manual layout edits on those prefabs will be lost.\n\nContinue?",
                        "Rebuild", "Cancel"))
                    {
                        ProductBrowserPrefabCreator.CreateAll();
                        AutoBindReferences(controller);
                    }
                }
                GUI.backgroundColor = prevBg;
            }

            EditorGUILayout.HelpBox(
                "Workflow: Snapshot → Sync → (Rebuild only when truly needed).\n\n" +
                "• Snapshot saves the current prefab and data state to Snapshots/.\n" +
                "• Sync creates missing assets/prefabs and re-wires references. Safe.\n" +
                "• Rebuild regenerates all prefabs from scratch — destroys manual edits.",
                MessageType.None);

            EditorGUILayout.EndVertical();
        }

        // ── Play-mode quick controls ──────────────────────────────────────────

        void DrawPlayModeControls(ProductBrowserController controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"State: {controller.CurrentState}    " +
                                       $"Product: {controller.SelectedIndex}    " +
                                       $"Category: {controller.ActiveCategory?.displayName ?? "none"}",
                                       EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();

            ProductCategoryData fridgeCat  = controller.fridgesCategory
                ?? AssetDatabase.LoadAssetAtPath<ProductCategoryData>(DefaultFridgeCategoryPath);
            if (GUILayout.Button("Open Fridges"))  controller.OpenDiscover(fridgeCat);

            ProductCategoryData cabinetCat = controller.cabinetsCategory
                ?? AssetDatabase.LoadAssetAtPath<ProductCategoryData>(DefaultCabinetCategoryPath);
            if (GUILayout.Button("Open Cabinets")) controller.OpenDiscover(cabinetCat);

            ProductCategoryData lightsCat  = controller.lightsCategory
                ?? AssetDatabase.LoadAssetAtPath<ProductCategoryData>(DefaultLightsCategoryPath);
            if (GUILayout.Button("Open Lights"))   controller.OpenDiscover(lightsCat);

            if (GUILayout.Button("Close"))         controller.Close();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // ── Auto-bind ─────────────────────────────────────────────────────────

        void AutoBindReferences(ProductBrowserController controller)
        {
            Undo.RecordObject(controller, "Auto-Bind Product Browser References");

            if (controller.view == null)
            {
                controller.view = controller.GetComponent<ProductBrowserView>();
                if (controller.view == null)
                    controller.view = controller.GetComponentInChildren<ProductBrowserView>(true);
            }

            EditorUtility.SetDirty(controller);

            if (controller.view != null)
                ProductBrowserPrefabBinder.RebindRoot(controller.gameObject);
        }
    }
}
#endif
