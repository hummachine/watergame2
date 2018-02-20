// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

#include "../Utility/NoiseLib.cginc"

#ifndef _WAVES_GERSTNER_COUNT
	#if SHADER_TARGET >= 50
		#define _WAVES_GERSTNER_COUNT 20
	#elif SHADER_TARGET == 30
		#define _WAVES_GERSTNER_COUNT 20
	#else
		#define _WAVES_GERSTNER_COUNT 12
	#endif
#endif

sampler2D	_GlobalDisplacementMap;
sampler2D	_GlobalDisplacementMap1;
sampler2D	_GlobalDisplacementMap2;
sampler2D	_GlobalDisplacementMap3;
float		_DisplacementsScale;

sampler2D	_LocalDisplacementMap;
sampler2D	_LocalSlopeMap;
sampler2D	_TotalDisplacementMap;

float4		_LocalMapsCoords;
float		_DetailFadeFactor;
float4		_WaterTileSize;
float4		_WaterTileOffsets;
half3		_WaterTileSizeScales;
float4		_SurfaceOffset;

sampler2D	_DisplacedHeightMaps;

half2		_GerstnerOrigin;
half4		_GrAmp[5];
half4		_GrFrq[5];
half4		_GrOff[5];
half4		_GrAB[5];
half4		_GrCD[5];

inline void Gerstner(float2 vertex, half4 amplitudes, half4 k, half4 offset, half4 dirAB, half4 dirCD, half t, inout half3 displacement, inout half2 normal)
{
	half4 dp = k.xyzw * half4(dot(dirAB.xy, vertex), dot(dirAB.zw, vertex), dot(dirCD.xy, vertex), dot(dirCD.zw, vertex));

	half4 c, s;
	sincos(dp + offset, s, c);

	// vertical displacement
	displacement.y += dot(s, amplitudes);

	// horizontal displacement
	half4 ab = amplitudes.xxyy * dirAB.xyzw;
	half4 cd = amplitudes.zzww * dirCD.xyzw;
	displacement.x += dot(c, half4(ab.xz, cd.xz));
	displacement.z += dot(c, half4(ab.yw, cd.yw));

	// normal
	ab *= k.xxyy;
	cd *= k.zzww;

	normal.xy += half2(
		dot(c, half4(ab.xz, cd.xz)),
		dot(c, half4(ab.yw, cd.yw))
	);
}

inline void ComputeEffectsMask(float3 posWorld, out half4 mask, out half4 totalMask)
{
#if SHADER_TARGET >= 30
	half3 w = (length(_WorldSpaceCameraPos.xyz - posWorld.xyz) - _WaterTileSize.xyz) / (_WaterTileSize.xyz * _DetailFadeFactor);
	w = saturate(w);

	mask.xyz = 1.0 - w;
	totalMask = half4(mask.xyz, 1.0);

	half sum = 1.0 - mask.y;
	mask.x *= sum;

	sum -= mask.x;
	mask.z *= sum;

	sum -= mask.z;
	mask.w = sum;
#else
	mask = 1;
	totalMask = 1;
#endif
}

inline half4 GetOcclusionDir(half3 partialDir)
{
	return half4(partialDir.xyz, 1.0 - dot(partialDir, half3(1, 1, 1)));
}

inline half4 approxTanh(half4 x)
{
	return x / sqrt(1.0 + x * x);
}

inline float GetVertexShaderDisplacedHeight4(float2 pos, float3 lod, half3 mask)
{
	float height = 0.0;

#if _WAVES_FFT && SHADER_TARGET >= 30
	float4 uv1 = pos.xyxy / _WaterTileSize.xxyy;
	float4 uv2 = pos.xyxy / _WaterTileSize.zzww;

	height += dot(half4(
		tex2Dlod(_DisplacedHeightMaps, float4(uv1.xy, 0, lod.x)).r,
		tex2Dlod(_DisplacedHeightMaps, float4(uv1.zw, 0, lod.y)).g,
		tex2Dlod(_DisplacedHeightMaps, float4(uv2.xy, 0, lod.z)).b,
		tex2Dlod(_DisplacedHeightMaps, float4(uv2.zw, 0, 0)).a
		), half4(mask.xyz, 1));
#endif

	return height;
}

