using UnityEngine;

namespace RoomRevive
{
    public class BillboardPanel : MonoBehaviour
    {
        [SerializeField] private float distance = 1.8f;
        [SerializeField] private float verticalOffset = 0f;

        private Transform _cam;
        private bool _initialized;

        void Start()
        {
            var centerEye = GameObject.Find("CenterEyeAnchor");
            if (centerEye != null) _cam = centerEye.transform;
            if (_cam == null) _cam = Camera.main?.transform;

            PlaceInFrontOfUser();
            _initialized = true;
        }

        void LateUpdate()
        {
            if (!_initialized || _cam == null) return;

            // Follow camera position at fixed distance
            var forward = _cam.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            transform.position = _cam.position + forward * distance + Vector3.up * verticalOffset;

            // Face the camera
            transform.rotation = Quaternion.LookRotation(forward);
        }

        void PlaceInFrontOfUser()
        {
            if (_cam == null) return;
            var forward = _cam.forward;
            forward.y = 0f;
            forward.Normalize();
            transform.position = _cam.position + forward * distance + Vector3.up * verticalOffset;
            var dir = transform.position - _cam.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
