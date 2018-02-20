using UnityEngine;
using UnityEditor;
using PlayWay.Water;

namespace PlayWay.WaterEditor
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(WaterPhysics))]
	public class WaterPhysicsEditor : WaterEditorBase
	{
		public override void OnInspectorGUI()
		{
			var physics = (WaterPhysics)target;

			PropertyField("sampleCount");
			PropertyField("dragCoefficient");
			PropertyField("precision");
			PropertyField("buoyancyIntensity");
			PropertyField("flowIntensity");

			serializedObject.ApplyModifiedProperties();

			EditorGUILayout.Space();

			float totalBuoyancy = physics.GetTotalBuoyancy();
			EditorGUILayout.LabelField(new GUIContent("Gravity Balance", "Buoyancy stated as a percent of the gravity force."), new GUIContent((100.0f * totalBuoyancy / Physics.gravity.magnitude).ToString("0.00") + "%"));
		}
	}
}
