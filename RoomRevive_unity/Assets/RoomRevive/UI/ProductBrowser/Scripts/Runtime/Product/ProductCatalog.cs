using System.Collections.Generic;
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
    }
}
