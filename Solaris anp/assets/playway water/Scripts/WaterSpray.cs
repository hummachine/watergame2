using UnityEngine;

namespace PlayWay.Water
{
	[RequireComponent(typeof(Water))]
	[RequireComponent(typeof(WindWaves))]
	[AddComponentMenu("Water/Spray", 1)]
	public class WaterSpray : MonoBehaviour, IOverlaysRenderer
	{
		[HideInInspector]
		[SerializeField]
		private Shader sprayTiledGeneratorShader;

		[HideInInspector]
		[SerializeField]
		private Shader sprayLocalGeneratorShader;

		[HideInInspector]
		[SerializeField]
		private Shader sprayToFoamShader;

		[HideInInspector]
		[SerializeField]
		private ComputeShader sprayControllerShader;

		[SerializeField]
		private Material sprayMaterial;

		[Range(16, 327675)]
		[SerializeField]
		private int maxParticles = 65535;
		
		private float spawnThreshold = 1.0f;
		private float spawnSkipRatio = 0.9f;
		private float scale = 1.0f;
		
		private Water water;
		private WindWaves windWaves;
		private WaterOverlays overlays;
		private Material sprayTiledGeneratorMaterial;
		private Material sprayLocalGeneratorMaterial;
		private Material sprayToFoamMaterial;
		private Transform probeAnchor;

		private RenderTexture blankOutput;
		private Texture2D blankWhiteTex;
		private ComputeBuffer particlesA;
		private ComputeBuffer particlesB;
		private ComputeBuffer particlesInfo;
		private ComputeBuffer spawnBuffer;
		private int resolution;
		private Mesh mesh;
		private bool supported;
		private bool resourcesReady;
		private int[] countBuffer = new int[4];
		private float finalSpawnSkipRatio;
		private float skipRatioPrecomp;
		private Particle[] particlesToSpawn = new Particle[10];
		private int numParticlesToSpawn;
		private MaterialPropertyBlock[] propertyBlocks;

		void Start()
		{
			water = GetComponent<Water>();
			windWaves = GetComponent<WindWaves>();
			windWaves.ResolutionChanged.AddListener(OnResolutionChanged);
			overlays = GetComponent<WaterOverlays>();

			supported = CheckSupport();

			if(!supported)
			{
				enabled = false;
				return;
			}
		}

		public int MaxParticles
		{
			get { return maxParticles; }
		}

		public int SpawnedParticles
		{
			get
			{
				if(particlesA != null)
				{
					ComputeBuffer.CopyCount(particlesA, particlesInfo, 0);
					particlesInfo.GetData(countBuffer);
					return countBuffer[0];
				}
				else
					return 0;
			}
		}

		public ComputeBuffer ParticlesBuffer
		{
			get { return particlesB; }
		}
		
		private bool CheckSupport()
		{
			return SystemInfo.supportsComputeShaders && sprayTiledGeneratorShader != null && sprayTiledGeneratorShader.isSupported;
		}

		private void CheckResources()
		{
			if(sprayTiledGeneratorMaterial == null)
			{
				sprayTiledGeneratorMaterial = new Material(sprayTiledGeneratorShader);
				sprayTiledGeneratorMaterial.hideFlags = HideFlags.DontSave;
			}

			if(sprayLocalGeneratorMaterial == null)
			{
				sprayLocalGeneratorMaterial = new Material(sprayLocalGeneratorShader);
				sprayLocalGeneratorMaterial.hideFlags = HideFlags.DontSave;
            }

			if(sprayToFoamMaterial == null)
			{
				sprayToFoamMaterial = new Material(sprayToFoamShader);
				sprayToFoamMaterial.hideFlags = HideFlags.DontSave;
            }

			if(blankOutput == null)
			{
				UpdatePrecomputedParams();

				blankOutput = new RenderTexture(resolution, resolution, 0, SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8) ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
				blankOutput.filterMode = FilterMode.Point;
				blankOutput.Create();
			}

			if(mesh == null)
			{
				int vertexCount = Mathf.Min(maxParticles, 65535);

				mesh = new Mesh();
				mesh.name = "Spray";
				mesh.hideFlags = HideFlags.DontSave;
				mesh.vertices = new Vector3[vertexCount];

				int[] indices = new int[vertexCount];

				for(int i = 0; i < vertexCount; ++i)
					indices[i] = i;
				
				mesh.SetIndices(indices, MeshTopology.Points, 0);
				mesh.bounds = new Bounds(Vector3.zero, new Vector3(10000000.0f, 10000000.0f, 10000000.0f));
			}

			if(propertyBlocks == null)
			{
				int numMeshes = Mathf.CeilToInt(maxParticles / 65535.0f);

				propertyBlocks = new MaterialPropertyBlock[numMeshes];

				for(int i=0; i<numMeshes; ++i)
				{
					var block = propertyBlocks[i] = new MaterialPropertyBlock();
					block.SetFloat("_ParticleOffset", i * 65535);
				}
			}

			if(particlesA == null)
				particlesA = new ComputeBuffer(maxParticles, 40, ComputeBufferType.Append);

			if(particlesB == null)
				particlesB = new ComputeBuffer(maxParticles, 40, ComputeBufferType.Append);

			if(particlesInfo == null)
			{
				particlesInfo = new ComputeBuffer(1, 16, ComputeBufferType.IndirectArguments);
				var args = new int[4];
				args[0] = 0;
				args[1] = 1;
				args[2] = 0;
				args[3] = 0;
				particlesInfo.SetData(args);
			}

			resourcesReady = true;
        }

