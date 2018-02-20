using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace PlayWay.Water
{
	/// <summary>
	/// Resolves spectrum data in the context of a specific water object.
	/// </summary>
	public class WaterWavesSpectrumData
	{
		readonly private Water water;
		readonly private WindWaves windWaves;
		readonly private WaterWavesSpectrum spectrum;

		// 1. mip level 2. scale index 3. spectrum data (finally)
		private Vector3[][][,] spectrumValues;

		// 1. scale index 2. mip level 3. cpu waves
		private WaterWave[][][] cpuWaves;

		// shoreline waves
		private WaterWave[] shorelineCandidates;

		private Texture2D texture;
		private float weight;
		private bool cpuWavesDirty = true;
		private float totalAmplitude;
		private Vector2 lastWindDirection;
		private int displayResolutionIndex;

		public WaterWavesSpectrumData(Water water, WaterWavesSpectrum spectrum)
		{
			this.water = water;
			this.windWaves = water.GetComponent<WindWaves>();
            this.spectrum = spectrum;
		}

		public Texture2D Texture
		{
			get
			{
				if(texture == null)
					CreateSpectrumTexture();

				return texture;
			}
		}

		public int CpuWavesCount
		{
			get
			{
				int sum = 0;

				if(cpuWaves != null)
				{
					foreach(var cpuWavesLevel in cpuWaves)
					{
						foreach(var mipLevel in cpuWavesLevel)
							sum += mipLevel.Length;
					}
				}
				
				return sum;
			}
		}

		public float TotalAmplitude
		{
			get { return totalAmplitude; }
		}

		public float Weight
		{
			get { return weight; }
			set { weight = value; }
		}

		public WaterWave[] ShorelineCandidates
		{
			get { return shorelineCandidates; }
		}

		public WaterWave[][] GetCpuWaves(int scaleIndex)
		{
			return cpuWaves[scaleIndex];
		}

		public void ValidateSpectrumData()
		{
			if(cpuWaves != null)
				return;

			lock (this)
			{
				if(cpuWaves != null)
					return;

				int resolution = windWaves.FinalResolution;
				Vector4 tileSizeScales = windWaves.TileSizeScales;
				Vector3[][,] spectrumValues;

				displayResolutionIndex = Mathf.RoundToInt(Mathf.Log(resolution, 2)) - 4;
				
				if(this.spectrumValues == null)
					this.spectrumValues = new Vector3[displayResolutionIndex + 1][][,];

				if(this.spectrumValues.Length <= displayResolutionIndex)
					System.Array.Resize(ref this.spectrumValues, displayResolutionIndex + 1);

				if(this.spectrumValues[displayResolutionIndex] == null)
				{
					this.spectrumValues[displayResolutionIndex] = spectrumValues = new Vector3[4][,];

					for(int i = 0; i < 4; ++i)
						spectrumValues[i] = new Vector3[resolution, resolution];
				}
				else
					spectrumValues = this.spectrumValues[displayResolutionIndex];
				
				int seed = water.Seed != 0 ? water.Seed : Random.Range(0, 1000000);

				totalAmplitude = 0.0f;

				var qualityLevels = WaterQualitySettings.Instance.GetQualityLevelsDirect();
				int maxResolution = qualityLevels[qualityLevels.Length - 1].maxSpectrumResolution;

				if(resolution > maxResolution)
					Debug.LogWarningFormat("In water quality settings spectrum resolution of {0} is max, but at runtime a spectrum with resolution of {1} is generated. That may generate some unexpected behaviour. Make sure that the last water quality level has the highest resolution and don't alter it at runtime.", maxResolution, resolution);

				for(byte scaleIndex = 0; scaleIndex < 4; ++scaleIndex)
				{
					Random.seed = seed + scaleIndex;
                    spectrum.ComputeSpectrum(spectrumValues[scaleIndex], tileSizeScales[scaleIndex], maxResolution, null);

					// debug spectrum
					//ResetSpectrum(spectrumValues[scaleIndex]);
					//if(scaleIndex == 2)
					//{
					//	//spectrumData[scaleIndex][0, 240] = new Vector3(1.0f, 0.0f, 1.0f);
					//	spectrumValues[scaleIndex][6, 2] = new Vector3(-3.92f, 0.0f, 1.0f);
					//	//spectrumData[scaleIndex][2, 12] = new Vector3(3.0f, 0.0f, 1.0f);
					//}
				}

				FindCpuWaves();
			}
		}

		private void FindCpuWaves()
		{
			if(cpuWaves == null)
				cpuWaves = new WaterWave[4][][];

			for(int i=0; i<4; ++i)
			{
				if(cpuWaves[i] == null)
					cpuWaves[i] = new WaterWave[displayResolutionIndex + 1][];
			}

			int resolution = windWaves.FinalResolution;
			int halfResolution = resolution >> 1;
			int cpuMaxWaves = windWaves.CpuMaxWaves;
			float cpuWaveThreshold = windWaves.CpuWaveThreshold;
			Vector4 tileSizeScales = windWaves.TileSizeScales;
			var shorelineCandidatesHeap = new Heap<WaterWave>();
			var importantWaves = new List<WaterWave>[displayResolutionIndex + 1];

			for(int i = 0; i <= displayResolutionIndex; ++i)
				importantWaves[i] = new List<WaterWave>();

			for(byte scaleIndex = 0; scaleIndex < 4; ++scaleIndex)
			{
				float tileSize = spectrum.TileSize * tileSizeScales[scaleIndex];
				Vector3[,] localValues = spectrumValues[displayResolutionIndex][scaleIndex];
				float frequencyScale = 2.0f * Mathf.PI / tileSize;
				float gravity = spectrum.Gravity;
				float offsetX = tileSize + (0.5f / resolution) * tileSize;
				float offsetZ = -tileSize + (0.5f / resolution) * tileSize;

				for(int x = 0; x < resolution; ++x)
				{
					float kx = frequencyScale * (x - halfResolution);
					ushort u = (ushort)((x + halfResolution) % resolution);

					for(int y = 0; y < resolution; ++y)
					{
						float ky = frequencyScale * (y - halfResolution);
						ushort v = (ushort)((y + halfResolution) % resolution);

						Vector3 s = localValues[u, v];
						float amplitude = Mathf.Sqrt(s.x * s.x + s.y * s.y);
						float k = Mathf.Sqrt(kx * kx + ky * ky);
						float w = Mathf.Sqrt(gravity * k);
						float cpuPriority = amplitude;

						if(cpuPriority < 0)
							cpuPriority = -cpuPriority;

						totalAmplitude += amplitude;

						if(amplitude >= cpuWaveThreshold)
						{
							int mipIndex = GetMipIndex(Mathf.Max(Mathf.Min(u, resolution - u - 1), Mathf.Min(v, resolution - v - 1)));
                            importantWaves[mipIndex].Add(new WaterWave(scaleIndex, offsetX, offsetZ, u, v, kx, ky, k, w, amplitude, cpuPriority));
						}

						if(amplitude > 0.025f)
						{
							float shorelinePriority = k / amplitude;            // it's used in a max-heap, so this is an inverse of a real priority
							shorelineCandidatesHeap.Insert(new WaterWave(scaleIndex, offsetX, offsetZ, u, v, kx, ky, k, w, amplitude, shorelinePriority));

							if(shorelineCandidatesHeap.Count > 200)
								shorelineCandidatesHeap.ExtractMax();
						}
                    }
				}

				lock (cpuWaves)
				{
					for(int mipIndex = 0; mipIndex <= displayResolutionIndex; ++mipIndex)
					{
						cpuWaves[scaleIndex][mipIndex] = importantWaves[mipIndex].ToArray();

						importantWaves[mipIndex].Clear();
						SortCpuWaves(cpuWaves[scaleIndex][mipIndex], false);

						if(cpuWaves[scaleIndex][mipIndex].Length > windWaves.CpuMaxWaves)
							System.Array.Resize(ref cpuWaves[scaleIndex][mipIndex], cpuMaxWaves);
					}
				}
			}

			shorelineCandidates = shorelineCandidatesHeap.ToArray();
			System.Array.Sort(shorelineCandidates);
        }

		static public int GetMipIndex(int i)
		{
			if(i == 0) return 0;

			int mip = (int)Mathf.Log(i, 2) - 4;

			return mip >= 0 ? mip : 0;
        }

		public Vector3[][,] GetSpectrumValues(int resolution)
		{
			int resolutionIndex = Mathf.RoundToInt(Mathf.Log(resolution, 2)) - 4;
			var spectrumValues = this.spectrumValues[resolutionIndex];

			if(spectrumValues == null)
			{
				// if it is missing, create it from some higher res mip level
				this.spectrumValues[resolutionIndex] = spectrumValues = new Vector3[4][,];

				int higherResIndex;
				Vector3[][,] higherResSpectrumValues = null;

				for(higherResIndex = resolutionIndex + 1; higherResIndex < this.spectrumValues.Length; ++higherResIndex)
				{
					higherResSpectrumValues = this.spectrumValues[higherResIndex];

					if(higherResSpectrumValues != null)
						break;
                }

				int halfResolution = resolution / 2;
				int quarterStartIndex = (1 << (higherResIndex + 4)) - resolution;

				for(int scaleIndex = 0; scaleIndex < 4; ++scaleIndex)
				{
					var spectrumValuesTile = spectrumValues[scaleIndex] = new Vector3[resolution, resolution];
					var higherRes = higherResSpectrumValues[scaleIndex];

					for(int y = 0; y < halfResolution; ++y)
					{
						for(int x = 0; x < halfResolution; ++x)
							spectrumValuesTile[y, x] = higherRes[y, x];

						for(int x = halfResolution; x < resolution; ++x)
							spectrumValuesTile[y, x] = higherRes[y, quarterStartIndex + x];
					}

					for(int y = halfResolution; y < resolution; ++y)
					{
						for(int x = 0; x < halfResolution; ++x)
							spectrumValuesTile[y, x] = higherRes[quarterStartIndex + y, x];

						for(int x = halfResolution; x < resolution; ++x)
							spectrumValuesTile[y, x] = higherRes[quarterStartIndex + y, quarterStartIndex + x];
					}
				}
			}
			
			return spectrumValues;
		}

		public void SetCpuWavesDirty()
		{
			cpuWavesDirty = true;
		}

		public void UpdateSpectralValues(Vector2 windDirection, float directionality)
		{
			ValidateSpectrumData();
			
			if(cpuWavesDirty)
			{
				lock (this)
				{
					if(cpuWaves == null || !cpuWavesDirty) return;

					lock (cpuWaves)
					{
						cpuWavesDirty = false;

						float directionalityInv = 1.0f - directionality;
						float horizontalDisplacementScale = water.HorizontalDisplacementScale;
						int resolution = windWaves.FinalResolution;
						bool mostlySorted = Vector2.Dot(lastWindDirection, windDirection) >= 0.97f;

						for(int tileIndex = 0; tileIndex < 4; ++tileIndex)
						{
							var cpuWavesLocal1 = cpuWaves[tileIndex];

							for(int mipIndex = 0; mipIndex <= displayResolutionIndex; ++mipIndex)
							{
								var cpuWavesLocal = cpuWavesLocal1[mipIndex];
								int numCpuWaves = cpuWavesLocal.Length;

								for(int i = 0; i < numCpuWaves; ++i)
									cpuWavesLocal[i].UpdateSpectralValues(spectrumValues[displayResolutionIndex], windDirection, directionalityInv, resolution, horizontalDisplacementScale);

								SortCpuWaves(cpuWavesLocal, mostlySorted);
							}
						}

						for(int i = 0; i < shorelineCandidates.Length; ++i)
							shorelineCandidates[i].UpdateSpectralValues(spectrumValues[displayResolutionIndex], windDirection, directionalityInv, resolution, horizontalDisplacementScale);

						lastWindDirection = windDirection;
					}
				}

			}
		}

		public void SortCpuWaves(WaterWave[] windWaves, bool mostlySorted)
		{
			if(!mostlySorted)
			{
				System.Array.Sort(windWaves, (a, b) =>
				{
					if(a.cpuPriority > b.cpuPriority)
						return -1;
					else
						return a.cpuPriority == b.cpuPriority ? 0 : 1;
				});
			}
			else
			{
				// bubble sort
				int numCpuWaves = windWaves.Length;
				int prevIndex = 0;

				for(int index = 1; index < numCpuWaves; ++index)
				{
					if(windWaves[prevIndex].cpuPriority < windWaves[index].cpuPriority)
					{
						var t = windWaves[prevIndex];
						windWaves[prevIndex] = windWaves[index];
						windWaves[index] = t;

						if(index != 1) index -= 2;
					}

					prevIndex = index;
				}
			}
		}
		
		public void Dispose(bool onlyTexture)
		{
			if(texture != null)
			{
				texture.Destroy();
				texture = null;
			}

			if(!onlyTexture)
			{
				lock(this)
				{
					spectrumValues = null;
					cpuWaves = null;
					cpuWavesDirty = true;
				}
			}
		}

		private void ResetSpectrum(Vector3[,] values)
		{
			int resolution = values.GetLength(0);

			for(int x = 0; x < resolution; ++x)
			{
				for(int y = 0; y < resolution; ++y)
				{
					values[x, y] = new Vector3(0.0f, 0.0f, 0.0f);
				}
			}
		}

		private void CreateSpectrumTexture()
		{
			ValidateSpectrumData();

			int resolution = windWaves.FinalResolution;
			int halfResolution = resolution >> 1;

			// create texture
			texture = new Texture2D(resolution << 1, resolution << 1, TextureFormat.RGBAFloat, false, true);
			texture.hideFlags = HideFlags.DontSave;
			texture.filterMode = FilterMode.Point;
			texture.wrapMode = TextureWrapMode.Repeat;

			for(int scaleIndex = 0; scaleIndex < 4; ++scaleIndex)
			{
				Vector3[,] spectrumValues = this.spectrumValues[displayResolutionIndex][scaleIndex];
				int uOffset = (scaleIndex & 1) == 0 ? 0 : resolution;
				int vOffset = (scaleIndex & 2) == 0 ? 0 : resolution;

				// fill texture
				for(int x = 0; x < resolution; ++x)
				{
					int u = (x + halfResolution) % resolution;

					for(int y = 0; y < resolution; ++y)
					{
						int v = (y + halfResolution) % resolution;

						Vector3 s = spectrumValues[u, v];
						texture.SetPixel(uOffset + u, vOffset + v, new Color(s.x, s.y, s.z, 1.0f));
					}
				}
			}

			texture.Apply(false, true);
		}
	}
}
