// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

#ifndef WATERLIB_INCLUDED
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
#define WATERLIB_INCLUDED

#if SHADER_TARGET <= 30
	#undef _INCLUDE_SLOPE_VARIANCE
#endif

#if SHADER_TARGET <= 20
	#undef _WAVES_FFT
	#undef _WATER_OVERLAYS
	#undef _INCLUDE_SLOPE_VARIANCE
	#undef _VOLUMETRIC_LIGHTING
	#undef _PROJECTION_GRID
	#undef _WATER_REFRACTION
	#undef _ALPHABLEND_ON
	#undef _ALPHAPREMULTIPLY_ON
	#undef _CUBEMAP_REFLECTIONS
	#undef _NORMALMAP
	#undef _WATER_FOAM_WS
	#undef _WATER_RECEIVE_SHADOWS
#endif

#if defined(_DISPLACED_VOLUME) && !defined(_CLIP_ABOVE)
	#define _CLIP_ABOVE 1
#endif

#if defined(_WAVES_FFT) && !defined(_DISPLACED_VOLUME)
	#define _WAVES_FFT_SLOPE 1
#endif

#include "../Water/UnityLightingCommon.cginc"
#include "../Water/UnityStandardUtils.cginc"
#include "../Water/WaterDisplace.cginc"

// use differently named refraction tex for water volumes to include previously rendered regular water surfaces in it
#if !_DISPLACED_VOLUME
	sampler2D	_RefractionTex;
	half2 _RefractionTex_TexelSize;
	#define REFRACTION_TEX _RefractionTex
#else
	sampler2D	_RefractionTex2;
	half2 _RefractionTex2_TexelSize;
	#define REFRACTION_TEX _RefractionTex2
#endif

sampler2D	_PlanarReflectionTex;
half4		_PlanarReflectionPack;

half3		_AbsorptionColor;
half3		_DepthColor;

half		_RefractionDistortion;
half		_RefractionMaxDepth;

sampler2D	_GlobalNormalMap;
sampler2D	_GlobalNormalMap1;
half		_DisplacementNormalsIntensity;

sampler2D	_SubtractiveMask;
sampler2D	_AdditiveMask;
sampler3D	_SlopeVariance;

half3		_SubsurfaceScatteringPack;
half4		_WrapSubsurfaceScatteringPack;

sampler2D	_FoamTex;
half		_EdgeBlendFactorInv;
half4		_Foam;
half2		_FoamTiling;

half		_SpecularFresnelBias;
half3		_ReflectionColor;

sampler2D	_FoamMapWS;
sampler2D	_FoamNormalMap;
half		_FoamNormalScale;
half4		_FoamSpecularColor;
half		_MaxDisplacement;
half		_LightSmoothnessMul;
half		_Cull;

half		_PlanarReflectionMipBias;
float2		_WaterId;

sampler2D	_UnderwaterMask;

float4x4	_InvViewMatrix;

sampler2D_float _CameraDepthTexture;
sampler2D_float _WaterlessDepthTexture;

// I've isolated most of the water data here to not touch Standard shader code structure too much
struct WaterData
{
	half depth;
	half4 mask;
	half4 totalMask;
	half2 fftUV;
	half2 fftUV2;
	half4 refractedScreenPos;
	half shoreMask;
};

WaterData globalWaterData;

#if (SHADER_TARGET < 30) || defined(SHADER_API_PSP2)
	#define UNITY_BRDF_PBS BRDF3_Unity_PBS_Water
#elif defined(SHADER_API_MOBILE)
	#define UNITY_BRDF_PBS BRDF2_Unity_PBS_Water
#else
	#define UNITY_BRDF_PBS BRDF1_Unity_PBS_Water
#endif

#define WATER_SETUP1(i, s) WaterFragmentSetupPre(i.pack0.xy, i.pack1.zw, posWorld, i.screenPos);
#define WATER_SETUP_ADD_1(i, s) WaterFragmentSetupPre(i.pack0.xy, i.pack1.zw, posWorld, i.screenPos);

