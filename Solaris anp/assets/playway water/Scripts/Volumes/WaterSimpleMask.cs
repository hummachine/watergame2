using UnityEngine;

namespace PlayWay.Water
{
	/// <summary>
	/// Attach this to objects supposed to mask water in screen-space. It will mask water surface and camera's underwater image effect.
	/// </summary>
	[RequireComponent(typeof(Renderer))]
	public class WaterSimpleMask : MonoBehaviour
	{
		[SerializeField]
		private Water water;
		
		public Water Water
		{
			get { return water; }
			set
			{
				if(water == value)
					return;

				enabled = false;
				water = value;
				enabled = true;
			}
		}

		void OnEnable()
		{
			var renderer = GetComponent<Renderer>();
			renderer.material.SetFloat("_WaterId", 1 << water.WaterId);

			gameObject.layer = WaterProjectSettings.Instance.WaterTempLayer;
			
			if(renderer == null)
				throw new System.InvalidOperationException("WaterSimpleMask is attached to an object without any renderer.");
			
			water.Renderer.AddMask(renderer);
		}

		void OnDisable()
		{
			var renderer = GetComponent<Renderer>();

			if(renderer == null)
				throw new System.InvalidOperationException("WaterSimpleMask is attached to an object without any renderer.");

			water.Renderer.RemoveMask(renderer);
		}

		void OnValidate()
		{
			gameObject.layer = WaterProjectSettings.Instance.WaterTempLayer;
		}
	}
}
