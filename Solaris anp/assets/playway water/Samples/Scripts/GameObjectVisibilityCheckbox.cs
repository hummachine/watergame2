using UnityEngine;
using UnityEngine.UI;

namespace PlayWay.WaterSamples
{
	public class GameObjectVisibilityCheckbox : MonoBehaviour
	{
		[SerializeField]
		private GameObject target;

		void Awake()
		{
			var toggle = GetComponent<Toggle>();
			toggle.onValueChanged.AddListener(OnValueChanged);
		}

		private void OnValueChanged(bool value)
		{
			target.gameObject.SetActive(value);
		}
	}
}
