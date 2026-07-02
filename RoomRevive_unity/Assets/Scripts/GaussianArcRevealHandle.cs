// SPDX-License-Identifier: MIT

using UnityEngine;

namespace GaussianSplatting.Runtime
{
    [ExecuteAlways]
    public class ArcRevealEditorHandle : MonoBehaviour
    {
        public enum ArcSweepDirection
        {
            LeftToRight = 0,
            RightToLeft = 1
        }

        [Header("Arc Value")]

        [Tooltip("Current reveal angle controlled by the draggable handle.")]
        [Range(0f, 360f)]
        public float m_RevealAngleDeg = 0f;

        [Tooltip("Maximum angle the handle can reveal.")]
        [Range(0f, 360f)]
        public float m_MaxRevealAngleDeg = 180f;

        [Tooltip("Choose which side stays fixed while the handle moves.")]
        public ArcSweepDirection m_ArcSweepDirection = ArcSweepDirection.LeftToRight;

        [Header("Handle")]

        [Tooltip("Radius of the draggable sphere from the center.")]
        public float m_HandleRadius = 1.25f;

        [Tooltip("Visual size of the draggable sphere in the Scene View.")]
        public float m_HandleSize = 0.08f;

        [Tooltip("How sensitive the handle is when dragging along the arc.")]
        public float m_DragSensitivity = 1f;

        [Tooltip("How closely your drag direction must match the arc direction before the handle moves. Higher = stricter.")]
        [Range(0f, 1f)]
        public float m_DragDirectionThreshold = 0.25f;

        [Header("Gizmos")]

        public bool m_DrawWhenNotSelected = true;
        public bool m_DrawFullSpan = true;
        public bool m_DrawCurrentReveal = true;
        public bool m_DrawFixedStartMarker = true;
        public bool m_DrawHandleLabel = true;

        [Tooltip("The full-span arc radius. Use -1 to use the same value as Handle Radius.")]
        public float m_FullSpanRadius = -1f;

        [Tooltip("The current reveal arc radius. Use -1 to use the same value as Handle Radius.")]
        public float m_CurrentRevealRadius = -1f;

        [Header("Gizmo Colors")]

        public Color m_FullSpanColor = new Color(0f, 1f, 1f, 0.35f);
        public Color m_CurrentRevealColor = new Color(1f, 0f, 1f, 0.9f);
        public Color m_StartMarkerColor = new Color(1f, 1f, 0f, 0.95f);
        public Color m_HandleColor = new Color(1f, 0.55f, 0f, 1f);

        private const float MinRadius = 0.01f;
        private const float TinyValue = 0.0001f;

        private void OnValidate()
        {
            m_MaxRevealAngleDeg = Mathf.Clamp(m_MaxRevealAngleDeg, 0f, 360f);
            m_RevealAngleDeg = Mathf.Clamp(m_RevealAngleDeg, 0f, m_MaxRevealAngleDeg);

            m_HandleRadius = Mathf.Max(MinRadius, m_HandleRadius);
            m_HandleSize = Mathf.Max(0.001f, m_HandleSize);
            m_DragSensitivity = Mathf.Max(0.001f, m_DragSensitivity);
        }

        public float GetCurrentRevealAngle()
        {
            return Mathf.Clamp(m_RevealAngleDeg, 0f, m_MaxRevealAngleDeg);
        }

        public float GetNormalizedRevealValue()
        {
            if (m_MaxRevealAngleDeg <= TinyValue)
            {
                return 0f;
            }

            return Mathf.Clamp01(GetCurrentRevealAngle() / m_MaxRevealAngleDeg);
        }

        public void SetNormalizedRevealValue(float normalizedValue)
        {
            normalizedValue = Mathf.Clamp01(normalizedValue);
            m_RevealAngleDeg = m_MaxRevealAngleDeg * normalizedValue;
        }

        public float GetFixedStartAngleDeg()
        {
            if (m_ArcSweepDirection == ArcSweepDirection.LeftToRight)
            {
                return -90f;
            }

            return 90f;
        }

        public float GetCurrentEndAngleDeg()
        {
            return GetEndAngleDeg(GetCurrentRevealAngle());
        }

        public float GetEndAngleDeg(float revealAngle)
        {
            revealAngle = Mathf.Clamp(revealAngle, 0f, m_MaxRevealAngleDeg);

            float startAngle = GetFixedStartAngleDeg();

            if (m_ArcSweepDirection == ArcSweepDirection.LeftToRight)
            {
                return startAngle + revealAngle;
            }

            return startAngle - revealAngle;
        }

