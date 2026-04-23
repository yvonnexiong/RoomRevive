using UnityEngine;
using UnityEngine.InputSystem;

namespace RoomRevive
{
    public class IntentDebugSwitcher : MonoBehaviour
    {
        [SerializeField] private IntentSO calm;
        [SerializeField] private IntentSO host;
        [SerializeField] private IntentSO fast;

        void Update()
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame) { Debug.Log("[IntentSwitcher] Key 1 → Calm & Unwind"); IntentManager.Instance.SetIntent(calm); }
            if (Keyboard.current.digit2Key.wasPressedThisFrame) { Debug.Log("[IntentSwitcher] Key 2 → Host & Gather"); IntentManager.Instance.SetIntent(host); }
            if (Keyboard.current.digit3Key.wasPressedThisFrame) { Debug.Log("[IntentSwitcher] Key 3 → Fast & Focused"); IntentManager.Instance.SetIntent(fast); }
        }
    }
}
