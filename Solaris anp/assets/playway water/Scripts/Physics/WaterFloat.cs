using UnityEngine;

namespace PlayWay.Water
{
	public class WaterFloat : MonoBehaviour
	{
		[SerializeField]
		private Water water;

		[SerializeField]
		private float heightBonus = 0.0f;

		[Range(0.04f, 1.0f)]
		[SerializeField]
		private float precision = 0.2f;

		[SerializeField]
		private DisplacementMode displacementMode = DisplacementMode.Displacement;
		
		private WaterSample sample;

		private Vector3 initialPosition;
		private Vector3 previousPosition;

		void Start()
		{
			initialPosition = transform.position;
			previousPosition = initialPosition;

			if(water == null)
				water = FindObjectOfType<Water>();

			sample = new WaterSample(water, (WaterSample.DisplacementMode)displacementMode, precision);
		}

		void OnDisable()
		{
			sample.Stop();
		}

		void LateUpdate()
		{
			initialPosition += transform.position - previousPosition;

			Vector3 displaced = sample.GetAndReset(initialPosition.x, initialPosition.z, WaterSample.ComputationsMode.ForceCompletion);
			displaced.y += heightBonus;
            transform.position = displaced;

			previousPosition = displaced;
        }

		public enum DisplacementMode
		{
			Height,
			Displacement,
		}
	}
}
