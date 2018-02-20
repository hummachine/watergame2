using UnityEngine;
using UnityEditor;
using PlayWay.Water;

namespace PlayWay.WaterEditor
{
	[CustomEditor(typeof(WaterProjectSettings))]
	public class WaterProjectSettingsEditor : WaterEditorBase
	{
		public override void OnInspectorGUI()
		{
			var waterLayerProp = serializedObject.FindProperty("waterLayer");
			waterLayerProp.intValue = EditorGUILayout.LayerField(new GUIContent(waterLayerProp.displayName, waterLayerProp.tooltip), waterLayerProp.intValue);

			var waterTempLayerProp = serializedObject.FindProperty("waterTempLayer");
			waterTempLayerProp.intValue = EditorGUILayout.LayerField(new GUIContent(waterTempLayerProp.displayName, waterTempLayerProp.tooltip), waterTempLayerProp.intValue);

			var waterCollidersLayerProp = serializedObject.FindProperty("waterCollidersLayer");
			waterCollidersLayerProp.intValue = EditorGUILayout.LayerField(new GUIContent(waterCollidersLayerProp.displayName, waterCollidersLayerProp.tooltip), waterCollidersLayerProp.intValue);

			PropertyField("assetFilesCreation");
			PropertyField("physicsThreads");
			PropertyField("physicsThreadsPriority");
			PropertyField("allowCpuFFT");
			PropertyField("allowFloatingPointMipMaps");
			PropertyField("debugPhysics");

			string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
			bool simd = defines.Contains("WATER_SIMD");
			bool newSimd = EditorGUILayout.Toggle("Use SIMD Acceleration", simd);

			if(simd != newSimd)
			{
				if(newSimd)
				{
					EditorUtility.DisplayDialog("DLL", "To make SIMD acceleration work, you will need to copy Mono.Simd.dll from \"(Unity Editor Path)/Unity/Editor/Data/Mono/lib/mono/2.0\" to a Plugins folder in your project.", "OK");
				}

				SetSimd(newSimd, BuildTargetGroup.Standalone);
				SetSimd(newSimd, BuildTargetGroup.PS4);
				SetSimd(newSimd, BuildTargetGroup.XboxOne);
			}

			serializedObject.ApplyModifiedProperties();
		}

		private void SetSimd(bool simd, BuildTargetGroup buildTargetGroup)
		{
			string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);

			if(simd)
				defines += " WATER_SIMD";
			else
				defines = defines.Replace(" WATER_SIMD", "").Replace(" WATER_SIMD", "").Replace("WATER_SIMD", "");          // it's an editor script so whatever :)

			PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, defines);
		}

		[MenuItem("Edit/Project Settings/Water")]
		static void OpenSettings()
		{
			var instance = WaterProjectSettings.Instance;

			Selection.activeObject = instance;
		}
	}
}
