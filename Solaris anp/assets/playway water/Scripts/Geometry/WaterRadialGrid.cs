using System.Collections.Generic;
using UnityEngine;

namespace PlayWay.Water
{
	[System.Serializable]
	public class WaterRadialGrid : WaterPrimitiveBase
	{
		protected override Mesh[] CreateMeshes(int vertexCount, bool volume)
		{
			int dim = Mathf.RoundToInt(Mathf.Sqrt(vertexCount));
			int verticesX = Mathf.RoundToInt(dim * 0.78f);
			int totalVerticesY = Mathf.RoundToInt((float)vertexCount / verticesX);
			int verticesY = totalVerticesY;

			if(volume) verticesY >>= 1;

			var meshes = new List<Mesh>();
			var vertices = new List<Vector3>();
			var indices = new List<int>();
			int vertexIndex = 0;
			int meshIndex = 0;

			var vectors = new Vector2[verticesX];

			for(int x = 0; x < verticesX; ++x)
			{
				float fx = (float)x / (verticesX - 1) * 2.0f - 1.0f;
				fx *= Mathf.PI * 0.25f;

				vectors[x] = new Vector2(
						Mathf.Sin(fx),
						Mathf.Cos(fx)
					).normalized;
			}

			for(int y = 0; y < verticesY; ++y)
			{
				float fy = (float)y / (totalVerticesY - 1);
				fy = 1.0f - Mathf.Cos(fy * Mathf.PI * 0.5f);

				for(int x = 0; x < verticesX; ++x)
				{
					Vector2 vector = vectors[x] * fy;

					if(y < verticesY - 2 || !volume)
						vertices.Add(new Vector3(vector.x, 0.0f, vector.y));
					else if(y == verticesY - 2)
						vertices.Add(new Vector3(vector.x * 10.0f, -0.9f, vector.y) * 0.5f);
					else
						vertices.Add(new Vector3(vector.x * 10.0f, -0.9f, vector.y * -10.0f) * 0.5f);

					if(x != 0 && y != 0 && vertexIndex > verticesX)
					{
						indices.Add(vertexIndex);
						indices.Add(vertexIndex - verticesX);
						indices.Add(vertexIndex - verticesX - 1);
						indices.Add(vertexIndex - 1);
					}

					++vertexIndex;

					if(vertexIndex == 65000)
					{
						var mesh = CreateMesh(vertices.ToArray(), indices.ToArray(), string.Format("Radial Grid {0}x{1} - {2}", verticesX, totalVerticesY, meshIndex.ToString("00")));
						meshes.Add(mesh);

						--x; --y;

						fy = (float)y / (totalVerticesY - 1);
						fy = 1.0f - Mathf.Cos(fy * Mathf.PI * 0.5f);

						vertexIndex = 0;
						vertices.Clear();
						indices.Clear();

						++meshIndex;
					}
				}
			}

			if(vertexIndex != 0)
			{
				var mesh = CreateMesh(vertices.ToArray(), indices.ToArray(), string.Format("Radial Grid {0}x{1} - {2}", verticesX, totalVerticesY, meshIndex.ToString("00")));
				meshes.Add(mesh);
			}

			return meshes.ToArray();
		}

		protected override Matrix4x4 GetMatrix(Camera camera)
		{
			Vector3 down = WaterUtilities.ViewportWaterPerpendicular(camera);
			Vector3 right = WaterUtilities.ViewportWaterRight(camera);

			float waterPositionY = water.transform.position.y;
			Vector3 ld = WaterUtilities.RaycastPlane(camera, waterPositionY, (down - right));
			Vector3 rd = WaterUtilities.RaycastPlane(camera, waterPositionY, (down + right));

			float farClipPlane = camera.farClipPlane;
			float fieldOfViewTan = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
			Vector3 position = camera.transform.position;
			Vector3 scale = camera.orthographic ? new Vector3(farClipPlane, farClipPlane, farClipPlane) : new Vector3(farClipPlane * fieldOfViewTan * camera.aspect + water.MaxHorizontalDisplacement * 2, farClipPlane, farClipPlane);

			float width = rd.x - ld.x;

			if(width < 0.0f)
				width = -width;

			float offset = (ld.z < rd.z ? ld.z : rd.z) - (width + water.MaxHorizontalDisplacement * 2.0f) * scale.z / scale.x;

			if(camera.orthographic)
				offset -= camera.orthographicSize * 3.2f;

			Vector3 backward;

			float dp = camera.transform.forward.y;             // Vector3.Dot(Vector3.down, camera.transform.forward)
			if(dp < -0.98f || dp > 0.98f)
			{
				backward = -camera.transform.up;
				float len = Mathf.Sqrt(backward.x * backward.x + backward.z * backward.z);
				backward.x /= len;
				backward.z /= len;

				if(!camera.orthographic)
					offset = -position.y * 4.0f * fieldOfViewTan;
			}
			else
			{
				backward = camera.transform.forward;
				float len = Mathf.Sqrt(backward.x * backward.x + backward.z * backward.z);
				backward.x /= len;
				backward.z /= len;
			}

			if(!camera.orthographic)
				scale.z -= offset;

			return Matrix4x4.TRS(
				new Vector3(position.x + backward.x * offset, waterPositionY, position.z + backward.z * offset),
				Quaternion.AngleAxis(Mathf.Atan2(backward.x, backward.z) * Mathf.Rad2Deg, Vector3.up),
				scale
			);
		}
	}
}
