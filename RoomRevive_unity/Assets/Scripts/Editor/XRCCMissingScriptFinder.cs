// Assets/Editor/XRCCMissingScriptFinder.cs

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class XRCCMissingScriptFinder
{
    [MenuItem("Tools/XRCC/Find Missing Scripts In Project")]
    public static void FindMissingScriptsInProject()
    {
        int totalMissing = 0;

        ScanPrefabs(ref totalMissing);
        ScanScenes(ref totalMissing);

        Debug.Log($"[XRCC] Missing script scan complete. Total missing scripts found: {totalMissing}");
    }

    private static void ScanPrefabs(ref int totalMissing)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);

            try
            {
                ScanGameObjectRecursive(prefabRoot, path, ref totalMissing);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }

    private static void ScanScenes(ref int totalMissing)
    {
        string currentScenePath = SceneManager.GetActiveScene().path;

        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");

        foreach (string guid in sceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                ScanGameObjectRecursive(root, path, ref totalMissing);
            }
        }

        if (!string.IsNullOrEmpty(currentScenePath))
        {
            EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
        }
    }

    private static void ScanGameObjectRecursive(GameObject go, string assetPath, ref int totalMissing)
    {
        int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);

        if (missingCount > 0)
        {
            totalMissing += missingCount;
            Debug.LogError(
                $"[XRCC] Missing script found on GameObject: '{GetHierarchyPath(go)}' in asset: {assetPath}. Missing count: {missingCount}",
                go
            );
        }

        foreach (Transform child in go.transform)
        {
            ScanGameObjectRecursive(child.gameObject, assetPath, ref totalMissing);
        }
    }

    private static string GetHierarchyPath(GameObject go)
    {
        string path = go.name;
        Transform current = go.transform.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
#endif