using System.Collections.Generic;
using UnityEngine;

#if WATER_SIMD
using vector4 = Mono.Simd.Vector4f;
#else
using vector4 = UnityEngine.Vector4;
#endif

namespace PlayWay.Water
{
	public class SpectrumResolverCPU
	{
		private Water water;
		private WindWaves windWaves;
		protected Dictionary<WaterWavesSpectrum, WaterWavesSpectrumData> spectraDataCache;
		private List<WaterWavesSpectrumData> spectraDataList;

		private List<CpuFFT> workers;
		private WaterTileSpectrum[] tileSpectra;
		private Vector2 surfaceOffset;
		private Vector2 windDirection;
		private int numTiles;
		private float lastFrameTime;
		private float uniformWaterScale;
		private WaterWave[] filteredCpuWaves;
		private int filteredCpuWavesCount;
		private int cachedSeed;
		private bool cpuWavesDirty;
		static private int[] fftComputationCosts;

		// statistical data
		private float totalAmplitude;
		private float maxVerticalDisplacement;
		private float maxHorizontalDisplacement;

		public SpectrumResolverCPU(WindWaves windWaves, int numScales)
		{
			this.water = windWaves.GetComponent<Water>();
			this.windWaves = windWaves;
			this.spectraDataCache = new Dictionary<WaterWavesSpectrum, WaterWavesSpectrumData>();
			this.spectraDataList = new List<WaterWavesSpectrumData>();
			this.filteredCpuWaves = new WaterWave[0];
			this.numTiles = numScales;
			this.cachedSeed = windWaves.GetComponent<Water>().Seed;

			if(fftComputationCosts == null)
				PrecomputeFFTCosts();

			CreateSpectraLevels();
		}

		public float TotalAmplitude
		{
			get { return totalAmplitude; }
		}

		public float MaxVerticalDisplacement
		{
			get { return maxVerticalDisplacement; }
		}

		public float MaxHorizontalDisplacement
		{
			get { return maxHorizontalDisplacement; }
		}

		public int AvgCpuWaves
		{
			get
			{
				int cpuWavesCount = 0;

				foreach(var spectrumData in spectraDataCache.Values)
					cpuWavesCount += spectrumData.CpuWavesCount;

				return Mathf.RoundToInt(((float)cpuWavesCount) / spectraDataCache.Count);
			}
		}

		public Vector2 WindDirection
		{
			get { return windDirection; }
		}

		public float LastFrameTime
		{
			get { return lastFrameTime; }
		}

		public WaterTileSpectrum GetTile(int index)
		{
			return tileSpectra[index];
		}

		internal void Update()
		{
			surfaceOffset = water.SurfaceOffset;
			lastFrameTime = water.Time;
			uniformWaterScale = water.UniformWaterScale;

			UpdateCachedSeed();

			int numSamples = water.ComputedSamplesCount;
			bool allowFFT = WaterProjectSettings.Instance.AllowCpuFFT;

			for(int scaleIndex = 0; scaleIndex < numTiles; ++scaleIndex)
			{
				int fftResolution = 16;
				int mipLevel = 0;

				while(true)
				{
					int numWaves = 0;

					for(int spectrumIndex = 0; spectrumIndex < spectraDataList.Count; ++spectrumIndex)
					{
						var spectrum = spectraDataList[spectrumIndex];
						spectrum.ValidateSpectrumData();

						var cpuWaves = spectrum.GetCpuWaves(scaleIndex)[mipLevel];

						if(cpuWaves != null)
							numWaves += (int)(cpuWaves.Length * spectrum.Weight);
					}

					if(numWaves * numSamples < fftComputationCosts[mipLevel] + numSamples)
					{
						fftResolution >>= 1;
						break;
					}

					if(fftResolution >= windWaves.FinalResolution)
						break;

					fftResolution <<= 1;
					++mipLevel;
				}

				fftResolution <<= windWaves.CpuFFTPrecisionBoost;

				if(fftResolution > windWaves.FinalResolution)
					fftResolution = windWaves.FinalResolution;

				var level = tileSpectra[scaleIndex];

				if(level.SetResolveMode(fftResolution >= 16 && allowFFT, fftResolution))
					cpuWavesDirty = true;
			}

#if WATER_DEBUG
			DebugUpdate();
#endif
		}

