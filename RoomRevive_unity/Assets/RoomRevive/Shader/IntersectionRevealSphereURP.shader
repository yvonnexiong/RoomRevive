Shader "XRCC/Intersection Reveal Sphere URP"
{
    Properties
    {
        _IntersectionColor ("Intersection Color", Color) = (0.45, 0.95, 1, 1)

        _IntersectionThickness ("Intersection Thickness", Float) = 0.16
        _EdgeSoftness ("Edge Softness", Float) = 0.08
        _Intensity ("Intensity", Float) = 1.6

        _ShellAlpha ("Invisible Shell Alpha", Range(0, 1)) = 0
        _FresnelPower ("Fresnel Power", Float) = 3

        _PulseSpeed ("Pulse Speed", Float) = 1.5
        _PulseStrength ("Pulse Strength", Float) = 0.18
        _NoiseScale ("Noise Scale", Float) = 6
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Intersection Reveal Sphere"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend SrcAlpha One
            ZWrite Off

            // Important:
            // ZTest Always lets the sphere compare itself against the scene depth texture.
            // If this is LEqual, the depth buffer may hide the exact intersection area.
            ZTest Always

            Cull Off

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _IntersectionColor;

                float _IntersectionThickness;
                float _EdgeSoftness;
                float _Intensity;

                float _ShellAlpha;
                float _FresnelPower;

                float _PulseSpeed;
                float _PulseStrength;
                float _NoiseScale;
                float _NoiseStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float2 uv : TEXCOORD3;
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

            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalize(normalInputs.normalWS);
                output.screenPos = ComputeScreenPos(positionInputs.positionCS);
                output.uv = input.uv;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                screenUV = UnityStereoTransformScreenSpaceTex(screenUV);

                float rawSceneDepth = SampleSceneDepth(screenUV);

                #if UNITY_REVERSED_Z
                    float sceneDepthValid = step(0.00001, rawSceneDepth);
                #else
                    float sceneDepthValid = 1.0 - step(0.99999, rawSceneDepth);
                #endif

                float sceneEyeDepth = LinearEyeDepth(rawSceneDepth, _ZBufferParams);
                float sphereEyeDepth = -TransformWorldToView(input.positionWS).z;

                float depthDelta = abs(sceneEyeDepth - sphereEyeDepth);

                float thickness = max(0.001, _IntersectionThickness);
                float softness = max(0.001, _EdgeSoftness);

                float intersectionBand = 1.0 - smoothstep(
                    thickness,
                    thickness + softness,
                    depthDelta
                );

                intersectionBand *= sceneDepthValid;

                float time = _Time.y;

                float noiseScale = max(0.001, _NoiseScale);
                float noise = ValueNoise(input.positionWS * noiseScale + time * 0.25);
                float noiseMask = lerp(1.0, 0.72 + noise * 0.56, saturate(_NoiseStrength));

                float pulse = 1.0 + sin(time * _PulseSpeed) * _PulseStrength;
                pulse = max(0.0, pulse);

                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 normalWS = normalize(input.normalWS);

                float ndotv = abs(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - saturate(ndotv), max(0.001, _FresnelPower));

                float intersectionAlpha =
                    intersectionBand *
                    noiseMask *
                    pulse *
                    _IntersectionColor.a *
                    _Intensity;

                float shellAlpha =
                    saturate(_ShellAlpha) *
                    fresnel *
                    _IntersectionColor.a;

                float finalAlpha = saturate(max(intersectionAlpha, shellAlpha));

                clip(finalAlpha - 0.001);

                float intersectionEnergy =
                    intersectionBand *
                    noiseMask *
                    pulse *
                    _Intensity *
                    (1.0 + fresnel * 0.75);

                float shellEnergy =
                    shellAlpha *
                    0.75;

                float3 color =
                    _IntersectionColor.rgb *
                    (intersectionEnergy + shellEnergy);

                color += float3(1.0, 1.0, 1.0) * intersectionBand * 0.18 * pulse;

                return half4(color, finalAlpha);
            }

            ENDHLSL
        }
    }

    FallBack Off
}