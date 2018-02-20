Shader "PlayWay Water/Spray/Spray To Foam"
{
	Properties
	{
		
	}

CGINCLUDE
	#define postGeomVert vert
	#define vertexInputType VertexOutput2

	#include "UnityCG.cginc"
	#include "UnityStandardCore.cginc"

	struct VertexOutput2
	{
		float4 pos				: SV_POSITION;
		half2 uv				: TEXCOORD0;
		half particleData		: TEXCOORD1;
	};

	float4 _LocalMapsCoords;

	VertexOutput2 vert(VertexInput vi)
	{
		VertexOutput2 vo;

		vo.pos.xy = (vi.vertex.xz + _LocalMapsCoords.xy) * _LocalMapsCoords.zz * 2.0 - 1.0;
		vo.pos.zw = float2(0.75, 1.0);
		vo.pos.y = -vo.pos.y;
		vo.uv = vi.uv0;
		vo.particleData = vi.particleData.x * 0.01;

		return vo;
	}

	half4 frag(VertexOutput2 vo) : SV_Target
	{
		float pi = 3.14159;
		//return vo.particleData * sin(vo.uv.x * pi) * sin(vo.uv.y * pi);
		return vo.particleData * max(0, sin(length(vo.uv.xy) * pi));
	}
ENDCG

	SubShader
	{
		Pass
		{
			Blend One One
			ZWrite Off ZTest Off Cull Off
			ColorMask A

			CGPROGRAM
			#pragma target 5.0

			#pragma vertex geomVert
			#pragma geometry geom
			#pragma fragment frag

			#define _FOAM_GAIN_MAP 1

			#include "ParticleGeometry.cginc"

			ENDCG
		}
	}
}
