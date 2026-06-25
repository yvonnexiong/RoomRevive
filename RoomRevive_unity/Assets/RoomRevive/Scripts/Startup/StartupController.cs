using GaussianSplatting.Runtime;
using UnityEngine;

namespace RoomRevive
{
    /// <summary>
    /// Handles the physical world setup — placing the splat pivot + alignment sphere, restoring the
    /// aligned pivot after tracking loss, and suppressing the boundary. UI visibility is owned by
    /// <see cref="GameManager"/> via its phase lists; the two buttons here just advance the phase.
    /// </summary>
    public class StartupController : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private Transform splatPivot;
        [SerializeField] private GameObject alignmentSphere;

        [Header("Splat")]
        [SerializeField] private GameObject splatRenderer1;
        [SerializeField] private GameObject splatRenderer2;
        [SerializeField] private GaussianSplatRenderer mainSplatRenderer;

        private Transform _cam;
        private bool _aligned;
        private Vector3 _savedPivotPos;
        private float _savedPivotRotY;

        void OnEnable()  => OVRManager.TrackingAcquired += OnTrackingAcquired;
        void OnDisable() => OVRManager.TrackingAcquired -= OnTrackingAcquired;

        void Start()
        {
            var centerEye = GameObject.Find("CenterEyeAnchor");
            _cam = centerEye != null ? centerEye.transform : Camera.main?.transform;
            OVRManager.boundary.SetVisible(false);
        }

        void Update()
        {
            // Keep boundary suppressed — OS can re-enable it each frame.
            OVRManager.boundary.SetVisible(false);
        }

        void OnTrackingAcquired()
        {
            // Restore pivot to aligned position after any tracking loss / recenter.
            if (!_aligned || splatPivot == null) return;
            splatPivot.position = _savedPivotPos;
            splatPivot.eulerAngles = new Vector3(0, _savedPivotRotY, 0);
        }

        // Called by the Start button onClick. Places the pivot + sphere, then advances to Alignment.
        public void OnStartPressed()
        {
            Debug.Log($"[StartupController] OnStartPressed — cam={(_cam != null)} gameManager={(GameManager.Instance != null)}", this);
            if (_cam == null) { Debug.LogWarning("[StartupController] _cam is null (CenterEyeAnchor not found) — aborting.", this); return; }

            splatRenderer1?.SetActive(false);
            splatRenderer2?.SetActive(false);

            // Spawn splat pivot in front of the user at floor level.
            if (splatPivot != null)
            {
                var forward = _cam.forward;
                forward.y = 0f;
                forward.Normalize();

                var floorPos = _cam.position + forward * 0.5f;
                floorPos.y = 0f;
                splatPivot.position = floorPos;
                splatPivot.rotation = Quaternion.LookRotation(forward);
            }

            // Position the alignment sphere above the pivot so it's grabbable (GameManager enables it).
            if (alignmentSphere != null)
                alignmentSphere.transform.position =
                    (splatPivot != null ? splatPivot.position : Vector3.zero) + Vector3.up * 1.2f;

            GameManager.Instance?.GoToPhase(GamePhase.Alignment);
        }

        // Called by the "Confirm world is aligned" button onClick. Saves alignment, then advances to Browsing.
        public void OnAlignConfirmed()
        {
            Debug.Log($"[StartupController] OnAlignConfirmed — gameManager={(GameManager.Instance != null)}", this);
            if (mainSplatRenderer != null)
            {
                mainSplatRenderer.m_Asset = null;
                mainSplatRenderer.UpdateRessources();
            }

            // Save aligned pivot transform so we can restore it after any tracking recenter.
            if (splatPivot != null)
            {
                _savedPivotPos = splatPivot.position;
                _savedPivotRotY = splatPivot.eulerAngles.y;
                _aligned = true;
            }

            IntentManager.Instance?.Initialize();

            GameManager.Instance?.GoToPhase(GamePhase.Browsing);
        }
    }
}
