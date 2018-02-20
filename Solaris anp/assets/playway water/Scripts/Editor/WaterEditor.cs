using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;
using System.Collections.Generic;
using System.Linq;

namespace PlayWay.Water
{
	[CustomEditor(typeof(Water))]
	public class WaterEditor : WaterEditorBase
	{
		private AnimBool environmentFoldout = new AnimBool(true);
		private AnimBool surfaceFoldout = new AnimBool(false);
		//private AnimBool spectrumFoldout = new AnimBool(false);
		private AnimBool geometryFoldout = new AnimBool(false);
		private AnimBool inspectFoldout = new AnimBool(false);

		static private GUIContent[] resolutionLabels = new GUIContent[] { new GUIContent("4x32x32 (runs on potatos)"), new GUIContent("4x64x64"), new GUIContent("4x128x128"), new GUIContent("4x256x256 (very high; most PCs)"), new GUIContent("4x512x512 (extreme; gaming PCs)"), new GUIContent("4x1024x1024 (as seen in Titanic® and Water World®; future PCs)") };
		static private int[] resolutions = new int[] { 32, 64, 128, 256, 512, 1024, 2048, 4096 };
		private GUIStyle boldLabel;

		private int selectedMapIndex = -1;
		private bool useCustomMaterials;
		private bool initialized;
		private bool askedForWaterCamera;

		override protected void UpdateStyles()
		{
			base.UpdateStyles();

			if(!initialized)
			{
				var waterMaterialPrefab = serializedObject.FindProperty("waterMaterialPrefab");
				var waterVolumeMaterialPrefab = serializedObject.FindProperty("waterVolumeMaterialPrefab");

				if(waterMaterialPrefab.objectReferenceValue != null || waterVolumeMaterialPrefab.objectReferenceValue != null)
					useCustomMaterials = true;

				initialized = true;
			}

			if(boldLabel == null)
			{
				boldLabel = new GUIStyle(GUI.skin.label);
				boldLabel.fontStyle = FontStyle.Bold;
			}
		}

		public override void OnInspectorGUI()
		{
			if(!askedForWaterCamera && WaterProjectSettings.Instance.AskForWaterCameras && Event.current.type == EventType.Layout && !PlayerSettings.virtualRealitySupported)
				LookForWaterCamera();

			UpdateGUI();

			GUILayout.Space(4);

			DrawProfileField();
			DrawNotifications();

			if(BeginGroup("Environment", environmentFoldout))
			{
				PropertyField("blendEdges");
				PropertyField("volumetricLighting");
				PropertyField("receiveShadows");
				PropertyField("shadowCastingMode");

				PropertyField("useCubemapReflections");
				SubPropertyField("waterRenderer", "reflectionProbeAnchor", "Reflection Probe Anchor");
				//DrawReflectionProbeModeGUI();

				PropertyField("seed");
				SubPropertyField("volume", "boundless", "Boundless");
			}

			EndGroup();

			if(BeginGroup("Geometry", geometryFoldout))
			{
				SubPropertyField("geometry", "type", "Type");

				SubPropertyField("geometry", "baseVertexCount", "Vertices");
				SubPropertyField("geometry", "tesselatedBaseVertexCount", "Vertices (Tesselation)");
				SubSubPropertyField("geometry", "customSurfaceMeshes", "customMeshes", "Custom Meshes");
			}

			EndGroup();

			/*if(BeginGroup("Spectrum", spectrumFoldout))
			{
				DrawResolutionGUI();
				SubPropertyField("spectraRenderer", "highPrecision", "High Precision");
				SubPropertyField("spectraRenderer", "cpuWaveThreshold", "Wave Threshold (CPU)");
				SubPropertyField("spectraRenderer", "cpuMaxWaves", "Max Waves (CPU)");
			}

			EndGroup();*/

			if(BeginGroup("Shading", surfaceFoldout))
			{
				//PropertyField("autoDepthColor", "Auto Depth Color");

				PropertyField("refraction", "Refraction");
				PropertyField("tesselationFactor", "Tesselation Factor");

				var waterMaterialPrefab = serializedObject.FindProperty("waterMaterialPrefab");
				var waterVolumeMaterialPrefab = serializedObject.FindProperty("waterVolumeMaterialPrefab");

				//bool newUseCustomMaterials = EditorGUILayout.Toggle(new GUIContent("Use Custom Materials", "Not recommended for the time being."), useCustomMaterials);
				bool newUseCustomMaterials = false;

				if(useCustomMaterials)
				{
					PropertyField("waterMaterialPrefab", "Surface (Custom Material)");
					PropertyField("waterVolumeMaterialPrefab", "Volume (Custom Material)");
				}

				if(!newUseCustomMaterials && useCustomMaterials)
				{
					waterMaterialPrefab.objectReferenceValue = null;
					waterVolumeMaterialPrefab.objectReferenceValue = null;
				}

				useCustomMaterials = newUseCustomMaterials;
			}

			EndGroup();

			if(BeginGroup("Inspect", inspectFoldout))
			{
				var maps = GetWaterMaps();
				selectedMapIndex = EditorGUILayout.Popup("Texture", selectedMapIndex, maps.Select(m => m.name).ToArray());

				if(selectedMapIndex >= 0 && selectedMapIndex < maps.Count)
				{
					var texture = maps[selectedMapIndex].getTexture();
					DisplayTextureInspector(texture);
				}
			}

			EndGroup();

			GUILayout.Space(10);
			DrawFeatureSelector();
			GUILayout.Space(10);

			serializedObject.ApplyModifiedProperties();
		}

