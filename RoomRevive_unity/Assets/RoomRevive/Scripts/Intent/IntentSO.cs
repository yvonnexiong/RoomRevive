using UnityEngine;

namespace RoomRevive
{
    [CreateAssetMenu(fileName = "Intent_New", menuName = "RoomRevive/Intent")]
    public class IntentSO : ScriptableObject
    {
        public string id;
        public string displayName;
        public GameObject splatWorld;
    }
}
