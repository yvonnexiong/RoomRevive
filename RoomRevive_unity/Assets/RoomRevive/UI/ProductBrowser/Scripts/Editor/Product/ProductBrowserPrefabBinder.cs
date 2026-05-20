#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace RoomRevive.ProductBrowser.EditorTools
{
    /// <summary>
    /// Non-destructive sync tool for the ProductBrowser prefabs.
    ///
    /// SyncAll: walks the ProductBrowserUI prefab and fills any null serialized
    /// references on Controller, View, DiscoverPanel, and SwapPanel without
    /// touching layout, colors, or hierarchy.
    ///
    /// RebindRoot: re-wires references on a live scene instance (used by the
    /// custom inspector's Auto-Bind button after the user drops the prefab).
    ///
    /// Menu: Tools → RoomRevive → Product Browser → Sync (Non-Destructive)
    /// </summary>
    public static class ProductBrowserPrefabBinder
    {
        const string BrowserUIPrefabPath = "Assets/RoomRevive/UI/ProductBrowser/Prefabs/Product/ProductBrowserUI.prefab";
        const string DiscoverPrefabPath  = "Assets/RoomRevive/UI/ProductBrowser/Prefabs/Product/ProductDiscoverPanel.prefab";
        const string SwapPrefabPath      = "Assets/RoomRevive/UI/ProductBrowser/Prefabs/Product/ProductSwapPanel.prefab";

        const string FridgeCategoryPath  = "Assets/RoomRevive/UI/ProductBrowser/Data/Product/Category_Fridges.asset";
        const string CabinetCategoryPath = "Assets/RoomRevive/UI/ProductBrowser/Data/Product/Category_Cabinets.asset";
        const string LightsCategoryPath  = "Assets/RoomRevive/UI/ProductBrowser/Data/Product/Category_Lights.asset";

        // ── Menu entry ────────────────────────────────────────────────────────

        [MenuItem("Tools/RoomRevive/Product Browser/Sync (Non-Destructive)")]
        public static void SyncAll()
        {
            // Ensure child panel prefabs exist first.
            if (!PrefabExists(DiscoverPrefabPath) || !PrefabExists(SwapPrefabPath))
            {
                Debug.Log("[ProductBrowserPrefabBinder] Child panel prefabs missing — running creator first.");
                ProductBrowserPrefabCreator.CreateAll();
                return;
            }

            SyncBrowserUIPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[ProductBrowserPrefabBinder] Sync complete.");
        }

        /// <summary>
        /// Re-wires all null references on a live scene root.
        /// Safe to call at any time — only fills null slots.
        /// </summary>
        public static void RebindRoot(GameObject root)
        {
            if (root == null) return;

            ProductBrowserController controller = root.GetComponent<ProductBrowserController>();
            ProductBrowserView view = root.GetComponent<ProductBrowserView>();

            if (controller != null && view != null && controller.view == null)
            {
                Undo.RecordObject(controller, "Bind ProductBrowserView");
                controller.view = view;
                EditorUtility.SetDirty(controller);
            }

            if (view != null)
            {
                if (view.discoverPanel == null)
                {
                    view.discoverPanel = root.GetComponentInChildren<ProductDiscoverView>(true);
                    if (view.discoverPanel != null) EditorUtility.SetDirty(view);
                }
                if (view.swapPanel == null)
                {
                    view.swapPanel = root.GetComponentInChildren<ProductSwapView>(true);
                    if (view.swapPanel != null) EditorUtility.SetDirty(view);
                }
            }

            ProductVariantRouter router = root.GetComponent<ProductVariantRouter>();
            if (router != null && router.controller == null)
            {
                Undo.RecordObject(router, "Bind ProductBrowserController to router");
                router.controller = controller;
                EditorUtility.SetDirty(router);
            }

            ProductVisibilityRouter visRouter = root.GetComponent<ProductVisibilityRouter>();
            if (visRouter != null && visRouter.controller == null)
            {
                Undo.RecordObject(visRouter, "Bind ProductBrowserController to vis router");
                visRouter.controller = controller;
                EditorUtility.SetDirty(visRouter);
            }
        }

        // ── Internals ─────────────────────────────────────────────────────────

        static void SyncBrowserUIPrefab()
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BrowserUIPrefabPath);
            if (prefabAsset == null)
            {
                Debug.Log("[ProductBrowserPrefabBinder] ProductBrowserUI.prefab not found — running full creator.");
                ProductBrowserPrefabCreator.CreateAll();
                return;
            }

            string stagePath = BrowserUIPrefabPath;
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            bool alreadyOpen = stage != null && stage.assetPath == stagePath;

            if (!alreadyOpen)
            {
                // Open prefab temporarily, edit, save, close.
                GameObject contents = PrefabUtility.LoadPrefabContents(stagePath);
                BindContents(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, stagePath);
                PrefabUtility.UnloadPrefabContents(contents);
            }
            else
            {
                BindContents(stage.prefabContentsRoot);
            }
        }

        /// <summary>Called by ProductBrowserPrefabCreator.SyncOnly to bind an already-loaded prefab contents root.</summary>
        internal static void BindContentsInternal(GameObject root) => BindContents(root);

        static void BindContents(GameObject root)
        {
            // Ensure ray interaction is present (added non-destructively).
            if (root.GetComponent<RoomRevive.IntentSelector.MetaWorldSpaceCanvasSetup>() == null)
                root.AddComponent<RoomRevive.IntentSelector.MetaWorldSpaceCanvasSetup>();

            ProductBrowserController controller = root.GetComponent<ProductBrowserController>();
            ProductBrowserView view = root.GetComponent<ProductBrowserView>();

            if (controller == null || view == null) return;

            if (controller.view == null) controller.view = view;

            if (view.discoverPanel == null)
                view.discoverPanel = root.GetComponentInChildren<ProductDiscoverView>(true);

            if (view.swapPanel == null)
                view.swapPanel = root.GetComponentInChildren<ProductSwapView>(true);

            // Wire routers
            ProductVariantRouter varRouter = root.GetComponent<ProductVariantRouter>();
            if (varRouter != null && varRouter.controller == null) varRouter.controller = controller;

            ProductVisibilityRouter visRouter = root.GetComponent<ProductVisibilityRouter>();
            if (visRouter != null && visRouter.controller == null) visRouter.controller = controller;

            EditorUtility.SetDirty(root);
        }

        static bool PrefabExists(string path) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
    }
}
#endif
