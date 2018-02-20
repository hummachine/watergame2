using System.Collections.Generic;
using UnityEngine;

namespace PlayWay.Water
{
	sealed public class WaveParticle : IPoint2D
	{
		public Vector2 position;
		public Vector2 direction;
		public float speed;
		public float targetSpeed = 1.0f;
		public float baseFrequency;
		public float frequency;
		public float baseAmplitude;
		public float amplitude;
		public float fadeFactor;
		public float energyBalance;
		public float targetEnergyBalance;
		public float shoaling = 0.0f;          // 0.35f
		public float invkh = 1.0f;				// freq * depth
		public float targetInvKh = 1.0f;
        public float baseSpeed;
		public float lifetime;
		public float amplitudeModifiers;
		public float expansionEnergyLoss = 0.0f;
		public bool isShoreWave;
		public bool isAlive = true;
		public bool disallowSubdivision;
        public WaveParticle leftNeighbour;
		public WaveParticle rightNeighbour;
		public WaveParticlesGroup group;

		static private Stack<WaveParticle> waveParticlesCache = new Stack<WaveParticle>();
		static float[] amplitudeFuncPrecomp;
		static float[] frequencyFuncPrecomp;

		static WaveParticle()
		{
			amplitudeFuncPrecomp = new float[2048];
			frequencyFuncPrecomp = new float[2048];

			for(int i = 0; i < 2048; ++i)
			{
				double p = (i + 0.49f) / 2047.0f;
				double ratio = 4.0 * (1.0 - System.Math.Pow(1.0 - p, 0.33333333));
				amplitudeFuncPrecomp[i] = ComputeAmplitudeAtShore(ratio);

				//frequencyFuncPrecomp[i] = 1.0f / ComputeWavelengthAtShore(ratio);		// this is a correct, physical value
				frequencyFuncPrecomp[i] = Mathf.Sqrt(1.0f / ComputeWavelengthAtShore(ratio));       // but this one looks better for some reason
			}
		}

		private WaveParticle(Vector2 position, Vector2 direction, float baseFrequency, float baseAmplitude, float lifetime, bool isShoreWave)
		{
			this.position = position;
			this.direction = direction;
			this.baseFrequency = baseFrequency;
			this.baseAmplitude = baseAmplitude;
			this.fadeFactor = 0.0f;
			this.frequency = baseFrequency;
			this.amplitude = baseAmplitude;
			this.isShoreWave = isShoreWave;
			this.baseSpeed = 3.2f * Mathf.Sqrt(9.81f / baseFrequency);
			this.lifetime = lifetime;

			CostlyUpdate(null, 0.1f);
		}

		public Vector2 Position
		{
			get { return position; }
		}

		public Vector4 PackedParticleData
		{
			get { return new Vector4(direction.x * 2.0f * Mathf.PI / frequency, direction.y * 2.0f * Mathf.PI / frequency, shoaling, frequency); }
		}

		public Vector3 VertexData
		{
			get { return new Vector3(position.x, position.y, amplitude); }
		}

		static public WaveParticle Create(Vector2 position, Vector2 direction, float baseFrequency, float baseAmplitude, float lifetime, bool isShoreWave)
		{
			WaveParticle particle;

			if(waveParticlesCache.Count != 0)
			{
				particle = waveParticlesCache.Pop();
				particle.position = position;
				particle.direction = direction;
				particle.baseFrequency = baseFrequency;
				particle.baseAmplitude = baseAmplitude;
				particle.fadeFactor = 0.0f;
				particle.isShoreWave = isShoreWave;
				particle.baseSpeed = 3.2f * Mathf.Sqrt(9.81f / baseFrequency);
				particle.amplitude = baseAmplitude;
				particle.frequency = baseFrequency;
				particle.targetSpeed = 1.0f;
				particle.invkh = 1.0f;
				particle.targetInvKh = 1.0f;
                particle.energyBalance = 0.0f;
				particle.shoaling = 0.0f;
				particle.speed = 0.0f;
				particle.lifetime = lifetime;
				particle.amplitudeModifiers = 0.0f;
				particle.expansionEnergyLoss = 0.0f;
				particle.isAlive = true;
				particle.disallowSubdivision = false;
                if(particle.leftNeighbour != null || particle.rightNeighbour != null)
				{
					particle.leftNeighbour = null;          // WYWALIC
					particle.rightNeighbour = null;
				}
				particle.CostlyUpdate(null, 0.1f);
			}
			else
				particle = new WaveParticle(position, direction, baseFrequency, baseAmplitude, lifetime, isShoreWave);

			return particle.baseAmplitude != 0.0f ? particle : null;
		}

