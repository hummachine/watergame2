using UnityEngine;
using UnityEditor;

namespace PlayWay.Water
{
	[CustomEditor(typeof(WavesParticleSystem))]
	public class WavesParticleSystemEditor : WaterEditor
	{
		public override void OnInspectorGUI()
		{
			var target = (WavesParticleSystem)this.target;

			PropertyField("maxParticles");
			PropertyField("maxParticlesPerTile");
			PropertyField("prewarmTime");
			PropertyField("timePerFrame");

			if(Application.isPlaying)
			{
				GUI.enabled = false;
				EditorGUILayout.IntField("Particle Count", target.ParticleCount);
				GUI.enabled = true;
			}

			serializedObject.ApplyModifiedProperties();
		}

		public override bool RequiresConstantRepaint()
		{
			return Application.isPlaying;
		}
	}
}