inline void TransformVertex(inout float4 posWorld, out half2 normal, out float4 fftUV, out float4 fftUV2, out float3 totalDisplacement)
{
	float2 samplePos = posWorld.xz + _SurfaceOffset.xz;

	totalDisplacement = float3(0, 0, 0);
	normal = half2(0, 0);

#if _WAVES_GERSTNER && !_DISPLACED_VOLUME			// putting it there solves temporary registers shortage problem on SM 2.0
	float2 samplePosGerstner = -samplePos;

	for (int i = 0; i < (_WAVES_GERSTNER_COUNT / 4); ++i)
		Gerstner(samplePosGerstner, _GrAmp[i], _GrFrq[i], _GrOff[i], _GrAB[i], _GrCD[i], _Time.y, /*out*/ totalDisplacement, /*out*/ normal);

	totalDisplacement.xz *= -_DisplacementsScale;
#endif

	fftUV = samplePos.xyxy / _WaterTileSize.xxyy;
	fftUV2 = samplePos.xyxy / _WaterTileSize.zzww;

#if _DISPLACED_VOLUME
	return;
#endif

	#if _WAVES_FFT
		half4 mask, totalMask;
		ComputeEffectsMask(posWorld.xyz, /*out*/ mask, /*out*/ totalMask);

		float3 lod = 1.0 / totalMask.xyz - 1.0;

		float3 displacement = tex2Dlod(_GlobalDisplacementMap, float4(fftUV.xy, 0, lod.x)).xyz * totalMask.x;
		displacement += tex2Dlod(_GlobalDisplacementMap1, float4(fftUV.zw, 0, lod.y)).xyz * totalMask.y;
	#if !defined(_WATER_OVERLAYS) || SHADER_TARGET >= 40
		displacement += tex2Dlod(_GlobalDisplacementMap2, float4(fftUV2.xy, 0, lod.z)).xyz * totalMask.z;
	#if !defined(_WAVES_ALIGN) || SHADER_TARGET >= 40
		displacement += tex2Dlod(_GlobalDisplacementMap3, float4(fftUV2.zw, 0, 0)).xyz;
	#endif
	#endif

	#if _WAVES_ALIGN
		displacement.y = GetVertexShaderDisplacedHeight4(samplePos.xy + displacement.xz, lod, totalMask);
	#endif

		totalDisplacement += displacement;
	#endif

	totalDisplacement *= _SurfaceOffset.w;

	#ifdef _WATER_OVERLAYS
		half4 localMapsUv = half4((posWorld.xz + _LocalMapsCoords.xy) * _LocalMapsCoords.zz, 0, 0);
		half3 overlayDisplacement = tex2Dlod(_LocalDisplacementMap, localMapsUv);

		totalDisplacement *= tex2Dlod(_LocalSlopeMap, localMapsUv).w;
		totalDisplacement += overlayDisplacement * half3(_DisplacementsScale, 1.0, _DisplacementsScale);
	#endif

	posWorld.xyz += totalDisplacement;
}

#ifndef _VERTICAL_OFFSET
	#ifdef _DISPLACED_VOLUME
		#define _VERTICAL_OFFSET _SurfaceOffset.y
	#else
		#define _VERTICAL_OFFSET unity_ObjectToWorld[1].w
	#endif
#endif

inline float GetDisplacedHeight4(float2 pos)
{
	float height = _VERTICAL_OFFSET;

#if _WAVES_FFT && SHADER_TARGET >= 30
	float4 uv1 = pos.xyxy / _WaterTileSize.xxyy;
	float4 uv2 = pos.xyxy / _WaterTileSize.zzww;

	height += dot(half4(
		tex2D(_DisplacedHeightMaps, uv1.xy).r,
		tex2D(_DisplacedHeightMaps, uv1.zw).g,
		tex2D(_DisplacedHeightMaps, uv2.xy).b,
		tex2D(_DisplacedHeightMaps, uv2.zw).a
		), half4(1, 1, 1, 1));
#endif

	return height;

	//return tex2D(_TotalDisplacementMap, (pos.xy + _LocalMapsCoords.xy) * _LocalMapsCoords.zz);
}

inline half3 GetWaterDisplacement(float2 samplePos)
{
	half3 displacement = half3(0.0, 0.0, 0.0);

#if _WAVES_FFT
	float4 fftUV = samplePos.xyxy / _WaterTileSize.xxyy;
	float4 fftUV2 = samplePos.xyxy / _WaterTileSize.zzww;

	displacement = tex2D(_GlobalDisplacementMap, fftUV.xy).xyz;
	displacement += tex2D(_GlobalDisplacementMap1, fftUV.zw).xyz;
#if !defined(_WATER_OVERLAYS) || SHADER_TARGET >= 40
	displacement += tex2D(_GlobalDisplacementMap2, fftUV2.xy).xyz;
	#if !defined(_WAVES_ALIGN) || SHADER_TARGET >= 40
	displacement += tex2D(_GlobalDisplacementMap3, fftUV2.zw).xyz;
	#endif
#endif
#endif

	half2 normal = half2(0.0, 0.0);			// will be compiled out

#if _WAVES_GERSTNER
	for (int i = 0; i < (_WAVES_GERSTNER_COUNT / 4); ++i)
		Gerstner(-samplePos, _GrAmp[i], _GrFrq[i], _GrOff[i], _GrAB[i], _GrCD[i], _Time.y, /*out*/ displacement, /*out*/ normal);
#endif

	return displacement;
}
