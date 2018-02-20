using System.Collections.Generic;
using UnityEngine;

namespace PlayWay.Water
{
	public class WavesEmitter : MonoBehaviour, IWavesParticleSystemPlugin
	{
		[SerializeField]
		private WavesParticleSystem wavesParticleSystem;
		
		[SerializeField]
		private WavesSource wavesSource;
		
		// single wave frequency
		[SerializeField]
		private float wavelength = 120.0f;

		[SerializeField]
		private float amplitude = 0.6f;

		[SerializeField]
		private float emissionRate = 2.0f;

		[SerializeField]
		private int width = 8;

		// spectrum wave frequencies set
		[Range(0.0f, 180.0f)]
		[SerializeField]
		private float spectrumCoincidenceRange = 20.0f;
		
		[Range(0, 100)]
		[SerializeField]
		private int spectrumWavesCount = 30;

		[Tooltip("Affects both waves and emission area width.")]
		[SerializeField]
		private float span = 1000.0f;

		[Range(1.0f, 3.5f)]
		[SerializeField]
		private float waveShapeIrregularity = 2.0f;

		[SerializeField]
		private float lifetime = 200.0f;

		[SerializeField]
		private bool shoreWaves = true;

		[SerializeField]
		private Vector2 boundsSize = new Vector2(500.0f, 500.0f);

		[Range(3.0f, 80.0f)]
		[SerializeField]
		private float spawnDepth = 8.0f;

		[Range(0.01f, 2.0f)]
		[SerializeField]
		private float emissionFrequencyScale = 1.0f;

		[SerializeField]
		private float spawnPointsDensity = 1.0f;

		private SpawnPoint[] spawnPoints;
		private WindWaves windWaves;
		private float nextSpawnTime;
		private float timeStep;

		void Awake()
		{
			windWaves = wavesParticleSystem.GetComponent<WindWaves>();

			OnValidate();
			wavesParticleSystem.RegisterPlugin(this);
        }
		
		void OnEnable()
		{
			OnValidate();
			nextSpawnTime = Time.time + Random.Range(0.0f, timeStep);
		}

		public void UpdateParticles(float time, float deltaTime)
		{
			if(!isActiveAndEnabled)
				return;

			switch(wavesSource)
			{
				case WavesSource.CustomWaveFrequency:
				{
					if(time > nextSpawnTime)
					{
						Vector3 position = transform.position;
						Vector3 direction = transform.forward;

						var particle = WaveParticle.Create(
							new Vector2(position.x, position.z),
							new Vector2(direction.x, direction.z).normalized,
							2.0f * Mathf.PI / wavelength, amplitude, lifetime, shoreWaves
						);

						if(particle != null)
						{
							wavesParticleSystem.Spawn(particle, width, waveShapeIrregularity);

							particle.Destroy();
							particle.AddToCache();
						}

						nextSpawnTime += timeStep;
					}

					break;
				}
			}
		}

		void OnValidate()
		{
			timeStep = wavelength / emissionRate;
		}

		private void UpdateSpawnPoints(float deltaTime)
		{
			deltaTime *= emissionFrequencyScale;

			for(int i = 0; i < spawnPoints.Length; ++i)
			{
				var spawnPoint = spawnPoints[i];
				spawnPoint.timeLeft -= deltaTime;

				if(spawnPoint.timeLeft < 0)
				{
					float waveLength = 2.0f * Mathf.PI / spawnPoint.frequency;
					float preferredParticleCount = (span * 0.3f) / waveLength;
					int minParticles = Mathf.Max(2, Mathf.RoundToInt(preferredParticleCount * 0.7f));
					int maxParticles = Mathf.Max(2, Mathf.RoundToInt(preferredParticleCount * 1.429f));

					spawnPoint.timeLeft += spawnPoint.timeInterval;
					Vector2 position = spawnPoint.position + new Vector2(spawnPoint.direction.y, -spawnPoint.direction.x) * Random.Range(-span * 0.35f, span * 0.35f);

					var particle = WaveParticle.Create(position, spawnPoint.direction, spawnPoint.frequency, spawnPoint.amplitude, lifetime, shoreWaves);

					if(particle != null)
					{
						wavesParticleSystem.Spawn(particle, Random.Range(minParticles, maxParticles), waveShapeIrregularity);
						particle.Destroy();
						particle.AddToCache();
					}
				}
			}
		}