		internal void SetWindDirection(Vector2 windDirection)
		{
			this.windDirection = windDirection;
			InvalidateDirectionalSpectrum();
		}

		public void DisposeCachedSpectra()
		{
			var kv = spectraDataCache.GetEnumerator();

			while(kv.MoveNext())
				kv.Current.Value.Dispose(false);
		}

		public WaterWavesSpectrumData GetSpectrumData(WaterWavesSpectrum spectrum)
		{
			WaterWavesSpectrumData spectrumData;

			if(!spectraDataCache.TryGetValue(spectrum, out spectrumData))
			{
				lock (spectraDataCache)
				{
					spectraDataCache[spectrum] = spectrumData = new WaterWavesSpectrumData(water, spectrum);
				}

				spectrumData.ValidateSpectrumData();
				cpuWavesDirty = true;

				lock (spectraDataList)
				{
					spectraDataList.Add(spectrumData);
				}
			}

			return spectrumData;
		}

		public void CacheSpectrum(WaterWavesSpectrum spectrum)
		{
			GetSpectrumData(spectrum);
		}

		public Dictionary<WaterWavesSpectrum, WaterWavesSpectrumData> GetCachedSpectraDirect()
		{
			return spectraDataCache;
		}

		#region WavemapsSampling
		private void InterpolationParams(float x, float z, int scaleIndex, float tileSize, out float fx, out float invFx, out float fy, out float invFy, out int index0, out int index1, out int index2, out int index3)
		{
			int resolution = tileSpectra[scaleIndex].ResolutionFFT;
			int displayResolution = windWaves.FinalResolution;
			x += (0.5f / displayResolution) * tileSize;
			z += (0.5f / displayResolution) * tileSize;

			float multiplier = resolution / tileSize;
			fx = x * multiplier;
			fy = z * multiplier;
			int indexX = (int)fx; if(indexX > fx) --indexX;     // inlined FastMath.FloorToInt(fx);
			int indexY = (int)fy; if(indexY > fy) --indexY;     // inlined FastMath.FloorToInt(fy);
			fx -= indexX;
			fy -= indexY;

			indexX = indexX % resolution;
			indexY = indexY % resolution;

			if(indexX < 0) indexX += resolution;
			if(indexY < 0) indexY += resolution;

			indexX = resolution - indexX - 1;
			indexY = resolution - indexY - 1;

			int indexX_2 = indexX + 1;
			int indexY_2 = indexY + 1;

			if(indexX_2 == resolution) indexX_2 = 0;
			if(indexY_2 == resolution) indexY_2 = 0;

			indexY *= resolution;
			indexY_2 *= resolution;

			index0 = indexY + indexX;
			index1 = indexY + indexX_2;
			index2 = indexY_2 + indexX;
			index3 = indexY_2 + indexX_2;

			invFx = 1.0f - fx;
			invFy = 1.0f - fy;
		}

		public Vector3 GetDisplacementAt(float x, float z, float spectrumStart, float spectrumEnd, float time)
		{
			Vector3 result = new Vector3();
			x = -(x + surfaceOffset.x);
			z = -(z + surfaceOffset.y);

			// sample FFT results
			if(spectrumStart == 0.0f)
			{
				for(int scaleIndex = numTiles - 1; scaleIndex >= 0; --scaleIndex)
				{
					if(tileSpectra[scaleIndex].resolveByFFT)
					{
						float fx, invFx, fy, invFy, t; int index0, index1, index2, index3;
						Vector2[] da, db; vector4[] fa, fb;

						lock (tileSpectra[scaleIndex])
						{
							InterpolationParams(x, z, scaleIndex, windWaves.TileSizes[scaleIndex], out fx, out invFx, out fy, out invFy, out index0, out index1, out index2, out index3);
							tileSpectra[scaleIndex].GetResults(time, out da, out db, out fa, out fb, out t);
						}

						Vector2 subResult = FastMath.Interpolate(
							ref da[index0], ref da[index1], ref da[index2], ref da[index3],
							ref db[index0], ref db[index1], ref db[index2], ref db[index3],
							fx, invFx, fy, invFy, t
						);

						result.x -= subResult.x;
						result.z -= subResult.y;

#if WATER_SIMD
						result.y += FastMath.Interpolate(
							fa[index0].W, fa[index1].W, fa[index2].W, fa[index3].W,
							fb[index0].W, fb[index1].W, fb[index2].W, fb[index3].W,
							fx, invFx, fy, invFy, t
						);
#else
						result.y += FastMath.Interpolate(
							fa[index0].w, fa[index1].w, fa[index2].w, fa[index3].w,
							fb[index0].w, fb[index1].w, fb[index2].w, fb[index3].w,
							fx, invFx, fy, invFy, t
						);
#endif
					}
				}
			}

			// sample waves directly
			if(filteredCpuWavesCount != 0)
			{
				SampleWavesDirectly(spectrumStart, spectrumEnd, (cpuWaves, startIndex, endIndex) =>
				{
					Vector3 subResult = new Vector3();

					for(int i = startIndex; i < endIndex; ++i)
						subResult += cpuWaves[i].GetDisplacementAt(x, z, time);

					result += subResult;
				});
			}

			float horizontalScale = -water.HorizontalDisplacementScale * uniformWaterScale;
			result.x = result.x * horizontalScale;
			result.y *= uniformWaterScale;
			result.z = result.z * horizontalScale;

			return result;
		}