		public void Destroy()
		{
			baseAmplitude = amplitude = 0.0f;
			isAlive = false;

			if(leftNeighbour != null)
			{
				leftNeighbour.rightNeighbour = rightNeighbour;
				leftNeighbour.disallowSubdivision = true;
			}

			if(rightNeighbour != null)
			{
				rightNeighbour.leftNeighbour = leftNeighbour;
				rightNeighbour.disallowSubdivision = true;

				if(leftNeighbour == null)
					group.leftParticle = rightNeighbour;
			}

			leftNeighbour = null;
			rightNeighbour = null;
		}

		public void AddToCache()
		{
			waveParticlesCache.Push(this);
		}

		public WaveParticle Clone(Vector2 position)
		{
			var particle = Create(position, direction, baseFrequency, baseAmplitude, lifetime, isShoreWave);

			if(particle != null)
			{
				particle.amplitude = amplitude;
				particle.frequency = frequency;
				particle.speed = speed;
				particle.targetSpeed = targetSpeed;
				particle.energyBalance = energyBalance;
				particle.shoaling = shoaling;
				particle.group = group;
            }

			return particle;
		}

		public void Update(float deltaTime, float step, float invStep)
		{
			// fade-in and fade-out
			if(lifetime > 0.0f)
			{
				if(fadeFactor != 1.0f)
				{
					fadeFactor += deltaTime;

					if(fadeFactor > 1.0f)
						fadeFactor = 1.0f;
				}
			}
			else
			{
				fadeFactor -= deltaTime;

				if(fadeFactor <= 0.0f)
				{
					Destroy();
					return;
                }
			}

			// energy loss
			if(targetEnergyBalance < energyBalance)
			{
				float energyLossFactor = step * 0.05f;
				energyBalance = energyBalance * (1.0f - energyLossFactor) + targetEnergyBalance * energyLossFactor;     // inlined Mathf.Lerp(this.energyBalance, energyBalance, 0.05f);
			}
			else
			{
				float energyGainFactor = step * 0.008f;
				energyBalance = energyBalance * (1.0f - energyGainFactor) + targetEnergyBalance * energyGainFactor;      // inlined Mathf.Lerp(this.energyBalance, energyBalance, 0.008f);
			}

			baseAmplitude += deltaTime * energyBalance;
			baseAmplitude *= step * expansionEnergyLoss + 1.0f;

			if(baseAmplitude <= 0.01f)
			{
				Destroy();
				return;
			}

			// shoaling effects
			speed = invStep * speed + step * targetSpeed;
			float realSpeed = speed + energyBalance * -20.0f;            // push wave forward when it starts to break as it may get trapped otherwise
			
			invkh = invStep * invkh + step * targetInvKh;

			int precompIndex = (int)(2047.0f * (1.0f - invkh * invkh * invkh) - 0.49f);
			frequency = baseFrequency * (precompIndex >= 2048 ? 1.0f : frequencyFuncPrecomp[precompIndex]);
			amplitude = fadeFactor * baseAmplitude * (precompIndex >= 2048 ? 1.0f : amplitudeFuncPrecomp[precompIndex]);
			shoaling = amplitudeModifiers * -energyBalance / amplitude;
			amplitude *= amplitudeModifiers;
			
			float speedMulDeltaTime = realSpeed * deltaTime;
			position.x += direction.x * speedMulDeltaTime;
			position.y += direction.y * speedMulDeltaTime;
		}

