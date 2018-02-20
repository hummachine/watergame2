using System.Diagnostics;
using UnityEngine;

namespace PlayWay.Water
{
	sealed public class WaveParticlesQuadtree : Quadtree<WaveParticle>
	{
		private Mesh mesh;
		private Vector3[] vertices;
		private Vector4[] tangentsPack;
		private WaveParticlesGroup[] particleGroups;
		private int numParticleGroups;
		private int lastGroupIndex = -1;
		private float stress = 1.0f;
		private bool tangentsPackChanged;
		private Stopwatch stopwatch;

		private int lastUpdateIndex;

		private WaveParticlesQuadtree a, b, c, d;
		private WaveParticlesQuadtree root;

		public WaveParticlesQuadtree(Rect rect, int maxElementsPerNode, int maxTotalElements) : base(rect, maxElementsPerNode, maxTotalElements)
		{
			root = this;
			particleGroups = new WaveParticlesGroup[maxElementsPerNode >> 3];
			CreateMesh();
        }

		private WaveParticlesQuadtree(WaveParticlesQuadtree root, Rect rect, int maxElementsPerNode) : this(rect, maxElementsPerNode, 0)
		{
			this.root = root;
		}

		public void Render(Rect renderRect)
		{
			if(!rect.Overlaps(renderRect))
				return;

			if(a != null)
			{
				a.Render(renderRect);
				b.Render(renderRect);
				c.Render(renderRect);
				d.Render(renderRect);
			}
			else if(numElements != 0)
			{
				mesh.vertices = vertices;

				if(tangentsPackChanged)
				{
					mesh.tangents = tangentsPack;
					tangentsPackChanged = false;
				}

				Graphics.DrawMeshNow(mesh, Matrix4x4.identity, 0);
			}
		}

		public void UpdateSimulation(float time, float maxExecutionTimeExp)
		{
			if(stopwatch == null)
				stopwatch = new Stopwatch();

			stopwatch.Reset();
			stopwatch.Start();

			UpdateSimulation(time);

			float duration = stopwatch.ElapsedTicks / 10000.0f;

			if(duration > 50.0f)
				duration = 50.0f;

			stress = stress * 0.98f + (Mathf.Exp(duration) - maxExecutionTimeExp) * 0.04f;            // execution time of more than 0.5 milisecond adds to stress

			if(stress < 1.0f) stress = 1.0f;
			if(!(stress < 20.0f)) stress = 20.0f;			// handles NaNs
		}

		public void UpdateSimulation(float time)
		{
			if(a != null)
			{
				// update sub-nodes
				a.UpdateSimulation(time);
				b.UpdateSimulation(time);
				c.UpdateSimulation(time);
				d.UpdateSimulation(time);
			}
			else if(numElements != 0)
			{
				// update local particles
				UpdateParticles(time);
            }
		}

		private void UpdateParticles(float time)
		{
			var enabledWaterCameras = WaterCamera.EnabledWaterCameras;
			int numEnabledWaterCameras = enabledWaterCameras.Count;
			bool isVisible = false;

			for(int i = 0; i < numEnabledWaterCameras; ++i)
			{
				if(rect.Overlaps(enabledWaterCameras[i].LocalMapsRect))
				{
					isVisible = true;
					break;
				}
			}

			int startIndex, endIndex, vertexIndex;

			if(!isVisible)
			{
				startIndex = lastUpdateIndex;
				endIndex = lastUpdateIndex + 8;
				vertexIndex = startIndex << 2;

				if(endIndex >= elements.Length)
				{
					endIndex = elements.Length;
					lastUpdateIndex = 0;
				}
				else
					lastUpdateIndex = endIndex;
			}
			else
			{
				startIndex = 0;
				endIndex = elements.Length;
				vertexIndex = 0;
			}

			WaveParticlesQuadtree rootQuadtree = isVisible ? root : null;
			float updateDelay = isVisible ? 0.01f : 1.5f;
			float costlyUpdateDelay = isVisible ? 0.4f : 8.0f;
			bool didCostlyUpdate = false;

			updateDelay *= root.stress;
			costlyUpdateDelay *= root.stress;

			for(int i = 0; particleGroups != null && i < particleGroups.Length; ++i)
			{
				var group = particleGroups[i];

				if(group != null)
				{
					if(!group.leftParticle.isAlive)
					{
						--numParticleGroups;
						particleGroups[i] = null;
						continue;
					}

					if(time >= group.lastUpdateTime + updateDelay)
					{
						if(time >= group.lastCostlyUpdateTime + costlyUpdateDelay && !didCostlyUpdate)
						{
							if(!RectContainsParticleGroup(group))
							{
								--numParticleGroups;
								particleGroups[i] = null;
								continue;
							}

							group.CostlyUpdate(rootQuadtree, time);
							didCostlyUpdate = true;
                        }

						group.Update(time);
					}
				}
			}

			if(elements != null)
			{
				for(int i = startIndex; i < endIndex; ++i)
				{
					var particle = elements[i];

					if(particle != null)
					{
						if(particle.isAlive)
						{
							if(marginRect.Contains(particle.position))
							{
								var vertexData = particle.VertexData;
								var particleData = particle.PackedParticleData;
								vertices[vertexIndex] = vertexData;
								tangentsPack[vertexIndex++] = particleData;
								vertices[vertexIndex] = vertexData;
								tangentsPack[vertexIndex++] = particleData;
								vertices[vertexIndex] = vertexData;
								tangentsPack[vertexIndex++] = particleData;
								vertices[vertexIndex] = vertexData;
								tangentsPack[vertexIndex++] = particleData;
								tangentsPackChanged = true;
							}
							else
							{
								// re-add particle
								base.RemoveElementAt(i);
								
								vertices[vertexIndex++].x = float.NaN;
								vertices[vertexIndex++].x = float.NaN;
								vertices[vertexIndex++].x = float.NaN;
								vertices[vertexIndex++].x = float.NaN;
								root.AddElement(particle);
							}
						}
						else
						{
							// remove particle
							base.RemoveElementAt(i);
							
							vertices[vertexIndex++].x = float.NaN;
							vertices[vertexIndex++].x = float.NaN;
							vertices[vertexIndex++].x = float.NaN;
							vertices[vertexIndex++].x = float.NaN;
							particle.AddToCache();
						}
					}
					else
						vertexIndex += 4;
				}
			}
		}

