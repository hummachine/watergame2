using System.Collections.Generic;
using UnityEngine;
using System.Threading;

namespace PlayWay.Water
{
	/// <summary>
	/// Renders all types of water spectra and animates them in time on CPU and GPU. This class in hierarchy contains GPU code.
	/// </summary>
	public class SpectrumResolver : SpectrumResolverCPU
	{
		private Texture2D tileSizeLookup;           // 2x2 tile sizes tex
		private Texture omnidirectionalSpectrum;
		private RenderTexture totalOmnidirectionalSpectrum;
		private RenderTexture directionalSpectrum;
		private RenderTexture heightSpectrum, slopeSpectrum, displacementSpectrum;
		private RenderBuffer[] renderTargetsx2;
		private RenderBuffer[] renderTargetsx3;

		private float renderTime;
		private int renderTimeId;
		private int resolution;
		private bool tileSizesLookupDirty = true;
		private bool directionalSpectrumDirty = true;
		private Vector4 tileSizes;
		private Material animationMaterial;

		private Water water;
		private WindWaves windWaves;

		public SpectrumResolver(WindWaves windWaves, Shader spectrumShader) : base(windWaves, 4)
		{
			this.water = windWaves.GetComponent<Water>();
			this.windWaves = windWaves;

			renderTimeId = Shader.PropertyToID("_RenderTime");

			animationMaterial = new Material(spectrumShader);
			animationMaterial.hideFlags = HideFlags.DontSave;
			animationMaterial.SetFloat(renderTimeId, Time.time);
		}

		public Texture TileSizeLookup
		{
			get
			{
				ValidateTileSizeLookup();

				return tileSizeLookup;
			}
		}
		
		public float RenderTime
		{
			get { return renderTime; }
		}
		
		public Texture RenderHeightSpectrumAt(float time)
		{
			CheckResources();

			var directionalSpectrum = GetRawDirectionalSpectrum();

			renderTime = time;
			animationMaterial.SetFloat(renderTimeId, time);
			Graphics.Blit(directionalSpectrum, heightSpectrum, animationMaterial, 0);

			return heightSpectrum;
		}

		public Texture RenderSlopeSpectrumAt(float time)
		{
			CheckResources();

			var directionalSpectrum = GetRawDirectionalSpectrum();

			renderTime = time;
			animationMaterial.SetFloat(renderTimeId, time);
			Graphics.Blit(directionalSpectrum, slopeSpectrum, animationMaterial, 1);

			return slopeSpectrum;
		}

		public void RenderDisplacementsSpectraAt(float time, out Texture height, out Texture displacement)
		{
			CheckResources();

			height = heightSpectrum;
			displacement = displacementSpectrum;

			// it's necessary to set it each frame for some reason
			renderTargetsx2[0] = heightSpectrum.colorBuffer;
			renderTargetsx2[1] = displacementSpectrum.colorBuffer;

			var directionalSpectrum = GetRawDirectionalSpectrum();

			renderTime = time;
			animationMaterial.SetFloat(renderTimeId, time);
			Graphics.SetRenderTarget(renderTargetsx2, heightSpectrum.depthBuffer);
			Graphics.Blit(directionalSpectrum, animationMaterial, 5);
			Graphics.SetRenderTarget(null);
		}

		public void RenderCompleteSpectraAt(float time, out Texture height, out Texture slope, out Texture displacement)
		{
			CheckResources();

			height = heightSpectrum;
			slope = slopeSpectrum;
			displacement = displacementSpectrum;

			// it's necessary to set it each frame for some reason
			renderTargetsx3[0] = heightSpectrum.colorBuffer;
			renderTargetsx3[1] = slopeSpectrum.colorBuffer;
			renderTargetsx3[2] = displacementSpectrum.colorBuffer;

			var directionalSpectrum = GetRawDirectionalSpectrum();

			renderTime = time;
			animationMaterial.SetFloat(renderTimeId, time);
			Graphics.SetRenderTarget(renderTargetsx3, heightSpectrum.depthBuffer);
			Graphics.Blit(directionalSpectrum, animationMaterial, 2);
			Graphics.SetRenderTarget(null);
		}

