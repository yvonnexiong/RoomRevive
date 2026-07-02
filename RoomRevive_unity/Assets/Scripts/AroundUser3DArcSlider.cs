using GaussianSplatting.Runtime;
using UnityEngine;

namespace RoomRevive
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class AroundUser3DArcSlider : MonoBehaviour
    {
        public enum CenterMode
        {
            ManualCenter,
            CutoutManagerDefaultFireFrom,
            MainCamera
        }

        [Header("References")]
        public GaussianArcCutoutManager cutoutManager;

        [Tooltip("Used when centerMode is ManualCenter.")]
        public Transform manualCenter;

        [Tooltip("The draggable handle object.")]
        public Transform handle;

        [Tooltip("Optional line renderer used to draw the slider arc.")]
        public LineRenderer trackLine;

        [Header("Center")]
        public CenterMode centerMode = CenterMode.CutoutManagerDefaultFireFrom;

        [Tooltip("Local position offset from the center transform.")]
        public Vector3 centerLocalOffset = Vector3.zero;

        [Tooltip("If enabled, the slider keeps following the center position.")]
        public bool followCenterPosition = true;

        [Tooltip("If enabled, the slider keeps following the center rotation.")]
        public bool followCenterRotation = true;

        [Header("Arc Shape")]
        [Min(0.01f)]
        public float radius = 0.8f;

        [Range(1f, 360f)]
        public float arcAngleDeg = 120f;

        [Tooltip("Rotates the whole slider arc around the user/defaultFireFrom.")]
        public float arcYawOffsetDeg = 0f;

        [Tooltip("Extra height offset in world units.")]
        public float worldHeightOffset = 0f;

        [Header("Value")]
        [Range(0f, 1f)]
        public float value01 = 0.5f;

        public bool invertValue = false;

        [Header("Manager Output")]
        public bool sendValueToCutoutManager = true;

        [Tooltip("If enabled, the manager is updated when the value changes in Edit Mode too.")]
        public bool sendValueInEditMode = true;

        [Header("Visuals")]
        public bool updateContinuously = true;

        [Range(4, 128)]
        public int trackSegments = 48;

        [Tooltip("If enabled, the handle points away from the center.")]
        public bool handleFacesAwayFromCenter = true;

        [Tooltip("Extra local rotation added to the handle after it is placed.")]
        public Vector3 handleExtraEulerOffset = Vector3.zero;

        [Tooltip("If enabled, a missing LineRenderer will be created automatically.")]
        public bool autoCreateTrackLine = true;

        [Tooltip("If enabled, a missing handle child will be created automatically.")]
        public bool autoCreateHandle = true;

        private bool isDragging;

        private void Reset()
        {
            TryAutoFindReferences();
            EnsureVisualObjects();
            ApplyVisuals();
            ApplyValueToManager();
        }

        private void OnEnable()
        {
            TryAutoFindReferences();
            EnsureVisualObjects();
            ApplyVisuals();
            ApplyValueToManager();
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0.01f, radius);
            arcAngleDeg = Mathf.Clamp(arcAngleDeg, 1f, 360f);
            value01 = Mathf.Clamp01(value01);
            trackSegments = Mathf.Clamp(trackSegments, 4, 128);

            TryAutoFindReferences();
            EnsureVisualObjects();
            ApplyVisuals();

            if (!Application.isPlaying && sendValueInEditMode)
            {
                ApplyValueToManager();
            }
        }

        private void Update()
        {
            if (!Application.isPlaying && !updateContinuously)
            {
                return;
            }

            if (updateContinuously)
            {
                ApplyVisuals();
            }
        }

        public void SetValue01(float newValue01)
        {
            value01 = Mathf.Clamp01(newValue01);
            ApplyVisuals();
            ApplyValueToManager();
        }

        public void SetValueFromWorldPoint(Vector3 worldPoint)
        {
            if (!TryGetCenterData(out Vector3 centerPosition, out Quaternion centerRotation, out Vector3 up, out Vector3 forward))
            {
                return;
            }

            Vector3 toPoint = worldPoint - centerPosition;
            toPoint = Vector3.ProjectOnPlane(toPoint, up);

            if (toPoint.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector3 direction = toPoint.normalized;

            Vector3 baseForward =
                Quaternion.AngleAxis(arcYawOffsetDeg, up) * forward;

            float signedAngle = Vector3.SignedAngle(baseForward, direction, up);

            float halfArc = arcAngleDeg * 0.5f;
            float clampedAngle = Mathf.Clamp(signedAngle, -halfArc, halfArc);

            float newValue = Mathf.InverseLerp(-halfArc, halfArc, clampedAngle);

            SetValue01(newValue);
        }

        public void BeginDragFromWorldPoint(Vector3 worldPoint)
        {
            isDragging = true;
            SetValueFromWorldPoint(worldPoint);
        }

        public void DragFromWorldPoint(Vector3 worldPoint)
        {
            if (!isDragging)
            {
                isDragging = true;
            }

            SetValueFromWorldPoint(worldPoint);
        }

        public void EndDrag()
        {
            isDragging = false;
        }

        public void BeginDragFromRay(Ray ray)
        {
            isDragging = true;

            if (TryGetPointOnSliderPlane(ray, out Vector3 worldPoint))
            {
                SetValueFromWorldPoint(worldPoint);
            }
        }

        public void DragFromRay(Ray ray)
        {
            if (!isDragging)
            {
                isDragging = true;
            }

            if (TryGetPointOnSliderPlane(ray, out Vector3 worldPoint))
            {
                SetValueFromWorldPoint(worldPoint);
            }
        }

        public void EndDragFromRay()
        {
            isDragging = false;
        }

        public bool TryGetPointOnSliderPlane(Ray ray, out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;

            if (!TryGetCenterData(out Vector3 centerPosition, out Quaternion centerRotation, out Vector3 up, out Vector3 forward))
            {
                return false;
            }

            Plane plane = new Plane(up, centerPosition);

            if (!plane.Raycast(ray, out float distance))
            {
                return false;
            }

            worldPoint = ray.GetPoint(distance);
            return true;
        }

        private void ApplyVisuals()
        {
            if (!TryGetCenterData(out Vector3 centerPosition, out Quaternion centerRotation, out Vector3 up, out Vector3 forward))
            {
                return;
            }

            Vector3 handlePosition = GetPositionOnArc(value01, centerPosition, up, forward);

            if (handle != null)
            {
                if (handleFacesAwayFromCenter)
                {
                    Vector3 awayDirection = Vector3.ProjectOnPlane(handlePosition - centerPosition, up);

                    if (awayDirection.sqrMagnitude <= 0.000001f)
                    {
                        awayDirection = forward;
                    }

                    Quaternion handleRotation =
                        Quaternion.LookRotation(awayDirection.normalized, up) *
                        Quaternion.Euler(handleExtraEulerOffset);

                    handle.SetPositionAndRotation(handlePosition, handleRotation);
                }
                else
                {
                    handle.position = handlePosition;
                }
            }

            UpdateTrackLine(centerPosition, up, forward);
        }

        private void ApplyValueToManager()
        {
            if (!sendValueToCutoutManager)
            {
                return;
            }

            if (cutoutManager == null)
            {
                cutoutManager = GaussianArcCutoutManager.GetOrFindInstance();
            }

            if (cutoutManager == null)
            {
                return;
            }

            float outputValue = invertValue ? 1f - value01 : value01;
            cutoutManager.SetReveal01(outputValue);
        }

        private Vector3 GetPositionOnArc(
            float normalizedValue,
            Vector3 centerPosition,
            Vector3 up,
            Vector3 forward
        )
        {
            float halfArc = arcAngleDeg * 0.5f;
            float angle = Mathf.Lerp(-halfArc, halfArc, Mathf.Clamp01(normalizedValue));

            Vector3 baseForward =
                Quaternion.AngleAxis(arcYawOffsetDeg, up) * forward;

            Vector3 direction =
                Quaternion.AngleAxis(angle, up) * baseForward;

            return centerPosition + direction.normalized * radius;
        }

        private void UpdateTrackLine(Vector3 centerPosition, Vector3 up, Vector3 forward)
        {
            if (trackLine == null)
            {
                return;
            }

            int pointCount = trackSegments + 1;
            trackLine.positionCount = pointCount;
            trackLine.useWorldSpace = true;

            for (int i = 0; i < pointCount; i++)
            {
                float t = i / (float)(pointCount - 1);
                Vector3 point = GetPositionOnArc(t, centerPosition, up, forward);
                trackLine.SetPosition(i, point);
            }
        }

        private bool TryGetCenterData(
            out Vector3 centerPosition,
            out Quaternion centerRotation,
            out Vector3 up,
            out Vector3 forward
        )
        {
            centerPosition = transform.position;
            centerRotation = transform.rotation;
            up = Vector3.up;
            forward = Vector3.forward;

            Transform centerTransform = GetCenterTransform();

            if (centerTransform == null)
            {
                return false;
            }

            centerRotation = followCenterRotation
                ? centerTransform.rotation
                : transform.rotation;

            centerPosition = followCenterPosition
                ? centerTransform.TransformPoint(centerLocalOffset)
                : transform.position;

            centerPosition += Vector3.up * worldHeightOffset;

            up = Vector3.up;

            forward = Vector3.ProjectOnPlane(centerRotation * Vector3.forward, up);

            if (forward.sqrMagnitude <= 0.000001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();

            return true;
        }

        private Transform GetCenterTransform()
        {
            if (centerMode == CenterMode.ManualCenter)
            {
                return manualCenter;
            }

            if (centerMode == CenterMode.CutoutManagerDefaultFireFrom)
            {
                if (cutoutManager == null)
                {
                    cutoutManager = GaussianArcCutoutManager.GetOrFindInstance();
                }

                if (cutoutManager != null && cutoutManager.defaultFireFrom != null)
                {
                    return cutoutManager.defaultFireFrom;
                }
            }

            if (centerMode == CenterMode.MainCamera)
            {
                Camera mainCamera = Camera.main;

                if (mainCamera != null)
                {
                    return mainCamera.transform;
                }
            }

            if (manualCenter != null)
            {
                return manualCenter;
            }

            Camera fallbackCamera = Camera.main;

            if (fallbackCamera != null)
            {
                return fallbackCamera.transform;
            }

            return null;
        }

        private void TryAutoFindReferences()
        {
            if (cutoutManager == null)
            {
                cutoutManager = GaussianArcCutoutManager.GetOrFindInstance();
            }
        }

        private void EnsureVisualObjects()
        {
            if (autoCreateTrackLine && trackLine == null)
            {
                GameObject existingTrack = transform.Find("TrackLine") != null
                    ? transform.Find("TrackLine").gameObject
                    : null;

                if (existingTrack == null)
                {
                    existingTrack = new GameObject("TrackLine");
                    existingTrack.transform.SetParent(transform, false);
                }

                trackLine = existingTrack.GetComponent<LineRenderer>();

                if (trackLine == null)
                {
                    trackLine = existingTrack.AddComponent<LineRenderer>();
                }

                trackLine.positionCount = trackSegments + 1;
                trackLine.useWorldSpace = true;
                trackLine.widthMultiplier = 0.015f;
            }

            if (autoCreateHandle && handle == null)
            {
                GameObject existingHandle = transform.Find("Handle") != null
                    ? transform.Find("Handle").gameObject
                    : null;

                if (existingHandle == null)
                {
                    existingHandle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    existingHandle.name = "Handle";
                    existingHandle.transform.SetParent(transform, false);
                    existingHandle.transform.localScale = Vector3.one * 0.08f;
                }

                handle = existingHandle.transform;
            }

            if (handle != null)
            {
                AroundUser3DArcSliderHandle handleScript =
                    handle.GetComponent<AroundUser3DArcSliderHandle>();

                if (handleScript == null)
                {
                    handleScript = handle.gameObject.AddComponent<AroundUser3DArcSliderHandle>();
                }

                handleScript.slider = this;

                Collider handleCollider = handle.GetComponent<Collider>();

                if (handleCollider == null)
                {
                    SphereCollider sphereCollider = handle.gameObject.AddComponent<SphereCollider>();
                    sphereCollider.radius = 0.08f;
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!TryGetCenterData(out Vector3 centerPosition, out Quaternion centerRotation, out Vector3 up, out Vector3 forward))
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(centerPosition, 0.04f);

            Vector3 previousPoint = GetPositionOnArc(0f, centerPosition, up, forward);

            for (int i = 1; i <= trackSegments; i++)
            {
                float t = i / (float)trackSegments;
                Vector3 point = GetPositionOnArc(t, centerPosition, up, forward);
                Gizmos.DrawLine(previousPoint, point);
                previousPoint = point;
            }

            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(GetPositionOnArc(value01, centerPosition, up, forward), 0.05f);
        }
#endif
    }
}