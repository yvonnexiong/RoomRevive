using UnityEngine;
using Oculus.Interaction;

namespace RoomRevive
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(RayInteractable))]
    public class HotspotInteractable : MonoBehaviour
    {
        [SerializeField] private HotspotSO _data;

        public static event System.Action<ProductSO> OnAnySelected;

        private RayInteractable _interactable;

        void Awake()
        {
            _interactable = GetComponent<RayInteractable>();
        }

        void OnEnable()
        {
            _interactable.WhenSelectingInteractorAdded.Action += HandleSelected;
        }

        void OnDisable()
        {
            _interactable.WhenSelectingInteractorAdded.Action -= HandleSelected;
        }

        void HandleSelected(RayInteractor _)
        {
            if (_data?.linkedProduct != null)
                OnAnySelected?.Invoke(_data.linkedProduct);
        }
    }
}
