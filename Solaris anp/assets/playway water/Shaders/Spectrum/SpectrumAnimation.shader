// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "PlayWay Water/Spectrum/Water Spectrum"
{
	Properties
	{
		_MainTex ("", 2D) = "" {}
		_TileSizeLookup ("", 2D) = "" {}
		_Gravity ("", Float) = 9.81
		_PlaneSizeInv ("", Float) = 0.01
		_TargetResolution ("", Vector) = (256, 256, 256, 256)
	}
	CGINCLUDE

	#include "UnityCG.cginc"

	struct VertexInput
	{
		float4 vertex	: POSITION;
		float2 uv0		: TEXCOORD0;
	};

	struct VertexOutput
	{
		float4 pos	: SV_POSITION;
		float2 uv	: TEXCOORD0;
		float2 uv1  : TEXCOORD1;
	};

	struct PSOutput
	{
		float4 height		: SV_Target;
		float4 displacement	: SV_Target1;
	};

	struct PSOutput2
	{
		float4 height					: SV_Target;
		float4 slope					: SV_Target1;
		float4 displacement				: SV_Target2;
	};

	sampler2D _MainTex;
	sampler2D _TileSizeLookup;
	float _RenderTime;
	float _Gravity;
	float2 _TargetResolution;
	float4 _MainTex_TexelSize;

	VertexOutput vert (VertexInput vi)
	{
		VertexOutput vo;

		vo.pos = UnityObjectToClipPos(vi.vertex);
		vo.uv = (vi.uv0 - _MainTex_TexelSize.xy * 0.5) * 2.0;
		vo.uv1 = vi.uv0;

		return vo;
	}

	float2 Spectrum(float2 uv1, float2 uv2, float t)
	{
		float2 s1 = tex2D(_MainTex, uv1);
		float2 s2 = tex2D(_MainTex, uv2);
		
		float s, c;
		sincos(t, s, c);

		return float2((s1.x + s2.x) * c - (s1.y + s2.y) * s, (s1.x - s2.x) * s + (s1.y - s2.y) * c);
	}

	float2 Spectrum(float2 uv, float2 uv1, out float2 k)
	{
		float2 selector = step(float2(0.5, 0.5), uv1.xy);
		float3 tileSize = tex2D(_TileSizeLookup, selector);
		float pix2 = 6.2831853;
		float2 centeredUV = -0.5 + frac(uv + 0.5);
		k = pix2 * _TargetResolution.xy * tileSize.z * centeredUV;
		float t = _RenderTime * sqrt(_Gravity * length(k));

		float2 uv2 = tileSize.xy - uv1 + _MainTex_TexelSize.xy;
		float2 selector2 = floor(uv2.xy * 2.0);

		if (selector.x != selector2.x)
			uv2.x = uv1.x;

		if (selector.y != selector2.y)
			uv2.y = uv1.y;

		return Spectrum(uv1, uv2, t);
	}

	float4 animate (VertexOutput vo) : SV_Target0
	{
		float2 k;
		return float4(Spectrum(vo.uv, vo.uv1, k), 0, 1);
	}

	PSOutput animateDisplacements(VertexOutput vo)
	{
		float2 k;
		float2 s = Spectrum(vo.uv, vo.uv1, k);

		PSOutput po;
		po.height = float4(s, 1, 1);

		k = normalize(k);
		if (length(frac(vo.uv)) < 0.0002) k = 0.707107;

		po.displacement.x = s.y * k.x;
		po.displacement.y = -s.x * k.x;
		po.displacement.z = s.y * k.y;
		po.displacement.w = -s.x * k.y;

		return po;
	}

	PSOutput2 animatex3 (VertexOutput vo)
	{
		float2 k;
		float2 s = Spectrum(vo.uv, vo.uv1, k);

		PSOutput2 po;
		po.height = float4(s, 1, 1);
		
		po.slope.x = -s.y * k.x;
		po.slope.y = s.x * k.x;
		po.slope.z = -s.y * k.y;
		po.slope.w = s.x * k.y;

		k = normalize(k);
		if (length(frac(vo.uv)) < 0.0002) k = 0.707107;

		po.displacement.x = s.y * k.x;
		po.displacement.y = -s.x * k.x;
		po.displacement.z = s.y * k.y;
		po.displacement.w = -s.x * k.y;

		return po;
	}

	float4 animateSlope(VertexOutput vo) : SV_Target
	{
		float2 k;
		float2 s = Spectrum(vo.uv, vo.uv1, k);

		float4 slope;
		slope.x = -s.y * k.x;
		slope.y = s.x * k.x;
		slope.z = -s.y * k.y;
		slope.w = s.x * k.y;

		return slope;
	}

	float2 _WindDirection;
	float _Directionality;

	/// Converts omnidirectional spectrum to a directional one
	float4 directionalSpectrum (VertexOutput vo) : SV_Target
	{
		float3 spectrum = tex2D(_MainTex, vo.uv1.xy);
		float tileSize = tex2D(_TileSizeLookup, step(float2(0.5, 0.5), vo.uv1.xy)).z;

		float pix2 = 6.2831853;
		float2 centeredUV = -0.5 + frac(vo.uv.xy + 0.5);
		float2 k = pix2 * _TargetResolution.xy * tileSize * centeredUV;
		
		k = normalize(k);
		if (length(frac(vo.uv)) < 0.0002) k = _WindDirection.xy;

		float dp = _WindDirection.x * k.x + _WindDirection.y * k.y;
		float phi = acos(dp * 0.999);

		float directionalFactor = sqrt(1.0f + spectrum.z * cos(2.0f * phi));

		if(dp < 0)
			directionalFactor *= _Directionality;
		
		return float4(spectrum.xy * directionalFactor, 0.0, 1.0);
	}

	struct VertexOutput2
	{
		float4 pos	: SV_POSITION;
		float2 uv	: TEXCOORD0;
	};

	float _Weight;

	VertexOutput2 vertSimple(VertexInput vi)
	{
		VertexOutput2 vo;

		vo.pos = UnityObjectToClipPos(vi.vertex);
		vo.uv = vi.uv0;

		return vo;
	}

	float4 addSpectrum (VertexOutput2 vo) : SV_Target
	{
		return tex2D(_MainTex, vo.uv.xy) * _Weight;
	}

	ENDCG

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			Name "Spectrum"

			CGPROGRAM
			
			#pragma target 2.0

			#pragma vertex vert
			#pragma fragment animate

			ENDCG
		}

		Pass
		{
			Name "Animate Slope"

			CGPROGRAM

			#pragma target 2.0

			#pragma vertex vert
			#pragma fragment animateSlope

			ENDCG
		}

		Pass
		{
			Name "Spectrumx3"

			CGPROGRAM
			
			#pragma target 3.0

			#pragma vertex vert
			#pragma fragment animatex3

			ENDCG
		}

		Pass
		{
			Name "Directional Spectrum"

			CGPROGRAM
			
			#pragma target 2.0

			#pragma vertex vert
			#pragma fragment directionalSpectrum

			ENDCG
		}

		Pass
		{
			Name "Add Spectrum"

			Blend One One

			CGPROGRAM

			#pragma target 2.0

			#pragma vertex vertSimple
			#pragma fragment addSpectrum

			ENDCG
		}

		Pass
		{
			Name "Animate Displacements"

			CGPROGRAM

			#pragma target 3.0

			#pragma vertex vert
			#pragma fragment animateDisplacements

			ENDCG
		}
	}
}