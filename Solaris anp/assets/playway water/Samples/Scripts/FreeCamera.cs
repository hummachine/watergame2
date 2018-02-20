using UnityEngine;

namespace PlayWay.WaterSamples
{
	public class FreeCamera : MonoBehaviour
	{
		[SerializeField]
		private float speed = 20.0f;

		[SerializeField]
		private float mouseSensitivity = 2.0f;

		private Camera localCamera;

		void Awake()
		{
			localCamera = GetComponent<Camera>();
		}
		
		void Update()
		{
			if(Input.GetKey(KeyCode.W))
				transform.position += transform.forward * speed * Time.deltaTime;
			
			if(Input.GetKey(KeyCode.S))
				transform.position -= transform.forward * speed * Time.deltaTime;

			if(Input.GetKey(KeyCode.A))
				transform.position -= transform.right * speed * Time.deltaTime;

			if(Input.GetKey(KeyCode.D))
				transform.position += transform.right * speed * Time.deltaTime;

			if(Input.GetMouseButton(1))
			{
				transform.localEulerAngles += new Vector3(-Input.GetAxisRaw("Mouse Y") * mouseSensitivity, 0.0f, 0.0f);
				transform.localEulerAngles += new Vector3(0.0f, Input.GetAxisRaw("Mouse X") * mouseSensitivity, 0.0f);
			}

			localCamera.farClipPlane = Mathf.Max(4000.0f, 2000.0f + transform.position.y * 40);
			localCamera.nearClipPlane = localCamera.farClipPlane * (1.0f / 4000.0f);
        }
	}
}
