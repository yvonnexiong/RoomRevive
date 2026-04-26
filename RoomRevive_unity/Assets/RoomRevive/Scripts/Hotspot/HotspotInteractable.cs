using UnityEngine;

namespace RoomRevive
{
    [RequireComponent(typeof(Collider))]
    public class HotspotInteractable : MonoBehaviour
    {
        [SerializeField] private HotspotSO _data;

        public static event System.Action<ProductSO> OnAnySelected;

        private Vector3 _baseScale;

        void Awake()
        {
            _baseScale = transform.localScale;
        }

        public void OnGazeEnter()
        {
            transform.localScale = _baseScale * 1.3f;
        }

        public void OnGazeExit()
        {
            transform.localScale = _baseScale;
        }

        public void OnGazeDwell(float t)
        {
            // t: 0-1, could drive a fill indicator here
        }

        public void OnGazeSelect()
        {
            transform.localScale = _baseScale;
            Debug.Log($"[Hotspot] OnGazeSelect — data={_data}, product={_data?.linkedProduct}, listeners={OnAnySelected?.GetInvocationList()?.Length ?? 0}");
            if (_data?.linkedProduct != null)
                OnAnySelected?.Invoke(_data.linkedProduct);
        }
    }
}
