using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(BoxMoverPanel))]
public class BoxMoverPanelEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        var p = (BoxMoverPanel)target;
        bool alreadySpawned = p.IsSpawned();

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(alreadySpawned))
            {
                GUI.backgroundColor = new Color(0.4f, 0.9f, 1f);
                if (GUILayout.Button(alreadySpawned ? "Already Spawned" : "Spawn UI", GUILayout.Height(32)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(p.gameObject, "Spawn BoxMoverPanel UI");
                    p.SpawnUI();
                    MarkDirty(p);
                }
            }
            using (new EditorGUI.DisabledScope(!alreadySpawned))
            {
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("Clear", GUILayout.Height(32), GUILayout.Width(80)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(p.gameObject, "Clear BoxMoverPanel UI");
                    p.ClearSpawned();
                    MarkDirty(p);
                }
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.HelpBox(
            "Builds a world-space canvas with LEFT / RIGHT buttons wired to Meta SDK ray " +
            "interaction (PointableCanvas + RayInteractable + PlaneSurface). " +
            "If 'Target Box' is unset, the panel finds (or creates) a cube named 'MovableBox'.",
            MessageType.None);
    }

    static void MarkDirty(BoxMoverPanel p)
    {
        if (Application.isPlaying) return;
        EditorUtility.SetDirty(p);
        EditorSceneManager.MarkSceneDirty(p.gameObject.scene);
    }
}