        public float GetSignedSweepAngle(float revealAngle)
        {
            revealAngle = Mathf.Clamp(revealAngle, 0f, m_MaxRevealAngleDeg);

            if (m_ArcSweepDirection == ArcSweepDirection.LeftToRight)
            {
                return revealAngle;
            }

            return -revealAngle;
        }

        public Vector3 GetWorldCenter()
        {
            return transform.position;
        }

        public Vector3 GetWorldArcNormal()
        {
            return transform.TransformDirection(Vector3.up).normalized;
        }

        public Vector3 GetWorldArcStartDirection()
        {
            float angleDeg = GetFixedStartAngleDeg();
            Vector3 localDirection = AngleToLocalPoint(angleDeg, 1f);

            return transform.TransformDirection(localDirection).normalized;
        }

        public Vector3 GetWorldHandlePosition()
        {
            float angleDeg = GetCurrentEndAngleDeg();
            Vector3 localPosition = AngleToLocalPoint(angleDeg, m_HandleRadius);

            return transform.TransformPoint(localPosition);
        }

        public Vector3 GetWorldStartPosition(float radius)
        {
            float angleDeg = GetFixedStartAngleDeg();
            Vector3 localPosition = AngleToLocalPoint(angleDeg, radius);

            return transform.TransformPoint(localPosition);
        }

        public Vector3 GetWorldEndPosition(float revealAngle, float radius)
        {
            float angleDeg = GetEndAngleDeg(revealAngle);
            Vector3 localPosition = AngleToLocalPoint(angleDeg, radius);

            return transform.TransformPoint(localPosition);
        }

        public Vector3 GetLocalArcTangentDirection()
        {
            float angleDeg = GetCurrentEndAngleDeg();
            float angleRad = angleDeg * Mathf.Deg2Rad;

            Vector3 tangentForIncreasingAngle = new Vector3(
                Mathf.Cos(angleRad),
                0f,
                -Mathf.Sin(angleRad)
            ).normalized;

            if (m_ArcSweepDirection == ArcSweepDirection.LeftToRight)
            {
                return tangentForIncreasingAngle;
            }

            return -tangentForIncreasingAngle;
        }

        public Vector3 GetWorldArcTangentDirection()
        {
            return transform
                .TransformDirection(GetLocalArcTangentDirection())
                .normalized;
        }

        public void ApplySceneDrag(
            Vector3 previousWorldHandlePosition,
            Vector3 requestedWorldHandlePosition)
        {
            Vector3 previousLocalPosition =
                transform.InverseTransformPoint(previousWorldHandlePosition);

            Vector3 requestedLocalPosition =
                transform.InverseTransformPoint(requestedWorldHandlePosition);

            previousLocalPosition.y = 0f;
            requestedLocalPosition.y = 0f;

            Vector3 localDragDelta = requestedLocalPosition - previousLocalPosition;

            if (localDragDelta.sqrMagnitude <= TinyValue)
            {
                return;
            }

            Vector3 localDragDirection = localDragDelta.normalized;
            Vector3 localArcTangent = GetLocalArcTangentDirection();

            float directionDot = Vector3.Dot(localDragDirection, localArcTangent);

            if (Mathf.Abs(directionDot) < m_DragDirectionThreshold)
            {
                return;
            }

            float signedDragDistance = Vector3.Dot(localDragDelta, localArcTangent);

            float revealDelta =
                signedDragDistance /
                Mathf.Max(MinRadius, m_HandleRadius) *
                Mathf.Rad2Deg *
                m_DragSensitivity;

            m_RevealAngleDeg = Mathf.Clamp(
                m_RevealAngleDeg + revealDelta,
                0f,
                m_MaxRevealAngleDeg
            );
        }

