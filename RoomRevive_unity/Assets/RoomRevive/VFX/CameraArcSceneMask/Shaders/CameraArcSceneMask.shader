Shader "Hidden/GaussianSplatting/CameraArcSceneMask"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Camera Arc Scene Mask"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _CameraArcSceneMaskEnabled;
            float4x4 _CameraArcSceneMaskWorldToLocal;
            float _CameraArcSceneMaskArcParam;
            float4 _CameraArcSceneMaskDistanceRange;
            float4 _CameraArcSceneMaskOutsideColor;
            float _CameraArcSceneMaskSoftness;
            float _CameraArcSceneMaskPreserveBackground;

            float GetArcMaskAmount(float3 worldPos)
            {
                if (_CameraArcSceneMaskEnabled < 0.5)
                {
                    return 1.0;
                }

                float3 localPos =
                    mul(_CameraArcSceneMaskWorldToLocal, float4(worldPos, 1.0)).xyz;

                float2 horizontal = localPos.xz;
                float horizontalDistance = length(horizontal);

                float nearDistance = _CameraArcSceneMaskDistanceRange.x;
                float farDistance = _CameraArcSceneMaskDistanceRange.y;

                if (nearDistance > 0.0 && horizontalDistance < nearDistance)
                {
                    return 0.0;
                }

                if (farDistance > 0.0 && horizontalDistance > farDistance)
                {
                    return 0.0;
                }

                if (horizontalDistance < 0.00001)
                {
                    return _CameraArcSceneMaskArcParam <= 1.0 ? 1.0 : 0.0;
                }

                float2 dir = horizontal / horizontalDistance;

                float dotToForward = dir.y;

                if (_CameraArcSceneMaskSoftness <= 0.00001)
                {
                    return dotToForward >= _CameraArcSceneMaskArcParam ? 1.0 : 0.0;
                }

                return smoothstep(
                    _CameraArcSceneMaskArcParam - _CameraArcSceneMaskSoftness,
                    _CameraArcSceneMaskArcParam + _CameraArcSceneMaskSoftness,
                    dotToForward
                );
            }

            float3 ReconstructWorldPosition(float2 uv)
            {
                real depth = SampleSceneDepth(uv);

                #if !UNITY_REVERSED_Z
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
                #endif

                return ComputeWorldSpacePosition(
                    uv,
                    depth,
                    UNITY_MATRIX_I_VP
                );
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                half4 color =
                    SAMPLE_TEXTURE2D_X(
                        _BlitTexture,
                        sampler_LinearClamp,
                        uv
                    );

                if (_CameraArcSceneMaskEnabled < 0.5)
                {
                    return color;
                }

                // A passthrough camera clears to alpha 0. Rendered meshes and the
                // Gaussian splat composite add alpha, so this keeps the untouched
                // passthrough/background pixels out of the transition.
                if (_CameraArcSceneMaskPreserveBackground > 0.5 && color.a <= 0.0001)
                {
                    return color;
                }

                float3 worldPos = ReconstructWorldPosition(uv);

                float maskAmount = GetArcMaskAmount(worldPos);

                return lerp(
                    _CameraArcSceneMaskOutsideColor,
                    color,
                    maskAmount
                );
            }

            ENDHLSL
        }
    }
}
