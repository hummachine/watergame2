Shader "PlayWay Water/Particles/Particles"
{
	Properties
	{

	}

	SubShader
	{
		Cull Off ZWrite Off ZTest Always
		Blend One One

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#pragma target 5.0
			
			#include "WaveParticles.cginc"

			ENDCG
		}
	}

	SubShader
	{
		Cull Off ZWrite Off ZTest Always
		Blend One One

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#pragma target 3.0
			
			#include "WaveParticles.cginc"

			ENDCG
		}
	}
}
