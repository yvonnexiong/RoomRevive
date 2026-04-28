using UnityEngine;
using GaussianSplatting.Runtime;

namespace RoomRevive
{
    [CreateAssetMenu(fileName = "Intent_New", menuName = "RoomRevive/Intent")]
    public class IntentSO : ScriptableObject
    {
        public string id;
        public string displayName;
        public GaussianSplatAsset splatAsset;

        [Header("Hotspot Visuals")]
        public Color hotspotColor = new Color(0.96f, 0.65f, 0.14f, 1f);
        public float hotspotPulseSpeed = 3f;
    }
}
