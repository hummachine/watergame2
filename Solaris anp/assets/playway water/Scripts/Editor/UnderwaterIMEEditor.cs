using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;

namespace PlayWay.Water
{
	[CustomEditor(typeof(UnderwaterIME))]
	public class UnderwaterIMEEditor : WaterEditorBase
	{
		private AnimBool advancedFoldout = new AnimBool(false);

		private bool customAbsorptionColor;

		public override void OnInspectorGUI()
		{
			SubPropertyField("blur", "iterations", "Blur Quality");
			PropertyField("underwaterAudio");

			useFoldouts = true;

			if(BeginGroup("Advanced", advancedFoldout))
			{
				PropertyField("cameraBlurScale");

				var cameraAbsorptionColorProp = serializedObject.FindProperty("cameraAbsorptionColor");
                customAbsorptionColor = customAbsorptionColor || cameraAbsorptionColorProp.colorValue.maxColorComponent != 0.0f;
				customAbsorptionColor = EditorGUILayout.Toggle(new GUIContent("Use Custom Absorption Color", cameraAbsorptionColorProp.tooltip), customAbsorptionColor);

				if(customAbsorptionColor)
					PropertyField("cameraAbsorptionColor");
				else
					cameraAbsorptionColorProp.colorValue = new Color(0.0f, 0.0f, 0.0f, 0.0f);
            }

			EndGroup();

			serializedObject.ApplyModifiedProperties();
		}
	}
}
