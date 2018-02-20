using UnityEngine;

namespace PlayWay.Water
{
	static public class WaterUtilities
	{
		static public Vector3 RaycastPlane(Camera camera, float planeHeight, Vector3 pos)
		{
			var ray = camera.ViewportPointToRay(pos);
			if(camera.transform.position.y > planeHeight)
			{
				if(ray.direction.y > -0.01f)
					ray.direction = new Vector3(ray.direction.x, -ray.direction.y - 0.02f, ray.direction.z);
			}
			else if(ray.direction.y < 0.01f)
				ray.direction = new Vector3(ray.direction.x, -ray.direction.y + 0.02f, ray.direction.z);

			float t = -(ray.origin.y - planeHeight) / ray.direction.y;
			Vector3 ws = ray.direction * t;

			return Quaternion.AngleAxis(-camera.transform.eulerAngles.y, Vector3.up) * ws;
		}

		static public Vector3 ViewportWaterPerpendicular(Camera camera)
		{
			Vector3 down = camera.worldToCameraMatrix.MultiplyVector(new Vector3(0.0f, -1.0f, 0.0f));
			down.z = 0.0f;
			down.Normalize();
			down *= 0.5f;
			down.x += 0.5f;
			down.y += 0.5f;

			return down;
		}

		static public Vector3 ViewportWaterRight(Camera camera)
		{
			Vector3 right = camera.worldToCameraMatrix.MultiplyVector(Vector3.Cross(camera.transform.forward, new Vector3(0.0f, -1.0f, 0.0f)));
			right.z = 0.0f;
			right.Normalize();
			right *= 0.5f;

			return right;
		}

		static public void Destroy(this Object obj)
		{
#if !UNITY_EDITOR
			Object.Destroy(obj);
#else
			if(Application.isPlaying)
				Object.Destroy(obj);
			else
				Object.DestroyImmediate(obj);
#endif
		}
	}
}
