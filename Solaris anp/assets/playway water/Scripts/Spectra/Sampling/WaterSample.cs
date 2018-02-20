using System;
using UnityEngine;

namespace PlayWay.Water
{
	/// <summary>
	///     Computes water data at a given point.
	/// </summary>
	public sealed class WaterSample
	{
		private readonly Water water;
		private float x;
		private float z;
		private float time;

		private Vector3 displaced;
		private Vector3 previousResult;
		private Vector3 forces;
		private Vector3 previousForces;

		private bool finished;
		private bool enqueued;

		private readonly float precision;
		private readonly float horizontalThreshold;
		private readonly DisplacementMode displacementMode;

		public WaterSample(Water water, DisplacementMode displacementMode = DisplacementMode.Height, float precision = 1.0f)
		{
			if(water == null)
				throw new System.ArgumentException("Argument 'water' is null.");

			if(precision <= 0.0f || precision > 1.0f) throw new System.ArgumentException("Precision has to be between 0.0 and 1.0.");

			this.precision = precision;
			this.horizontalThreshold = 0.045f / (precision * precision * precision);

			this.water = water;
			this.displacementMode = displacementMode;
			this.previousResult.x = float.NaN;
		}

		public bool Finished
		{
			get { return finished; }
		}

		public Vector2 Position
		{
			get { return new Vector2(x, z); }
		}

		/// <summary>
		/// Starts water height computations.
		/// </summary>
		/// <param name="origin"></param>
		public void Start(Vector3 origin)
		{
			previousResult = origin;
			finished = true;
			GetAndReset(origin.x, origin.z);
		}

		/// <summary>
		/// Starts water height computations.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="z"></param>
		public void Start(float x, float z)
		{
			previousResult = new Vector3(x, water.transform.position.y, z);
			finished = true;
			GetAndReset(x, z);
		}

		/// <summary>
		/// Retrieves recently computed displacement and restarts computations on a new position.
		/// </summary>
		/// <param name="origin"></param>
		/// <param name="mode"></param>
		/// <returns></returns>
		public Vector3 GetAndReset(Vector3 origin, ComputationsMode mode = ComputationsMode.Normal)
		{
			return GetAndReset(origin.x, origin.z, mode);
		}

		/// <summary>
		/// Retrieves recently computed displacement and restarts computations on a new position.
		/// </summary>
		/// <param name="x">World space coordinate.</param>
		/// <param name="z">World space coordinate.</param>
		/// <param name="mode">Determines if the computations should be completed on the current thread if necessary. May hurt performance, but setting it to false may cause some 'flickering'.</param>
		/// <returns></returns>
		public Vector3 GetAndReset(float x, float z, ComputationsMode mode = ComputationsMode.Normal)
		{
			Vector3 forces;
			return GetAndReset(x, z, mode, out forces);
		}

		/// <summary>
		/// Retrieves recently computed displacement and restarts computations on a new position.
		/// </summary>
		/// <param name="x">World space coordinate.</param>
		/// <param name="z">World space coordinate.</param>
		/// <param name="mode">Determines if the computations should be completed on the current thread if necessary. May hurt performance, but setting it to false may cause some 'flickering'.</param>
		/// <param name="forces">Wave force vector.</param>
		/// <returns></returns>
		public Vector3 GetAndReset(float x, float z, ComputationsMode mode, out Vector3 forces)
		{
			switch(mode)
			{
				case ComputationsMode.ForceCompletion:
				{
					if(!finished)
					{
						finished = true;
						ComputationStep(true);
					}

					break;
				}

				case ComputationsMode.Normal:
				{
					if(!finished && !float.IsNaN(previousResult.x))
					{
						forces = previousForces;
						return previousResult;
					}

					previousResult = this.displaced;
					previousForces = this.forces;

					break;
				}
			}

			finished = true;

			if(!enqueued)
			{
				WaterAsynchronousTasks.Instance.AddWaterSampleComputations(this);
				enqueued = true;

				water.OnSamplingStarted();
			}

			Vector3 result = this.displaced;
			result.y += water.transform.position.y;
			forces = this.forces;

			this.x = x;
			this.z = z;
			this.displaced.x = x;
			this.displaced.y = 0.0f;
			this.displaced.z = z;
			this.forces.x = 0.0f;
			this.forces.y = 0.0f;
			this.forces.z = 0.0f;
			this.time = water.Time;
			this.finished = false;

			return result;
		}

