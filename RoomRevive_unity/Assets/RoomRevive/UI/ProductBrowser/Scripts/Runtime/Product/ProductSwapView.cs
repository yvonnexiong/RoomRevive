using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoomRevive.ProductBrowser
{
    /// <summary>
    /// Binds a <see cref="ProductCatalog"/> into the Swap panel prefab.
    /// Shows the active product image, brand, name, description, price,
    /// dot indicators, and enables/disables the Prev/Next arrow buttons.
    ///
    /// The favorite toggle on the SelectButton is owned by <see cref="FavoriteButton"/>
    /// — this view no longer manages its label or color.
    ///
    /// All visual wiring (layout, colors, fonts) is authored on the prefab.
    /// This script only assigns text strings, sprites, and dot states.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class ProductSwapView : MonoBehaviour, IProductPanel
    {
        [Header("Authoring (drives preview in Prefab Mode)")]
        [Tooltip("Assign a ProductCategoryData asset here to preview the panel in Prefab Mode.")]
        public ProductCategoryData previewCategory;
        [Tooltip("Product index to preview.")]
        [Min(0)] public int previewIndex = 0;

        [Header("Prefab Wiring — Header")]
        public TextMeshProUGUI categoryLabel;
        public TextMeshProUGUI brandLabel;
        public TextMeshProUGUI productNameLabel;

        [Header("Prefab Wiring — Product")]
        public Image productImage;
        public TextMeshProUGUI shortDescriptionLabel;
        public TextMeshProUGUI priceLabel;
        public GameObject priceRow;

        [Header("Prefab Wiring — Navigation")]
        [Tooltip("Previous product button — auto-disabled when at the first product.")]
        public Button prevButton;
        [Tooltip("Next product button — auto-disabled when at the last product.")]
        public Button nextButton;

        [Header("Prefab Wiring — Dots")]
        [Tooltip("Parent that holds the dot indicator Images. Dots are toggled active/inactive; do not destroy them.")]
        public Transform dotsContainer;
        [Tooltip("Active dot sprite (current product). Optional — color fallback used when null.")]
        public Sprite dotActive;
        [Tooltip("Inactive dot sprite. Optional — color fallback used when null.")]
        public Sprite dotInactive;
        [Tooltip("Color of the active dot. Set by PrefabCreator; override in inspector if needed.")]
        public Color dotActiveColor   = new Color(0.227f, 0.251f, 0.333f, 1f);
        [Tooltip("Color of the inactive dot. Set by PrefabCreator; override in inspector if needed.")]
        public Color dotInactiveColor = new Color(0.227f, 0.251f, 0.333f, 0.25f);

        // ── Tracking ─────────────────────────────────────────────────────────

        ProductCategoryData _category;
        int _currentIndex;

        // ── IProductPanel ────────────────────────────────────────────────────

        public void Bind(ProductCategoryData category, int productIndex)
        {
            _category = category;
            _currentIndex = productIndex;
            Refresh();
        }

        /// <summary>Called by <see cref="ProductData"/>.OnValidate to refresh if this product is displayed.</summary>
        public void RefreshIfDisplaying(ProductData product)
        {
            if (_category?.catalog == null) return;
            if (_category.catalog.GetProduct(_currentIndex) == product) Refresh();
        }

        // ── Editor live-preview ──────────────────────────────────────────────

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                if (previewCategory != null) Bind(previewCategory, previewIndex);
            };
        }
#endif

        // ── Internal refresh ─────────────────────────────────────────────────

        void Refresh()
        {
            if (_category == null) return;
            ProductCatalog catalog = _category.catalog;

            if (categoryLabel != null) categoryLabel.text = _category.displayName;

            ProductData product = catalog?.GetProduct(_currentIndex);
            if (product == null) return;

            if (brandLabel != null)          brandLabel.text = product.brandName;
            if (productNameLabel != null)    productNameLabel.text = product.productName;
            if (shortDescriptionLabel != null) shortDescriptionLabel.text = product.shortDescription;

            if (productImage != null)
            {
                Sprite img = product.productImage;
                productImage.sprite = img;
                productImage.gameObject.SetActive(img != null);
            }

            bool hasPrice = !string.IsNullOrEmpty(product.fromPrice);
            if (priceLabel != null) priceLabel.text = product.fromPrice;
            if (priceRow != null)   priceRow.SetActive(hasPrice);

            RefreshNavigation(catalog);
            RefreshDots(catalog);
        }

        void RefreshNavigation(ProductCatalog catalog)
        {
            if (catalog == null) return;
            int count = catalog.Count;

            if (prevButton != null) prevButton.interactable = _currentIndex > 0;
            if (nextButton != null) nextButton.interactable = _currentIndex < count - 1;
        }

        void RefreshDots(ProductCatalog catalog)
        {
            if (dotsContainer == null || catalog == null) return;
            int count = catalog.Count;

            for (int i = 0; i < dotsContainer.childCount; i++)
            {
                var dot = dotsContainer.GetChild(i);
                bool show = i < count;
                dot.gameObject.SetActive(show);

                if (!show) continue;

                Image img = dot.GetComponent<Image>();
                if (img == null) continue;

                bool isActive = i == _currentIndex;
                // Always update color (driven by dotActiveColor / dotInactiveColor).
                img.color = isActive ? dotActiveColor : dotInactiveColor;
                // Optionally swap sprites if designer has provided distinct active/inactive sprites.
                if (isActive  && dotActive   != null) img.sprite = dotActive;
                if (!isActive && dotInactive != null) img.sprite = dotInactive;
            }
        }
    }
}
