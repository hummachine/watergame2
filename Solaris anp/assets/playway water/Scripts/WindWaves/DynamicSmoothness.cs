using UnityEngine;

namespace PlayWay.Water
{
	/// <summary>
	/// Precomputes spectra variance used by shader microfacet model. Currently works only on platforms with compute shaders. General SM 3.0 support will be added later.
	/// <seealso cref="WindWaves.DynamicSmoothness"/>
	/// </summary>
	[System.Serializable]
	public class DynamicSmoothness
	{
		[SerializeField]
		private ComputeShader varianceShader;

		[Tooltip("Incorporates tiny waves on the screen into Unity's shader micro-facet model. Makes water look realistic at all view distances. Recommended.\nUsed only on DX11.")]
		[SerializeField]
		private bool enabled = true;
		
		// variance
		private RenderTexture varianceTexture;
		private int lastResetIndex;
		private int currentIndex;
		private bool finished;
		private bool initialized;
		private bool supported = true;
		
		private WindWaves windWaves;
		
		public void Start(WindWaves windWaves)
		{
			this.windWaves = windWaves;
			this.supported = CheckSupport();

			OnCopyModeChanged();
			OnValidate(windWaves);
        }

		public bool Enabled
		{
			get { return enabled/* && supported*/; }
		}

		public Texture VarianceTexture
		{
			get { return varianceTexture; }
		}

		/// <summary>
		/// You need to set this in your script, when instantiating WindWaves manually as compute shaders need to be directly referenced in Unity.
		/// </summary>
		public ComputeShader ComputeShader
		{
			get { return varianceShader; }
			set { varianceShader = value; }
		}

		public void FreeResources()
		{
			if(varianceTexture != null)
			{
				varianceTexture.Destroy();
				varianceTexture = null;
			}
		}

		public void OnCopyModeChanged()
		{
			if(windWaves != null && windWaves.CopyFrom != null)
			{
				FreeResources();

				windWaves.CopyFrom.DynamicSmoothness.ValidateVarianceTextures(windWaves.CopyFrom);
				windWaves.GetComponent<Water>().WaterMaterial.SetTexture("_SlopeVariance", windWaves.CopyFrom.DynamicSmoothness.varianceTexture);
			}
		}

		public bool CheckSupport()
		{
			return SystemInfo.supportsComputeShaders && SystemInfo.supports3DTextures;
		}
		
		public void Update()
		{
			if(!enabled || !supported) return;

			if(!initialized) InitializeVariance();

			ValidateVarianceTextures(windWaves);

			if(!finished)
				RenderNextPixel();
		}

		private void InitializeVariance()
		{
			initialized = true;
			
			var water = windWaves.GetComponent<Water>();
			water.ProfilesChanged.AddListener(OnProfilesChanged);
			windWaves.WindDirectionChanged.AddListener(OnWindDirectionChanged);
		}

		private void ValidateVarianceTextures(WindWaves windWaves)
		{
			if(varianceTexture == null)
				varianceTexture = CreateVarianceTexture(RenderTextureFormat.RGHalf);

			if(!varianceTexture.IsCreated())
			{
				varianceTexture.Create();

				var water = windWaves.GetComponent<Water>();
				water.WaterMaterial.SetTexture("_SlopeVariance", varianceTexture);
				varianceShader.SetTexture(0, "_Variance", varianceTexture);

				lastResetIndex = 0;
				currentIndex = 0;
			}
		}

		private void RenderNextPixel()
		{
			varianceShader.SetInt("_FFTSize", windWaves.FinalResolution);
			varianceShader.SetInt("_FFTSizeHalf", windWaves.FinalResolution >> 1);
			varianceShader.SetFloat("_VariancesSize", varianceTexture.width);
			varianceShader.SetVector("_TileSizes", windWaves.TileSizes);
			varianceShader.SetVector("_Coordinates", new Vector4(currentIndex % 4, (currentIndex >> 2) % 4, currentIndex >> 4, 0));
			varianceShader.SetTexture(0, "_Spectrum", windWaves.SpectrumResolver.GetRawDirectionalSpectrum());
			varianceShader.Dispatch(0, 1, 1, 1);

			++currentIndex;

			if(currentIndex >= 64)
				currentIndex = 0;

			if(currentIndex == lastResetIndex)
				finished = true;
        }
		
		private void ResetComputations()
		{
			lastResetIndex = currentIndex;
			finished = false;
		}

		internal void OnValidate(WindWaves windWaves)
		{
#if UNITY_EDITOR
			if(varianceShader == null)
			{
				var guids = UnityEditor.AssetDatabase.FindAssets("\"Spectral Variances\" t:ComputeShader");

				if(guids.Length != 0)
				{
					string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
					varianceShader = (ComputeShader)UnityEditor.AssetDatabase.LoadAssetAtPath(path, typeof(ComputeShader));
					UnityEditor.EditorUtility.SetDirty(windWaves);
				}
			}
#endif
		}

		private RenderTexture CreateVarianceTexture(RenderTextureFormat format)
		{
			var variancesTexture = new RenderTexture(4, 4, 0, format, RenderTextureReadWrite.Linear);
			variancesTexture.hideFlags = HideFlags.DontSave;
			variancesTexture.volumeDepth = 4;
			variancesTexture.isVolume = true;
			variancesTexture.enableRandomWrite = true;
			variancesTexture.wrapMode = TextureWrapMode.Clamp;
			variancesTexture.filterMode = FilterMode.Bilinear;

			return variancesTexture;
		}
		
		private void OnProfilesChanged(Water water)
		{
			ResetComputations();
		}

		private void OnWindDirectionChanged(WindWaves windWaves)
		{
			ResetComputations();
		}
    }
}