		/// <summary>
		/// Faster version of GetAndReset. Assumes HeightAndForces displacement mode and that computations were started earlier with a Start call. 
		/// </summary>
		/// <param name="x"></param>
		/// <param name="z"></param>
		/// <param name="time"></param>
		/// <param name="result"></param>
		/// <param name="forces"></param>
		public void GetAndResetFast(float x, float z, float time, ref Vector3 result, ref Vector3 forces)
		{
			if(!finished)
			{
				forces = previousForces;
				result = previousResult;
				return;
			}

			previousResult = this.displaced;
			previousForces = this.forces;

			result = this.displaced;
			result.y += water.transform.position.y;
			forces = this.forces;

			this.x = x;
			this.z = z;
			this.displaced.x = x;
			this.displaced.y = 0.0f;
			this.displaced.z = z;
			this.forces.x = 0.0f;
			this.forces.y = 0.0f;
			this.forces.z = 0.0f;
			this.time = time;
			this.finished = false;
		}

		public Vector3 Stop()
		{
			if(enqueued)
			{
				if(WaterAsynchronousTasks.HasInstance)
					WaterAsynchronousTasks.Instance.RemoveWaterSampleComputations(this);

				enqueued = false;

				if(water != null)
					water.OnSamplingStopped();
			}

			return displaced;
		}

		private static readonly float[] weights = { 0.85f, 0.75f, 0.83f, 0.77f, 0.85f, 0.75f, 0.85f, 0.75f, 0.83f, 0.77f, 0.85f, 0.75f, 0.85f, 0.75f };

		internal void ComputationStep(bool ignoreFinishedFlag = false)
		{
			if(!finished || ignoreFinishedFlag)
			{
				if(displacementMode == DisplacementMode.Height || displacementMode == DisplacementMode.HeightAndForces)
				{
					CompensateHorizontalDisplacement();

					if(displacementMode == DisplacementMode.Height)
					{
						// compute height at resultant point
						float result = water.GetHeightAt(x, z, 0.0f, precision, time);
						displaced.y += result;
					}
					else
					{
						Vector4 result = water.GetHeightAndForcesAt(x, z, 0.0f, precision, time);

						displaced.y += result.w;
						forces.x += result.x;
						forces.y += result.y;
						forces.z += result.z;
					}
				}
				else
				{
					Vector3 result = water.WaterId != -1 ? water.GetDisplacementAt(x, z, 0.0f, precision, time) : new Vector3();            // make computations only on enabled water
					displaced += result;
				}

				finished = true;
			}
		}

		private void CompensateHorizontalDisplacement()
		{
			Vector2 offset = water.GetHorizontalDisplacementAt(x, z, 0, precision * 0.5f, time);

			x -= offset.x;
			z -= offset.y;

			if(offset.x > horizontalThreshold || offset.y > horizontalThreshold || offset.x < -horizontalThreshold || offset.y < -horizontalThreshold)
			{
				for(int i = 0; i < 14; ++i)
				{
					offset = water.GetHorizontalDisplacementAt(x, z, 0, precision * 0.5f, time);

					float dx = displaced.x - (x + offset.x);
					float dz = displaced.z - (z + offset.y);
					x += dx * weights[i];
					z += dz * weights[i];

					if(dx < horizontalThreshold && dz < horizontalThreshold && dx > -horizontalThreshold && dz > -horizontalThreshold)
						break;
				}
			}
		}

		public enum DisplacementMode
		{
			Height,
			Displacement,
			HeightAndForces
		}

		public enum ComputationsMode
		{
			Normal = 0,
			[Obsolete]
			Stabilized = 0,
			ForceCompletion = 2
		}
	}
}
