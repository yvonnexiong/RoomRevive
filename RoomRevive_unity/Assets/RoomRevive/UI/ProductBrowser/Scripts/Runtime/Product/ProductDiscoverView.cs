using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoomRevive.ProductBrowser
{
    /// <summary>
    /// Binds a <see cref="ProductCategoryData"/> into the compact Discover panel.
    /// Shows only the category-level headline and subline — no product image, brand, or price.
    /// Both strings are authored on the ProductCategoryData SO, one asset per category.
    ///
    /// Visual layout is authored on the prefab. This script only assigns text.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class ProductDiscoverView : MonoBehaviour, IProductPanel
    {
        [Header("Authoring (drives preview in Prefab Mode)")]
        [Tooltip("Assign a ProductCategoryData asset to preview the panel in Prefab Mode.")]
        public ProductCategoryData previewCategory;

        [Header("Prefab Wiring")]
        [Tooltip("Title line at the top — mapped to ProductCategoryData.discoverHeadline.")]
        public TextMeshProUGUI headlineLabel;

        [Tooltip("Supporting sentence below the headline — mapped to ProductCategoryData.discoverSubline.")]
        public TextMeshProUGUI bodyLabel;

        [Tooltip("Button on the panel root (or CTA). Wired at runtime by ProductBrowserController to open the Swap panel.")]
        public Button ctaButton;

        // ── IProductPanel ────────────────────────────────────────────────────

        public void Bind(ProductCategoryData category, int productIndex)
        {
            Refresh(category);
        }

        // ── Editor live-preview ──────────────────────────────────────────────

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                if (previewCategory != null) Refresh(previewCategory);
            };
        }
#endif

        // ── Internal ─────────────────────────────────────────────────────────

        void Refresh(ProductCategoryData category)
        {
            if (headlineLabel != null)
                headlineLabel.text = category != null ? category.discoverHeadline : string.Empty;

            if (bodyLabel != null)
                bodyLabel.text = category != null ? category.discoverSubline : string.Empty;
        }
    }
}
