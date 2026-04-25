using UnityEditor;
using UnityEngine;
using System.IO;

[CustomEditor(typeof(MRUKRoomToFBXExporter))]
public class MRUKRoomToFBXExporterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MRUKRoomToFBXExporter exporter = (MRUKRoomToFBXExporter)target;
        serializedObject.Update();

        // ── Target Prefab ─────────────────────────────────────────────
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("targetPrefab"),
            new GUIContent("Target Prefab / GameObject",
                "Drag the room prefab or scene GameObject to export. " +
                "Leave empty to use this GameObject.")
        );

        EditorGUILayout.Space(8);

        // ── Export Folder with Browse button ──────────────────────────
        EditorGUILayout.LabelField("Destination", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        SerializedProperty folderProp = serializedObject.FindProperty("exportFolder");
        EditorGUILayout.PropertyField(folderProp, new GUIContent("Root Export Folder"));
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string current = folderProp.stringValue;
            if (!Path.IsPathRooted(current))
                current = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "..", current));
            string chosen = EditorUtility.OpenFolderPanel(
                "Choose Root Export Folder", current, "");
            if (!string.IsNullOrEmpty(chosen))
            {
                string projectRoot = Path.GetFullPath(
                    Path.Combine(Application.dataPath, ".."));
                if (chosen.StartsWith(projectRoot))
                    chosen = chosen.Substring(projectRoot.Length)
                                   .TrimStart('/', '\\');
                folderProp.stringValue = chosen;
            }
        }
        EditorGUILayout.EndHorizontal();

        // ── Sub-folder / file name override ───────────────────────────
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("exportFileName"),
            new GUIContent("Sub-folder Name (optional)",
                "Override the auto-generated sub-folder name. " +
                "Leave blank to use the prefab/GameObject name.")
        );

        serializedObject.ApplyModifiedProperties();

        // ── Preview the output path ───────────────────────────────────
        EditorGUILayout.Space(4);
        string folder   = serializedObject.FindProperty("exportFolder").stringValue;
        string nameOver = serializedObject.FindProperty("exportFileName").stringValue;
        var    src      = serializedObject.FindProperty("targetPrefab").objectReferenceValue;
        string subName  = !string.IsNullOrEmpty(nameOver)
            ? nameOver
            : (src != null ? src.name : exporter.gameObject.name);
        EditorGUILayout.HelpBox(
            $"Output folder:\n{folder}/{subName}/\n\nEach child object will be saved as its own .fbx file inside that folder.",
            MessageType.None
        );

        EditorGUILayout.Space(10);

        // ── Export Button (per-object) ────────────────────────────────
        GUI.backgroundColor = new Color(0.3f, 0.75f, 0.3f);
        if (GUILayout.Button("Export Room To FBX (one .fbx per child)", GUILayout.Height(38)))
        {
            exporter.ExportToFBX();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);

        // ── Export Button (combined shell) ────────────────────────────
        GUI.backgroundColor = new Color(0.3f, 0.6f, 0.85f);
        if (GUILayout.Button("Export Structure Only (Walls + Floor + Ceiling, combined)",
                             GUILayout.Height(32)))
        {
            exporter.ExportStructureToFBX();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "• Green button: each child object is exported as a separate .fbx into the sub-folder (duplicate names get _1, _2 …).\n" +
            "• Blue button: walls, floor, and ceiling/roof are merged into a SINGLE Structure.fbx in the same sub-folder. Matching is case-insensitive on the name substrings WALL / FLOOR / CEILING / ROOF.\n" +
            "• Run in Play Mode to capture MRUK runtime meshes.",
            MessageType.Info
        );
    }
}

[CustomEditor(typeof(MRUKRoomToFBXExporter2))]
public class MRUKRoomToFBXExporter2Editor : Editor
{
    public override void OnInspectorGUI()
    {
        MRUKRoomToFBXExporter2 exporter = (MRUKRoomToFBXExporter2)target;
        serializedObject.Update();

        // ── Target Prefab ─────────────────────────────────────────────
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("targetPrefab"),
            new GUIContent("Target Prefab / GameObject",
                "Drag the room prefab or scene GameObject to export. " +
                "Leave empty to use this GameObject.")
        );

        EditorGUILayout.Space(8);

        // ── Export Folder with Browse button ──────────────────────────
        EditorGUILayout.LabelField("Destination", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        SerializedProperty folderProp = serializedObject.FindProperty("exportFolder");
        EditorGUILayout.PropertyField(folderProp, new GUIContent("Root Export Folder"));
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string current = folderProp.stringValue;
            if (!Path.IsPathRooted(current))
                current = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "..", current));
            string chosen = EditorUtility.OpenFolderPanel(
                "Choose Root Export Folder", current, "");
            if (!string.IsNullOrEmpty(chosen))
            {
                string projectRoot = Path.GetFullPath(
                    Path.Combine(Application.dataPath, ".."));
                if (chosen.StartsWith(projectRoot))
                    chosen = chosen.Substring(projectRoot.Length)
                                   .TrimStart('/', '\\');
                folderProp.stringValue = chosen;
            }
        }
        EditorGUILayout.EndHorizontal();

        // ── Sub-folder / file name override ───────────────────────────
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("exportFileName"),
            new GUIContent("Sub-folder Name (optional)",
                "Override the auto-generated sub-folder name. " +
                "Leave blank to use the prefab/GameObject name.")
        );

        serializedObject.ApplyModifiedProperties();

        // ── Preview the output path ───────────────────────────────────
        EditorGUILayout.Space(4);
        string folder   = serializedObject.FindProperty("exportFolder").stringValue;
        string nameOver = serializedObject.FindProperty("exportFileName").stringValue;
        var    src      = serializedObject.FindProperty("targetPrefab").objectReferenceValue;
        string subName  = !string.IsNullOrEmpty(nameOver)
            ? nameOver
            : (src != null ? src.name : exporter.gameObject.name);
        EditorGUILayout.HelpBox(
            $"Output folder:\n{folder}/{subName}/\n\nEach child object will be saved as its own .fbx file inside that folder.",
            MessageType.None
        );

        EditorGUILayout.Space(10);

        // ── Export Button (per-object) ────────────────────────────────
        GUI.backgroundColor = new Color(0.3f, 0.75f, 0.3f);
        if (GUILayout.Button("Export Room To FBX (one .fbx per child)", GUILayout.Height(38)))
        {
            exporter.ExportToFBX();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);

        // ── Export Button (combined shell) ────────────────────────────
        GUI.backgroundColor = new Color(0.3f, 0.6f, 0.85f);
        if (GUILayout.Button("Export Structure Only (Walls + Floor + Ceiling, combined)",
                             GUILayout.Height(32)))
        {
            exporter.ExportStructureToFBX();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "• Green button: each child object is exported as a separate .fbx into the sub-folder (duplicate names get _1, _2 …).\n" +
            "• Blue button: walls, floor, and ceiling/roof are merged into a SINGLE Structure.fbx in the same sub-folder. Matching is case-insensitive on the name substrings WALL / FLOOR / CEILING / ROOF.\n" +
            "• Run in Play Mode to capture MRUK runtime meshes.",
            MessageType.Info
        );
    }
}
