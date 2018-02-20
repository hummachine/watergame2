using UnityEngine;
using UnityEngine.Networking;

namespace PlayWay.Water
{
	[AddComponentMenu("Water/Network Synchronization", 2)]
	public class NetworkWater : NetworkBehaviour
	{
		[SyncVar]
		private float time;

		private Water water;

		void Start()
		{
			water = GetComponent<Water>();

			if(water == null)
			{
				enabled = false;
				return;
			}
		}

		void Update()
		{
			if(isServer)
				time = Time.time;
			else
				time += Time.deltaTime;
			
			water.Time = time;
        }
	}
}
