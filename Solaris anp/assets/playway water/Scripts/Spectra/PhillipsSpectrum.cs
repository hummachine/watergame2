using UnityEngine;

namespace PlayWay.Water
{
	/// <summary>
	/// Spectrum is based on the following paper:
	/// "Simulating Ocean Water" Jerry Tessendorf
	/// </summary>
	public class PhillipsSpectrum : WaterWavesSpectrum
	{
		private float cutoffFactor;
		
		public PhillipsSpectrum(float tileSize, float gravity, float windSpeed, float amplitude, float cutoffFactor) : base(tileSize, gravity, windSpeed, amplitude)
		{
			this.cutoffFactor = cutoffFactor;
		}

		public override void ComputeSpectrum(Vector3[,] spectrum, float tileSizeMultiplier, int maxResolution, System.Random random)
		{
			float tileSize = TileSize * tileSizeMultiplier;
			float totalAmplitude = amplitude * ComputeWaveAmplitude(windSpeed);
			float realSizeInv = 1.0f / tileSize;

			int resolution = spectrum.GetLength(0);
			int halfResolution = resolution / 2;
			float linearWindSpeed = windSpeed;
			float L = linearWindSpeed * linearWindSpeed / gravity;
			float LPow2 = L * L;
			float l = FastMath.Pow2(L / cutoffFactor);
			
			float scale = Mathf.Sqrt(totalAmplitude * Mathf.Pow(100.0f / tileSize, 2.35f) / 2000000.0f);
			
			for(int x=0; x<resolution; ++x)
			{
				float kx = 2.0f * Mathf.PI * (x/* + 0.5f*/ - halfResolution) * realSizeInv;

				for(int y=0; y<resolution; ++y)
				{
					float ky = 2.0f * Mathf.PI * (y/* + 0.5f*/ - halfResolution) * realSizeInv;

					float k = Mathf.Sqrt(kx * kx + ky * ky);
					float kk = k * k;
					float kkkk = kk * kk;
					
					float p = Mathf.Exp(-1.0f / (kk * LPow2) - kk * l) / kkkk;
					p = scale * Mathf.Sqrt(p);

					float h = FastMath.Gauss01() * p;
					float hi = FastMath.Gauss01() * p;

					int xCoord = (x + halfResolution) % resolution;
					int yCoord = (y + halfResolution) % resolution;

					if(x == halfResolution && y == halfResolution)
					{
						h = 0;
						hi = 0;
					}
					
					spectrum[xCoord, yCoord] = new Vector3(h, hi, 1.0f);
				}
			}
		}

		/// <summary>
		/// Computes maximum wave amplitude from a wind speed. Waves amplitude is a third power of the wind speed.
		/// </summary>
		/// <param name="windSpeed"></param>
		/// <returns></returns>
		static private float ComputeWaveAmplitude(float windSpeed)
		{
			return 0.002f * windSpeed * windSpeed * windSpeed;
		}
	}
}
