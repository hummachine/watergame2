Shader "PlayWay Water/Depth/Water Depth"
{
	Properties
	{
		_SubtractiveMask("", 2D) = "black" {}
		_AdditiveMask("", 2D) = "black" {}
		_DisplacedHeightMaps("", 2D) = "black" {}
		_WaterTileSize("Heightmap Tile Size", Vector) = (180.0, 18.0, 600.0, 1800.0)
		_SurfaceOffset("", Vector) = (0.0, 0.0, 0.0, 0.0)
		_WaterId ("", Vector) = (128, 256, 0, 0)
	}

	/*
	 * WATER
	 */
	SubShader
	{
		Tags { "CustomType"="Water" }
		Cull Off
		ZTest Always
		ZWrite On
		
		Pass
		{
			Fog { Mode Off }

			CGPROGRAM

			#pragma target 5.0
			#pragma only_renderers d3d11

			#if UNITY_CAN_COMPILE_TESSELLATION
				#pragma vertex tessvert_surf
				#pragma fragment frag

				#pragma hull hs_surf
				#pragma domain ds_surf
			#endif
			
			#pragma multi_compile __ _WAVES_FFT
			#pragma multi_compile ____ _WAVES_GERSTNER
			#pragma multi_compile _____ _BOUNDED_WATER
			#pragma multi_compile _______ _WAVES_ALIGN
			#pragma multi_compile ___ _TRIANGLES

			#define _DEPTH 1
			
			#define BASIC_INPUTS 1
			#define POST_TESS_VERT vert
			#define TESS_OUTPUT v2f

			#include "Depth.cginc"
			#include "WaterTessellation.cginc"

			ENDCG
		}
	}

	SubShader
	{
		Tags { "CustomType"="Water" }
		Cull Off
		ZTest Less
		ZWrite On
		
		Pass
		{
			Fog { Mode Off }

			CGPROGRAM

			#pragma multi_compile __ _WAVES_FFT
			#pragma multi_compile ____ _WAVES_GERSTNER
			#pragma multi_compile _____ _BOUNDED_WATER
			#pragma multi_compile _______ _WAVES_ALIGN

			#define _DEPTH 1

			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag

			#include "Depth.cginc"

			ENDCG
		}
	}

	SubShader
	{
		Tags{ "CustomType" = "Water" }
		Cull Off
		ZTest Less
		ZWrite On
		
		Pass
		{
			Fog{ Mode Off }

			CGPROGRAM

			#pragma multi_compile ____ _WAVES_GERSTNER
			#pragma multi_compile _____ _BOUNDED_WATER

			#define _DEPTH 1

			#pragma target 2.0
			#pragma vertex vert
			#pragma fragment frag

			#include "Depth.cginc"

			ENDCG
		}
	}

	/*
	 * WATER VOLUME
	 */
	SubShader
	{
		Tags { "CustomType"="WaterVolume" }
		Cull Off
		ZTest Less
		ZWrite On
		
		Pass
		{
			Fog { Mode Off }

			CGPROGRAM

			#pragma multi_compile __ _WAVES_FFT
			#pragma multi_compile ____ _WAVES_GERSTNER

			#define _DISPLACED_VOLUME 1
			#define _CLIP_ABOVE 1
			#define _DEPTH 1

			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag

			#include "Depth.cginc"

			ENDCG
		}
	}

	SubShader
	{
		Tags{ "CustomType" = "WaterVolume" }
		Cull Off
		ZTest Less
		ZWrite On
		

		Pass
		{
			Fog{ Mode Off }

			CGPROGRAM

			#pragma multi_compile ____ _WAVES_GERSTNER

			#define _DISPLACED_VOLUME 1
			#define _CLIP_ABOVE 1
			#define _DEPTH 1

			#pragma target 2.0
			#pragma vertex vert
			#pragma fragment frag

			#include "Depth.cginc"

			ENDCG
		}
	}
}