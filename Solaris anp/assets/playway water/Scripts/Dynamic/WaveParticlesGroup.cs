using UnityEngine;

namespace PlayWay.Water
{
	public class WaveParticlesGroup
	{
		public float lastUpdateTime;
		public float lastCostlyUpdateTime;
		public WaveParticle leftParticle;

		public WaveParticlesGroup(float startTime)
		{
			lastUpdateTime = lastCostlyUpdateTime = startTime;
		}

		public int ParticleCount
		{
			get
			{
				int count = 0;
				var particle = leftParticle;
				
				while(particle != null)
				{
					++count;
					particle = particle.rightNeighbour;
				}

				return count;
			}
		}

		public void Update(float time)
		{
			WaveParticle particle = leftParticle;
			float deltaTime = time - lastUpdateTime;
			lastUpdateTime = time;

			float step = deltaTime < 1.0f ? deltaTime : 1.0f;
			float invStep = 1.0f - step;

			do
			{
				var p = particle;
				particle = particle.rightNeighbour;
				p.Update(deltaTime, step, invStep);
			}
			while(particle != null);
		}

		public void CostlyUpdate(WaveParticlesQuadtree quadtree, float time)
		{
			WaveParticle particle = leftParticle;
			float deltaTime = time - lastCostlyUpdateTime;
			lastCostlyUpdateTime = time;
			int numSubdivisions = 0;

			do
			{
				var p = particle;
				particle = particle.rightNeighbour;
				numSubdivisions += p.CostlyUpdate(numSubdivisions < 30 ? quadtree : null, deltaTime);
			}
			while(particle != null);

			particle = leftParticle;
			WaveParticle firstParticleInWave = particle;
			int waveLength = 0;

			do
			{
				var p = particle;
				particle = particle.rightNeighbour;

				++waveLength;

				if(p != firstParticleInWave && (p.disallowSubdivision || particle == null))
				{
					if(waveLength > 3)
						FilterRefractedDirections(firstParticleInWave, p, waveLength);

					firstParticleInWave = particle;
					waveLength = 0;
				}
			}
			while(particle != null);
		}

		/// <summary>
		/// Ensures that whole wave is either expanding or contracting.
		/// </summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <param name="waveLength"></param>
		private void FilterRefractedDirections(WaveParticle left, WaveParticle right, int waveLength)
		{
			WaveParticle p = left;
			int halfLength = waveLength / 2;
			
			Vector2 leftDirection = new Vector2();
			
			for(int i = 0; i < halfLength; ++i)
			{
				leftDirection += p.direction;
				p = p.rightNeighbour;
			}

			Vector2 rightDirection = new Vector2();

			for(int i = halfLength; i < waveLength; ++i)
			{
				rightDirection += p.direction;
				p = p.rightNeighbour;
			}

			leftDirection.Normalize();
			rightDirection.Normalize();

			p = left;

			for(int i = 0; i < waveLength; ++i)
			{
				p.direction = Vector2.Lerp(leftDirection, rightDirection, i / (waveLength - 1));
				p = p.rightNeighbour;
			}
		}
	}
}
