// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "PlayWay Water/Utility/Jacobian"
{
	Properties
	{
		_MainTex ("", 2D) = "white" {}
	}

CGINCLUDE
	#include "UnityCG.cginc"

	struct appdata
	{
		float4 vertex : POSITION;
		float2 uv : TEXCOORD0;
	};

	struct v2f
	{
		float4 vertex : SV_POSITION;
		float2 uv : TEXCOORD0;
	};

	v2f vert(appdata v)
	{
		v2f o;
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = v.uv;
		return o;
	}

	sampler2D _MainTex;
	float _Scale;

	half4 frag(v2f vo) : SV_Target
	{
		half2 displacement = tex2D(_MainTex, vo.uv);
		half3 j = half3(ddx(displacement.x), ddy(displacement.y), ddx(displacement.y)) * _Scale;
		j.xy += 1.0;

		half jacobian = -(j.x * j.y - j.z * j.z);

		return jacobian;
	}

	struct v2f_eigen
	{
		float4 vertex	: SV_POSITION;
		half2 uv_a		: TEXCOORD0;		// right
		half2 uv_b		: TEXCOORD1;		// up
		half2 uv_c		: TEXCOORD2;		// left
		half2 uv_d		: TEXCOORD3;		// down
	};

	VertexOutput vert_eigen(VertexInput vi)
	{
		VertexOutput vo;

		half offset = 0.25;
		float2 worldPos = _Coordinates.xy + vi.uv0 * _Coordinates.zw;

		vo.pos = UnityObjectToClipPos(vi.vertex);

		SetMapsUV(worldPos + float2(offset, 0.0), /*out*/ vo.uv_a1, /*out*/ vo.uv_a2);
		SetMapsUV(worldPos + float2(0.0, offset), /*out*/ vo.uv_b1, /*out*/ vo.uv_b2);
		SetMapsUV(worldPos + float2(-offset, 0.0), /*out*/ vo.uv_c1, /*out*/ vo.uv_c2);
		SetMapsUV(worldPos + float2(0.0, -offset), /*out*/ vo.uv_d1, /*out*/ vo.uv_d2);

		return vo;
	}

	inline half2 SampleHorizontalDisplacement(half2 uv1)
	{
		half2 displacement = tex2D(_GlobalDisplacementMap, uv1.xy).xz;
		displacement += tex2D(_GlobalDisplacementMap1, uv1.zw).xz;
		displacement += tex2D(_GlobalDisplacementMap2, uv2.xy).xz;
		displacement += tex2D(_GlobalDisplacementMap3, uv2.zw).xz;

		return displacement;
	}

	half4 frag_eigen(VertexOutput vo) : SV_Target
	{
		half2 h10 = SampleHorizontalDisplacement(vo.uv_a);
		half2 h01 = SampleHorizontalDisplacement(vo.uv_b);
		half2 h20 = SampleHorizontalDisplacement(vo.uv_c);
		half2 h02 = SampleHorizontalDisplacement(vo.uv_d);

		half4 diff = half4(h20 - h10, h02 - h01) * -0.7;
		half3 j = half3(diff.x, diff.w, diff.y) * _Params.x;

		j.xy += 1.0;

		half2 eigenvalue = ((j.x + j.y) + half2(1, -1) * sqrt(pow(j.x - j.y, 2) + 4.0 * j.z * j.z)) * 0.5;
		half2 q = (eigenvalue.xy - j.xx) / (j.z == 0 ? 0.00001 : j.z);
		half4 eigenvector = half4(1.0, q.x, 1.0, q.y);

		return eigen
	}
ENDCG

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		// jacobian pass
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#pragma target 3.0
			ENDCG
		}

		// eigenvectors pass
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment eigenvectors
			
			#pragma target 2.0
			ENDCG
		}
	}
}
