using GaussianSplatting.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace RoomRevive
{
    // Slider (0 = before/passthrough, 1 = after/full splat) that controls the
    // normalized reveal value on GaussianArcCutoutManager.
    [ExecuteAlways]
    public class BeforeAfterSlider : MonoBehaviour
    {
        [Header("Cutout Reveal")]
        [Tooltip("Optional explicit reference. If empty, the active GaussianArcCutoutManager singleton is used.")]
        [SerializeField] private GaussianArcCutoutManager cutoutManager;

        [Tooltip("Current normalized value of the UI slider (0 = before, 1 = after).")]
        [SerializeField, Range(0f, 1f)] private float currentSliderValue = 1f;

        [Tooltip("If enabled, the slider's draggable handle is hidden and interaction disabled — the slider " +
                 "becomes a display-only bar driven by code (SetSliderValue / the cutout manager).")]
        [SerializeField] private bool hideHandle = true;

        [Tooltip("If enabled, the reveal value is pushed in OnValidate and (in edit mode) every Update, so the " +
                 "Scene view previews changes live. Disable to stop the editor from continuously syncing.")]
        [SerializeField] private bool updateInEditor = true;

        public float CurrentSliderValue => currentSliderValue;

        private Slider slider;

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
            if (!Application.isPlaying)
            {
                SyncEditorPreview();
                return;
            }

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            SetVisible(false);
            SetupCameraReference();
            SetupCutoutManager();
            SetupSlider();

            ApplyRevealValue(currentSliderValue);
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                if (updateInEditor)
                    SyncEditorPreview();
                return;
            }

            if (OVRInput.GetDown(toggleButton))
                SetVisible(!_visible);
        }

        private void OnValidate()
        {
            currentSliderValue = Mathf.Clamp01(currentSliderValue);
            ResolveSlider(false);

            if (slider != null)
            {
                ConfigureSliderRange();
                slider.SetValueWithoutNotify(currentSliderValue);
            }

            if (!updateInEditor) return;

            SetupCutoutManager();
            ApplyRevealValue(currentSliderValue);
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
            ResolveSlider(true);
            if (slider == null) return;

            ConfigureSliderRange();
            slider.SetValueWithoutNotify(currentSliderValue);

            slider.onValueChanged.RemoveListener(OnSliderChanged);
            slider.onValueChanged.AddListener(OnSliderChanged);
        }

        private void ResolveSlider(bool allowSceneFallback)
        {
            if (slider == null)
                slider = GetComponentInChildren<Slider>(true);
            if (slider == null && allowSceneFallback)
                slider = FindFirstObjectByType<Slider>();
        }

        private void ConfigureSliderRange()
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            ApplyHandleVisibility();
        }

        // When hideHandle is on, disable the handle GameObject, detach it from the slider, and turn off
        // interaction/transition so the slider reads as a display-only bar (no draggable knob).
        private void ApplyHandleVisibility()
        {
            if (slider == null) return;

            if (hideHandle)
            {
                if (slider.handleRect != null)
                {
                    slider.handleRect.gameObject.SetActive(false);
                    slider.handleRect = null;
                }
                slider.interactable = false;
                slider.transition = Selectable.Transition.None;
            }
            else
            {
                slider.interactable = true;
            }
        }

        private void SyncEditorPreview()
        {
            ResolveSlider(false);
            if (slider == null) return;

            ConfigureSliderRange();
            float value = Mathf.Clamp01(slider.value);
            slider.SetValueWithoutNotify(value);

            if (!Mathf.Approximately(currentSliderValue, value) || cutoutManager == null)
            {
                ApplyRevealValue(value);
            }
        }

        private void SetupCutoutManager()
        {
            if (cutoutManager == null)
            {
                cutoutManager = GaussianArcCutoutManager.GetOrFindInstance();
            }
        }

        private void OnSliderChanged(float value)
        {
            ApplyRevealValue(value);
        }

        /// <summary>
        /// Updates this component and its Unity UI Slider from the cutout manager
        /// without sending the value back to the manager.
        /// </summary>
        public void SetValueFromCutoutManager(float normalizedValue)
        {
            currentSliderValue = Mathf.Clamp01(normalizedValue);
            ResolveSlider(false);

            if (slider == null) return;

            ConfigureSliderRange();
            slider.SetValueWithoutNotify(currentSliderValue);
        }

        /// <summary>
        /// Updates the slider and applies the new normalized reveal value to the manager.
        /// </summary>
        public void SetSliderValue(float normalizedValue)
        {
            currentSliderValue = Mathf.Clamp01(normalizedValue);
            ResolveSlider(false);

            if (slider != null)
            {
                ConfigureSliderRange();
                slider.SetValueWithoutNotify(currentSliderValue);
            }

            ApplyRevealValue(currentSliderValue);
        }

        public bool UsesCutoutManager(GaussianArcCutoutManager manager)
        {
            return cutoutManager == null || cutoutManager == manager;
        }

        private void ApplyRevealValue(float value)
        {
            currentSliderValue = Mathf.Clamp01(value);

            if (cutoutManager == null)
            {
                SetupCutoutManager();
            }

            if (cutoutManager != null)
            {
                cutoutManager.SetReveal01(currentSliderValue);
            }
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
                ApplyRevealValue(1f);
            }
        }

        private void OnDestroy()
        {
            if (slider != null)
            {
                slider.onValueChanged.RemoveListener(OnSliderChanged);
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
