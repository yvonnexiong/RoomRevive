
Shader "XRCC/Sphere Intersection Demo Surface URP"
{
    Properties
    {
        _EffectColor ("Effect Color", Color) = (0.25, 0.95, 1, 1)
        _SphereCenterWS ("Sphere Center WS", Vector) = (0, 0, 0, 0)
        _SphereRadiusWS ("Sphere Radius WS", Float) = 1
        _BandThickness ("Band Thickness", Float) = 0.14
        _EdgeSoftness ("Edge Softness", Float) = 0.08
        _Intensity ("Intensity", Float) = 4.5

        _CustomTime ("Custom Time", Float) = 0
        _NoiseScale ("Noise Scale", Float) = 8
        _NoiseStrength ("Noise Strength", Float) = 0.28
        _GridScale ("Grid Scale", Float) = 18
        _GridStrength ("Grid Strength", Float) = 0.45
        _RimPower ("Rim Power", Float) = 1.5
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
            Name "XRCC Sphere Surface Contact"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -8, -8

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _EffectColor;
                float4 _SphereCenterWS;
                float _SphereRadiusWS;
                float _BandThickness;
                float _EdgeSoftness;
                float _Intensity;

                float _CustomTime;
                float _NoiseScale;
                float _NoiseStrength;
                float _GridScale;
                float _GridStrength;
                float _RimPower;
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

            float GridMask(float2 uv, float scale)
            {
                float2 gridUV = abs(frac(uv * scale) - 0.5);
                float line = 1.0 - smoothstep(0.0, 0.035, min(gridUV.x, gridUV.y));
                return line;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
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

                float innerSpark = 1.0 - smoothstep(
                    _BandThickness * 0.18,
                    _BandThickness * 0.55,
                    shellDistance
                );

                float noise = ValueNoise(input.positionWS * _NoiseScale + _CustomTime * 0.45);
                float noiseMask = lerp(1.0, noise, _NoiseStrength);

                float scan = sin((distanceToCenter * 22.0) - (_CustomTime * 8.0)) * 0.5 + 0.5;
                scan = pow(scan, 4.0);

                float grid = GridMask(input.uv, _GridScale) * _GridStrength;

                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(1.0 - saturate(dot(viewDir, normalize(input.normalWS))), _RimPower);

                float alpha = band;
                alpha *= noiseMask;
                alpha *= 1.0 + scan * 0.35;
                alpha *= _EffectColor.a * _Intensity;
                alpha = saturate(alpha);

                clip(alpha - 0.005);

                float energy = band * 1.4 + innerSpark * 2.4 + fresnel * 1.2 + grid * band;
                float3 color = _EffectColor.rgb * energy;

                color += _EffectColor.rgb * scan * band * 0.65;
                color += float3(1.0, 1.0, 1.0) * innerSpark * 0.45;

                return half4(color, alpha);
            }

            ENDHLSL
        }
    }

    FallBack Off
}