		public Texture GetSpectrum(SpectrumType type)
		{
			switch(type)
			{
				case SpectrumType.Height: return heightSpectrum;
				case SpectrumType.Slope: return slopeSpectrum;
				case SpectrumType.Displacement: return displacementSpectrum;
				case SpectrumType.RawDirectional: return directionalSpectrum;
				case SpectrumType.RawOmnidirectional: return omnidirectionalSpectrum;
				default: throw new System.InvalidOperationException();
			}
		}

		override internal void OnProfilesChanged()
		{
			base.OnProfilesChanged();

			if(tileSizes != windWaves.TileSizes)
			{
				tileSizesLookupDirty = true;
				tileSizes = windWaves.TileSizes;
			}

			RenderTotalOmnidirectionalSpectrum();
		}

		private void RenderTotalOmnidirectionalSpectrum()
		{
			animationMaterial.SetFloat("_Gravity", water.Gravity);
			animationMaterial.SetVector("_TargetResolution", new Vector4(windWaves.FinalResolution, windWaves.FinalResolution, 0.0f, 0.0f));

			var profiles = water.Profiles;

			if(profiles.Length > 1)
			{
				var totalOmnidirectionalSpectrum = GetTotalOmnidirectionalSpectrum();

				Graphics.SetRenderTarget(totalOmnidirectionalSpectrum);
				GL.Clear(false, true, Color.black);
				Graphics.SetRenderTarget(null);

				foreach(var weightedProfile in profiles)
				{
					if(weightedProfile.weight <= 0.0001f)
						continue;

					var spectrum = weightedProfile.profile.Spectrum;

					WaterWavesSpectrumData spectrumData;

					if(!spectraDataCache.TryGetValue(spectrum, out spectrumData))
						spectrumData = GetSpectrumData(spectrum);

					animationMaterial.SetFloat("_Weight", spectrumData.Weight);
					Graphics.Blit(spectrumData.Texture, totalOmnidirectionalSpectrum, animationMaterial, 4);
				}

				omnidirectionalSpectrum = totalOmnidirectionalSpectrum;
			}
			else
			{
				var spectrum = profiles[0].profile.Spectrum;
				WaterWavesSpectrumData spectrumData;

				if(!spectraDataCache.TryGetValue(spectrum, out spectrumData))
					spectrumData = GetSpectrumData(spectrum);

				spectrumData.Weight = 1.0f;
				omnidirectionalSpectrum = spectrumData.Texture;
			}
			
			water.WaterMaterial.SetFloat("_MaxDisplacement", MaxHorizontalDisplacement);
		}

		protected override void InvalidateDirectionalSpectrum()
		{
			base.InvalidateDirectionalSpectrum();

			directionalSpectrumDirty = true;
		}

		private void RenderDirectionalSpectrum()
		{
			if(omnidirectionalSpectrum == null)
				RenderTotalOmnidirectionalSpectrum();

			ValidateTileSizeLookup();

			animationMaterial.SetFloat("_Directionality", 1.0f - water.Directionality);
			animationMaterial.SetVector("_WindDirection", WindDirection);
			animationMaterial.SetTexture("_TileSizeLookup", tileSizeLookup);
			Graphics.Blit(omnidirectionalSpectrum, directionalSpectrum, animationMaterial, 3);
			directionalSpectrumDirty = false;
		}

		internal RenderTexture GetRawDirectionalSpectrum()
		{
			if((directionalSpectrumDirty || !directionalSpectrum.IsCreated()) && Application.isPlaying)
			{
				CheckResources();
				RenderDirectionalSpectrum();
			}

			return directionalSpectrum;
		}

