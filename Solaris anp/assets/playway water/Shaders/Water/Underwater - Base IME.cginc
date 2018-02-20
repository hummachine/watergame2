// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

	
	struct VertexInput
	{
		float4 vertex	: POSITION;
		float2 uv0		: TEXCOORD0;
	};

	struct VertexOutput
	{
		float4 pos	: SV_POSITION;
		float2 uv	: TEXCOORD0;
		float2 uv1	: TEXCOORD1;
	};

	sampler2D _MainTex;
	half2 _MainTex_TexelSize;
	float _Offset;
	half _DistortionIntensity;
	float4x4 UNITY_MATRIX_VP_INVERSE;

	sampler2D _DistortionTex;

	#define _VERTICAL_OFFSET _SurfaceOffset.y
	#define _VOLUMETRIC_LIGHTING 1
	#define _UNDERWATER_EFFECT 1

	#include "UnityCG.cginc"
	#include "WaterLib.cginc"

	VertexOutput vert (VertexInput vi)
	{
		VertexOutput vo;

		vo.pos = UnityObjectToClipPos(vi.vertex);
		vo.uv = vi.uv0;
		vo.uv1 = vi.uv0;

#if UNITY_UV_STARTS_AT_TOP
		if (_MainTex_TexelSize.y < 0)
			vo.uv1.y = 1.0 - vo.uv1.y;
#endif

		return vo;
	}

	fixed4 forward(VertexOutput vo)
	{
#if UNITY_UV_STARTS_AT_TOP
		vo.uv.y -= 0.04;
#else
		vo.uv.y += 0.04;
#endif

		return tex2D(_MainTex, vo.uv).r;
	}

	fixed4 PropagateMask (VertexOutput vo) : SV_Target
	{
		fixed2 c = tex2D(_MainTex, vo.uv).rg;

#if UNITY_UV_STARTS_AT_TOP
		vo.uv.y -= _Offset;
#else
		vo.uv.y += _Offset;
#endif

		c.r += forward(vo);
		c.r += forward(vo);
		c.r += forward(vo);

		return fixed4(c, 0, 1);
	}

	fixed4 FinishMask (VertexOutput vo) : SV_Target
	{
		fixed2 c = tex2D(_MainTex, vo.uv).rg;

		return fixed4(c.r - c.g, 0, 0, 1);
	}

	half4 ime (VertexOutput vo) : SV_Target
	{
		fixed mask = tex2D(_UnderwaterMask, vo.uv1);
		half4 color = tex2D(_MainTex, vo.uv);
		half4 screenPos = half4(vo.uv1 * 2 - 1, 0, 1);

		half4 pixelWorldSpacePos = mul(UNITY_MATRIX_VP_INVERSE, screenPos);
		pixelWorldSpacePos.xyz /= pixelWorldSpacePos.w;

		half3 ray = pixelWorldSpacePos - _WorldSpaceCameraPos;
		ray.xyz = normalize(ray.xyz) * half3(3, -3, 3);

		float depth = tex2D(_CameraDepthTexture, vo.uv1);
		depth = LinearEyeDepth(depth);

		half3 depthColor = ComputeDepthColor(pixelWorldSpacePos, ray.xyz, half3(0, 1, 0), half3(1, 1, 1), depth)/* * (1.0 - exp(-depth * 0.01))*/;

		return half4(lerp(color.rgb, depthColor, mask * (1.0 - exp(-_AbsorptionColor * depth))), 1);
	}

	half4 ime2 (VertexOutput vo) : SV_Target
	{
		fixed mask = tex2D(_UnderwaterMask, vo.uv1);

		half2 distortion = tex2D(_DistortionTex, vo.uv).xy - 0.75;
		vo.uv += distortion * mask * _DistortionIntensity;

		return tex2D(_MainTex, vo.uv);
	}

	//
	// Mask IME
	//

	struct VertexOutput2
	{
		float4 pos			: SV_POSITION;
		half2 uv0			: TEXCOORD0;
		half2 uv1			: TEXCOORD1;
		half2 uv2			: TEXCOORD2;
	};

	VertexOutput2 vertMask(VertexInput vi)
	{
		VertexOutput2 vo;

		vo.pos = UnityObjectToClipPos(vi.vertex);
		vo.uv0.xy = vi.uv0;
		vo.uv1.xy = vi.uv0 - half2(_MainTex_TexelSize.xy * 2);
		vo.uv2.xy = vi.uv0 + half2(_MainTex_TexelSize.xy * 2);

		return vo;
	}

	fixed4 fragMask(VertexOutput2 vo) : SV_Target
	{
		fixed3 mask;
		mask.x = tex2D(_MainTex, vo.uv0);
		mask.y = tex2D(_MainTex, vo.uv1);
		mask.z = tex2D(_MainTex, vo.uv2);

		mask = mask * 255 % 2;

		return dot(mask, 1) < 1.5 ? 0 : 1;
	}