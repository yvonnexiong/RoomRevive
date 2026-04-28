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
    }

    [System.Serializable]
    public class ProductVariant
    {
        public string name;
        public Sprite image;
        [TextArea] public string description;
        public string price;
    }
}
