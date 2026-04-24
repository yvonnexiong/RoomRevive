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
            if (Keyboard.current.digit1Key.wasPressedThisFrame) IntentManager.Instance.SetIntent(calm);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) IntentManager.Instance.SetIntent(host);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) IntentManager.Instance.SetIntent(fast);
        }
    }
}
