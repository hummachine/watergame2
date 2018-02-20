using System.Collections.Generic;
using UnityEngine;

#if WATER_SIMD
using Mono.Simd;
using vector4 = Mono.Simd.Vector4f;
#else
using vector4 = UnityEngine.Vector4;
#endif

namespace PlayWay.Water
{
	public class CpuFFT
	{
		private WaterTileSpectrum targetSpectrum;
		private float time;
		private int resolution;
		
		static private Dictionary<int, FFTBuffers> buffersCache = new Dictionary<int, FFTBuffers>();

		public void Compute(WaterTileSpectrum targetSpectrum, float time, int outputBufferIndex)
		{
			this.targetSpectrum = targetSpectrum;
			this.time = time;

			Vector2[] displacements; vector4[] forceAndHeight;
			Vector2[] directionalSpectrum;

			lock (targetSpectrum)
			{
				this.resolution = targetSpectrum.ResolutionFFT;

				directionalSpectrum = targetSpectrum.directionalSpectrum;
				displacements = targetSpectrum.displacements[outputBufferIndex];
				forceAndHeight = targetSpectrum.forceAndHeight[outputBufferIndex];
            }
			
			FFTBuffers buffers;

			if(!buffersCache.TryGetValue(resolution, out buffers))
				buffersCache[resolution] = buffers = new FFTBuffers(resolution);

			float tileSize = targetSpectrum.windWaves.UnscaledTileSizes[targetSpectrum.tileIndex];
			Vector3[] kMap = buffers.GetPrecomputedK(tileSize);

			if(targetSpectrum.directionalSpectrumDirty > 0)
			{
				ComputeDirectionalSpectra(targetSpectrum.tileIndex, directionalSpectrum, kMap);
				--targetSpectrum.directionalSpectrumDirty;
            }

			ComputeTimedSpectra(directionalSpectrum, buffers.timed, kMap);
			ComputeFFT(buffers.timed, displacements, forceAndHeight, buffers.indices, buffers.weights, buffers.pingPongA, buffers.pingPongB);
		}

		private void ComputeDirectionalSpectra(int scaleIndex, Vector2[] directional, Vector3[] kMap)
		{
			float directionality = 1.0f - targetSpectrum.water.Directionality;
			var cachedSpectra = targetSpectrum.windWaves.SpectrumResolver.GetCachedSpectraDirect();
			int resolutionSqr = resolution * resolution;

			Vector2 windDirection = targetSpectrum.windWaves.SpectrumResolver.WindDirection;

			for(int i = 0; i < resolutionSqr; ++i)
			{
				directional[i].x = 0.0f;
				directional[i].y = 0.0f;
			}

			lock(cachedSpectra)
			{
				foreach(var spectrum in cachedSpectra.Values)
				{
					float w = spectrum.Weight;

					if(w <= 0.005f)
						continue;

					int index = 0;
					Vector3[,] omnidirectional = spectrum.GetSpectrumValues(resolution)[scaleIndex];

					for(int y = 0; y < resolution; ++y)
					{
						for(int x = 0; x < resolution; ++x)
						{
							float nkx = kMap[index].x;
							float nky = kMap[index].y;

							if(nkx == 0.0f && nky == 0.0f)
							{
								nkx = windDirection.x;
								nky = windDirection.y;
							}

							float dp = windDirection.x * nkx + windDirection.y * nky;
							float phi = Mathf.Acos(dp * 0.999f);

							float directionalFactor = Mathf.Sqrt(1.0f + omnidirectional[x, y].z * Mathf.Cos(2.0f * phi));

							if(dp < 0)
								directionalFactor *= directionality;

							directional[index].x += omnidirectional[x, y].x * directionalFactor * w;
							directional[index++].y += omnidirectional[x, y].y * directionalFactor * w;
						}
					}
				}
			}
		}

