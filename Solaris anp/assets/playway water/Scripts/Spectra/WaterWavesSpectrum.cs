using UnityEngine;

namespace PlayWay.Water
{
	/// <summary>
	/// Base class for oceanic omnidirectional spectrum generators.
	/// </summary>
	abstract public class WaterWavesSpectrum
	{
		protected float tileSize;
		protected float gravity;
		protected float windSpeed;
		protected float amplitude;

		public WaterWavesSpectrum(float tileSize, float gravity, float windSpeed, float amplitude)
		{
			this.tileSize = tileSize;
			this.gravity = gravity;
			this.windSpeed = windSpeed;
			this.amplitude = amplitude;
		}

		public float TileSize
		{
			get { return tileSize * WaterQualitySettings.Instance.TileSizeScale; }
		}

		public float Gravity
		{
			get { return gravity; }
		}
		
		abstract public void ComputeSpectrum(Vector3[,] spectrum, float tileSizeMultiplier, int maxResolution, System.Random random);
	}
}
