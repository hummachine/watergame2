using UnityEngine;
using UnityEditor;
using PlayWay.Water;

namespace PlayWay.WaterEditor
{
	[CustomEditor(typeof(WaterVolumeProbe))]
	public class WaterVolumeProbeEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			var target = (WaterVolumeProbe)this.target;

			GUI.enabled = false;
			EditorGUILayout.ObjectField("Currently in: ", target.CurrentWater, typeof(PlayWay.Water.Water), true);
			GUI.enabled = true;
		}
	}
}