		public Vector2 GetHorizontalDisplacementAt(float x, float z, float spectrumStart, float spectrumEnd, float time)
		{
			Vector2 result = new Vector2();
			x = -(x + surfaceOffset.x);
			z = -(z + surfaceOffset.y);

			// sample FFT results
			if(spectrumStart == 0.0f)
			{
				for(int scaleIndex = numTiles - 1; scaleIndex >= 0; --scaleIndex)
				{
					if(tileSpectra[scaleIndex].resolveByFFT)
					{
						float fx, invFx, fy, invFy, t; int index0, index1, index2, index3;
						Vector2[] da, db; vector4[] fa, fb;

						lock (tileSpectra[scaleIndex])
						{
							InterpolationParams(x, z, scaleIndex, windWaves.TileSizes[scaleIndex], out fx, out invFx, out fy, out invFy, out index0, out index1, out index2, out index3);
							tileSpectra[scaleIndex].GetResults(time, out da, out db, out fa, out fb, out t);
						}

						result -= FastMath.Interpolate(
							ref da[index0], ref da[index1], ref da[index2], ref da[index3],
							ref db[index0], ref db[index1], ref db[index2], ref db[index3],
							fx, invFx, fy, invFy, t
						);
					}
				}
			}

			// sample waves directly
			if(filteredCpuWavesCount != 0)
			{
				SampleWavesDirectly(spectrumStart, spectrumEnd, (cpuWaves, startIndex, endIndex) =>
				{
					Vector2 subResult = new Vector2();

					for(int i = startIndex; i < endIndex; ++i)
						subResult += cpuWaves[i].GetRawHorizontalDisplacementAt(x, z, time);

					result += subResult;
				});
			}

			float horizontalScale = -water.HorizontalDisplacementScale * uniformWaterScale;
			result.x = result.x * horizontalScale;
			result.y = result.y * horizontalScale;

			return result;
		}

		public Vector4 GetForceAndHeightAt(float x, float z, float spectrumStart, float spectrumEnd, float time)
		{
			vector4 result = new vector4();
			x = -(x + surfaceOffset.x);
			z = -(z + surfaceOffset.y);

			// sample FFT results
			if(spectrumStart == 0.0f)
			{
				for(int scaleIndex = numTiles - 1; scaleIndex >= 0; --scaleIndex)
				{
					if(tileSpectra[scaleIndex].resolveByFFT)
					{
						float fx, invFx, fy, invFy, t; int index0, index1, index2, index3;
						Vector2[] da, db; vector4[] fa, fb;

						lock (tileSpectra[scaleIndex])
						{
							InterpolationParams(x, z, scaleIndex, windWaves.TileSizes[scaleIndex], out fx, out invFx, out fy, out invFy, out index0, out index1, out index2, out index3);
							tileSpectra[scaleIndex].GetResults(time, out da, out db, out fa, out fb, out t);
						}

						result += FastMath.Interpolate(
							fa[index0], fa[index1], fa[index2], fa[index3],
							fb[index0], fb[index1], fb[index2], fb[index3],
							fx, invFx, fy, invFy, t
						);
					}
				}
			}

			// sample waves directly
			if(filteredCpuWavesCount != 0)
			{
				SampleWavesDirectly(spectrumStart, spectrumEnd, (cpuWaves, startIndex, endIndex) =>
				{
					Vector4 subResult = new Vector4();

					for(int i = startIndex; i < endIndex; ++i)
						cpuWaves[i].GetForceAndHeightAt(x, z, time, ref subResult);

#if WATER_SIMD
					result += new vector4(subResult.x, subResult.y, subResult.z, subResult.w);
#else
					result += subResult;
#endif
				});
			}

			float horizontalScale = water.HorizontalDisplacementScale * uniformWaterScale;

#if WATER_SIMD
			return new Vector4(result.X * horizontalScale, result.Y * 0.5f, result.Z * horizontalScale, result.W);
#else
			result.x = result.x * horizontalScale;
			result.z = result.z * horizontalScale;
			result.y *= 0.5f * uniformWaterScale;
			result.w *= uniformWaterScale;              // not 100% sure about this

			return result;
#endif
		}

