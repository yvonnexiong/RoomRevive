using UnityEngine;
using UnityEngine.EventSystems;

namespace RoomRevive.IntentSelector
{
    /// <summary>
    /// Forwards Unity pointer events (Meta ray + mouse) to the parent IntentCardView.
    /// Holds no global selection logic.
    /// </summary>
    [DisallowMultipleComponent]
    public class IntentCardPointerProxy : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler
    {
        public IntentCardView card;

        void Reset()
        {
            if (card == null) card = GetComponent<IntentCardView>();
            if (card == null) card = GetComponentInParent<IntentCardView>();
        }

        public void OnPointerEnter(PointerEventData eventData) => card?.NotifyPointerEnter();
        public void OnPointerExit(PointerEventData eventData) => card?.NotifyPointerExit();
        public void OnPointerDown(PointerEventData eventData) => card?.NotifyPointerDown();
        public void OnPointerUp(PointerEventData eventData) => card?.NotifyPointerUp();
        public void OnPointerClick(PointerEventData eventData) => card?.NotifyPointerClick();
    }
}
