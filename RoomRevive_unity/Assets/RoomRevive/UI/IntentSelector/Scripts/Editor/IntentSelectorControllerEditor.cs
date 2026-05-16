using UnityEditor;
using UnityEngine;

namespace RoomRevive.IntentSelector.EditorTools
{
    /// <summary>
    /// Inspector for IntentSelectorController with a one-click "Rebuild" button that
    /// scaffolds default assets + prefab via IntentSelectorPrefabCreator and auto-binds
    /// catalog / theme / view on the current component when those references are empty.
    /// </summary>
    [CustomEditor(typeof(IntentSelectorController))]
    public class IntentSelectorControllerEditor : Editor
    {
        const string DefaultCatalogPath = "Assets/RoomRevive/UI/IntentSelector/Data/IntentStateCatalog.asset";
        const string DefaultThemePath = "Assets/RoomRevive/UI/IntentSelector/Data/IntentSelectorTheme.asset";

        public override void OnInspectorGUI()
        {
            IntentSelectorController controller = (IntentSelectorController)target;

            DrawSetupBox(controller);

            EditorGUILayout.Space(6);
            DrawDefaultInspector();
        }

        void DrawSetupBox(IntentSelectorController controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Intent Selector — Setup", EditorStyles.boldLabel);

            // Status row.
            bool hasCatalog = controller.catalog != null;
            bool hasTheme = controller.theme != null;
            bool hasView = controller.view != null;

            EditorGUILayout.LabelField($"Catalog: {(hasCatalog ? "✓" : "missing")}     " +
                                       $"Theme: {(hasTheme ? "✓" : "missing")}     " +
                                       $"View: {(hasView ? "✓" : "missing")}");

            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                // Order matters: read first → safe edit → destructive last.

                if (GUILayout.Button("📸  Export Snapshot (read-only)", GUILayout.Height(24)))
                {
                    IntentSelectorSnapshotExporter.ExportSnapshot();
                }

                if (GUILayout.Button("🔗  Sync (non-destructive)", GUILayout.Height(24)))
                {
                    IntentSelectorPrefabCreator.SyncOnly();
                    AutoBindReferences(controller);
                }

                if (GUILayout.Button("🔧  Auto-Bind References on this controller", GUILayout.Height(22)))
                {
                    AutoBindReferences(controller);
                }

                EditorGUILayout.Space(4);

                Color prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.85f, 0.5f, 0.5f);
                if (GUILayout.Button("⚠  Rebuild (destructive — wipes manual edits)", GUILayout.Height(28)))
                {
                    IntentSelectorPrefabCreator.RebuildWithConfirmation();
                    AutoBindReferences(controller);
                }
                GUI.backgroundColor = prevBg;
            }

            EditorGUILayout.HelpBox(
                "Workflow: Snapshot → Sync → (Rebuild only when truly needed).\n\n" +
                "• Snapshot dumps the current prefab + data state to a markdown file under Snapshots/. Run this first when you (or an AI) is about to change something.\n" +
                "• Sync creates missing assets and re-wires references, but never overwrites existing prefab values or hierarchy.\n" +
                "• Rebuild regenerates the prefab from scratch — destroys any manual layout / colour / font edits.",
                MessageType.None);

            EditorGUILayout.EndVertical();
        }

        void AutoBindReferences(IntentSelectorController controller)
        {
            Undo.RecordObject(controller, "Auto-Bind Intent Selector References");

            if (controller.catalog == null)
                controller.catalog = AssetDatabase.LoadAssetAtPath<IntentStateCatalog>(DefaultCatalogPath);

            if (controller.theme == null)
                controller.theme = AssetDatabase.LoadAssetAtPath<IntentSelectorTheme>(DefaultThemePath);

            if (controller.view == null)
            {
                controller.view = controller.GetComponent<IntentSelectorView>();
                if (controller.view == null)
                    controller.view = controller.GetComponentInChildren<IntentSelectorView>(true);
            }

            EditorUtility.SetDirty(controller);

            // Also bind the view's cardsContainer/cardTemplate if it's the prefab's own view.
            if (controller.view != null)
            {
                IntentSelectorPrefabBinder.RebindRoot(controller.gameObject);
            }
        }
    }
}
