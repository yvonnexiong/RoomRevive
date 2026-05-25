Shader "RoomRevive/HotspotPulseAdvanced"
{
    Properties
    {
        [HDR] _Color       ("Primary Rim Color", Color) = (0.96, 0.65, 0.14, 1)
        [HDR] _AccentColor ("Accent Scan Color", Color) = (0.15, 0.85, 1.00, 1)
        [HDR] _CoreColor   ("Core Pulse Color", Color) = (1.00, 0.35, 0.08, 1)

        _Alpha             ("Overall Alpha", Range(0, 2)) = 1.0
        _InnerAlpha        ("Inner Fill Alpha", Range(0, 0.5)) = 0.055

        _RimMin            ("Minimum Rim Presence", Range(0, 1)) = 0.08
        _RimIntensity      ("Rim Intensity", Range(0, 5)) = 1.35

        _PulseT            ("Pulse T - C# Driven", Range(0, 1)) = 0.0
        _PulseBoost        ("Pulse Brightness Boost", Range(0, 4)) = 1.1
        _GazeT             ("Gaze T - C# Driven", Range(0, 1)) = 0.0

        _AutoPulseAmount   ("Auto Pulse Amount", Range(0, 1)) = 0.35
        _AutoPulseSpeed    ("Auto Pulse Speed", Float) = 0.75

        _PulseRadius       ("Pulse Ring Radius", Float) = 1.15
        _PulseWidth        ("Pulse Ring Width", Range(0.005, 0.5)) = 0.08
        _PulseRingIntensity("Pulse Ring Intensity", Range(0, 5)) = 1.65

        _ScanSpeed         ("Scan Speed", Float) = 1.2
        _ScanDensity       ("Scan Density", Float) = 6.0
        _ScanWidth         ("Scan Beam Width", Range(0.002, 0.35)) = 0.045
        _ScanIntensity     ("Scan Intensity", Range(0, 5)) = 1.0
        _ScanVector        ("Scan Direction Object Space", Vector) = (0, 1, 0, 0)

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent"
            "RenderType"     = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "HotspotLite"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define RR_TAU 6.28318530718

            CBUFFER_START(UnityPerMaterial)

                float4 _Color;
                float4 _AccentColor;
                float4 _CoreColor;

                float _Alpha;
                float _InnerAlpha;

                float _RimMin;
                float _RimIntensity;

                float _PulseT;
                float _PulseBoost;
                float _GazeT;

                float _AutoPulseAmount;
                float _AutoPulseSpeed;

                float _PulseRadius;
                float _PulseWidth;
                float _PulseRingIntensity;

                float _ScanSpeed;
                float _ScanDensity;
                float _ScanWidth;
                float _ScanIntensity;
                float4 _ScanVector;

            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };

            half GetPulse()
            {
                half autoPulse = sin(_Time.y * _AutoPulseSpeed * RR_TAU) * 0.5h + 0.5h;
                autoPulse *= _AutoPulseAmount;

                return saturate(max((half)_PulseT, autoPulse));
            }

            half ThinLine(float value, float width)
            {
                float f = frac(value);
                float d = min(f, 1.0 - f);
                return 1.0h - smoothstep(0.0, max(0.0001, width), d);
            }

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

                float3 positionOS = IN.positionOS.xyz;
                float3 positionWS = TransformObjectToWorld(positionOS);

                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS  = GetWorldSpaceViewDir(positionWS);
                OUT.positionOS = positionOS;

                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                half3 normalWS = normalize((half3)IN.normalWS);
                half3 viewWS   = normalize((half3)IN.viewDirWS);

                half ndotv = saturate(dot(normalWS, viewWS));

                // Cheap rim: fixed squared rim instead of pow().
                half rim = 1.0h - ndotv;
                rim *= rim;
                rim = saturate(rim + _RimMin);
                half rimMask = rim * _RimIntensity;

                half pulse = GetPulse();
                half pulseBrightness = 1.0h + pulse * _PulseBoost;

                // Cheap pulse ring using object-space XZ radius.
                float radius = length(IN.positionOS.xz);
                float pulseCenter = _PulseT * _PulseRadius;

                half pulseRing = 1.0h - smoothstep(
                    0.0,
                    max(0.0001, _PulseWidth),
                    abs(radius - pulseCenter)
                );

                pulseRing *= saturate(_PulseT * 8.0);
                pulseRing *= 1.0h - saturate((half)_GazeT);

                half pulseMask = pulseRing * _PulseRingIntensity;

                // Cheap scan line: frac-based instead of sin/noise.
                float scanCoord = dot(IN.positionOS, _ScanVector.xyz);
                float scanPhase = scanCoord * _ScanDensity + _Time.y * _ScanSpeed;

                half scan = ThinLine(scanPhase, _ScanWidth);
                half scanMask = scan * _ScanIntensity;

                // Simple gaze fade instead of noise dissolve.
                half gazeFade = 1.0h - saturate((half)_GazeT);
                gazeFade *= gazeFade;

                half innerMask = _InnerAlpha * lerp(1.0h, 0.35h, rim);

                half alpha =
                    innerMask +
                    rimMask * 0.42h +
                    scanMask * 0.12h +
                    pulseMask * 0.25h;

                alpha *= pulseBrightness;
                alpha *= gazeFade;
                alpha *= _Alpha;

                half3 col =
                    _Color.rgb       * (rimMask + innerMask) * 0.85h +
                    _AccentColor.rgb * scanMask              * 0.65h +
                    _CoreColor.rgb   * pulseMask             * 0.85h;

                col *= pulseBrightness;

                return half4(col, saturate(alpha));
            }

            ENDHLSL
        }
    }

    FallBack Off
}