		public float GetHeightAt(float x, float z, float spectrumStart, float spectrumEnd, float time)
		{
			float result = 0.0f;
			x = -(x + surfaceOffset.x);
			z = -(z + surfaceOffset.y);

			// sample FFT results
			if(spectrumStart == 0.0f)
			{
				for(int scaleIndex = numTiles - 1; scaleIndex >= 0; --scaleIndex)
				{
					if(tileSpectra[scaleIndex].resolveByFFT)
					{
						float fx, invFx, fy, invFy, t; int index0, index1, index2, index3;
						Vector2[] da, db; vector4[] fa, fb;

						lock (tileSpectra[scaleIndex])
						{
							InterpolationParams(x, z, scaleIndex, windWaves.TileSizes[scaleIndex], out fx, out invFx, out fy, out invFy, out index0, out index1, out index2, out index3);
							tileSpectra[scaleIndex].GetResults(time, out da, out db, out fa, out fb, out t);
						}

#if WATER_SIMD
						result += FastMath.Interpolate(
							fa[index0].W, fa[index1].W, fa[index2].W, fa[index3].W,
							fb[index0].W, fb[index1].W, fb[index2].W, fb[index3].W,
							fx, invFx, fy, invFy, t
						);
#else
						result += FastMath.Interpolate(
							fa[index0].w, fa[index1].w, fa[index2].w, fa[index3].w,
							fb[index0].w, fb[index1].w, fb[index2].w, fb[index3].w,
							fx, invFx, fy, invFy, t
						);
#endif
					}
				}
			}

			// sample waves directly
			if(filteredCpuWavesCount != 0)
			{
				SampleWavesDirectly(spectrumStart, spectrumEnd, (cpuWaves, startIndex, endIndex) =>
				{
					float subResult = 0.0f;

					for(int i = startIndex; i < endIndex; ++i)
						subResult += cpuWaves[i].GetHeightAt(x, z, time);

					result += subResult;
				});
			}

			return result * uniformWaterScale;
		}

		private void SampleWavesDirectly(float spectrumStart, float spectrumEnd, System.Action<WaterWave[], int, int> func)
		{
			lock (this)
			{
				var waves = GetFilteredCpuWaves();
				int startIndex = (int)(spectrumStart * filteredCpuWavesCount);
				int endIndex = (int)(spectrumEnd * filteredCpuWavesCount);

				if(startIndex != endIndex)
					func(waves, startIndex, endIndex);
			}
		}
		#endregion

		#region WavesSelecting
		public WaterWave[] GetFilteredCpuWaves()
		{
			if(cpuWavesDirty)
			{
				int index = 0;

				foreach(var spectrum in spectraDataList)
				{
					spectrum.UpdateSpectralValues(windDirection, water.Directionality);

					float weight = spectrum.Weight;

					for(int tileIndex = 0; tileIndex < numTiles; ++tileIndex)
					{
						var cpuWavesForTile = spectrum.GetCpuWaves(tileIndex);
						int minMipIndex = tileSpectra[tileIndex].IsResolvedByFFT ? tileSpectra[tileIndex].MipIndexFFT + 1 : 0;          // all mip levels up to this one are covered by FFT, far better than direct waves sampling

						for(int mipIndex = minMipIndex; mipIndex < cpuWavesForTile.Length; ++mipIndex)
						{
							var cpuWaves = cpuWavesForTile[mipIndex];

							if(filteredCpuWaves.Length < index + cpuWaves.Length)
								System.Array.Resize(ref filteredCpuWaves, index + cpuWaves.Length + 120);           // reserve a bit more than necessary

							for(int i = 0; i < cpuWaves.Length; ++i)
							{
								filteredCpuWaves[index] = cpuWaves[i];
								filteredCpuWaves[index++].amplitude *= weight;
							}
						}
					}
				}

				filteredCpuWavesCount = index;
				cpuWavesDirty = false;
			}

			return filteredCpuWaves;
		}

