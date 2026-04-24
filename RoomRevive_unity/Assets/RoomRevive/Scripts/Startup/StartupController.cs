using UnityEngine;

namespace RoomRevive
{
    public class StartupController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject startUI;
        [SerializeField] private GameObject alignmentUI;

        [Header("Scene")]
        [SerializeField] private Transform splatPivot;
        [SerializeField] private GameObject alignmentSphere;

        [Header("References")]
        [SerializeField] private GameObject intentSelectorUI;

        private Transform _cam;
        private bool _aligned;
        private Vector3 _savedPivotPos;
        private float _savedPivotRotY;

        void OnEnable()
        {
            OVRManager.TrackingAcquired += OnTrackingAcquired;
        }

        void OnDisable()
        {
            OVRManager.TrackingAcquired -= OnTrackingAcquired;
        }

        void PlaceInFrontOfUser(Transform target, float dist = 1.8f)
        {
            if (_cam == null) return;
            var forward = _cam.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            target.position = _cam.position + forward * dist;
            target.rotation = Quaternion.LookRotation(forward);
        }

        void OnTrackingAcquired()
        {
            // Restore pivot to aligned position after any tracking loss/recenter
            if (!_aligned || splatPivot == null) return;
            splatPivot.position = _savedPivotPos;
            splatPivot.eulerAngles = new Vector3(0, _savedPivotRotY, 0);
        }

        void Awake()
        {
            // Hide everything except start screen
            if (splatPivot != null) splatPivot.gameObject.SetActive(false);
            if (alignmentUI != null) alignmentUI.SetActive(false);
            if (alignmentSphere != null) alignmentSphere.SetActive(false);
            if (intentSelectorUI != null) intentSelectorUI.SetActive(false);
        }

        void Start()
        {
            var centerEye = GameObject.Find("CenterEyeAnchor");
            _cam = centerEye != null ? centerEye.transform : Camera.main?.transform;
            OVRManager.boundary.SetVisible(false);
        }

        void Update()
        {
            // Keep boundary suppressed — OS can re-enable it each frame
            OVRManager.boundary.SetVisible(false);
        }

        // Called by Start button onClick
        public void OnStartPressed()
        {
            if (_cam == null) return;

            // Spawn splat pivot at user's feet, facing user's forward direction
            if (splatPivot != null)
            {
                var forward = _cam.forward;
                forward.y = 0f;
                forward.Normalize();

                var floorPos = _cam.position;
                floorPos.y = 0f;
                splatPivot.position = floorPos;
                splatPivot.rotation = Quaternion.LookRotation(forward);
                splatPivot.gameObject.SetActive(true);
            }

            // Place alignment sphere 1m above pivot origin so it's within reach
            if (alignmentSphere != null)
            {
                alignmentSphere.transform.position = (splatPivot != null ? splatPivot.position : Vector3.zero)
                    + Vector3.up * 1.5f;
                alignmentSphere.SetActive(true);
            }

            if (startUI != null) startUI.SetActive(false);
            if (alignmentUI != null) alignmentUI.SetActive(true);
        }

        // Called by "Confirm world is aligned" button onClick
        public void OnAlignConfirmed()
        {
            if (alignmentSphere != null) alignmentSphere.SetActive(false);
            if (alignmentUI != null) alignmentUI.SetActive(false);

            // Save aligned pivot transform so we can restore it after any tracking recenter
            if (splatPivot != null)
            {
                _savedPivotPos = splatPivot.position;
                _savedPivotRotY = splatPivot.eulerAngles.y;
                _aligned = true;
            }

            IntentManager.Instance.Initialize();
            if (intentSelectorUI != null)
            {
                PlaceInFrontOfUser(intentSelectorUI.transform);
                intentSelectorUI.SetActive(true);
            }
        }
    }
}
