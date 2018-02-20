// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "PlayWay Water/Foam/Global"
{
	Properties
	{
		_MainTex ("", 2D) = "black" {}
		_FoamParameters ("", Vector) = (0, 0, 0, 0)
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
		half2 uv		: TEXCOORD0;		// center
		half2 uv0		: TEXCOORD1;		// right
		half2 uv1		: TEXCOORD2;		// up
		half2 uv2		: TEXCOORD3;		// left
		half2 uv3		: TEXCOORD4;		// down
	};

	sampler2D _MainTex;
	sampler2D _DisplacementMap0;
	sampler2D _DisplacementMap1;
	sampler2D _DisplacementMap2;
	sampler2D _DisplacementMap3;
	half2 _MainTex_TexelSize;

	half4 _SampleDir1;
	half4 _FoamParameters;		// x = intensity, y = horizonal displacement scale, z = power, w = fading factor
	half4 _FoamIntensity;

	VertexOutput vert (VertexInput vi)
	{
		VertexOutput vo;

		float offset = _MainTex_TexelSize.x;

		vo.pos = UnityObjectToClipPos(vi.vertex);

		vo.uv = half2(vi.uv0 - _SampleDir1.zw * 0.000002);
		vo.uv0 = half2(vi.uv0 + half2(offset, 0.0));
		vo.uv1 = half2(vi.uv0 + half2(0.0, offset));
		vo.uv2 = half2(vi.uv0 + half2(-offset, 0.0));
		vo.uv3 = half2(vi.uv0 + half2(0.0, -offset));

		return vo;
	}

	inline half ComputeFoamGain(VertexOutput vo, sampler2D displacementMap, half intensity)
	{
		half2 h10 = tex2D(displacementMap, vo.uv0).xz;
		half2 h01 = tex2D(displacementMap, vo.uv1).xz;
		half2 h20 = tex2D(displacementMap, vo.uv2).xz;
		half2 h02 = tex2D(displacementMap, vo.uv3).xz;

		half4 diff = half4(h20 - h10, h02 - h01) * -0.7;

		half3 j = half3(diff.x, diff.w, diff.y) * intensity;
		j.xy += 1.0;

		half jacobian = -(j.x * j.y - j.z * j.z);
		half gain = max(0.0, jacobian + 0.94);

		return gain;
	}

	half4 frag(VertexOutput vo) : SV_Target
	{
		half4 foam = tex2D(_MainTex, vo.uv) * _FoamParameters.w;

		half4 gain;
		gain.x = ComputeFoamGain(vo, _DisplacementMap0, _FoamIntensity.x);
		gain.y = ComputeFoamGain(vo, _DisplacementMap1, _FoamIntensity.y);
		gain.z = ComputeFoamGain(vo, _DisplacementMap2, _FoamIntensity.z);
		gain.w = ComputeFoamGain(vo, _DisplacementMap3, _FoamIntensity.w);
		gain *= 6 * _FoamParameters.x;

		return foam + gain;
	}

	ENDCG

	SubShader
	{
		Pass
		{
			ZTest Always Cull Off ZWrite Off

			CGPROGRAM
			
			#pragma target 2.0

			#pragma vertex vert
			#pragma fragment frag

			ENDCG
		}
	}
}