		public override void Destroy()
		{
			base.Destroy();

			if(mesh != null)
			{
				mesh.Destroy();
				mesh = null;
			}

			vertices = null;
			tangentsPack = null;
		}

		private bool HasParticleGroup(WaveParticlesGroup group)
		{
			for(int i = 0; i < particleGroups.Length; ++i)
			{
				if(particleGroups[i] == group)
					return true;
			}

			return false;
		}

		private void AddParticleGroup(WaveParticlesGroup group)
		{
			if(particleGroups.Length == numParticleGroups)
				System.Array.Resize(ref particleGroups, numParticleGroups << 1);

			for(++lastGroupIndex; lastGroupIndex < particleGroups.Length; ++lastGroupIndex)
			{
				if(particleGroups[lastGroupIndex] == null)
				{
					++numParticleGroups;
					particleGroups[lastGroupIndex] = group;
					return;
				}
			}

			for(lastGroupIndex = 0; lastGroupIndex < particleGroups.Length; ++lastGroupIndex)
			{
				if(particleGroups[lastGroupIndex] == null)
				{
					++numParticleGroups;
					particleGroups[lastGroupIndex] = group;
					return;
				}
			}
		}
		
		private bool RectContainsParticleGroup(WaveParticlesGroup group)
		{
			var particle = group.leftParticle;

			if(!particle.isAlive)
				return false;

			do
			{
				if(marginRect.Contains(particle.position))
					return true;

				particle = particle.rightNeighbour;
			}
			while(particle != null);

			return false;
		}

		protected override void AddElementAt(WaveParticle particle, int index)
		{
			base.AddElementAt(particle, index);

			if(!HasParticleGroup(particle.group))
				AddParticleGroup(particle.group);
		}

		protected override void RemoveElementAt(int index)
		{
			base.RemoveElementAt(index);

			int vertexIndex = index << 2;
			vertices[vertexIndex++].x = float.NaN;
			vertices[vertexIndex++].x = float.NaN;
			vertices[vertexIndex++].x = float.NaN;
			vertices[vertexIndex].x = float.NaN;
        }

		protected override void SpawnChildNodes()
		{
			mesh.Destroy();
			mesh = null;

			float halfWidth = rect.width * 0.5f;
			float halfHeight = rect.height * 0.5f;
			base.a = a = new WaveParticlesQuadtree(root, new Rect(rect.xMin, center.y, halfWidth, halfHeight), elements.Length);
			base.b = b = new WaveParticlesQuadtree(root, new Rect(center.x, center.y, halfWidth, halfHeight), elements.Length);
			base.c = c = new WaveParticlesQuadtree(root, new Rect(rect.xMin, rect.yMin, halfWidth, halfHeight), elements.Length);
			base.d = d = new WaveParticlesQuadtree(root, new Rect(center.x, rect.yMin, halfWidth, halfHeight), elements.Length);

			vertices = null;
			tangentsPack = null;
			particleGroups = null;
			numParticleGroups = 0;
		}

		private void CreateMesh()
		{
			int numVertices = elements.Length << 2;
			vertices = new Vector3[numVertices];

			for(int i = 0; i < vertices.Length; ++i)
				vertices[i].x = float.NaN;

			tangentsPack = new Vector4[numVertices];
			
			var uvs = new Vector2[numVertices];

			for(int i = 0; i < uvs.Length;)
			{
				uvs[i++] = new Vector2(0.0f, 0.0f);
				uvs[i++] = new Vector2(0.0f, 1.0f);
				uvs[i++] = new Vector2(1.0f, 1.0f);
				uvs[i++] = new Vector2(1.0f, 0.0f);
			}

			var indices = new int[numVertices];

			for(int i = 0; i < indices.Length; ++i)
				indices[i] = i;

			mesh = new Mesh();
			mesh.hideFlags = HideFlags.DontSave;
			mesh.name = "Wave Particles";
			mesh.vertices = vertices;
			mesh.uv = uvs;
			mesh.tangents = tangentsPack;
			mesh.SetIndices(indices, MeshTopology.Quads, 0);
		}
	}
}
