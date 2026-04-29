using UnityEngine;
using UnityEngine.UI;

namespace RoomRevive
{
    // Slider (0 = before/passthrough, 1 = after/full splat) that moves GSCutout
    // between two local positions.
    public class BeforeAfterSlider : MonoBehaviour
    {
        [SerializeField] private Transform cutoutTransform;
        [SerializeField] private Slider slider;

        [Header("Cutout Positions (local space)")]
        [SerializeField] private Vector3 beforePosition = new Vector3(-0.32f, 1.45f, -2.88f);
        [SerializeField] private Vector3 afterPosition = new Vector3(-4.3f, 1.45f, -5.65f);

        [Header("Follow Settings")]
        [Tooltip("If enabled, this slider follows the user's head/camera.")]
        [SerializeField] private bool followUserHead = true;

        [Tooltip("If enabled, the script tries to find CenterEyeAnchor first, then falls back to Camera.main.")]
        [SerializeField] private bool autoFindCamera = true;

        [Tooltip("Optional manual camera/head reference. If empty, the script can auto-find CenterEyeAnchor or Camera.main.")]
        [SerializeField] private Transform cameraTransform;

        [SerializeField] private float distance = 1.5f;
        [SerializeField] private float rightOffset = 0.4f;
        [SerializeField] private float verticalOffset = -0.35f;

        [Header("Rotation")]
        [Tooltip("If enabled, the slider also rotates to face the same direction as the camera.")]
        [SerializeField] private bool followRotation = true;

        private Transform _cam;

        private void Start()
        {
            SetupCameraReference();
            SetupSlider();

            // Default = after/full splat
            ApplyCutoutPosition(1f);
        }

        private void LateUpdate()
        {
            if (!followUserHead) return;

            if (_cam == null)
            {
                SetupCameraReference();
            }

            if (_cam == null) return;

            transform.position =
                _cam.position +
                _cam.forward * distance +
                _cam.right * rightOffset +
                _cam.up * verticalOffset;

            if (followRotation)
            {
                transform.rotation = Quaternion.LookRotation(_cam.forward, _cam.up);
            }
        }

        private void SetupCameraReference()
        {
            if (cameraTransform != null)
            {
                _cam = cameraTransform;
                return;
            }

            if (!autoFindCamera)
            {
                _cam = null;
                return;
            }

            GameObject centerEye = GameObject.Find("CenterEyeAnchor");
            _cam = centerEye != null ? centerEye.transform : Camera.main != null ? Camera.main.transform : null;
        }

        private void SetupSlider()
        {
            if (slider == null) return;

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            slider.onValueChanged.RemoveListener(OnSliderChanged);
            slider.onValueChanged.AddListener(OnSliderChanged);
        }

        private void OnSliderChanged(float value)
        {
            ApplyCutoutPosition(value);
        }

        private void ApplyCutoutPosition(float value)
        {
            if (cutoutTransform == null) return;

            cutoutTransform.localPosition = Vector3.Lerp(beforePosition, afterPosition, value);
        }

        // Call this on intent switch to reset to full splat view
        public void ResetToAfter()
        {
            if (slider != null)
            {
                slider.value = 1f;
            }
            else
            {
                ApplyCutoutPosition(1f);
            }
        }

        public void SetFollowUserHead(bool shouldFollow)
        {
            followUserHead = shouldFollow;
        }

        public void EnableFollow()
        {
            followUserHead = true;
        }

        public void DisableFollow()
        {
            followUserHead = false;
        }
    }
}