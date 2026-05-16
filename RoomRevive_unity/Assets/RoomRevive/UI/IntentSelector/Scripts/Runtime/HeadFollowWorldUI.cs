using UnityEngine;

namespace RoomRevive.IntentSelector
{
    /// <summary>
    /// Keeps a world-space UI in front of the HMD camera at a fixed offset.
    /// </summary>
    [DisallowMultipleComponent]
    public class HeadFollowWorldUI : MonoBehaviour
    {
        [Header("Behavior")]
        public bool followInPlayMode = true;
        public string cameraObjectName = "CenterEyeAnchor";

        [Header("Placement")]
        public float distance = 1.4f;
        public float rightOffset = 0f;
        public float upOffset = -0.1f;
        public bool faceCameraForward = true;

        Transform _cam;

        void Start()
        {
            if (!Application.isPlaying) return;
            FindCamera();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || !followInPlayMode) return;
            if (_cam == null) FindCamera();
            if (_cam == null) return;

            transform.position =
                _cam.position +
                _cam.forward * distance +
                _cam.right * rightOffset +
                _cam.up * upOffset;

            if (faceCameraForward)
                transform.rotation = Quaternion.LookRotation(_cam.forward);
        }

        void FindCamera()
        {
            if (!string.IsNullOrEmpty(cameraObjectName))
            {
                GameObject eye = GameObject.Find(cameraObjectName);
                if (eye != null) { _cam = eye.transform; return; }
            }

            if (Camera.main != null) _cam = Camera.main.transform;
        }
    }
}
