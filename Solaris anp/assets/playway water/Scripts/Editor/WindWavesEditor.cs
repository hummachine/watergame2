using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;

namespace PlayWay.Water
{
	[CustomEditor(typeof(WindWaves))]
	public class WindWavesEditor : WaterEditorBase
	{
		private AnimBool cpuFoldout = new AnimBool(false);
		private AnimBool fftFoldout = new AnimBool(false);
		private AnimBool gerstnerFoldout = new AnimBool(false);

		static private GUIContent[] resolutionLabels = new GUIContent[] { new GUIContent("4x32x32 (runs on potatos)"), new GUIContent("4x64x64"), new GUIContent("4x128x128"), new GUIContent("4x256x256 (very high; most PCs)"), new GUIContent("4x512x512 (extreme; gaming PCs)"), new GUIContent("4x1024x1024 (as seen in Titanic® and Water World®; future PCs)") };
		static private int[] resolutions = new int[] { 32, 64, 128, 256, 512, 1024, 2048, 4096 };

		public override void OnInspectorGUI()
		{
			if(BeginGroup("Rendering", null))
			{
				var copyFromProp = serializedObject.FindProperty("copyFrom");

				GUI.enabled = copyFromProp.objectReferenceValue == null;
				PropertyField("renderMode");

				DrawResolutionGUI();
				PropertyField("highPrecision");

				PropertyField("windDirectionPointer");
				GUI.enabled = true;

				SubPropertyField("dynamicSmoothness", "enabled", "Dynamic Smoothness");
				PropertyField("copyFrom");
			}

			EndGroup();

			useFoldouts = true;

			if(BeginGroup("CPU", cpuFoldout))
			{
				PropertyField("cpuWaveThreshold", "Wave Threshold");
				PropertyField("cpuMaxWaves", "Max Waves");
				PropertyField("cpuFFTPrecisionBoost", "FFT Precision Boost");
			}

			EndGroup();

			if(BeginGroup("FFT", fftFoldout))
			{
				SubPropertyField("waterWavesFFT", "highQualitySlopeMaps", "High Quality Slope Maps");
				SubPropertyField("waterWavesFFT", "forcePixelShader", "Force Pixel Shader");
				SubPropertyField("waterWavesFFT", "flattenMode", "Flatten Mode");
			}

			EndGroup();

			if(BeginGroup("Gerstner", gerstnerFoldout))
			{
				SubPropertyField("waterWavesGerstner", "numGerstners", "Waves Count");
			}

			EndGroup();

			useFoldouts = false;

			serializedObject.ApplyModifiedProperties();

			((WindWaves)target).OnValidate();

			serializedObject.Update();
		}

		private void DrawResolutionGUI()
		{
			var property = serializedObject.FindProperty("resolution");
			DrawResolutionGUI(property);
		}

		static public void DrawResolutionGUI(SerializedProperty property, string name = null)
		{
			const string tooltip = "Higher values increase quality, but also decrease performance. Directly controls quality of waves, foam and spray.";

			int newResolution = IndexToResolution(EditorGUILayout.Popup(new GUIContent(name != null ? name : property.displayName, tooltip), ResolutionToIndex(property.intValue), resolutionLabels));

			if(newResolution != property.intValue)
				property.intValue = newResolution;
		}

		static int ResolutionToIndex(int resolution)
		{
			switch(resolution)
			{
				case 32: return 0;
				case 64: return 1;
				case 128: return 2;
				case 256: return 3;
				case 512: return 4;
				case 1024: return 5;
				case 2048: return 6;
				case 4096: return 7;
			}

			return 0;
		}

		static int IndexToResolution(int index)
		{
			return resolutions[index];
		}
	}
}