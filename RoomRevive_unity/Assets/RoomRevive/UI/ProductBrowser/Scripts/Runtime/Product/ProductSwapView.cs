using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoomRevive.ProductBrowser
{
    /// <summary>
    /// Binds a <see cref="ProductCatalog"/> into the Swap panel prefab.
    /// Shows the active product image, brand (with accent dot), name, subtitle,
    /// headline, description, spec chips, price, dot indicators, and enables/disables
    /// the Prev/Next arrow buttons. Empty rows (subtitle / headline / specs / price)
    /// hide themselves.
    ///
    /// The favorite toggle on the SelectButton is owned by <see cref="FavoriteButton"/>
    /// — this view no longer manages its label or color.
    ///
    /// All visual wiring (layout, colors, fonts) is authored on the prefab.
    /// This script only assigns text strings, sprites, colors, and active state.
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
        [Tooltip("Small colored dot left of the brand. Tinted with the category accent color.")]
        public Image brandDot;
        public TextMeshProUGUI brandLabel;
        public TextMeshProUGUI productNameLabel;
        [Tooltip("Small line under the product name — mapped to ProductData.subtitle. Hidden when empty.")]
        public TextMeshProUGUI subtitleLabel;

        [Header("Prefab Wiring — Product")]
        public Image productImage;
        [Tooltip("Large feature line — mapped to ProductData.emotionalLine. Hidden when empty.")]
        public TextMeshProUGUI headlineLabel;
        public TextMeshProUGUI shortDescriptionLabel;
        [Tooltip("Parent of the spec chip pills. Chips are toggled active/inactive and their label set; do not destroy them. Hidden when ProductData.specs is empty.")]
        public Transform specChipsContainer;

        [Header("Prefab Wiring — Price")]
        [Tooltip("Static 'From' label left of the price value. Hidden when there is no price.")]
        public TextMeshProUGUI fromLabel;
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
        [Tooltip("Maximum dots shown. With more products than this, a sliding window of dots is shown and the chevrons indicate more.")]
        public int maxDots = 10;
        [Tooltip("Chevron shown when there are products BEFORE the visible dot window. Optional.")]
        public GameObject dotsPrevChevron;
        [Tooltip("Chevron shown when there are products AFTER the visible dot window. Optional.")]
        public GameObject dotsNextChevron;

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

            // Brand dot uses the category accent color.
            if (brandDot != null)
            {
                brandDot.color = _category.accentColor;
                brandDot.gameObject.SetActive(true);
            }

            ProductData product = catalog?.GetProduct(_currentIndex);
            if (product == null) return;

            if (brandLabel != null)          brandLabel.text = product.brandName;
            if (productNameLabel != null)    productNameLabel.text = product.productName;
            SetTextRow(subtitleLabel, product.subtitle);
            SetTextRow(headlineLabel, product.emotionalLine);
            if (shortDescriptionLabel != null) shortDescriptionLabel.text = product.shortDescription;

            if (productImage != null)
            {
                Sprite img = product.productImage;
                productImage.sprite = img;
                productImage.gameObject.SetActive(img != null);
            }

            // Price row: split a leading "From" off the value so it shows as
            // [From] [value] (e.g. "From $7,400" → From + "$7,400", "€870" → From + "€870").
            bool hasPrice = !string.IsNullOrEmpty(product.fromPrice);
            if (priceLabel != null) priceLabel.text = StripFromPrefix(product.fromPrice);
            if (fromLabel != null)  fromLabel.gameObject.SetActive(hasPrice);
            if (priceRow != null)   priceRow.SetActive(hasPrice);

            RefreshSpecChips(product);
            RefreshNavigation(catalog);
            RefreshDots(catalog);
        }

        /// <summary>Sets a label's text and hides its GameObject when the text is empty.</summary>
        static void SetTextRow(TextMeshProUGUI label, string text)
        {
            if (label == null) return;
            label.text = text;
            label.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        /// <summary>Removes a leading "From " token (case-insensitive) so the value sits beside the static From label.</summary>
        static string StripFromPrefix(string price)
        {
            if (string.IsNullOrEmpty(price)) return price;
            string trimmed = price.TrimStart();
            if (trimmed.StartsWith("From", System.StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(4).TrimStart();
            return trimmed;
        }

        void RefreshSpecChips(ProductData product)
        {
            if (specChipsContainer == null) return;

            string[] specs = product != null ? product.specs : null;
            int count = specs != null ? specs.Length : 0;

            specChipsContainer.gameObject.SetActive(count > 0);

            for (int i = 0; i < specChipsContainer.childCount; i++)
            {
                Transform chip = specChipsContainer.GetChild(i);
                bool show = i < count;
                chip.gameObject.SetActive(show);
                if (!show) continue;

                TextMeshProUGUI label = chip.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = specs[i];
            }
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
            int pool  = dotsContainer.childCount;          // how many dot objects exist in the prefab
            int max   = Mathf.Clamp(maxDots > 0 ? maxDots : pool, 1, pool);

            // Decide the visible window of dots.
            //  • count ≤ max → one dot per product, no chevrons.
            //  • count > max → a sliding window of `max` dots with the active dot kept centered;
            //    chevrons flag products hidden before / after the window.
            int start = 0, visible = count;
            bool moreBefore = false, moreAfter = false;
            if (count > max)
            {
                start       = Mathf.Clamp(_currentIndex - max / 2, 0, count - max);
                visible     = max;
                moreBefore  = start > 0;
                moreAfter   = start + max < count;
            }

            for (int i = 0; i < pool; i++)
            {
                var dot = dotsContainer.GetChild(i);
                bool show = i < visible;
                dot.gameObject.SetActive(show);
                if (!show) continue;

                Image img = dot.GetComponent<Image>();
                if (img == null) continue;

                bool isActive = (start + i) == _currentIndex;
                img.color = isActive ? dotActiveColor : dotInactiveColor;
                if (isActive  && dotActive   != null) img.sprite = dotActive;
                if (!isActive && dotInactive != null) img.sprite = dotInactive;
            }

            if (dotsPrevChevron != null) dotsPrevChevron.SetActive(moreBefore);
            if (dotsNextChevron != null) dotsNextChevron.SetActive(moreAfter);
        }
    }
}
