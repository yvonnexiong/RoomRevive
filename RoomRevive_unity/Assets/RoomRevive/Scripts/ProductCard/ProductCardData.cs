using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RoomRevive
{
    /// <summary>
    /// ScriptableObject data asset for a single product in the MetaRayProductCardMenu carousel.
    /// Create via: Assets → Create → RoomRevive → Product Card Data
    /// </summary>
    [CreateAssetMenu(fileName = "ProductCardData_New", menuName = "RoomRevive/Product Card Data", order = 2)]
    public class ProductCardData : ScriptableObject
    {
        [Header("Product Info")]
        public string productName = "Product Name";
        public string category    = "Category";
        public Sprite productImage;

        [Header("Appearance")]
        [Tooltip("Accent color: active dot indicator + SPECIFICATIONS header.")]
        public Color accentColor  = new Color(0.58f, 0.67f, 1f, 1f);
        [Tooltip("Background fill of the image placeholder area.")]
        public Color imageBgColor = new Color(0.15f, 0.12f, 0.35f, 1f);

        [Header("Specifications")]
        [Tooltip("Each entry fills one chip in the 2-column spec grid.")]
        public List<ProductSpec> specs = new List<ProductSpec>();

        [Header("Events")]
        [Tooltip("Fired when this product becomes the active carousel item.")]
        public UnityEvent onSelected;
        [Tooltip("Fired when the user confirms (e.g. pinch) while this product is active.")]
        public UnityEvent onConfirmed;
    }

    /// <summary>One spec chip: icon emoji, short label, formatted value.</summary>
    [Serializable]
    public class ProductSpec
    {
        [Tooltip("Emoji / symbol shown top-left of chip, e.g. 📏 or ↔")]
        public string icon  = "📏";
        [Tooltip("Short uppercase descriptor shown above the value, e.g. BASE HEIGHT")]
        public string label = "LABEL";
        [Tooltip("Formatted value string, e.g. 42 cm or Hinged")]
        public string value = "0 cm";
    }
}
