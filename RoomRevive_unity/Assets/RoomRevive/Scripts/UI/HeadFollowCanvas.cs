using UnityEngine;

namespace RoomRevive
{
    public class HeadFollowCanvas : MonoBehaviour
    {
        [SerializeField] private float distance = 1.5f;
        [SerializeField] private float verticalOffset = -0.1f;

        private Transform _cam;

        void Start()
        {
            var centerEye = GameObject.Find("CenterEyeAnchor");
            _cam = centerEye != null ? centerEye.transform : Camera.main?.transform;
        }

        void LateUpdate()
        {
            if (_cam == null) return;
            transform.position = _cam.position
                + _cam.forward * distance
                + _cam.up * verticalOffset;
            transform.rotation = Quaternion.LookRotation(_cam.forward);
        }
    }
}
