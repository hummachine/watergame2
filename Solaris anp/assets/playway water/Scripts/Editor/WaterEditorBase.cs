using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;
using System.Collections.Generic;

namespace PlayWay.Water
{
	public class WaterEditorBase : Editor
	{
		private GUIStyle headerStyle;
		private GUIStyle groupStyle;
		private GUIStyle textureBoxStyle;
		private GUIStyle textureLabelStyle;
		private GUIStyle textureBox;

		private Material inspectMaterial;
		private Stack<bool> foldouts = new Stack<bool>();
		private float inspectMinValue = 0.0f;
		private float inspectMaxValue = 1.0f;
		protected bool useFoldouts;

		virtual protected void UpdateValues()
		{
			
		}

		virtual protected void UpdateStyles()
		{
			//if(headerStyle == null)
			{
				headerStyle = new GUIStyle(EditorStyles.foldout);
				//headerStyle.fontStyle = FontStyle.Bold;
				headerStyle.margin = new RectOffset(0, 0, 0, 0);
				headerStyle.fixedHeight = 10;
				headerStyle.stretchHeight = false;

				/*headerStyle = new GUIStyle(GUI.skin.label);
				headerStyle.fontStyle = FontStyle.Bold;
				headerStyle.stretchWidth = false;
				headerStyle.margin = new RectOffset(0, 12, 0, 0);
				headerStyle.normal.textColor = Color.white;
				//headerStyle.margin = new RectOffset(0, 0, 12, 0);*/
			}

			if(groupStyle == null)
			{
				groupStyle = new GUIStyle();
				groupStyle.margin = new RectOffset(0, 0, 0, 0);
			}

			if(textureBoxStyle == null)
			{
				textureBoxStyle = new GUIStyle(GUI.skin.box);
				textureBoxStyle.margin = new RectOffset(GUI.skin.label.margin.left + 2, 0, 0, 0);
			}

			if(textureLabelStyle == null)
			{
				textureLabelStyle = new GUIStyle(GUI.skin.label);
				textureLabelStyle.alignment = TextAnchor.MiddleLeft;
			}
		}

		protected void UpdateGUI()
		{
			if(Event.current.type == EventType.Layout)
				UpdateValues();

			UpdateStyles();
		}

		protected bool BeginGroup(string label, AnimBool anim)
		{
			if(headerStyle == null)
				UpdateStyles();

			GUILayout.Space(4);

			if(!useFoldouts)
			{
				GUILayout.Label(label, EditorStyles.boldLabel);
				EditorGUILayout.BeginVertical(groupStyle);
				return true;
			}
			else
			{
				if(anim.isAnimating)
					Repaint();

				anim.target = EditorGUILayout.Foldout(anim.target, label, headerStyle);

				if(EditorGUILayout.BeginFadeGroup(anim.faded))
				{
					EditorGUILayout.BeginVertical(groupStyle);
					foldouts.Push(true);
					return true;
				}

				foldouts.Push(false);
				return false;
			}
		}

		protected void EndGroup()
		{
			if(!useFoldouts)
			{
				GUILayout.Space(6);
				EditorGUILayout.EndVertical();
			}
			else
			{
				if(foldouts.Pop())
				{
					GUILayout.Space(6);
					EditorGUILayout.EndVertical();
				}

				EditorGUILayout.EndFadeGroup();
			}
		}

		static protected void MaterialFloatSlider(Material material, string label, string property, float spaceLeft = 0, float spaceRight = 0, float min = 0.0f, float max = 1.0f)
		{
			MaterialFloatSlider(material, new GUIContent(label), property, spaceLeft, spaceRight, min, max);
		}

		static protected void MaterialFloatSlider(Material material, GUIContent label, string property, float spaceLeft = 0, float spaceRight = 0, float min = 0.0f, float max = 1.0f)
		{
			EditorGUILayout.BeginHorizontal();

			if(spaceLeft != 0)
				GUILayout.Space(spaceLeft);

			float val = material.GetFloat(property);
			float newValue = EditorGUILayout.Slider(label, val, min, max);

			if(val != newValue)
				material.SetFloat(property, newValue);

			if(spaceRight != 0)
				GUILayout.Space(spaceRight);

			EditorGUILayout.EndHorizontal();
		}

		protected bool MaterialFloat(Material material, string name, string label, float leftSpace = 0, float rightSpace = 0)
		{
			return MaterialFloat(material, name, new GUIContent(label), leftSpace, rightSpace);
		}

		protected bool MaterialFloat(Material material, string name, GUIContent label, float leftSpace = 0, float rightSpace = 0)
		{
			EditorGUILayout.BeginHorizontal();

			if(leftSpace != 0)
				GUILayout.Space(leftSpace);

			float val = material.GetFloat(name);
			float newVal = EditorGUILayout.FloatField(label, val);

			if(rightSpace != 0)
				GUILayout.Space(rightSpace);

			EditorGUILayout.EndHorizontal();

			if(newVal != val)
			{
				material.SetFloat(name, newVal);
				return true;
			}

			return false;
		}

