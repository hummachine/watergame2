using System.Collections.Generic;
using UnityEngine;

namespace PlayWay.Water
{
	/// <summary>
	/// Manages water primitives.
	/// </summary>
	[System.Serializable]
	public class WaterGeometry
	{
		[Tooltip("Geometry type used for display.")]
		[SerializeField]
		private Type type = Type.RadialGrid;

		[Tooltip("Water geometry vertex count at 1920x1080.")]
		[SerializeField]
		private int baseVertexCount = 500000;

		[Tooltip("Water geometry vertex count at 1920x1080 on systems with tesselation support. Set it a bit lower as tesselation will place additional, better distributed vertices in shader.")]
		[SerializeField]
		private int tesselatedBaseVertexCount = 16000;

		[SerializeField]
		private bool adaptToResolution = true;

		// sub-classes managing their primitive types

		[SerializeField]
		private WaterRadialGrid radialGrid;

		[SerializeField]
		private WaterProjectionGrid projectionGrid;

		[SerializeField]
		private WaterUniformGrid uniformGrid;

		[SerializeField]
		private WaterCustomSurfaceMeshes customSurfaceMeshes;

		[System.Obsolete]
		[SerializeField]
		private Mesh[] customMeshes;
		
		private Type previousType;
		private int previousTargetVertexCount;
		private int thisSystemVertexCount;
		private int frameCount;

		internal void OnEnable(Water water)
		{
#pragma warning disable 0612 // Type or member is obsolete
			if(customMeshes != null && customMeshes.Length != 0)
			{
				customSurfaceMeshes.Meshes = customMeshes;
				customMeshes = null;
			}
#pragma warning restore 0612 // Type or member is obsolete

			OnValidate(water);
			UpdateVertexCount();

			radialGrid.OnEnable(water);
			projectionGrid.OnEnable(water);
			uniformGrid.OnEnable(water);
			customSurfaceMeshes.OnEnable(water);
		}

		internal void OnDisable()
		{
			if(radialGrid != null) radialGrid.OnDisable();
			if(projectionGrid != null) projectionGrid.OnDisable();
			if(uniformGrid != null) uniformGrid.OnDisable();
			if(customSurfaceMeshes != null) customSurfaceMeshes.OnDisable();
		}

		public Type GeometryType
		{
			get { return type; }
		}

		public int VertexCount
		{
			get { return baseVertexCount; }
		}

		public int TesselatedBaseVertexCount
		{
			get { return tesselatedBaseVertexCount; }
		}

		public bool AdaptToResolution
		{
			get { return adaptToResolution; }
		}

		public bool Triangular
		{
			get
			{
				if(type == Type.CustomMeshes)
					return customSurfaceMeshes.Triangular;
				else
					return false;
			}
		}

		public WaterCustomSurfaceMeshes CustomSurfaceMeshes
		{
			get { return customSurfaceMeshes; }
		}

		[System.Obsolete("Use WaterGeometry.CustomMeshes.Meshes")]
		public Mesh[] GetCustomMeshesDirect()
		{
			return customSurfaceMeshes.Meshes;
		}

		[System.Obsolete("Use WaterGeometry.CustomMeshes.Meshes")]
		public void SetCustomMeshes(Mesh[] meshes)
		{
			customSurfaceMeshes.Meshes = meshes;
        }

		internal void OnValidate(Water water)
		{
			if(radialGrid == null) radialGrid = new WaterRadialGrid();
			if(projectionGrid == null) projectionGrid = new WaterProjectionGrid();
			if(uniformGrid == null) uniformGrid = new WaterUniformGrid();
			if(customSurfaceMeshes == null) customSurfaceMeshes = new WaterCustomSurfaceMeshes();

			// if geometry type changed
			if(previousType != type)
			{
				if(previousType == Type.RadialGrid) radialGrid.RemoveFromMaterial(water);
				if(previousType == Type.ProjectionGrid) projectionGrid.RemoveFromMaterial(water);
				if(previousType == Type.UniformGrid) uniformGrid.RemoveFromMaterial(water);

				if(type == Type.RadialGrid) radialGrid.AddToMaterial(water);
				if(type == Type.ProjectionGrid) projectionGrid.AddToMaterial(water);
				if(type == Type.UniformGrid) uniformGrid.AddToMaterial(water);

				previousType = type;
			}
			
			UpdateVertexCount();

			if(previousTargetVertexCount != thisSystemVertexCount)
			{
				radialGrid.Dispose();
				uniformGrid.Dispose();
				projectionGrid.Dispose();
				previousTargetVertexCount = thisSystemVertexCount;
			}
		}

