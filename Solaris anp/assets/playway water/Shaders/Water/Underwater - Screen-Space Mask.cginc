#include "UnityCG.cginc"
#include "UnityStandardCore.cginc"

struct VertexInput2
{
	float4 vertex	: POSITION;
};

struct VertexOutput
{
	float4 pos			: SV_POSITION;
#if _BOUNDED_WATER
	half4 screenPos		: TEXCOORD0;
#endif
};

VertexOutput vert(VertexInput2 vi)
{
	VertexOutput vo;

	float4 posWorld = GET_WORLD_POS(vi.vertex);

	half2 normal;
	float4 fftUV;
	float4 fftUV2;
	float3 displacement;
	TransformVertex(posWorld, normal, fftUV, fftUV2, displacement);

	vo.pos = mul(UNITY_MATRIX_VP, posWorld);
#if _BOUNDED_WATER
	vo.screenPos = ComputeScreenPos(vo.pos);
#endif

	return vo;
}

fixed4 maskFrag(VertexOutput vo) : SV_Target
{
#if _BOUNDED_WATER
	float4 addMask = tex2Dproj(_AdditiveMask, UNITY_PROJ_COORD(vo.screenPos));

	if (0 < addMask.z || 0 > addMask.y || fmod(addMask.x, _WaterId.y) < _WaterId.x)
		return 0;
#endif

	return 1.0 / 255.0;
}
