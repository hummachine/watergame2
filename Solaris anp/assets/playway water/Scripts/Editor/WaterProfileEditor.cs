using UnityEditor;
using UnityEngine;

namespace PlayWay.Water
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(WaterProfile))]
	public class WaterProfileEditor : WaterEditorBase
	{
		private Texture2D illustrationTex;

		private GUIStyle warningLabel;
		private GUIStyle normalMapLabel;
		private bool initialized;

		protected override void UpdateStyles()
		{
			base.UpdateStyles();
			
			if(!initialized)
			{
				Undo.undoRedoPerformed -= OnUndoRedoPerformed;
				Undo.undoRedoPerformed += OnUndoRedoPerformed;
				initialized = true;
			}

			if(warningLabel == null)
			{
				warningLabel = new GUIStyle(GUI.skin.label);
				warningLabel.wordWrap = true;
				warningLabel.normal.textColor = new Color32(255, 201, 2, 255);
			}

			if(illustrationTex == null)
			{
				string texPath = WaterPackageUtilities.WaterPackagePath + "/Textures/Editor/Illustration.png";
				illustrationTex = (Texture2D)AssetDatabase.LoadMainAssetAtPath(texPath);
			}

			if(normalMapLabel == null)
			{
				normalMapLabel = new GUIStyle(GUI.skin.label);
				normalMapLabel.stretchHeight = true;
				normalMapLabel.fontStyle = FontStyle.Bold;
				normalMapLabel.alignment = TextAnchor.MiddleLeft;
			}
		}

		public override bool RequiresConstantRepaint()
		{
			return true;
		}

		public override void OnInspectorGUI()
		{
			UpdateGUI();

			var profile = (WaterProfile)target;

			GUI.enabled = !Application.isPlaying;
			PropertyField("spectrumType");

			DrawWindSpeedGUI();

			PropertyField("tileSize");
			PropertyField("tileScale");
			PropertyField("wavesAmplitude");
			GUI.enabled = true;

			PropertyField("horizontalDisplacementScale");

			if(profile.SpectrumType == WaterProfile.WaterSpectrumType.Phillips)
				PropertyField("phillipsCutoffFactor", "Cutoff Factor");

			PropertyField("directionality");
			PropertyField("fetch");

			GUILayout.Space(12.0f);

			EditorGUILayout.BeginHorizontal();
			{
				GUILayout.Box(illustrationTex);

				EditorGUILayout.BeginVertical(GUILayout.Height(illustrationTex.height + 12));
				{
					PropertyField("absorptionColor", "Absorption");
					PropertyField("underwaterAbsorptionColor", "Absorption (Underwater IME)");
					PropertyField("diffuseColor", "Diffuse");
					PropertyField("specularColor", "Specular");
					PropertyField("depthColor", "Depth");
					PropertyField("emissionColor", "Emission");
					PropertyField("reflectionColor", "Reflection");

					EditorGUILayout.EndVertical();
				}

				EditorGUILayout.EndHorizontal();
			}

			GUILayout.Space(8.0f);

			PropertyField("smoothness");
			var customAmbientSmoothnessProp = PropertyField("customAmbientSmoothness");

			if(!customAmbientSmoothnessProp.hasMultipleDifferentValues)
			{
				if(customAmbientSmoothnessProp.boolValue)
					PropertyField("ambientSmoothness");
			}

			PropertyField("edgeBlendFactor", "Edge Blend Factor");
			PropertyField("subsurfaceScattering", "Subsurface Scattering");
			PropertyField("directionalWrapSSS", "Directional Wrap SSS");
			PropertyField("pointWrapSSS", "Point Wrap SSS");
			PropertyField("refractionDistortion", "Refraction Distortion");
			DrawIOREditor();
			PropertyField("density");

			GUILayout.Space(8.0f);

			GUILayout.Label("Normals", EditorStyles.boldLabel);
			//PropertyField("normalsFadeDistance", "Fade Distance");
			//PropertyField("normalsFadeBias", "Fade Bias");
			PropertyField("detailFadeDistance", "Detail Fade Distance");
			PropertyField("displacementNormalsIntensity", "Slope Intensity");
			DrawNormalAnimationEditor();

			GUILayout.Space(8.0f);

			GUILayout.Label("Foam", EditorStyles.boldLabel);
			PropertyField("foamIntensity", "Intensity");
			PropertyField("foamThreshold", "Threshold");
			PropertyField("foamFadingFactor", "Fade Factor");
			PropertyField("foamNormalScale", "Foam Normal Scale");
			PropertyField("foamSpecularColor", "Foam Specular Color");

			GUILayout.Space(8.0f);

			GUILayout.Label("Planar Reflections", EditorStyles.boldLabel);
			PropertyField("planarReflectionIntensity", "Intensity");
			PropertyField("planarReflectionFlatten", "Flatten");
			PropertyField("planarReflectionVerticalOffset", "Offset");

			GUILayout.Space(8.0f);

			GUILayout.Label("Underwater", EditorStyles.boldLabel);
			PropertyField("underwaterBlurSize", "Blur Size");
			PropertyField("underwaterDistortionsIntensity", "Distortion Intensity");
			PropertyField("underwaterDistortionAnimationSpeed", "Distortion Animation Speed");

			GUILayout.Space(8.0f);

			GUILayout.Label("Spray", EditorStyles.boldLabel);
			PropertyField("sprayThreshold", "Threshold");
			PropertyField("spraySkipRatio", "Skip Ratio");
			PropertyField("spraySize", "Size");

			GUILayout.Space(8.0f);

			GUILayout.Label("Textures", EditorStyles.boldLabel);
			PropertyField("normalMap", "Normal Map");
			//PropertyField("heightMap", "Height Map");
			PropertyField("foamDiffuseMap", "Foam Diffuse Map");
			PropertyField("foamNormalMap", "Foam Normal Map");
			PropertyField("foamTiling", "Foam Tiling");

			serializedObject.ApplyModifiedProperties();

			if(GUI.changed)
				ValidateWaterObjects();
        }

		private void ValidateWaterObjects()
		{
			var waters = FindObjectsOfType<Water>();

			foreach(var water in waters)
			{
				water.SetProfiles(water.Profiles);
				water.OnValidate();
			}
		}

		private void DrawWindSpeedGUI()
		{
			//var profile = (WaterProfile)target;

			var windSpeedProp = serializedObject.FindProperty("windSpeed");

			float mps = windSpeedProp.floatValue;
			float knots = MpsToKnots(mps);

			if(windSpeedProp.hasMultipleDifferentValues)
				EditorGUI.showMixedValue = true;

			float newKnots = EditorGUILayout.Slider(new GUIContent(string.Format("Wind Speed ({0})", GetWindSpeedClassification(knots)), "Wind speed in knots."), knots, 0.0f, 70.0f);

			EditorGUI.showMixedValue = false;

			if(knots != newKnots)
				windSpeedProp.floatValue = KnotsToMps(newKnots);

			//float wavelength = WindSpeedToWavelength(mps);
			//if(wavelength >= profile.TileSize)
			//	EditorGUILayout.LabelField(string.Format("This wind should generate waves of length {0:0}m but \"Tile Size\" is set to {1:0}m. Results will be incorrect.", wavelength, profile.TileSize), warningLabel);
		}

		private void DrawIOREditor()
		{
			var fresnelBiasProp = serializedObject.FindProperty("fresnelBias");
			float originalIOR = BiasToIOR(fresnelBiasProp.floatValue);

			if(fresnelBiasProp.hasMultipleDifferentValues)
				EditorGUI.showMixedValue = true;
			
			float ior = EditorGUILayout.Slider(new GUIContent("Index of Refraction", "Water index of refraction is 1.33333333."), originalIOR, 1.0f, 4.05f);

			EditorGUI.showMixedValue = false;

			if(originalIOR != ior)
				fresnelBiasProp.floatValue = IORToBias(ior);
		}

		private void DrawNormalAnimationEditor()
		{
			EditorGUILayout.BeginHorizontal(GUILayout.Height(60.0f));
			{
				GUILayout.Space(10);
				GUILayout.Label("Tiles 1", normalMapLabel);

				EditorGUILayout.BeginVertical();
				{
					SubPropertyField("normalMapAnimation1", "speed", "Speed");
					SubPropertyField("normalMapAnimation1", "deviation", "Deviation");
					SubPropertyField("normalMapAnimation1", "intensity", "Intensity");
					SubPropertyField("normalMapAnimation1", "tiling", "Tiling");

					EditorGUILayout.EndVertical();
				}

				EditorGUILayout.EndHorizontal();
			}

			GUILayout.Space(6);

			EditorGUILayout.BeginHorizontal(GUILayout.Height(60.0f));
			{
				GUILayout.Space(10);
				GUILayout.Label("Tiles 2", normalMapLabel);

				EditorGUILayout.BeginVertical();
				{
					SubPropertyField("normalMapAnimation2", "speed", "Speed");
					SubPropertyField("normalMapAnimation2", "deviation", "Deviation");
					SubPropertyField("normalMapAnimation2", "intensity", "Intensity");
					SubPropertyField("normalMapAnimation2", "tiling", "Tiling");

					EditorGUILayout.EndVertical();
				}

				EditorGUILayout.EndHorizontal();
			}
		}

		private float WindSpeedToWavelength(float f)
		{
			return 2.0f * (0.338798f * f * f - 0.418664f * f + 1.60147f);
		}

		private float MpsToKnots(float f)
		{
			return f / 0.5144f;
		}

		private float KnotsToMps(float f)
		{
			return 0.5144f * f;
		}

		private string GetWindSpeedClassification(float f)
		{
			if(f < 1.0f)
				return "Calm";
			else if(f < 3.0f)
				return "Light Air";
			else if(f < 6.0f)
				return "Light Breeze";
			else if(f < 10.0f)
				return "Gentle Breeze";
			else if(f < 16.0f)
				return "Moderate Breeze";
			else if(f < 21.0f)
				return "Fresh Breeze";
			else if(f < 27.0f)
				return "Strong Breeze";
			else if(f < 33.0f)
				return "Near Gale";
			else if(f < 40.0f)
				return "Gale";
			else if(f < 47.0f)
				return "Strong Gale";
			else if(f < 55.0f)
				return "Storm";
			else if(f < 63.0f)
				return "Violent Storm";
			else
				return "Hurricane";
		}

		private void OnUndoRedoPerformed()
		{
			serializedObject.Update();
			ValidateWaterObjects();
			Repaint();
		}

		private float IORToBias(float ior)
		{
			float a = (1.0f - ior);
			float b = (1.0f + ior);
			return (a * a) / (b * b);
		}

		private float BiasToIOR(float bias)
		{
			return (Mathf.Sqrt(bias) + 1) / (1 - Mathf.Sqrt(bias));
        }
	}
}