        private void OnDrawGizmos()
        {
            if (!m_DrawWhenNotSelected)
            {
                return;
            }

            DrawGizmoVisuals(false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawGizmoVisuals(true);
        }

        private void DrawGizmoVisuals(bool selected)
        {
            float alphaMultiplier = selected ? 1f : 0.45f;

            if (m_DrawFullSpan)
            {
                DrawFullSpanGizmo(alphaMultiplier);
            }

            if (m_DrawCurrentReveal)
            {
                DrawCurrentRevealGizmo(alphaMultiplier);
            }

            if (m_DrawFixedStartMarker)
            {
                DrawFixedStartMarkerGizmo(alphaMultiplier);
            }

            DrawHandleSphereGizmo(alphaMultiplier);
        }

        private void DrawFullSpanGizmo(float alphaMultiplier)
        {
            Color color = m_FullSpanColor;
            color.a *= alphaMultiplier;
            Gizmos.color = color;

            float radius = GetFullSpanRadius();

            DrawArcGizmo(radius, m_MaxRevealAngleDeg, 96);

            Vector3 start = GetWorldStartPosition(radius);
            Vector3 end = GetWorldEndPosition(m_MaxRevealAngleDeg, radius);

            Gizmos.DrawLine(GetWorldCenter(), start);
            Gizmos.DrawLine(GetWorldCenter(), end);

            Gizmos.DrawWireSphere(end, m_HandleSize * 0.45f);
        }

        private void DrawCurrentRevealGizmo(float alphaMultiplier)
        {
            Color color = m_CurrentRevealColor;
            color.a *= alphaMultiplier;
            Gizmos.color = color;

            float radius = GetCurrentRevealRadius();

            DrawArcGizmo(radius, GetCurrentRevealAngle(), 48);

            Vector3 start = GetWorldStartPosition(radius);
            Vector3 end = GetWorldEndPosition(GetCurrentRevealAngle(), radius);

            Gizmos.DrawLine(GetWorldCenter(), start);
            Gizmos.DrawLine(GetWorldCenter(), end);
        }

        private void DrawFixedStartMarkerGizmo(float alphaMultiplier)
        {
            Color color = m_StartMarkerColor;
            color.a *= alphaMultiplier;
            Gizmos.color = color;

            Vector3 start = GetWorldStartPosition(m_HandleRadius);

            Gizmos.DrawLine(GetWorldCenter(), start);
            Gizmos.DrawWireSphere(start, m_HandleSize * 0.75f);
        }

        private void DrawHandleSphereGizmo(float alphaMultiplier)
        {
            Color color = m_HandleColor;
            color.a *= alphaMultiplier;
            Gizmos.color = color;

            Gizmos.DrawWireSphere(GetWorldHandlePosition(), m_HandleSize);
        }

        private void DrawArcGizmo(float radius, float revealAngle, int segments)
        {
            float startAngle = GetFixedStartAngleDeg();
            float signedSweep = GetSignedSweepAngle(revealAngle);

            Vector3 previousPoint =
                transform.TransformPoint(AngleToLocalPoint(startAngle, radius));

            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = startAngle + signedSweep * t;

                Vector3 point =
                    transform.TransformPoint(AngleToLocalPoint(angle, radius));

                Gizmos.DrawLine(previousPoint, point);
                previousPoint = point;
            }
        }

        private float GetFullSpanRadius()
        {
            if (m_FullSpanRadius > 0f)
            {
                return m_FullSpanRadius;
            }

            return m_HandleRadius;
        }

        private float GetCurrentRevealRadius()
        {
            if (m_CurrentRevealRadius > 0f)
            {
                return m_CurrentRevealRadius;
            }

            return m_HandleRadius;
        }

        private static Vector3 AngleToLocalPoint(float angleDeg, float radius)
        {
            float angleRad = angleDeg * Mathf.Deg2Rad;

            return new Vector3(
                Mathf.Sin(angleRad) * radius,
                0f,
                Mathf.Cos(angleRad) * radius
            );
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(ArcRevealEditorHandle))]
    public class ArcRevealEditorHandleEditor : UnityEditor.Editor
    {
        private ArcRevealEditorHandle TargetHandle
        {
            get { return (ArcRevealEditorHandle)target; }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ArcRevealEditorHandle handle = TargetHandle;

            UnityEditor.EditorGUILayout.Space();
            UnityEditor.EditorGUILayout.LabelField("Handle Value", UnityEditor.EditorStyles.boldLabel);

            UnityEditor.EditorGUI.BeginChangeCheck();

            float newReveal = UnityEditor.EditorGUILayout.Slider(
                "Reveal Angle",
                handle.GetCurrentRevealAngle(),
                0f,
                handle.m_MaxRevealAngleDeg
            );

            if (UnityEditor.EditorGUI.EndChangeCheck())
            {
                UnityEditor.Undo.RecordObject(handle, "Change Arc Reveal Angle");

                handle.m_RevealAngleDeg = newReveal;

                UnityEditor.EditorUtility.SetDirty(handle);
                UnityEditor.SceneView.RepaintAll();
            }

            UnityEditor.EditorGUILayout.LabelField(
                "Normalized",
                handle.GetNormalizedRevealValue().ToString("0.000")
            );
        }

