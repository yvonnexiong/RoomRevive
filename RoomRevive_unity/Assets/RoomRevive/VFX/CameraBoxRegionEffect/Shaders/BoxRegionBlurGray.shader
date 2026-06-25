// Full-screen post effect (URP) that blurs + desaturates the camera image ONLY inside a set of
// screen-space rectangles. The rectangles are the on-screen bounding boxes of 3D BoxColliders,
// fed in each frame by CameraBoxRegionEffect.cs via global shader properties.
//
// Use with URP's "Full Screen Pass Renderer Feature": create a Material from this shader, add the
// feature to your URP Renderer, assign the material, injection point = After Rendering Post Processing.
Shader "RoomRevive/Box Region Blur Gray"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "BoxRegionBlurGray"

            // Exclusions are handled by re-drawing the excluded layers SHARP on top of this blur
            // (a RenderObjects pass at AfterRendering), so no stencil/mask is needed here.

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Driven globally by CameraBoxRegionEffect.cs.
            // Each region is a convex polygon (the projected silhouette of a 3D box), up to
            // 8 vertices, CCW in UV space. Up to 4 polygons.
            #define MAX_VERTS 8
            float4 _PolyVerts[32];  // MAX_POLYS(4) * MAX_VERTS(8); xy = UV vertex
            float4 _PolyInfo[4];    // x = vertex count for this polygon
            float  _PolyCount;
            float  _BlurSize;       // tap offset in UV units
            float  _GrayAmount;     // 0..1
            float  _Softness;       // edge feather in UV units

            // 0 outside all polygons → 1 inside, with a soft edge that follows the silhouette.
            float PolyMask(float2 uv)
            {
                float m = 0.0;
                int polys = (int)_PolyCount;
                [loop] for (int p = 0; p < polys; p++)
                {
                    int vc = (int)_PolyInfo[p].x;
                    if (vc < 3) continue;
                    int baseI = p * MAX_VERTS;

                    // Signed distance to the convex polygon: min over edges of the distance to the
                    // inward half-plane. Positive (well) inside, negative outside.
                    float minD = 1e9;
                    [loop] for (int i = 0; i < vc; i++)
                    {
                        int j = (i + 1 == vc) ? 0 : i + 1;
                        float2 a = _PolyVerts[baseI + i].xy;
                        float2 b = _PolyVerts[baseI + j].xy;
                        float2 e = b - a;
                        float2 n = float2(-e.y, e.x);        // inward normal (CCW winding)
                        float  len = max(length(n), 1e-6);
                        float  d = dot(uv - a, n) / len;
                        minD = min(minD, d);
                    }
                    m = max(m, saturate(minD / max(_Softness, 1e-4)));
                }
                return saturate(m);
            }

            half3 SampleSrc(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
            }

            // Cheap separable-ish 9-tap gaussian.
            half3 Blur(float2 uv)
            {
                float2 o = _BlurSize;
                half3 c = SampleSrc(uv) * 0.227;
                c += SampleSrc(uv + float2( o.x, 0)) * 0.155;
                c += SampleSrc(uv + float2(-o.x, 0)) * 0.155;
                c += SampleSrc(uv + float2( 0, o.y)) * 0.155;
                c += SampleSrc(uv + float2( 0,-o.y)) * 0.155;
                c += SampleSrc(uv + float2( o.x, o.y)) * 0.0382;
                c += SampleSrc(uv + float2(-o.x, o.y)) * 0.0382;
                c += SampleSrc(uv + float2( o.x,-o.y)) * 0.0382;
                c += SampleSrc(uv + float2(-o.x,-o.y)) * 0.0382;
                return c;
            }

            half4 Frag (Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                // Preserve the source alpha so we don't clobber the passthrough mask: the eye buffer
                // is transparent (alpha 0) where passthrough should show. Writing alpha=1 here would
                // make the whole frame opaque and hide passthrough.
                half4 srcFull = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half3 src = srcFull.rgb;
                half  a   = srcFull.a;

                float m = (_PolyCount > 0.5) ? PolyMask(uv) : 0.0;
                if (m <= 0.001)
                    return half4(src, a);

                half3 blurred = Blur(uv);
                half  gray    = dot(blurred, half3(0.299, 0.587, 0.114));
                half3 effect  = lerp(blurred, gray.xxx, saturate(_GrayAmount));
                return half4(lerp(src, effect, m), a);
            }
            ENDHLSL
        }
    }
}
