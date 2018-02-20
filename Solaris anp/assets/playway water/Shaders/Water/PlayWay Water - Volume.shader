Shader "PlayWay Water/Standard Volume"
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

		_WaterTileSize ("Heightmap Tile Size", Vector) = (180.0, 18.0, 600.0, 1800.0)
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

		_TesselationFactor("Tesselation Factor", Float) = 14
		_MaxDisplacement("", Float) = 10
		_SurfaceOffset ("", Vector) = (0.0, 0.0, 0.0, 0.0)

		_WaterId ("", Vector) = (128, 256, 0, 0)
		_WaterStencilId("", Float) = 0
		_WaterStencilIdInv("", Float) = 0

		// Blending state
		[HideInInspector] _Cull ("_Cull", Float) = 2
		[HideInInspector] _Mode("__mode", Float) = 0.0
		[HideInInspector] _SrcBlend("__src", Float) = 1.0
		[HideInInspector] _DstBlend("__dst", Float) = 0.0
	}

	CGINCLUDE
		#define UNITY_SETUP_BRDF_INPUT SpecularSetup
	ENDCG

	/*
	 * HIGH-QUALITY
	 */
	SubShader
	{
		Tags { "RenderType"="Opaque" "PerformanceChecks"="False" "Queue"="Transparent" "CustomType"="WaterVolume" }
		LOD 300
		
		GrabPass { "_RefractionTex2" }

		// ------------------------------------------------------------------
		//  Base forward pass (directional light, emission, lightmaps, ...)
		Pass
		{
			Name "FORWARD" 
			Tags { "LightMode" = "ForwardBase" }

			//Blend [_SrcBlend] [_DstBlend]
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite On
			Cull [_Cull]
			ZTest LEqual

			// PLACE_CODE_HERE
			
			CGPROGRAM
			#pragma target 3.0
			// TEMPORARY: GLES2.0 temporarily disabled to prevent errors spam on devices without textureCubeLodEXT
			#pragma exclude_renderers gles
			
			// -------------------------------------
					
			#define PLACE_KEYWORDS_HERE
			#define _DISPLACED_VOLUME 1

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
			ZTest LEqual

			// PLACE_CODE_HERE
			
			CGPROGRAM
			#pragma target 3.0
			// GLES2.0 temporarily disabled to prevent errors spam on devices without textureCubeLodEXT
			#pragma exclude_renderers gles

			// -------------------------------------
			
			#define PLACE_KEYWORDS_HERE
			#define _DISPLACED_VOLUME 1
			
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
			Cull [_Cull]

			CGPROGRAM
			#pragma target 3.0
			// TEMPORARY: GLES2.0 temporarily disabled to prevent errors spam on devices without textureCubeLodEXT
			#pragma exclude_renderers gles
			
			// -------------------------------------
			
			#define PLACE_KEYWORDS_HERE
			#pragma multi_compile_shadowcaster

			#define _DISPLACED_VOLUME 1

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
			Cull [_Cull]

			// PLACE_CODE_HERE

			CGPROGRAM
			#pragma target 3.0
			#pragma only_renderers d3d11

			#define PLACE_KEYWORDS_HERE
			#define _DISPLACED_VOLUME 1
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

		//	Cull Front

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

	/*
	 * MEDIUM-QUALITY
	 */
	/*SubShader
	{
		Tags { "RenderType"="Opaque" "PerformanceChecks"="False" "Queue"="Geometry" "CustomType"="WaterVolume" }
		LOD 50
		
		// ------------------------------------------------------------------
		//  Base forward pass (directional light, emission, lightmaps, ...)
		Pass
		{
			Name "FORWARD" 
			Tags { "LightMode" = "ForwardBase" }

			Blend One Zero
			ZWrite On
			Cull Front
			ZTest LEqual

			CGPROGRAM
			#pragma target 2.0
			
			#pragma shader_feature _NORMALMAP
			//#pragma shader_feature _ _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
			//#pragma shader_feature _CUBEMAP_REFLECTIONS
			//#pragma shader_feature _WATER_FOAM_WS
			#pragma shader_feature _PROJECTION_GRID

			#define _DISPLACED_VOLUME 1

			#pragma skip_variants SHADOWS_SOFT DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE SHADOWS_SCREEN SHADOWS_NATIVE SHADOWS_NONATIVE SHADOWS_CUBE
			
			#pragma multi_compile_fwdbase
			#pragma multi_compile_fog
	
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

			CGPROGRAM
			#pragma target 2.0
			
			//#pragma shader_feature _ _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
			#pragma multi_compile_shadowcaster

			#define _DISPLACED_VOLUME 1

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

		//	Cull Front

		//	CGPROGRAM
		//	#pragma vertex vert_meta
		//	#pragma fragment frag_meta

		//	//#pragma shader_feature _EMISSION
		//	#pragma shader_feature _SPECGLOSSMAP
		//	#pragma shader_feature ___ _DETAIL_MULX2

		//	#include "UnityStandardMeta.cginc"
		//	ENDCG
		//}
	}*/

	//FallFront "VertexLit"
	//CustomEditor "StandardShaderGUI"
}
