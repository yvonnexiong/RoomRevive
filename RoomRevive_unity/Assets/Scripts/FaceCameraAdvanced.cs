using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class FaceCameraAdvanced : MonoBehaviour
{
    public enum UpdateMode
    {
        Update,
        LateUpdate
    }

    [Header("Camera Target")]
    [Tooltip("Camera the object should face. If empty, Camera.main will be used.")]
    public Camera targetCamera;

    [Tooltip("Automatically use Camera.main if Target Camera is empty.")]
    public bool useMainCameraIfEmpty = true;

    [Header("Facing")]
    [Tooltip("Invert the facing direction. Enable this if the object faces away from the camera.")]
    public bool invertDirection = false;

    [Tooltip("Mirror the object horizontally (flips the local X scale). Useful for billboards/text " +
             "that should read as a reflection.")]
    public bool mirror = false;

    [Header("Axis Locks")]
    [Tooltip("If true, the object's X rotation will stay locked to lockedEulerAngles.x.")]
    public bool lockXRotation = false;

    [Tooltip("If true, the object's Y rotation will stay locked to lockedEulerAngles.y.")]
    public bool lockYRotation = false;

    [Tooltip("If true, the object's Z rotation will stay locked to lockedEulerAngles.z.")]
    public bool lockZRotation = false;

    [Tooltip("Euler rotation values used for locked axes.")]
    public Vector3 lockedEulerAngles = Vector3.zero;

    [Header("Runtime")]
    public UpdateMode updateMode = UpdateMode.LateUpdate;

    [Tooltip("Also align in edit mode, not just Play Mode.")]
    public bool updateInEditMode = true;

    private void OnEnable()
    {
        FindCameraIfNeeded();
        FaceTargetCamera();
    }

    private void Update()
    {
        if (!Application.isPlaying && !updateInEditMode) return;
        if (updateMode != UpdateMode.Update) return;

        FaceTargetCamera();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying && !updateInEditMode) return;
        if (updateMode != UpdateMode.LateUpdate) return;

        FaceTargetCamera();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        FindCameraIfNeeded();

        if (updateInEditMode)
        {
            FaceTargetCamera();
        }
    }
#endif

    [ContextMenu("Face Camera Now")]
    public void FaceTargetCamera()
    {
        FindCameraIfNeeded();

        if (targetCamera == null)
        {
            return;
        }

        Vector3 direction = transform.position - targetCamera.transform.position;

        if (invertDirection)
        {
            direction = -direction;
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        Vector3 targetEuler = targetRotation.eulerAngles;

        if (lockXRotation)
        {
            targetEuler.x = lockedEulerAngles.x;
        }

        if (lockYRotation)
        {
            targetEuler.y = lockedEulerAngles.y;
        }

        if (lockZRotation)
        {
            targetEuler.z = lockedEulerAngles.z;
        }

        transform.rotation = Quaternion.Euler(targetEuler);

        ApplyMirror();
    }

    // Flips the object horizontally by setting the sign of the local X scale to match 'mirror'.
    private void ApplyMirror()
    {
        Vector3 scale = transform.localScale;
        float desiredX = Mathf.Abs(scale.x) * (mirror ? -1f : 1f);

        if (!Mathf.Approximately(scale.x, desiredX))
        {
            scale.x = desiredX;
            transform.localScale = scale;
        }
    }

    [ContextMenu("Use Current Rotation As Locked Rotation")]
    public void UseCurrentRotationAsLockedRotation()
    {
        lockedEulerAngles = transform.rotation.eulerAngles;
    }

    private void FindCameraIfNeeded()
    {
        if (targetCamera != null) return;
        if (!useMainCameraIfEmpty) return;

        targetCamera = Camera.main;
    }
}