		internal void Update()
		{
			// clean up unused geometries
			if(++frameCount > 8)
				frameCount = 0;

			switch(frameCount)
			{
				case 0:
				{
					radialGrid.Update();
					break;
				}

				case 3:
				{
					projectionGrid.Update();
					break;
				}

				case 6:
				{
					uniformGrid.Update();
					break;
				}
			}
		}

		public Mesh[] GetMeshes(WaterGeometryType geometryType, int vertexCount, bool volume)
		{
			if(geometryType == WaterGeometryType.ProjectionGrid)
				throw new System.InvalidOperationException("Projection grid needs camera to be retrieved. Use GetTransformedMeshes instead.");

			Matrix4x4 matrix;

			switch(geometryType)
			{
				case WaterGeometryType.Auto:
				{
					switch(type)
					{
						case Type.RadialGrid: return radialGrid.GetTransformedMeshes(null, out matrix, vertexCount, volume);
						case Type.ProjectionGrid: return projectionGrid.GetTransformedMeshes(null, out matrix, vertexCount, volume);
						case Type.UniformGrid: return uniformGrid.GetTransformedMeshes(null, out matrix, vertexCount, volume);
						case Type.CustomMeshes: return customSurfaceMeshes.GetTransformedMeshes(null, out matrix, volume);
						default: throw new System.InvalidOperationException("Unknown water geometry type.");
					}
				}

				case WaterGeometryType.RadialGrid: return radialGrid.GetTransformedMeshes(null, out matrix, vertexCount, volume);
				case WaterGeometryType.ProjectionGrid: return projectionGrid.GetTransformedMeshes(null, out matrix, vertexCount, volume);
				case WaterGeometryType.UniformGrid: return uniformGrid.GetTransformedMeshes(null, out matrix, vertexCount, volume);
				default: throw new System.InvalidOperationException("Unknown water geometry type.");
			}
		}

		public Mesh[] GetTransformedMeshes(Camera camera, out Matrix4x4 matrix, WaterGeometryType geometryType, bool volume, int vertexCount = 0)
		{
			if(vertexCount == 0)
			{
				if(adaptToResolution)
					vertexCount = Mathf.RoundToInt(thisSystemVertexCount * ((float)(camera.pixelWidth * camera.pixelHeight) / (1920 * 1080)));
				else
					vertexCount = thisSystemVertexCount;
			}

			switch(geometryType)
			{
				case WaterGeometryType.Auto:
				{
					switch(type)
					{
						case Type.RadialGrid: return radialGrid.GetTransformedMeshes(camera, out matrix, vertexCount, volume);
						case Type.ProjectionGrid: return projectionGrid.GetTransformedMeshes(camera, out matrix, vertexCount, volume);
						case Type.UniformGrid: return uniformGrid.GetTransformedMeshes(camera, out matrix, vertexCount, volume);
						case Type.CustomMeshes: return customSurfaceMeshes.GetTransformedMeshes(null, out matrix, volume);
						default: throw new System.InvalidOperationException("Unknown water geometry type.");
					}
				}

				case WaterGeometryType.RadialGrid: return radialGrid.GetTransformedMeshes(camera, out matrix, vertexCount, volume);
				case WaterGeometryType.ProjectionGrid: return projectionGrid.GetTransformedMeshes(camera, out matrix, vertexCount, volume);
				case WaterGeometryType.UniformGrid: return uniformGrid.GetTransformedMeshes(camera, out matrix, vertexCount, volume);
				default: throw new System.InvalidOperationException("Unknown water geometry type.");
			}
		}

		private void UpdateVertexCount()
		{
			thisSystemVertexCount = SystemInfo.supportsComputeShaders ?
				Mathf.Min(tesselatedBaseVertexCount, WaterQualitySettings.Instance.MaxTesselatedVertexCount) :
				Mathf.Min(baseVertexCount, WaterQualitySettings.Instance.MaxVertexCount);
		}

		public enum Type
		{
			RadialGrid,
			ProjectionGrid,
			UniformGrid,
            CustomMeshes
		}
	}
}
