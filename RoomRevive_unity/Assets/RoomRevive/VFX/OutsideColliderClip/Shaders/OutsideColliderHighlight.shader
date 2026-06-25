// Tints the part of a mesh that falls OUTSIDE a given volume (a BoxCollider or SphereCollider)
// with a configurable color (red by default) and makes it transparent. The part inside the
// volume renders with the normal base color/texture.
//
// The volume is fed in by the companion OutsideColliderClip.cs via global shader properties,
// so one component pointing at a collider drives every material using this shader.
//
// URP (Universal Render Pipeline). Unlit — see the header note in the C# file for a Lit upgrade.
Shader "RoomRevive/Outside Collider Highlight"
{
    Properties
    {
        [MainTexture] _BaseMap   ("Base Map", 2D)      = "white" {}
        [MainColor]   _BaseColor ("Base Color", Color) = (1,1,1,1)

        [Header(Outside Volume)]
        _OutsideColor ("Outside Color", Color)          = (1,0,0,1)
        _OutsideAlpha ("Outside Alpha (0=invisible)", Range(0,1)) = 0.25
        _Softness     ("Edge Softness", Range(0.0001,0.5)) = 0.02

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull   ("Cull", Float) = 2
        [Enum(Off,0,On,1)]                     _ZWrite ("ZWrite", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _OutsideColor;
                half   _OutsideAlpha;
                half   _Softness;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // ── Driven globally by OutsideColliderClip.cs ──
            float4x4 _ClipWorldToLocal; // maps world space → canonical volume space
            float    _ClipShape;        // 0 = box (|local| <= 0.5), 1 = sphere (length <= 0.5)
            float    _ClipEnabled;      // 0 = no clip (render normally), 1 = clip active

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = p.positionCS;
                OUT.positionWS  = p.positionWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                // Position in the volume's canonical space; >0 distance = outside.
                float3 lp = mul(_ClipWorldToLocal, float4(IN.positionWS, 1.0)).xyz;
                float d = (_ClipShape < 0.5)
                    ? max(max(abs(lp.x), abs(lp.y)), abs(lp.z)) - 0.5  // box
                    : length(lp) - 0.5;                                // sphere

                // 0 inside → 1 outside, ramped over _Softness.
                float t = (_ClipEnabled < 0.5) ? 0.0 : saturate(d / max(_Softness, 1e-4));

                half3 rgb = lerp(baseCol.rgb, _OutsideColor.rgb, t);
                half  a   = lerp(baseCol.a,   _OutsideAlpha,     t);
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
