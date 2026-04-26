using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FridgeController))]
public class FridgeControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        FridgeController fridge = (FridgeController)target;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Test Controls", EditorStyles.boldLabel);

        bool inPlayMode = Application.isPlaying;

        // ── Left Door ────────────────────────────────────────────────
        EditorGUILayout.LabelField("Left Door", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f);
        if (GUILayout.Button(inPlayMode ? "Toggle" : "Open", GUILayout.Height(28)))
        {
            if (inPlayMode)
            {
                fridge.ToggleLeftDoor();
            }
            else
            {
                Undo.RecordObject(fridge, "Open Left Door");
                fridge.leftDoorAngle = 0f;   // open = 0
                fridge.Apply();
            }
        }

        if (!inPlayMode)
        {
            GUI.backgroundColor = new Color(0.85f, 0.4f, 0.4f);
            if (GUILayout.Button("Close", GUILayout.Height(28)))
            {
                Undo.RecordObject(fridge, "Close Left Door");
                fridge.leftDoorAngle = -90f;  // closed = -90
                fridge.Apply();
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // ── Right Door ───────────────────────────────────────────────
        EditorGUILayout.LabelField("Right Door", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f);
        if (GUILayout.Button(inPlayMode ? "Toggle" : "Open", GUILayout.Height(28)))
        {
            if (inPlayMode)
            {
                fridge.ToggleRightDoor();
            }
            else
            {
                Undo.RecordObject(fridge, "Open Right Door");
                fridge.rightDoorAngle = 0f;   // open = 0
                fridge.Apply();
            }
        }

        if (!inPlayMode)
        {
            GUI.backgroundColor = new Color(0.85f, 0.4f, 0.4f);
            if (GUILayout.Button("Close", GUILayout.Height(28)))
            {
                Undo.RecordObject(fridge, "Close Right Door");
                fridge.rightDoorAngle = 90f;  // closed = 90
                fridge.Apply();
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // ── Refrigerator Drawer ──────────────────────────────────────
        EditorGUILayout.LabelField("Refrigerator Drawer", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f);
        if (GUILayout.Button(inPlayMode ? "Toggle" : "Open", GUILayout.Height(28)))
        {
            if (inPlayMode)
            {
                fridge.ToggleRefrigerator();
            }
            else
            {
                Undo.RecordObject(fridge, "Open Refrigerator");
                fridge.refrigeratorY = -0.85f;
                fridge.Apply();
            }
        }

        if (!inPlayMode)
        {
            GUI.backgroundColor = new Color(0.85f, 0.4f, 0.4f);
            if (GUILayout.Button("Close", GUILayout.Height(28)))
            {
                Undo.RecordObject(fridge, "Close Refrigerator");
                fridge.refrigeratorY = -0.474f;
                fridge.Apply();
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        // ── All ───────────────────────────────────────────────────────
        if (!inPlayMode)
        {
            EditorGUILayout.LabelField("All", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.3f, 0.75f, 0.3f);
            if (GUILayout.Button("Open All", GUILayout.Height(32)))
            {
                Undo.RecordObject(fridge, "Open All Fridge Parts");
                fridge.leftDoorAngle  =   0f;   // open = 0
                fridge.rightDoorAngle =   0f;   // open = 0
                fridge.refrigeratorY  = -0.85f;
                fridge.Apply();
            }

            GUI.backgroundColor = new Color(0.75f, 0.3f, 0.3f);
            if (GUILayout.Button("Close All", GUILayout.Height(32)))
            {
                Undo.RecordObject(fridge, "Close All Fridge Parts");
                fridge.leftDoorAngle  = -90f;  // closed = -90
                fridge.rightDoorAngle =  90f;  // closed =  90
                fridge.refrigeratorY  = -0.474f;
                fridge.Apply();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        // ── Cooldown status (Play Mode only) ──────────────────────────
        if (inPlayMode)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Cooldown Status", EditorStyles.miniBoldLabel);
            GUI.enabled = false;
            EditorGUILayout.Toggle("Left Door ready",    !fridge.IsLeftDoorOnCooldown());
            EditorGUILayout.Toggle("Right Door ready",   !fridge.IsRightDoorOnCooldown());
            EditorGUILayout.Toggle("Refrigerator ready", !fridge.IsRefrigeratorOnCooldown());
            GUI.enabled = true;
        }
    }
}