		private void LookForWaterCamera()
		{
			askedForWaterCamera = true;

			foreach(var camera in Camera.allCameras)
			{
				if(WaterCamera.GetWaterCamera(camera) != null)
					return;
			}

			if(Camera.main == null)
				return;

			switch(EditorUtility.DisplayDialogComplex("PlayWay Water - Missing water camera", "Your scene doesn't contain any cameras with WaterCamera component, but only such cameras may actually see the water. Would you like to add this component to camera named \"" + Camera.main.name + "\"? ", "Ok", "Cancel", "Don't ask again"))
			{
				case 0:
				{
					Camera.main.gameObject.AddComponent<WaterCamera>();
					break;
				}
				
				case 2:
				{
					WaterProjectSettings.Instance.AskForWaterCameras = false;
					break;
				}
			}
		}

		private void DrawProfileField()
		{
			var profileField = serializedObject.FindProperty("profile");

			if(profileField.objectReferenceValue == null)
			{
				string[] guids = AssetDatabase.FindAssets("\"Sea - 6. Strong Breeze\"");

				if(guids.Length != 0)
				{
					string path = AssetDatabase.GUIDToAssetPath(guids[0]);
					profileField.objectReferenceValue = AssetDatabase.LoadAssetAtPath<WaterProfile>(path);

					UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                }
			}

			PropertyField("profile");
		}

		private void DrawNotifications()
		{
			var water = (Water)target;

			if(!Application.isPlaying)
			{
				var versionProp = serializedObject.FindProperty("version");

				if(versionProp.floatValue != WaterProjectSettings.CurrentVersion)
				{
					GUILayout.BeginVertical();
					{
						GUILayout.Space(10);

						EditorGUILayout.HelpBox("This water object was created on version " + versionProp.floatValue.ToString("0.0") + ". Would you like to perform common update tasks? If everything looks as expected, you may dismiss this message.", MessageType.Error);

						GUILayout.BeginHorizontal();
						{
							GUILayout.FlexibleSpace();

							if(GUILayout.Button("Dismiss", EditorStyles.miniButtonRight, GUILayout.Width(100)))
							{
								versionProp.floatValue = WaterProjectSettings.CurrentVersion;
							}

							if(GUILayout.Button("Update to " + WaterProjectSettings.CurrentVersionString, EditorStyles.miniButtonRight, GUILayout.Width(120)))
							{
								EditorApplication.update += UpdateWater;
							}

							GUILayout.EndHorizontal();
						}

						GUILayout.Space(10);

						GUILayout.EndVertical();
					}
				}
			}

			if(water.ShaderCollection == null && !Application.isPlaying)
			{
				if(Event.current.type == EventType.Layout)
					SearchShaderVariantCollection();

				if(water.ShaderCollection == null)
					DrawMissingAssetFilesGUI();
			}
			else
			{
				PropertyField("shaderCollection");
			}
		}

		private void DrawFoamToggle(Material material)
		{
			EditorGUILayout.BeginHorizontal();
			{
				GUILayout.Space(28);

				GUI.enabled = false;
				EditorGUILayout.Toggle("Enabled", material.IsKeywordEnabled("_WATER_FOAM_WS"));
				//PropertyField("displayFoam", "Enabled", 28);
				GUI.enabled = true;

				EditorGUILayout.EndHorizontal();
			}
		}

