using UnityEngine;

namespace RoomRevive
{
    [CreateAssetMenu(fileName = "Product_New", menuName = "RoomRevive/Product")]
    public class ProductSO : ScriptableObject
    {
        public string id;
        public string brandName;
        public string productName;
        [TextArea] public string emotionalLine;
        [TextArea] public string shortDescription;
        public Sprite thumbnail;
        public ProductVariant[] variants;

        [Header("Details Panel")]
        public DimensionSpec[] dimensions;
        [TextArea] public string materials;
        public string[] features;
        [TextArea] public string storage;
        public string fromPrice;
    }

    [System.Serializable]
    public class ProductVariant
    {
        public string name;
        public Sprite image;
        [TextArea] public string description;
        public string price;
        public Color swatchColor = new Color(0.8f, 0.8f, 0.8f);
    }

    [System.Serializable]
    public class DimensionSpec
    {
        public string label;
        public string value;
    }
}
