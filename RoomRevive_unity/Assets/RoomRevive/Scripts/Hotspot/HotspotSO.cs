using UnityEngine;

namespace RoomRevive
{
    [CreateAssetMenu(fileName = "Hotspot_New", menuName = "RoomRevive/Hotspot")]
    public class HotspotSO : ScriptableObject
    {
        public string id;
        public string displayName;
        public Vector3 worldAnchorOffset;
        public ProductSO linkedProduct;
    }
}