#define LOCAL_MAPS_UV i.pack0.zw

#ifndef _OBJECT2WORLD
	#define _OBJECT2WORLD unity_ObjectToWorld
#endif

#if _PROJECTION_GRID
	#ifdef TESS_OUTPUT
		#define GET_WORLD_POS(i) i;
	#else
		#define GET_WORLD_POS(i) float4(GetProjectedPosition(i.xy), 1);
	#endif
#else
	#ifdef TESS_OUTPUT
		#define GET_WORLD_POS(i) i;
	#else
		#define GET_WORLD_POS(i) mul(_OBJECT2WORLD, i);
	#endif
#endif

// Unity doesn't support shadows for transparent objects, so these macros have to be redefined here
#if _WATER_RECEIVE_SHADOWS
	sampler2D _WaterShadowmap;
	#define WATER_SHADOW_COORDS(idx1) unityShadowCoord4 _ShadowCoord : TEXCOORD##idx1;
	#define WATER_TRANSFER_SHADOW(a) a._ShadowCoord = ComputeScreenPos(a.pos);
	#define WATER_SHADOW_ATTENUATION(a) ShadowAttenuation(a._ShadowCoord)

	half ShadowAttenuation(half4 shadowCoord)
	{
		fixed shadow = tex2Dproj(_WaterShadowmap, UNITY_PROJ_COORD(shadowCoord)).r;
		return shadow;
	}
#else
	#define WATER_SHADOW_COORDS(idx1) 
	#define WATER_TRANSFER_SHADOW(a) 
	#define WATER_SHADOW_ATTENUATION(a) 1.0
#endif

inline half4 ComputeDistortOffset(half3 normalWorld, half distort)
{
	return half4(normalWorld.xz * distort, 0, 0);
}

inline half LinearEyeDepthHalf(half z)
{
	return 1.0 / (_ZBufferParams.z * z + _ZBufferParams.w);
}

// most of the water-specific data used around the shader is stored in a global struct to make future updates to the standard shader easier
inline void WaterFragmentSetupPre(half2 fftUV, half2 fftUV2, float3 worldPos, half4 screenPos)
{
	globalWaterData.fftUV = fftUV;
	globalWaterData.fftUV2 = fftUV2;
	globalWaterData.depth = LinearEyeDepth(screenPos.z / screenPos.w);

	ComputeEffectsMask(worldPos, globalWaterData.mask, globalWaterData.totalMask);
}

inline void WaterFragmentSetupPost(half3 normalWorld, half4 screenPos)
{
	half offset = ComputeDistortOffset(normalWorld, _RefractionDistortion);

#if SHADER_TARGET >= 30
	half centerSceneDepth = LinearEyeDepthHalf(SAMPLE_DEPTH_TEXTURE_PROJ(_WaterlessDepthTexture, screenPos + offset).r) - globalWaterData.depth;
	half distortScale = saturate(centerSceneDepth);
#else
	half distortScale = 1.0;
#endif

#ifndef _DISPLACED_VOLUME
	globalWaterData.refractedScreenPos = UNITY_PROJ_COORD(screenPos + offset * distortScale);
#else
	globalWaterData.refractedScreenPos = UNITY_PROJ_COORD(screenPos);
#endif
}

half2 UnpackScaleNormal2(half4 packednormal, half bumpScale)
{
#if defined(UNITY_NO_DXT5nm)
	return packednormal.xy * 2 - 1;
#else
	half2 normal;
	normal.xy = (packednormal.wy * 2 - 1);
#if (SHADER_TARGET >= 30)
	// SM2.0: instruction count limitation
	// SM2.0: normal scaler is not supported
	normal.xy *= bumpScale;
#endif
	return normal;
#endif
}

