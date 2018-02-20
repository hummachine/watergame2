// Upgrade NOTE: replaced '_World2Object' with 'unity_WorldToObject'

#ifndef UNITY_STANDARD_CORE_INCLUDED
#define UNITY_STANDARD_CORE_INCLUDED

#include "UnityCG.cginc"
#include "../Water/WaterLib.cginc"
#include "UnityShaderVariables.cginc"
#include "../Water/UnityStandardConfig.cginc"
#include "../Water/UnityStandardInput.cginc"
#include "../Water/UnityPBSLighting.cginc"
#include "../Water/UnityStandardUtils.cginc"
#include "../Water/UnityStandardBRDF.cginc"

#include "../Water/AutoLight.cginc"


//-------------------------------------------------------------------------------------
// counterpart for NormalizePerPixelNormal
// skips normalization per-vertex and expects normalization to happen per-pixel
half3 NormalizePerVertexNormal (half3 n)
{
	#if (SHADER_TARGET < 30)
		return normalize(n);
	#else
		return n; // will normalize per-pixel instead
	#endif
}

half3 NormalizePerPixelNormal (half3 n)
{
	#if (SHADER_TARGET < 30)
		return n;
	#else
		return normalize(n);
	#endif
}

//-------------------------------------------------------------------------------------
UnityLight MainLight (half3 normalWorld)
{
	UnityLight l;
	#ifdef LIGHTMAP_OFF
		
		l.color = _LightColor0.rgb;
		l.dir = _WorldSpaceLightPos0.xyz;
		l.ndotl = LambertTerm (normalWorld, l.dir);
	#else
		// no light specified by the engine
		// analytical light might be extracted from Lightmap data later on in the shader depending on the Lightmap type
		l.color = half3(0.f, 0.f, 0.f);
		l.ndotl  = 0.f;
		l.dir = half3(0.0001f, 0.f, 0.f);			// x = 0.0001 to prevent NaN evaluation during compulation when normalized (it's not allowed on older APIs)
	#endif

	return l;
}

UnityLight AdditiveLight (half3 normalWorld, half3 lightDir, half atten)
{
	UnityLight l;

	l.color = _LightColor0.rgb;
	l.dir = lightDir;
	#ifndef USING_DIRECTIONAL_LIGHT
		l.dir = NormalizePerPixelNormal(l.dir);
	#endif
	l.ndotl = LambertTerm (normalWorld, l.dir);

	// shadow the light
	l.color *= atten;
	return l;
}

UnityLight DummyLight (half3 normalWorld)
{
	UnityLight l;
	l.color = 0;
	l.dir = half3 (0,1,0);
	l.ndotl = LambertTerm (normalWorld, l.dir);
	return l;
}

UnityIndirect ZeroIndirect ()
{
	UnityIndirect ind;
	ind.diffuse = 0;
	ind.specular = 0;
	return ind;
}

//-------------------------------------------------------------------------------------
// Common fragment setup
half3 WorldNormal(half4 tan2world[3])
{
	return normalize(tan2world[2].xyz);
}

#ifdef _TANGENT_TO_WORLD
	/*half3x3 ExtractTangentToWorldPerPixel(half4 tan2world[3])
	{
		half3 t = tan2world[0].xyz;
		half3 b = tan2world[1].xyz;
		half3 n = tan2world[2].xyz;

	#if UNITY_TANGENT_ORTHONORMALIZE
		n = NormalizePerPixelNormal(n);

		// ortho-normalize Tangent
		t = normalize (t - n * dot(t, n));

		// recalculate Binormal
		half3 newB = cross(n, t);
		b = newB * sign (dot (newB, b));
	#endif

		return half3x3(t, b, n);
	}*/
	half3x3 ExtractTangentToWorldPerPixel()
	{
		half3 t = half3(-1, 0, 0);
		half3 b = half3(0, 0, -1);
		half3 n = half3(0, 1, 0);

	/*#if UNITY_TANGENT_ORTHONORMALIZE
		n = NormalizePerPixelNormal(n);

		// ortho-normalize Tangent
		t = normalize (t - n * dot(t, n));

		// recalculate Binormal
		half3 newB = cross(n, t);
		b = newB * sign (dot (newB, b));
	#endif*/

		return half3x3(t, b, n);
	}
#else
	half3x3 ExtractTangentToWorldPerPixel()
	{
		return half3x3(0,0,0,0,0,0,0,0,0);
	}
