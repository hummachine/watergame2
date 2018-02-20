// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "PlayWay Water/Underwater/Compose Underwater Mask"
{
	Properties
	{
		_MainTex("", 2D) = "white" {}
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
	};

	sampler2D _MainTex;

	VertexOutput vert (VertexInput vi)
	{
		VertexOutput vo;
		vo.pos = UnityObjectToClipPos(vi.vertex);
		vo.uv = vi.uv0;

		return vo;
	}

	fixed4 frag(VertexOutput vo) : SV_Target
	{
		half4 c = tex2D(_MainTex, vo.uv);
		return c.y > 900000 ? c.w : 1;
	}

	ENDCG

	SubShader
	{
		Pass
		{
			ZTest Always Cull Off ZWrite Off
			Blend Zero SrcColor
			ColorMask R

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			ENDCG
		}
	}
}