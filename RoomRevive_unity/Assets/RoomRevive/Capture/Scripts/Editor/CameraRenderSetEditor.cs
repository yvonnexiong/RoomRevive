using System.IO;
using UnityEditor;
using UnityEngine;

namespace RoomRevive.Capture.EditorTools
{
    /// <summary>
    /// Inspector buttons for <see cref="CameraRenderSet"/>: preview the isolation (hold it on so you
    /// can frame the shot), restore the scene, and capture a PNG of just the render set.
    /// </summary>
    [CustomEditor(typeof(CameraRenderSet))]
    public class CameraRenderSetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var set = (CameraRenderSet)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Capture", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (set.IsIsolated)
                {
                    if (GUILayout.Button("Restore Scene")) { set.EndIsolation(); RepaintViews(); }
                }
                else if (GUILayout.Button("Isolate (Preview)")) { set.BeginIsolation(); RepaintViews(); }

                if (GUILayout.Button("Capture")) Capture(set);
            }

            EditorGUILayout.HelpBox(
                set.IsIsolated
                    ? "Previewing: the camera is restricted to 'Render Layers'. Click 'Restore " +
                      "Scene' when done."
                    : $"Capture overwrites a single file: {set.outputPath}\n" +
                      "Set 'Render Layers' to the layers you want captured first.",
                MessageType.Info);
        }

        void Capture(CameraRenderSet set)
        {
            string path = CaptureToFile(set);
            if (path == null) return;
            // Select & ping the asset so it's easy to find.
            var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (asset != null) EditorGUIUtility.PingObject(asset);
        }

        /// <summary>Captures and overwrites the component's single output file. Returns the asset path.</summary>
        public static string CaptureToFile(CameraRenderSet set)
        {
            string path = string.IsNullOrEmpty(set.outputPath) ? "Assets/Captures/RoomCapture.png" : set.outputPath;

            // Ensure the folder exists.
            string dir = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
                Directory.CreateDirectory(Path.GetFullPath(dir));

            Texture2D tex = set.CaptureToTexture(Mathf.Max(1, set.captureWidth), Mathf.Max(1, set.captureHeight));
            if (tex == null) return null;

            File.WriteAllBytes(Path.GetFullPath(path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[CameraRenderSet] Capture saved (overwrote) → {path}");
            return path;
        }

        static void RepaintViews()
        {
            SceneView.RepaintAll();
            foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (w.GetType().Name == "GameView") w.Repaint();
        }
    }
}
