// SPDX-License-Identifier: MIT

using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GaussianSplatting.Runtime
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CameraArcSceneMask : MonoBehaviour
    {
        public enum ArcSweepDirection
        {
            LeftToRight = 0,
            RightToLeft = 1
        }

        private static readonly int MaskEnabledId =
            Shader.PropertyToID("_CameraArcSceneMaskEnabled");

        private static readonly int WorldToMaskId =
            Shader.PropertyToID("_CameraArcSceneMaskWorldToLocal");

        private static readonly int ArcParamId =
            Shader.PropertyToID("_CameraArcSceneMaskArcParam");

        private static readonly int DistanceRangeId =
            Shader.PropertyToID("_CameraArcSceneMaskDistanceRange");

        private static readonly int OutsideColorId =
            Shader.PropertyToID("_CameraArcSceneMaskOutsideColor");

        private static readonly int SoftnessId =
            Shader.PropertyToID("_CameraArcSceneMaskSoftness");

        private static readonly int PreserveBackgroundId =
            Shader.PropertyToID("_CameraArcSceneMaskPreserveBackground");

        [Header("Mask")]

        [Tooltip("Enables the camera arc scene mask.")]
        public bool m_EnableMask = true;

        [Tooltip("Choose which side stays fixed while the other side expands.")]
        public ArcSweepDirection m_ArcSweepDirection = ArcSweepDirection.LeftToRight;

        [Tooltip("Horizontal reveal angle in degrees. 0 = render nothing. 360 = render everything.")]
        [Range(0f, 360f)]
        public float m_RevealAngleDeg = 0f;

        [Tooltip("If enabled, angle 0 renders nothing.")]
        public bool m_ZeroAngleRendersNothing = true;

        [Header("Distance")]

        [Tooltip("Optional near distance from the camera. 0 = no near limit.")]
        public float m_NearDistance = 0f;

        [Tooltip("Optional far distance from the camera. 0 = no far limit.")]
        public float m_FarDistance = 0f;

        [Header("Visual")]

        [Tooltip("Color shown outside the arc. Keep alpha at 0 to reveal passthrough instead of drawing a dark mask.")]
        public Color m_OutsideColor = Color.clear;

        [Tooltip("Softness around the arc edge. 0 = hard edge.")]
        [Range(0f, 0.25f)]
        public float m_EdgeSoftness = 0f;

        [Tooltip("Keeps pixels that belong to the camera's transparent background unchanged. " +
                 "Enable this for Quest passthrough so only rendered scene content is hidden.")]
        public bool m_PreserveCameraBackground = true;

        [Header("Transition")]

        [Tooltip("Duration used by Show Scene and Hide Scene.")]
        [Min(0f)]
        public float m_TransitionDuration = 1f;

        [Tooltip("Timing curve used by the animated transition.")]
        public AnimationCurve m_TransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Gizmos")]

        public bool m_DrawGizmos = true;

        [Tooltip("Radius used to draw the arc gizmo.")]
        public float m_GizmoRadius = 2f;

        [Range(8, 128)]
        public int m_GizmoSegments = 48;

        public Color m_CurrentArcColor = new Color(1f, 0f, 1f, 0.9f);
        public Color m_StartSideColor = new Color(1f, 1f, 0f, 0.95f);
        public Color m_FullCircleColor = new Color(0f, 1f, 1f, 0.25f);

        private const float TinyValue = 0.0001f;
        private Coroutine m_TransitionCoroutine;

        private void OnEnable()
        {
            UpdateShaderGlobals();
        }

        private void OnDisable()
        {
            StopTransition();
            DisableShaderGlobals();
        }

        private void OnValidate()
        {
            m_RevealAngleDeg = Mathf.Clamp(m_RevealAngleDeg, 0f, 360f);
            m_NearDistance = Mathf.Max(0f, m_NearDistance);
            m_FarDistance = Mathf.Max(0f, m_FarDistance);
            m_GizmoRadius = Mathf.Max(0.01f, m_GizmoRadius);
            m_GizmoSegments = Mathf.Clamp(m_GizmoSegments, 8, 128);
            m_TransitionDuration = Mathf.Max(0f, m_TransitionDuration);

            UpdateShaderGlobals();
        }

        private void LateUpdate()
        {
            UpdateShaderGlobals();
        }

        private void OnPreCull()
        {
            UpdateShaderGlobals();
        }

        [ContextMenu("Transition/Show Scene")]
        public void ShowScene()
        {
            TransitionTo(360f);
        }

        [ContextMenu("Transition/Hide Scene")]
        public void HideScene()
        {
            TransitionTo(0f);
        }

        public void SetSceneVisible(bool visible, bool animate = true)
        {
            float targetAngle = visible ? 360f : 0f;

            if (animate)
            {
                TransitionTo(targetAngle);
                return;
            }

            StopTransition();
            m_RevealAngleDeg = targetAngle;
            UpdateShaderGlobals();
        }

        private void TransitionTo(float targetAngle)
        {
            targetAngle = Mathf.Clamp(targetAngle, 0f, 360f);
            StopTransition();

            if (!Application.isPlaying || m_TransitionDuration <= TinyValue)
            {
                m_RevealAngleDeg = targetAngle;
                UpdateShaderGlobals();
                return;
            }

            m_TransitionCoroutine = StartCoroutine(
                AnimateRevealAngle(m_RevealAngleDeg, targetAngle)
            );
        }

        private IEnumerator AnimateRevealAngle(float startAngle, float targetAngle)
        {
            float elapsed = 0f;

            while (elapsed < m_TransitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / m_TransitionDuration);
                float curvedTime = m_TransitionCurve != null
                    ? m_TransitionCurve.Evaluate(normalizedTime)
                    : normalizedTime;

                m_RevealAngleDeg = Mathf.LerpUnclamped(startAngle, targetAngle, curvedTime);
                UpdateShaderGlobals();
                yield return null;
            }

            m_RevealAngleDeg = targetAngle;
            m_TransitionCoroutine = null;
            UpdateShaderGlobals();
        }

        private void StopTransition()
        {
            if (m_TransitionCoroutine == null)
            {
                return;
            }

            StopCoroutine(m_TransitionCoroutine);
            m_TransitionCoroutine = null;
        }

        private void UpdateShaderGlobals()
        {
            if (!isActiveAndEnabled || !m_EnableMask)
            {
                DisableShaderGlobals();
                return;
            }

            float revealAngle = Mathf.Clamp(m_RevealAngleDeg, 0f, 360f);

            bool zeroAngle =
                m_ZeroAngleRendersNothing &&
                revealAngle <= TinyValue;

            Matrix4x4 worldToMaskMatrix = transform.worldToLocalMatrix;

            if (!zeroAngle)
            {
                float halfAngle = revealAngle * 0.5f;

                float centerOffsetDeg;

                if (m_ArcSweepDirection == ArcSweepDirection.LeftToRight)
                {
                    centerOffsetDeg = -90f + halfAngle;
                }
                else
                {
                    centerOffsetDeg = 90f - halfAngle;
                }

                Matrix4x4 arcRotation =
                    Matrix4x4.Rotate(Quaternion.Euler(0f, -centerOffsetDeg, 0f));

                worldToMaskMatrix = arcRotation * worldToMaskMatrix;
            }

            float arcParam;

            if (zeroAngle)
            {
                arcParam = 2f;
            }
            else
            {
                arcParam = Mathf.Cos(
                    0.5f *
                    revealAngle *
                    Mathf.Deg2Rad
                );
            }

            Shader.SetGlobalFloat(MaskEnabledId, 1f);
            Shader.SetGlobalMatrix(WorldToMaskId, worldToMaskMatrix);
            Shader.SetGlobalFloat(ArcParamId, arcParam);
            Shader.SetGlobalVector(
                DistanceRangeId,
                new Vector4(m_NearDistance, m_FarDistance, 0f, 0f)
            );
            Shader.SetGlobalColor(OutsideColorId, m_OutsideColor);
            Shader.SetGlobalFloat(SoftnessId, m_EdgeSoftness);
            Shader.SetGlobalFloat(
                PreserveBackgroundId,
                m_PreserveCameraBackground ? 1f : 0f
            );
        }

        private static void DisableShaderGlobals()
        {
            Shader.SetGlobalFloat(MaskEnabledId, 0f);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!m_DrawGizmos)
            {
                return;
            }

            DrawCameraArcGizmo(false);
        }

        private void OnDrawGizmosSelected()
        {
            if (!m_DrawGizmos)
            {
                return;
            }

            DrawCameraArcGizmo(true);
        }

        private void DrawCameraArcGizmo(bool selected)
        {
            float alphaMultiplier = selected ? 1f : 0.45f;

            DrawFullCircleGizmo(alphaMultiplier);
            DrawCurrentArcGizmo(alphaMultiplier);
            DrawStartSideGizmo(alphaMultiplier);
        }

        private void DrawFullCircleGizmo(float alphaMultiplier)
        {
            Color color = m_FullCircleColor;
            color.a *= alphaMultiplier;

            Gizmos.color = color;

            const int fullSegments = 96;

            Vector3 previous = transform.TransformPoint(
                AngleToLocalPoint(-180f, m_GizmoRadius)
            );

            for (int i = 1; i <= fullSegments; i++)
            {
                float t = i / (float)fullSegments;
                float angle = Mathf.Lerp(-180f, 180f, t);

                Vector3 point = transform.TransformPoint(
                    AngleToLocalPoint(angle, m_GizmoRadius)
                );

                Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }

        private void DrawCurrentArcGizmo(float alphaMultiplier)
        {
            float revealAngle = Mathf.Clamp(m_RevealAngleDeg, 0f, 360f);

            Color color = m_CurrentArcColor;
            color.a *= alphaMultiplier;

            Gizmos.color = color;

            if (m_ZeroAngleRendersNothing && revealAngle <= TinyValue)
            {
                return;
            }

            float startDeg = GetFixedStartAngleDeg();
            float endDeg = GetCurrentEndAngleDeg(revealAngle);

            Vector3 center = transform.position;

            Vector3 startPoint = transform.TransformPoint(
                AngleToLocalPoint(startDeg, m_GizmoRadius)
            );

            Vector3 endPoint = transform.TransformPoint(
                AngleToLocalPoint(endDeg, m_GizmoRadius)
            );

            Gizmos.DrawLine(center, startPoint);
            Gizmos.DrawLine(center, endPoint);

            Vector3 previous = startPoint;

            for (int i = 1; i <= m_GizmoSegments; i++)
            {
                float t = i / (float)m_GizmoSegments;
                float angle = Mathf.Lerp(startDeg, endDeg, t);

                Vector3 point = transform.TransformPoint(
                    AngleToLocalPoint(angle, m_GizmoRadius)
                );

                Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }

        private void DrawStartSideGizmo(float alphaMultiplier)
        {
            Color color = m_StartSideColor;
            color.a *= alphaMultiplier;

            Gizmos.color = color;

            float startDeg = GetFixedStartAngleDeg();

            Vector3 center = transform.position;

            Vector3 startPoint = transform.TransformPoint(
                AngleToLocalPoint(startDeg, m_GizmoRadius)
            );

            Gizmos.DrawLine(center, startPoint);
            Gizmos.DrawWireSphere(startPoint, m_GizmoRadius * 0.025f);
        }

        private float GetFixedStartAngleDeg()
        {
            if (m_ArcSweepDirection == ArcSweepDirection.LeftToRight)
            {
                return -90f;
            }

            return 90f;
        }

        private float GetCurrentEndAngleDeg(float revealAngle)
        {
            if (m_ArcSweepDirection == ArcSweepDirection.LeftToRight)
            {
                return -90f + revealAngle;
            }

            return 90f - revealAngle;
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
#endif
    }
}
