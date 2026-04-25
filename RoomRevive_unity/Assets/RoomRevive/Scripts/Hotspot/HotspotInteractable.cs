using UnityEngine;
using Oculus.Interaction;

namespace RoomRevive
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(RayInteractable))]
    public class HotspotInteractable : MonoBehaviour
    {
        [SerializeField] private HotspotSO _data;
        [SerializeField] private float _hoverScale = 1.3f;

        public static event System.Action<ProductSO> OnAnySelected;

        private RayInteractable _interactable;
        private Vector3 _baseScale;

        void Awake()
        {
            _interactable = GetComponent<RayInteractable>();
            _baseScale = transform.localScale;
        }

        void OnEnable()
        {
            _interactable.WhenSelectingInteractorAdded.Action += HandleSelected;
            _interactable.WhenInteractorAdded.Action          += HandleHoverEnter;
            _interactable.WhenInteractorRemoved.Action        += HandleHoverExit;
        }

        void OnDisable()
        {
            _interactable.WhenSelectingInteractorAdded.Action -= HandleSelected;
            _interactable.WhenInteractorAdded.Action          -= HandleHoverEnter;
            _interactable.WhenInteractorRemoved.Action        -= HandleHoverExit;
        }

        void HandleSelected(RayInteractor _)
        {
            if (_data?.linkedProduct != null)
                OnAnySelected?.Invoke(_data.linkedProduct);
        }

        void HandleHoverEnter(RayInteractor _) => transform.localScale = _baseScale * _hoverScale;
        void HandleHoverExit(RayInteractor _)  => transform.localScale = _baseScale;
    }
}