		private void ComputeTimedSpectra(Vector2[] directional, float[] timed, Vector3[] kMap)
		{
			Vector2 windDirection = targetSpectrum.windWaves.SpectrumResolver.WindDirection;
			float gravity = targetSpectrum.water.Gravity;
			int index = 0;
			int index3 = 0;

			for(int y = 0; y < resolution; ++y)
			{
				for(int x = 0; x < resolution; ++x)
				{
					float nkx = kMap[index].x;
					float nky = kMap[index].y;
					float k = kMap[index].z;

					if(nkx == 0.0f && nky == 0.0f)
					{
						nkx = windDirection.x;
						nky = windDirection.y;
					}

					int index2 = resolution * ((resolution - y) % resolution) + (resolution - x) % resolution;
					
					Vector2 s1 = directional[index];
					Vector2 s2 = directional[index2];

					float t = time * Mathf.Sqrt(gravity * k);

					float s = Mathf.Sin(t);
					float c = Mathf.Cos(t);
					//int icx = ((int)(t * 325.949f) & 2047);
					//float s = FastMath.sines[icx];
					//float c = FastMath.cosines[icx];
					
					float sx = (s1.x + s2.x) * c - (s1.y + s2.y) * s;
					float sy = (s1.x - s2.x) * s + (s1.y - s2.y) * c;

					timed[index3++] = sy * nkx;		// fx1
					timed[index3++] = sy * nky;		// fz1
					timed[index3++] = -sx;			// fy1
					timed[index3++] = sy;			// dy1

					timed[index3++] = sx * nkx;     // fx0
					timed[index3++] = sx * nky;     // fz0
					timed[index3++] = sy;			// fy0
					timed[index3++] = sx;			// dy0

					timed[index3++] = sy * nkx;     // dx0
					timed[index3++] = -sx * nkx;	// dx1
					timed[index3++] = sy * nky;		// dz0
					timed[index3++] = -sx * nky;    // dz1

					++index;
				}
			}
		}

		private void ComputeFFT(float[] data, Vector2[] displacements, vector4[] forceAndHeight, int[][] indices, float[][] weights, float[] pingPongA, float[] pingPongB)
		{
			int resolutionx12 = pingPongA.Length;
			int index = 0;

			for(int y = resolution - 1; y >= 0; --y)
			{
				System.Array.Copy(data, index, pingPongA, 0, resolutionx12);

				FFT(indices, weights, ref pingPongA, ref pingPongB);
				
				System.Array.Copy(pingPongA, 0, data, index, resolutionx12);
				index += resolutionx12;
			}

			index = resolution * (resolution + 1) * 12;

			for(int x = resolution - 1; x >= 0; --x)
			{
				index -= 12;

				int index2 = index;

				for(int y = resolutionx12 - 12; y >= 0; y -= 12)
				{
					index2 -= resolutionx12;

					for(int i=0; i<12; ++i)
						pingPongA[y + i] = data[index2 + i];
                }

				FFT(indices, weights, ref pingPongA, ref pingPongB);

				index2 = index / 12;

				for(int y = resolutionx12 - 12; y >= 0; y -= 12)
				{
					index2 -= resolution;
					
					forceAndHeight[index2] = new vector4(pingPongA[y], pingPongA[y + 2], pingPongA[y + 1], pingPongA[y + 7]);
					displacements[index2] = new Vector2(pingPongA[y + 8], pingPongA[y + 10]);
				}
			}
		}

