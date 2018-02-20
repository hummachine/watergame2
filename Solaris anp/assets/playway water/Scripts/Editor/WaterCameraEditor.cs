using UnityEditor;
using PlayWay.Water;
using UnityEngine;

namespace PlayWay.WaterEditor
{
	[CustomEditor(typeof(WaterCamera))]
	public class WaterCameraEditor : WaterEditorBase
	{
		public override void OnInspectorGUI()
		{
			var waterCamera = (WaterCamera)target;
			var camera = waterCamera.GetComponent<Camera>();

			PropertyField("geometryType", "Water Geometry");

			PropertyField("renderWaterDepth", "Render Water Depth");
			PropertyField("renderVolumes", "Render Volumes");

			PropertyField("sharedCommandBuffers", "Shared Command Buffers");
			PropertyField("baseEffectsQuality", "Base Effects Quality");

			PropertyField("submersionStateChanged", "Submersion State Changed");

			if(camera.farClipPlane < 100.0f)
				EditorGUILayout.HelpBox("Your camera farClipPlane is set below 100 units. It may be too low for the underwater effects to \"see\" the max depth and they may produce some artifacts.", MessageType.Warning, true);

			if(Application.isPlaying && StaticWaterInteraction.NumStaticWaterInteractions != 0)
			{
				Ray ray = waterCamera.GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);

				if(ray.direction.y < 0.0f)
				{
					float distance = -ray.origin.y / ray.direction.y;
					Vector3 position = ray.origin + ray.direction * distance;

					EditorGUILayout.LabelField("Shore Depth at Cursor", StaticWaterInteraction.GetTotalDepthAt(position.x, position.z).ToString());
				}
			}

			serializedObject.ApplyModifiedProperties();
		}

		/*private void DisplayTexturesInspector()
		{
			var waterCamera = (WaterCamera)target;
		}

		private List<WaterMap> GetWaterMaps()
		{
			var camera = (WaterCamera)target;
			var textures = new List<WaterMap>();

			textures.Add(new WaterMap("WaterCamera - SubtractiveMask", () => camera.SubtractiveMask));

			return textures;
		}*/
	}
}