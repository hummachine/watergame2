// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "PlayWay Water/Spray/Generator (Tiles)"
{
	Properties
	{
		_Lambda("", Float) = 1
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
		float4 pos		: SV_POSITION;
		half4 uv_a1		: TEXCOORD0;		// right
		half4 uv_a2		: TEXCOORD1;
		half4 uv_b1		: TEXCOORD2;		// up
		half4 uv_b2		: TEXCOORD3;
		half4 uv_c1		: TEXCOORD4;		// left
		half4 uv_c2		: TEXCOORD5;
		half4 uv_d1		: TEXCOORD6;		// down
		half4 uv_d2		: TEXCOORD7;
		half2 uv_local	: TEXCOORD8;
	};

	struct ParticleData
	{
		float3 position;
		float3 velocity;
		float2 lifetime;
		float offset;
		float maxIntensity;
	};

	half4 _Params;			// x - lambda, y - spawn rate, z - horizontal displacement scale / tile size, w - scale
	half4 _Coordinates;

	sampler2D	_GlobalDisplacementMap;
	sampler2D	_GlobalDisplacementMap1;
	sampler2D	_GlobalDisplacementMap2;
	sampler2D	_GlobalDisplacementMap3;
	sampler2D	_LocalSlopeMap;
	float4		_WaterTileSize;
	float2		_SurfaceOffset;
	float4		_LocalMapsCoords;

	AppendStructuredBuffer<ParticleData> particles : register(u1);

	inline void SetMapsUV(float2 worldPos, out half4 uv1, out half4 uv2)
	{
		uv1 = worldPos.xyxy / _WaterTileSize.xxyy;
		uv2 = worldPos.xyxy / _WaterTileSize.zzww;
	}

	VertexOutput vert (VertexInput vi)
	{
		VertexOutput vo;

		half offset = 0.25;
		float2 worldPos = _Coordinates.xy + vi.uv0 * _Coordinates.zw;

		vo.pos = UnityObjectToClipPos(vi.vertex);

		SetMapsUV(worldPos + float2(offset, 0.0), /*out*/ vo.uv_a1, /*out*/ vo.uv_a2);
		SetMapsUV(worldPos + float2(0.0, offset), /*out*/ vo.uv_b1, /*out*/ vo.uv_b2);
		SetMapsUV(worldPos + float2(-offset, 0.0), /*out*/ vo.uv_c1, /*out*/ vo.uv_c2);
		SetMapsUV(worldPos + float2(0.0, -offset), /*out*/ vo.uv_d1, /*out*/ vo.uv_d2);
		vo.uv_local = (worldPos + _LocalMapsCoords.xy) * _LocalMapsCoords.zz;

		return vo;
	}

	inline float random(float2 p)
	{
		float2 r = float2(23.14069263277926, 2.665144142690225);
		return frac(cos(dot(p, r)) * 123.0);
	}

	inline float gauss(float2 p)
	{
		return sqrt(-2.0f * log(random(p))) * sin(3.14159 * 2.0 * random(p * -0.3241241));
	}

	inline float halfGauss(float2 p)
	{
		return abs(sqrt(-2.0f * log(random(p))) * sin(3.14159 * 2.0 * random(p * -0.3241241)));
	}

	inline half2 SampleHorizontalDisplacement(half4 uv1, half4 uv2)
	{
		half2 displacement = tex2D(_GlobalDisplacementMap, uv1.xy).xz;
		displacement += tex2D(_GlobalDisplacementMap1, uv1.zw).xz;
		displacement += tex2D(_GlobalDisplacementMap2, uv2.xy).xz;
		displacement += tex2D(_GlobalDisplacementMap3, uv2.zw).xz;

		return displacement;
	}

	inline half3 SampleFullDisplacement(half4 uv1, half4 uv2)
	{
		half3 displacement = tex2Dlod(_GlobalDisplacementMap, half4(uv1.xy, 0, 0));
		displacement += tex2Dlod(_GlobalDisplacementMap1, half4(uv1.zw, 0, 0));
		displacement += tex2Dlod(_GlobalDisplacementMap2, half4(uv2.xy, 0, 0));
		displacement += tex2Dlod(_GlobalDisplacementMap3, half4(uv2.zw, 0, 0));

		return displacement;
	}

	fixed4 frag(VertexOutput vo) : SV_Target
	{
		half2 h10 = SampleHorizontalDisplacement(vo.uv_a1, vo.uv_a2);
		half2 h01 = SampleHorizontalDisplacement(vo.uv_b1, vo.uv_b2);
		half2 h20 = SampleHorizontalDisplacement(vo.uv_c1, vo.uv_c2);
		half2 h02 = SampleHorizontalDisplacement(vo.uv_d1, vo.uv_d2);

		half4 diff = half4(h20 - h10, h02 - h01) * -0.7 * tex2D(_LocalSlopeMap, vo.uv_local).a;
		half3 j = half3(diff.x, diff.w, diff.y) * _Params.x;

		j.xy += 1.0;

		half2 eigenvalue = ((j.x + j.y) + half2(1, -1) * sqrt(pow(j.x - j.y, 2) + 4.0 * j.z * j.z)) * 0.5;
		half2 q = (eigenvalue.xy - j.xx) / (j.z == 0 ? 0.00001 : j.z);
		half4 eigenvector = half4(1.0, q.x, 1.0, q.y);

		half spawnRate = 0.94 - eigenvalue.y;
		half r = random(h10);

		[branch]
		if( spawnRate > 0 && r > _Params.y)
		{
			half3 displacement = SampleFullDisplacement(half4(vo.uv_b1.x, vo.uv_a1.y, vo.uv_b1.z, vo.uv_a1.w), half4(vo.uv_b2.x, vo.uv_a2.y, vo.uv_b2.z, vo.uv_a2.w));
			float2 worldPos = float2(vo.uv_b1.x, vo.uv_a1.y) * _WaterTileSize.xx + displacement.xz;

			spawnRate += 2.0;
			half intensity = log(spawnRate + 1) * (0.25 + halfGauss(displacement.zx) * 0.75);

			ParticleData particle;
			particle.position = float3(worldPos.x + _SurfaceOffset.x, displacement.y - 0.2, worldPos.y + _SurfaceOffset.y);
			particle.velocity.xz = spawnRate * normalize(eigenvector.zw) * 0.1;
			particle.velocity.y = spawnRate;

			particle.lifetime = 2.0 * intensity * _Params.w;
			particle.offset = r * 2;
			particle.maxIntensity = saturate(intensity) * _Params.w;
			particles.Append(particle);
		}

		return 0;
	}

	ENDCG

	SubShader
	{
		Pass
		{
			ZTest Always Cull Off ZWrite Off

			CGPROGRAM
			
			#pragma target 5.0

			#pragma vertex vert
			#pragma fragment frag

			ENDCG
		}
	}
}