		private RenderTexture GetTotalOmnidirectionalSpectrum()
		{
			if(totalOmnidirectionalSpectrum == null)
			{
				int finalResolutionx2 = windWaves.FinalResolution << 1;

				totalOmnidirectionalSpectrum = new RenderTexture(finalResolutionx2, finalResolutionx2, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
				totalOmnidirectionalSpectrum.hideFlags = HideFlags.DontSave;
				totalOmnidirectionalSpectrum.filterMode = FilterMode.Point;
				totalOmnidirectionalSpectrum.wrapMode = TextureWrapMode.Repeat;
			}

			return totalOmnidirectionalSpectrum;
		}
		
		private void CheckResources()
		{
			if(heightSpectrum == null)          // these are always all null or non-null
			{
				int finalResolutionx2 = windWaves.FinalResolution << 1;
				bool highPrecision = windWaves.FinalHighPrecision;

				heightSpectrum = new RenderTexture(finalResolutionx2, finalResolutionx2, 0, highPrecision ? RenderTextureFormat.RGFloat : RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear);
				heightSpectrum.hideFlags = HideFlags.DontSave;
				heightSpectrum.filterMode = FilterMode.Point;

				slopeSpectrum = new RenderTexture(finalResolutionx2, finalResolutionx2, 0, highPrecision ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
				slopeSpectrum.hideFlags = HideFlags.DontSave;
				slopeSpectrum.filterMode = FilterMode.Point;

				displacementSpectrum = new RenderTexture(finalResolutionx2, finalResolutionx2, 0, highPrecision ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
				displacementSpectrum.hideFlags = HideFlags.DontSave;
				displacementSpectrum.filterMode = FilterMode.Point;

				directionalSpectrum = new RenderTexture(finalResolutionx2, finalResolutionx2, 0, highPrecision ? RenderTextureFormat.RGFloat : RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear);
				directionalSpectrum.hideFlags = HideFlags.DontSave;
				directionalSpectrum.filterMode = FilterMode.Point;
				directionalSpectrum.wrapMode = TextureWrapMode.Clamp;

				renderTargetsx2 = new RenderBuffer[] { heightSpectrum.colorBuffer, displacementSpectrum.colorBuffer };
				renderTargetsx3 = new RenderBuffer[] { heightSpectrum.colorBuffer, slopeSpectrum.colorBuffer, displacementSpectrum.colorBuffer };
			}
		}

		override internal void OnMapsFormatChanged(bool resolution)
		{
			base.OnMapsFormatChanged(resolution);

			if(totalOmnidirectionalSpectrum != null)
			{
				Object.Destroy(totalOmnidirectionalSpectrum);
				totalOmnidirectionalSpectrum = null;
			}

			if(heightSpectrum != null)
			{
				Object.Destroy(heightSpectrum);
				heightSpectrum = null;
			}

			if(slopeSpectrum != null)
			{
				Object.Destroy(slopeSpectrum);
				slopeSpectrum = null;
			}

			if(displacementSpectrum != null)
			{
				Object.Destroy(displacementSpectrum);
				displacementSpectrum = null;
			}

			if(directionalSpectrum != null)
			{
				Object.Destroy(directionalSpectrum);
				directionalSpectrum = null;
			}

			omnidirectionalSpectrum = null;
			renderTargetsx2 = null;
			renderTargetsx3 = null;
		}

		private void ValidateTileSizeLookup()
		{
			if(tileSizeLookup == null)
			{
				tileSizeLookup = new Texture2D(2, 2, SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat) ? TextureFormat.RGBAFloat : TextureFormat.RGBAHalf, false, true);
				tileSizeLookup.hideFlags = HideFlags.DontSave;
				tileSizeLookup.wrapMode = TextureWrapMode.Clamp;
				tileSizeLookup.filterMode = FilterMode.Point;
			}

			if(tileSizesLookupDirty)
			{
				tileSizeLookup.SetPixel(0, 0, new Color(0.5f, 0.5f, 1.0f / tileSizes.x, 0.0f));
				tileSizeLookup.SetPixel(1, 0, new Color(1.5f, 0.5f, 1.0f / tileSizes.y, 0.0f));
				tileSizeLookup.SetPixel(0, 1, new Color(0.5f, 1.5f, 1.0f / tileSizes.z, 0.0f));
				tileSizeLookup.SetPixel(1, 1, new Color(1.5f, 1.5f, 1.0f / tileSizes.w, 0.0f));
				tileSizeLookup.Apply(false, false);

				tileSizesLookupDirty = false;
			}
		}

		public enum SpectrumType
		{
			Height,
			Slope,
			Displacement,
			RawDirectional,
			RawOmnidirectional
		}
	}
}