#endif

#ifdef _PARALLAXMAP
	#define IN_VIEWDIR4PARALLAX(i) NormalizePerPixelNormal(half3(i.parallax.xyz))
	#define IN_VIEWDIR4PARALLAX_FWDADD(i) NormalizePerPixelNormal(i.viewDirForParallax.xyz)
#else
	#define IN_VIEWDIR4PARALLAX(i) half3(0,0,0)
	#define IN_VIEWDIR4PARALLAX_FWDADD(i) half3(0,0,0)
#endif

#define IN_LIGHTDIR_FWDADD(i) half3(i.lightDir.xyz)

#if _WAVES_GERSTNER || _DISPLACED_VOLUME
	#define VERTEX_NORMAL i.pack1
#else
	#define VERTEX_NORMAL half3(0, 1, 0)
#endif

#define FRAGMENT_SETUP(x) FragmentCommonData x = \
	FragmentSetup(i.pack0.xyxy, LOCAL_MAPS_UV, i.eyeVec, VERTEX_NORMAL, IN_VIEWDIR4PARALLAX(i), ExtractTangentToWorldPerPixel(), posWorld);

#define FRAGMENT_SETUP_FWDADD(x) FragmentCommonData x = \
	FragmentSetup(i.pack0.xyxy, LOCAL_MAPS_UV, i.eyeVec, VERTEX_NORMAL, IN_VIEWDIR4PARALLAX_FWDADD(i), ExtractTangentToWorldPerPixel(/*i.tangentToWorldAndLightDir*/), posWorld);

struct FragmentCommonData
{
	half3 diffColor, specColor;
	// Note: oneMinusRoughness & oneMinusReflectivity for optimization purposes, mostly for DX9 SM2.0 level.
	// Most of the math is being done on these (1-x) values, and that saves a few precious ALU slots.
	half oneMinusReflectivity, oneMinusRoughness;
	half3 normalWorld, eyeVec, posWorld;
	half alpha;
	half refractivity;
};

#ifndef UNITY_SETUP_BRDF_INPUT
	#define UNITY_SETUP_BRDF_INPUT SpecularSetup
#endif

inline FragmentCommonData SpecularSetup (half4 i_tex, half2 localMapsUv, inout half3 normalWorld)
{
	half4 specGloss = SpecularGloss(i_tex.xy);
	half3 specColor = specGloss.rgb;
	half oneMinusRoughness = specGloss.a;
	half3 albedo = Albedo(i_tex);
	half refractivity = 1.0;

#ifdef _WATER_OVERLAYS
	globalWaterData.shoreMask = tex2D(_LocalSlopeMap, localMapsUv.xy).w;
#endif

	AddFoam(i_tex, localMapsUv, /*out*/ specColor, /*out*/ oneMinusRoughness, /*out*/ albedo, /*out*/ refractivity, /*out*/ normalWorld);

	half oneMinusReflectivity;
	half3 diffColor = EnergyConservationBetweenDiffuseAndSpecular (albedo, specColor, /*out*/ oneMinusReflectivity);
	
	FragmentCommonData o = (FragmentCommonData)0;
	o.diffColor = diffColor;
	o.specColor = specColor;
	o.oneMinusReflectivity = oneMinusReflectivity;
	o.oneMinusRoughness = oneMinusRoughness;
	o.refractivity = refractivity;
	return o;
}

/*inline FragmentCommonData MetallicSetup (half4 i_tex)
{
	half2 metallicGloss = MetallicGloss(i_tex.xy);
	half metallic = metallicGloss.x;
	half oneMinusRoughness = metallicGloss.y;

	half oneMinusReflectivity;
	half3 specColor;
	half3 diffColor = DiffuseAndSpecularFromMetallic (Albedo(i_tex), metallic, /*out* / specColor, /*out* / oneMinusReflectivity);

	FragmentCommonData o = (FragmentCommonData)0;
	o.diffColor = diffColor;
	o.specColor = specColor;
	o.oneMinusReflectivity = oneMinusReflectivity;
	o.oneMinusRoughness = oneMinusRoughness;
	return o;
} */

