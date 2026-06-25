// Region-desaturating variant of the package's "Gaussian Splatting/Render Splats" shader.
//
// Identical to Packages/com.worldlabs.gaussian-splatting/Shaders/RenderGaussianSplats.shader,
// except each splat's color is pushed toward grayscale when its CENTER falls inside (or outside)
// a volume — same idea as a GaussianCutout, but it recolors instead of culling.
//
// The package is NOT modified. Assign this shader to the GaussianSplatRenderer's "Render Shader"
// (m_ShaderSplats) field; the volume is fed via global properties by GaussianRegionDesaturate.cs.
Shader "Gaussian Splatting/Render Splats (Region Desaturate)"
{
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }

        Pass
        {
            ZWrite Off
            Blend OneMinusDstAlpha One
            Cull Off

CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma require compute
#pragma use_dxc

#include "UnityCG.cginc"
#include "Packages/com.worldlabs.gaussian-splatting/Shaders/GaussianSplatting.hlsl"

StructuredBuffer<uint> _OrderBuffer;

struct v2f
{
    half4 col : COLOR0;
    float2 pos : TEXCOORD0;
    float4 vertex : SV_POSITION;
};

StructuredBuffer<SplatViewData> _SplatViewData;
ByteAddressBuffer _SplatSelectedBits;
uint _SplatBitsValid;
uint _OptimizeForQuest;

// ── Region desaturation — driven globally by GaussianRegionDesaturate.cs ──
float4x4 _GSDesatWorldToLocal; // world space → canonical volume space
float    _GSDesatShape;        // 0 = box (|local| <= 0.5), 1 = sphere (length <= 0.5)
float    _GSDesatEnabled;      // 0 = off (render normally), 1 = on
float    _GSDesatInside;       // 1 = desaturate splats INSIDE the volume, 0 = OUTSIDE
float    _GSDesatAmount;       // 0..1 grayscale blend
float    _GSDesatSoftness;     // boundary feather (canonical units)

v2f vert (uint vtxID : SV_VertexID, uint instID : SV_InstanceID)
{
	v2f o = (v2f)0;
    instID = _OrderBuffer[instID];

	SplatViewData view = _SplatViewData[instID];

	float4 centerClipPos = view.pos;

	// Need to recalculate here for Quest (Why tho?)
	if (_OptimizeForQuest) {
		SplatData splat = LoadSplatData(instID);
		float3 centerWorldPos = mul(unity_ObjectToWorld, float4(splat.pos, 1)).xyz;
	    centerClipPos = mul(UNITY_MATRIX_VP, float4(centerWorldPos, 1));;
	}

	bool behindCam = centerClipPos.w <= 0;
	if (behindCam)
	{
		o.vertex = asfloat(0x7fc00000); // NaN discards the primitive
	}
	else
	{
		o.col.r = f16tof32(view.color.x >> 16);
		o.col.g = f16tof32(view.color.x);
		o.col.b = f16tof32(view.color.y >> 16);
		o.col.a = f16tof32(view.color.y);

		// Region desaturation: test the splat center against the volume.
		if (_GSDesatEnabled > 0.5)
		{
			SplatData splatD = LoadSplatData(instID);
			float3 wp = mul(unity_ObjectToWorld, float4(splatD.pos, 1)).xyz;
			float3 lp = mul(_GSDesatWorldToLocal, float4(wp, 1)).xyz;
			float d = (_GSDesatShape < 0.5)
				? max(max(abs(lp.x), abs(lp.y)), abs(lp.z)) - 0.5
				: length(lp) - 0.5;
			float soft = max(_GSDesatSoftness, 1e-4);
			float zone = (_GSDesatInside > 0.5) ? saturate(-d / soft) : saturate(d / soft);
			float gray = dot(o.col.rgb, float3(0.299, 0.587, 0.114));
			o.col.rgb = lerp(o.col.rgb, gray.xxx, zone * _GSDesatAmount);
		}

		uint idx = vtxID;
		float2 quadPos = float2(idx&1, (idx>>1)&1) * 2.0 - 1.0;
		quadPos *= 2;

		o.pos = quadPos;

		float2 deltaScreenPos = (quadPos.x * view.axis1 + quadPos.y * view.axis2) * 2 / _ScreenParams.xy;
		o.vertex = centerClipPos;
		o.vertex.xy += deltaScreenPos * centerClipPos.w;

		// is this splat selected?
		if (_SplatBitsValid)
		{
			uint wordIdx = instID / 32;
			uint bitIdx = instID & 31;
			uint selVal = _SplatSelectedBits.Load(wordIdx * 4);
			if (selVal & (1 << bitIdx))
			{
				o.col.a = -1;
			}
		}
	}
    return o;
}

half4 frag (v2f i) : SV_Target
{
	float power = -dot(i.pos, i.pos);
	half alpha = exp(power);
	if (i.col.a >= 0)
	{
		alpha = saturate(alpha * i.col.a);
	}
	else
	{
		// "selected" splat: magenta outline, increase opacity, magenta tint
		half3 selectedColor = half3(1,0,1);
		if (alpha > 7.0/255.0)
		{
			if (alpha < 10.0/255.0)
			{
				alpha = 1;
				i.col.rgb = selectedColor;
			}
			alpha = saturate(alpha + 0.3);
		}
		i.col.rgb = lerp(i.col.rgb, selectedColor, 0.5);
	}

    if (alpha < 1.0/255.0)
        discard;

    half4 res = half4(i.col.rgb * alpha, alpha);
    return res;
}

ENDCG
        }
    }
}