		public GerstnerWave[] SelectShorelineWaves(int count, float angle, float coincidenceRange)
		{
			var list = new List<FoundWave>();

			foreach(var spectrum in spectraDataList)
			{
				if(spectrum.Weight < 0.001f)
					continue;

				spectrum.UpdateSpectralValues(windDirection, water.Directionality);

				lock (this)
				{
					var shorelineCandidates = spectrum.ShorelineCandidates;
					int countToAdd = count;

					for(int i = 0; i < shorelineCandidates.Length && countToAdd != 0; ++i)
					{
						float waveAngle = Mathf.Atan2(shorelineCandidates[i].nkx, shorelineCandidates[i].nky) * Mathf.Rad2Deg;

						if(Mathf.Abs(Mathf.DeltaAngle(waveAngle, angle)) < coincidenceRange && shorelineCandidates[i].amplitude > 0.025f)
						{
							list.Add(new FoundWave(spectrum, shorelineCandidates[i]));
							--countToAdd;
						}
					}
				}
			}

			list.Sort((a, b) => b.importance.CompareTo(a.importance));

			// compute texture offsets from the FFT shader to match Gerstner waves to FFT
			Vector2[] offsets = new Vector2[4];

			for(int i = 0; i < 4; ++i)
			{
				float tileSize = windWaves.TileSizes[i];

				offsets[i].x = tileSize + (0.5f / windWaves.FinalResolution) * tileSize;
				offsets[i].y = -tileSize + (0.5f / windWaves.FinalResolution) * tileSize;
			}

			int c = Mathf.Min(list.Count, count);
			var gerstners = new GerstnerWave[c];

			for(int i = 0; i < c; ++i)
				gerstners[i] = list[list.Count - i - 1].ToGerstner(offsets);            // shoreline waves have a reversed order here...

			return gerstners;
		}

		public GerstnerWave[] FindMostMeaningfulWaves(int count, bool mask)
		{
			var list = new List<FoundWave>();

			foreach(var spectrum in spectraDataList)
			{
				if(spectrum.Weight < 0.001f)
					continue;

				spectrum.UpdateSpectralValues(windDirection, water.Directionality);

				lock (this)
				{
					var cpuWaves = GetFilteredCpuWaves();
					int numWaves = Mathf.Min(cpuWaves.Length, count);

					for(int i = 0; i < numWaves; ++i)
						list.Add(new FoundWave(spectrum, cpuWaves[i]));
				}
			}

			list.Sort((a, b) => b.importance.CompareTo(a.importance));

			// compute texture offsets from the FFT shader to match Gerstner waves to FFT
			Vector2[] offsets = new Vector2[4];

			for(int i = 0; i < 4; ++i)
			{
				float tileSize = windWaves.TileSizes[i];

				offsets[i].x = tileSize + (0.5f / windWaves.FinalResolution) * tileSize;
				offsets[i].y = -tileSize + (0.5f / windWaves.FinalResolution) * tileSize;
			}

			var gerstners = new GerstnerWave[count];

			for(int i = 0; i < count; ++i)
				gerstners[i] = list[i].ToGerstner(offsets);

			return gerstners;
		}

