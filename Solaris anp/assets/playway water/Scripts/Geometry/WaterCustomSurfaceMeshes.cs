using System.Collections.Generic;
using UnityEngine;

namespace PlayWay.Water
{
	[System.Serializable]
	public class WaterCustomSurfaceMeshes
	{
		[SerializeField]
		private Mesh[] customMeshes;

		private Water water;
		private Mesh[] usedMeshes;
		private Mesh[] volumeMeshes;

		virtual internal void OnEnable(Water water)
		{
			this.water = water;
		}

		virtual internal void OnDisable()
		{
			Dispose();
		}

		public Mesh[] Meshes
		{
			get { return customMeshes; }
			set
			{
				customMeshes = value;
				usedMeshes = null;
				volumeMeshes = null;
			}
		}

		private Mesh[] UsedMeshes
		{
			get
			{
				if(usedMeshes == null)
				{
					var list = new List<Mesh>();

					foreach(var mesh in customMeshes)
					{
						if(mesh != null)
							list.Add(mesh);
					}

					usedMeshes = list.ToArray();
				}

				return usedMeshes;
			}
		}

		/// <summary>
		/// Retrieves auto-generated boundary meshes used for underwater detection etc.
		/// </summary>
		public Mesh[] VolumeMeshes
		{
			get
			{
				if(volumeMeshes == null)
				{
					var usedMeshes = UsedMeshes;
					var list = new List<Mesh>();

					foreach(var mesh in usedMeshes)
					{
						list.Add(mesh);
						list.Add(CreateBoundaryMesh(mesh));
					}

					volumeMeshes = list.ToArray();
				}

				return volumeMeshes;
			}
		}

		public bool Triangular
		{
			get { return customMeshes == null || UsedMeshes.Length == 0 || UsedMeshes[0].GetTopology(0) == MeshTopology.Triangles; }
		}

		public Mesh[] GetTransformedMeshes(Camera camera, out Matrix4x4 matrix, bool volume)
		{
			matrix = water.transform.localToWorldMatrix;

			if(volume)
				return VolumeMeshes;
			else
				return UsedMeshes;
		}

		public void Dispose()
		{
			if(volumeMeshes != null)
			{
				for(int i = 1; i < volumeMeshes.Length; i += 2)
					volumeMeshes[i].Destroy();

				volumeMeshes = null;
			}

			usedMeshes = null;
		}

		private Mesh CreateBoundaryMesh(Mesh sourceMesh)
		{
			var volumeMesh = new Mesh();
			volumeMesh.hideFlags = HideFlags.DontSave;
			
			Vector3[] sourceVertices = sourceMesh.vertices;

			List<Vector3> targetVertices = new List<Vector3>();
			List<int> targetIndices = new List<int>();

			var edges = BuildManifoldEdges(sourceMesh);

			Vector3 center = new Vector3();
			int centerIndex = edges.Length * 4;
				
			for(int i=0; i<edges.Length; ++i)
			{
				int vertexIndex = targetVertices.Count;

				Vector3 vertexA = sourceVertices[edges[i].vertexIndex0];
				Vector3 vertexB = sourceVertices[edges[i].vertexIndex1];

				targetVertices.Add(vertexA);
				targetVertices.Add(vertexB);

				// TODO: fix this by using vertex shader snapping to far plane and some big values here
				vertexA.y -= 1000.0f;
				vertexB.y -= 1000.0f;

				targetVertices.Add(vertexA);
				targetVertices.Add(vertexB);

				targetIndices.Add(vertexIndex + 3);
				targetIndices.Add(vertexIndex + 2);
				targetIndices.Add(vertexIndex);

				targetIndices.Add(vertexIndex + 3);
				targetIndices.Add(vertexIndex);
				targetIndices.Add(vertexIndex + 1);

				targetIndices.Add(vertexIndex + 3);
				targetIndices.Add(vertexIndex + 2);
				targetIndices.Add(centerIndex);

				center += vertexA;
				center += vertexB;
            }

			center /= (targetVertices.Count / 2);

			targetVertices.Add(center);

			volumeMesh.vertices = targetVertices.ToArray();
			volumeMesh.SetIndices(targetIndices.ToArray(), MeshTopology.Triangles, 0);

			return volumeMesh;
		}

		/// Source: Unity Technologies "Procedural Examples" from the Asset Store
		/// Builds an array of edges that connect to only one triangle.
		/// In other words, the outline of the mesh	
		private static Edge[] BuildManifoldEdges(Mesh mesh)
		{
			// Build a edge list for all unique edges in the mesh
			Edge[] edges = BuildEdges(mesh.vertexCount, mesh.triangles);

			// We only want edges that connect to a single triangle
			List<Edge> culledEdges = new List<Edge>();
			foreach(Edge edge in edges)
			{
				if(edge.faceIndex0 == edge.faceIndex1)
				{
					culledEdges.Add(edge);
				}
			}

			return culledEdges.ToArray();
		}

