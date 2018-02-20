using UnityEngine;
using System.Collections;

public class SwitchView :  MonoBehaviour {
	[SerializeField]
	Camera firstPCamera;
	[SerializeField]
	Camera thirdPCamera;
	[SerializeField]
	Camera backCamera;
	private bool switchCam = false;
	private bool backCam = false;
	// Use this for initialization
	void Start ()
	{
	 	firstPCamera.GetComponent<Camera>().enabled = false;
	 	thirdPCamera.GetComponent<Camera>().enabled = true;
	 	backCamera.GetComponent<Camera>().enabled = false;
	 	

		StartCoroutine(SwitchCamera());
	 }
	 	
	IEnumerator SwitchCamera(){
		while (true) {
			firstPCamera.GetComponent<Camera>().enabled = true;
			thirdPCamera.GetComponent<Camera>().enabled = false;
			backCamera.GetComponent<Camera>().enabled = false;

			yield return new WaitForSeconds(10);

			firstPCamera.GetComponent<Camera>().enabled = false;
			thirdPCamera.GetComponent<Camera>().enabled = true;
			backCamera.GetComponent<Camera>().enabled = false;

			yield return new WaitForSeconds(10);

			firstPCamera.GetComponent<Camera>().enabled = false;
			thirdPCamera.GetComponent<Camera>().enabled = false;
			backCamera.GetComponent<Camera>().enabled = true;

			yield return new WaitForSeconds(10);
		}

	}
	 
}
			
			