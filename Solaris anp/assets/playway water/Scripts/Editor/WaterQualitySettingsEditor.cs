using UnityEditor;
using UnityEngine;

namespace PlayWay.Water
{
	[CustomEditor(typeof(WaterQualitySettings))]
	public class WaterQualitySettingsEditor : WaterEditorBase
	{
		private GUIStyle selectedLevel;
		private GUIStyle separator;

		protected override void UpdateStyles()
		{
			base.UpdateStyles();

			if(selectedLevel == null)
			{
				var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false, true);
				texture.hideFlags = HideFlags.DontSave;
				FillTexture(texture, EditorGUIUtility.isProSkin ? new Color32(72, 72, 72, 255) : new Color32(255, 255, 255, 255));
				
				selectedLevel = new GUIStyle(GUI.skin.label);
				selectedLevel.normal.background = texture;
			}

			if(separator == null)
			{
				var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false, true);
				texture.hideFlags = HideFlags.DontSave;
				FillTexture(texture, EditorGUIUtility.isProSkin ? new Color32(144, 144, 144, 255) : new Color32(255, 255, 255, 255));

				separator = new GUIStyle();
				separator.normal.background = texture;
				separator.stretchWidth = true;
				separator.fixedHeight = 1;
			}
		}

		public override void OnInspectorGUI()
		{
			UpdateGUI();

			var qualitySettings = (WaterQualitySettings)target;
			
			if(Event.current.type == EventType.Layout)
				qualitySettings.SynchronizeQualityLevel();
			
			GUILayout.BeginHorizontal();
			{
				GUILayout.FlexibleSpace();

				GUILayout.BeginVertical();
				{
					GUILayout.Label("Levels", EditorStyles.boldLabel);

					var qualityLevelsProp = serializedObject.FindProperty("qualityLevels");
					int numQualityLevels = qualityLevelsProp.arraySize;

					for(int levelIndex=0; levelIndex < numQualityLevels; ++levelIndex)
						DrawLevelGUI(levelIndex, qualityLevelsProp.GetArrayElementAtIndex(levelIndex));

					GUILayout.Space(10);

					if(GUILayout.Button("Open Unity Settings"))
					{
						EditorApplication.ExecuteMenuItem("Edit/Project Settings/Quality");
					}

					GUILayout.Space(10);

					DrawGeneralOptionsGUI();

					GUILayout.EndVertical();
				}

				GUILayout.FlexibleSpace();

				GUILayout.EndHorizontal();
			}

			GUILayout.Space(10);

			GUILayout.Label("", separator);

			GUILayout.Space(10);

			DrawCurrentLevelGUI();

			if(serializedObject.ApplyModifiedProperties())
				WaterQualitySettings.Instance.SetQualityLevel(WaterQualitySettings.Instance.GetQualityLevel());
        }

		private void DrawLevelGUI(int index, SerializedProperty property)
		{
			var nameProperty = property.FindPropertyRelative("name");
			string name = nameProperty.stringValue;

			var qualitySettings = WaterQualitySettings.Instance;
			var style = WaterQualitySettings.Instance.GetQualityLevel() == index ? selectedLevel : GUI.skin.label;

			if(GUILayout.Button(name, style, GUILayout.Width(180)))
			{
				if(qualitySettings.SynchronizeWithUnity)
					QualitySettings.SetQualityLevel(index);

				WaterQualitySettings.Instance.SetQualityLevel(index);
			}
		}

		private void DrawGeneralOptionsGUI()
		{
			var syncWithUnityProp = serializedObject.FindProperty("synchronizeWithUnity");
			EditorGUILayout.PropertyField(syncWithUnityProp);
		}

		private void DrawCurrentLevelGUI()
		{
			int qualityLevelIndex = WaterQualitySettings.Instance.GetQualityLevel();
			var currentLevelProp = serializedObject.FindProperty("qualityLevels").GetArrayElementAtIndex(qualityLevelIndex);

			GUI.enabled = false;
			EditorGUILayout.PropertyField(currentLevelProp.FindPropertyRelative("name"));
			GUI.enabled = true;

			if(BeginGroup("Spectrum", null))
			{
				WaterEditor.DrawResolutionGUI(currentLevelProp.FindPropertyRelative("maxSpectrumResolution"), "Max Resolution");
				EditorGUILayout.PropertyField(currentLevelProp.FindPropertyRelative("allowHighPrecisionTextures"));
				EditorGUILayout.PropertyField(currentLevelProp.FindPropertyRelative("tileSizeScale"));
			}

			EndGroup();

			if(BeginGroup("Simulation", null))
			{
				EditorGUILayout.PropertyField(currentLevelProp.FindPropertyRelative("wavesMode"));
				EditorGUILayout.PropertyField(currentLevelProp.FindPropertyRelative("allowHighQualitySlopeMaps"));
				EditorGUILayout.PropertyField(currentLevelProp.FindPropertyRelative("allowSpray"));
				EditorGUILayout.PropertyField(currentLevelProp.FindPropertyRelative("foamQuality"));
				//EditorGUILayout.PropertyField(currentLevelProp.FindPropertyRelative("maxGerstnerWavesCount"));
			}

			EndGroup();

			if(BeginGroup("Shader", null))
			{
				EditorGUILayout.PropertyField(currentLevelProp.FindPropertyRelative("allowVolumetricLighting"));
				EditorGUILayout.PropertyField(currentLevelProp.FindPropertyRelative("maxTesselationFactor"));
				EditorGUILayout.PropertyField(currentLevelProp.FindPropertyRelative("maxVertexCount"));
				EditorGUILayout.PropertyField(currentLevelProp.FindPropertyRelative("maxTesselatedVertexCount"));
				EditorGUILayout.PropertyField(currentLevelProp.FindPropertyRelative("allowAlphaBlending"));
				EditorGUILayout.PropertyField(currentLevelProp.FindPropertyRelative("allowHighQualityReflections"));
            }

			EndGroup();
		}

		private void FillTexture(Texture2D tex, Color color)
		{
			for(int x=0; x<tex.width; ++x)
			{
				for(int y=0; y<tex.height; ++y)
				{
					tex.SetPixel(x, y, color);
				}
			}

			tex.Apply();
		}
		
		[MenuItem("Edit/Project Settings/Water Quality")]
		static public void OpenQualitySettings()
		{
			var instance = WaterQualitySettings.Instance;

			Selection.activeObject = instance;
		}
	}
}