inline void AddFoam(half4 i_tex, half2 localMapsUv, inout half3 specColor, inout half smoothness, inout half3 albedo, inout half refractivity, inout half3 normalWorld)
{
#if _WATER_OVERLAYS || _WATER_FOAM_WS
	half foamIntensity = 0.0;

#if _WATER_FOAM_WS
	half4 uv1 = globalWaterData.fftUV.xyxy * _WaterTileSizeScales.yyzz;

	half4 foamIntensities;
	foamIntensities.x = tex2D(_FoamMapWS, globalWaterData.fftUV).x;
	foamIntensities.y = tex2D(_FoamMapWS, globalWaterData.fftUV2).y;
	foamIntensities.z = tex2D(_FoamMapWS, uv1.xy).z;
	foamIntensities.w = tex2D(_FoamMapWS, uv1.zw).w;

	foamIntensity += dot(foamIntensities, globalWaterData.mask.xyzw);
#endif

#if _WATER_FOAM_WS && _WATER_OVERLAYS				// currently it's not possible to have both modes (probably there is no reason)
	foamIntensity *= globalWaterData.shoreMask;
#endif

#if _WATER_OVERLAYS
	foamIntensity += tex2D(_LocalSlopeMap, localMapsUv.xy).z;
#endif

	// 1.2 - changes
	//foamIntensity = tanh(foamIntensity);
	foamIntensity = 1.0 - exp(-foamIntensity);
	half2 foamUV = i_tex.xy * _FoamTiling;

#if SHADER_TARGET >= 40		// make foam less tileable on modern platforms
	half3 foam = half3(tex2D(_FoamTex, foamUV).x, tex2D(_FoamTex, foamUV * 0.7731).y, tex2D(_FoamTex, foamUV * 0.437).z);
#else
	half3 foam = tex2D(_FoamTex, foamUV);
#endif

	//foam = lerp(half3(0.05, 0.36, 0.7), foam, globalWaterData.totalMask.yxz);			// do this later by blurring mip maps manually and exporting them to dds

	half3 channelIntensities = saturate((foamIntensity + half3(0.0, -0.25, -0.8)) / half3(0.3, 0.55, 0.2));
	foamIntensity *= saturate(dot(foam, channelIntensities));

	//half2 foamUV = i_tex.xy * _FoamTiling;
	//foamIntensity *= tex2D(_FoamTex, foamUV).r;

	albedo = lerp(albedo, half3(1.0, 1.0, 1.0), foamIntensity);
	specColor = lerp(specColor, _FoamSpecularColor.rgb, foamIntensity);
	smoothness = lerp(smoothness, _FoamSpecularColor.a, foamIntensity);

	half2 foamNormal = UnpackScaleNormal2(tex2D(_FoamNormalMap, foamUV), foamIntensity * _FoamNormalScale);
	normalWorld = normalize(normalWorld + half3(foamNormal.x, 0, foamNormal.y));

	refractivity *= 1.0 - foamIntensity;
#endif
}

//
// Derived from the paper and accompanying implementation:
// "Real-time Realistic Ocean Lighting using 
// Seamless Transitions from Geometry to BRDF"
// Eric Bruneton, Fabrice Neyret, Nicolas Holzschuch
//
inline void ApplySlopeVariance(float3 posWorld, inout float oneMinusRoughness, out half2 dirRoughness)
{
	half Jxx = ddx(posWorld.x);
	half Jxy = ddy(posWorld.x);
	half Jyx = ddx(posWorld.z);
	half Jyy = ddy(posWorld.z);
	half A = Jxx * Jxx + Jyx * Jyx;
	half B = Jxx * Jxy + Jyx * Jyy;
	half C = Jxy * Jxy + Jyy * Jyy;
	half SCALE = 10.0;
	half ua = pow(A / SCALE, 0.25);
	half ub = 0.5 + 0.5 * B / sqrt(A * C);
	half uc = pow(C / SCALE, 0.25);
	half2 sigmaSq = tex3D(_SlopeVariance, half3(ua, ub, uc)).xy;

	dirRoughness = 1.0 - oneMinusRoughness * (1.0 - sigmaSq);
	oneMinusRoughness = oneMinusRoughness * (1.0 - length(sigmaSq));
}

