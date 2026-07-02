using UnityEngine;

namespace RoomRevive
{
    /// <summary>
    /// Positions a referenced UI relative to the player's head/camera, using a single Vector3 offset
    /// (x = right, y = up, z = forward). Mirrors the BeforeAfterSlider's head-follow so a panel keeps a
    /// fixed placement in front of the user. Assign the UI in the inspector and tune the offset.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class HeadFollowOffset : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The UI (or any transform) to place. Defaults to this object if empty.")]
        [SerializeField] private Transform targetUI;

        [Header("Anchor (priority: Track Line → Anchor → head)")]
        [Tooltip("Optional. If set, the UI is centered on the MIDDLE of this line (stable — does not move " +
                 "with the slider handle). Offset below is applied in the player's local space.")]
        [SerializeField] private LineRenderer trackLine;

        [Tooltip("Optional. If set (and no Track Line), the UI is placed at this transform plus the offset, " +
                 "in the anchor's local space. Leave empty to place relative to the player's head.")]
        [SerializeField] private Transform anchor;

        [Header("Offset")]
        [Tooltip("x = right, y = up, z = forward. Relative to the anchor when one is set, otherwise the player.")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 0f, 0f);

        [Header("Rotation")]
        [Tooltip("Also face the same direction as the player.")]
        [SerializeField] private bool followRotation = true;

        [Header("Player Feet")]
        [Tooltip("If on (and no Track Line / Anchor), place at the player's FEET — the head's horizontal " +
                 "position at floor height — so the UI sits on the ground under the player as they walk.")]
        [SerializeField] private bool followPlayerFeet = false;
        [Tooltip("Optional. Its Y is used as the floor height for feet mode (e.g. the camera-rig root). " +
                 "If empty, Floor Y below is used.")]
        [SerializeField] private Transform floorReference;
        [SerializeField] private float floorY = 0f;

        [Header("Camera")]
        [Tooltip("Manual head/camera reference. If empty, auto-find CenterEyeAnchor then Camera.main.")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private bool autoFindCamera = true;

        [Header("When")]
        [Tooltip("Also position in edit mode (preview). Otherwise it only follows in Play mode, like the slider.")]
        [SerializeField] private bool updateInEditMode = false;

        private Transform _cam;

        private void OnEnable() => SetupCameraReference();

        private void LateUpdate()
        {
            if (!Application.isPlaying && !updateInEditMode) return;

            Transform t = targetUI != null ? targetUI : transform;

            if (_cam == null) SetupCameraReference();

            if (trackLine != null && trackLine.positionCount > 0)
            {
                // Centered on the middle of the slider line (stable regardless of the handle position).
                // Offset is applied in the player's local space so it stays intuitive.
                Vector3 mid = LineMiddleWorld(trackLine);
                if (_cam != null)
                    mid += _cam.right * offset.x + _cam.up * offset.y + _cam.forward * offset.z;
                t.position = mid;
            }
            else if (anchor != null)
            {
                t.position = anchor.position
                    + anchor.right   * offset.x
                    + anchor.up      * offset.y
                    + anchor.forward * offset.z;
            }
            else if (followPlayerFeet && _cam != null)
            {
                // Feet = head's horizontal position at floor height. Use yaw-only axes so the ground UI
                // doesn't tilt when the player looks up/down.
                float y = floorReference != null ? floorReference.position.y : floorY;
                Vector3 feet = new Vector3(_cam.position.x, y, _cam.position.z);

                Vector3 fwd = _cam.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 1e-5f ? fwd.normalized : Vector3.forward;
                Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);   // yaw-right

                t.position = feet + right * offset.x + Vector3.up * offset.y + fwd * offset.z;
            }
            else
            {
                if (_cam == null) return;
                t.position = _cam.position
                    + _cam.right   * offset.x
                    + _cam.up      * offset.y
                    + _cam.forward * offset.z;
            }

            if (followRotation && _cam != null)
                t.rotation = Quaternion.LookRotation(_cam.forward, _cam.up);
        }

        // World-space middle point of a LineRenderer (the arc's apex — does not move with the handle).
        private static Vector3 LineMiddleWorld(LineRenderer line)
        {
            int n = line.positionCount;
            Vector3 p = line.GetPosition(n / 2);   // middle vertex = visual middle of the arc
            return line.useWorldSpace ? p : line.transform.TransformPoint(p);
        }

        private void SetupCameraReference()
        {
            if (cameraTransform != null) { _cam = cameraTransform; return; }
            if (!autoFindCamera) { _cam = null; return; }

            GameObject centerEye = GameObject.Find("CenterEyeAnchor");
            _cam = centerEye != null ? centerEye.transform
                 : (Camera.main != null ? Camera.main.transform : null);
        }
    }
}