		protected bool MaterialColor(Material material, string name, string label, float space = 0, bool showAlpha = true)
		{
			return MaterialColor(material, name, new GUIContent(label), space, showAlpha);
		}

		protected bool MaterialColor(Material material, string name, GUIContent label, float space = 0, bool showAlpha = true)
		{
			EditorGUILayout.BeginHorizontal();

			if(space != 0)
				GUILayout.Space(space);

			Color color = material.GetColor(name);
			Color newColor = EditorGUILayout.ColorField(label, color, true, showAlpha, false, new ColorPickerHDRConfig(0.0f, 1.0f, 0.0f, 1.0f));

			EditorGUILayout.EndHorizontal();

			if(newColor != color)
			{
				material.SetColor(name, newColor);
				return true;
			}

			return false;
		}

		protected SerializedProperty SubPropertyField(string topProperty, string subProperty, string label, float space = 0)
		{
			if(space != 0)
			{
				EditorGUILayout.BeginHorizontal();
				GUILayout.Space(space);
			}

			SerializedProperty prop = serializedObject.FindProperty(topProperty);
			SerializedProperty property = prop.FindPropertyRelative(subProperty);
			EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);

			if(space != 0)
				EditorGUILayout.EndHorizontal();

			return property;
		}

		protected SerializedProperty SubSubPropertyField(string topProperty, string midProperty, string subProperty, string label, float space = 0)
		{
			if(space != 0)
			{
				EditorGUILayout.BeginHorizontal();
				GUILayout.Space(space);
			}

			SerializedProperty prop = serializedObject.FindProperty(topProperty).FindPropertyRelative(midProperty);
			SerializedProperty property = prop.FindPropertyRelative(subProperty);
			EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);

			if(space != 0)
				EditorGUILayout.EndHorizontal();

			return property;
		}

		protected SerializedProperty PropertyField(string name)
		{
			SerializedProperty property = serializedObject.FindProperty(name);
			EditorGUILayout.PropertyField(property, true);

			return property;
		}

		protected SerializedProperty PropertyField(string name, string label, float space = 0)
		{
			if(space != 0)
			{
				EditorGUILayout.BeginHorizontal();
				GUILayout.Space(space);
			}

			SerializedProperty property = serializedObject.FindProperty(name);
			EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);

			if(space != 0)
				EditorGUILayout.EndHorizontal();

			return property;
		}

		protected void DisplayTextureInspector(Texture texture)
		{
			if(textureBox == null)
			{
				textureBox = new GUIStyle(GUI.skin.box);
				textureBox.alignment = TextAnchor.MiddleCenter;
				textureBox.fontStyle = FontStyle.Bold;
				textureBox.normal.textColor = EditorStyles.boldLabel.normal.textColor;
				textureBox.active.textColor = EditorStyles.boldLabel.normal.textColor;
				textureBox.focused.textColor = EditorStyles.boldLabel.normal.textColor;
			}

			GUILayout.BeginHorizontal();
			{
				GUILayout.FlexibleSpace();
				GUILayout.Box(texture != null ? "" : "NOT AVAILABLE", textureBox, GUILayout.Width(Screen.width * 0.85f), GUILayout.Height(Screen.width * 0.85f));
				Rect texRect = GUILayoutUtility.GetLastRect();

				if(texture != null && Event.current.type == EventType.Repaint)
				{
					if(inspectMaterial == null)
					{
						inspectMaterial = new Material(Shader.Find("PlayWay Water/Editor/Inspect Texture"));
						inspectMaterial.hideFlags = HideFlags.DontSave;
					}

					inspectMaterial.SetVector("_RangeR", new Vector4(inspectMinValue, 1.0f / (inspectMaxValue - inspectMinValue)));
					inspectMaterial.SetVector("_RangeG", new Vector4(inspectMinValue, 1.0f / (inspectMaxValue - inspectMinValue)));
					inspectMaterial.SetVector("_RangeB", new Vector4(inspectMinValue, 1.0f / (inspectMaxValue - inspectMinValue)));
					Graphics.DrawTexture(texRect, texture, inspectMaterial);
					Repaint();
				}

				GUILayout.FlexibleSpace();
			}

			GUILayout.EndHorizontal();

			EditorGUILayout.MinMaxSlider(ref inspectMinValue, ref inspectMaxValue, 0.0f, 1.0f);
		}

		protected struct WaterMap
		{
			public string name;
			public System.Func<Texture> getTexture;

			public WaterMap(string name, System.Func<Texture> getTexture)
			{
				this.name = name;
				this.getTexture = getTexture;
			}
		}
	}
}