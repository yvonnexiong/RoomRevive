using System;
using UnityEngine;

namespace RoomRevive
{
    /// <summary>
    /// Attach to the AlignmentUI GameObject.
    /// Fires OnConfirmed when the confirm button is pressed or Enter is hit.
    /// GameManager listens to this to advance to the browsing phase.
    /// </summary>
    public class AlignmentUIController : MonoBehaviour
    {
        public static event Action OnConfirmed;

        void Update()
        {
            if (!Application.isPlaying) return;
            if (KeyInput.GetKeyDown(KeyCode.Return) || KeyInput.GetKeyDown(KeyCode.KeypadEnter))
                Confirm();
        }

        /// <summary>Wire this to the confirm button OnClick in the prefab.</summary>
        public void Confirm()
        {
            OnConfirmed?.Invoke();
        }
    }
}
