using UnityEngine;
using UnityEngine.UI;

namespace RoomRevive
{
    // Slider (0 = before/passthrough, 1 = after/full splat) that moves GSCutout
    // between two local positions.
    public class BeforeAfterSlider : MonoBehaviour
    {
        [SerializeField] private Transform cutoutTransform;
        private Slider slider;

        [Header("Cutout Positions")]
        [SerializeField] private Transform beforeLocator;
        [SerializeField] private Transform afterLocator;

        [Header("Follow Settings")]
        [Tooltip("If enabled, this slider follows the user's head/camera.")]
        [SerializeField] private bool followUserHead = false;

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

        [Header("Toggle")]
        [SerializeField] private OVRInput.Button toggleButton = OVRInput.Button.Two;

        private CanvasGroup _canvasGroup;
        private bool _visible = true;
        private Transform _cam;

        private void Start()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            SetVisible(false);
            SetupCameraReference();
            SetupSlider();

            // Default = after/full splat
            ApplyCutoutPosition(1f);
        }

        private void Update()
        {
            if (OVRInput.GetDown(toggleButton))
                SetVisible(!_visible);
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
            if (slider == null)
                slider = GetComponentInChildren<Slider>(true);
            if (slider == null)
                slider = FindFirstObjectByType<Slider>();
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
            if (cutoutTransform == null || beforeLocator == null || afterLocator == null) return;

            cutoutTransform.position = Vector3.Lerp(beforeLocator.position, afterLocator.position, value);
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

        private void SetVisible(bool visible)
        {
            _visible = visible;
            if (_canvasGroup == null) return;
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }
    }
}