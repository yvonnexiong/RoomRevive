// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GaussianSplatting.Runtime
{
    public class GaussianCutout : MonoBehaviour
    {
        public enum Type
        {
            Ellipsoid,
            Box,
            Arc
        }

        public enum ArcSweepDirection
        {
            LeftToRight = 0,
            RightToLeft = 1
        }

        public enum ArcVisibilityMode
        {
            HideInsideArc = 0,
            ShowOnlyInsideArc = 1
        }

        private const uint InvertFlag = 0x100u;

        // Shader flag:
        // If this is enabled on an Arc cutout, the arc should NOT cut by itself.
        // Instead, it should only limit/mask the existing Box/Ellipsoid cutouts.
        public const uint ArcMasksExistingCutoutsFlag = 0x200u;

        [Header("Cutout")]
        public Type m_Type = Type.Ellipsoid;

        [Tooltip("General invert flag. For Box/Ellipsoid this works normally. For Arc, use Arc Visibility Mode instead.")]
        public bool m_Invert = false;

        [Header("Arc")]
        [Tooltip("Arc type only: choose whether the arc hides the inside area or shows only the inside area.")]
        public ArcVisibilityMode m_ArcVisibilityMode = ArcVisibilityMode.HideInsideArc;

        [Tooltip("Arc type only: choose which side stays fixed while the other side expands.")]
        public ArcSweepDirection m_ArcSweepDirection = ArcSweepDirection.LeftToRight;

        [Tooltip("Arc type only: horizontal reveal angle in degrees. 0 = fully hidden, 360 = fully visible.")]
        [Range(0f, 360f)]
        public float m_RevealAngleDeg = 180f;

        [Tooltip("Arc type only: if enabled, angle 0 sends an impossible arc to the shader, making the splat fully hidden.")]
        public bool m_ZeroAngleDisablesArc = true;

        [Tooltip("Arc type only: if enabled, this arc does not cut by itself. It only masks/limits existing Box and Ellipsoid cutouts. Ignored when Arc Visibility Mode is ShowOnlyInsideArc.")]
        public bool m_ArcMasksExistingCutouts = true;

        [Header("Layer Cutting")]
        public bool[] layersToCut = Array.Empty<bool>();

        public unsafe struct ShaderData
        {
            public Matrix4x4 matrix;
            public uint typeAndFlags;
            public fixed int cutIndices[8];
            public float arcParam;
        }

        public void SetRevealAngle(float revealAngleDeg)
        {
            m_RevealAngleDeg = Mathf.Clamp(revealAngleDeg, 0f, 360f);
        }

        public void SetReveal01(float reveal01)
        {
            m_RevealAngleDeg = Mathf.Clamp01(reveal01) * 360f;
        }

        public void ForceArc()
        {
            m_Type = Type.Arc;
        }

        public void SetArcVisibilityMode(ArcVisibilityMode mode)
        {
            m_ArcVisibilityMode = mode;
        }

        public void ShowOnlyInsideArc()
        {
            m_Type = Type.Arc;
            m_ArcVisibilityMode = ArcVisibilityMode.ShowOnlyInsideArc;
        }

        public void HideInsideArc()
        {
            m_Type = Type.Arc;
            m_ArcVisibilityMode = ArcVisibilityMode.HideInsideArc;
        }

        public static bool IsArcMaskOnly(ShaderData data)
        {
            return (data.typeAndFlags & ArcMasksExistingCutoutsFlag) != 0u;
        }

        public static ShaderData GetShaderData(
            GaussianCutout self,
            Matrix4x4 rendererMatrix,
            GaussianSplatAsset asset)
        {
            ShaderData sd = default;

            if (!(self && self.isActiveAndEnabled))
            {
                sd.typeAndFlags = ~0u;
                return sd;
            }

            unsafe
            {
                for (int i = 0; i < 8; i++)
                {
                    sd.cutIndices[i] = -1;
                }
            }

            Transform tr = self.transform;

            Matrix4x4 cutoutMatrix = tr.worldToLocalMatrix * rendererMatrix;

            float revealAngle = Mathf.Clamp(self.m_RevealAngleDeg, 0f, 360f);

            bool isArc = self.m_Type == Type.Arc;

            bool arcShowsOnlyInside =
                isArc &&
                self.m_ArcVisibilityMode == ArcVisibilityMode.ShowOnlyInsideArc;

            bool zeroAngleArc =
                isArc &&
                self.m_ZeroAngleDisablesArc &&
                revealAngle <= 0.0001f;

            if (isArc && !zeroAngleArc)
            {
                float halfAngle = revealAngle * 0.5f;

                float centerOffsetDeg;

                if (self.m_ArcSweepDirection == ArcSweepDirection.LeftToRight)
                {
                    // Left boundary stays fixed.
                    // The arc expands from local -X toward local +X.
                    centerOffsetDeg = -90f + halfAngle;
                }
                else
                {
                    // Right boundary stays fixed.
                    // The arc expands from local +X toward local -X.
                    centerOffsetDeg = 90f - halfAngle;
                }

                Matrix4x4 arcRotation =
                    Matrix4x4.Rotate(Quaternion.Euler(0f, -centerOffsetDeg, 0f));

                cutoutMatrix = arcRotation * cutoutMatrix;
            }

            sd.matrix = cutoutMatrix;

            uint flags = 0u;

            bool shouldInvert = self.m_Invert;

            if (arcShowsOnlyInside)
            {
                // This makes the arc keep what is inside the arc,
                // instead of cutting/hiding what is inside the arc.
                shouldInvert = true;
            }

            if (shouldInvert)
            {
                flags |= InvertFlag;
            }

            bool arcMasksExistingCutouts =
                isArc &&
                self.m_ArcMasksExistingCutouts &&
                !arcShowsOnlyInside;

            if (arcMasksExistingCutouts)
            {
                flags |= ArcMasksExistingCutoutsFlag;
            }

            sd.typeAndFlags =
                ((uint)self.m_Type) |
                flags;

            if (zeroAngleArc)
            {
                // Dot products cannot be greater than 1.
                // Setting arcParam above 1 means the shader finds no valid arc area.
                sd.arcParam = 2f;
            }
            else
            {
                sd.arcParam = Mathf.Cos(
                    0.5f *
                    revealAngle *
                    Mathf.Deg2Rad
                );
            }

            for (int layer = 0; layer < Math.Min(4, self.layersToCut.Length); layer++)
            {
                if (self.layersToCut[layer] && asset.layerInfo.TryGetValue(layer, out int count))
                {
                    int idxFrom = asset.layerInfo
                        .Where(kv => kv.Key < layer)
                        .Sum(kv => kv.Value);

                    unsafe
                    {
                        sd.cutIndices[layer * 2] = idxFrom;
                        sd.cutIndices[layer * 2 + 1] = idxFrom + count;
                    }
                }
            }

            return sd;
        }

#if UNITY_EDITOR
        public void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;

            Color color = Color.magenta;

            if (m_Type == Type.Arc && m_ArcVisibilityMode == ArcVisibilityMode.ShowOnlyInsideArc)
            {
                color = Color.cyan;
            }

            color.a = 0.2f;

            if (Selection.Contains(gameObject))
            {
                color.a = 0.9f;
            }
            else
            {
                GameObject activeGo = Selection.activeGameObject;

                if (activeGo != null)
                {
                    GaussianSplatRenderer activeSplat =
                        activeGo.GetComponent<GaussianSplatRenderer>();

                    if (activeSplat != null)
                    {
                        if (activeSplat.m_Cutouts != null &&
                            activeSplat.m_Cutouts.Contains(this))
                        {
                            color.a = 0.5f;
                        }
                    }
                }
            }

            Gizmos.color = color;

            if (m_Type == Type.Ellipsoid)
            {
                Gizmos.DrawWireSphere(Vector3.zero, 1.0f);
            }

            if (m_Type == Type.Box)
            {
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one * 2f);
            }

            if (m_Type == Type.Arc)
            {
                DrawArcGizmo();
            }
        }

        private void DrawArcGizmo()
        {
            float revealAngle = Mathf.Clamp(m_RevealAngleDeg, 0f, 360f);

            if (m_ZeroAngleDisablesArc && revealAngle <= 0.0001f)
            {
                DrawZeroAngleArcGizmo();
                return;
            }

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

            float centerRad = centerOffsetDeg * Mathf.Deg2Rad;
            float halfRad = halfAngle * Mathf.Deg2Rad;

            float startRad = centerRad - halfRad;
            float endRad = centerRad + halfRad;

            const int segments = 32;

            Vector3 startPoint = AngleToLocalPoint(startRad);
            Vector3 endPoint = AngleToLocalPoint(endRad);

            Gizmos.DrawLine(Vector3.zero, startPoint);
            Gizmos.DrawLine(Vector3.zero, endPoint);

            Vector3 previousPoint = startPoint;

            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = Mathf.Lerp(startRad, endRad, t);

                Vector3 point = AngleToLocalPoint(angle);

                Gizmos.DrawLine(previousPoint, point);
                previousPoint = point;
            }
        }

        private void DrawZeroAngleArcGizmo()
        {
            float startDeg;

            if (m_ArcSweepDirection == ArcSweepDirection.LeftToRight)
            {
                startDeg = -90f;
            }
            else
            {
                startDeg = 90f;
            }

            Vector3 p = AngleToLocalPoint(startDeg * Mathf.Deg2Rad);

            Gizmos.DrawLine(Vector3.zero, p);
            Gizmos.DrawWireSphere(p, 0.03f);
        }

        private static Vector3 AngleToLocalPoint(float angleRad)
        {
            return new Vector3(
                Mathf.Sin(angleRad),
                0f,
                Mathf.Cos(angleRad)
            );
        }
#endif
    }
}