// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "PlayWay Water/Utility/Map Local Displacements"
{
	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile __ _WAVES_FFT
			#pragma multi_compile ____ _WAVES_GERSTNER

			#pragma target 3.0
			
			#include "UnityCG.cginc"
			#include "WaterDisplace.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex	: SV_POSITION;
				float2 uv		: TEXCOORD0;
				float2 worldPos	: TEXCOORD1;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				o.worldPos = v.uv / _LocalMapsCoords.zz - _LocalMapsCoords.xy;
				return o;
			}
			
			half4 frag (v2f i) : SV_Target
			{
				half3 displacement = GetWaterDisplacement(i.worldPos);

				displacement *= tex2D(_LocalSlopeMap, i.uv).w;
				displacement += tex2D(_LocalDisplacementMap, i.uv).xyz * half3(_DisplacementsScale, 1.0, _DisplacementsScale);

				//displacement.y += _SurfaceOffset.y;

				return half4(displacement, 0.0);
			}
			ENDCG
		}
	}
}