        private void OnSceneGUI()
        {
            ArcRevealEditorHandle handle = TargetHandle;

            DrawSceneArcVisuals(handle);
            DrawSceneDragHandle(handle);
        }

        private void DrawSceneArcVisuals(ArcRevealEditorHandle handle)
        {
            UnityEditor.Handles.zTest =
                UnityEngine.Rendering.CompareFunction.LessEqual;

            Vector3 center = handle.GetWorldCenter();
            Vector3 normal = handle.GetWorldArcNormal();
            Vector3 startDirection = handle.GetWorldArcStartDirection();

            if (handle.m_DrawFullSpan)
            {
                UnityEditor.Handles.color = handle.m_FullSpanColor;

                float radius = handle.m_FullSpanRadius > 0f
                    ? handle.m_FullSpanRadius
                    : handle.m_HandleRadius;

                UnityEditor.Handles.DrawWireArc(
                    center,
                    normal,
                    startDirection,
                    handle.GetSignedSweepAngle(handle.m_MaxRevealAngleDeg),
                    radius
                );
            }

            if (handle.m_DrawCurrentReveal)
            {
                UnityEditor.Handles.color = handle.m_CurrentRevealColor;

                float radius = handle.m_CurrentRevealRadius > 0f
                    ? handle.m_CurrentRevealRadius
                    : handle.m_HandleRadius;

                UnityEditor.Handles.DrawWireArc(
                    center,
                    normal,
                    startDirection,
                    handle.GetSignedSweepAngle(handle.GetCurrentRevealAngle()),
                    radius
                );
            }

            if (handle.m_DrawFixedStartMarker)
            {
                UnityEditor.Handles.color = handle.m_StartMarkerColor;

                Vector3 startPosition =
                    handle.GetWorldStartPosition(handle.m_HandleRadius);

                UnityEditor.Handles.DrawLine(center, startPosition);

                float startHandleSize =
                    UnityEditor.HandleUtility.GetHandleSize(startPosition) *
                    handle.m_HandleSize *
                    0.75f;

                UnityEditor.Handles.SphereHandleCap(
                    0,
                    startPosition,
                    Quaternion.identity,
                    startHandleSize,
                    EventType.Repaint
                );
            }
        }

        private void DrawSceneDragHandle(ArcRevealEditorHandle handle)
        {
            Vector3 currentWorldPosition = handle.GetWorldHandlePosition();

            float sceneHandleSize =
                UnityEditor.HandleUtility.GetHandleSize(currentWorldPosition) *
                handle.m_HandleSize;

            UnityEditor.Handles.color = handle.m_HandleColor;

            UnityEditor.EditorGUI.BeginChangeCheck();

            Vector3 requestedWorldPosition = UnityEditor.Handles.FreeMoveHandle(
                currentWorldPosition,
                sceneHandleSize,
                Vector3.zero,
                UnityEditor.Handles.SphereHandleCap
            );

            if (UnityEditor.EditorGUI.EndChangeCheck())
            {
                UnityEditor.Undo.RecordObject(handle, "Drag Arc Reveal Handle");

                float previousReveal = handle.GetCurrentRevealAngle();

                handle.ApplySceneDrag(
                    currentWorldPosition,
                    requestedWorldPosition
                );

                float newReveal = handle.GetCurrentRevealAngle();

                if (!Mathf.Approximately(previousReveal, newReveal))
                {
                    UnityEditor.EditorUtility.SetDirty(handle);
                    UnityEditor.SceneView.RepaintAll();
                }
            }

            DrawHandleLabel(handle);
        }

        private void DrawHandleLabel(ArcRevealEditorHandle handle)
        {
            if (!handle.m_DrawHandleLabel)
            {
                return;
            }

            Vector3 handlePosition = handle.GetWorldHandlePosition();

            Vector3 labelOffset =
                handle.GetWorldArcNormal() *
                UnityEditor.HandleUtility.GetHandleSize(handlePosition) *
                0.12f;

            string label =
                handle.GetCurrentRevealAngle().ToString("0.0") +
                "° / " +
                handle.m_MaxRevealAngleDeg.ToString("0.0") +
                "°";

            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(handlePosition + labelOffset, label);
        }
    }
#endif
}