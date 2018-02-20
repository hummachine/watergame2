using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlayWay.Water
{
	public class StaticWaterInteraction : MonoBehaviour, IWaterShore, IWaterInteraction
	{
		[HideInInspector]
		[SerializeField]
		private Shader maskGenerateShader;

		[HideInInspector]
		[SerializeField]
		private Shader maskDisplayShader;

		[HideInInspector]
		[SerializeField]
		private Shader heightMapperShader;

		[HideInInspector]
		[SerializeField]
		private Shader heightMapperShaderAlt;

		[Tooltip("Specifies a distance from the shore over which a water gets one meter deeper (value of 50 means that water has a depth of 1m at a distance of 50m from the shore).")]
		[Range(0.001f, 80.0f)]
		[SerializeField]
		private float shoreSmoothness = 50.0f;

		[Tooltip("If set to true, geometry that floats above water is correctly ignored.\n\nUse for objects that are closed and have faces at the bottom like basic primitives and custom meshes, but not terrain.")]
		[SerializeField]
		private bool hasBottomFaces = false;

		[SerializeField]
		private int resolution = 1024;
		
		private RenderTexture intensityMask;
		private MeshRenderer shorelineRenderer;
		private int resolutionSqr;
		private Bounds bounds;
		private Bounds totalBounds;

		private float[] heightMapData;
		private float offsetX, offsetZ, scaleX, scaleZ;
		private float terrainSize;
		private int width, height;

		static private Mesh quadMesh;
		static public List<StaticWaterInteraction> staticWaterInteractions = new List<StaticWaterInteraction>();

		void Start()
		{
			if(quadMesh == null)
				CreateQuadMesh();

			OnValidate();
			
			//CreateSpawnPoints();
			RenderShorelineIntensityMask();
			CreateMaskRenderer();
        }

		public Bounds Bounds
		{
			get { return totalBounds; }
		}

		public Texture IntensityMask
		{
			get { return intensityMask; }
		}

		public Renderer InteractionRenderer
		{
			get { return shorelineRenderer; }
		}

		public int Layer
		{
			get { return gameObject.layer; }
		}

		static public int NumStaticWaterInteractions
		{
			get { return staticWaterInteractions.Count; }
		}

		void OnValidate()
		{
			if(maskGenerateShader == null)
				maskGenerateShader = Shader.Find("PlayWay Water/Utility/ShorelineMaskGenerate");

			if(maskDisplayShader == null)
				maskDisplayShader = Shader.Find("PlayWay Water/Utility/ShorelineMaskRender");

			if(heightMapperShader == null)
				heightMapperShader = Shader.Find("PlayWay Water/Utility/HeightMapper");

			if(heightMapperShaderAlt == null)
				heightMapperShaderAlt = Shader.Find("PlayWay Water/Utility/HeightMapperAlt");
		}
		
		void OnEnable()
		{
			staticWaterInteractions.Add(this);
            WaterOverlays.RegisterInteraction(this);
		}

		void OnDisable()
		{
			WaterOverlays.UnregisterInteraction(this);
			staticWaterInteractions.Remove(this);
        }
		
		private void RenderShorelineIntensityMask()
		{
			try
			{
				PrepareRenderers();

				float shoreSteepness = 1.0f / shoreSmoothness;
				float distanceToFullSeaInMeters = 80.0f / shoreSteepness;
				totalBounds = bounds;
				totalBounds.Expand(new Vector3(distanceToFullSeaInMeters, 0.0f, distanceToFullSeaInMeters));

				float heightOffset = transform.position.y;
				var heightMap = RenderHeightMap(resolution, resolution);

				if(intensityMask == null)
				{
					intensityMask = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
					intensityMask.hideFlags = HideFlags.DontSave;
				}
				
				offsetX = -totalBounds.min.x;
				offsetZ = -totalBounds.min.z;
				scaleX = resolution / totalBounds.size.x;
				scaleZ = resolution / totalBounds.size.z;
				width = resolution;
				height = resolution;

				var temp1 = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
				var temp2 = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);

				var material = new Material(maskGenerateShader);
				material.SetVector("_ShorelineExtendRange", new Vector2(totalBounds.size.x / bounds.size.x - 1.0f, totalBounds.size.z / bounds.size.z - 1.0f));
				material.SetFloat("_TerrainMinPoint", heightOffset);
				material.SetFloat("_Steepness", Mathf.Max(totalBounds.size.x, totalBounds.size.z) * shoreSteepness);

				// distance map
				var distanceMapA = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
				var distanceMapB = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
				Graphics.Blit(heightMap, distanceMapA, material, 2);
				ComputeDistanceMap(material, distanceMapA, distanceMapB);
				RenderTexture.ReleaseTemporary(distanceMapA);

				// create filtered height map
				material.SetTexture("_DistanceMap", distanceMapB);
				Graphics.Blit(heightMap, temp1, material, 0);
				RenderTexture.ReleaseTemporary(heightMap);
				RenderTexture.ReleaseTemporary(distanceMapB);

				Graphics.Blit(temp1, temp2);
				ReadBackHeightMap(temp1);

				// create intensity mask
				Graphics.Blit(temp1, intensityMask, material, 1);

				RenderTexture.ReleaseTemporary(temp2);
				RenderTexture.ReleaseTemporary(temp1);
				Destroy(material);
			}
			finally
			{
				RestoreRenderers();
            }
		}

		private GameObject[] gameObjects;
		private Terrain[] terrains;
		private int[] originalRendererLayers;
		private float[] originalTerrainPixelErrors;

		private void PrepareRenderers()
		{
			bounds = new Bounds();

			var gameObjectsList = new List<GameObject>();
			var renderers = GetComponentsInChildren<Renderer>(false);

			for(int i = 0; i < renderers.Length; ++i)
			{
				var swi = renderers[i].GetComponent<StaticWaterInteraction>();

				if(swi == null || swi == this)
				{
					gameObjectsList.Add(renderers[i].gameObject);
					bounds.Encapsulate(renderers[i].bounds);
				}
            }

			terrains = GetComponentsInChildren<Terrain>(false);
			originalTerrainPixelErrors = new float[terrains.Length];

			for(int i = 0; i < terrains.Length; ++i)
			{
				originalTerrainPixelErrors[i] = terrains[i].heightmapPixelError;

				var swi = terrains[i].GetComponent<StaticWaterInteraction>();
				
				if(swi == null || swi == this)
				{
					gameObjectsList.Add(terrains[i].gameObject);
					terrains[i].heightmapPixelError = 1.0f;

					bounds.Encapsulate(terrains[i].transform.position);
					bounds.Encapsulate(terrains[i].transform.position + terrains[i].terrainData.size);
				}
			}

			gameObjects = gameObjectsList.ToArray();
			originalRendererLayers = new int[gameObjects.Length];

			for(int i = 0; i < gameObjects.Length; ++i)
			{
				originalRendererLayers[i] = gameObjects[i].layer;
				gameObjects[i].layer = WaterProjectSettings.Instance.WaterTempLayer;
			}
		}

		private void RestoreRenderers()
		{
			if(terrains != null)
			{
				for(int i = 0; i < terrains.Length; ++i)
					terrains[i].heightmapPixelError = originalTerrainPixelErrors[i];
			}

			if(gameObjects != null)
			{
				for(int i = gameObjects.Length - 1; i >= 0; --i)
					gameObjects[i].layer = originalRendererLayers[i];
			}
		}

		private RenderTexture RenderHeightMap(int width, int height)
		{
			var heightMap = RenderTexture.GetTemporary(width, height, 32, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
			heightMap.wrapMode = TextureWrapMode.Clamp;

			RenderTexture.active = heightMap;
			GL.Clear(true, true, new Color(-4000.0f, -4000.0f, -4000.0f, -4000.0f), 1000000.0f);
			RenderTexture.active = null;

			var cameraGo = new GameObject();
			var camera = cameraGo.AddComponent<Camera>();
			camera.enabled = false;
			camera.clearFlags = CameraClearFlags.Nothing;
			camera.depthTextureMode = DepthTextureMode.None;
			camera.orthographic = true;
			camera.cullingMask = 1 << WaterProjectSettings.Instance.WaterTempLayer;
			camera.nearClipPlane = 0.95f;
			camera.farClipPlane = bounds.size.y + 2.0f;
			camera.orthographicSize = bounds.size.z * 0.5f;
			camera.aspect = bounds.size.x / bounds.size.z;

			Vector3 cameraPosition = bounds.center;
			cameraPosition.y = bounds.max.y + 1.0f;

			camera.transform.position = cameraPosition;
			camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);

			camera.targetTexture = heightMap;
			camera.RenderWithShader(hasBottomFaces ? heightMapperShaderAlt : heightMapperShader, "RenderType");
			camera.targetTexture = null;

			Destroy(cameraGo);

			return heightMap;
		}

		static private void ComputeDistanceMap(Material material, RenderTexture sa, RenderTexture sb)
		{
			sa.filterMode = FilterMode.Point;
			sb.filterMode = FilterMode.Point;

			material.SetFloat("_Offset1", 1.0f / Mathf.Max(sa.width, sa.height));
			material.SetFloat("_Offset2", 1.41421356f / Mathf.Max(sa.width, sa.height));

			var a = sa;
			var b = sb;
			int w = (int)(Mathf.Max(sa.width, sa.height) * 0.7f);
			
			for(int i = 0; i < w; ++i)
			{
				Graphics.Blit(a, b, material, 3);
				
				var t = a;
				a = b;
				b = t;
			}
			
			// ensure that result is in b tex
			if(a != sb)
				Graphics.Blit(a, sb, material, 3);
		}

		private void ReadBackHeightMap(RenderTexture source)
		{
			int width = intensityMask.width;
			int height = intensityMask.height;

			heightMapData = new float[width * height + width + 1];

			RenderTexture.active = source;
			var gpuDownloadTex = new Texture2D(intensityMask.width, intensityMask.height, TextureFormat.RGBAFloat, false, true);
			gpuDownloadTex.ReadPixels(new Rect(0, 0, intensityMask.width, intensityMask.height), 0, 0);
			gpuDownloadTex.Apply();
			RenderTexture.active = null;

			int index = 0;

			for(int y = 0; y < height; ++y)
			{
				for(int x = 0; x < width; ++x)
				{
					float h = gpuDownloadTex.GetPixel(x, y).r;

					if(h > 0.0f && h < 1.0f)
						h = Mathf.Sqrt(h);

                    heightMapData[index++] = h;
				}
			}

			Destroy(gpuDownloadTex);
		}

		private void CreateMaskRenderer()
		{
			var go = new GameObject("Shoreline Mask");
			go.hideFlags = HideFlags.DontSave;
			go.layer = WaterProjectSettings.Instance.WaterTempLayer;

			var mf = go.AddComponent<MeshFilter>();
			mf.sharedMesh = quadMesh;

			var material = new Material(maskDisplayShader);
			material.hideFlags = HideFlags.DontSave;
			material.SetTexture("_MainTex", intensityMask);

			shorelineRenderer = go.AddComponent<MeshRenderer>();
			shorelineRenderer.sharedMaterial = material;
			shorelineRenderer.enabled = false;

			go.transform.SetParent(transform);
			go.transform.position = new Vector3(totalBounds.center.x, 0.0f, totalBounds.center.z);
			go.transform.localRotation = Quaternion.identity;
			go.transform.localScale = totalBounds.size;
		}
		
		static private void CreateQuadMesh()
		{
			quadMesh = new Mesh();
			quadMesh.name = "Shoreline Quad Mesh";
			quadMesh.hideFlags = HideFlags.DontSave;
			quadMesh.vertices = new Vector3[] { new Vector3(-0.5f, 0.0f, -0.5f), new Vector3(-0.5f, 0.0f, 0.5f), new Vector3(0.5f, 0.0f, 0.5f), new Vector3(0.5f, 0.0f, -0.5f) };
			quadMesh.uv = new Vector2[] { new Vector2(0.0f, 0.0f), new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(1.0f, 0.0f) };
			quadMesh.SetIndices(new int[] { 0, 1, 2, 3 }, MeshTopology.Quads, 0);
			quadMesh.UploadMeshData(true);
		}

		public float GetDepthAt(float x, float z)
		{
			x = (x + offsetX) * scaleX;
			z = (z + offsetZ) * scaleZ;

			int ix = (int)x; if(ix > x) --ix;       // inlined FastMath.FloorToInt(x);
			int iz = (int)z; if(iz > z) --iz;       // inlined FastMath.FloorToInt(z);

			if(ix >= width || ix < 0 || iz >= height || iz < 0)
				return 100.0f;

			x -= ix;
			z -= iz;

			int index = iz * width + ix;

			float a = heightMapData[index] * (1.0f - x) + heightMapData[index + 1] * x;
			float b = heightMapData[index + width] * (1.0f - x) + heightMapData[index + width + 1] * x;

			return a * (1.0f - z) + b * z;
		}

		static public float GetTotalDepthAt(float x, float z)
		{
			float minDepth = 100.0f;
			int numInteractions = staticWaterInteractions.Count;
			
			for(int i=0; i<numInteractions; ++i)
			{
				float depth = staticWaterInteractions[i].GetDepthAt(x, z);

				if(minDepth > depth)
					minDepth = depth;
			}

			return minDepth;
		}
	}
}
