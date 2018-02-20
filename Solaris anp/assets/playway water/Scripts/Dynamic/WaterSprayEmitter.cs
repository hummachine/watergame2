using UnityEngine;

namespace PlayWay.Water
{
	public class WaterSprayEmitter : MonoBehaviour
	{
		[SerializeField]
		private WaterSpray water;

		[SerializeField]
		private float emissionRate = 5.0f;

		[SerializeField]
		private float startIntensity = 1.0f;

		[SerializeField]
		private float startVelocity = 1.0f;

		[SerializeField]
		private float lifetime = 4.0f;

		private float totalTime;
		private float timeStep;
		private WaterSpray.Particle[] particles;

		void Start()
		{
			OnValidate();
			particles = new WaterSpray.Particle[Mathf.Max(1, (int)emissionRate)];
        }
		
		void Update()
		{
			int particleIndex = 0;
			totalTime += Time.deltaTime;

			while(totalTime >= timeStep)
			{
				totalTime -= timeStep;

				particles[particleIndex].lifetime = new Vector2(lifetime, lifetime);
				particles[particleIndex].maxIntensity = startIntensity;
				particles[particleIndex].position = transform.position + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f));
				particles[particleIndex].velocity = transform.forward * startVelocity;
				particles[particleIndex++].offset = Random.Range(0.0f, 10.0f);
			}

			if(particleIndex != 0)
				water.SpawnCustomParticles(particles, particleIndex);
		}

		void OnValidate()
		{
			timeStep = 1.0f / emissionRate;
		}
	}
}
