Shader "PlayWay Water/Variations/Water _ALPHABLEND_ON _CUBEMAP_REFLECTIONS _INCLUDE_SLOPE_VARIANCE _NORMALMAP _PLANAR_REFLECTIONS_HQ _VOLUMETRIC_LIGHTING _WATER_FOAM_WS _WATER_REFRACTION _WAVES_FFT"
{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo", 2D) = "white" {}

		_DepthColor("Depth Color", Color) = (0.0, 0.012, 0.05)
		
		_Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

		_Glossiness("Smoothness", Range(0.0, 1.0)) = 0.5
		_SpecColor("Specular", Color) = (0.2,0.2,0.2)

		_BumpScale("Bump Scale", Vector) = (1.0, 1.0, 0.0, 0.0)
		_BumpMap("Normal Map", 2D) = "bump" {}

		_DisplacementNormalsIntensity ("Displacement Normals Intensity", Float) = 1.4
		_GlobalNormalMap ("", 2D) = "black" {}
		_GlobalNormalMap1 ("", 2D) = "black" {}
		_GlobalNormalMap2 ("", 2D) = "black" {}
		_GlobalNormalMap3 ("", 2D) = "black" {}
		_GlobalDisplacementMap ("", 2D) = "black" {}
		_GlobalDisplacementMap1 ("", 2D) = "black" {}
		_GlobalDisplacementMap2 ("", 2D) = "black" {}
		_GlobalDisplacementMap3 ("", 2D) = "black" {}

		_DisplacementsScale ("Horizontal Displacement Scale", Float) = 1.0

		_OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
		_OcclusionMap("Occlusion", 2D) = "white" {}

		_EmissionColor("Color", Color) = (0,0,0)
		_EmissionMap("Emission", 2D) = "white" {}
		
		_SubtractiveMask("", 2D) = "black" {}
		_AdditiveMask("", 2D) = "black" {}

		_DetailAlbedoMap("Detail Albedo x2", 2D) = "grey" {}
		_DetailNormalMapScale("Scale", Float) = 1.0
		_DetailNormalMap("Normal Map", 2D) = "bump" {}

		_DetailFadeFactor("", Float) = 2

		_PlanarReflectionTex ("Planar Reflection", 2D) = "black" {}
		_PlanarReflectionPack("Planar reflection (distortion, intensity, offset Y, unused)", Vector) = (1.0, 0.45, -0.3, 0.0)

		_LightSmoothnessMul("Light Smoothness Multiplier", Float) = 1.0
		_WrapSubsurfaceScatteringPack ("Wrap SSS", Vector) = (0.2, 0.833333, 0.5, 0.66666)
		_SpecularFresnelBias ("Specular Fresnel Bias", Float) = 0.02041
		_RefractionDistortion ("Refraction Distortion", Float) = 0.55
		_RefractionMaxDepth ("Refraction Max Depth", Float) = -1.0

		_WaterTileSize ("Heightmap Tile Size", Vector) = (180.0, 18.0, 600.0, 1800.0)
		_WaterTileOffsets ("Tile Offsets", Vector) = (0.0, 0.0, 0.0, 0.0)
		_WaterTileSizeScales ("", Vector) = (0.63241, 0.113151, 3.165131, 10.265131)

		_Foam ("Foam (intensity, cutoff)", Vector) = (0.1, 0.375, 0.0, 0.0)
		_FoamTex ("Foam texture ", 2D) = "black" {}
		_FoamNormalMap ("Foam Normal Map", 2D) = "bump" {}
		_FoamNormalScale ("Foam Normal Scale", Float) = 2.2
		_FoamTiling ("Foam Tiling", Vector) = (5.4, 5.4, 1.0, 1.0)
		_FoamSpecularColor("Foam Specular Color", Color) = (1, 1, 1, 1)
		_EdgeBlendFactorInv ("Edge Blend Factor", Float) = 0.3333

		_FoamMapWS ("", 2D) = "black" {} 
		_AbsorptionColor ("", Color) = (0.35, 0.04, 0.001, 1.0)
		_ReflectionColor ("", Color) = (1.0, 1.0, 1.0, 1.0)
		_LocalDisplacementMap("", 2D) = "black" {}
		_LocalSlopeMap("", 2D) = "black" {}
		_DisplacedHeightMaps("", 2D) = "black" {}

		_SubsurfaceScatteringPack("Subsurface Scattering", Vector) = (1.0, 0.15, 1.65, 0)
		//_LocalMapsCoords("", Vector) = (0.0, 1.0, 0.0, 1.0)

		_SlopeVariance("", 3D) = "black" {}

		_TesselationFactor ("Tesselation Factor", Float) = 14
		_MaxDisplacement ("", Float) = 10
		_SurfaceOffset ("", Vector) = (0.0, 0.0, 0.0, 1.0)

		_WaterId ("", Vector) = (128, 256, 0, 0)
		_WaterStencilId ("", Float) = 0
		_WaterStencilIdInv ("", Float) = 0

		// -- gerstner
		_GerstnerOrigin("", Vector) = (0, 0, 0, 0)

		_GrAmp0("", Vector) = (0, 0, 0, 0)
		_GrOff0("", Vector) = (0, 0, 0, 0)
		_GrFrq0("", Vector) = (0, 0, 0, 0)
		_GrAB0("", Vector) = (0, 0, 0, 0)
		_GrCD0("", Vector) = (0, 0, 0, 0)

		_GrAmp1("", Vector) = (0, 0, 0, 0)
		_GrOff1("", Vector) = (0, 0, 0, 0)
		_GrFrq1("", Vector) = (0, 0, 0, 0)
		_GrAB1("", Vector) = (0, 0, 0, 0)
		_GrCD1("", Vector) = (0, 0, 0, 0)

		_GrAmp2("", Vector) = (0, 0, 0, 0)
		_GrOff2("", Vector) = (0, 0, 0, 0)
		_GrFrq2("", Vector) = (0, 0, 0, 0)
		_GrAB2("", Vector) = (0, 0, 0, 0)
		_GrCD2("", Vector) = (0, 0, 0, 0)

		_GrAmp3("", Vector) = (0, 0, 0, 0)
		_GrOff3("", Vector) = (0, 0, 0, 0)
		_GrFrq3("", Vector) = (0, 0, 0, 0)
		_GrAB3("", Vector) = (0, 0, 0, 0)
		_GrCD3("", Vector) = (0, 0, 0, 0)

		_GrAmp4("", Vector) = (0, 0, 0, 0)
		_GrOff4("", Vector) = (0, 0, 0, 0)
		_GrFrq4("", Vector) = (0, 0, 0, 0)
		_GrAB4("", Vector) = (0, 0, 0, 0)
		_GrCD4("", Vector) = (0, 0, 0, 0)
		// --

		// Blending state
		[HideInInspector] _Cull ("_Cull", Float) = 2
		[HideInInspector] _Mode ("__mode", Float) = 0.0
		[HideInInspector] _SrcBlend ("__src", Float) = 1.0
		[HideInInspector] _DstBlend ("__dst", Float) = 0.0
	}

	CGINCLUDE
		#define UNITY_SETUP_BRDF_INPUT SpecularSetup
	ENDCG


	//
	// HIGH-QUALITY + TESSELATION
	//
	SubShader
	{
		Tags { "RenderType" = "Opaque" "PerformanceChecks" = "False" "Queue" = "Transparent-1" "CustomType" = "Water" }
		LOD 300

		GrabPass { "_RefractionTex" }

		// ------------------------------------------------------------------
		//  Base forward pass (directional light, emission, lightmaps, ...)
		Pass
		{
			Name "FORWARD"
			Tags { "LightMode" = "ForwardBase" }

			Blend [_SrcBlend][_DstBlend]
			ZWrite On
			Cull [_Cull]
			ZTest Less

			

			CGPROGRAM
			#pragma target 5.0
			#pragma only_renderers d3d11

			#define _ALPHABLEND_ON 1
			#define _CUBEMAP_REFLECTIONS 1
			#define _INCLUDE_SLOPE_VARIANCE 1
			#define _NORMALMAP 1
			#define _PLANAR_REFLECTIONS_HQ 1
			#define _VOLUMETRIC_LIGHTING 1
			#define _WATER_FOAM_WS 1
			#define _WATER_REFRACTION 1

			#pragma multi_compile _WAVES_FFT

			#pragma multi_compile _WATER_FRONT _WATER_BACK

			#pragma multi_compile_fwdbase
			#pragma multi_compile_fog

			#pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE SHADOWS_SCREEN SHADOWS_NATIVE SHADOWS_NONATIVE SHADOWS_CUBE SHADOWS_DEPTH SHADOWS_SOFT
			
			#if UNITY_CAN_COMPILE_TESSELLATION
				#pragma vertex tessvert_surf
				#pragma fragment fragForwardBase

				#pragma hull hs_surf
				#pragma domain ds_surf
			#endif

			#define POST_TESS_VERT vertForwardBase
			#define TESS_OUTPUT VertexOutputForwardBase

			#include "UnityStandardCore.cginc"
			#include "WaterTessellation.cginc"
			
			ENDCG
		}
		// ------------------------------------------------------------------
		//  Additive forward pass (one light per pass)
		Pass
		{
			Name "FORWARD_DELTA"
			Tags { "LightMode" = "ForwardAdd" }
			Blend [_SrcBlend] One
			Fog { Color (0,0,0,0) } // in additive pass fog should be black
			ZWrite Off
			Cull [_Cull]
			ZTest LEqual

			

			CGPROGRAM
			#pragma target 5.0
			#pragma only_renderers d3d11
			
			#define _ALPHABLEND_ON 1
			#define _CUBEMAP_REFLECTIONS 1
			#define _INCLUDE_SLOPE_VARIANCE 1
			#define _NORMALMAP 1
			#define _PLANAR_REFLECTIONS_HQ 1
			#define _VOLUMETRIC_LIGHTING 1
			#define _WATER_FOAM_WS 1
			#define _WATER_REFRACTION 1

			#pragma multi_compile _WAVES_FFT

			#pragma multi_compile _WATER_FRONT _WATER_BACK
			
			#pragma multi_compile_fwdadd
			#pragma multi_compile_fog

			#if UNITY_CAN_COMPILE_TESSELLATION
				#pragma vertex tessvert_surf
				#pragma fragment fragForwardAdd

				#pragma hull hs_surf
				#pragma domain ds_surf
			#endif

			#define POST_TESS_VERT vertForwardAdd
			#define TESS_OUTPUT VertexOutputForwardAdd

			#include "UnityStandardCore.cginc"
			#include "WaterTessellation.cginc"

			ENDCG
		}
		// ------------------------------------------------------------------
		//  Shadow rendering pass
		Pass {
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }
			
			ZWrite On ZTest LEqual
			Cull Off

			CGPROGRAM
			#pragma target 5.0
			#pragma only_renderers d3d11

			#define _ALPHABLEND_ON 1
			#define _CUBEMAP_REFLECTIONS 1
			#define _INCLUDE_SLOPE_VARIANCE 1
			#define _NORMALMAP 1
			#define _PLANAR_REFLECTIONS_HQ 1
			#define _VOLUMETRIC_LIGHTING 1
			#define _WATER_FOAM_WS 1
			#define _WATER_REFRACTION 1

			#pragma multi_compile _WAVES_FFT

			#pragma multi_compile_shadowcaster

			#if UNITY_CAN_COMPILE_TESSELLATION
				#pragma vertex tessvert_surf
				#pragma fragment fragShadowCaster

				#pragma hull hs_surf
				#pragma domain ds_surf
			#endif

			#define POST_TESS_VERT vertShadowCaster
			#define TESS_OUTPUT VertexOutputShadowCaster
			#define _SHADOWS_PASS 1

			#include "UnityStandardShadow.cginc"
			#include "WaterTessellation.cginc"

			ENDCG
		}
		// ------------------------------------------------------------------
		//  Depth rendering pass
		Pass {
			Name "Depth"
			Tags { "LightMode" = "VertexLMRGBM" }
			
			ZWrite On
			ZTest LEqual
			Cull Off

			

			CGPROGRAM
			#pragma target 5.0
			#pragma only_renderers d3d11

			#define _ALPHABLEND_ON 1
			#define _CUBEMAP_REFLECTIONS 1
			#define _INCLUDE_SLOPE_VARIANCE 1
			#define _NORMALMAP 1
			#define _PLANAR_REFLECTIONS_HQ 1
			#define _VOLUMETRIC_LIGHTING 1
			#define _WATER_FOAM_WS 1
			#define _WATER_REFRACTION 1

			#pragma multi_compile _WAVES_FFT

			#define SHADOWS_DEPTH 1

			#if UNITY_CAN_COMPILE_TESSELLATION
				#pragma vertex tessvert_surf
				#pragma fragment fragDepth

				#pragma hull hs_surf
				#pragma domain ds_surf
			#endif

			#define POST_TESS_VERT vertDepth
			#define TESS_OUTPUT VertexOutputDepth
			#define _SHADOWS_PASS 1

			#include "UnityStandardShadow.cginc"
			#include "WaterTessellation.cginc"

			ENDCG
		}

		// ------------------------------------------------------------------
		// Extracts information for lightmapping, GI (emission, albedo, ...)
		// This pass it not used during regular rendering.
		//Pass
		//{
		//	Name "META" 
		//	Tags { "LightMode"="Meta" }

		//	Cull [_Cull]

		//	CGPROGRAM
		//	#pragma vertex vert_meta
		//	#pragma fragment frag_meta

		//	//#pragma shader_feature _EMISSION

		//	#include "UnityStandardMeta.cginc"
		//	ENDCG
		//}
	}

	//
	// HIGH-QUALITY
	//
	SubShader
	{
		Tags { "RenderType"="Opaque" "PerformanceChecks"="False" "Queue"="Transparent-1" "CustomType"="Water" }
		LOD 300
		
		GrabPass { "_RefractionTex" }

		// ------------------------------------------------------------------
		//  Base forward pass (directional light, emission, lightmaps, ...)
		Pass
		{
			Name "FORWARD" 
			Tags { "LightMode" = "ForwardBase" }

			Blend [_SrcBlend] [_DstBlend]
			ZWrite On
			Cull [_Cull]
			ZTest Less

			

			CGPROGRAM
			#pragma target 3.0
			// TEMPORARY: GLES2.0 temporarily disabled to prevent errors spam on devices without textureCubeLodEXT
			#pragma exclude_renderers gles
			
			// -------------------------------------
					
			#define _ALPHABLEND_ON 1
			#define _CUBEMAP_REFLECTIONS 1
			#define _INCLUDE_SLOPE_VARIANCE 1
			#define _NORMALMAP 1
			#define _PLANAR_REFLECTIONS_HQ 1
			#define _VOLUMETRIC_LIGHTING 1
			#define _WATER_FOAM_WS 1
			#define _WATER_REFRACTION 1

			#pragma multi_compile _WAVES_FFT

			#pragma multi_compile _WATER_FRONT _WATER_BACK
			
			#pragma multi_compile_fwdbase
			#pragma multi_compile_fog
			
			#pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE SHADOWS_SCREEN SHADOWS_NATIVE SHADOWS_NONATIVE SHADOWS_CUBE SHADOWS_DEPTH SHADOWS_SOFT

			#pragma vertex vertForwardBase
			#pragma fragment fragForwardBase

			#include "UnityStandardCore.cginc"
			

			ENDCG
		}
		// ------------------------------------------------------------------
		//  Additive forward pass (one light per pass)
		Pass
		{
			Name "FORWARD_DELTA"
			Tags { "LightMode" = "ForwardAdd" }
			Blend [_SrcBlend] One
			Fog { Color (0,0,0,0) } // in additive pass fog should be black
			ZWrite Off
			Cull [_Cull]
			ZTest LEqual

			

			CGPROGRAM
			#pragma target 3.0
			// GLES2.0 temporarily disabled to prevent errors spam on devices without textureCubeLodEXT
			#pragma exclude_renderers gles

			// -------------------------------------

			
			#define _ALPHABLEND_ON 1
			#define _CUBEMAP_REFLECTIONS 1
			#define _INCLUDE_SLOPE_VARIANCE 1
			#define _NORMALMAP 1
			#define _PLANAR_REFLECTIONS_HQ 1
			#define _VOLUMETRIC_LIGHTING 1
			#define _WATER_FOAM_WS 1
			#define _WATER_REFRACTION 1

			#pragma multi_compile _WAVES_FFT

			#pragma multi_compile _WATER_FRONT _WATER_BACK
			
			#pragma multi_compile_fwdadd
			#pragma multi_compile_fog
			
			#pragma vertex vertForwardAdd
			#pragma fragment fragForwardAdd

			#include "UnityStandardCore.cginc"

			ENDCG
		}
		// ------------------------------------------------------------------
		//  Shadow rendering pass
		Pass {
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }
			
			ZWrite On ZTest LEqual
			Cull Off

			CGPROGRAM
			#pragma target 3.0
			// TEMPORARY: GLES2.0 temporarily disabled to prevent errors spam on devices without textureCubeLodEXT
			#pragma exclude_renderers gles
			
			// -------------------------------------


			#define _ALPHABLEND_ON 1
			#define _CUBEMAP_REFLECTIONS 1
			#define _INCLUDE_SLOPE_VARIANCE 1
			#define _NORMALMAP 1
			#define _PLANAR_REFLECTIONS_HQ 1
			#define _VOLUMETRIC_LIGHTING 1
			#define _WATER_FOAM_WS 1
			#define _WATER_REFRACTION 1

			#pragma multi_compile _WAVES_FFT

			#pragma multi_compile_shadowcaster

			#pragma vertex vertShadowCaster
			#pragma fragment fragShadowCaster

			#include "UnityStandardShadow.cginc"

			ENDCG
		}

			// ------------------------------------------------------------------
			//  Depth rendering pass
		Pass{
			Name "Depth"
			Tags{ "LightMode" = "VertexLMRGBM" }

			ZWrite On
			ZTest LEqual
			Cull Off

			

			CGPROGRAM
			#pragma target 3.0

			#define _ALPHABLEND_ON 1
			#define _CUBEMAP_REFLECTIONS 1
			#define _INCLUDE_SLOPE_VARIANCE 1
			#define _NORMALMAP 1
			#define _PLANAR_REFLECTIONS_HQ 1
			#define _VOLUMETRIC_LIGHTING 1
			#define _WATER_FOAM_WS 1
			#define _WATER_REFRACTION 1

			#pragma multi_compile _WAVES_FFT

			#define SHADOWS_DEPTH 1

			#pragma vertex vertDepth
			#pragma fragment fragDepth

			#define _SHADOWS_PASS 1

			#include "UnityStandardShadow.cginc"

			ENDCG
		}

		// ------------------------------------------------------------------
		// Extracts information for lightmapping, GI (emission, albedo, ...)
		// This pass it not used during regular rendering.
		//Pass
		//{
		//	Name "META" 
		//	Tags { "LightMode"="Meta" }

		//	Cull [_Cull]

		//	CGPROGRAM
		//	#pragma vertex vert_meta
		//	#pragma fragment frag_meta

		//	//#pragma shader_feature _EMISSION
		//	#pragma shader_feature _SPECGLOSSMAP
		//	#pragma shader_feature ___ _DETAIL_MULX2

		//	#include "UnityStandardMeta.cginc"
		//	ENDCG
		//}
	}

	//
	// LOW-QUALITY
	//
	SubShader
	{
		Tags { "RenderType"="Opaque" "PerformanceChecks"="False" "Queue"="Geometry" "CustomType"="Water" }
		LOD 50
		
		// ------------------------------------------------------------------
		//  Base forward pass (directional light, emission, lightmaps, ...)
		Pass
		{
			Name "FORWARD" 
			Tags { "LightMode" = "ForwardBase" }

			Blend [_SrcBlend] [_DstBlend]
			ZWrite On
			Cull [_Cull]
			ZTest Less

			

			CGPROGRAM
			#pragma target 2.0

			#pragma exclude_renderers d3d11
			
			#define _ALPHABLEND_ON 1
			#define _CUBEMAP_REFLECTIONS 1
			#define _INCLUDE_SLOPE_VARIANCE 1
			#define _NORMALMAP 1
			#define _PLANAR_REFLECTIONS_HQ 1
			#define _VOLUMETRIC_LIGHTING 1
			#define _WATER_FOAM_WS 1
			#define _WATER_REFRACTION 1

			#pragma multi_compile _WAVES_FFT

			#pragma multi_compile _WATER_FRONT _WATER_BACK
			
			#pragma multi_compile_fwdbase
			#pragma multi_compile_fog

			#pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE SHADOWS_SCREEN SHADOWS_NATIVE SHADOWS_NONATIVE SHADOWS_CUBE SHADOWS_DEPTH SHADOWS_SOFT
	
			#pragma vertex vertForwardBase
			#pragma fragment fragForwardBase

			#include "UnityStandardCore.cginc"
			
			ENDCG
		}

		// ------------------------------------------------------------------
		//  Shadow rendering pass
		Pass {
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }
			
			ZWrite On ZTest LEqual
			Cull Off

			CGPROGRAM
			#pragma target 2.0
			
			#pragma exclude_renderers d3d11
			
			#define _ALPHABLEND_ON 1
			#define _CUBEMAP_REFLECTIONS 1
			#define _INCLUDE_SLOPE_VARIANCE 1
			#define _NORMALMAP 1
			#define _PLANAR_REFLECTIONS_HQ 1
			#define _VOLUMETRIC_LIGHTING 1
			#define _WATER_FOAM_WS 1
			#define _WATER_REFRACTION 1

			#pragma multi_compile _WAVES_FFT

			#pragma multi_compile_shadowcaster

			#pragma vertex vertShadowCaster
			#pragma fragment fragShadowCaster

			#include "UnityStandardShadow.cginc"

			ENDCG
		}

		// ------------------------------------------------------------------
		// Extracts information for lightmapping, GI (emission, albedo, ...)
		// This pass it not used during regular rendering.
		//Pass
		//{
		//	Name "META" 
		//	Tags { "LightMode"="Meta" }

		//	Cull Back

		//	CGPROGRAM
		//	#pragma vertex vert_meta
		//	#pragma fragment frag_meta

		//	//#pragma shader_feature _EMISSION
		//	#pragma shader_feature _SPECGLOSSMAP

		//	#include "UnityStandardMeta.cginc"
		//	ENDCG
		//}
	}

	//FallBack "VertexLit"
	//CustomEditor "StandardShaderGUI"
}
