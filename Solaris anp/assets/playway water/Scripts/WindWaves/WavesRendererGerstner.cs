using UnityEngine;

namespace PlayWay.Water
{
	/// <summary>
	/// Displays water spectrum using a few Gerstner waves directly in vertex shader. Works on all platforms.
	/// </summary>
	[System.Serializable]
	public class WavesRendererGerstner
	{
		[Range(0, 20)]
		[SerializeField]
		private int numGerstners = 20;

		private Water water;
		private WindWaves windWaves;
		private Gerstner4[] gerstnerFours;
		private int lastUpdateFrame;
		private bool enabled;

		internal void Enable(WindWaves windWaves)
		{
			if(enabled) return;

			enabled = true;

			this.water = windWaves.GetComponent<Water>();
			this.windWaves = windWaves;

			if(Application.isPlaying)
			{
				water.ProfilesChanged.AddListener(OnProfilesChanged);
				FindBestWaves();
			}
		}

		internal void Disable()
		{
			if(!enabled) return;

			enabled = false;

			if(water != null)
				water.InvalidateMaterialKeywords();
		}

		internal void OnValidate(WindWaves windWaves)
		{
			if(enabled)
				FindBestWaves();
		}

		public bool Enabled
		{
			get { return enabled; }
		}

		private void FindBestWaves()
		{
			gerstnerFours = windWaves.SpectrumResolver.FindGerstners(numGerstners, false);
			UpdateMaterial();
		}

		private void UpdateMaterial()
		{
			var material = water.WaterMaterial;
			//material.SetVector("_GerstnerOrigin", new Vector4(water.TileSize + (0.5f / water.SpectraRenderer.FinalResolution) * water.TileSize, -water.TileSize + (0.5f / water.SpectraRenderer.FinalResolution) * water.TileSize, 0.0f, 0.0f));

			for(int index = 0; index < gerstnerFours.Length; ++index)
			{
				var gerstner4 = gerstnerFours[index];

				Vector4 amplitude, directionAB, directionCD, frequencies;

				amplitude.x = gerstner4.wave0.amplitude;
				frequencies.x = gerstner4.wave0.frequency;
				directionAB.x = gerstner4.wave0.direction.x;
				directionAB.y = gerstner4.wave0.direction.y;

				amplitude.y = gerstner4.wave1.amplitude;
				frequencies.y = gerstner4.wave1.frequency;
				directionAB.z = gerstner4.wave1.direction.x;
				directionAB.w = gerstner4.wave1.direction.y;

				amplitude.z = gerstner4.wave2.amplitude;
				frequencies.z = gerstner4.wave2.frequency;
				directionCD.x = gerstner4.wave2.direction.x;
				directionCD.y = gerstner4.wave2.direction.y;

				amplitude.w = gerstner4.wave3.amplitude;
				frequencies.w = gerstner4.wave3.frequency;
				directionCD.z = gerstner4.wave3.direction.x;
				directionCD.w = gerstner4.wave3.direction.y;

				material.SetVector("_GrAB" + index, directionAB);
				material.SetVector("_GrCD" + index, directionCD);
				material.SetVector("_GrAmp" + index, amplitude);
				material.SetVector("_GrFrq" + index, frequencies);
			}

			// zero unused waves
			for(int index = gerstnerFours.Length; index < 5; ++index)
				material.SetVector("_GrAmp" + index, Vector4.zero);
		}

		public void OnWaterRender(Camera camera)
		{
			if(!Application.isPlaying || !enabled) return;

			UpdateWaves();
		}

		public void OnWaterPostRender(Camera camera)
		{

		}

		public void BuildShaderVariant(ShaderVariant variant, Water water, WindWaves windWaves, WaterQualityLevel qualityLevel)
		{
			variant.SetUnityKeyword("_WAVES_GERSTNER", enabled);
		}

		private void UpdateWaves()
		{
			int frameCount = Time.frameCount;

			if(lastUpdateFrame == frameCount)
				return;         // it's already done

			lastUpdateFrame = frameCount;

			var material = water.WaterMaterial;
			float t = Time.time;

			for(int index = 0; index < gerstnerFours.Length; ++index)
			{
				var gerstner4 = gerstnerFours[index];

				Vector4 offset;
				offset.x = gerstner4.wave0.offset + gerstner4.wave0.speed * t;
				offset.y = gerstner4.wave1.offset + gerstner4.wave1.speed * t;
				offset.z = gerstner4.wave2.offset + gerstner4.wave2.speed * t;
				offset.w = gerstner4.wave3.offset + gerstner4.wave3.speed * t;

				material.SetVector("_GrOff" + index, offset);
			}
		}

		private void OnProfilesChanged(Water water)
		{
			FindBestWaves();
		}
	}

	public class Gerstner4
	{
		public GerstnerWave wave0;
		public GerstnerWave wave1;
		public GerstnerWave wave2;
		public GerstnerWave wave3;

		public Gerstner4(GerstnerWave wave0, GerstnerWave wave1, GerstnerWave wave2, GerstnerWave wave3)
		{
			this.wave0 = wave0;
			this.wave1 = wave1;
			this.wave2 = wave2;
			this.wave3 = wave3;
		}
	}

	public class GerstnerWave
	{
		public Vector2 direction;
		public float amplitude;
		public float offset;
		public float frequency;
		public float speed;

		public GerstnerWave()
		{
			direction = new Vector2(0, 1);
			frequency = 1;
		}

		public GerstnerWave(Vector2 direction, float amplitude, float offset, float frequency, float speed)
		{
			this.direction = direction;
			this.amplitude = amplitude;
			this.offset = offset;
			this.frequency = frequency;
			this.speed = speed;
		}
	}
}