inline FragmentCommonData FragmentSetup (half4 i_tex, half2 localMapsUv, half3 i_eyeVec, half3 i_normalWorld, half3 i_viewDirForParallax, half3x3 i_tanToWorld, half3 i_posWorld)
{
	half3 eyeVec = i_eyeVec;
	eyeVec = NormalizePerPixelNormal(eyeVec);

#ifndef _DISPLACED_VOLUME
	half2 normalFlat = NormalInTangentSpace(i_tex, i_posWorld, eyeVec, localMapsUv, i_normalWorld.xy);
	half3 normalWorld = normalize(half3(normalFlat.x, 1.0, normalFlat.y));
#else
	half3 normalWorld = normalize(i_normalWorld);

	if (_Cull == 1.0)
		normalWorld = -normalWorld;
#endif

#if _WATER_BACK
	normalWorld = -normalWorld;
#endif

	FragmentCommonData o = UNITY_SETUP_BRDF_INPUT (i_tex, localMapsUv, normalWorld);
	o.normalWorld = normalWorld;
	o.eyeVec = eyeVec;
	o.posWorld = i_posWorld;
	o.alpha = 1.0;

	return o;
}

inline UnityGI FragmentGI (
	float3 posWorld, 
	half occlusion, half4 i_ambientOrLightmapUV, half atten, half oneMinusRoughness, half3 normalWorld, half3 eyeVec,
	UnityLight light, half4 screenPos, half2 dirRoughness)
{
	UnityGIInput d;
	d.light = light;
	d.worldPos = posWorld;
	d.worldViewDir = -eyeVec;
	d.screenPos = screenPos;
	d.atten = atten;
	#if defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON)
		d.ambient = 0;
		d.lightmapUV = i_ambientOrLightmapUV;
	#else
		d.ambient = i_ambientOrLightmapUV.rgb;
		d.lightmapUV = 0;
	#endif
	d.boxMax[0] = unity_SpecCube0_BoxMax;
	d.boxMin[0] = unity_SpecCube0_BoxMin;
	d.probePosition[0] = unity_SpecCube0_ProbePosition;
	d.probeHDR[0] = unity_SpecCube0_HDR;

	d.boxMax[1] = unity_SpecCube1_BoxMax;
	d.boxMin[1] = unity_SpecCube1_BoxMin;
	d.probePosition[1] = unity_SpecCube1_ProbePosition;
	d.probeHDR[1] = unity_SpecCube1_HDR;

	return UnityGlobalIllumination (
		d, occlusion, oneMinusRoughness, normalWorld, true, dirRoughness);
}

//-------------------------------------------------------------------------------------
half4 OutputForward (half4 output, half alphaFromSurface)
{
	#if defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)
		output.a = alphaFromSurface;
	#else
		UNITY_OPAQUE_ALPHA(output.a);
	#endif
	return output;
}

// ------------------------------------------------------------------
//  Base forward pass (directional light, emission, lightmaps, ...)

struct VertexOutputForwardBase
{
	float4 pos							: SV_POSITION;
	half4 tex							: TEXCOORD0;
	half4 eyeVec 						: TEXCOORD1;			// w - free
	float4 pack0						: TEXCOORD2;			// xy - global uv 0, zw - local maps uv
	half4 screenPos						: TEXCOORD3;
	half4 ambientOrLightmapUV			: TEXCOORD4;			// SH or Lightmap UV
	half4 pack1							: TEXCOORD5;			// gerstner: xy - normals, displaced volume: xyz - normals, non displaced volume: zw - fft uv 2
	float4 fogCoord						: TEXCOORD6;

	WATER_SHADOW_COORDS(7)
};

inline half3 UnityObjectToWorldNormalFast( in half3 norm )
{
	// Multiply by transposed inverse matrix, actually using transpose() generates badly optimized code
	return normalize(unity_WorldToObject[0].xyz * norm.x + unity_WorldToObject[1].xyz * norm.y + unity_WorldToObject[2].xyz * norm.z);
}

