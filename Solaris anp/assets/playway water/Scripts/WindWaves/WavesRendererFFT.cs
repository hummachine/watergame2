using UnityEngine;

namespace PlayWay.Water
{
	/// <summary>
	/// Displays water spectrum using Fast Fourier Transform. Uses vertex shader texture fetch available on platforms with Shader Model 3.0+.
	/// </summary>
	[System.Serializable]
	public class WavesRendererFFT
	{
		[HideInInspector]
		[SerializeField]
		private Shader fftShader;

		[HideInInspector]
		[SerializeField]
		private Shader fftUtilitiesShader;

		[SerializeField]
		private ComputeShader dx11FFT;

		[Tooltip("Determines if GPU partial derivatives or Fast Fourier Transform (high quality) should be used to compute slope map (Recommended: on). Works only if displacement map rendering is enabled.")]
		[SerializeField]
		private bool highQualitySlopeMaps = true;

#pragma warning disable 0414
		[Tooltip("Check this option, if your water is flat or game crashes instantly on a DX11 GPU (in editor or build). Compute shaders are very fast, so use this as a last resort.")]
		[SerializeField]
		private bool forcePixelShader = false;
#pragma warning restore 0414

		[Tooltip("Fixes crest artifacts during storms, but lowers overall quality. Enabled by default when used with additive water volumes as it is actually needed and disabled in all other cases.")]
		[SerializeField]
		private FlattenMode flattenMode = FlattenMode.Auto;

		private RenderTexture[] slopeMaps;
		private RenderTexture[] displacementMaps;
		private RenderTexture displacedHeightMap;

		private RenderTexturesCache singleTargetCache;
		private RenderTexturesCache doubleTargetCache;
		private RenderTexturesCache displacedHeightMapsCache;

		private Water water;
		private WindWaves windWaves;

		private GpuFFT heightFFT;
		private GpuFFT slopeFFT;
		private GpuFFT displacementFFT;
		private Material fftUtilitiesMaterial;

		private MapType renderedMaps;
		private bool finalHighQualitySlopeMaps;
		private bool flatten;
		private bool enabled;
		private bool copyModeDirty;
		private int waveMapsFrame, displacementMapJacobianFrame;
		private WindWaves lastCopyFrom;

		public event System.Action ResourcesChanged;

		static private ComputeShader defaultDx11Fft;
		static private Vector4[] offsets = new Vector4[] { new Vector4(0.0f, 0.0f, 0.0f, 0.0f), new Vector4(0.5f, 0.0f, 0.0f, 0.0f), new Vector4(0.0f, 0.5f, 0.0f, 0.0f), new Vector4(0.5f, 0.5f, 0.0f, 0.0f) };
		static private Vector4[] offsetsDual = new Vector4[] { new Vector4(0.0f, 0.0f, 0.5f, 0.0f), new Vector4(0.0f, 0.5f, 0.5f, 0.5f) };

		internal void Enable(WindWaves windWaves)
		{
			if(enabled) return;

			if(dx11FFT != null)
				defaultDx11Fft = dx11FFT;
			else
				dx11FFT = defaultDx11Fft;

			enabled = true;

			this.water = windWaves.GetComponent<Water>();
			this.windWaves = windWaves;

			OnCopyModeChanged();

			if(Application.isPlaying)
			{
				if(lastCopyFrom == null)
					ValidateResources();

				windWaves.ResolutionChanged.AddListener(OnResolutionChanged);
			}

			OnValidate(windWaves);

			water.InvalidateMaterialKeywords();

			fftUtilitiesMaterial = new Material(fftUtilitiesShader);
			fftUtilitiesMaterial.hideFlags = HideFlags.DontSave;
		}

		internal void Disable()
		{
			if(!enabled) return;

			enabled = false;

			Dispose(false);

			if(water != null)
				water.InvalidateMaterialKeywords();
		}

		public MapType RenderedMaps
		{
			get { return renderedMaps; }
			set
			{
				renderedMaps = value;

				if(enabled && Application.isPlaying)
				{
					Dispose(false);
					ValidateResources();
				}
			}
		}

		public bool Enabled
		{
			get { return enabled; }
		}

		private bool FloatingPointMipMapsSupport
		{
			get
			{
				string vendor = SystemInfo.graphicsDeviceVendor.ToLower();
				return !vendor.Contains("amd") && !vendor.Contains("ati") && !SystemInfo.graphicsDeviceName.ToLower().Contains("radeon") && WaterProjectSettings.Instance.AllowFloatingPointMipMaps;
			}
		}

