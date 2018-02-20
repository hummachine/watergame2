using System;
using UnityEngine;

namespace PlayWay.Water
{
	[RequireComponent(typeof(Water))]
	[RequireComponent(typeof(WindWaves))]
	[OverlayRendererOrder(1000)]
	[AddComponentMenu("Water/Foam", 1)]
	public class WaterFoam : MonoBehaviour, IWaterRenderAware, IOverlaysRenderer
	{
		[HideInInspector]
		[SerializeField]
		private Shader globalFoamSimulationShader;

		[HideInInspector]
		[SerializeField]
		private Shader localFoamSimulationShader;

		[Tooltip("Foam map supersampling in relation to the waves simulator resolution. Has to be a power of two (0.25, 0.5, 1, 2, etc.)")]
		[SerializeField]
		private float supersampling = 1.0f;

		private float foamIntensity = 1.0f;
		private float foamThreshold = 1.0f;
		private float foamFadingFactor = 0.85f;

		private RenderTexture foamMapA;
		private RenderTexture foamMapB;
		private Material globalFoamSimulationMaterial;
		private Material localFoamSimulationMaterial;
		private Vector2 lastCameraPos;
		private Vector2 deltaPosition;
		private Water water;
		private WindWaves windWaves;
		private WaterOverlays overlays;
		private int resolution;
		private bool firstFrame;

		private int foamParametersId;
		private int foamIntensityId;

		void Start()
		{
			water = GetComponent<Water>();
			windWaves = GetComponent<WindWaves>();
			overlays = GetComponent<WaterOverlays>();

			foamParametersId = Shader.PropertyToID("_FoamParameters");
			foamIntensityId = Shader.PropertyToID("_FoamIntensity");

			windWaves.ResolutionChanged.AddListener(OnResolutionChanged);

			resolution = Mathf.RoundToInt(windWaves.FinalResolution * supersampling);
			
			globalFoamSimulationMaterial = new Material(globalFoamSimulationShader);
			globalFoamSimulationMaterial.hideFlags = HideFlags.DontSave;

			localFoamSimulationMaterial = new Material(localFoamSimulationShader);
			localFoamSimulationMaterial.hideFlags = HideFlags.DontSave;

			firstFrame = true;
		}

		void OnEnable()
		{
			water = GetComponent<Water>();
			water.ProfilesChanged.AddListener(OnProfilesChanged);
			OnProfilesChanged(water);
        }

		void OnDisable()
		{
			water.InvalidateMaterialKeywords();
			water.ProfilesChanged.RemoveListener(OnProfilesChanged);
		}

		public Texture FoamMap
		{
			get { return foamMapA; }
		}

		private bool FloatingPointMipMapsSupport
		{
			get
			{
				string vendor = SystemInfo.graphicsDeviceVendor.ToLower();
				return !vendor.Contains("amd") && !vendor.Contains("ati") && !SystemInfo.graphicsDeviceName.ToLower().Contains("radeon") && WaterProjectSettings.Instance.AllowFloatingPointMipMaps;
			}
		}

		public void OnWaterRender(Camera camera)
		{

		}

		public void OnWaterPostRender(Camera camera)
		{

		}

		public void BuildShaderVariant(ShaderVariant variant, Water water, WaterQualityLevel qualityLevel)
		{
			OnValidate();

            variant.SetWaterKeyword("_WATER_FOAM_WS", enabled && overlays == null && CheckPreresquisites());
		}

		public void UpdateMaterial(Water water, WaterQualityLevel qualityLevel)
		{

		}

		private void SetupFoamMaterials()
		{
			if(globalFoamSimulationMaterial != null)
			{
				float t = foamThreshold * resolution / 2048.0f * 220.0f * 0.7f;
				globalFoamSimulationMaterial.SetVector(foamParametersId, new Vector4(foamIntensity * 0.6f, 0.0f, 0.0f, foamFadingFactor));
				globalFoamSimulationMaterial.SetVector(foamIntensityId, new Vector4(t / windWaves.TileSizes.x, t / windWaves.TileSizes.y, t / windWaves.TileSizes.z, t / windWaves.TileSizes.w));
            }
		}

		private void SetKeyword(Material material, string name, bool val)
		{
			if(val)
				material.EnableKeyword(name);
			else
				material.DisableKeyword(name);
		}

		private void SetKeyword(Material material, int index, params string[] names)
		{
			foreach(var name in names)
				material.DisableKeyword(name);

			material.EnableKeyword(names[index]);
		}

		void OnValidate()
		{
			if(globalFoamSimulationShader == null)
				globalFoamSimulationShader = Shader.Find("PlayWay Water/Foam/Global");

			if(localFoamSimulationShader == null)
				localFoamSimulationShader = Shader.Find("PlayWay Water/Foam/Local");
			
			supersampling = Mathf.ClosestPowerOfTwo(Mathf.RoundToInt(supersampling * 4096)) / 4096.0f;

			water = GetComponent<Water>();
			windWaves = GetComponent<WindWaves>();
			overlays = GetComponent<WaterOverlays>();
		}
		