VertexOutputForwardBase vertForwardBase (VertexInput v)
{
	VertexOutputForwardBase o;
	UNITY_INITIALIZE_OUTPUT(VertexOutputForwardBase, o);

	float4 posWorld = GET_WORLD_POS(v.vertex);
	half2 uv2 = posWorld.xz / 100.0;

#if _DISPLACED_VOLUME || _WATER_OVERLAYS
	o.pack0.zw = (posWorld.xz + _LocalMapsCoords.xy) * _LocalMapsCoords.zz;
#endif

	half2 normal;
	float4 fftUV;
	float4 fftUV2;
	float3 displacement;
	TransformVertex(posWorld, normal, fftUV, fftUV2, displacement);

	#if _WAVES_GERSTNER
		o.pack1.xy = normal * _DisplacementNormalsIntensity;
	#elif _DISPLACED_VOLUME
		o.pack1.xyz = UnityObjectToWorldNormal(v.normal);		// use mesh normals
	#endif

	#if _WAVES_FFT_SLOPE
		o.pack0.xy = fftUV.xy;
		o.pack1.zw = fftUV.zw;
	#endif

	o.tex = TexCoords(uv2, uv2);
	
	o.pos = mul(UNITY_MATRIX_VP, posWorld);
	o.screenPos = ComputeScreenPos(o.pos);

	o.eyeVec.xyz = NormalizePerVertexNormal(posWorld.xyz - _WorldSpaceCameraPos);

	half3 normalWorld = normalize(half3(normal.x, 1.0, normal.y));
	
	//We need this for shadow receving
	WATER_TRANSFER_SHADOW(o);

	// Static lightmaps
	#ifndef LIGHTMAP_OFF
		o.ambientOrLightmapUV.xy = uv2.xy * unity_LightmapST.xy + unity_LightmapST.zw;
		o.ambientOrLightmapUV.zw = 0;
	// Sample light probe for Dynamic objects only (no static or dynamic lightmaps)
	#elif UNITY_SHOULD_SAMPLE_SH
		#if UNITY_SAMPLE_FULL_SH_PER_PIXEL
			o.ambientOrLightmapUV.rgb = 0;
		#elif (SHADER_TARGET < 30)
			o.ambientOrLightmapUV.rgb = ShadeSH9(half4(normalWorld, 1.0));
		#else
			// Optimization: L2 per-vertex, L0..L1 per-pixel
			o.ambientOrLightmapUV.rgb = ShadeSH3Order(half4(normalWorld, 1.0));
		#endif
		// Add approximated illumination from non-important point lights
		#ifdef VERTEXLIGHT_ON
			o.ambientOrLightmapUV.rgb += Shade4PointLights (
				unity_4LightPosX0, unity_4LightPosY0, unity_4LightPosZ0,
				unity_LightColor[0].rgb, unity_LightColor[1].rgb, unity_LightColor[2].rgb, unity_LightColor[3].rgb,
				unity_4LightAtten0, posWorld, normalWorld);
		#endif
	#endif

	#ifdef DYNAMICLIGHTMAP_ON
		o.ambientOrLightmapUV.zw = v.uv2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
	#endif
	
	UNITY_TRANSFER_FOG(o,o.pos);
	o.fogCoord.yzw = posWorld;
	return o;
}

half4 fragForwardBase (VertexOutputForwardBase i) : SV_Target
{
	half alpha = 1;

	UnderwaterClip(i.screenPos);

	float3 posWorld = i.fogCoord.yzw;

#if SHADER_TARGET >= 30
	MaskWater(alpha, i.screenPos, posWorld);
	clip(alpha - 0.006);
#endif

	WATER_SETUP1(i, s)
	FRAGMENT_SETUP(s)
	WaterFragmentSetupPost(s.normalWorld, i.screenPos);

	half2 dirRoughness = 1.0 - s.oneMinusRoughness;
#if _INCLUDE_SLOPE_VARIANCE
	ApplySlopeVariance(posWorld, s.oneMinusRoughness, /* out */ dirRoughness);
#endif

	UnityLight mainLight = MainLight (s.normalWorld);
	half atten = WATER_SHADOW_ATTENUATION(i);

	//half occlusion = Occlusion(i.tex.xy);
	half occlusion = 1;

	UnityGI gi = FragmentGI (
		s.posWorld, occlusion, i.ambientOrLightmapUV, atten, s.oneMinusRoughness, s.normalWorld, s.eyeVec, mainLight, i.screenPos, dirRoughness);

	half4 c = UNITY_BRDF_PBS (s.diffColor, s.specColor, s.oneMinusReflectivity, s.oneMinusRoughness, s.refractivity, s.normalWorld, -s.eyeVec, s.posWorld, gi.light, gi.indirect, atten);
	c.rgb += UNITY_BRDF_GI (s.diffColor, s.specColor, s.oneMinusReflectivity, s.oneMinusRoughness, s.normalWorld, -s.eyeVec, occlusion, gi);
	c.rgb += Emission(i.tex.xy);

#if _DEBUG_SHORES
	c.r += pow(1.0 - tex2D(_LocalSlopeMap, LOCAL_MAPS_UV).a, 2) * 0.8;
#endif

	UNITY_APPLY_FOG(i.fogCoord, c.rgb);

	return OutputForward (c, alpha);
}

