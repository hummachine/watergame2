using UnityEngine;
using UnityEditor;

namespace PlayWay.Water
{
	[CustomEditor(typeof(StaticWaterInteraction))]
	public class StaticWaterInteractionEditor : WaterEditorBase
	{
		private GUIStyle boxStyle;

		override protected void UpdateStyles()
		{
			if(boxStyle == null)
			{
				boxStyle = new GUIStyle(GUI.skin.box);
				boxStyle.alignment = TextAnchor.MiddleCenter;
				boxStyle.fontStyle = FontStyle.Bold;

				if(EditorGUIUtility.isProSkin)
					boxStyle.normal.textColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
			}
		}

		public override void OnInspectorGUI()
		{
			UpdateStyles();

			PropertyField("hasBottomFaces");
			DrawShoreAngleProperty();
			
			DrawIntensityMask();

			serializedObject.ApplyModifiedProperties();
		}

		private void DrawShoreAngleProperty()
		{
			var shoreSmoothness = PropertyField("shoreSmoothness").floatValue;
			string type;

			if(shoreSmoothness <= 1.0f)
				type = "Cliff";
			else if(shoreSmoothness < 8.0f)
				type = "Coast";
			else if(shoreSmoothness < 35.0f)
				type = "Beach (Steep)";
			else
				type = "Beach (Gentle)";

			EditorGUILayout.LabelField("Type", type);
		}

		private void DrawIntensityMask()
		{
			GUILayout.Space(6);

			var target = (StaticWaterInteraction)this.target;

			GUILayout.BeginHorizontal();
			{
				GUILayout.FlexibleSpace();
				GUILayout.Box(target.IntensityMask != null ? "" : "NOT AVAILABLE", boxStyle, GUILayout.Width(Screen.width * 0.6f), GUILayout.Height(Screen.width * 0.6f));
				Rect texRect = GUILayoutUtility.GetLastRect();

				if(target.IntensityMask != null && Event.current.type == EventType.Repaint)
				{
					Graphics.DrawTexture(texRect, target.IntensityMask);
					Repaint();
				}

				GUILayout.FlexibleSpace();
			}

			GUILayout.EndHorizontal();
		}
	}
}