		private void CreateShoalingSpawnPoints()
		{
			var bounds = new Bounds(transform.position, new Vector3(boundsSize.x, 0.0f, boundsSize.y));
			
			float minX = bounds.min.x;
			float minZ = bounds.min.z;
			float maxX = bounds.max.x;
			float maxZ = bounds.max.z;
			float spawnPointsDensitySqrt = Mathf.Sqrt(spawnPointsDensity);
            float stepX = Mathf.Max(35.0f, bounds.size.x / 256.0f) / spawnPointsDensitySqrt;
			float stepZ = Mathf.Max(35.0f, bounds.size.z / 256.0f) / spawnPointsDensitySqrt;
			bool[,] tiles = new bool[32, 32];
			var spawnPoints = new List<SpawnPoint>();

			var waves = windWaves.SpectrumResolver.SelectShorelineWaves(50, 0, 360);

			if(waves.Length == 0)
			{
				this.spawnPoints = new SpawnPoint[0];
				return;
			}

			float minSpawnDepth = spawnDepth * 0.85f;
			float maxSpawnDepth = spawnDepth * 1.18f;

			for(float z = minZ; z < maxZ; z += stepZ)
			{
				for(float x = minX; x < maxX; x += stepX)
				{
					int tileX = Mathf.FloorToInt(32.0f * (x - minX) / (maxX - minX));
					int tileZ = Mathf.FloorToInt(32.0f * (z - minZ) / (maxZ - minZ));
					
					if(!tiles[tileX, tileZ])
					{
						float depth = StaticWaterInteraction.GetTotalDepthAt(x, z);

						if(depth > minSpawnDepth && depth < maxSpawnDepth && Random.value < 0.06f)
						{
							tiles[tileX, tileZ] = true;

							Vector2 dir;
							dir.x = StaticWaterInteraction.GetTotalDepthAt(x - 3.0f, z) - StaticWaterInteraction.GetTotalDepthAt(x + 3.0f, z);
							dir.y = StaticWaterInteraction.GetTotalDepthAt(x, z - 3.0f) - StaticWaterInteraction.GetTotalDepthAt(x, z + 3.0f);
							dir.Normalize();

							var bestWave = waves[0];
							float bestWaveDot = Vector2.Dot(dir, waves[0].direction);

							for(int i = 1; i < waves.Length; ++i)
							{
								float dot = Vector2.Dot(dir, waves[i].direction);

                                if(dot > bestWaveDot)
								{
									bestWaveDot = dot;
									bestWave = waves[i];
								}
							}
							
							spawnPoints.Add(new SpawnPoint(new Vector2(x, z), dir, bestWave.frequency, Mathf.Abs(bestWave.amplitude), bestWave.speed));
						}
					}
				}
			}

			this.spawnPoints = spawnPoints.ToArray();
		}

		private void CreateSpectralWavesSpawnPoints()
		{
			Vector3 forward = transform.forward.normalized;
			float angle = Mathf.Atan2(forward.x, forward.z);
            var waves = windWaves.SpectrumResolver.SelectShorelineWaves(spectrumWavesCount, angle * Mathf.Rad2Deg, spectrumCoincidenceRange);
			spectrumWavesCount = waves.Length;

			Vector3 center = new Vector3(transform.position.x + span * 0.5f, 0.0f, transform.position.z + span * 0.5f);
			Vector2 centerPos = new Vector2(center.x, center.z);

			var spawnPoints = new List<SpawnPoint>();

			for(int i = 0; i < spectrumWavesCount; ++i)
			{
				var wave = waves[i];

				if(wave.amplitude != 0.0f)
				{
					Vector2 point = centerPos - wave.direction * span * 0.5f;
					spawnPoints.Add(new SpawnPoint(point, wave.direction, wave.frequency, Mathf.Abs(wave.amplitude), wave.speed));
				}
			}

			this.spawnPoints = spawnPoints.ToArray();
		}

		public enum WavesSource
		{
			CustomWaveFrequency
		}

		class SpawnPoint
		{
			public Vector2 position;
			public Vector2 direction;
			public float frequency;
			public float amplitude;
			public float timeInterval;
			public float timeLeft;

			public SpawnPoint(Vector2 position, Vector2 direction, float frequency, float amplitude, float speed)
			{
				this.position = position;
				this.direction = direction;
				this.frequency = frequency;
				this.amplitude = amplitude;

				//this.timeInterval = 2.0f * Mathf.PI / speed;
				this.timeInterval = 2.0f * Mathf.PI / speed * Random.Range(1.0f, 8.0f);
				//this.timeInterval = (2.0f * Mathf.PI / frequency) / speed;
				this.timeLeft = Random.Range(0.0f, timeInterval);
			}
		}
	}
}
