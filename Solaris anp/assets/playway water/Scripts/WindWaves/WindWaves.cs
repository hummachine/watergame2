using System;
using UnityEngine;
using UnityEngine.Events;

namespace PlayWay.Water
{
	/// <summary>
	/// Renders wind waves on water surface and also resolves them on CPU for physics etc.
	/// </summary>
	///
	[AddComponentMenu("Water/Wind Waves", 0)]
	[ExecuteInEditMode]
	public class WindWaves : MonoBehaviour, IWaterDisplacements, IWaterRenderAware
	{
		[HideInInspector]
		[SerializeField]
		private Shader spectrumShader;

		[SerializeField]
		private Transform windDirectionPointer;

		[Tooltip("Higher values increase quality, but also decrease performance. Directly controls quality of waves, foam and spray.")]
		[SerializeField]
		private int resolution = 256;

		[Tooltip("Determines if 32-bit precision buffers should be used for computations (Default: off). Not supported on most mobile devices. This setting has impact on performance, even on PCs.\n\nTips:\n 1024 and higher - The difference is clearly visible, use this option on higher quality settings.\n 512 or lower - Keep it disabled.")]
		[SerializeField]
		private bool highPrecision = true;

		[Tooltip("Determines how small waves should be considered by the CPU in ongoing computations. Higher values will increase the precision of all wave computations done on CPU (GetHeightAt etc.), but may decrease performance. Most waves in the ocean spectrum have negligible visual impact and may be safely ignored.")]
		[SerializeField]
		private float cpuWaveThreshold = 0.008f;

		[Tooltip("How many waves at most should be considered by the CPU.")]
		[SerializeField]
		private int cpuMaxWaves = 2500;

		[Tooltip("Determines final CPU FFT resolution (0 - acceptable, 1 - good, 2 - perfect, 3 - insane).")]
		[Range(0, 3)]
		[SerializeField]
		private int cpuFFTPrecisionBoost = 1;

		[Tooltip("Copying wave spectrum from other fluid will make this instance a lot faster.")]
		[SerializeField]
		private WindWaves copyFrom;

		[SerializeField]
		private WaveSpectrumRenderMode renderMode;

		[SerializeField]
		private WindWavesEvent windDirectionChanged;

		[SerializeField]
		private WindWavesEvent resolutionChanged;

		[SerializeField]
		private WavesRendererFFT waterWavesFFT;

		[SerializeField]
		private WavesRendererGerstner waterWavesGerstner;
		
		[SerializeField]
		private DynamicSmoothness dynamicSmoothness;

		// I didn't found any practical reason for now to adjust these scales in inspector
		//[SerializeField]
		private Vector4 tileSizeScales = new Vector4(0.79241f, 0.163151f, 3.175131f, 13.7315131f);

		private Water water;
		private int finalResolution;
		private bool finalHighPrecision;
		private float windSpeedMagnitude;
		private float tileSize;
		private Vector4 tileSizes;
		private Vector4 unscaledTileSizes;
		private Vector2 windDirection;
		private Vector2 windSpeed;
		private WaveSpectrumRenderMode finalRenderMode;
		private SpectrumResolver spectrumResolver;
		private WindWaves runtimeCopyFrom;
		private Vector2 lastWaterPos;
		private bool initialized;
		private bool windSpeedChanged;

		// cached shader ids
		private int tileSizeId;
		private int tileSizeScalesId;
		private int maxDisplacementId;

		void Awake()
		{
			runtimeCopyFrom = copyFrom;

			tileSizeId = Shader.PropertyToID("_WaterTileSize");
			tileSizeScalesId = Shader.PropertyToID("_WaterTileSizeScales");
			maxDisplacementId = Shader.PropertyToID("_MaxDisplacement");
			
			CheckSupport();

			if(!initialized)
				Initialize();

			ResolveFinalSettings(WaterQualitySettings.Instance.CurrentQualityLevel);
		}

		void OnEnable()
		{
			if(!initialized)
				Initialize();

			UpdateWind();

			CreateObjects();
		}

		void OnDisable()
		{
			if(waterWavesFFT != null) waterWavesFFT.Disable();
			if(waterWavesGerstner != null) waterWavesGerstner.Disable();
			if(dynamicSmoothness != null) dynamicSmoothness.FreeResources();
		}

		void Start()
		{
			dynamicSmoothness.Start(this);

			if(!Application.isPlaying)
				return;

			water.ProfilesChanged.AddListener(OnProfilesChanged);
			OnProfilesChanged(water);
		}

