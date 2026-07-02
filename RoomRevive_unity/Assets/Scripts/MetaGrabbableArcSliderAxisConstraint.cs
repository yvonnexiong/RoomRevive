// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Reflection;
using GaussianSplatting.Runtime;
using UnityEngine;

namespace RoomRevive
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public class MetaGrabbableArcSliderAxisConstraint : MonoBehaviour
    {
        public enum CenterMode
        {
            ManualCenter,
            CutoutManagerDefaultFireFrom,
            MainCamera
        }

        public enum ConstraintMode
        {
            ArcAroundCenter,
            StraightLine
        }

        public enum ControlledGameObjectMode
        {
            DisableWhenInsideArc,
            EnableWhenInsideArc
        }

        [Header("References")]
        public GaussianArcCutoutManager cutoutManager;

        [Tooltip("Used when centerMode is ManualCenter.")]
        public Transform manualCenter;

        [Header("Center")]
        public CenterMode centerMode = CenterMode.CutoutManagerDefaultFireFrom;

        [Tooltip("The center point offset from the chosen center transform.")]
        public Vector3 centerLocalOffset = Vector3.zero;

        [Tooltip("Extra vertical world offset from the center.")]
        public float worldHeightOffset = 0f;

        [Header("Horizontal Arc Lock")]
        [Tooltip("If enabled, the arc is always flat in world-horizontal space. It ignores looking up/down and only uses yaw.")]
        public bool forceHorizontalPlane = true;

        [Tooltip("If enabled with forceHorizontalPlane, centerLocalOffset is applied without inheriting pitch/roll from the center transform.")]
        public bool useHorizontalLocalOffset = true;

        [Tooltip("World up axis used for the horizontal arc.")]
        public Vector3 horizontalUpAxis = Vector3.up;

        [Header("Constraint")]
        public ConstraintMode constraintMode = ConstraintMode.ArcAroundCenter;

        [Tooltip("Radius from the center when using ArcAroundCenter.")]
        [Min(0.01f)]
        public float radius = 0.8f;

        [Tooltip("Total arc angle. Example: 120 gives -60 to +60 degrees.")]
        [Range(1f, 360f)]
        public float arcAngleDeg = 120f;

        [Tooltip("Rotates the whole slider path around the center.")]
        public float yawOffsetDeg = 0f;

        [Tooltip("Length of the slider when using StraightLine.")]
        [Min(0.01f)]
        public float lineLength = 1.2f;

        [Tooltip("Local axis used for StraightLine mode.")]
        public Vector3 localLineAxis = Vector3.right;

        [Header("Value")]
        [Tooltip("This is the stored slider value from the ball position on the arc. The CutOutManager reads this value.")]
        [Range(0f, 1f)]
        public float value01 = 0.5f;

        public bool invertValue = false;

        [Header("Controlled GameObjects")]
        [Tooltip("If enabled, this script will enable/disable the dragged-in GameObjects based on whether their transform is inside the current arc area.")]
        public bool controlGameObjectsFromArc = false;

        [Tooltip("DisableWhenInsideArc = objects inside the current arc are hidden. EnableWhenInsideArc = objects inside the current arc are shown, and outside objects are hidden.")]
        public ControlledGameObjectMode controlledGameObjectMode = ControlledGameObjectMode.DisableWhenInsideArc;

        [Tooltip("GameObjects that should be enabled/disabled by the current arc area.")]
        public GameObject[] controlledGameObjects = new GameObject[0];

        [Tooltip("If enabled, the active arc area follows value01. If disabled, the full arcAngleDeg area is used.")]
        public bool controlledObjectsUseCurrentValue = true;

        [Tooltip("If enabled, the arc area uses the same inverted output value that can be sent to the CutOutManager.")]
        public bool controlledObjectsUseOutputValue = false;

        [Tooltip("Extra radius added to the arc area check. Useful if the objects are slightly outside the exact radius.")]
        [Min(0f)]
        public float controlledObjectsOuterRadiusPadding = 0f;

        [Tooltip("Optional inner radius. Objects closer than this to the center are treated as outside the arc area.")]
        [Min(0f)]
        public float controlledObjectsInnerRadius = 0f;

        [Tooltip("If enabled, the controlled GameObjects are updated in Edit Mode too.")]
        public bool updateControlledGameObjectsInEditMode = true;

        [Header("Grab Lock")]
        [Tooltip("If enabled, value01 only changes while the sphere is grabbed. When released, the sphere keeps the same value01 and follows the moving arc at that fixed value.")]
        public bool updateValueOnlyWhileGrabbed = true;

        [Tooltip("Current grab state. This can be set manually from Meta Interaction Toolkit events.")]
        public bool isGrabbed = false;

        [Tooltip("Tries to detect the Meta Interaction Toolkit Grabbable state automatically by reading common selected/grabbed properties through reflection.")]
        public bool autoDetectMetaGrabState = true;

        [Tooltip("When the grab ends, the script reads the final sphere position once, stores value01, then locks the sphere to that value.")]
        public bool readFinalValueOnRelease = true;

        [Header("Output")]
        [Tooltip("Usually keep this OFF when the CutOutManager reads value01 directly from this script.")]
        public bool sendValueToCutoutManager = false;

        [Tooltip("If true, the value is sent to the manager in edit mode too.")]
        public bool sendValueInEditMode = true;

        [Header("Hard Constraint")]
        [Tooltip("Keeps the ball locked onto the arc/axis in LateUpdate after Meta Grabbable has moved it.")]
        public bool constrainInLateUpdate = true;

        [Tooltip("Also constrains right before rendering. Useful in VR if another script moves the object after LateUpdate.")]
        public bool constrainBeforeRender = true;

        [Tooltip("If enabled, Rigidbody velocity is cleared after snapping, preventing physics drift/jitter.")]
        public bool clearRigidbodyVelocity = true;

        [Tooltip("If enabled, the ball snaps to the current value when this component enables.")]
        public bool snapToValueOnEnable = true;

        [Tooltip("If enabled, the ball updates immediately when inspector values change.")]
        public bool updateInOnValidate = true;

        [Header("Handle Rotation")]
        [Tooltip("If enabled, the ball will face away from the center in Arc mode, or along the line in Line mode.")]
        public bool controlHandleRotation = true;

        public Vector3 handleExtraEulerOffset = Vector3.zero;

        [Header("Arc Visual - Play Mode")]
        [Tooltip("Shows the arc/axis path using a LineRenderer in Play Mode.")]
        public bool showVisualInPlayMode = true;

        [Tooltip("Shows the arc/axis path using a LineRenderer in Edit Mode.")]
        public bool showVisualInEditMode = true;

        [Tooltip("Automatically creates a LineRenderer child if none is assigned.")]
        public bool autoCreateVisualLine = true;

        [Tooltip("Optional LineRenderer used to draw the arc/axis path.")]
        public LineRenderer visualLine;

        [Range(4, 128)]
        public int visualSegments = 64;

        [Min(0.001f)]
        public float visualWidth = 0.015f;

        public Color visualColor = new Color(0f, 0.85f, 1f, 1f);

        [Tooltip("Name of the auto-created child object used for the arc visual.")]
        public string visualChildName = "Arc Visual";

        [Header("Gizmos")]
        public bool drawGizmos = true;

        public Color gizmoArcColor = Color.cyan;
        public Color gizmoHandleColor = Color.white;

        private Rigidbody cachedRigidbody;
        private bool hasInitialized;
        private bool wasGrabbedLastFrame;
        private bool justReleasedThisFrame;
        private MonoBehaviour cachedMetaGrabbable;

        private void Reset()
        {
            TryAutoFindReferences();
            CacheRigidbody();
            EnsureVisualLine();
            UpdateValueFromCurrentPosition();
            SnapTransformToValue();
            UpdateVisualLine();
            ApplyValueToManager();
            ApplyControlledGameObjects();
        }

        private void OnEnable()
        {
            TryAutoFindReferences();
            CacheRigidbody();
            EnsureVisualLine();

            Application.onBeforeRender -= HandleBeforeRender;
            Application.onBeforeRender += HandleBeforeRender;

            if (!hasInitialized)
            {
                if (snapToValueOnEnable)
                {
                    SnapTransformToValue();
                }
                else
                {
                    UpdateValueFromCurrentPosition();
                    SnapTransformToValue();
                }

                ApplyValueToManager();
                ApplyControlledGameObjects();
                hasInitialized = true;
            }

            wasGrabbedLastFrame = isGrabbed;
            UpdateVisualLine();
            ApplyControlledGameObjects();
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= HandleBeforeRender;
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0.01f, radius);
            arcAngleDeg = Mathf.Clamp(arcAngleDeg, 1f, 360f);
            lineLength = Mathf.Max(0.01f, lineLength);
            value01 = Mathf.Clamp01(value01);
            visualSegments = Mathf.Clamp(visualSegments, 4, 128);
            visualWidth = Mathf.Max(0.001f, visualWidth);
            controlledObjectsOuterRadiusPadding = Mathf.Max(0f, controlledObjectsOuterRadiusPadding);
            controlledObjectsInnerRadius = Mathf.Max(0f, controlledObjectsInnerRadius);

            if (localLineAxis.sqrMagnitude <= 0.000001f)
            {
                localLineAxis = Vector3.right;
            }

            if (horizontalUpAxis.sqrMagnitude <= 0.000001f)
            {
                horizontalUpAxis = Vector3.up;
            }

            TryAutoFindReferences();
            CacheRigidbody();
            EnsureVisualLine();
            UpdateVisualLineVisibility();

            if (updateInOnValidate)
            {
                SnapTransformToValue();
                UpdateVisualLine();

                if (!Application.isPlaying && sendValueInEditMode)
                {
                    ApplyValueToManager();
                }

                ApplyControlledGameObjects();
            }
        }

        private void Update()
        {
            UpdateVisualLine();

            if (constrainInLateUpdate)
            {
                return;
            }

            ConstrainNow();
        }

        private void LateUpdate()
        {
            if (!constrainInLateUpdate)
            {
                return;
            }

            ConstrainNow();
        }

        private void HandleBeforeRender()
        {
            if (!constrainBeforeRender)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                return;
            }

            ConstrainNow();
        }

        public void NotifyGrabStarted()
        {
            isGrabbed = true;
            justReleasedThisFrame = false;
            wasGrabbedLastFrame = true;

            UpdateValueFromCurrentPosition();
            SnapTransformToValue();
            ClearPhysicsMotion();
            ApplyValueToManager();
            ApplyControlledGameObjects();
            UpdateVisualLine();
        }

        public void NotifyGrabEnded()
        {
            if (readFinalValueOnRelease)
            {
                UpdateValueFromCurrentPosition();
            }

            isGrabbed = false;
            justReleasedThisFrame = true;
            wasGrabbedLastFrame = false;

            SnapTransformToValue();
            ClearPhysicsMotion();
            ApplyValueToManager();
            ApplyControlledGameObjects();
            UpdateVisualLine();
        }

        public void SetGrabbed(bool grabbed)
        {
            if (grabbed)
            {
                NotifyGrabStarted();
            }
            else
            {
                NotifyGrabEnded();
            }
        }

        public void ConstrainNow()
        {
            RefreshGrabState();

            if (ShouldUpdateValueFromCurrentPosition())
            {
                UpdateValueFromCurrentPosition();
            }

            SnapTransformToValue();
            ClearPhysicsMotion();
            ApplyValueToManager();
            ApplyControlledGameObjects();
            UpdateVisualLine();

            wasGrabbedLastFrame = isGrabbed;
            justReleasedThisFrame = false;
        }

        public void SetValue01(float newValue01)
        {
            value01 = Mathf.Clamp01(newValue01);
            SnapTransformToValue();
            ClearPhysicsMotion();
            ApplyValueToManager();
            ApplyControlledGameObjects();
            UpdateVisualLine();
        }

        private bool ShouldUpdateValueFromCurrentPosition()
        {
            if (!Application.isPlaying)
            {
                return true;
            }

            if (!updateValueOnlyWhileGrabbed)
            {
                return true;
            }

            if (isGrabbed)
            {
                return true;
            }

            if (justReleasedThisFrame && readFinalValueOnRelease)
            {
                return true;
            }

            return false;
        }

        private void RefreshGrabState()
        {
            justReleasedThisFrame = false;

            bool previousGrabState = isGrabbed;

            if (autoDetectMetaGrabState &&
                TryAutoDetectMetaGrabState(out bool detectedGrabState))
            {
                isGrabbed = detectedGrabState;
            }

            if (wasGrabbedLastFrame && !isGrabbed)
            {
                justReleasedThisFrame = true;
            }

            if (previousGrabState && !isGrabbed)
            {
                justReleasedThisFrame = true;
            }
        }

        private void UpdateValueFromCurrentPosition()
        {
            if (!TryGetCenterData(
                    out Vector3 centerPosition,
                    out Quaternion centerRotation,
                    out Vector3 up,
                    out Vector3 forward,
                    out Vector3 right
                ))
            {
                return;
            }

            if (constraintMode == ConstraintMode.ArcAroundCenter)
            {
                UpdateArcValueFromCurrentPosition(centerPosition, up, forward);
            }
            else
            {
                UpdateLineValueFromCurrentPosition(centerPosition, centerRotation);
            }

            value01 = Mathf.Clamp01(value01);
        }

        private void UpdateArcValueFromCurrentPosition(
            Vector3 centerPosition,
            Vector3 up,
            Vector3 forward
        )
        {
            Vector3 toHandle = transform.position - centerPosition;

            // Important:
            // This removes all up/down movement from the grabbed position.
            // The value is calculated only from the horizontal direction around the center.
            Vector3 projected = Vector3.ProjectOnPlane(toHandle, up);

            if (projected.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector3 baseForward =
                Quaternion.AngleAxis(yawOffsetDeg, up) * forward;

            float signedAngle =
                Vector3.SignedAngle(baseForward, projected.normalized, up);

            float halfArc = arcAngleDeg * 0.5f;
            float clampedAngle = Mathf.Clamp(signedAngle, -halfArc, halfArc);

            value01 = Mathf.InverseLerp(-halfArc, halfArc, clampedAngle);
        }

        private void UpdateLineValueFromCurrentPosition(
            Vector3 centerPosition,
            Quaternion centerRotation
        )
        {
            Vector3 up = GetSafeUpAxis();

            Vector3 axisWorld =
                centerRotation * localLineAxis.normalized;

            if (forceHorizontalPlane)
            {
                axisWorld = Vector3.ProjectOnPlane(axisWorld, up);

                if (axisWorld.sqrMagnitude <= 0.000001f)
                {
                    axisWorld = Vector3.right;
                }
            }

            axisWorld =
                Quaternion.AngleAxis(yawOffsetDeg, up) * axisWorld.normalized;

            Vector3 toHandle = transform.position - centerPosition;

            if (forceHorizontalPlane)
            {
                toHandle = Vector3.ProjectOnPlane(toHandle, up);
            }

            float distanceOnAxis = Vector3.Dot(toHandle, axisWorld.normalized);
            float halfLength = lineLength * 0.5f;
            float clampedDistance = Mathf.Clamp(distanceOnAxis, -halfLength, halfLength);

            value01 = Mathf.InverseLerp(-halfLength, halfLength, clampedDistance);
        }

        private void SnapTransformToValue()
        {
            if (!TryGetCenterData(
                    out Vector3 centerPosition,
                    out Quaternion centerRotation,
                    out Vector3 up,
                    out Vector3 forward,
                    out Vector3 right
                ))
            {
                return;
            }

            if (constraintMode == ConstraintMode.ArcAroundCenter)
            {
                SnapToArc(centerPosition, up, forward);
            }
            else
            {
                SnapToLine(centerPosition, centerRotation);
            }
        }

        private void SnapToArc(
            Vector3 centerPosition,
            Vector3 up,
            Vector3 forward
        )
        {
            Vector3 targetPosition = GetArcPoint(value01, centerPosition, up, forward);

            if (controlHandleRotation)
            {
                Vector3 direction =
                    Vector3.ProjectOnPlane(targetPosition - centerPosition, up);

                if (direction.sqrMagnitude <= 0.000001f)
                {
                    direction = forward;
                }

                Quaternion targetRotation =
                    Quaternion.LookRotation(direction.normalized, up) *
                    Quaternion.Euler(handleExtraEulerOffset);

                transform.SetPositionAndRotation(targetPosition, targetRotation);
            }
            else
            {
                transform.position = targetPosition;
            }
        }

        private void SnapToLine(
            Vector3 centerPosition,
            Quaternion centerRotation
        )
        {
            Vector3 targetPosition = GetLinePoint(value01, centerPosition, centerRotation);

            if (controlHandleRotation)
            {
                Vector3 up = GetSafeUpAxis();

                Vector3 axisWorld =
                    centerRotation * localLineAxis.normalized;

                if (forceHorizontalPlane)
                {
                    axisWorld = Vector3.ProjectOnPlane(axisWorld, up);

                    if (axisWorld.sqrMagnitude <= 0.000001f)
                    {
                        axisWorld = Vector3.right;
                    }
                }

                axisWorld =
                    Quaternion.AngleAxis(yawOffsetDeg, up) * axisWorld.normalized;

                Quaternion targetRotation =
                    Quaternion.LookRotation(axisWorld.normalized, up) *
                    Quaternion.Euler(handleExtraEulerOffset);

                transform.SetPositionAndRotation(targetPosition, targetRotation);
            }
            else
            {
                transform.position = targetPosition;
            }
        }

        private Vector3 GetArcPoint(
            float normalizedValue,
            Vector3 centerPosition,
            Vector3 up,
            Vector3 forward
        )
        {
            float halfArc = arcAngleDeg * 0.5f;

            float angle =
                Mathf.Lerp(
                    -halfArc,
                    halfArc,
                    Mathf.Clamp01(normalizedValue)
                );

            Vector3 baseForward =
                Quaternion.AngleAxis(yawOffsetDeg, up) * forward;

            Vector3 direction =
                Quaternion.AngleAxis(angle, up) * baseForward;

            direction = Vector3.ProjectOnPlane(direction, up);

            if (direction.sqrMagnitude <= 0.000001f)
            {
                direction = forward;
            }

            // Important:
            // This point is always on the horizontal plane of centerPosition.
            // No up/down movement is added here.
            return centerPosition + direction.normalized * radius;
        }

        private Vector3 GetLinePoint(
            float normalizedValue,
            Vector3 centerPosition,
            Quaternion centerRotation
        )
        {
            Vector3 up = GetSafeUpAxis();

            Vector3 axisWorld =
                centerRotation * localLineAxis.normalized;

            if (forceHorizontalPlane)
            {
                axisWorld = Vector3.ProjectOnPlane(axisWorld, up);

                if (axisWorld.sqrMagnitude <= 0.000001f)
                {
                    axisWorld = Vector3.right;
                }
            }

            axisWorld =
                Quaternion.AngleAxis(yawOffsetDeg, up) * axisWorld.normalized;

            float halfLength = lineLength * 0.5f;

            float distance =
                Mathf.Lerp(
                    -halfLength,
                    halfLength,
                    Mathf.Clamp01(normalizedValue)
                );

            return centerPosition + axisWorld.normalized * distance;
        }

        private void ApplyControlledGameObjects()
        {
            if (!controlGameObjectsFromArc)
            {
                return;
            }

            if (!Application.isPlaying && !updateControlledGameObjectsInEditMode)
            {
                return;
            }

            if (controlledGameObjects == null || controlledGameObjects.Length == 0)
            {
                return;
            }

            if (constraintMode != ConstraintMode.ArcAroundCenter)
            {
                return;
            }

            if (!TryGetCenterData(
                    out Vector3 centerPosition,
                    out Quaternion centerRotation,
                    out Vector3 up,
                    out Vector3 forward,
                    out Vector3 right
                ))
            {
                return;
            }

            for (int i = 0; i < controlledGameObjects.Length; i++)
            {
                GameObject controlledObject = controlledGameObjects[i];

                if (controlledObject == null)
                {
                    continue;
                }

                bool isInsideArc = IsWorldPointInsideControlledArc(
                    controlledObject.transform.position,
                    centerPosition,
                    up,
                    forward
                );

                bool shouldBeActive =
                    controlledGameObjectMode == ControlledGameObjectMode.EnableWhenInsideArc
                        ? isInsideArc
                        : !isInsideArc;

                if (controlledObject.activeSelf != shouldBeActive)
                {
                    controlledObject.SetActive(shouldBeActive);
                }
            }
        }

        private bool IsWorldPointInsideControlledArc(
            Vector3 worldPoint,
            Vector3 centerPosition,
            Vector3 up,
            Vector3 forward
        )
        {
            Vector3 toPoint = worldPoint - centerPosition;
            Vector3 projected = Vector3.ProjectOnPlane(toPoint, up);

            if (projected.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            float distanceFromCenter = projected.magnitude;
            float outerRadius = radius + controlledObjectsOuterRadiusPadding;

            if (distanceFromCenter > outerRadius)
            {
                return false;
            }

            if (distanceFromCenter < controlledObjectsInnerRadius)
            {
                return false;
            }

            Vector3 baseForward =
                Quaternion.AngleAxis(yawOffsetDeg, up) * forward;

            float signedAngle =
                Vector3.SignedAngle(baseForward, projected.normalized, up);

            float halfArc = arcAngleDeg * 0.5f;
            float minAngle = -halfArc;
            float maxAngle = halfArc;

            if (controlledObjectsUseCurrentValue)
            {
                float normalizedArcValue = GetControlledObjectsArcValue();
                maxAngle = Mathf.Lerp(-halfArc, halfArc, normalizedArcValue);
            }

            const float angleEpsilon = 0.001f;

            return
                signedAngle >= minAngle - angleEpsilon &&
                signedAngle <= maxAngle + angleEpsilon;
        }

        private float GetControlledObjectsArcValue()
        {
            float arcValue = value01;

            if (controlledObjectsUseOutputValue && invertValue)
            {
                arcValue = 1f - arcValue;
            }

            return Mathf.Clamp01(arcValue);
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

        private bool TryGetCenterData(
            out Vector3 centerPosition,
            out Quaternion centerRotation,
            out Vector3 up,
            out Vector3 forward,
            out Vector3 right
        )
        {
            centerPosition = transform.position;
            centerRotation = Quaternion.identity;
            up = GetSafeUpAxis();
            forward = Vector3.forward;
            right = Vector3.right;

            Transform centerTransform = GetCenterTransform();

            if (centerTransform == null)
            {
                return false;
            }

            if (forceHorizontalPlane && useHorizontalLocalOffset)
            {
                Vector3 horizontalForward =
                    Vector3.ProjectOnPlane(centerTransform.forward, up);

                if (horizontalForward.sqrMagnitude <= 0.000001f)
                {
                    horizontalForward = Vector3.forward;
                }

                horizontalForward.Normalize();

                Vector3 horizontalRight =
                    Vector3.ProjectOnPlane(centerTransform.right, up);

                if (horizontalRight.sqrMagnitude <= 0.000001f)
                {
                    horizontalRight = Vector3.Cross(up, horizontalForward);
                }

                horizontalRight.Normalize();

                centerPosition =
                    centerTransform.position +
                    horizontalRight * centerLocalOffset.x +
                    up * centerLocalOffset.y +
                    horizontalForward * centerLocalOffset.z +
                    up * worldHeightOffset;

                centerRotation = Quaternion.LookRotation(horizontalForward, up);
                forward = horizontalForward;
                right = horizontalRight;
            }
            else
            {
                centerPosition =
                    centerTransform.TransformPoint(centerLocalOffset) +
                    up * worldHeightOffset;

                centerRotation = centerTransform.rotation;

                forward = Vector3.ProjectOnPlane(
                    centerRotation * Vector3.forward,
                    up
                );

                if (forward.sqrMagnitude <= 0.000001f)
                {
                    forward = Vector3.forward;
                }

                forward.Normalize();

                right = Vector3.ProjectOnPlane(
                    centerRotation * Vector3.right,
                    up
                );

                if (right.sqrMagnitude <= 0.000001f)
                {
                    right = Vector3.right;
                }

                right.Normalize();
            }

            return true;
        }

        private Vector3 GetSafeUpAxis()
        {
            if (horizontalUpAxis.sqrMagnitude <= 0.000001f)
            {
                return Vector3.up;
            }

            return horizontalUpAxis.normalized;
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

        private void CacheRigidbody()
        {
            if (cachedRigidbody == null)
            {
                cachedRigidbody = GetComponent<Rigidbody>();
            }
        }

        private void ClearPhysicsMotion()
        {
            if (!clearRigidbodyVelocity)
            {
                return;
            }

            if (cachedRigidbody == null)
            {
                return;
            }

            cachedRigidbody.linearVelocity = Vector3.zero;
            cachedRigidbody.angularVelocity = Vector3.zero;
        }

        private bool TryAutoDetectMetaGrabState(out bool grabbed)
        {
            grabbed = false;

            MonoBehaviour grabbable = GetCachedMetaGrabbable();

            if (grabbable == null)
            {
                return false;
            }

            object target = grabbable;
            Type type = target.GetType();

            if (TryReadBoolMember(target, type, "IsGrabbed", out grabbed))
            {
                return true;
            }

            if (TryReadBoolMember(target, type, "IsSelected", out grabbed))
            {
                return true;
            }

            if (TryReadBoolMember(target, type, "Selected", out grabbed))
            {
                return true;
            }

            if (TryReadBoolMember(target, type, "IsSelecting", out grabbed))
            {
                return true;
            }

            if (TryReadCountMember(target, type, "SelectingInteractorsCount", out int count))
            {
                grabbed = count > 0;
                return true;
            }

            if (TryReadCountMember(target, type, "SelectingPointsCount", out count))
            {
                grabbed = count > 0;
                return true;
            }

            if (TryReadCountMember(target, type, "InteractorsCount", out count))
            {
                grabbed = count > 0;
                return true;
            }

            if (TryReadCollectionMember(target, type, "SelectingInteractors", out count))
            {
                grabbed = count > 0;
                return true;
            }

            if (TryReadCollectionMember(target, type, "Interactors", out count))
            {
                grabbed = count > 0;
                return true;
            }

            if (TryReadCollectionMember(target, type, "SelectingPoints", out count))
            {
                grabbed = count > 0;
                return true;
            }

            return false;
        }

        private MonoBehaviour GetCachedMetaGrabbable()
        {
            if (cachedMetaGrabbable != null)
            {
                return cachedMetaGrabbable;
            }

            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();

            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];

                if (behaviour == null || behaviour == this)
                {
                    continue;
                }

                Type type = behaviour.GetType();
                string typeName = type.Name;
                string namespaceName = type.Namespace ?? string.Empty;

                bool looksLikeMetaGrabbable =
                    typeName.Equals("Grabbable", StringComparison.OrdinalIgnoreCase) ||
                    typeName.IndexOf("Grabbable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    namespaceName.IndexOf("Oculus.Interaction", StringComparison.OrdinalIgnoreCase) >= 0;

                if (looksLikeMetaGrabbable)
                {
                    cachedMetaGrabbable = behaviour;
                    return cachedMetaGrabbable;
                }
            }

            return null;
        }

        private bool TryReadBoolMember(
            object target,
            Type type,
            string memberName,
            out bool value
        )
        {
            value = false;

            try
            {
                PropertyInfo property = type.GetProperty(
                    memberName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );

                if (property != null &&
                    property.PropertyType == typeof(bool) &&
                    property.GetIndexParameters().Length == 0)
                {
                    value = (bool)property.GetValue(target);
                    return true;
                }

                FieldInfo field = type.GetField(
                    memberName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );

                if (field != null && field.FieldType == typeof(bool))
                {
                    value = (bool)field.GetValue(target);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private bool TryReadCountMember(
            object target,
            Type type,
            string memberName,
            out int count
        )
        {
            count = 0;

            try
            {
                PropertyInfo property = type.GetProperty(
                    memberName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );

                if (property != null &&
                    property.GetIndexParameters().Length == 0 &&
                    property.PropertyType == typeof(int))
                {
                    count = (int)property.GetValue(target);
                    return true;
                }

                FieldInfo field = type.GetField(
                    memberName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );

                if (field != null && field.FieldType == typeof(int))
                {
                    count = (int)field.GetValue(target);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private bool TryReadCollectionMember(
            object target,
            Type type,
            string memberName,
            out int count
        )
        {
            count = 0;

            try
            {
                object value = null;

                PropertyInfo property = type.GetProperty(
                    memberName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );

                if (property != null &&
                    property.GetIndexParameters().Length == 0)
                {
                    value = property.GetValue(target);
                }

                if (value == null)
                {
                    FieldInfo field = type.GetField(
                        memberName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic
                    );

                    if (field != null)
                    {
                        value = field.GetValue(target);
                    }
                }

                if (value == null)
                {
                    return false;
                }

                if (value is ICollection collection)
                {
                    count = collection.Count;
                    return true;
                }

                if (value is IEnumerable enumerable && !(value is string))
                {
                    int found = 0;

                    foreach (object _ in enumerable)
                    {
                        found++;

                        if (found > 0)
                        {
                            break;
                        }
                    }

                    count = found;
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private void EnsureVisualLine()
        {
            if (!autoCreateVisualLine)
            {
                return;
            }

            if (visualLine != null)
            {
                SetupVisualLineRenderer();
                return;
            }

            Transform existingChild = transform.Find(visualChildName);

            GameObject visualObject;

            if (existingChild != null)
            {
                visualObject = existingChild.gameObject;
            }
            else
            {
                visualObject = new GameObject(visualChildName);
                visualObject.transform.SetParent(transform, false);
            }

            visualLine = visualObject.GetComponent<LineRenderer>();

            if (visualLine == null)
            {
                visualLine = visualObject.AddComponent<LineRenderer>();
            }

            SetupVisualLineRenderer();
        }

        private void SetupVisualLineRenderer()
        {
            if (visualLine == null)
            {
                return;
            }

            visualLine.useWorldSpace = true;
            visualLine.loop = false;
            visualLine.widthMultiplier = visualWidth;
            visualLine.startColor = visualColor;
            visualLine.endColor = visualColor;
            visualLine.positionCount = visualSegments + 1;

            if (visualLine.sharedMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");

                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                }

                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }

                if (shader != null)
                {
                    visualLine.sharedMaterial = new Material(shader);
                }
            }

            UpdateVisualLineVisibility();
        }

        private void UpdateVisualLineVisibility()
        {
            if (visualLine == null)
            {
                return;
            }

            bool shouldShow =
                Application.isPlaying
                    ? showVisualInPlayMode
                    : showVisualInEditMode;

            visualLine.enabled = shouldShow;
        }

        private void UpdateVisualLine()
        {
            if (visualLine == null)
            {
                return;
            }

            UpdateVisualLineVisibility();

            if (!visualLine.enabled)
            {
                return;
            }

            SetupVisualLineRenderer();

            if (!TryGetCenterData(
                    out Vector3 centerPosition,
                    out Quaternion centerRotation,
                    out Vector3 up,
                    out Vector3 forward,
                    out Vector3 right
                ))
            {
                return;
            }

            int pointCount = visualSegments + 1;
            visualLine.positionCount = pointCount;

            for (int i = 0; i < pointCount; i++)
            {
                float t = i / (float)(pointCount - 1);

                Vector3 point;

                if (constraintMode == ConstraintMode.ArcAroundCenter)
                {
                    point = GetArcPoint(t, centerPosition, up, forward);
                }
                else
                {
                    point = GetLinePoint(t, centerPosition, centerRotation);
                }

                visualLine.SetPosition(i, point);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawGizmos)
            {
                return;
            }

            if (!TryGetCenterData(
                    out Vector3 centerPosition,
                    out Quaternion centerRotation,
                    out Vector3 up,
                    out Vector3 forward,
                    out Vector3 right
                ))
            {
                return;
            }

            Gizmos.color = gizmoArcColor;
            Gizmos.DrawWireSphere(centerPosition, 0.035f);

            if (constraintMode == ConstraintMode.ArcAroundCenter)
            {
                DrawArcGizmo(centerPosition, up, forward);
            }
            else
            {
                DrawLineGizmo(centerPosition, centerRotation);
            }
        }
        //OK
        private void DrawArcGizmo(
            Vector3 centerPosition,
            Vector3 up,
            Vector3 forward
        )
        {
            int segments = Mathf.Max(4, visualSegments);
            Vector3 previousPoint = GetArcPoint(0f, centerPosition, up, forward);

            Gizmos.color = gizmoArcColor;

            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 point = GetArcPoint(t, centerPosition, up, forward);
                Gizmos.DrawLine(previousPoint, point);
                previousPoint = point;
            }

            Gizmos.color = gizmoHandleColor;
            Gizmos.DrawWireSphere(
                GetArcPoint(value01, centerPosition, up, forward),
                0.045f
            );
        }

        private void DrawLineGizmo(
            Vector3 centerPosition,
            Quaternion centerRotation
        )
        {
            Vector3 a = GetLinePoint(0f, centerPosition, centerRotation);
            Vector3 b = GetLinePoint(1f, centerPosition, centerRotation);

            Gizmos.color = gizmoArcColor;
            Gizmos.DrawLine(a, b);

            Gizmos.color = gizmoHandleColor;
            Gizmos.DrawWireSphere(Vector3.Lerp(a, b, value01), 0.045f);
        }
#endif
    }
}
