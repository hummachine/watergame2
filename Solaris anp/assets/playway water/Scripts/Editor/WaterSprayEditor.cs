using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;

namespace PlayWay.Water
{
	[CustomEditor(typeof(WaterSpray))]
	public class WaterSprayEditor : WaterEditorBase
	{
		private AnimBool profileFoldout = new AnimBool(false);

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			UpdateGUI();

			useFoldouts = true;

			if(BeginGroup("Profiling", profileFoldout))
			{
				var spray = target as WaterSpray;

				GUILayout.Label("Draw Calls: " + Mathf.CeilToInt(spray.MaxParticles / 65535.0f));
				GUILayout.Label("Spawned Particles: " + spray.SpawnedParticles);
			}

			EndGroup();
		}
	}
}