		public int CostlyUpdate(WaveParticlesQuadtree quadtree, float deltaTime)
		{
			float depth;

			if(frequency < 0.025f)          // in case of big waves, sample center and front of the particle to get a better result
			{
				float posx = position.x + direction.x / frequency;
				float posy = position.y + direction.y / frequency;
				depth = Mathf.Max(StaticWaterInteraction.GetTotalDepthAt(position.x, position.y), StaticWaterInteraction.GetTotalDepthAt(posx, posy));
			}
			else
				depth = StaticWaterInteraction.GetTotalDepthAt(position.x, position.y);

			if(depth <= 0.001f)
			{
				Destroy();
				return 0;
			}

			UpdateWaveParameters(deltaTime, depth);
			
			int numSubdivisions = 0;

			if(quadtree != null && !disallowSubdivision)
			{
				if(leftNeighbour != null)
					Subdivide(quadtree, leftNeighbour, this, ref numSubdivisions);
				
				if(rightNeighbour != null)
					Subdivide(quadtree, this, rightNeighbour, ref numSubdivisions);
			}

			return numSubdivisions;
        }

		private void UpdateWaveParameters(float deltaTime, float depth)
		{
			lifetime -= deltaTime;

			targetInvKh = 1.0f - 0.25f * baseFrequency * depth;

			if(targetInvKh < 0.0f)
				targetInvKh = 0.0f;

			// a lot faster than: targetTanh = Mathf.Sqrt((float)System.Math.Tanh(baseFrequency * depth));
			int precompIndex = (int)(baseFrequency * depth * 512.0f);
			targetSpeed = baseSpeed * (precompIndex >= 2048 ? 1.0f : FastMath.positiveTanhSqrtNoZero[precompIndex]);

			if(targetSpeed < 0.5f)
				targetSpeed = 0.5f;
			
			targetEnergyBalance = baseFrequency * -0.0004f;
			float wavelength = 0.224f / frequency;				// 0.5 * PI / 7 = 0.224

			if(wavelength < amplitude)
				targetEnergyBalance += 0.04f * (wavelength - amplitude);
			
			// refraction
			if(leftNeighbour != null && rightNeighbour != null && !disallowSubdivision)
			{
				Vector2 newDirection = new Vector2(
					rightNeighbour.position.y - leftNeighbour.position.y,
					leftNeighbour.position.x - rightNeighbour.position.x
				);

				// normalize
				float newDirectionLen = Mathf.Sqrt(newDirection.x * newDirection.x + newDirection.y * newDirection.y);

				if(newDirectionLen > 0.001f)
				{
					if(newDirection.x * direction.x + newDirection.y * direction.y < 0)
						newDirectionLen = -newDirectionLen;

					newDirection.x /= newDirectionLen;
					newDirection.y /= newDirectionLen;

					float refractionFactor = 0.06f * deltaTime;
					if(refractionFactor > 0.06f)
						refractionFactor = 0.06f;

					// inlined Vector2.Lerp(direction, newDirection, 0.00005f);
					direction.x = direction.x * (1.0f - refractionFactor) + newDirection.x * refractionFactor;
					direction.y = direction.y * (1.0f - refractionFactor) + newDirection.y * refractionFactor;

					// normalize
					float directionLen = Mathf.Sqrt(direction.x * direction.x + direction.y * direction.y);
					direction.x /= directionLen;
					direction.y /= directionLen;
				}
				
				//expansionEnergyLoss = 0.5f * (Vector2.Dot(direction, leftNeighbour.direction) + Vector2.Dot(direction, rightNeighbour.direction));			// inlined below
				expansionEnergyLoss = -1.0f + 0.5f * (direction.x * (leftNeighbour.direction.x + rightNeighbour.direction.x) + direction.y * (leftNeighbour.direction.y + rightNeighbour.direction.y));

				if(expansionEnergyLoss < -1.0f)
					expansionEnergyLoss = -1.0f;

				if(leftNeighbour.disallowSubdivision)
					leftNeighbour.expansionEnergyLoss = expansionEnergyLoss;

				if(rightNeighbour.disallowSubdivision)
					rightNeighbour.expansionEnergyLoss = expansionEnergyLoss;

				amplitudeModifiers = 1.0f;
			}
			else
				amplitudeModifiers = 0.5f;

			if(isShoreWave)
			{
				// inlined 1.0f - FastMath.TanhSqrt2048Positive(depth * 0.01f)
				int precompIndex2 = (int)(depth * (0.01f * 512.0f));

				if(precompIndex2 < 2048)
					amplitudeModifiers *= 1.0f - FastMath.positiveTanhSqrtNoZero[precompIndex2];
			}
		}