		/// <summary>
		/// May be innacurate if flattening is not used.
		/// </summary>
		public Texture DisplacedHeightMap
		{
			get { return displacedHeightMap; }
		}

		public Texture GetDisplacementMap(int index)
		{
			return displacementMaps != null ? displacementMaps[index] : null;
		}

		public Texture GetSlopeMap(int index)
		{
			return slopeMaps[index];
		}
		
		public void BuildShaderVariant(ShaderVariant variant, Water water, WindWaves windWaves, WaterQualityLevel qualityLevel)
		{
			OnValidate(windWaves);

			ResolveFinalSettings(qualityLevel);

			variant.SetWaterKeyword("_WAVES_FFT_SLOPE", enabled && renderedMaps == MapType.Slope);
			variant.SetUnityKeyword("_WAVES_ALIGN", (!water.Volume.Boundless && water.Volume.HasRenderableAdditiveVolumes && flattenMode == FlattenMode.Auto) || flattenMode == FlattenMode.ForcedOn);
			variant.SetUnityKeyword("_WAVES_FFT", enabled && (renderedMaps & MapType.Displacement) != 0);
		}

		private void ValidateResources()
		{
			if(windWaves.CopyFrom == null)
			{
				ValidateFFT(ref heightFFT, (renderedMaps & MapType.Displacement) != 0, false);
				ValidateFFT(ref displacementFFT, (renderedMaps & MapType.Displacement) != 0, true);
				ValidateFFT(ref slopeFFT, (renderedMaps & MapType.Slope) != 0, true);
			}

			if(displacementMaps == null || slopeMaps == null || displacedHeightMap == null)
			{
				bool flatten = (!water.Volume.Boundless && flattenMode == FlattenMode.Auto) || flattenMode == FlattenMode.ForcedOn;

				if(this.flatten != flatten)
				{
					this.flatten = flatten;

					if(displacedHeightMap != null)
					{
						displacedHeightMap.Destroy();
						displacedHeightMap = null;
					}
				}
				
				RenderTexture[] usedDisplacementMaps, usedSlopeMaps;
				RenderTexture usedDisplacedHeightMap;

				if(windWaves.CopyFrom == null)
				{
					int resolution = windWaves.FinalResolution;
					int displacedHeightMapResolution = flatten ? resolution : (resolution >> 2);
					int packResolution = resolution << 1;
					singleTargetCache = RenderTexturesCache.GetCache(packResolution, packResolution, 0, RenderTextureFormat.RHalf, true, heightFFT is Dx11FFT);
					doubleTargetCache = RenderTexturesCache.GetCache(packResolution, packResolution, 0, RenderTextureFormat.RGHalf, true, displacementFFT is Dx11FFT);
					displacedHeightMapsCache = RenderTexturesCache.GetCache(displacedHeightMapResolution, displacedHeightMapResolution, 0, RenderTextureFormat.ARGBHalf, true, false, false);

					if(displacementMaps == null && (renderedMaps & MapType.Displacement) != 0)
						CreateRenderTextures(ref displacementMaps, RenderTextureFormat.ARGBHalf, 4, true);

					if(slopeMaps == null && (renderedMaps & MapType.Slope) != 0)
						CreateRenderTextures(ref slopeMaps, RenderTextureFormat.ARGBHalf, 2, true);

					if(displacedHeightMap == null)
					{
						displacedHeightMap = new RenderTexture(displacedHeightMapResolution, displacedHeightMapResolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
						displacedHeightMap.hideFlags = HideFlags.DontSave;
						displacedHeightMap.wrapMode = TextureWrapMode.Repeat;

						if(FloatingPointMipMapsSupport)
						{
							displacedHeightMap.filterMode = FilterMode.Trilinear;
							displacedHeightMap.useMipMap = true;
							displacedHeightMap.autoGenerateMips = true;
						}
						else
							displacedHeightMap.filterMode = FilterMode.Bilinear;
					}

					usedDisplacementMaps = displacementMaps;
					usedSlopeMaps = slopeMaps;
					usedDisplacedHeightMap = displacedHeightMap;
                }
				else
				{
					var copyFrom = windWaves.CopyFrom;

					if(copyFrom.WaterWavesFFT.windWaves == null)
						copyFrom.ResolveFinalSettings(WaterQualitySettings.Instance.CurrentQualityLevel);

					copyFrom.WaterWavesFFT.ValidateResources();
					
					usedDisplacementMaps = copyFrom.WaterWavesFFT.displacementMaps;
					usedSlopeMaps = copyFrom.WaterWavesFFT.slopeMaps;
					usedDisplacedHeightMap = copyFrom.WaterWavesFFT.displacedHeightMap;
                }

				for(int scaleIndex = 0; scaleIndex < 4; ++scaleIndex)
				{
					string suffix = scaleIndex != 0 ? scaleIndex.ToString() : "";

					if(usedDisplacementMaps != null)
					{
						water.WaterMaterial.SetTexture("_GlobalDisplacementMap" + suffix, usedDisplacementMaps[scaleIndex]);
						water.WaterBackMaterial.SetTexture("_GlobalDisplacementMap" + suffix, usedDisplacementMaps[scaleIndex]);
					}

					if(scaleIndex < 2 && usedSlopeMaps != null)
					{
						water.WaterMaterial.SetTexture("_GlobalNormalMap" + suffix, usedSlopeMaps[scaleIndex]);
						water.WaterBackMaterial.SetTexture("_GlobalNormalMap" + suffix, usedSlopeMaps[scaleIndex]);
					}
				}
				
				water.WaterMaterial.SetTexture("_DisplacedHeightMaps", usedDisplacedHeightMap);
				water.WaterBackMaterial.SetTexture("_DisplacedHeightMaps", usedDisplacedHeightMap);
				water.WaterVolumeMaterial.SetTexture("_DisplacedHeightMaps", usedDisplacedHeightMap);

				if(ResourcesChanged != null)
					ResourcesChanged();
            }
		}

		public void OnCopyModeChanged()
		{
			copyModeDirty = true;

			if(lastCopyFrom != null)
				lastCopyFrom.WaterWavesFFT.ResourcesChanged -= ValidateResources;

			if(windWaves.CopyFrom != null)
				windWaves.CopyFrom.WaterWavesFFT.ResourcesChanged += ValidateResources;

			lastCopyFrom = windWaves.CopyFrom;

			Dispose(false);
		}

		private void CreateRenderTextures(ref RenderTexture[] renderTextures, RenderTextureFormat format, int count, bool mipMaps)
		{
			renderTextures = new RenderTexture[count];

			for(int i = 0; i < count; ++i)
				renderTextures[i] = CreateRenderTexture(format, mipMaps);
		}

		private RenderTexture CreateRenderTexture(RenderTextureFormat format, bool mipMaps)
		{
			var texture = new RenderTexture(windWaves.FinalResolution, windWaves.FinalResolution, 0, format, RenderTextureReadWrite.Linear);
			texture.hideFlags = HideFlags.DontSave;
			texture.wrapMode = TextureWrapMode.Repeat;

			if(mipMaps && FloatingPointMipMapsSupport)
			{
				texture.filterMode = FilterMode.Trilinear;
				texture.useMipMap = true;
				texture.autoGenerateMips = true;
			}
			else
				texture.filterMode = FilterMode.Bilinear;

			return texture;
		}

		private void ValidateFFT(ref GpuFFT fft, bool present, bool twoChannels)
		{
			if(present)
			{
				if(fft == null)
					fft = ChooseBestFFTAlgorithm(twoChannels);
			}
			else if(fft != null)
			{
				fft.Dispose();
				fft = null;
			}
		}

		private GpuFFT ChooseBestFFTAlgorithm(bool twoChannels)
		{
			GpuFFT fft;

			int resolution = windWaves.FinalResolution;

#if !UNITY_IOS && !UNITY_ANDROID && !UNITY_PS3 && !UNITY_PS4 && !UNITY_BLACKBERRY && !UNITY_TIZEN && !UNITY_WEBGL && !UNITY_STANDALONE_OSX && !UNITY_STANDALONE_LINUX && !UNITY_EDITOR_OSX
			if(!forcePixelShader && dx11FFT != null && SystemInfo.supportsComputeShaders && /*resolution >= 128 && */resolution <= 512)
				fft = new Dx11FFT(dx11FFT, resolution, windWaves.FinalHighPrecision || resolution >= 2048, twoChannels);
			else
#endif
				fft = new PixelShaderFFT(fftShader, resolution, windWaves.FinalHighPrecision || resolution >= 2048, twoChannels);

			fft.SetupMaterials();

			return fft;
		}

		internal void ResolveFinalSettings(WaterQualityLevel qualityLevel)
		{
			finalHighQualitySlopeMaps = highQualitySlopeMaps;

			if(!qualityLevel.allowHighQualitySlopeMaps)
				finalHighQualitySlopeMaps = false;

			if((renderedMaps & MapType.Displacement) == 0)           // if heightmap is not rendered, only high-quality slope map is possible
				finalHighQualitySlopeMaps = true;
		}

		internal void OnValidate(WindWaves windWaves)
		{
			if(fftShader == null)
				fftShader = Shader.Find("PlayWay Water/Base/FFT");

			if(fftUtilitiesShader == null)
				fftUtilitiesShader = Shader.Find("PlayWay Water/Utilities/FFT Utilities");

#if UNITY_EDITOR
			if(dx11FFT == null)
			{
				var guids = UnityEditor.AssetDatabase.FindAssets("\"DX11 FFT\" t:ComputeShader");

				if(guids.Length != 0)
				{
					string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
					dx11FFT = (ComputeShader)UnityEditor.AssetDatabase.LoadAssetAtPath(path, typeof(ComputeShader));
					UnityEditor.EditorUtility.SetDirty(windWaves);
				}
			}
#endif

			if(Application.isPlaying && enabled)
				ResolveFinalSettings(WaterQualitySettings.Instance.CurrentQualityLevel);
		}

		void OnDestroy()
		{
			Dispose(true);
		}

		private void Dispose(bool total)
		{
			waveMapsFrame = -1;

			if(heightFFT != null)
			{
				heightFFT.Dispose();
				heightFFT = null;
			}

			if(slopeFFT != null)
			{
				slopeFFT.Dispose();
				slopeFFT = null;
			}

			if(displacementFFT != null)
			{
				displacementFFT.Dispose();
				displacementFFT = null;
			}

			if(displacedHeightMap != null)
			{
				displacedHeightMap.Destroy();
				displacedHeightMap = null;
			}

			if(slopeMaps != null)
			{
				foreach(var slopeMap in slopeMaps)
					slopeMap.Destroy();

				slopeMaps = null;
			}

			if(displacementMaps != null)
			{
				foreach(var displacementMap in displacementMaps)
					displacementMap.Destroy();

				displacementMaps = null;
			}

			if(total)
			{
				if(fftUtilitiesMaterial != null)
				{
					fftUtilitiesMaterial.Destroy();
					fftUtilitiesMaterial = null;
				}
			}
		}

		public void OnWaterRender(Camera camera)
		{
			if(fftUtilitiesMaterial == null) return;

			ValidateWaveMaps();
		}

		private void OnResolutionChanged(WindWaves windWaves)
		{
			Dispose(false);
			ValidateResources();
		}

		private void ValidateWaveMaps()
		{
			int frameCount = Time.frameCount;
			
			if(waveMapsFrame == frameCount || !Application.isPlaying)
				return;         // it's already done

			if(lastCopyFrom != null)
			{
				if(copyModeDirty)
				{
					copyModeDirty = false;
					ValidateResources();
				}

				return;
			}
			
			waveMapsFrame = frameCount;

			// render needed spectra
			Texture heightSpectrum, slopeSpectrum, displacementSpectrum;
			RenderSpectra(out heightSpectrum, out slopeSpectrum, out displacementSpectrum);

			// displacement
			if((renderedMaps & MapType.Displacement) != 0)
			{
				ClearDisplacedHeightMaps();

				TemporaryRenderTexture displacedHeightMapTemp = displacedHeightMapsCache.GetTemporary();
				TemporaryRenderTexture packedHeightMaps = singleTargetCache.GetTemporary();
				TemporaryRenderTexture packedHorizontalDisplacementMaps = doubleTargetCache.GetTemporary();

				heightFFT.ComputeFFT(heightSpectrum, packedHeightMaps);
				displacementFFT.ComputeFFT(displacementSpectrum, packedHorizontalDisplacementMaps);

				fftUtilitiesMaterial.SetTexture("_HeightTex", packedHeightMaps);
				fftUtilitiesMaterial.SetTexture("_DisplacementTex", packedHorizontalDisplacementMaps);
				fftUtilitiesMaterial.SetFloat("_HorizontalDisplacementScale", water.HorizontalDisplacementScale);

				for(int scaleIndex = 0; scaleIndex < 4; ++scaleIndex)
				{
					fftUtilitiesMaterial.SetFloat("_JacobianScale", water.HorizontalDisplacementScale * 0.1f * displacementMaps[scaleIndex].width / windWaves.TileSizes[scaleIndex]);     // * 220.0f * displacementMaps[scaleIndex].width / (2048.0f * water.SpectraRenderer.TileSizes[scaleIndex])
					fftUtilitiesMaterial.SetVector("_Offset", offsets[scaleIndex]);

					Graphics.Blit(null, displacementMaps[scaleIndex], fftUtilitiesMaterial, 1);

					RenderDisplacedHeightMaps(displacementMaps[scaleIndex], displacedHeightMapTemp, scaleIndex);
				}

				packedHeightMaps.Dispose();
				packedHorizontalDisplacementMaps.Dispose();

				// copy and generate mip maps
				Graphics.Blit(displacedHeightMapTemp, displacedHeightMap);

				displacedHeightMapTemp.Dispose();
            }

			// slope
			if((renderedMaps & MapType.Slope) != 0)
			{
				if(!finalHighQualitySlopeMaps)
				{
					for(int scalesIndex = 0; scalesIndex < 2; ++scalesIndex)
					{
						int resolution = windWaves.FinalResolution;

						fftUtilitiesMaterial.SetFloat("_Intensity1", 0.58f * resolution / windWaves.TileSizes[scalesIndex * 2]);
						fftUtilitiesMaterial.SetFloat("_Intensity2", 0.58f * resolution / windWaves.TileSizes[scalesIndex * 2 + 1]);
						fftUtilitiesMaterial.SetTexture("_MainTex", displacementMaps[scalesIndex * 2]);
						fftUtilitiesMaterial.SetTexture("_SecondTex", displacementMaps[scalesIndex * 2 + 1]);
						Graphics.Blit(null, slopeMaps[scalesIndex], fftUtilitiesMaterial, 0);
					}
				}
				else
				{
					TemporaryRenderTexture packedSlopeMaps = doubleTargetCache.GetTemporary();
					slopeFFT.ComputeFFT(slopeSpectrum, packedSlopeMaps);

					for(int scalesIndex = 0; scalesIndex < 2; ++scalesIndex)
					{
						fftUtilitiesMaterial.SetVector("_Offset", offsetsDual[scalesIndex]);
						Graphics.Blit(packedSlopeMaps, slopeMaps[scalesIndex], fftUtilitiesMaterial, 3);
					}

					packedSlopeMaps.Dispose();
				}
			}
		}

		private void ClearDisplacedHeightMaps()
		{
#if UNITY_IOS || UNITY_ANDROID || UNITY_WP8 || UNITY_WP8_1
			// clear only on mobile gpus (improves performance)
			Graphics.SetRenderTarget(displacedHeightMap);
			GL.Clear(false, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));
#endif
		}

