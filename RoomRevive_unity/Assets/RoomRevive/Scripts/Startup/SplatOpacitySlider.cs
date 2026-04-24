using UnityEngine;
using UnityEngine.UI;
using GaussianSplatting.Runtime;

namespace RoomRevive
{
    // World-space slider that always follows the user's head and controls splat opacity.
    public class SplatOpacitySlider : MonoBehaviour
    {
        [SerializeField] private GaussianSplatRenderer splatRenderer;
        [SerializeField] private Slider slider;

        [Header("Follow Settings")]
        [SerializeField] private float distance = 1.5f;
        [SerializeField] private float rightOffset = 0.4f;
        [SerializeField] private float verticalOffset = -0.3f;

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
        }

        void LateUpdate()
        {
            if (_cam == null) return;
            transform.position = _cam.position
                + _cam.forward * distance
                + _cam.right * rightOffset
                + _cam.up * verticalOffset;
            transform.rotation = Quaternion.LookRotation(_cam.forward);
        }

        void OnSliderChanged(float value)
        {
            if (splatRenderer != null)
                splatRenderer.m_OpacityScale = value;
        }
    }
}