		private void Subdivide(WaveParticlesQuadtree quadtree, WaveParticle left, WaveParticle right, ref int numSubdivisions)
		{
			Vector2 diff = left.position - right.position;
			float distance = diff.magnitude;

			if(distance * frequency > 1.0f && distance > 1.0f && quadtree.FreeSpace != 0)          // don't subdivide below 1m on CPU
			{
				var newParticle = Create(right.position + diff * 0.5f, (left.direction + right.direction) * 0.5f, (left.baseFrequency + right.baseFrequency) * 0.5f, (left.baseAmplitude + right.baseAmplitude) * 0.5f, (left.lifetime + right.lifetime) * 0.5f, left.isShoreWave);

				if(newParticle != null)
				{
					newParticle.group = left.group;
					newParticle.amplitude = (left.amplitude + right.amplitude) * 0.5f;
					newParticle.frequency = (left.frequency + right.frequency) * 0.5f;
					newParticle.speed = (left.speed + right.speed) * 0.5f;
					newParticle.targetSpeed = (left.targetSpeed + right.targetSpeed) * 0.5f;
					newParticle.energyBalance = (left.energyBalance + right.energyBalance) * 0.5f;
					newParticle.shoaling = (left.shoaling + right.shoaling) * 0.5f;

					if(quadtree.AddElement(newParticle))
					{
						/*const float subdivideEnergyLoss = 0.94f;

						left.baseAmplitude *= subdivideEnergyLoss;
						left.amplitude *= subdivideEnergyLoss;
						right.baseAmplitude *= subdivideEnergyLoss;
						right.amplitude *= subdivideEnergyLoss;
						newParticle.baseAmplitude *= subdivideEnergyLoss;
						newParticle.amplitude *= subdivideEnergyLoss;*/

						newParticle.leftNeighbour = left;
						newParticle.rightNeighbour = right;
						left.rightNeighbour = newParticle;
						right.leftNeighbour = newParticle;
					}

					++numSubdivisions;
				}
			}
		}

		/// <summary>
		/// http://link.springer.com/referenceworkentry/10.1007%2F0-387-30843-1_413
		/// </summary>
		/// <param name="ratio">Ratio of depth to deep water wavelength.</param>
		/// <returns></returns>
		static private float ComputeAmplitudeAtShore(double kh)
		{
			double cosh = System.Math.Cosh(kh);
            return (float)System.Math.Sqrt(2.0 * cosh * cosh / (System.Math.Sinh(2.0 * kh) + 2.0 * kh));
        }

		/// <summary>
		/// Fenton and McKee (1989)
		/// </summary>
		/// <param name="ratio">Ratio of depth to deep water wavelength.</param>
		/// <returns></returns>
		static private float ComputeWavelengthAtShore(double kh)
		{
			return (float)System.Math.Pow(System.Math.Tanh(System.Math.Pow(kh * System.Math.Tanh(kh), 0.75)), 0.666666);
        }
	}
}