		private void DrawFeatureSelector()
		{
			EditorGUILayout.BeginHorizontal();
			{
				GUILayout.FlexibleSpace();

				if(GUILayout.Button("Add feature...", GUILayout.Width(120)))
				{
					var menu = new GenericMenu();

					AddMenuItem(menu, "Wind Waves", typeof(WindWaves));
					AddMenuItem(menu, "Planar Reflections", typeof(WaterPlanarReflection));
					AddMenuItem(menu, "Foam", typeof(WaterFoam));
					AddMenuItem(menu, "Spray", typeof(WaterSpray));
					AddMenuItem(menu, "Network Water", typeof(NetworkWater));
					AddMenuItem(menu, "Overlays", typeof(WaterOverlays));

					menu.ShowAsContext();
				}

				GUILayout.FlexibleSpace();

				EditorGUILayout.EndHorizontal();
			}
		}

		private void DrawMissingAssetFilesGUI()
		{
			var water = (Water)target;

			if(WaterProjectSettings.Instance.AssetFilesCreation == WaterProjectSettings.WaterAssetFilesCreation.Automatic)
			{
				if(Event.current.type != EventType.Layout)
					return;
				
				#if UNITY_5_2 || UNITY_5_1 || UNITY_5_0
					string scenePath = EditorApplication.currentScene;
				#else
					string scenePath = water.gameObject.scene.path;
				#endif

				int dotIndex = scenePath.LastIndexOf('.');

				if(!scenePath.EndsWith(".unity") || dotIndex == -1)
					return;

				string assetFilePath = scenePath.Substring(0, dotIndex) + " Water.asset";
                SaveWaterAssetFileTo(assetFilePath);

				PropertyField("shaderCollection");

				return;
            }

			EditorGUILayout.HelpBox("Each scene with water needs one unique asset file somewhere in your project. This file will contain shaders and baked data.", MessageType.Warning, true);

			EditorGUILayout.BeginHorizontal();
			{
				GUILayout.FlexibleSpace();

				if(GUILayout.Button("Save Asset..."))
				{
					string path = EditorUtility.SaveFilePanelInProject("Save Water Assets...", water.name, "asset", "");

					if(!string.IsNullOrEmpty(path))
						SaveWaterAssetFileTo(path);
				}

				EditorGUILayout.EndHorizontal();
			}
		}

		private void SaveWaterAssetFileTo(string path)
		{
			var shaderCollection = CreateInstance<ShaderCollection>();
			AssetDatabase.CreateAsset(shaderCollection, path);

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			serializedObject.FindProperty("shaderCollection").objectReferenceValue = shaderCollection;
			serializedObject.FindProperty("sceneHash").intValue = GetSceneHash();
		}

		private void AddMenuItem(GenericMenu menu, string label, System.Type type)
		{
			var water = (Water)target;

			if(water.GetComponent(type) == null)
			{
				menu.AddItem(new GUIContent(label), false, OnAddComponent, type);
			}
		}

		private void OnAddComponent(object componentTypeObj)
		{
			var water = (Water)target;
			water.gameObject.AddComponent((System.Type)componentTypeObj);
		}

		private void DrawReflectionProbeModeGUI()
		{
			GUI.enabled = PropertyField("useCubemapReflections").boolValue;

			var prop = serializedObject.FindProperty("reflectionProbeUsage");
			ReflectionProbeUsage val = (ReflectionProbeUsage)prop.intValue;
			val = (ReflectionProbeUsage)EditorGUILayout.EnumPopup("Reflection Probe Usage", val);
			prop.intValue = (int)val;

			GUI.enabled = true;
		}

		private void DrawResolutionGUI()
		{
			var property = serializedObject.FindProperty("spectraRenderer").FindPropertyRelative("resolution");
			DrawResolutionGUI(property);
		}

		static public void DrawResolutionGUI(SerializedProperty property, string name = null)
		{
			const string tooltip = "Higher values increase quality, but also decrease performance. Directly controls quality of waves, foam and spray.";

			int newResolution = IndexToResolution(EditorGUILayout.Popup(new GUIContent(name != null ? name : property.displayName, tooltip), ResolutionToIndex(property.intValue), resolutionLabels));

			if(newResolution != property.intValue)
				property.intValue = newResolution;
		}

