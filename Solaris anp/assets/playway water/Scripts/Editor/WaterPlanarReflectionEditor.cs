using UnityEditor;

namespace PlayWay.Water
{
	[CustomEditor(typeof(WaterPlanarReflection))]
	public class WaterPlanarReflectionEditor : WaterEditorBase
	{
		public override void OnInspectorGUI()
		{
			UpdateGUI();
			
			PropertyField("reflectSkybox", "Reflect Skybox");
			PropertyField("reflectionMask", "Reflection Mask");
			PropertyField("downsample", "Downsample");
			PropertyField("retinaDownsample", "Downsample (Retina)");
			//PropertyField("clipPlaneOffset", "Clip Plane Offset");
			PropertyField("highQuality", "High Quality");

			serializedObject.ApplyModifiedProperties();
		}
	}
}
