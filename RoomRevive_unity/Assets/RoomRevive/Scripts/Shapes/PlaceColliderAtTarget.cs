using UnityEngine;

namespace RoomRevive
{
    /// <summary>
    /// Places this GameObject's collider at another transform's position (and optionally rotation).
    /// Useful for a gaze/look-at collider that must sit where some other object is (e.g. a UI panel)
    /// without being parented to it. Runs once on start, or every frame if <see cref="follow"/> is on.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class PlaceColliderAtTarget : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The transform whose position (and optionally rotation) this collider should sit at.")]
        [SerializeField] private Transform target;

        [Header("What to copy")]
        [SerializeField] private bool matchPosition = true;
        [SerializeField] private bool matchRotation = false;

        [Tooltip("World-space offset added after matching the target position.")]
        [SerializeField] private Vector3 worldOffset = Vector3.zero;

        [Header("When")]
        [Tooltip("If true, follow the target every frame. If false, snap once on enable/start.")]
        [SerializeField] private bool follow = false;

        [Tooltip("Also update in the editor (not just Play mode).")]
        [SerializeField] private bool updateInEditMode = true;

        private Collider _collider;

        void OnEnable()
        {
            _collider = GetComponent<Collider>();
            Apply();
        }

        void Update()
        {
            if (Application.isPlaying)
            {
                if (follow) Apply();          // Play mode: only track continuously when Follow is on.
            }
            else if (updateInEditMode)
            {
                Apply();                      // Edit mode: keep previewing so World Offset moves it live.
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (updateInEditMode) Apply();
        }
#endif

        /// <summary>Snap the collider to the target now.</summary>
        [ContextMenu("Place At Target Now")]
        public void Apply()
        {
            if (target == null) return;

            if (matchPosition)
                transform.position = target.position + worldOffset;

            if (matchRotation)
                transform.rotation = target.rotation;
        }
    }
}
