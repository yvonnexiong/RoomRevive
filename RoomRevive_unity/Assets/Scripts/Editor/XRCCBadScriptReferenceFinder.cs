#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class XRCCBadScriptReferenceFinder
{
    private static readonly string[] TargetScriptNames =
    {
        "XRCCMissingScriptFinder",
        "MetaRayFurnitureExampleCatalogCreator"
    };

    [MenuItem("Tools/XRCC/Find Bad Script References")]
    public static void FindBadScriptReferences()
    {
        Debug.Log("===== XRCC Bad Script Reference Scan Started =====");

        ScanOpenScenesForMissingScripts();
        ScanAllPrefabsForMissingScripts();
        SearchProjectFilesForTargetScriptGuids();

        Debug.Log("===== XRCC Bad Script Reference Scan Finished =====");
    }

    private static void ScanOpenScenesForMissingScripts()
    {
        Debug.Log("[XRCC] Scanning open scenes for missing scripts...");

        int foundCount = 0;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (!scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();

            foreach (GameObject root in roots)
            {
                foundCount += ScanGameObjectRecursive(root, scene.path, "Scene");
            }
        }

        if (foundCount == 0)
        {
            Debug.Log("[XRCC] No missing scripts found in currently open scenes.");
        }
    }

    private static void ScanAllPrefabsForMissingScripts()
    {
        Debug.Log("[XRCC] Scanning all prefabs for missing scripts...");

        int foundCount = 0;
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");

        foreach (string prefabGuid in prefabGuids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);

            GameObject prefabRoot = null;

            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

                if (prefabRoot != null)
                {
                    foundCount += ScanGameObjectRecursive(prefabRoot, prefabPath, "Prefab");
                }
            }
            catch
            {
                Debug.LogWarning("[XRCC] Could not scan prefab: " + prefabPath);
            }
            finally
            {
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        if (foundCount == 0)
        {
            Debug.Log("[XRCC] No missing scripts found in prefabs.");
        }
    }

    private static int ScanGameObjectRecursive(GameObject go, string assetPath, string assetType)
    {
        int foundCount = 0;

        int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);

        if (missingCount > 0)
        {
            foundCount += missingCount;

            Debug.LogError(
                $"[XRCC] Missing script found in {assetType}: {assetPath}\n" +
                $"GameObject path: {GetHierarchyPath(go)}\n" +
                $"Missing script count on this GameObject: {missingCount}",
                go
            );
        }

        foreach (Transform child in go.transform)
        {
            foundCount += ScanGameObjectRecursive(child.gameObject, assetPath, assetType);
        }

        return foundCount;
    }

    private static void SearchProjectFilesForTargetScriptGuids()
    {
        Debug.Log("[XRCC] Searching project files for direct references to target script GUIDs...");

        foreach (string scriptName in TargetScriptNames)
        {
            string[] scriptGuids = AssetDatabase.FindAssets(scriptName + " t:MonoScript");

            if (scriptGuids == null || scriptGuids.Length == 0)
            {
                Debug.LogWarning("[XRCC] Could not find script asset for: " + scriptName);
                continue;
            }

            foreach (string scriptGuid in scriptGuids)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuid);

                Debug.Log($"[XRCC] Script '{scriptName}' has GUID {scriptGuid} at path: {scriptPath}");

                SearchAssetsForGuid(scriptName, scriptGuid);
            }
        }
    }

    private static void SearchAssetsForGuid(string scriptName, string guid)
    {
        string assetsFolder = Application.dataPath;

        string[] files = Directory.GetFiles(assetsFolder, "*.*", SearchOption.AllDirectories);

        foreach (string absolutePath in files)
        {
            string extension = Path.GetExtension(absolutePath).ToLowerInvariant();

            bool isSearchableUnityFile =
                extension == ".unity" ||
                extension == ".prefab" ||
                extension == ".asset";

            if (!isSearchableUnityFile)
                continue;

            string text;

            try
            {
                text = File.ReadAllText(absolutePath);
            }
            catch
            {
                continue;
            }

            if (!text.Contains(guid))
                continue;

            string relativePath = "Assets" + absolutePath.Replace(Application.dataPath, "").Replace("\\", "/");
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(relativePath);

            Debug.LogError(
                $"[XRCC] Script reference found!\n" +
                $"Script: {scriptName}\n" +
                $"Referenced inside: {relativePath}\n" +
                $"Open this scene/prefab/asset and remove the broken component/reference.",
                asset
            );
        }
    }

    private static string GetHierarchyPath(GameObject go)
    {
        if (go == null)
            return "<null>";

        List<string> names = new List<string>();
        Transform current = go.transform;

        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }
}
#endif