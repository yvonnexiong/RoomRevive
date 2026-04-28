
Shader "XRCC/Cyanilux Style Forcefield URP"
{
    Properties
    {
        _ForcefieldColor ("Forcefield Color", Color) = (0.15, 0.85, 1, 1)

        _SphereCenterWS ("Sphere Center WS", Vector) = (0, 0, 0, 0)
        _SphereRadiusWS ("Sphere Radius WS", Float) = 1

        _SurfaceAlpha ("Surface Alpha", Range(0, 1)) = 0.025
        _FresnelPower ("Fresnel Power", Float) = 6
        _FresnelIntensity ("Fresnel Intensity", Float) = 1.5

        _IntersectionDistance ("Intersection Distance", Float) = 0.18
        _IntersectionSoftness ("Intersection Softness", Float) = 0.16
        _IntersectionIntensity ("Intersection Intensity", Float) = 6

        _NoiseScale ("Noise Scale", Float) = 9
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.22
        _ScanlineScale ("Scanline Scale", Float) = 14
        _ScanlineStrength ("Scanline Strength", Range(0, 2)) = 0.45

        _RippleWidth ("Ripple Width", Float) = 0.22
        _RippleWorldRadius ("Ripple World Radius", Float) = 1.4
        _RippleIntensity ("Ripple Intensity", Float) = 3

        _CustomTime ("Custom Time", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "XRCC Forcefield Depth Intersection"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend SrcAlpha One
            ZWrite Off
            ZTest Always
            Cull Off
            Offset -8, -8

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ForcefieldColor;

                float4 _SphereCenterWS;
                float _SphereRadiusWS;

                float _SurfaceAlpha;
                float _FresnelPower;
                float _FresnelIntensity;

                float _IntersectionDistance;
                float _IntersectionSoftness;
                float _IntersectionIntensity;

                float _NoiseScale;
                float _NoiseStrength;
                float _ScanlineScale;
                float _ScanlineStrength;

                float _RippleWidth;
                float _RippleWorldRadius;
                float _RippleIntensity;

                float _CustomTime;
            CBUFFER_END

            uniform float _Points[24];

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);

                f = f * f * (3.0 - 2.0 * f);

                float n000 = Hash31(i + float3(0, 0, 0));
                float n100 = Hash31(i + float3(1, 0, 0));
                float n010 = Hash31(i + float3(0, 1, 0));
                float n110 = Hash31(i + float3(1, 1, 0));

                float n001 = Hash31(i + float3(0, 0, 1));
                float n101 = Hash31(i + float3(1, 0, 1));
                float n011 = Hash31(i + float3(0, 1, 1));
                float n111 = Hash31(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);

                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);

                return lerp(nxy0, nxy1, f.z);
            }

            float GetRipple(float3 scenePositionWS)
            {
                float rippleOutput = 0.0;

                [unroll]
                for (int i = 0; i < 24; i += 4)
                {
                    float3 pointWS = float3(_Points[i + 0], _Points[i + 1], _Points[i + 2]);
                    float lifetime = _Points[i + 3];

                    if (lifetime <= 1.0)
                    {
                        float expandingRadius = lifetime * _RippleWorldRadius;
                        float distanceToRipple = distance(scenePositionWS, pointWS);
                        float ringDistance = abs(distanceToRipple - expandingRadius);

                        float ring = 1.0 - smoothstep(0.0, _RippleWidth, ringDistance);
                        float fade = saturate(1.0 - lifetime);

                        rippleOutput += ring * fade;
                    }
                }

                return saturate(rippleOutput);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalize(normalInputs.normalWS);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;

                float rawSceneDepth = SampleSceneDepth(screenUV);

                #if UNITY_REVERSED_Z
                    float depth = rawSceneDepth;
                    float sceneValid = step(0.00001, rawSceneDepth);
                #else
                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawSceneDepth);
                    float sceneValid = step(rawSceneDepth, 0.99999);
                #endif

                float3 scenePositionWS = ComputeWorldSpacePosition(screenUV, depth, UNITY_MATRIX_I_VP);

                float sceneDistanceFromSphereCenter = distance(scenePositionWS, _SphereCenterWS.xyz);
                float sceneDistanceFromSphereShell = abs(sceneDistanceFromSphereCenter - _SphereRadiusWS);

                float intersection = 1.0 - smoothstep(
                    _IntersectionDistance,
                    _IntersectionDistance + _IntersectionSoftness,
                    sceneDistanceFromSphereShell
                );

                intersection *= sceneValid;

                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float normalDot = abs(dot(viewDir, normalize(input.normalWS)));
                float fresnel = pow(1.0 - saturate(normalDot), _FresnelPower);

                float noise = ValueNoise(scenePositionWS * _NoiseScale + _CustomTime * 0.35);
                float noiseMask = lerp(1.0, noise, _NoiseStrength);

                float scan = sin(sceneDistanceFromSphereCenter * _ScanlineScale - _CustomTime * 6.0) * 0.5 + 0.5;
                scan = pow(scan, 5.0) * _ScanlineStrength;

                float ripple = GetRipple(scenePositionWS);

                float surfaceGlow = fresnel * _FresnelIntensity * _SurfaceAlpha;
                float intersectionGlow = intersection * _IntersectionIntensity * noiseMask;
                float rippleGlow = ripple * _RippleIntensity;

                float alpha = surfaceGlow + intersectionGlow + rippleGlow;
                alpha = saturate(alpha);

                clip(alpha - 0.001);

                float energy = surfaceGlow + intersectionGlow + rippleGlow + scan * intersection;
                float3 color = _ForcefieldColor.rgb * energy;

                color += float3(1.0, 1.0, 1.0) * intersection * 0.35;
                color += float3(1.0, 1.0, 1.0) * ripple * 0.25;

                return half4(color, alpha);
            }

            ENDHLSL
        }
    }

    FallBack Off
}
