using UnityEngine;
using UnityEngine.EventSystems;

namespace RoomRevive.Onboarding
{
    // Thin event bridge placed on CardBody (the raycaster hit surface).
    // Forwards Unity pointer events to the parent OnboardingOptionCardView.
    //
    // XR migration path:
    //   Mouse / editor  → GraphicRaycaster fires these interfaces as-is.
    //   XR ray          → swap Canvas GraphicRaycaster for TrackedDeviceGraphicRaycaster;
    //                      this script requires no changes.
    //   XR poke         → XRPokeInteractor also fires IPointer* via TrackedDeviceGraphicRaycaster;
    //                      this script requires no changes.
    //   If you later need XR-specific hover distance feedback, add
    //   IXRHoverEnterHandler / IXRHoverExitHandler here alongside the existing interfaces.
    public class OnboardingCardInteractionProxy : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler,  IPointerUpHandler
    {
        [SerializeField] OnboardingOptionCardView _view;

        public void Init(OnboardingOptionCardView view) => _view = view;

        public void OnPointerEnter(PointerEventData e) => _view?.OnHoverEnter();
        public void OnPointerExit(PointerEventData e)  => _view?.OnHoverExit();
        public void OnPointerDown(PointerEventData e)  => _view?.OnPressDown();
        public void OnPointerUp(PointerEventData e)    => _view?.OnPressUp();
    }
}
