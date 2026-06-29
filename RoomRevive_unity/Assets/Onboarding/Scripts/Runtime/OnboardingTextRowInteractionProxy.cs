using UnityEngine;
using UnityEngine.EventSystems;

namespace RoomRevive.Onboarding
{
    // Sits on RowBody (the raycaster hit surface); forwards IPointer* to OnboardingTextRowView.
    // XR migration: swap GraphicRaycaster for TrackedDeviceGraphicRaycaster — no changes here.
    public class OnboardingTextRowInteractionProxy : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler,  IPointerUpHandler
    {
        OnboardingTextRowView _view;
        public void Init(OnboardingTextRowView view) => _view = view;

        public void OnPointerEnter(PointerEventData _) => _view?.OnHoverEnter();
        public void OnPointerExit (PointerEventData _) => _view?.OnHoverExit();
        public void OnPointerDown (PointerEventData _) => _view?.OnPressDown();
        public void OnPointerUp   (PointerEventData _) => _view?.OnPressUp();
    }
}
