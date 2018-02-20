using UnityEngine;

namespace PlayWay.Water
{
	public class SpritesheetAnimation : MonoBehaviour
	{
		[SerializeField]
		private int horizontal = 2;

		[SerializeField]
		private int vertical = 2;

		[SerializeField]
		private float timeStep = 0.06f;

		[SerializeField]
		private bool loop;

		[SerializeField]
		private bool destroyGo;

		private Material material;

		private float nextChangeTime;
		private int x, y;

		void Start()
		{
			var renderer = GetComponent<Renderer>();
			material = renderer.material;
			material.mainTextureScale = new Vector2(1.0f / horizontal, 1.0f / vertical);
			material.mainTextureOffset = new Vector2(0.0f, 0.0f);

			nextChangeTime = Time.time + timeStep;
		}

		void Update()
		{
			if(Time.time >= nextChangeTime)
			{
				nextChangeTime += timeStep;

				if(x == horizontal - 1 && y == vertical - 1)
				{
					if(loop)
					{
						x = 0;
						y = 0;
					}
					else
					{
						if(destroyGo)
							Destroy(gameObject);
						else
							enabled = false;

						return;
					}
				}
				else
				{
					++x;

					if(x >= horizontal)
					{
						x = 0;
						++y;
					}
				}

				material.mainTextureOffset = new Vector2(x / (float)horizontal, 1.0f - (y + 1) / (float)vertical);
			}
		}
	}
}