static const half4 _Weights[7] = { half4(0.0205,0.0205,0.0205,0.0205), half4(0.0855,0.0855,0.0855,0.0855), half4(0.232,0.232,0.232,0.232), half4(0.324,0.324,0.324,0.324), half4(0.232,0.232,0.232,0.232), half4(0.0855,0.0855,0.0855,0.0855), half4(0.0205,0.0205,0.0205,0.0205) };
half4x4 _PlanarReflectionProj;

half Pow2(half x)
{
	return x * x;
}

inline half4 SamplePlanarReflectionHq(sampler2D tex, half4 screenPos, half roughness, half2 dirRoughness)
{
	half4 color = 0;

#if UNITY_GLOSS_MATCHES_MARMOSET_TOOLBAG2
	dirRoughness = pow(dirRoughness, 3.0 / 4.0);
#endif

	half mip = _PlanarReflectionMipBias + min(dirRoughness.x, dirRoughness.y) * 7;
	half2 step = (clamp(dirRoughness.xy / dirRoughness.yx, 1, 1.006) - 1.0) * 1.5;

	half4 uv = half4(screenPos.xy / screenPos.w, 0, mip);
	uv.xy -= 3 * step;

	for (int i = 0; i < 7; ++i)
	{
#if SHADER_TARGET >= 30
		color += tex2Dlod(tex, uv) * _Weights[i];
#else
		color += tex2D(tex, uv.xy) * _Weights[i];
#endif

		uv.xy += step;
	}

	return color;
}

inline half4 SamplePlanarReflectionSimple(sampler2D tex, half4 screenPos, half roughness, half2 dirRoughness)
{
	half4 color;

#if SHADER_TARGET >= 30
	#if UNITY_GLOSS_MATCHES_MARMOSET_TOOLBAG2
		roughness = pow(roughness, 3.0 / 4.0);
	#endif

	half mip = _PlanarReflectionMipBias + roughness * 7;
	half4 uv = half4(screenPos.xy / screenPos.w, 0, mip);
	color = tex2Dlod(tex, uv);
#else
	color = tex2Dproj(tex, UNITY_PROJ_COORD(screenPos));
#endif

	return color;
}

#if _PLANAR_REFLECTIONS_HQ && SHADER_TARGET >= 40
	#define SamplePlanarReflection SamplePlanarReflectionHq
#else
	#define SamplePlanarReflection SamplePlanarReflectionSimple
#endif

// Used by 'UnityGlobalIllumination' in 'UnityGlobalIllumination.cginc'
inline void PlanarReflection(inout UnityGI gi, half4 screenPos, half roughness, half2 dirRoughness, half3 worldNormal)
{
#if SHADER_API_GLES || SHADER_API_OPENGL || SHADER_API_GLES3 || SHADER_API_METAL || SHADER_API_PSSL || SHADER_API_PS3 || SHADER_API_PSP2 || SHADER_API_PSM
	screenPos = mul(_PlanarReflectionProj, half4(worldNormal, 0));
#else
	screenPos = mul((half4x3)_PlanarReflectionProj, worldNormal);
#endif

	screenPos.y += (worldNormal.y - 1.0) * _PlanarReflectionPack.z;

#if _CUBEMAP_REFLECTIONS && (_PLANAR_REFLECTIONS || _PLANAR_REFLECTIONS_HQ)

	half4 planarReflection = SamplePlanarReflection(_PlanarReflectionTex, screenPos, roughness, dirRoughness);
	gi.indirect.specular.rgb = lerp(gi.indirect.specular.rgb, planarReflection.rgb, _PlanarReflectionPack.x * planarReflection.a);

#elif _PLANAR_REFLECTIONS || _PLANAR_REFLECTIONS_HQ

	half4 planarReflection = SamplePlanarReflection(_PlanarReflectionTex, screenPos, roughness, dirRoughness);
	gi.indirect.specular.rgb = planarReflection.rgb;

#endif
}

