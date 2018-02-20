using UnityEngine;
using UnityEditor;

namespace PlayWay.Water
{
	[CustomEditor(typeof(WavesEmitter))]
	public class WavesEmitterEditor : WaterEditor
	{
		public override void OnInspectorGUI()
		{
			PropertyField("wavesParticleSystem");

			var wavesSourceProp = PropertyField("wavesSource");
			var wavesSource = (WavesEmitter.WavesSource)wavesSourceProp.enumValueIndex;

			switch(wavesSource)
			{
				case WavesEmitter.WavesSource.CustomWaveFrequency:
				{
					PropertyField("wavelength");
					PropertyField("amplitude");
					PropertyField("emissionRate");
					PropertyField("width");
					PropertyField("waveShapeIrregularity");
					PropertyField("lifetime");
					PropertyField("shoreWaves");
					break;
				}
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
}
