using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RoomRevive.ProductBrowser
{
    /// <summary>
    /// Ordered list of <see cref="ProductData"/> assets for one category (Fridges, Cabinets, Lights).
    /// Assign one catalog per <see cref="ProductCategoryData"/>.
    ///
    /// Create via: Assets → Create → RoomRevive / Product Browser / Product Catalog
    /// </summary>
    [CreateAssetMenu(menuName = "RoomRevive/Product Browser/Product Catalog", fileName = "ProductCatalog_New")]
    public class ProductCatalog : ScriptableObject
    {
        [Tooltip("Ordered list of products. Element order is the display order in the swap panel.")]
        public List<ProductData> products = new List<ProductData>();

        public int Count => products != null ? products.Count : 0;

        public ProductData GetProduct(int index)
        {
            if (products == null) return null;
            if (index < 0 || index >= products.Count) return null;
            return products[index];
        }

        public int IndexOf(ProductData product)
        {
            if (products == null || product == null) return -1;
            return products.IndexOf(product);
        }

        public ProductData GetDefaultProduct()
        {
            if (products == null || products.Count == 0) return null;

            foreach (ProductData p in products)
            {
                if (p != null && p.startsSelectedByDefault) return p;
            }

            return products[0];
        }

        public int GetDefaultIndex()
        {
            ProductData def = GetDefaultProduct();
            return def != null ? IndexOf(def) : -1;
        }

        /// <summary>
        /// Stable-sorts the list so products with <see cref="ProductData.pinnedFirst"/> lead, keeping
        /// relative order within the pinned group and within the rest. Returns true if order changed.
        /// </summary>
        public bool SortPinnedFirst()
        {
            if (products == null || products.Count < 2) return false;
            var sorted = products.OrderByDescending(p => p != null && p.pinnedFirst).ToList();
            bool changed = false;
            for (int i = 0; i < products.Count; i++)
                if (!ReferenceEquals(products[i], sorted[i])) { changed = true; break; }
            if (changed) products = sorted;
            return changed;
        }

        /// <summary>Sorts pinned-first only if this catalog actually contains the given product.</summary>
        public bool SortPinnedFirstIfContains(ProductData product)
        {
            return products != null && products.Contains(product) && SortPinnedFirst();
        }
    }
}