inline half BlendEdges(half4 screenPos)
{
#if _ALPHABLEND_ON
	half depth = LinearEyeDepthHalf(SAMPLE_DEPTH_TEXTURE_PROJ(_WaterlessDepthTexture, UNITY_PROJ_COORD(screenPos)));
	half depthScene = screenPos.w;
	return saturate(_EdgeBlendFactorInv * (depth - depthScene));			// if depth test indicates that the water is behind the scene, it is wrong because this shader wouldn't execute and it must be antialiasing artifact
#else
	return 1.0;
#endif
}

inline void MaskWater(out half alpha, float4 screenPos, float3 worldPos)
{
#if !defined(_DISPLACED_VOLUME) && !defined(_DEPTH) && defined(_WATER_REFRACTION)
	alpha = BlendEdges(screenPos);
#else
	alpha = 1.0;
#endif

	float depth = screenPos.z / screenPos.w;

#ifndef _CLIP_ABOVE
	float4 subMask = tex2Dproj(_SubtractiveMask, UNITY_PROJ_COORD(screenPos));

#if _DISPLACED_VOLUME
	alpha *= subMask.w;
#else
#if UNITY_VERSION < 550
	if (depth <= subMask.y && depth >= subMask.z && fmod(subMask.x, _WaterId.y) >= _WaterId.x)
#else
	if (depth <= subMask.z && depth >= subMask.y && fmod(subMask.x, _WaterId.y) >= _WaterId.x)
#endif
		alpha *= subMask.w;
#endif
#endif

#if _BOUNDED_WATER
	float4 addMask = tex2Dproj(_AdditiveMask, UNITY_PROJ_COORD(screenPos));

#if UNITY_VERSION < 550
	if (depth < addMask.z || depth > addMask.y || fmod(addMask.x, _WaterId.y) < _WaterId.x)
#else
	if (depth < addMask.y || depth > addMask.z || fmod(addMask.x, _WaterId.y) < _WaterId.x)
#endif
		alpha = 0.0;
#endif

#if _CLIP_ABOVE
	alpha = step(worldPos.y, GetDisplacedHeight4(worldPos.xz + _SurfaceOffset.xz) + 0.04);
#endif
}

inline void UnderwaterClip(half4 screenPos)
{
#if _WATER_BACK && SHADER_TARGET >= 30				// on sm 2.0, there is no support for underwater effect masking
	fixed mask = tex2Dproj(_UnderwaterMask, UNITY_PROJ_COORD(screenPos));
	clip(-0.001 + mask);
#endif
}

sampler2D _CausticsMap;
float4x4  _CausticsMapProj;

inline half3 ComputeDepthColor(half3 posWorld, half3 eyeVec, half3 lightDir, half3 lightColor, float maxDepth = 0)
{
#if defined(_VOLUMETRIC_LIGHTING) && !defined(LIGHTMAP_ON)
	half3 result = 0;
	half step = _SubsurfaceScatteringPack.y;

	lightDir.y *= 0.25;
	lightDir = normalize(lightDir);

	half3 samplePoint = posWorld + half3(_SurfaceOffset.x, 0.0, _SurfaceOffset.z);
	half depth = 0;
	half pidiv2 = 3.14159 * 0.5;

#if SHADER_TARGET >= 40 || _UNDERWATER_EFFECT
	for (int i = 0; i < 5; ++i)
#elif SHADER_TARGET >= 30
	for (int i = 0; i < 3; ++i)
#else
	//for (int i = 0; i < 1; ++i)
#endif
	{
		samplePoint -= eyeVec * step;
		depth += step;
		step *= _SubsurfaceScatteringPack.z;

		half waterHeight = GetDisplacedHeight4(samplePoint.xz);

#if _WATER_OVERLAYS
		waterHeight *= globalWaterData.shoreMask;
#endif

		half3 light1 = 1.0;			// light that has been transmitted from the entry point to here

#if _UNDERWATER_EFFECT
		if (depth < maxDepth)
			light1 = saturate((maxDepth - depth) / step);
		else
			light1 = 0;
#endif

		result += exp(_AbsorptionColor * min(samplePoint.y - waterHeight - _MaxDisplacement * 0.08, 0)) * exp(-_AbsorptionColor * depth) * light1;			// multiply this by depth
	}

	half dp = dot(lightDir, -eyeVec);

#if _UNDERWATER_EFFECT
	if (dp < 0) dp = 0;
#else
	if (dp < 0) dp *= -0.25;
	result = lerp(half3(0.5, 0.5, 0.5), result, globalWaterData.totalMask.x);			// mask tiles when viewed from a distance
#endif

	dp = 0.333333 + dp * dp * 0.666666;

	return result * _SubsurfaceScatteringPack.x * dp * lightColor;			// _DepthColor
#else
	return 0;
#endif
}

