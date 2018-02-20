#include "UnityCG.cginc"

struct appdata
{
	float4 vertex		: POSITION;
	float2 uv			: TEXCOORD0;
	float4 tangent		: TANGENT;
};

struct v2f
{
	float4 vertex		: SV_POSITION;
	half4 uv			: TEXCOORD0;
	half amplitude		: TEXCOORD1;
	half4 dir			: TEXCOORD2;
	half k				: TEXCOORD3;
	half2 cosUV			: TEXCOORD4;
	half shoaling		: TEXCOORD5;
};

struct PsOutput
{
	float4 displacement	: SV_Target0;
	float4 slope		: SV_Target1;
	float4 utility		: SV_Target2;
};

struct ParticleData
{
	float3 position;
	float3 velocity;
	float2 lifetime;
	float offset;
	float maxIntensity;
};

sampler2D _MainTex;
float4 _MainTex_TexelSize;
float4 _LocalMapsCoords;
float _WaterScale;

//AppendStructuredBuffer<ParticleData> particles : register(u2);

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

v2f vert(appdata vi)
{
	v2f vo;

	float pi = 3.14159;
	float2 forward = vi.tangent.xy * _LocalMapsCoords.z/* * _WaterScale*/;
	float2 right = float2(forward.y, -forward.x);

	vi.vertex.xy = (vi.vertex.xy + _LocalMapsCoords.xy) * _LocalMapsCoords.zz * 2.0 - 1.0;
	vo.vertex = half4(vi.vertex.xy + forward * (vi.uv.y - 0.5) + right * (vi.uv.x - 0.5), 0, 1);
	vo.vertex.y = -vo.vertex.y;
	vo.uv = half4(vi.uv * pi, vo.vertex.xy);
	vo.cosUV = vi.uv * pi - tanh(vi.tangent.z * 40) * 0.5;// *0.7;
	vo.amplitude = min(vi.vertex.z, 0.165 / vi.tangent.w);
	vo.dir = half4(normalize(vi.tangent.xy), vi.tangent.xy);
	vo.k = vi.tangent.w;
	vo.shoaling = vi.tangent.z * 40;
	return vo;
}

PsOutput frag(v2f vo)
{
	half2 s, c;
	s = sin(vo.uv.xy);
	c = cos(vo.cosUV.xy);
	//sincos(vo.uv.xy, s, c);

	half fade = max(0, 1.0 - pow(max(abs(vo.uv.z), abs(vo.uv.w)), 4));
	half box = s.x * s.y;

	half height = box * (s.x - sin(vo.uv.x * 2 - 3.14159 * 0.5) * 0.9) * vo.amplitude;						// * s.x ensures that neighbouring waves will sum to 1 (sin^2(x) + cos^2(x) = 1)
	half2 displacement = box * s.x * c.y * vo.dir.xy * vo.amplitude;
	half2 slope = displacement * vo.k * 0.45;
	half foam = max(0, box * s.x * (s.y - 0.7) * 0.5) * 0.5 * vo.shoaling * vo.amplitude / vo.k;

/*	half r = random(vo.uv.zw);

#if SHADER_TARGET >= 50
	[branch]
	if (foam > 0.2 && r > 0.05)
	{
		float2 worldPos = vo.uv.zw * _LocalMapsCoords.ww - _LocalMapsCoords.xy;
		//worldPos += (random(displacement.yx) - 0.5) * _MainTex_TexelSize.xy * _LocalMapsCoords.ww * 10;

		half spawnRate = foam + 2.0;
		half intensity = log(spawnRate + 1) * (0.25 + halfGauss(displacement.yx) * 0.75);

		ParticleData particle;
		particle.position = float3(worldPos.x, displacement.y + 0.5, worldPos.y);
		particle.velocity.xz = spawnRate * vo.dir.xy * 0.1;
		particle.velocity.y = spawnRate;

		half paramsw = 1.0;

		particle.lifetime = 2.0 * intensity * paramsw;
		particle.offset = r * 2;
		particle.maxIntensity = saturate(intensity) * paramsw;
		particles.Append(particle);
	}
#endif*/

	PsOutput po;
	po.displacement = half4(displacement.x, height, displacement.y, foam * 1) * fade;
	po.slope = half4(-slope.x, -slope.y, 0, 0) * fade;

	half t = saturate(vo.cosUV.y - 1.95) * saturate(3.0 - vo.cosUV.y);
	po.utility = half4(foam * 100 * vo.dir.xy * t, 0, 0) * fade;

	return po;
}