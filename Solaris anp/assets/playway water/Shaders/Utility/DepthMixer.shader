// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "PlayWay Water/Depth/CopyMix" {

	CGINCLUDE
		#include "UnityCG.cginc"

		struct appdata_t
		{
			float4 vertex : POSITION;
			half2 texcoord : TEXCOORD0;
		};

		struct v2f
		{
			float4 vertex : SV_POSITION;
			half2 texcoord : TEXCOORD0;
		};

		sampler2D_float _CameraDepthTexture;
		sampler2D_float _WaterDepthTexture;

		v2f vert(appdata_t v)
		{
			v2f o;
			o.vertex = UnityObjectToClipPos(v.vertex);
			o.texcoord = v.texcoord.xy;
			return o;
		}

#if SHADER_TARGET >= 40 && !defined(UNITY_MIGHT_NOT_HAVE_DEPTH_TEXTURE)		// probably all 4.0 targets have depth textures, but whatever
		float frag(v2f i) : SV_Depth
		{
			return tex2D(_CameraDepthTexture, i.texcoord);
		}

		float fragMix(v2f i) : SV_Depth
		{
			float d1 = tex2D(_CameraDepthTexture, i.texcoord);
			float d2 = tex2D(_WaterDepthTexture, i.texcoord);

#if UNITY_VERSION < 550
			return min(d1, d2);
#else
			return max(d1, d2);
#endif
		}
#else
		float4 frag(v2f i) : SV_Target
		{
			return tex2D(_CameraDepthTexture, i.texcoord);
		}

		float4 fragMix(v2f i) : SV_Target
		{
			float d1 = tex2D(_CameraDepthTexture, i.texcoord);
			float d2 = tex2D(_WaterDepthTexture, i.texcoord);

#if UNITY_VERSION < 550
			return min(d1, d2);
#else
			return max(d1, d2);
#endif
		}
#endif

		sampler2D _LightTexture0;

		float4 fragShadows(v2f i) : SV_Target
		{
			return tex2D(_LightTexture0, i.texcoord);
		}
	ENDCG

	SubShader
	{
		Pass
		{
 			ZTest Always Cull Off ZWrite On ColorMask 0

			CGPROGRAM

			#pragma target 4.0

			#pragma vertex vert
			#pragma fragment frag

			ENDCG
		}

		Pass
		{
			ZTest Always Cull Off ZWrite On ColorMask 0

			CGPROGRAM

			#pragma target 4.0

			#pragma vertex vert
			#pragma fragment fragMix

			ENDCG
		}

		Pass
		{
			ZTest Always Cull Off ZWrite On ColorMask 0

			CGPROGRAM

			#pragma target 4.0

			#pragma vertex vert
			#pragma fragment fragShadows

			ENDCG
		}
	}

	SubShader
	{
		Pass
		{
 			ZTest Always Cull Off ZWrite Off

			CGPROGRAM

			#pragma target 2.0

			#pragma vertex vert
			#pragma fragment frag

			ENDCG
		}

		Pass
		{
			ZTest Always Cull Off ZWrite Off

			CGPROGRAM

			#pragma target 2.0

			#pragma vertex vert
			#pragma fragment fragMix

			ENDCG
		}

		Pass
		{
			ZTest Always Cull Off ZWrite Off

			CGPROGRAM

			#pragma target 2.0

			#pragma vertex vert
			#pragma fragment fragShadows

			ENDCG
		}
	}
	Fallback Off 
}