// used by lighting functions at the bottom of this file
inline half3 ComputeRefractionColor(half3 normalWorld, half3 eyeVec, half3 posWorld, UnityLight light, WaterData waterData, out half3 depthFade)
{
	half waterSurfaceDepth = waterData.depth;

#if !defined(_WATER_BACK)
	half sceneDepth = LinearEyeDepthHalf(SAMPLE_DEPTH_TEXTURE_PROJ(_WaterlessDepthTexture, waterData.refractedScreenPos).r) - waterSurfaceDepth;
	depthFade = min(exp(-_AbsorptionColor * sceneDepth), 1);
#else
	depthFade = 1;
#endif

#if _WATER_REFRACTION
	#if UNITY_UV_STARTS_AT_TOP
		if (_ProjectionParams.x >= 0)
			waterData.refractedScreenPos.y = waterData.refractedScreenPos.w - waterData.refractedScreenPos.y;
	#endif

	half3 depthColor = ComputeDepthColor(posWorld, eyeVec, light.dir, light.color);
	return lerp(depthColor, tex2Dproj(REFRACTION_TEX, waterData.refractedScreenPos).rgb, depthFade);
#else
	depthFade = 0;
	return _DepthColor;
#endif
}

// grid projection
float3 GetScreenRay(float2 screenPos)
{
	return mul((float3x3)_InvViewMatrix, float3(screenPos.xy, -UNITY_MATRIX_P[0].x));
}

float3 GetProjectedPosition(float2 vertex)
{
	float screenScale = 1.2;
	float focal = UNITY_MATRIX_P[0].x;
	float aspect = UNITY_MATRIX_P[1].y;

	float2 screenPos = float2((vertex.x - 0.5) * screenScale * aspect, (vertex.y - 0.5) * screenScale * focal);

	float3 ray = GetScreenRay(screenPos);

	if (ray.y == 0) ray.y = 0.001;

	float d = _WorldSpaceCameraPos.y / -ray.y;

	float3 pos;

	if (d >= 0.0)
		pos = _WorldSpaceCameraPos.xyz + ray * d;
	else
		pos = float3(_WorldSpaceCameraPos.x, 0.0, _WorldSpaceCameraPos.z) + normalize(float3(ray.x, 0.0, ray.z)) * _ProjectionParams.z * (1.0 + -1.0 / d);

	pos -= normalize(float3(_InvViewMatrix[0].x, 0, _InvViewMatrix[0].z)) * (vertex.x - 0.5) * 2 * _MaxDisplacement;
	pos -= normalize(float3(_InvViewMatrix[2].x, 0, _InvViewMatrix[2].z)) * (vertex.y - 0.5) * 2 * _MaxDisplacement;

	return pos;
}

inline half SimpleFresnel(half dp)
{
	half t = 1.0 - dp;

	return t * t;
}

#include "UnityStandardBRDF.cginc"

inline half3 FresnelFast2 (half cosA)
{
	return Pow4 (1 - cosA);
}

inline half3 FresnelTerm (half3 F0, half cosA, half bias)
{
	half t = bias + (1.0 - bias) * Pow5 (1 - cosA);	// ala Schlick interpoliation
	return F0 + (1-F0) * t;
}