		public WindWaves CopyFrom
		{
			get { return runtimeCopyFrom; }
			set
			{
				if(copyFrom != value || runtimeCopyFrom != value)
				{
					copyFrom = value;
					runtimeCopyFrom = value;
					
					dynamicSmoothness.OnCopyModeChanged();
					waterWavesFFT.OnCopyModeChanged();
				}
			}
		}
		
		public SpectrumResolver SpectrumResolver
		{
			get { return copyFrom == null ? spectrumResolver : copyFrom.spectrumResolver; }
		}

		public WavesRendererFFT WaterWavesFFT
		{
			get { return waterWavesFFT; }
		}

		public WavesRendererGerstner WaterWavesGerstner
		{
			get { return waterWavesGerstner; }
		}
		
		public DynamicSmoothness DynamicSmoothness
		{
			get { return dynamicSmoothness; }
		}

		public WaveSpectrumRenderMode RenderMode
		{
			get { return renderMode; }
			set
			{
				renderMode = value;
				ResolveFinalSettings(WaterQualitySettings.Instance.CurrentQualityLevel);
			}
		}

		public WaveSpectrumRenderMode FinalRenderMode
		{
			get { return finalRenderMode; }
		}

		public Vector4 TileSizes
		{
			get { return tileSizes; }
		}

		public Vector4 UnscaledTileSizes
		{
			get { return unscaledTileSizes; }
		}

		/// <summary>
		/// Current wind speed as resolved from the currently set profiles.
		/// </summary>
		public Vector2 WindSpeed
		{
			get { return windSpeed; }
		}

		public bool WindSpeedChanged
		{
			get { return windSpeedChanged; }
		}

		/// <summary>
		/// Current wind direction. It's controlled by the WindDirectionPointer.
		/// </summary>
		public Vector2 WindDirection
		{
			get { return windDirection; }
		}

		public Transform WindDirectionPointer
		{
			get { return windDirectionPointer; }
		}

		/// <summary>
		/// Event invoked when wind direction changes.
		/// </summary>
		public WindWavesEvent WindDirectionChanged
		{
			get { return windDirectionChanged; }
		}

		/// <summary>
		/// Event invoked when wind spectrum resolution changes.
		/// </summary>
		public WindWavesEvent ResolutionChanged
		{
			get { return resolutionChanged ?? (resolutionChanged = new WindWavesEvent()); }
		}

		public int Resolution
		{
			get { return resolution; }
			set
			{
				if(resolution == value)
					return;

				resolution = value;
				ResolveFinalSettings(WaterQualitySettings.Instance.CurrentQualityLevel);
			}
		}

		public int FinalResolution
		{
			get { return finalResolution; }
		}

		public bool FinalHighPrecision
		{
			get { return finalHighPrecision; }
		}

		public bool HighPrecision
		{
			get { return highPrecision; }
		}

		public int CpuMaxWaves
		{
			get { return cpuMaxWaves; }
		}

		public float CpuWaveThreshold
		{
			get { return cpuWaveThreshold; }
		}

		public int CpuFFTPrecisionBoost
		{
			get { return cpuFFTPrecisionBoost; }
		}

		public Vector4 TileSizeScales
		{
			get { return tileSizeScales; }
		}

		public float MaxVerticalDisplacement
		{
			get { return spectrumResolver.MaxVerticalDisplacement; }
		}

		public float MaxHorizontalDisplacement
		{
			get { return spectrumResolver.MaxHorizontalDisplacement; }
		}

		public void OnValidate()
		{
			if(spectrumShader == null)
				spectrumShader = Shader.Find("PlayWay Water/Spectrum/Water Spectrum");

			if(dynamicSmoothness != null)
				dynamicSmoothness.OnValidate(this);

			if(isActiveAndEnabled && Application.isPlaying)
				CopyFrom = copyFrom;

#if UNITY_EDITOR
			if(copyFrom != null && !Application.isPlaying)
			{
				renderMode = copyFrom.renderMode;
				resolution = copyFrom.resolution;
				highPrecision = copyFrom.highPrecision;
				cpuWaveThreshold = copyFrom.cpuWaveThreshold;
				cpuMaxWaves = copyFrom.cpuMaxWaves;
            }
#endif

			if(spectrumResolver != null)
			{
				ResolveFinalSettings(WaterQualitySettings.Instance.CurrentQualityLevel);

				waterWavesFFT.OnValidate(this);
				waterWavesGerstner.OnValidate(this);

				water.OnValidate();
			}

			if(water != null)
				UpdateShaderParams();
		}

