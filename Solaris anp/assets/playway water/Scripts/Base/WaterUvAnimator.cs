using UnityEngine;

namespace PlayWay.Water
{
	/// <summary>
	/// Animates water UV in time to simulate a water flow.
	/// </summary>
	[System.Serializable]
	sealed public class WaterUvAnimator
	{
		private NormalMapAnimation normalMapAnimation1 = new NormalMapAnimation(1.0f, -10.0f, 1.0f, new Vector2(1.0f, 1.0f));
		private NormalMapAnimation normalMapAnimation2 = new NormalMapAnimation(-0.55f, 20.0f, 0.74f, new Vector2(1.5f, 1.5f));

		private float windOffset1x, windOffset1y;
		private float windOffset2x, windOffset2y;
		private Vector2 windSpeed1;
		private Vector2 windSpeed2;
		private Vector2 windSpeed;

		private Water water;
		private WindWaves windWaves;
		private bool hasWindWaves;

		private int bumpMapST;
		private int detailAlbedoMapST;
		private Vector4 uvTransform1;
		private Vector4 uvTransform2;
		private bool windVectorsDirty = true;

		private float lastTime;

		public void Start(Water water)
		{
			this.water = water;
			this.windWaves = water.GetComponent<WindWaves>();
			this.hasWindWaves = windWaves != null;

			bumpMapST = Shader.PropertyToID("_BumpMap_ST");
			detailAlbedoMapST = Shader.PropertyToID("_DetailAlbedoMap_ST");
		}

		public Vector2 WindOffset
		{
			get { return new Vector2(windOffset1x, windOffset1y); }
		}

		public NormalMapAnimation NormalMapAnimation1
		{
			get { return normalMapAnimation1; }
			set
			{
				normalMapAnimation1 = value;
				windVectorsDirty = true;
				uvTransform1.x = normalMapAnimation1.Tiling.x;
				uvTransform1.y = normalMapAnimation1.Tiling.y;
            }
		}

		public NormalMapAnimation NormalMapAnimation2
		{
			get { return normalMapAnimation2; }
			set
			{
				normalMapAnimation2 = value;
				windVectorsDirty = true;
				uvTransform2.x = normalMapAnimation2.Tiling.x;
				uvTransform2.y = normalMapAnimation2.Tiling.y;
			}
		}

		public void Update()
		{
			float time = water.Time;
			float deltaTime = time - lastTime;
			lastTime = time;

			if(windVectorsDirty || HasWindSpeedChanged())
			{
				PrecomputeWindVectors();
				windVectorsDirty = false;
			}

			// apply offset
			windOffset1x += windSpeed1.x * deltaTime;
			windOffset1y += windSpeed1.y * deltaTime;
			windOffset2x += windSpeed2.x * deltaTime;
			windOffset2y += windSpeed2.y * deltaTime;

			uvTransform1.z = -windOffset1x * uvTransform1.x;
			uvTransform1.w = -windOffset1y * uvTransform1.y;

			uvTransform2.z = -windOffset2x * uvTransform2.x;
			uvTransform2.w = -windOffset2x * uvTransform2.y;

			// apply to material
			var waterMaterial = water.WaterMaterial;
			waterMaterial.SetVector(bumpMapST, uvTransform1);
			waterMaterial.SetVector(detailAlbedoMapST, uvTransform2);
		}
		
		private void PrecomputeWindVectors()
		{
			windSpeed = GetWindSpeed();
			windSpeed1 = FastMath.Rotate(windSpeed, normalMapAnimation1.Deviation * Mathf.Deg2Rad) * (normalMapAnimation1.Speed * 0.001365f);
			windSpeed2 = FastMath.Rotate(windSpeed, normalMapAnimation2.Deviation * Mathf.Deg2Rad) * (normalMapAnimation2.Speed * 0.00084f);
		}

		private Vector2 GetWindSpeed()
		{
			if(hasWindWaves)
				return windWaves.WindSpeed;
			else
				return new Vector2(1.0f, 0.0f);
		}

		private bool HasWindSpeedChanged()
		{
			if(hasWindWaves)
				return windWaves.WindSpeedChanged;
			else
				return false;
		}
	}
}
