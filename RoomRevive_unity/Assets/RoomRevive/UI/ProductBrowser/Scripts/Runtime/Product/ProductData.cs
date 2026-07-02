using GaussianSplatting.Runtime;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace RoomRevive.ProductBrowser
{
    /// <summary>
    /// Data asset for a single browsable product (fridge, cabinet style, light fixture).
    /// One asset per product — assign a catalog to group them.
    ///
    /// Create via: Assets → Create → RoomRevive / Product Browser / Product Data
    /// </summary>
    [CreateAssetMenu(menuName = "RoomRevive/Product Browser/Product Data", fileName = "Product_New")]
    public class ProductData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id — used by routers and logs. Should be unique within a catalog.")]
        public string id;

        [Tooltip("If set, this product is synced from HTML_Editor/admin/catalog.json (matches a JSON item's modelKey). " +
                 "Managed by the catalog sync tool — leave EMPTY for hand-authored products so the sync never overwrites them.")]
        public string catalogKey;

        [Header("Card Content")]
        [Tooltip("Brand name shown above the product name (e.g. Miele, Nobilia, Neuhaus).")]
        public string brandName;

        [Tooltip("Short product or style name shown as the card headline.")]
        public string productName;

        [Tooltip("Small line under the product name, e.g. 'Freestanding refrigerator · white'.")]
        public string subtitle;

        [Tooltip("One emotional/lifestyle sentence. Used as the Discover card line AND the Swap panel headline.")]
        [TextArea(1, 3)]
        public string emotionalLine;

        [Tooltip("Supporting description shown on the Swap panel.")]
        [TextArea(2, 4)]
        public string shortDescription;

        [Header("Visuals")]
        [Tooltip("Primary product image used on both the Discover card and the Swap panel.")]
        public Sprite productImage;

        [Header("Specs")]
        [Tooltip("Short spec chips shown as pills, e.g. '141 L', '73 kWh/yr', '34 dB'. One entry per chip. Leave empty to hide the chip row.")]
        public string[] specs;

        [Header("Pricing")]
        [Tooltip("Optional price string, e.g. 'From $1,299'. A leading 'From' is shown as a separate label on the Swap panel. Leave empty to hide price.")]
        public string fromPrice;

        [Header("Gaussian Splat")]
        [Tooltip("Splat asset rendered when this product is active. Used by ProductVariantRouter in GaussianSplat mode.")]
        public GaussianSplatAsset splatAsset;

        [Header("Splat Editor Swap (Kitchens)")]
        [Tooltip("Cabinet-front material filename in HTML_Editor/CabinetMaterials, sent to the live splat editor as 'cab'. " +
                 "Auto-filled by the catalog sync from the kitchen's front design element. Empty for non-kitchen products.")]
        public string splatCabMaterial;

        [Tooltip("Worktop material filename in HTML_Editor/WorktopMaterials, sent to the live splat editor as 'wt'. " +
                 "Auto-filled by the catalog sync from the kitchen's worktop design element. Empty when the kitchen has no worktop.")]
        public string splatWtMaterial;

        [Header("Variants")]
        [Tooltip("Ordered list of selectable variants (colours, finishes, models). Optional.")]
        public ProductVariantData[] variants;

        [Header("Selection")]
        [Tooltip("If true, ProductCatalog.GetDefaultProduct() returns this product first.")]
        public bool startsSelectedByDefault;

        [Tooltip("If on, this product is pinned to the FRONT of every catalog it belongs to (above " +
                 "un-pinned products; multiple pinned products keep their relative order).")]
        public bool pinnedFirst;

        [Header("Optional Asset Event")]
        [Tooltip("Fired when this product is selected. NOTE: asset events cannot reference scene objects. " +
                 "Use ProductVariantRouter on the scene prefab for scene wiring.")]
        public UnityEvent onSelectedAssetEvent;

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += RefreshReferencingViews;
            UnityEditor.EditorApplication.delayCall += ResortContainingCatalogs;
        }

        // Re-orders any catalog holding this product so pinned products lead. Deferred — AssetDatabase
        // calls aren't allowed directly inside OnValidate.
        void ResortContainingCatalogs()
        {
            UnityEditor.EditorApplication.delayCall -= ResortContainingCatalogs;
            if (this == null) return;
            foreach (string guid in AssetDatabase.FindAssets("t:ProductCatalog"))
            {
                var cat = AssetDatabase.LoadAssetAtPath<ProductCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if (cat != null && cat.SortPinnedFirstIfContains(this))
                    EditorUtility.SetDirty(cat);
            }
        }

        void RefreshReferencingViews()
        {
            if (this == null) return;

#if UNITY_2022_2_OR_NEWER
            ProductSwapView[] views = FindObjectsByType<ProductSwapView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            ProductSwapView[] views = FindObjectsOfType<ProductSwapView>(true);
#endif
            foreach (ProductSwapView v in views)
            {
                if (v != null) v.RefreshIfDisplaying(this);
            }

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage?.prefabContentsRoot != null)
            {
                foreach (ProductSwapView v in stage.prefabContentsRoot.GetComponentsInChildren<ProductSwapView>(true))
                {
                    if (v != null) v.RefreshIfDisplaying(this);
                }
            }

            SceneView.RepaintAll();
        }
#endif
    }

    /// <summary>A single selectable variant within a product (e.g. colour, finish, model).</summary>
    [System.Serializable]
    public class ProductVariantData
    {
        [Tooltip("Label shown under the product image when this variant is active.")]
        public string label;

        [Tooltip("Optional per-variant image. Falls back to ProductData.productImage if null.")]
        public Sprite image;

        [Tooltip("Optional per-variant price override. Falls back to ProductData.fromPrice if empty.")]
        public string price;

        [Tooltip("Swatch color shown as a dot indicator in the swap panel.")]
        public Color swatchColor = new Color(0.8f, 0.8f, 0.8f);
    }
}
