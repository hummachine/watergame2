using System.Collections.Generic;
using UnityEngine;

namespace PlayWay.Water
{
	[System.Serializable]
	abstract public class WaterPrimitiveBase
	{
		protected Water water;
		protected Dictionary<int, CachedMeshSet> cache = new Dictionary<int, CachedMeshSet>(Int32EqualityComparer.Default);
		private List<int> keysToRemove;

		public void Dispose()
		{
			foreach(var cachedMeshSet in cache.Values)
			{
				foreach(var mesh in cachedMeshSet.meshes)
				{
					if(Application.isPlaying)
						Object.Destroy(mesh);
					else
						Object.DestroyImmediate(mesh);
				}
			}

			cache.Clear();
		}

		virtual internal void OnEnable(Water water)
		{
			this.water = water;
        }

		virtual internal void OnDisable()
		{
			Dispose();
		}

		virtual internal void AddToMaterial(Water water)
		{
			
		}

		virtual internal void RemoveFromMaterial(Water water)
		{
			
		}

		virtual public Mesh[] GetTransformedMeshes(Camera camera, out Matrix4x4 matrix, int vertexCount, bool volume)
		{
			if(camera != null)
				matrix = GetMatrix(camera);
			else
				matrix = Matrix4x4.identity;

			CachedMeshSet cachedMeshSet;
			int hash = vertexCount;

			if(volume) hash = -hash;

			if(!cache.TryGetValue(hash, out cachedMeshSet))
				cache[hash] = cachedMeshSet = new CachedMeshSet(CreateMeshes(vertexCount, volume));
			else
				cachedMeshSet.Update();

			return cachedMeshSet.meshes;
		}

		internal void Update()
		{
			int currentFrame = Time.frameCount;

			if(keysToRemove == null)
				keysToRemove = new List<int>();

			var enumerator = cache.GetEnumerator();
			while(enumerator.MoveNext())
			{
				var kv = enumerator.Current;

                if(currentFrame - kv.Value.lastFrameUsed > 27)			// waterprimitivebase updates run every 9 frame
				{
					keysToRemove.Add(kv.Key);

					foreach(var mesh in kv.Value.meshes)
					{
						if(Application.isPlaying)
							Object.Destroy(mesh);
						else
							Object.DestroyImmediate(mesh);
					}
				}
			}

			for(int i=0; i<keysToRemove.Count; ++i)
				cache.Remove(keysToRemove[i]);

			keysToRemove.Clear();
		}

		abstract protected Matrix4x4 GetMatrix(Camera camera);
		abstract protected Mesh[] CreateMeshes(int vertexCount, bool volume);

		protected Mesh CreateMesh(Vector3[] vertices, int[] indices, string name, bool triangular = false)
		{
			var mesh = new Mesh();
			mesh.hideFlags = HideFlags.DontSave;
			mesh.name = name;
			mesh.vertices = vertices;
			mesh.SetIndices(indices, triangular ? MeshTopology.Triangles : MeshTopology.Quads, 0);
			mesh.RecalculateBounds();
			mesh.UploadMeshData(true);

			return mesh;
		}

		protected class CachedMeshSet
		{
			public Mesh[] meshes;
			public int lastFrameUsed;

			public CachedMeshSet(Mesh[] meshes)
			{
				this.meshes = meshes;

				Update();
			}

			public void Update()
			{
				lastFrameUsed = Time.frameCount;
			}
		}
	}
}