		private void Dispose()
		{
			if(blankOutput != null)
			{
				Destroy(blankOutput);
				blankOutput = null;
			}

			if(particlesA != null)
			{
				particlesA.Dispose();
				particlesA = null;
			}

			if(particlesB != null)
			{
				particlesB.Dispose();
				particlesB = null;
			}

			if(particlesInfo != null)
			{
				particlesInfo.Release();
				particlesInfo = null;
			}

			if(mesh != null)
			{
				Destroy(mesh);
				mesh = null;
			}

			if(probeAnchor != null)
			{
				Destroy(probeAnchor.gameObject);
				probeAnchor = null;
            }

			if(spawnBuffer != null)
			{
				spawnBuffer.Release();
				spawnBuffer = null;
            }

			resourcesReady = false;
		}

		void OnEnable()
		{
			water = GetComponent<Water>();
			water.ProfilesChanged.AddListener(OnProfilesChanged);
			OnProfilesChanged(water);

			Camera.onPreCull -= OnSomeCameraPreCull;
			Camera.onPreCull += OnSomeCameraPreCull;
		}

		void OnDisable()
		{
			Camera.onPreCull -= OnSomeCameraPreCull;

			Dispose();
		}

		void OnValidate()
		{
			if(sprayTiledGeneratorShader == null)
				sprayTiledGeneratorShader = Shader.Find("PlayWay Water/Spray/Generator (Tiles)");

			if(sprayLocalGeneratorShader == null)
				sprayLocalGeneratorShader = Shader.Find("PlayWay Water/Spray/Generator (Local)");

			if(sprayToFoamShader == null)
				sprayToFoamShader = Shader.Find("PlayWay Water/Spray/Spray To Foam");

#if UNITY_EDITOR
			if(sprayControllerShader == null)
			{
				var guids = UnityEditor.AssetDatabase.FindAssets("\"SprayController\" t:ComputeShader");

				if(guids.Length != 0)
				{
					string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
					sprayControllerShader = (ComputeShader)UnityEditor.AssetDatabase.LoadAssetAtPath(path, typeof(ComputeShader));
					UnityEditor.EditorUtility.SetDirty(this);
				}
			}

			if(sprayMaterial == null)
			{
				var guids = UnityEditor.AssetDatabase.FindAssets("\"Spray\" t:Material");

				if(guids.Length != 0)
				{
					string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
					sprayMaterial = (Material)UnityEditor.AssetDatabase.LoadAssetAtPath(path, typeof(Material));
					UnityEditor.EditorUtility.SetDirty(this);
				}
			}
#endif
			
			UpdatePrecomputedParams();
        }

		void LateUpdate()
		{
			if(Time.frameCount < 10)
				return;

			if(!resourcesReady)
				CheckResources();
			
			SwapParticleBuffers();
			ClearParticles();
			UpdateParticles();

			if(overlays == null && Camera.main != null)
				SpawnWindWavesParticlesTiled(Camera.main.transform);

			if(numParticlesToSpawn != 0)
			{
				SpawnCustomParticles(particlesToSpawn, numParticlesToSpawn);
				numParticlesToSpawn = 0;
            }
		}

		void OnSomeCameraPreCull(Camera camera)
		{
			if(!resourcesReady)
				return;

			var waterCamera = WaterCamera.GetWaterCamera(camera);

			if(waterCamera != null)
			{
				sprayMaterial.SetBuffer("_Particles", particlesA);
				sprayMaterial.SetVector("_CameraUp", camera.transform.up);
				sprayMaterial.SetFloat("_SpecularFresnelBias", water.WaterMaterial.GetFloat("_SpecularFresnelBias"));
				sprayMaterial.SetVector("_WrapSubsurfaceScatteringPack", water.WaterMaterial.GetVector("_WrapSubsurfaceScatteringPack"));
				sprayMaterial.SetTexture("_SubtractiveMask", waterCamera.SubtractiveMask);
				sprayMaterial.SetFloat("_UniformWaterScale", water.UniformWaterScale);

				if(probeAnchor == null)
				{
					var probeAnchorGo = new GameObject("Spray Probe Anchor");
					probeAnchorGo.hideFlags = HideFlags.HideAndDontSave;
					probeAnchor = probeAnchorGo.transform;
				}

				probeAnchor.position = camera.transform.position;

				int numMeshes = propertyBlocks.Length;

				for(int i = 0; i < numMeshes; ++i)
                    Graphics.DrawMesh(mesh, Matrix4x4.identity, sprayMaterial, 0, camera, 0, propertyBlocks[i], UnityEngine.Rendering.ShadowCastingMode.Off, false, probeAnchor);
			}
		}

