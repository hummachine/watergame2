using System.Collections.Generic;
using UnityEngine;

namespace PlayWay.Water
{
	[System.Serializable]
	[ExecuteInEditMode]
	public class WaterProjectionGrid : WaterPrimitiveBase
	{
		private float previousVerticesPerPixel;
		
		override internal void OnEnable(Water water)
		{
			base.OnEnable(water);
			this.water = water;
		}

		override internal void OnDisable()
		{
			base.OnDisable();
		}

		override internal void AddToMaterial(Water water)
		{
			water.SetKeyword("_PROJECTION_GRID", true);
		}

		override internal void RemoveFromMaterial(Water water)
		{
			water.SetKeyword("_PROJECTION_GRID", false);
		}

		override public Mesh[] GetTransformedMeshes(Camera camera, out Matrix4x4 matrix, int vertexCount, bool volume)
		{
			int pixelWidth = camera.pixelWidth;
			int pixelHeight = camera.pixelHeight;

			CachedMeshSet cachedMeshSet;
			int hash = pixelHeight | (pixelWidth << 16);
			Vector3 cameraPosition = camera.transform.position;
			matrix = Matrix4x4.identity;
			matrix.m03 = cameraPosition.x;
			matrix.m13 = 0.0f;
			matrix.m23 = cameraPosition.z;

			float verticesPerPixel = (float)vertexCount / (pixelWidth * pixelHeight);

			water.WaterMaterial.SetMatrix("_InvViewMatrix", camera.cameraToWorldMatrix);
			water.WaterBackMaterial.SetMatrix("_InvViewMatrix", camera.cameraToWorldMatrix);

			if(!cache.TryGetValue(hash, out cachedMeshSet))
				cache[hash] = cachedMeshSet = new CachedMeshSet(CreateMeshes(Mathf.RoundToInt(pixelWidth * verticesPerPixel), Mathf.RoundToInt(pixelHeight * verticesPerPixel)));

			return cachedMeshSet.meshes;
		}
		
		override protected Mesh[] CreateMeshes(int vertexCount, bool volume)
		{
			throw new System.InvalidOperationException();
		}

		private Mesh[] CreateMeshes(int verticesX, int verticesY)
		{
			List<Mesh> meshes = new List<Mesh>();
			
			List<Vector3> vertices = new List<Vector3>();
			List<int> indices = new List<int>();
			int vertexIndex = 0;
			int meshIndex = 0;

			for(int y = 0; y < verticesY; ++y)
			{
				float fy = (float)y / (verticesY - 1);

				for(int x = 0; x < verticesX; ++x)
				{
					float fx = (float)x / (verticesX - 1);

					vertices.Add(new Vector3(fx, fy, 0.0f));

					if(x != 0 && y != 0 && vertexIndex > verticesX)
					{
						indices.Add(vertexIndex);
						indices.Add(vertexIndex - 1);
						indices.Add(vertexIndex - verticesX - 1);
						indices.Add(vertexIndex - verticesX);
					}

					++vertexIndex;

					if(vertexIndex == 65000)
					{
						var mesh = CreateMesh(vertices.ToArray(), indices.ToArray(), string.Format("Projection Grid {0}x{1} - {2}", verticesX, verticesY, meshIndex.ToString("00")), false);
						mesh.bounds = new Bounds(Vector3.zero, new Vector3(100000.0f, 0.2f, 100000.0f));
						meshes.Add(mesh);

						--x; --y;

						fy = (float)y / (verticesY - 1);

						vertexIndex = 0;
						vertices.Clear();
                        indices.Clear();

						++meshIndex;
					}
				}
			}

			if(vertexIndex != 0)
			{
				var mesh = CreateMesh(vertices.ToArray(), indices.ToArray(), string.Format("Projection Grid {0}x{1} - {2}", verticesX, verticesY, meshIndex.ToString("00")), false);
				mesh.bounds = new Bounds(Vector3.zero, new Vector3(100000.0f, 0.2f, 100000.0f));
				meshes.Add(mesh);
			}

			return meshes.ToArray();
		}

		protected override Matrix4x4 GetMatrix(Camera camera)
		{
			throw new System.NotImplementedException();
		}
	}
}