		private void Dispose(bool completely)
		{
			if(foamMapA != null)
			{
				Destroy(foamMapA);
				Destroy(foamMapB);

				foamMapA = null;
				foamMapB = null;
			}
		}

		void OnDestroy()
		{
			Dispose(true);
		}

		void LateUpdate()
		{
			if(!firstFrame)
				UpdateFoamMap();
			else
				firstFrame = false;
		}

		private void CheckResources()
		{
			if(foamMapA == null)
			{
				foamMapA = CreateRT(0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear, TextureWrapMode.Repeat);
				foamMapB = CreateRT(0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear, TextureWrapMode.Repeat);

				RenderTexture.active = null;
			}
		}
		
		private RenderTexture CreateRT(int depth, RenderTextureFormat format, RenderTextureReadWrite readWrite, TextureWrapMode wrapMode)
		{
			var renderTexture = new RenderTexture(resolution, resolution, depth, format, readWrite);
			renderTexture.hideFlags = HideFlags.DontSave;
			renderTexture.wrapMode = wrapMode;
			if (FloatingPointMipMapsSupport)
			{
				renderTexture.filterMode = FilterMode.Trilinear;
				renderTexture.useMipMap = true;
				renderTexture.autoGenerateMips = true;
			}
			else
				renderTexture.filterMode = FilterMode.Bilinear;

			RenderTexture.active = renderTexture;
			GL.Clear(false, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));

			return renderTexture;
		}

		private void UpdateFoamMap()
		{
			if(!CheckPreresquisites())
				return;

			if(overlays == null)
			{
				CheckResources();
				SetupFoamMaterials();

				globalFoamSimulationMaterial.SetTexture("_DisplacementMap0", windWaves.WaterWavesFFT.GetDisplacementMap(0));
				globalFoamSimulationMaterial.SetTexture("_DisplacementMap1", windWaves.WaterWavesFFT.GetDisplacementMap(1));
				globalFoamSimulationMaterial.SetTexture("_DisplacementMap2", windWaves.WaterWavesFFT.GetDisplacementMap(2));
				globalFoamSimulationMaterial.SetTexture("_DisplacementMap3", windWaves.WaterWavesFFT.GetDisplacementMap(3));
				Graphics.Blit(foamMapA, foamMapB, globalFoamSimulationMaterial, 0);

				water.WaterMaterial.SetTexture("_FoamMapWS", foamMapB);

				SwapRenderTargets();
			}
		}

		private void OnResolutionChanged(WindWaves windWaves)
		{
			resolution = Mathf.RoundToInt(windWaves.FinalResolution * supersampling);

			Dispose(false);
		}

		private bool CheckPreresquisites()
		{
			return windWaves != null && windWaves.enabled && windWaves.FinalRenderMode == WaveSpectrumRenderMode.FullFFT;
		}

		private void OnProfilesChanged(Water water)
		{
			var profiles = water.Profiles;

			foamIntensity = 0.0f;
			foamThreshold = 0.0f;
			foamFadingFactor = 0.0f;
			float foamNormalScale = 0.0f;

			if(profiles != null)
			{
				foreach(var weightedProfile in profiles)
				{
					var profile = weightedProfile.profile;
					float weight = weightedProfile.weight;

					foamIntensity += profile.FoamIntensity * weight;
					foamThreshold += profile.FoamThreshold * weight;
					foamFadingFactor += profile.FoamFadingFactor * weight;
					foamNormalScale += profile.FoamNormalScale * weight;
                }
			}

			water.WaterMaterial.SetFloat("_FoamNormalScale", foamNormalScale);
		}

		private Vector2 RotateVector(Vector2 vec, float angle)
		{
			float s = Mathf.Sin(angle);
			float c = Mathf.Cos(angle);

			return new Vector2(c * vec.x + s * vec.y, c * vec.y + s * vec.x);
		}
		
		private void SwapRenderTargets()
		{
			var t = foamMapA;
			foamMapA = foamMapB;
			foamMapB = t;
		}
		
		public void RenderOverlays(WaterOverlaysData overlays)
		{
			if(!enabled)
				return;
			
			localFoamSimulationMaterial.CopyPropertiesFromMaterial(water.WaterMaterial);

			float t = foamThreshold * overlays.DynamicDisplacementMap.width / 2048.0f * 0.7f;
			localFoamSimulationMaterial.SetVector(foamParametersId, new Vector4(foamIntensity * 0.6f, t, 0.0f, foamFadingFactor));
			localFoamSimulationMaterial.SetTexture("_DisplacementMap", overlays.DynamicDisplacementMap);
			localFoamSimulationMaterial.SetTexture("_SlopeMapPrevious", overlays.SlopeMapPrevious);
			localFoamSimulationMaterial.SetTexture("_TotalDisplacementMap", overlays.GetTotalDisplacementMap());
			localFoamSimulationMaterial.SetVector("_Offset", new Vector4((UnityEngine.Random.value - 0.5f) * 0.001f, (UnityEngine.Random.value - 0.5f) * 0.001f, 0.0f, 0.0f));
			Graphics.Blit(overlays.SlopeMapPrevious, overlays.SlopeMap, localFoamSimulationMaterial, overlays.Initialization ? 1 : 0);
        }
	}
}
