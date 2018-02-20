using UnityEngine;
#if WATER_SIMD
using vector4 = Mono.Simd.Vector4f;
#else
using vector4 = UnityEngine.Vector4;
#endif

namespace PlayWay.Water
{
	/// <summary>
	/// Holds data for a spectrum of one of water tiles.
	/// </summary>
	public class WaterTileSpectrum
	{
		// work-time data
		public Vector2[] directionalSpectrum;

		// results
		public Vector2[][] displacements;
		public vector4[][] forceAndHeight;
		public float[] resultsTiming;
		public int recentResultIndex;

		// cache
		public float cachedTime = float.NegativeInfinity;
		public float cachedTimeProp;
		public Vector2[] cachedDisplacementsA, cachedDisplacementsB;
		public vector4[] cachedForceAndHeightA, cachedForceAndHeightB;

		// state and context
		public bool resolveByFFT;
		public int directionalSpectrumDirty;
		public int tileIndex;
		private int resolutionFFT;
		private int mipIndexFFT;
		public WindWaves windWaves;
		public Water water;

		public WaterTileSpectrum(WindWaves windWaves, int index)
		{
			this.windWaves = windWaves;
			this.water = windWaves.GetComponent<Water>();
			this.tileIndex = index;
		}

		public bool IsResolvedByFFT
		{
			get { return resolveByFFT; }
		}

		public int ResolutionFFT
		{
			get { return resolutionFFT; }
		}

		public int MipIndexFFT
		{
			get { return mipIndexFFT; }
		}

		public bool SetResolveMode(bool resolveByFFT, int resolution)
		{
			if(this.resolveByFFT != resolveByFFT || (this.resolveByFFT && this.resolutionFFT != resolution))
			{
				if(resolveByFFT)
				{
					lock(this)
					{
						this.resolutionFFT = resolution;
						this.mipIndexFFT = WaterWavesSpectrumData.GetMipIndex(resolution);
						int resolutionSquared = resolution * resolution;
						directionalSpectrum = new Vector2[resolutionSquared];
						displacements = new Vector2[4][];
						forceAndHeight = new vector4[4][];
						resultsTiming = new float[4];
						directionalSpectrumDirty = 2;
						cachedTime = float.NegativeInfinity;
						
						for(int i = 0; i < 4; ++i)
						{
							displacements[i] = new Vector2[resolutionSquared];
							forceAndHeight[i] = new vector4[resolutionSquared];
						}

						if(this.resolveByFFT == false)
						{
							WaterAsynchronousTasks.Instance.AddFFTComputations(this);
							this.resolveByFFT = true;
						}
					}
				}
				else
				{
					WaterAsynchronousTasks.Instance.RemoveFFTComputations(this);
					this.resolveByFFT = false;
				}

				return true;
			}

			return false;
		}

		public void GetResults(float time, out Vector2[] da, out Vector2[] db, out vector4[] fa, out vector4[] fb, out float p)
		{
			if(time == cachedTime)
			{
				// there is a very minor chance of threads reading/writing this in the same time, but this shouldn't have noticeable consequences and should be extremely rare
				da = cachedDisplacementsA;
				db = cachedDisplacementsB;
				fa = cachedForceAndHeightA;
				fb = cachedForceAndHeightB;
				p = cachedTimeProp;

				return;
			}

			int recentResultIndex = this.recentResultIndex;

			for(int i = recentResultIndex - 1; i >= 0; --i)
			{
				if(resultsTiming[i] <= time)
				{
					int nextIndex = i + 1;

					da = displacements[i];
					db = displacements[nextIndex];
					fa = forceAndHeight[i];
					fb = forceAndHeight[nextIndex];

					float duration = resultsTiming[nextIndex] - resultsTiming[i];

					if(duration != 0.0f)
						p = (time - resultsTiming[i]) / duration;
					else
						p = 0.0f;

					if(time > cachedTime)
					{
						cachedDisplacementsA = da;
						cachedDisplacementsB = db;
						cachedForceAndHeightA = fa;
						cachedForceAndHeightB = fb;
						cachedTimeProp = p;
						cachedTime = time;
					}

					return;
				}
			}

			for(int i = resultsTiming.Length - 1; i > recentResultIndex; --i)
			{
				if(resultsTiming[i] <= time)
				{
					int nextIndex = i != displacements.Length - 1 ? i + 1 : 0;

					da = displacements[i];
					db = displacements[nextIndex];
					fa = forceAndHeight[i];
					fb = forceAndHeight[nextIndex];

					float duration = resultsTiming[nextIndex] - resultsTiming[i];

					if(duration != 0.0f)
						p = (time - resultsTiming[i]) / duration;
					else
						p = 0.0f;

					return;
				}
			}

			da = displacements[recentResultIndex];
			db = displacements[recentResultIndex];
			fa = forceAndHeight[recentResultIndex];
			fb = forceAndHeight[recentResultIndex];
			p = 0.0f;
		}
	}
}
