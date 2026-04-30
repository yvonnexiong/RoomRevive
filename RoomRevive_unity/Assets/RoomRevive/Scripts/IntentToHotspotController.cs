using UnityEngine;

namespace RoomRevive
{
    public class IntentToHotspotController : MonoBehaviour
    {
        [SerializeField] private GameObject intentMenuUI;
        [SerializeField] private GameObject hotspots;

        void Update()
        {
            if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
            {
                if (intentMenuUI != null) intentMenuUI.SetActive(false);
                if (hotspots != null)    hotspots.SetActive(true);
            }
        }
    }
}
