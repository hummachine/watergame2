using UnityEngine;

namespace PlayWay.WaterSamples
{
	/// <summary>
	/// Simulates forces applied by a sail.
	/// </summary>
	public class Sail : MonoBehaviour
	{
		[SerializeField]
		private WindZone wind;

		[SerializeField]
		private float area = 100.0f;

		private Rigidbody rigidBody;

		void Awake()
		{
			rigidBody = GetComponentInParent<Rigidbody>();
		}

		void FixedUpdate()
		{
			float force = Vector3.Dot(transform.forward, wind.transform.forward);

			if(force >= 0.0f)
				force = 2.0f - force;

			if(force < 0.0f)
				force = 2.0f + force * 2.0f;

			rigidBody.AddForce(transform.forward * (wind.windMain * force * area), ForceMode.Force);
		}
	}
}