		private void FFT(int[][] indices, float[][] weights, ref float[] pingPongA, ref float[] pingPongB)
		{
			int numButterflies = weights.Length;

			for(int butterflyIndex = 0; butterflyIndex < numButterflies; ++butterflyIndex)
			{
				var localIndices = indices[numButterflies - butterflyIndex - 1];
				var localWeights = weights[butterflyIndex];
				int i12 = (resolution - 1) * 12;

				for(int i2 = localIndices.Length - 2; i2 >= 0; i2 -= 2)
				{
					int ix = localIndices[i2];
					int iy = localIndices[i2 + 1];

#if WATER_SIMD
					float wyf = localWeights[i2 + 1];
                    vector4 wx = new vector4(localWeights[i2]);
					vector4 wy = new vector4(wyf);
					
					pingPongB.SetVector(pingPongA.GetVector(ix) + wy * pingPongA.GetVector(iy + 4) + wx * pingPongA.GetVector(iy), i12);
					pingPongB.SetVector(pingPongA.GetVector(ix + 4) + wx * pingPongA.GetVector(iy + 4) - wy * pingPongA.GetVector(iy), i12 + 4);

					iy += 8;
					wy = new vector4(-wyf, wyf, -wyf, wyf);
					pingPongB.SetVector(pingPongA.GetVector(ix + 8) + wx * pingPongA.GetVector(iy) + wy * pingPongA.GetVector(iy).Shuffle(ShuffleSel.XFromY | ShuffleSel.YFromX | ShuffleSel.ZFromW | ShuffleSel.WFromZ), i12 + 8);

					i12 -= 12;
#else
					float wx = localWeights[i2];
					float wy = localWeights[i2 + 1];
					int iy4 = iy + 4;

					pingPongB[i12++] = pingPongA[ix++] + wy * pingPongA[iy4++] + wx * pingPongA[iy++];
					pingPongB[i12++] = pingPongA[ix++] + wy * pingPongA[iy4++] + wx * pingPongA[iy++];
					pingPongB[i12++] = pingPongA[ix++] + wy * pingPongA[iy4++] + wx * pingPongA[iy++];
					pingPongB[i12++] = pingPongA[ix++] + wy * pingPongA[iy4++] + wx * pingPongA[iy++];

					iy4 = iy;
					iy -= 4;

					pingPongB[i12++] = pingPongA[ix++] + wx * pingPongA[iy4++] - wy * pingPongA[iy++];
					pingPongB[i12++] = pingPongA[ix++] + wx * pingPongA[iy4++] - wy * pingPongA[iy++];
					pingPongB[i12++] = pingPongA[ix++] + wx * pingPongA[iy4++] - wy * pingPongA[iy++];
					pingPongB[i12++] = pingPongA[ix++] + wx * pingPongA[iy4++] - wy * pingPongA[iy++];

					iy = iy4;

					pingPongB[i12++] = pingPongA[ix++] + wx * pingPongA[iy4++] - wy * pingPongA[iy + 1];
					pingPongB[i12++] = pingPongA[ix++] + wx * pingPongA[iy4++] + wy * pingPongA[iy];
					pingPongB[i12++] = pingPongA[ix++] + wx * pingPongA[iy4++] - wy * pingPongA[iy + 3];
					pingPongB[i12] = pingPongA[ix] + wx * pingPongA[iy4] + wy * pingPongA[iy + 2];

					i12 -= 23;

					//CpuSpectrumValue a1 = pingPongA[ix];
					//CpuSpectrumValue b1 = pingPongA[iy];

					//pingPongB[i].dx0 = a1.dx0 + wx * b1.dx0 - wy * b1.dx1;
					//pingPongB[i].dx1 = a1.dx1 + wy * b1.dx0 + wx * b1.dx1;
					//pingPongB[i].dy0 = a1.dy0 + wx * b1.dy0 - wy * b1.dy1;
					//pingPongB[i].dy1 = a1.dy1 + wy * b1.dy0 + wx * b1.dy1;
					//pingPongB[i].dz0 = a1.dz0 + wx * b1.dz0 - wy * b1.dz1;
					//pingPongB[i].dz1 = a1.dz1 + wy * b1.dz0 + wx * b1.dz1;

					//pingPongB[i].fx0 = a1.fx0 + wx * b1.fx0 - wy * b1.fx1;
					//pingPongB[i].fx1 = a1.fx1 + wy * b1.fx0 + wx * b1.fx1;
					//pingPongB[i].fy0 = a1.fy0 + wx * b1.fy0 - wy * b1.fy1;
					//pingPongB[i].fy1 = a1.fy1 + wy * b1.fy0 + wx * b1.fy1;
					//pingPongB[i].fz0 = a1.fz0 + wx * b1.fz0 - wy * b1.fz1;
					//pingPongB[i].fz1 = a1.fz1 + wy * b1.fz0 + wx * b1.fz1;
#endif
				}

				var t = pingPongA;
				pingPongA = pingPongB;
				pingPongB = t;
			}
		}
		
		public class FFTBuffers
		{
			public float[] timed;
			public float[] pingPongA;
			public float[] pingPongB;
			public int[][] indices;
			public float[][] weights;
			public int numButterflies;
			private int resolution;

			private Dictionary<float, Vector3[]> precomputedKMap = new Dictionary<float, Vector3[]>(new FloatEqualityComparer());

			public FFTBuffers(int resolution)
			{
				this.resolution = resolution;
				timed = new float[resolution * resolution * 12];
				pingPongA = new float[resolution * 12];
				pingPongB = new float[resolution * 12];
				numButterflies = (int)(Mathf.Log((float)resolution) / Mathf.Log(2.0f));
				
				ButterflyFFTUtility.ComputeButterfly(resolution, numButterflies, out indices, out weights);

				for(int ii = 0; ii < indices.Length; ++ii)
				{
					var localIndices = indices[ii];

					for(int i = 0; i < localIndices.Length; ++i)
						localIndices[i] *= 12;
				}
			}

			public Vector3[] GetPrecomputedK(float tileSize)
			{
				Vector3[] map;

				if(!precomputedKMap.TryGetValue(tileSize, out map))
				{
					int halfResolution = resolution >> 1;
					float frequencyScale = 2.0f * Mathf.PI / tileSize;

					map = new Vector3[resolution * resolution];
					int index = 0;

					for(int y = 0; y < resolution; ++y)
					{
						int v = (y + halfResolution) % resolution;
						float ky = frequencyScale * (v/* + 0.5f*/ - halfResolution);

						for(int x = 0; x < resolution; ++x)
						{
							int u = (x + halfResolution) % resolution;
							float kx = frequencyScale * (u/* + 0.5f*/ - halfResolution);

							float k = Mathf.Sqrt(kx * kx + ky * ky);

							map[index++] = new Vector3(k != 0 ? kx / k : 0.0f, k != 0 ? ky / k : 0.0f, k);
						}
					}

					precomputedKMap[tileSize] = map;
				}

				return map;
            }
		}
	}
}
