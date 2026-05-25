#if UNITY_EDITOR
using RoomRevive.IntentSelector;
using UnityEditor;
using UnityEngine;

namespace RoomRevive.EditorTools
{
    [CustomEditor(typeof(WelcomeUI))]
    public class WelcomeUIEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            WelcomeUI ui = (WelcomeUI)target;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Welcome UI — Setup", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("🔧  Rebuild UI", GUILayout.Height(28)))
                {
                    ui.Rebuild();
                    EditorUtility.SetDirty(ui);
                }

                EditorGUILayout.Space(2);

                var raySetup = ui.GetComponent<MetaWorldSpaceCanvasSetup>();

                if (raySetup == null)
                {
                    EditorGUILayout.HelpBox("MetaWorldSpaceCanvasSetup not found on this GameObject. Add it to enable hand / ray interaction.", MessageType.Warning);

                    if (GUILayout.Button("➕  Add Ray Interaction Setup", GUILayout.Height(24)))
                    {
                        Undo.AddComponent<MetaWorldSpaceCanvasSetup>(ui.gameObject);
                        EditorUtility.SetDirty(ui.gameObject);
                    }
                }
                else
                {
                    bool configured = ui.GetComponent<Oculus.Interaction.PointableCanvas>() != null
                                   && ui.transform.Find("ISDK_RayInteractionSurface") != null;

                    EditorGUILayout.HelpBox(
                        configured
                            ? "Ray interaction is configured. ✓"
                            : "Ray interaction not yet configured — click Configure.",
                        configured ? MessageType.None : MessageType.Warning);

                    if (GUILayout.Button("🎯  Configure Ray Interaction", GUILayout.Height(24)))
                    {
                        raySetup.Configure();
                        EditorUtility.SetDirty(ui.gameObject);
                    }
                }
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            DrawDefaultInspector();
        }
    }
}
#endif
