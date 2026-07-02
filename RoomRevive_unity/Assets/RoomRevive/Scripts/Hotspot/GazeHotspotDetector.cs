using UnityEngine;

namespace RoomRevive
{
    public class GazeHotspotDetector : MonoBehaviour
    {
        [SerializeField] private float dwellTime = 0.7f;
        [SerializeField] private LayerMask hotspotLayer;

        [Header("Proximity Scaling")]
        [SerializeField] private float proximityOuterRadius = 1.0f; // dot starts appearing
        [SerializeField] private float proximityInnerRadius = 0.5f; // dot fully visible (matches collider radius)

        private Transform _cam;
        private HotspotInteractable _currentTarget;
        private float _dwellTimer;
        private HotspotInteractable[] _allHotspots;

        void Start()
        {
            var centerEye = GameObject.Find("CenterEyeAnchor");
            _cam = centerEye != null ? centerEye.transform : Camera.main?.transform;
            _allHotspots = FindObjectsOfType<HotspotInteractable>(true);
        }

        void Update()
        {
            if (_cam == null) return;

            // --- Gaze raycast and dwell ---
            HotspotInteractable hit = null;
            if (Physics.Raycast(_cam.position, _cam.forward, out RaycastHit hitInfo, 10f, hotspotLayer))
                hit = hitInfo.collider.GetComponent<HotspotInteractable>();

            // A visibility controller can disable a hotspot between frames. Treat
            // inactive or disabled components as no hit, even when still cached.
            if (hit != null && !hit.isActiveAndEnabled)
                hit = null;

            if (hit != _currentTarget)
            {
                if (_currentTarget != null && _currentTarget.isActiveAndEnabled)
                    _currentTarget.OnGazeExit();

                _currentTarget = hit;
                _dwellTimer = 0f;
                if (_currentTarget != null && _currentTarget.isActiveAndEnabled)
                    _currentTarget.OnGazeEnter();
            }

            if (_currentTarget != null && !_currentTarget.isActiveAndEnabled)
            {
                _currentTarget = null;
                _dwellTimer = 0f;
            }

            if (_currentTarget != null)
            {
                _dwellTimer += Time.deltaTime;
                _currentTarget.OnGazeDwell(_dwellTimer / dwellTime);

                if (_dwellTimer >= dwellTime)
                {
                    _currentTarget.OnGazeSelect();
                    _currentTarget = null;
                    _dwellTimer = 0f;
                }
            }

            // --- Proximity-based dot scaling for all hotspots ---
            foreach (var h in _allHotspots)
            {
                if (h == null || !h.isActiveAndEnabled) continue;

                if (h == _currentTarget)
                {
                    h.SetProximityScale(1f); // keep smoothed value synced while gazed
                    continue;
                }

                float d = RayToPointDistance(_cam.position, _cam.forward, h.transform.position);
                float t = Mathf.InverseLerp(proximityOuterRadius, proximityInnerRadius, d);
                h.SetProximityScale(t);
            }
        }

        private void OnDisable()
        {
            if (_currentTarget != null && _currentTarget.isActiveAndEnabled)
                _currentTarget.OnGazeExit();

            _currentTarget = null;
            _dwellTimer = 0f;
        }

        // Perpendicular distance from a ray to a world point
        static float RayToPointDistance(Vector3 origin, Vector3 dir, Vector3 point)
        {
            Vector3 v = point - origin;
            float proj = Vector3.Dot(v, dir);
            if (proj <= 0f) return v.magnitude; // point is behind camera
            return Vector3.Cross(dir, v).magnitude;
        }
    }
}
