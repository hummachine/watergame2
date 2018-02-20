using PlayWay.Water;
using UnityEngine;
using UnityEngine.UI;

namespace PlayWay.WaterSamples
{
	public class QualityLevelDropdown : MonoBehaviour
	{
#if !UNITY_5_0 && !UNITY_5_1
		void Awake()
		{
			var dropdown = GetComponent<Dropdown>();
			dropdown.value = WaterQualitySettings.Instance.GetQualityLevel();
			dropdown.onValueChanged.AddListener(OnValueChanged);
		}

		private void OnValueChanged(int index)
		{
			WaterQualitySettings.Instance.SetQualityLevel(index);
		}
#endif
	}
}