inline half3 FresnelLerp (half3 F0, half3 F90, half cosA, half bias)
{
	half t = bias + (1.0 - bias) * Pow5 (1 - cosA);	// ala Schlick interpoliation
	return lerp (F0, F90, t);
}

half4 BRDF1_Unity_PBS_Water (half3 diffColor, half3 specColor, half oneMinusReflectivity, half oneMinusRoughness, half refractivity,
	half3 normal, half3 viewDir, half3 posWorld,
	UnityLight light, UnityIndirect gi, half atten)
{
	oneMinusRoughness *= _LightSmoothnessMul;

	half roughness = 1-oneMinusRoughness;
	half3 halfDir = normalize (light.dir + viewDir);

	half nl = light.ndotl;
	half nh = BlinnTerm (normal, halfDir);
	half nv = DotClamped (normal, viewDir);
	half lv = DotClamped (light.dir, viewDir);
	half lh = DotClamped (light.dir, halfDir);

#if defined(POINT) || defined(SPOT) || defined(POINT_COOKIE)
	nl = (nl + _WrapSubsurfaceScatteringPack.z) * _WrapSubsurfaceScatteringPack.w;
#else
	nl = (nl + _WrapSubsurfaceScatteringPack.x) * _WrapSubsurfaceScatteringPack.y;
#endif

#if 0 // UNITY_BRDF_GGX - I'm not sure when it's set, but we don't want this in the case of water
	half V = SmithGGXVisibilityTerm (nl, nv, roughness);
	half D = GGXTerm (nh, roughness);
#else
	half V = SmithBeckmannVisibilityTerm (nl, nv, roughness);
	half D = NDFBlinnPhongNormalizedTerm (nh, RoughnessToSpecPower (roughness));
#endif

	half nlPow5 = Pow5 (1-nl);
	half nvPow5 = Pow5 (1-nv);
	half Fd90 = 0.5 + 2 * lh * lh * roughness;
	half disneyDiffuse = (1 + (Fd90-1) * nlPow5) * (1 + (Fd90-1) * nvPow5);
	
	// HACK: theoretically we should divide by Pi diffuseTerm and not multiply specularTerm!
	// BUT 1) that will make shader look significantly darker than Legacy ones
	// and 2) on engine side "Non-important" lights have to be divided by Pi to in cases when they are injected into ambient SH
	// NOTE: multiplication by Pi is part of single constant together with 1/4 now

	half pix4inv = 0.07958;			// 1 / 4pi
	half specularTerm = max(0, (V * D * nl) * pix4inv);// Torrance-Sparrow model, Fresnel is applied later (for optimization reasons)
	half diffuseTerm = disneyDiffuse * nl;

	half3 depthFade;
	half3 refraction = ComputeRefractionColor(normal, viewDir, posWorld, light, globalWaterData, depthFade);
	
	half grazingTerm = saturate(oneMinusRoughness + (1-oneMinusReflectivity));
    half3 color =	(diffColor * (gi.diffuse + light.color * diffuseTerm) * (1.0 - depthFade)				// diffuse part here represents a portion of light that is refracted inside the water and scattered back
                    + specularTerm * light.color * FresnelTerm(specColor, lh, _SpecularFresnelBias)) * atten
					+ gi.specular * FresnelLerp (specColor, grazingTerm, nv, _SpecularFresnelBias);

	color += refraction * refractivity * disneyDiffuse / UNITY_PI;
	//return half4(refraction * refractivity * disneyDiffuse / UNITY_PI, 1);

	return half4(color, 1);
}


