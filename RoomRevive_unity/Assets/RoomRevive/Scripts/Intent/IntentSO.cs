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
    }
}