		public void SpawnCustomParticle(Particle particle)
		{
			if(!enabled)
				return;

			if(particlesToSpawn.Length <= numParticlesToSpawn)
				System.Array.Resize(ref particlesToSpawn, particlesToSpawn.Length << 1);

			particlesToSpawn[numParticlesToSpawn] = particle;
			++numParticlesToSpawn;
		}

		public void SpawnCustomParticles(Particle[] particles, int numParticles)
		{
			if(!enabled)
				return;

			CheckResources();

			if(spawnBuffer == null || spawnBuffer.count < particles.Length)
			{
				if(spawnBuffer != null)
					spawnBuffer.Release();

				spawnBuffer = new ComputeBuffer(particles.Length, 40);
			}

			spawnBuffer.SetData(particles);

			sprayControllerShader.SetFloat("particleCount", numParticles);
			sprayControllerShader.SetBuffer(2, "SourceParticles", spawnBuffer);
			sprayControllerShader.SetBuffer(2, "TargetParticles", particlesA);
			sprayControllerShader.Dispatch(2, 1, 1, 1);
		}
		
		private void SpawnWindWavesParticlesTiled(Transform origin)
		{
			Vector3 originPosition = origin.position;
			float pixelSize = 400.0f / blankOutput.width;
			
			sprayTiledGeneratorMaterial.CopyPropertiesFromMaterial(water.WaterMaterial);
			sprayTiledGeneratorMaterial.SetVector("_SurfaceOffset", -water.SurfaceOffset);
            sprayTiledGeneratorMaterial.SetVector("_Params", new Vector4(spawnThreshold * 0.25835f, skipRatioPrecomp, 0.0f, scale * 0.455f));
			sprayTiledGeneratorMaterial.SetVector("_Coordinates", new Vector4(originPosition.x - 200.0f + Random.value * pixelSize, originPosition.z - 200.0f + Random.value * pixelSize, 400.0f, 400.0f));

			if(overlays == null)
				sprayTiledGeneratorMaterial.SetTexture("_LocalSlopeMap", GetBlankWhiteTex());

			Graphics.SetRandomWriteTarget(1, particlesA);
			Graphics.Blit(null, blankOutput, sprayTiledGeneratorMaterial, 0);
			Graphics.ClearRandomWriteTargets();
        }

		private void SpawnWindWavesParticlesLocal(WaterOverlaysData overlays)
		{
			sprayLocalGeneratorMaterial.CopyPropertiesFromMaterial(water.WaterMaterial);
			sprayLocalGeneratorMaterial.SetVector("_SurfaceOffset", -water.SurfaceOffset);
			sprayLocalGeneratorMaterial.SetVector("_Params", new Vector4(spawnThreshold * 0.25835f, spawnSkipRatio, 0.0f, scale * 0.455f));
			sprayLocalGeneratorMaterial.SetTexture("_TotalDisplacementMap", overlays.GetTotalDisplacementMap());
			sprayLocalGeneratorMaterial.SetTexture("_LocalSlopeMap", overlays.SlopeMapPrevious);
			sprayLocalGeneratorMaterial.SetTexture("_LocalUtilityMap", overlays.UtilityMap);
			
			float size = overlays.Camera.LocalMapsRect.width / water.UniformWaterScale;
			int iterations = 7 + Mathf.CeilToInt(size / 650.0f);
			iterations = Mathf.Clamp(iterations, 8, 12);

            sprayLocalGeneratorMaterial.SetInt("_Iterations", iterations);

			Graphics.SetRandomWriteTarget(1, particlesA);
			Graphics.Blit(null, blankOutput, sprayLocalGeneratorMaterial, 0);
			Graphics.ClearRandomWriteTargets();
		}

		private void GenerateLocalFoam(WaterOverlaysData overlays)
		{
			Graphics.SetRenderTarget(overlays.DynamicDisplacementMap);

			sprayToFoamMaterial.SetBuffer("_Particles", particlesA);
			sprayToFoamMaterial.SetVector("_LocalMapsCoords", overlays.Camera.LocalMapsShaderCoords);
			sprayToFoamMaterial.SetFloat("_UniformWaterScale", water.UniformWaterScale);

			Vector4 particleParams = sprayMaterial.GetVector("_ParticleParams");
			particleParams.x *= 8.0f;
			particleParams.z = 1.0f;
			sprayToFoamMaterial.SetVector("_ParticleParams", particleParams);

			int numMeshes = propertyBlocks.Length;

			for(int i = 0; i < numMeshes; ++i)
			{
				sprayToFoamMaterial.SetFloat("_ParticleOffset", i * 65535);
				sprayToFoamMaterial.SetPass(0);

				Graphics.DrawMeshNow(mesh, Matrix4x4.identity, 0);
			}
		}

