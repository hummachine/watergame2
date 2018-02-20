using UnityEngine;
using UnityEngine.UI;

namespace PlayWay.WaterSamples
{
	public class FogCheckbox : MonoBehaviour
	{
		void Awake()
		{
			var toggle = GetComponent<Toggle>();
			toggle.onValueChanged.AddListener(OnValueChanged);
		}

		private void OnValueChanged(bool value)
		{
			RenderSettings.fog = value;
		}
	}
}