		void Update()
		{
			UpdateWind();

			if(!Application.isPlaying || runtimeCopyFrom != null) return;

			spectrumResolver.Update();
			dynamicSmoothness.Update();
			UpdateShaderParams();
		}

		/// <summary>
		/// Resolves final component settings based on the desired values, quality settings and hardware limitations.
		/// </summary>
		internal void ResolveFinalSettings(WaterQualityLevel quality)
		{
			CreateObjects();

			var wavesMode = quality.wavesMode;

			if(wavesMode == WaterWavesMode.DisallowAll)
			{
				enabled = false;
				return;
			}

			bool supportsFloats = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat) || SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf);

			int finalResolution = Mathf.Min(resolution, quality.maxSpectrumResolution, SystemInfo.maxTextureSize);
			bool finalHighPrecision = highPrecision && quality.allowHighPrecisionTextures && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat);
			
			if(renderMode == WaveSpectrumRenderMode.FullFFT && wavesMode == WaterWavesMode.AllowAll && supportsFloats)
				finalRenderMode = WaveSpectrumRenderMode.FullFFT;
			else if(renderMode <= WaveSpectrumRenderMode.GerstnerAndFFTSlope && wavesMode <= WaterWavesMode.AllowSlopeFFT && supportsFloats)
				finalRenderMode = WaveSpectrumRenderMode.GerstnerAndFFTSlope;
			else
				finalRenderMode = WaveSpectrumRenderMode.Gerstner;

			if(this.finalResolution != finalResolution)
			{
				lock (this)
				{
					this.finalResolution = finalResolution;
					this.finalHighPrecision = finalHighPrecision;

					if(spectrumResolver != null)
						spectrumResolver.OnMapsFormatChanged(true);

					if(ResolutionChanged != null)
						ResolutionChanged.Invoke(this);
				}
			}
			else if(this.finalHighPrecision != finalHighPrecision)
			{
				lock (this)
				{
					this.finalHighPrecision = finalHighPrecision;

					if(spectrumResolver != null)
						spectrumResolver.OnMapsFormatChanged(false);
				}
			}
			
			switch(finalRenderMode)
			{
				case WaveSpectrumRenderMode.FullFFT:
				{
					waterWavesFFT.RenderedMaps = WavesRendererFFT.MapType.Displacement | WavesRendererFFT.MapType.Slope;
					waterWavesFFT.Enable(this);

					waterWavesGerstner.Disable();
					break;
				}

				case WaveSpectrumRenderMode.GerstnerAndFFTSlope:
				{
					waterWavesFFT.RenderedMaps = WavesRendererFFT.MapType.Slope;
					waterWavesFFT.Enable(this);

					waterWavesGerstner.Enable(this);
					break;
				}

				case WaveSpectrumRenderMode.Gerstner:
				{
					waterWavesFFT.Disable();
                    waterWavesGerstner.Enable(this);
					break;
				}
			}
		}

		private void UpdateShaderParams()
		{
			float scaledTileSize = tileSize * water.UniformWaterScale;
			tileSizes.x = scaledTileSize * tileSizeScales.x;
			tileSizes.y = scaledTileSize * tileSizeScales.y;
			tileSizes.z = scaledTileSize * tileSizeScales.z;
			tileSizes.w = scaledTileSize * tileSizeScales.w;
			water.SetVectorDirect(tileSizeId, tileSizes);                        // _WaterTileSize
		}

		private void OnProfilesChanged(Water water)
		{
			tileSize = 0.0f;
			windSpeedMagnitude = 0.0f;

			foreach(var weightedProfile in water.Profiles)
			{
				var profile = weightedProfile.profile;
				float weight = weightedProfile.weight;

				tileSize += profile.TileSize * profile.TileScale * weight;
				windSpeedMagnitude += profile.WindSpeed * weight;
			}

			// scale by quality settings
			var waterQualitySettings = WaterQualitySettings.Instance;
			tileSize *= waterQualitySettings.TileSizeScale;

			unscaledTileSizes = tileSize * tileSizeScales;

			float scaledTileSize = tileSize * water.UniformWaterScale;
			tileSizes.x = scaledTileSize * tileSizeScales.x;
			tileSizes.y = scaledTileSize * tileSizeScales.y;
			tileSizes.z = scaledTileSize * tileSizeScales.z;
			tileSizes.w = scaledTileSize * tileSizeScales.w;
			water.SetVectorDirect(tileSizeId, tileSizes);                        // _WaterTileSize

			water.SetVectorDirect(tileSizeScalesId, new Vector4(tileSizeScales.x / tileSizeScales.y, tileSizeScales.x / tileSizeScales.z, tileSizeScales.x / tileSizeScales.w, 0.0f));         // _WaterTileSizeScales

			spectrumResolver.OnProfilesChanged();

			water.SetFloatDirect(maxDisplacementId, spectrumResolver.MaxHorizontalDisplacement);
		}

		private void Initialize()
		{
			OnValidate();

			water = GetComponent<Water>();

			if(spectrumResolver == null) spectrumResolver = new SpectrumResolver(this, spectrumShader);
			if(windDirectionChanged == null) windDirectionChanged = new WindWavesEvent();

			CreateObjects();

			initialized = true;
		}

		void OnDestroy()
		{
			if(spectrumResolver != null)
			{
				spectrumResolver.OnDestroy();
				spectrumResolver = null;
			}
		}

		private void UpdateWind()
		{
			Vector2 newWindDirection;
			
			if(windDirectionPointer != null)
			{
				Vector3 forward = windDirectionPointer.forward;
				newWindDirection = new Vector2(forward.x, forward.z).normalized;
			}
			else
				newWindDirection = new Vector2(1.0f, 0.0f);

			Vector2 newWindSpeed = windDirection * windSpeedMagnitude;

			if(windDirection != newWindDirection || windSpeed != newWindSpeed)
			{
				windDirection = newWindDirection;
				windSpeed = newWindSpeed;
				windSpeedChanged = true;

				spectrumResolver.SetWindDirection(windDirection);
			}
			else
				windSpeedChanged = false;
        }

		private void CreateObjects()
		{
			if(waterWavesFFT == null) waterWavesFFT = new WavesRendererFFT();
			if(waterWavesGerstner == null) waterWavesGerstner = new WavesRendererGerstner();
			if(dynamicSmoothness == null) dynamicSmoothness = new DynamicSmoothness();
		}

		private void CheckSupport()
		{
			if(highPrecision && (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGFloat) || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat)))
				finalHighPrecision = false;

			if(!highPrecision && (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGHalf) || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)))
			{
				if(SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGFloat))
					finalHighPrecision = true;
				else if(renderMode == WaveSpectrumRenderMode.FullFFT)
				{
#if UNITY_EDITOR
					Debug.LogError("Your hardware doesn't support floating point render textures. FFT water waves won't work in editor.");
#endif

					finalRenderMode = WaveSpectrumRenderMode.Gerstner;
					return;
				}
			}
		}

		public void OnWaterRender(Camera camera)
		{
			if(!Application.isPlaying || !enabled) return;

			if(waterWavesFFT.Enabled)
				waterWavesFFT.OnWaterRender(camera);

			if(waterWavesGerstner.Enabled)
				waterWavesGerstner.OnWaterRender(camera);
		}

		public void OnWaterPostRender(Camera camera)
		{
			
		}

		public void UpdateMaterial(Water water, WaterQualityLevel qualityLevel)
		{
			
		}

		public void BuildShaderVariant(ShaderVariant variant, Water water, WaterQualityLevel qualityLevel)
		{
			CreateObjects();
			ResolveFinalSettings(qualityLevel);

			waterWavesFFT.BuildShaderVariant(variant, water, this, qualityLevel);
			waterWavesGerstner.BuildShaderVariant(variant, water, this, qualityLevel);

			variant.SetWaterKeyword("_INCLUDE_SLOPE_VARIANCE", dynamicSmoothness.Enabled);
		}

		public Vector3 GetDisplacementAt(float x, float z, float spectrumStart, float spectrumEnd, float time)
		{
			return spectrumResolver.GetDisplacementAt(x, z, spectrumStart, spectrumEnd, time);
		}

		public Vector2 GetHorizontalDisplacementAt(float x, float z, float spectrumStart, float spectrumEnd, float time)
		{
			return spectrumResolver.GetHorizontalDisplacementAt(x, z, spectrumStart, spectrumEnd, time);
		}

		public float GetHeightAt(float x, float z, float spectrumStart, float spectrumEnd, float time)
		{
			return spectrumResolver.GetHeightAt(x, z, spectrumStart, spectrumEnd, time);
		}

		public Vector4 GetForceAndHeightAt(float x, float z, float spectrumStart, float spectrumEnd, float time)
		{
			return spectrumResolver.GetForceAndHeightAt(x, z, spectrumStart, spectrumEnd, time);
		}

		[Serializable]
		public class WindWavesEvent : UnityEvent<WindWaves> { };
	}

	public enum WaveSpectrumRenderMode
	{
		FullFFT,
		GerstnerAndFFTSlope,
		Gerstner
	}
}