half4 BRDF2_Unity_PBS_Water (half3 diffColor, half3 specColor, half oneMinusReflectivity, half oneMinusRoughness, half refractivity,
	half3 normal, half3 viewDir, half3 posWorld,
	UnityLight light, UnityIndirect gi, half atten)
{
	half3 halfDir = normalize (light.dir + viewDir);

	half nl = light.ndotl;
	half nh = BlinnTerm (normal, halfDir);
	half nv = DotClamped (normal, viewDir);
	half lh = DotClamped (light.dir, halfDir);

	half roughness = 1-oneMinusRoughness;
	half specularPower = RoughnessToSpecPower (roughness);
	// Modified with approximate Visibility function that takes roughness into account
	// Original ((n+1)*N.H^n) / (8*Pi * L.H^3) didn't take into account roughness 
	// and produced extremely bright specular at grazing angles

	// HACK: theoretically we should divide by Pi diffuseTerm and not multiply specularTerm!
	// BUT 1) that will make shader look significantly darker than Legacy ones
	// and 2) on engine side "Non-important" lights have to be divided by Pi to in cases when they are injected into ambient SH
	// NOTE: multiplication by Pi is cancelled with Pi in denominator

	half invV = lh * lh * oneMinusRoughness + roughness * roughness; // approx ModifiedKelemenVisibilityTerm(lh, 1-oneMinusRoughness);
	half invF = lh;
	half specular = ((specularPower + 1) * pow (nh, specularPower)) / (unity_LightGammaCorrectionConsts_8 * invV * invF + 1e-4f); // @TODO: might still need saturate(nl*specular) on Adreno/Mali

	half fresnelTerm = FresnelFast2(nv);

	half grazingTerm = saturate(oneMinusRoughness + (1-oneMinusReflectivity));
    half3 color =	specular * light.color * nl * atten
					+ gi.specular * lerp (specColor, grazingTerm, fresnelTerm);

	half3 depthFade;
	half3 refraction = ComputeRefractionColor(normal, viewDir, posWorld, light, globalWaterData, depthFade);
	color = lerp(color, refraction, (1.0 - fresnelTerm) * refractivity);

	return half4(color, 1);
}


half4 BRDF3_Unity_PBS_Water (half3 diffColor, half3 specColor, half oneMinusReflectivity, half oneMinusRoughness, half refractivity,
	half3 normal, half3 viewDir, half3 posWorld,
	UnityLight light, UnityIndirect gi, half atten)
{
	half LUT_RANGE = 16.0; // must match range in NHxRoughness() function in GeneratedTextures.cpp

	half3 reflDir = reflect (viewDir, normal);
	half3 halfDir = normalize (light.dir + viewDir);

	half nl = light.ndotl;
	half nh = BlinnTerm (normal, halfDir);
	half nv = DotClamped (normal, viewDir);
	half rl = dot(reflDir, light.dir);

	// Vectorize Pow4 to save instructions
	half rlPow4 = Pow4(rl); // power exponent must match kHorizontalWarpExp in NHxRoughness() function in GeneratedTextures.cpp
	half fresnelTerm = SimpleFresnel(nv);

	half3 depthFade;
	half3 refraction = ComputeRefractionColor(normal, viewDir, posWorld, light, globalWaterData, depthFade);

#if 1 // Lookup texture to save instructions
	half specular = tex2D(unity_NHxRoughness, half2(rlPow4, 0.5 * (1-oneMinusRoughness))).UNITY_ATTEN_CHANNEL * LUT_RANGE;
#else
	half roughness = 1-oneMinusRoughness;
	half n = RoughnessToSpecPower (roughness) * .25;
	half specular = (n + 2.0) / (2.0 * UNITY_PI * UNITY_PI) * pow(dot(reflDir, light.dir), n) * nl;// / unity_LightGammaCorrectionConsts_PI;
	//half specular = (1.0/(UNITY_PI*roughness*roughness)) * pow(dot(reflDir, light.dir), n) * nl;// / unity_LightGammaCorrectionConsts_PI;
#endif
	//half grazingTerm = saturate(oneMinusRoughness + (1-oneMinusReflectivity));
	half grazingTerm = oneMinusRoughness + (1-oneMinusReflectivity);

    half3 color =	specular * light.color * nl
					+ gi.specular * lerp (specColor, grazingTerm, fresnelTerm);

	color = lerp(color, refraction, (1.0 - fresnelTerm) * refractivity);

	return half4(color, 1);
}

#endif