		private void RenderDisplacedHeightMaps(RenderTexture displacementMap, RenderTexture target, int channel)
		{
			int resolution = displacementMap.width;
			int displacedHeightMapResolution = flatten ? resolution : (resolution >> 2);
			int numVertices = displacedHeightMapResolution * displacedHeightMapResolution;

			var meshes = water.Geometry.GetMeshes(WaterGeometryType.UniformGrid, numVertices, false);
			fftUtilitiesMaterial.SetFloat("_ColorMask", 8 >> channel);
			fftUtilitiesMaterial.SetFloat("_WorldToPixelSpace", 2.0f / windWaves.TileSizes[channel]);
			fftUtilitiesMaterial.SetTexture("_MainTex", displacementMap);

			Graphics.SetRenderTarget(target);

			if(fftUtilitiesMaterial.SetPass(5))
			{
				foreach(var mesh in meshes)
					Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
			}
		}

		private void RenderSpectra(out Texture heightSpectrum, out Texture slopeSpectrum, out Texture displacementSpectrum)
		{
			float time = water.Time;

			if(renderedMaps == MapType.Slope)
			{
				heightSpectrum = null;
				displacementSpectrum = null;
				slopeSpectrum = windWaves.SpectrumResolver.RenderSlopeSpectrumAt(time);
			}
			else if((renderedMaps & MapType.Slope) == 0 || !finalHighQualitySlopeMaps)
			{
				slopeSpectrum = null;
				windWaves.SpectrumResolver.RenderDisplacementsSpectraAt(time, out heightSpectrum, out displacementSpectrum);
			}
			else
				windWaves.SpectrumResolver.RenderCompleteSpectraAt(time, out heightSpectrum, out slopeSpectrum, out displacementSpectrum);
		}

		public enum SpectrumType
		{
			Phillips,
			Unified
		}

		[System.Flags]
		public enum MapType
		{
			Displacement = 1,
			Slope = 2
		}

		public enum FlattenMode
		{
			Auto,
			ForcedOn,
			ForcedOff
		}
	}
}
