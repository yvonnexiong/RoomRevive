using UnityEngine;

namespace RoomRevive
{
    public class GazeHotspotDetector : MonoBehaviour
    {
        [SerializeField] private float dwellTime = 0.7f;
        [SerializeField] private LayerMask hotspotLayer;

        private Transform _cam;
        private HotspotInteractable _currentTarget;
        private float _dwellTimer;

        void Start()
        {
            var centerEye = GameObject.Find("CenterEyeAnchor");
            _cam = centerEye != null ? centerEye.transform : Camera.main?.transform;
        }

        void Update()
        {
            if (_cam == null) return;

            HotspotInteractable hit = null;

            if (Physics.Raycast(_cam.position, _cam.forward, out RaycastHit hitInfo, 10f, hotspotLayer))
                hit = hitInfo.collider.GetComponent<HotspotInteractable>();

            if (hit != _currentTarget)
            {
                if (_currentTarget != null) _currentTarget.OnGazeExit();
                _currentTarget = hit;
                _dwellTimer = 0f;
                if (_currentTarget != null) _currentTarget.OnGazeEnter();
            }

            if (_currentTarget != null)
            {
                _dwellTimer += Time.deltaTime;
                _currentTarget.OnGazeDwell(_dwellTimer / dwellTime);

                if (_dwellTimer >= dwellTime)
                {
                    Debug.Log($"[Gaze] Dwell complete on {_currentTarget.name}");
                    _currentTarget.OnGazeSelect();
                    _currentTarget = null;
                    _dwellTimer = 0f;
                }
            }
        }
    }
}
