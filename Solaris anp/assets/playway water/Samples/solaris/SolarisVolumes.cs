using UnityEngine;

namespace PlayWay.WaterSamples
{
	public class SolarisVolumesSample : MonoBehaviour
	{
		[SerializeField]
		private Transform water;
		
		void Update()
		{
			if(Time.frameCount >= 2)
				water.gameObject.SetActive(true);

		}
	}
}