		private void UpdateParticles()
		{
			Vector2 windSpeed = windWaves.WindSpeed * 0.0008f;
			Vector3 gravity = Physics.gravity;
			float deltaTime = Time.deltaTime;

			if(overlays != null)
			{
				var overlaysData = overlays.GetCameraOverlaysData(Camera.main);
				sprayControllerShader.SetTexture(0, "LocalSlopeMap", overlaysData.SlopeMap);

				var mainWaterCamera = WaterCamera.GetWaterCamera(Camera.main);

				if(mainWaterCamera != null)
					sprayControllerShader.SetVector("localMapsCoords", mainWaterCamera.LocalMapsShaderCoords);
			}
			else
				sprayControllerShader.SetTexture(0, "LocalSlopeMap", GetBlankWhiteTex());

			sprayControllerShader.SetFloat("deltaTime", deltaTime);
			sprayControllerShader.SetVector("externalForces", new Vector3((windSpeed.x + gravity.x) * deltaTime, gravity.y * deltaTime, (windSpeed.y + gravity.z) * deltaTime));
			sprayControllerShader.SetVector("surfaceOffset", water.WaterMaterial.GetVector("_SurfaceOffset"));
			sprayControllerShader.SetVector("waterTileSize", water.WaterMaterial.GetVector("_WaterTileSize"));
			sprayControllerShader.SetTexture(0, "DisplacedHeightMap", windWaves.WaterWavesFFT.DisplacedHeightMap);
			sprayControllerShader.SetBuffer(0, "SourceParticles", particlesB);
			sprayControllerShader.SetBuffer(0, "TargetParticles", particlesA);
			sprayControllerShader.Dispatch(0, maxParticles / 128, 1, 1);
		}

		private Texture2D GetBlankWhiteTex()
		{
			if(blankWhiteTex == null)
			{
				blankWhiteTex = new Texture2D(2, 2, TextureFormat.ARGB32, false, true);

				for(int x = 0; x < 2; ++x)
					for(int y = 0; y < 2; ++y)
						blankWhiteTex.SetPixel(x, y, new Color(1.0f, 1.0f, 1.0f, 1.0f));

				blankWhiteTex.Apply(false, true);
			}

			return blankWhiteTex;
        }
		
		private void ClearParticles()
		{
			sprayControllerShader.SetBuffer(1, "TargetParticlesFlat", particlesA);
			sprayControllerShader.Dispatch(1, maxParticles / 128, 1, 1);
		}

		private void SwapParticleBuffers()
		{
			var t = particlesB;
			particlesB = particlesA;
			particlesA = t;
		}

		private void OnResolutionChanged(WindWaves windWaves)
		{
			if(blankOutput != null)
			{
				Destroy(blankOutput);
				blankOutput = null;
			}
			
			resourcesReady = false;
        }

		private void OnProfilesChanged(Water water)
		{
			var profiles = water.Profiles;

			spawnThreshold = 0.0f;
			spawnSkipRatio = 0.0f;
			scale = 0.0f;

			if(profiles != null)
			{
				foreach(var weightedProfile in profiles)
				{
					var profile = weightedProfile.profile;
					float weight = weightedProfile.weight;

					spawnThreshold += profile.SprayThreshold * weight;
					spawnSkipRatio += profile.SpraySkipRatio * weight;
					scale += profile.SpraySize * weight;
				}
			}
		}

		private void UpdatePrecomputedParams()
		{
			if(water != null)
				resolution = windWaves.FinalResolution;
			
			skipRatioPrecomp = Mathf.Pow(spawnSkipRatio, 1024.0f / resolution);
        }

		public void RenderOverlays(WaterOverlaysData overlays)
		{
			if(!enabled)
				return;

			CheckResources();
			SpawnWindWavesParticlesLocal(overlays);
			GenerateLocalFoam(overlays);
		}

		public struct Particle
		{
			public Vector3 position;
			public Vector3 velocity;
			public Vector2 lifetime;
			public float offset;
			public float maxIntensity;

			public Particle(Vector3 position, Vector3 velocity, float lifetime, float offset, float maxIntensity)
			{
				this.position = position;
				this.velocity = velocity;
				this.lifetime = new Vector2(lifetime, lifetime);
				this.offset = offset;
				this.maxIntensity = maxIntensity;
			}
		}
	}
}