		public Gerstner4[] FindGerstners(int count, bool mask)
		{
			var list = new List<FoundWave>();

			foreach(var spectrum in spectraDataCache.Values)
			{
				if(spectrum.Weight < 0.001f)
					continue;

				spectrum.UpdateSpectralValues(windDirection, water.Directionality);

				lock (this)
				{
					var cpuWaves = GetFilteredCpuWaves();
					int numWaves = Mathf.Min(cpuWaves.Length, count);

					for(int i = 0; i < numWaves; ++i)
						list.Add(new FoundWave(spectrum, cpuWaves[i]));
				}
			}

			list.Sort((a, b) => b.importance.CompareTo(a.importance));

			int index = 0;
			int numFours = (count >> 2);
			var result = new Gerstner4[numFours];

			// compute texture offsets from the FFT shader to match Gerstner waves to FFT
			Vector2[] offsets = new Vector2[4];

			for(int i = 0; i < 4; ++i)
			{
				float tileSize = windWaves.TileSizes[i];

				offsets[i].x = tileSize + (0.5f / windWaves.FinalResolution) * tileSize;
				offsets[i].y = -tileSize + (0.5f / windWaves.FinalResolution) * tileSize;
			}

			for(int i = 0; i < numFours; ++i)
			{
				var wave0 = index < list.Count ? list[index++].ToGerstner(offsets) : new GerstnerWave();
				var wave1 = index < list.Count ? list[index++].ToGerstner(offsets) : new GerstnerWave();
				var wave2 = index < list.Count ? list[index++].ToGerstner(offsets) : new GerstnerWave();
				var wave3 = index < list.Count ? list[index++].ToGerstner(offsets) : new GerstnerWave();

				result[i] = new Gerstner4(wave0, wave1, wave2, wave3);
			}

			//if(mask)
			//	foundWave.spectrum.texture.SetPixel(wave.u, wave.v, new Color(0.0f, 0.0f, 0.0f, 0.0f));

			/*if(mask)
			{
				foreach(var spectrum in spectra)
					spectrum.texture.Apply(false, false);

				ComputeTotalSpectrum();
				directionalSpectrumDirty = true;
			}*/

			return result;
		}
		#endregion

		private void UpdateCachedSeed()
		{
			if(cachedSeed != water.Seed)
			{
				cachedSeed = water.Seed;
				DisposeCachedSpectra();
				OnProfilesChanged();
			}
		}

		virtual internal void OnProfilesChanged()
		{
			var profiles = water.Profiles;

			foreach(var spectrumData in spectraDataCache.Values)
				spectrumData.Weight = 0.0f;

			foreach(var weightedProfile in profiles)
			{
				if(weightedProfile.weight <= 0.0001f)
					continue;

				var spectrum = weightedProfile.profile.Spectrum;

				WaterWavesSpectrumData spectrumData;

				if(!spectraDataCache.TryGetValue(spectrum, out spectrumData))
					spectrumData = GetSpectrumData(spectrum);

				spectrumData.Weight = weightedProfile.weight;
			}

			totalAmplitude = 0.0f;

			foreach(var spectrumData in spectraDataCache.Values)
				totalAmplitude += spectrumData.TotalAmplitude * spectrumData.Weight;

			maxVerticalDisplacement = totalAmplitude * 0.06f;
			maxHorizontalDisplacement = maxVerticalDisplacement * water.HorizontalDisplacementScale;
			//maxHorizontalDisplacement = Mathf.Sqrt(FastMath.Pow2(maxVerticalDisplacement) + FastMath.Pow2(maxVerticalDisplacement * water.HorizontalDisplacementScale));

			InvalidateDirectionalSpectrum();
		}

		private void PrecomputeFFTCosts()
		{
			fftComputationCosts = new int[10];
			int resolution = 16;

			for(int i = 0; i < fftComputationCosts.Length; ++i)
			{
				int numButterflies = (int)(Mathf.Log((float)resolution) / Mathf.Log(2.0f));
				fftComputationCosts[i] = resolution * resolution * numButterflies;
				resolution <<= 1;
			}

			for(int i = fftComputationCosts.Length - 1; i >= 1; --i)
				fftComputationCosts[i] -= fftComputationCosts[i - 1];
		}