#if SHADER_TARGET >= 30
// ------------------------------------------------------------------
//  Additive forward pass (one light per pass)
struct VertexOutputForwardAdd
{
	float4 pos							: SV_POSITION;
	half4 tex							: TEXCOORD0;
	half4 eyeVec 						: TEXCOORD1;
	//half4 tangentToWorldAndLightDir[3]	: TEXCOORD2;	// [3x3:tangentToWorld | 1x3:lightDir]
	half4 lightDir						: TEXCOORD2;			// w - unused

	LIGHTING_COORDS(3,4)
	float4 fogCoord						: TEXCOORD5;

	half4 pack1							: TEXCOORD6;			// gerstner: xy - normals, displaced volume: xyz - normals, non displaced volume: zw - fft uv 2

	half4 screenPos						: TEXCOORD7;
	half4 pack0							: TEXCOORD8;			// xy - global maps uv, zw - local maps uv
};

VertexOutputForwardAdd vertForwardAdd (VertexInput v)
{
	VertexOutputForwardAdd o;
	UNITY_INITIALIZE_OUTPUT(VertexOutputForwardAdd, o);

	float4 posWorld = GET_WORLD_POS(v.vertex);
	half2 uv2 = half2(posWorld.xz);

	half2 normal;
	float4 fftUV;
	float4 fftUV2;
	float3 displacement;
	TransformVertex(posWorld, normal, fftUV, fftUV2, displacement);

	#if _WAVES_GERSTNER
		o.pack1.xy = normal * _DisplacementNormalsIntensity;
	#elif _DISPLACED_VOLUME
		o.pack1.xyz = UnityObjectToWorldNormal(v.normal);		// use mesh normals
	#endif

	#if _WAVES_FFT_SLOPE
		o.pack0.xy = fftUV.xy;
		o.pack1.zw = fftUV.zw;
	#endif

	o.tex = TexCoords(uv2, uv2);

	o.pos = mul(UNITY_MATRIX_VP, posWorld);

	o.screenPos = ComputeScreenPos(o.pos);
	o.eyeVec.xyz = NormalizePerVertexNormal(posWorld.xyz - _WorldSpaceCameraPos);
	half3 normalWorld = normalize(half3(normal.x, 1.0, normal.y));
	
	//We need this for shadow receving
	TRANSFER_VERTEX_TO_FRAGMENT(o);

	half3 lightDir = _WorldSpaceLightPos0.xyz - posWorld.xyz * _WorldSpaceLightPos0.w;
	#ifndef USING_DIRECTIONAL_LIGHT
		lightDir = NormalizePerVertexNormal(lightDir);
	#endif
	o.lightDir.xyz = lightDir;
	
	UNITY_TRANSFER_FOG(o,o.pos);
	o.fogCoord.yzw = posWorld;
	return o;
}

half4 fragForwardAdd (VertexOutputForwardAdd i) : SV_Target
{
	half alpha = 1;

	UnderwaterClip(i.screenPos);
	float3 posWorld = i.fogCoord.yzw;
	MaskWater(alpha, i.screenPos, posWorld);

	clip(alpha - 0.006);

	WATER_SETUP_ADD_1(i, s)
	FRAGMENT_SETUP_FWDADD(s)
	WaterFragmentSetupPost(s.normalWorld, i.screenPos);

	UnityLight light = AdditiveLight (s.normalWorld, IN_LIGHTDIR_FWDADD(i), LIGHT_ATTENUATION(i));
	UnityIndirect noIndirect = ZeroIndirect ();

	half4 c = UNITY_BRDF_PBS (s.diffColor, s.specColor, s.oneMinusReflectivity, s.oneMinusRoughness, 0.0, s.normalWorld, -s.eyeVec, posWorld, light, noIndirect, 1);
	
	UNITY_APPLY_FOG_COLOR(i.fogCoord, c.rgb, half4(0,0,0,0)); // fog towards black in additive pass

	return OutputForward (c, alpha);
}

#endif
			
#endif // UNITY_STANDARD_CORE_INCLUDED