		/// Source: Unity Technologies "Procedural Examples" from the Asset Store
		/// Builds an array of unique edges
		/// This requires that your mesh has all vertices welded. However on import, Unity has to split
		/// vertices at uv seams and normal seams. Thus for a mesh with seams in your mesh you
		/// will get two edges adjoining one triangle.
		/// Often this is not a problem but you can fix it by welding vertices 
		/// and passing in the triangle array of the welded vertices.
		private static Edge[] BuildEdges(int vertexCount, int[] triangleArray)
		{
			int maxEdgeCount = triangleArray.Length;
			int[] firstEdge = new int[vertexCount + maxEdgeCount];
			int nextEdge = vertexCount;
			int triangleCount = triangleArray.Length / 3;

			for(int a = 0; a < vertexCount; a++)
				firstEdge[a] = -1;

			// First pass over all triangles. This finds all the edges satisfying the
			// condition that the first vertex index is less than the second vertex index
			// when the direction from the first vertex to the second vertex represents
			// a counterclockwise winding around the triangle to which the edge belongs.
			// For each edge found, the edge index is stored in a linked list of edges
			// belonging to the lower-numbered vertex index i. This allows us to quickly
			// find an edge in the second pass whose higher-numbered vertex index is i.
			Edge[] edgeArray = new Edge[maxEdgeCount];

			int edgeCount = 0;
			for(int a = 0; a < triangleCount; a++)
			{
				int i1 = triangleArray[a * 3 + 2];
				for(int b = 0; b < 3; b++)
				{
					int i2 = triangleArray[a * 3 + b];
					if(i1 < i2)
					{
						Edge newEdge = new Edge();
						newEdge.vertexIndex0 = i1;
						newEdge.vertexIndex1 = i2;
						newEdge.faceIndex0 = a;
						newEdge.faceIndex1 = a;
						edgeArray[edgeCount] = newEdge;

						int edgeIndex = firstEdge[i1];
						if(edgeIndex == -1)
						{
							firstEdge[i1] = edgeCount;
						}
						else
						{
							while(true)
							{
								int index = firstEdge[nextEdge + edgeIndex];
								if(index == -1)
								{
									firstEdge[nextEdge + edgeIndex] = edgeCount;
									break;
								}

								edgeIndex = index;
							}
						}

						firstEdge[nextEdge + edgeCount] = -1;
						edgeCount++;
					}

					i1 = i2;
				}
			}

			// Second pass over all triangles. This finds all the edges satisfying the
			// condition that the first vertex index is greater than the second vertex index
			// when the direction from the first vertex to the second vertex represents
			// a counterclockwise winding around the triangle to which the edge belongs.
			// For each of these edges, the same edge should have already been found in
			// the first pass for a different triangle. Of course we might have edges with only one triangle
			// in that case we just add the edge here
			// So we search the list of edges
			// for the higher-numbered vertex index for the matching edge and fill in the
			// second triangle index. The maximum number of comparisons in this search for
			// any vertex is the number of edges having that vertex as an endpoint.

			for(int a = 0; a < triangleCount; a++)
			{
				int i1 = triangleArray[a * 3 + 2];
				for(int b = 0; b < 3; b++)
				{
					int i2 = triangleArray[a * 3 + b];
					if(i1 > i2)
					{
						bool foundEdge = false;
						for(int edgeIndex = firstEdge[i2]; edgeIndex != -1; edgeIndex = firstEdge[nextEdge + edgeIndex])
						{
							Edge edge = edgeArray[edgeIndex];
							if((edge.vertexIndex1 == i1) && (edge.faceIndex0 == edge.faceIndex1))
							{
								edgeArray[edgeIndex].faceIndex1 = a;
								foundEdge = true;
								break;
							}
						}

						if(!foundEdge)
						{
							Edge newEdge = new Edge();
							newEdge.vertexIndex0 = i1;
							newEdge.vertexIndex1 = i2;
							newEdge.faceIndex0 = a;
							newEdge.faceIndex1 = a;
							edgeArray[edgeCount] = newEdge;
							edgeCount++;
						}
					}

					i1 = i2;
				}
			}

			Edge[] compactedEdges = new Edge[edgeCount];
			for(int e = 0; e < edgeCount; e++)
				compactedEdges[e] = edgeArray[e];

			return compactedEdges;
		}

		struct Edge
		{
			public int vertexIndex0;
			public int vertexIndex1;

			public int faceIndex0;
			public int faceIndex1;
		}
	}
}