		private void CreateSpectraLevels()
		{
			this.tileSpectra = new WaterTileSpectrum[numTiles];

			for(int scaleIndex = 0; scaleIndex < numTiles; ++scaleIndex)
				tileSpectra[scaleIndex] = new WaterTileSpectrum(windWaves, scaleIndex);
		}

#if WATER_DEBUG
		private void DebugUpdate()
		{
			if(Input.GetKeyDown(KeyCode.F10))
			{
				float scale = 0.1f;

				for(int i = 0; i < 4; ++i)
				{
					if(!tileSpectra[i].IsResolvedByFFT) continue;

					int resolution = tileSpectra[i].ResolutionFFT;

					lock (tileSpectra[i])
					{
						var tex = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false, true);
						for(int y = 0; y < resolution; ++y)
						{
							for(int x = 0; x < resolution; ++x)
							{
								tex.SetPixel(x, y, new Color(tileSpectra[i].directionalSpectrum[y * resolution + x].x, tileSpectra[i].directionalSpectrum[y * resolution + x].y, 0.0f, 1.0f));
							}
						}

						tex.Apply();
						var bytes = tex.EncodeToPNG();
						System.IO.File.WriteAllBytes("CPU Dir " + i + ".png", bytes);

						tex.Destroy();
					}
				}

				for(int i = 0; i < 4; ++i)
				{
					if(!tileSpectra[i].IsResolvedByFFT) continue;

					int resolution = tileSpectra[i].ResolutionFFT;

					var tex = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false, true);
					for(int y = 0; y < resolution; ++y)
					{
						for(int x = 0; x < resolution; ++x)
						{
							tex.SetPixel(x, y, new Color(tileSpectra[i].displacements[1][y * resolution + x].x * water.HorizontalDisplacementScale * scale, tileSpectra[i].forceAndHeight[1][y * resolution + x][3] * scale, tileSpectra[i].displacements[1][y * resolution + x].y * water.HorizontalDisplacementScale * scale, 1.0f));
						}
					}

					tex.Apply();
					var bytes = tex.EncodeToPNG();
					System.IO.File.WriteAllBytes("CPU FFT " + i + ".png", bytes);

					tex.Destroy();
				}

				for(int i = 0; i < 4; ++i)
				{
					if(!tileSpectra[i].IsResolvedByFFT) continue;

					int resolution = tileSpectra[i].ResolutionFFT;

					var tex = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false, true);
					for(int y = 0; y < resolution; ++y)
					{
						for(int x = 0; x < resolution; ++x)
						{
							Vector2 displacement = GetHorizontalDisplacementAt((float)(x + 0.5f) / resolution * windWaves.TileSizes[i], (float)(y + 0.5f) / resolution * windWaves.TileSizes[i], 0.0f, 1.0f, Time.time);
							float height = GetHeightAt((float)(x + 0.5f) / resolution * windWaves.TileSizes[i], (float)(y + 0.5f) / resolution * windWaves.TileSizes[i], 0.0f, 1.0f, Time.time);
							tex.SetPixel(x, y, new Color(displacement.x * scale, height * scale, displacement.y * scale, 1.0f));
						}
					}

					tex.Apply();
					var bytes = tex.EncodeToPNG();
					System.IO.File.WriteAllBytes("CPU FFT Sampled " + i + ".png", bytes);

					tex.Destroy();
				}
			}
		}
#endif

		virtual protected void InvalidateDirectionalSpectrum()
		{
			cpuWavesDirty = true;

			foreach(var spectrum in spectraDataList)
				spectrum.SetCpuWavesDirty();

			for(int scaleIndex = 0; scaleIndex < numTiles; ++scaleIndex)
				tileSpectra[scaleIndex].directionalSpectrumDirty = 2;
		}

		virtual internal void OnMapsFormatChanged(bool resolution)
		{
			if(spectraDataCache != null)
			{
				foreach(var spectrumData in spectraDataCache.Values)
					spectrumData.Dispose(!resolution);
			}

			InvalidateDirectionalSpectrum();
		}

		virtual internal void OnDestroy()
		{
			OnMapsFormatChanged(true);
			spectraDataCache = null;

			lock (spectraDataList)
			{
				spectraDataList.Clear();
			}
		}

		private class FoundWave
		{
			public WaterWavesSpectrumData spectrum;
			public WaterWave wave;
			public float importance;

			public FoundWave(WaterWavesSpectrumData spectrum, WaterWave wave)
			{
				this.spectrum = spectrum;
				this.wave = wave;

				importance = wave.cpuPriority * spectrum.Weight;
			}

			public GerstnerWave ToGerstner(Vector2[] scaleOffsets)
			{
				float speed = wave.w;
				float mapOffset = (scaleOffsets[wave.scaleIndex].x * wave.nkx + scaleOffsets[wave.scaleIndex].y * wave.nky) * wave.k;       // match Gerstner to FFT map equivalent

				return new GerstnerWave(new Vector2(wave.nkx, wave.nky), wave.amplitude * spectrum.Weight, mapOffset + wave.offset, wave.k, speed);
			}
		}
	}
}
