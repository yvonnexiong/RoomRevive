using UnityEngine;

namespace RoomRevive
{
    public class FaceCamera : MonoBehaviour
    {
        private Transform _cam;

        void Start()
        {
            var eye = GameObject.Find("CenterEyeAnchor");
            _cam = eye != null ? eye.transform : Camera.main?.transform;
        }

        void LateUpdate()
        {
            if (_cam == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - _cam.position);
        }
    }
}
