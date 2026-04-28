
Shader "XRCC/Yinan Visual Cube Sphere Intersection URP"
{
    Properties
    {
        _EffectColor ("Effect Color", Color) = (0.15, 0.9, 1, 1)

        _SphereCenterWS ("Sphere Center WS", Vector) = (0, 0, 0, 0)
        _SphereRadiusWS ("Sphere Radius WS", Float) = 1

        _BandThickness ("Band Thickness", Float) = 0.18
        _EdgeSoftness ("Edge Softness", Float) = 0.08
        _Intensity ("Intensity", Float) = 6

        _NoiseScale ("Noise Scale", Float) = 8
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.22
        _ScanlineScale ("Scanline Scale", Float) = 18
        _ScanlineStrength ("Scanline Strength", Range(0, 2)) = 0.35

        _FresnelPower ("Fresnel Power", Float) = 1.5
        _FresnelStrength ("Fresnel Strength", Float) = 1.2

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
            Name "Yinan Visual Cube Sphere Intersection"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _EffectColor;

                float4 _SphereCenterWS;
                float _SphereRadiusWS;

                float _BandThickness;
                float _EdgeSoftness;
                float _Intensity;

                float _NoiseScale;
                float _NoiseStrength;
                float _ScanlineScale;
                float _ScanlineStrength;

                float _FresnelPower;
                float _FresnelStrength;

                float _CustomTime;
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
                float2 uv : TEXCOORD2;
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

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS);

                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = normalize(normal.normalWS);
                output.uv = input.uv;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 center = _SphereCenterWS.xyz;

                float distanceToCenter = distance(input.positionWS, center);
                float shellDistance = abs(distanceToCenter - _SphereRadiusWS);

                float band = 1.0 - smoothstep(
                    _BandThickness,
                    _BandThickness + _EdgeSoftness,
                    shellDistance
                );

                clip(band - 0.002);

                float noise = ValueNoise(input.positionWS * _NoiseScale + _CustomTime * 0.35);
                float noiseMask = lerp(1.0, noise, _NoiseStrength);

                float scan = sin(distanceToCenter * _ScanlineScale - _CustomTime * 5.0) * 0.5 + 0.5;
                scan = pow(scan, 5.0) * _ScanlineStrength;

                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float ndotv = abs(dot(normalize(input.normalWS), viewDir));
                float fresnel = pow(1.0 - saturate(ndotv), _FresnelPower) * _FresnelStrength;

                float alpha = band * noiseMask * _EffectColor.a * _Intensity;
                alpha = saturate(alpha);

                float energy = band * (1.0 + scan + fresnel);
                float3 color = _EffectColor.rgb * energy * _Intensity;
                color += float3(1.0, 1.0, 1.0) * band * 0.25;

                return half4(color, alpha);
            }

            ENDHLSL
        }
    }

    FallBack Off
}
