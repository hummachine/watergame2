using System.Collections.Generic;
using UnityEngine;

namespace PlayWay.Water
{
	/// <summary>
	/// Simulates wave particles.
	/// </summary>
	[RequireComponent(typeof(WaterOverlays))]
	public class WavesParticleSystem : MonoBehaviour, IOverlaysRenderer
	{
		[HideInInspector]
		[SerializeField]
		private Shader waterWavesParticlesShader;

		[SerializeField]
		private int maxParticles = 50000;

		[SerializeField]
		private int maxParticlesPerTile = 2000;

		[SerializeField]
		private float prewarmTime = 40.0f;

		[Tooltip("Allowed execution time per frame.")]
		[SerializeField]
		private float timePerFrame = 0.8f;

		private WaveParticlesQuadtree particles;

		private Water water;
		private Material waterWavesParticlesMaterial;
		private List<IWavesParticleSystemPlugin> plugins;
		private float simulationTime;
		private float timePerFrameExp;
        private bool prewarmed;

		private int lastLinearParticleCostlyUpdate;

		public WavesParticleSystem()
		{
			plugins = new List<IWavesParticleSystemPlugin>();
		}

		void Awake()
		{
			water = GetComponent<Water>();
			OnValidate();
		}

		public int ParticleCount
		{
			get { return particles.Count; }
		}
		
		public bool Spawn(WaveParticle particle)
		{
			if(particle != null)
			{
				particle.group = new WaveParticlesGroup(simulationTime);
				particle.group.leftParticle = particle;
				return particles.AddElement(particle);
			}
			else
				return false;
		}

		public bool Spawn(WaveParticle particle, int clones, float waveShapeIrregularity)
		{
			if(particle == null || particles.FreeSpace < clones * 2 + 1)
				return false;

			particle.group = new WaveParticlesGroup(simulationTime);
			particle.baseAmplitude *= water.UniformWaterScale;
			particle.baseFrequency /= water.UniformWaterScale;

			WaveParticle previousParticle = null;

			float minAmplitude = 1.0f / waveShapeIrregularity;

			for(int i=-clones; i<=clones; ++i)
			{
				var p = particle.Clone(particle.position + new Vector2(particle.direction.y, -particle.direction.x) * (i * 1.48f / particle.baseFrequency));

				if(p == null)
					continue;

				p.baseAmplitude *= Random.Range(minAmplitude, 1.0f);
				p.leftNeighbour = previousParticle;

				if(previousParticle != null)
				{
					previousParticle.rightNeighbour = p;

					if(i == clones)
						p.disallowSubdivision = true;           // it's a last particle of the group
				}
				else
				{
					p.group.leftParticle = p;               // it's a first particle of the group
					p.disallowSubdivision = true;
				}
				
				if(!particles.AddElement(p))
					return previousParticle != null;

				previousParticle = p;
			}

			return true;
		}

		void OnEnable()
		{
			CheckResources();
		}

		public void RenderOverlays(WaterOverlaysData overlays)
		{
			if(enabled)
				RenderParticles(overlays);
		}

		void OnDisable()
		{
			FreeResources();
		}

		void OnValidate()
		{
			timePerFrameExp = Mathf.Exp(timePerFrame * 0.5f);

			if(waterWavesParticlesShader == null)
				waterWavesParticlesShader = Shader.Find("PlayWay Water/Particles/Particles");
        }

		void LateUpdate()
		{
			if(!prewarmed)
				Prewarm();
			
			UpdateSimulation(Time.deltaTime);
		}

		public void RegisterPlugin(IWavesParticleSystemPlugin plugin)
		{
			if(!plugins.Contains(plugin))
				plugins.Add(plugin);
		}

		public void UnregisterPlugin(IWavesParticleSystemPlugin plugin)
		{
			plugins.Remove(plugin);
		}

		private void Prewarm()
		{
			prewarmed = true;

			while(simulationTime < prewarmTime)
				UpdateSimulationWithoutFrameBudget(0.1f);
		}

		private void UpdateSimulation(float deltaTime)
		{
			simulationTime += deltaTime;

			UpdatePlugins(deltaTime);
			particles.UpdateSimulation(simulationTime, timePerFrameExp);
		}

		private void UpdateSimulationWithoutFrameBudget(float deltaTime)
		{
			simulationTime += deltaTime;

			UpdatePlugins(deltaTime);
			particles.UpdateSimulation(simulationTime);
		}

		private void UpdatePlugins(float deltaTime)
		{
			int numPlugins = plugins.Count;
			for(int i = 0; i < numPlugins; ++i)
				plugins[i].UpdateParticles(simulationTime, deltaTime);
		}

		private void RenderParticles(WaterOverlaysData overlays)
		{
			if(overlays.UtilityMap == null)
				Graphics.SetRenderTarget(new RenderBuffer[] { overlays.DynamicDisplacementMap.colorBuffer, overlays.SlopeMap.colorBuffer }, overlays.DynamicDisplacementMap.depthBuffer);
			else
				Graphics.SetRenderTarget(new RenderBuffer[] { overlays.DynamicDisplacementMap.colorBuffer, overlays.SlopeMap.colorBuffer, overlays.UtilityMap.colorBuffer }, overlays.DynamicDisplacementMap.depthBuffer);
			
			//var spray = GetComponent<WaterSpray>();
			//Graphics.SetRandomWriteTarget(2, spray.ParticlesBuffer);
			Vector4 localMapsShaderCoords = overlays.Camera.LocalMapsShaderCoords;
			float uniformWaterScale = GetComponent<Water>().UniformWaterScale;
            waterWavesParticlesMaterial.SetFloat("_WaterScale", uniformWaterScale);
			waterWavesParticlesMaterial.SetVector("_LocalMapsCoords", localMapsShaderCoords);
			waterWavesParticlesMaterial.SetPass(0);
			
			particles.Render(overlays.Camera.LocalMapsRect);
			//Graphics.ClearRandomWriteTargets();
		}
		
		private void CheckResources()
		{
			if(waterWavesParticlesMaterial == null)
			{
				waterWavesParticlesMaterial = new Material(waterWavesParticlesShader);
				waterWavesParticlesMaterial.hideFlags = HideFlags.DontSave;
            }

			if(particles == null)
				particles = new WaveParticlesQuadtree(new Rect(-1000.0f, -1000.0f, 2000.0f, 2000.0f), maxParticlesPerTile, maxParticles);
		}

		private void FreeResources()
		{
			if(waterWavesParticlesMaterial != null)
			{
				waterWavesParticlesMaterial.Destroy();
				waterWavesParticlesMaterial = null;
			}
		}
	}
}
