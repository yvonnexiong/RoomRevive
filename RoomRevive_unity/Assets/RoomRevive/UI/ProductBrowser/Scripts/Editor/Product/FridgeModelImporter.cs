#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomRevive.ProductBrowser.EditorTools
{
    /// <summary>
    /// One-shot: instantiate every catalog fridge's 3D model under the scene "Fridges" group and wire
    /// the FridgeBrowserUI's <see cref="ProductVariantRouter.objectVariants"/> so index i = the model for
    /// catalog product i. FBX models are expected at <see cref="FbxFolder"/> named after each product's
    /// catalogKey (produced by the GLB→FBX batch convert).
    ///
    /// Re-runnable: it removes previously-imported model instances first (everything under Fridges that
    /// isn't one of the four original hand-made variants), then rebuilds.
    /// </summary>
    public static class FridgeModelImporter
    {
        const string FridgeCategoryPath = "Assets/RoomRevive/UI/ProductBrowser/Data/Product/Category_Fridges.asset";
        const string FbxFolder          = "Assets/3DModels/Fridges/FromCatalog";

        static readonly string[] Originals = { "Modern-Fridge", "Ikea-Fridge", "FrenchDoorFridge", "NewFridge" };

        [MenuItem("Tools/RoomRevive/Product Browser/Import All Fridge Models + Wire Router")]
        public static void ImportAndWire()
        {
            var cat = AssetDatabase.LoadAssetAtPath<ProductCategoryData>(FridgeCategoryPath);
            if (cat == null || cat.catalog == null) { Debug.LogError("[FridgeImport] Fridge catalog missing"); return; }

            ProductVariantRouter router = null;
            foreach (var r in Object.FindObjectsByType<ProductVariantRouter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (r.swapMode == RouterSwapMode.Model3D) { router = r; break; }
            if (router == null) { Debug.LogError("[FridgeImport] No Model3D ProductVariantRouter found"); return; }

            Transform fridges = null;
            foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (tr.name == "Fridges" && tr.gameObject.scene.IsValid() && tr.Find("Modern-Fridge") != null) { fridges = tr; break; }
            if (fridges == null) { Debug.LogError("[FridgeImport] Fridges group not found"); return; }

            var keep = new HashSet<string>(Originals);

            // Snapshot the existing original variants by catalog slot BEFORE we rewrite the array.
            var existing = router.objectVariants;

            // Remove previously-imported catalog models (idempotent).
            for (int c = fridges.childCount - 1; c >= 0; c--)
            {
                var ch = fridges.GetChild(c);
                if (!keep.Contains(ch.name)) Undo.DestroyObjectImmediate(ch.gameObject);
            }

            Vector3 pl = fridges.lossyScale;
            Vector3 invParent = new Vector3(Safe(1f, pl.x), Safe(1f, pl.y), Safe(1f, pl.z)); // → world scale 1 (real size)

            var variants = new GameObject[cat.catalog.Count];
            int created = 0, missing = 0, kept = 0;

            for (int i = 0; i < cat.catalog.Count; i++)
            {
                var p = cat.catalog.GetProduct(i);
                if (p == null) continue;

                // Hand-made originals (no catalogKey): keep whatever the router already pointed at for this slot.
                if (string.IsNullOrEmpty(p.catalogKey))
                {
                    if (existing != null && i < existing.Length) { variants[i] = existing[i]; kept++; }
                    continue;
                }

                string fbxPath = $"{FbxFolder}/{p.catalogKey}.fbx";
                AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceSynchronousImport);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (asset == null) { Debug.LogWarning($"[FridgeImport] No FBX for key '{p.catalogKey}' at {fbxPath}"); missing++; continue; }

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, fridges);
                inst.name = p.catalogKey;
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale    = invParent;
                inst.SetActive(false);
                Undo.RegisterCreatedObjectUndo(inst, "Add fridge model");
                variants[i] = inst;
                created++;
            }

            router.objectVariants = variants;
            EditorUtility.SetDirty(router);
            EditorSceneManager.MarkSceneDirty(fridges.gameObject.scene);
            EditorSceneManager.SaveScene(fridges.gameObject.scene);
            Debug.Log($"[FridgeImport] Done — created {created}, kept {kept} originals, {missing} missing. " +
                      $"objectVariants size = {variants.Length}.");
        }

        static float Safe(float a, float b) => Mathf.Approximately(b, 0f) ? a : a / b;
    }
}
#endif
