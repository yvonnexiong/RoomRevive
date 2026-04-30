using UnityEngine;

namespace RoomRevive
{
    // Attach to the grabbable sphere GameObject. No event wiring needed.
    // The sphere only moves when grabbed, so syncing every frame is safe.
    // Drives SplatPivot: XZ position + Y rotation only. Y is locked to floor level.
    public class AlignmentSphere : MonoBehaviour
    {
        [SerializeField] private Transform splatPivot;

        private float _floorY;
        private Rigidbody _rb;

        void OnEnable()
        {
            _rb = GetComponent<Rigidbody>();
            _floorY = transform.position.y;
        }

        void LateUpdate()
        {
            // Kill any physics velocity so the sphere can't drift or get thrown
            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            // Lock sphere Y to floor level at all times
            var pos = transform.position;
            pos.y = _floorY;
            transform.position = pos;

            if (splatPivot == null) return;

            // Sync pivot to sphere — sphere only moves when grabbed, so this is safe every frame
            var pivotPos = splatPivot.position;
            pivotPos.x = transform.position.x;
            pivotPos.z = transform.position.z;
            splatPivot.position = pivotPos;

            // Y rotation only
            var pivotEuler = splatPivot.eulerAngles;
            pivotEuler.y = transform.eulerAngles.y;
            splatPivot.eulerAngles = pivotEuler;
        }
    }
}