		private List<WaterMap> GetWaterMaps()
		{
			var water = (Water)target;
			var textures = new List<WaterMap>();

			var windWaves = water.GetComponent<WindWaves>();

			if(windWaves != null)
			{
				textures.Add(new WaterMap("WindWaves - Raw Omnidirectional Spectrum", () => windWaves.SpectrumResolver.GetSpectrum(SpectrumResolver.SpectrumType.RawOmnidirectional)));
				textures.Add(new WaterMap("WindWaves - Raw Directional Spectrum", () => windWaves.SpectrumResolver.GetSpectrum(SpectrumResolver.SpectrumType.RawDirectional)));
				textures.Add(new WaterMap("WindWaves - Height Spectrum", () => windWaves.SpectrumResolver.GetSpectrum(SpectrumResolver.SpectrumType.Height)));
				textures.Add(new WaterMap("WindWaves - Slope Spectrum", () => windWaves.SpectrumResolver.GetSpectrum(SpectrumResolver.SpectrumType.Slope)));
				textures.Add(new WaterMap("WindWaves - Horizontal Displacement Spectrum", () => windWaves.SpectrumResolver.GetSpectrum(SpectrumResolver.SpectrumType.Displacement)));

				var wavesFFT = windWaves.WaterWavesFFT;
				textures.Add(new WaterMap("WindWaves - Displacement Map 0", () => wavesFFT != null ? wavesFFT.GetDisplacementMap(0) : null));
				textures.Add(new WaterMap("WindWaves - Displacement Map 1", () => wavesFFT != null ? wavesFFT.GetDisplacementMap(1) : null));
				textures.Add(new WaterMap("WindWaves - Displacement Map 2", () => wavesFFT != null ? wavesFFT.GetDisplacementMap(2) : null));
				textures.Add(new WaterMap("WindWaves - Displacement Map 3", () => wavesFFT != null ? wavesFFT.GetDisplacementMap(3) : null));
				textures.Add(new WaterMap("WindWaves - Slope Map 0", () => wavesFFT != null ? wavesFFT.GetSlopeMap(0) : null));
				textures.Add(new WaterMap("WindWaves - Slope Map 1", () => wavesFFT != null ? wavesFFT.GetSlopeMap(1) : null));

				var foam = water.GetComponent<WaterFoam>();
				textures.Add(new WaterMap("WaterFoam - Foam Map", () => foam != null ? foam.FoamMap : null));
			}

			return textures;
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

		private void SearchShaderVariantCollection()
		{
			var editedWater = (Water)target;
			var transforms = FindObjectsOfType<Transform>();

			foreach(var root in transforms)
			{
				if(root.parent == null)     // if that's really a root
				{
					var waters = root.GetComponentsInChildren<Water>(true);

					foreach(var water in waters)
					{
						if(water != editedWater && water.ShaderCollection != null)
						{
							serializedObject.FindProperty("shaderCollection").objectReferenceValue = water.ShaderCollection;
							serializedObject.FindProperty("sceneHash").intValue = GetSceneHash();
							serializedObject.ApplyModifiedProperties();
							return;
						}
					}
				}
			}
		}

		private int GetSceneHash()
		{
			var md5 = System.Security.Cryptography.MD5.Create();

#if UNITY_5_2 || UNITY_5_1 || UNITY_5_0
			string sceneName = EditorApplication.currentScene + "#" + target.name;
#else
			string sceneName = (target as Water).gameObject.scene.name;
#endif

			if(!string.IsNullOrEmpty(sceneName))
			{
				var hash = md5.ComputeHash(System.Text.Encoding.ASCII.GetBytes(sceneName));
				return System.BitConverter.ToInt32(hash, 0);
			}
			else
				return -1;
		}

		private void UpdateWater()
		{
			EditorApplication.update -= UpdateWater;

			var water = target as Water;

			var versionProp = serializedObject.FindProperty("version");
			float currentVersion = versionProp.floatValue;

			if(currentVersion <= 1.0f)
			{
#pragma warning disable 0612
				var wavesFFT = water.GetComponent<WaterWavesFFT>();
				var wavesGerstner = water.GetComponent<WaterWavesGerstner>();

				if(wavesFFT != null || wavesGerstner != null)
				{
					var windWaves = water.GetComponent<WindWaves>();

					if(windWaves == null)
						windWaves = Undo.AddComponent<WindWaves>(water.gameObject);

					if(wavesFFT != null)
						windWaves.RenderMode = WaveSpectrumRenderMode.FullFFT;
					else if(wavesGerstner != null)
						windWaves.RenderMode = WaveSpectrumRenderMode.GerstnerAndFFTSlope;

					if(wavesFFT != null) DestroyImmediate(wavesFFT);
					if(wavesGerstner != null) DestroyImmediate(wavesGerstner);

					Debug.Log("Update notification: WaterWavesFFT and WaterWavesGerstner are now deprecated and replaced by WindWaves component. Some of the Water.cs properties was also moved to this class to make Water.cs lighter.");
				}
#pragma warning restore 0612
			}

			versionProp.floatValue = WaterProjectSettings.CurrentVersion;
			serializedObject.ApplyModifiedProperties();

			Debug.Log("Update was successful.");
		}

		enum ReflectionProbeUsage
		{
			Skybox = 0,
			BlendProbes = 1,
			BlendProbesAndSkybox = 2,
			Simple = 3,
		}
	}
}
