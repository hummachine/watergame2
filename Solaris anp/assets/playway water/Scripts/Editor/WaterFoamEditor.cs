using UnityEngine;
using UnityEditor;

namespace PlayWay.Water
{
	[CustomEditor(typeof(WaterFoam))]
	public class WaterFoamEditor : WaterEditorBase
	{
		public override void OnInspectorGUI()
		{
			var target = (WaterFoam)this.target;

			bool isFoamLocal = target.GetComponent<WaterOverlays>() != null;

			if(isFoamLocal)
			{
				EditorGUILayout.LabelField(new GUIContent("Simulation space"), new GUIContent("Local", "Simulation will be performed in each camera local space. Features:\n• High quality\n• No noticeable pattern\n• Allows dynamic foam\n• Limited range (no distant foam)\n• A bit lower performance\n\nTo switch to simulation based on FFT wave tiles, remove WaterOverlays component."));
			}
			else
			{
				EditorGUILayout.LabelField(new GUIContent("Simulation space"), new GUIContent("Wave Tiles", "Simulation will be performed on FFT wave tiles Features:\n• Less precise\n• Occasionally may produce noticeable pattern\n• Displays distant foam\n• High performance\n\nTo switch to simulation based in each camera local space, add WaterOverlays component."));
				PropertyField("supersampling");
			}

			serializedObject.ApplyModifiedProperties();
		}

		enum SimulationSpace
		{
			WaveTiles,
			Local
		}
	}
}
