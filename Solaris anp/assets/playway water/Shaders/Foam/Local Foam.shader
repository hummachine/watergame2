// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "PlayWay Water/Foam/Local"
{
	Properties
	{
		_MainTex ("", 2D) = "" {}
		_FoamParameters ("", Vector) = (0, 0, 0, 0)
	}

	CGINCLUDE
	
	#include "UnityCG.cginc"

	struct VertexInput2
	{
		float4 vertex	: POSITION;
		float2 uv0		: TEXCOORD0;
	};

	struct VertexOutput
	{
		float4 pos				: SV_POSITION;
		half2 uv				: TEXCOORD0;		// center
		half2 uv0				: TEXCOORD1;		// right
		half2 uv1				: TEXCOORD2;		// up
		half2 uv2				: TEXCOORD3;		// left
		half2 uv3				: TEXCOORD4;		// down
		half2 uvPreviousFrame	: TEXCOORD5;
		half4 uvFull			: TEXCOORD6;			// -1 to 1
		half2 uvPreviousFrame2	: TEXCOORD7;
	};
	
	half4 _DistortionMapCoords;

	sampler2D _SlopeMapPrevious;			// previous foam
	sampler2D _DisplacementMap;
	sampler2D _TotalDisplacementMap;

	half4	_SampleDir1;
	half2	_DeltaPosition;
	half4	_FoamParameters;		// x = intensity, y = horizonal displacement scale, z = power, w = fading factor
	half4	_TotalDisplacementMap_TexelSize;
	float4	_LocalMapsCoords;
	float4	_LocalMapsCoordsPrevious;
	float4	_WaterTileSize;
	float2	_Offset;

	sampler2D	_GlobalDisplacementMap;
	sampler2D	_GlobalDisplacementMap1;
	sampler2D	_GlobalDisplacementMap2;
	sampler2D	_GlobalDisplacementMap3;
	float		_DisplacementsScale;

	VertexOutput vert (VertexInput2 vi)
	{
		VertexOutput vo;

		float offset = 0.2 * _LocalMapsCoords.z;			// 0.25m offset

		vo.pos = UnityObjectToClipPos(vi.vertex);
		vo.uv = vi.uv0;// -_SampleDir1.zw * 0.000002;		// wind
		vo.uv0 = vi.uv0 + float2(offset, 0.0);
		vo.uv1 = vi.uv0 + float2(0.0, offset);
		vo.uv2 = vi.uv0 + float2(-offset, 0.0);
		vo.uv3 = vi.uv0 + float2(0.0, -offset);
		vo.uvPreviousFrame = ((vi.uv0 / _LocalMapsCoords.zz) - _LocalMapsCoords.xy + _LocalMapsCoordsPrevious.xy) * _LocalMapsCoordsPrevious.zz;
		vo.uvPreviousFrame2 = ((vi.uv0 / _LocalMapsCoords.zz) - _LocalMapsCoords.xy + _Offset * 100 + _LocalMapsCoordsPrevious.xy) * _LocalMapsCoordsPrevious.zz;
		vo.uvFull.xy = vi.uv0 * 2 - 1;
		vo.uvFull.zw = vo.uvPreviousFrame * 2 - 1;
		return vo;
	}

	inline half2 SampleDisplacement(float2 worldPos)
	{
		float4 fftUV = worldPos.xyxy / _WaterTileSize.xxyy;
		float4 fftUV2 = worldPos.xyxy / _WaterTileSize.zzww;

		half2 displacement = tex2D(_GlobalDisplacementMap, fftUV.xy).xz;
		displacement += tex2D(_GlobalDisplacementMap1, fftUV.zw).xz;
		displacement += tex2D(_GlobalDisplacementMap2, fftUV2.xy).xz;
#if !_WAVES_ALIGN || SHADER_TARGET >= 40
		displacement += tex2D(_GlobalDisplacementMap3, fftUV2.zw).xz;
#endif

		return displacement;
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

	half4 frag (VertexOutput vo) : SV_Target
	{
		half4 prev = tex2D(_SlopeMapPrevious, vo.uvPreviousFrame);
		half foam = prev.b * 0.8 + tex2D(_SlopeMapPrevious, vo.uvPreviousFrame2).b * 0.2;// * 0.99;// *_FoamParameters.w;

		half foamFade = max(0.0, (prev.a - 0.06) / 0.94);

		if (prev.a <= 0.1)
			foam += 0.001 * (1.0 - prev.a / 0.1);
		
		foam *= lerp(1.0 - foam * foam * 0.001, _FoamParameters.w, foamFade * foamFade);

		//float2 worldPos = vo.uv / _LocalMapsCoords.zz - _LocalMapsCoords.xy;
		//half2 displacement = SampleDisplacement(worldPos);

		half shoreMask = tex2D(_SlopeMapPrevious, vo.uvPreviousFrame).a;
		vo.uv += tex2D(_TotalDisplacementMap, vo.uv).xz * _LocalMapsCoords.zz;

		half foamGain = tex2D(_DisplacementMap, vo.uv).a * unity_DeltaTime.x;			// transform foam generated in this frame (contained in _DisplacementMap alpha channel) from final space to not derformed water space
		foamGain += ComputeFoamGain(vo, _TotalDisplacementMap, _FoamParameters.y) * _FoamParameters.x * shoreMask;
		
		foam += foamGain;

		float fade = 1.0 - pow(min(1.0, length(vo.uvFull.xy)), 8);

		//float previousFade = 1.0 - pow(min(1.0, length(vo.uvFull.zw)), 8);
		//foam += 6 * (1.0 - prev.a / 0.2) * max(0, fade * fade - previousFade * previousFade);
		foam = lerp((1.0 - prev.a / 0.2), foam, fade);

		return half4(0, 0, foam * fade, 0);
	}

	half4 fragInit(VertexOutput vo) : SV_Target
	{
		half4 prev = tex2D(_SlopeMapPrevious, vo.uvPreviousFrame);
		return half4(0, 0, (1.0 - prev.a / 0.2) * 0.15, 0);
	}

	ENDCG

	SubShader
	{
		Pass
		{
			ZTest Always Cull Off ZWrite Off
			ColorMask B

			CGPROGRAM
			
			#pragma target 2.0

			#pragma vertex vert
			#pragma fragment frag

			ENDCG
		}

		Pass
		{
			ZTest Always Cull Off ZWrite Off
			ColorMask B

			CGPROGRAM
			
			#pragma target 2.0

			#pragma vertex vert
			#pragma fragment fragInit

			ENDCG
		}
	}
}
