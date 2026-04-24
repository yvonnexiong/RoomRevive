using UnityEngine;
using UnityEngine.UI;

namespace RoomRevive
{
    // Slider (0 = before/passthrough, 1 = after/full splat) that moves GSCutout
    // between two local positions. Follows user's head like the opacity slider.
    public class BeforeAfterSlider : MonoBehaviour
    {
        [SerializeField] private Transform cutoutTransform;
        [SerializeField] private Slider slider;

        [Header("Cutout Positions (local space)")]
        [SerializeField] private Vector3 beforePosition = new Vector3(-0.32f, 1.45f, -2.88f);
        [SerializeField] private Vector3 afterPosition  = new Vector3(-4.3f,  1.45f, -5.65f);

        [Header("Follow Settings")]
        [SerializeField] private float distance      = 1.5f;
        [SerializeField] private float rightOffset   = 0.4f;
        [SerializeField] private float verticalOffset = -0.1f;

        private Transform _cam;

        void Start()
        {
            var centerEye = GameObject.Find("CenterEyeAnchor");
            _cam = centerEye != null ? centerEye.transform : Camera.main?.transform;

            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.value = 1f;
                slider.onValueChanged.AddListener(OnSliderChanged);
            }

            // Default = after (full splat)
            if (cutoutTransform != null)
                cutoutTransform.localPosition = afterPosition;
        }

        void LateUpdate()
        {
            if (_cam == null) return;
            transform.position = _cam.position
                + _cam.forward * distance
                + _cam.right  * rightOffset
                + _cam.up     * verticalOffset;
            transform.rotation = Quaternion.LookRotation(_cam.forward);
        }

        void OnSliderChanged(float value)
        {
            if (cutoutTransform == null) return;
            cutoutTransform.localPosition = Vector3.Lerp(beforePosition, afterPosition, value);
        }

        // Call this on intent switch to reset to full splat view
        public void ResetToAfter()
        {
            if (slider != null) slider.value = 1f;
        }
    }
}
