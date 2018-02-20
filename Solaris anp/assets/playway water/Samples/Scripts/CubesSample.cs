using UnityEngine;
using UnityEngine.UI;

namespace PlayWay.WaterSamples
{
	public class CubesSample : MonoBehaviour
	{
		[SerializeField]
		private GameObject prefab;

		[SerializeField]
		private Text cubesCountLabel;

		[SerializeField]
		private Text fpsLabel;

		private int cubesCount = 1;
		private float nextFpsUpdate;
		private int lastFrameCount;
		private float lastTime;

		private float nextCubeSpawnTime;

		void Update()
		{
			if(Input.GetKey(KeyCode.Space) && Time.time >= nextCubeSpawnTime)
			{
				var cubeGo = Instantiate(prefab);
				cubeGo.transform.SetParent(transform);
				cubeGo.transform.localPosition = new Vector3(Random.Range(-5.0f, 5.0f), 0.0f, Random.Range(-5.0f, 5.0f));

				var rigidBody = cubeGo.GetComponent<Rigidbody>();
				rigidBody.AddForce(Random.Range(-15.0f, 15.0f), Random.Range(-25.0f, 15.0f), Random.Range(-15.0f, 15.0f), ForceMode.Impulse);
				rigidBody.AddTorque(Random.Range(-15.0f, 15.0f), Random.Range(-15.0f, 15.0f), Random.Range(-15.0f, 15.0f));

				++cubesCount;
				cubesCountLabel.text = "Cubes: " + cubesCount;

				nextCubeSpawnTime = Time.time + 0.05f;
			}

			if(Input.GetKey(KeyCode.A))
			{
				Camera.main.transform.RotateAround(transform.position, Vector3.up, Time.deltaTime * 20.0f);
			}

			if(Input.GetKey(KeyCode.D))
			{
				Camera.main.transform.RotateAround(transform.position, Vector3.up, -Time.deltaTime * 20.0f);
			}

			if(Time.time >= nextFpsUpdate)
			{
				int frameCount = Time.frameCount;

				fpsLabel.text = "FPS: " + ((frameCount - lastFrameCount) / (Time.time - lastTime)).ToString("0.0");
				nextFpsUpdate = Time.time + 0.25f;

				lastFrameCount = frameCount;
				lastTime = Time.time;
			}
		}
	}
}
