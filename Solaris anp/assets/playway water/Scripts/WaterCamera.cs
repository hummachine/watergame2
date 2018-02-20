using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace PlayWay.Water
{
	/// <summary>
	/// Each camera supposed to see water needs this component attached. Renders all camera-specific maps for the water:
	/// <list type="bullet">
	/// <item>Depth Maps</item>
	/// <item>Displaced water info map</item>
	/// <item>Volume maps</item>
	/// </list>
	/// </summary>
	[AddComponentMenu("Water/Water Camera", -1)]
	[ExecuteInEditMode]
	public class WaterCamera : MonoBehaviour
	{
		[HideInInspector]
		[SerializeField]
		private Shader depthBlitCopyShader;

		[HideInInspector]
		[SerializeField]
		private Shader waterDepthShader;

		[HideInInspector]
		[SerializeField]
		private Shader volumeFrontShader;

		[HideInInspector]
		[SerializeField]
		private Shader volumeBackShader;

		[HideInInspector]
		[SerializeField]
		private Shader volumeFrontFastShader;

		[HideInInspector]
		[SerializeField]
		private Shader shadowEnforcerShader;

		[SerializeField]
		private WaterGeometryType geometryType = WaterGeometryType.Auto;

		[SerializeField]
		private bool renderWaterDepth = true;

		[Tooltip("Water has a pretty smooth shape so it's often safe to render it's depth in a lower resolution than the rest of the scene. Although the default value is 1.0, you may probably safely use 0.5 and gain some minor performance boost. If you will encounter any artifacts in masking or image effects, set it back to 1.0.")]
		[Range(0.2f, 1.0f)]
		[SerializeField]
		private float baseEffectsQuality = 1.0f;

		[SerializeField]
		private bool renderVolumes = true;

		[SerializeField]
		private bool renderFlatMasks = true;

		[SerializeField]
		private bool sharedCommandBuffers = false;

		[HideInInspector]
		[SerializeField]
		private int forcedVertexCount = 0;

		[SerializeField]
		private WaterCameraEvent submersionStateChanged;

		private RenderTexture waterDepthTexture;
		private RenderTexture subtractiveMaskTexture, additiveMaskTexture;
        private CommandBuffer depthRenderCommands;
		private CommandBuffer cleanUpCommands;
		private WaterCamera baseCamera;
        private Camera effectCamera;
		private Camera mainCamera;
		private Camera thisCamera;
		private Material depthMixerMaterial;
        private RenderTextureFormat waterDepthTextureFormat;
		private RenderTextureFormat blendedDepthTexturesFormat;
		private int waterDepthTextureId;
		private int underwaterMaskId;
		private int additiveMaskId;
		private int subtractiveMaskId;
        private bool isEffectCamera;
		private bool effectsEnabled;
		private IWaterImageEffect[] imageEffects;
		private Rect localMapsRect;
		private Rect localMapsRectPrevious;
		private Rect shadowedWaterRect;
		private int pixelWidth, pixelHeight;
		private Mesh shadowsEnforcerMesh;
		private Material shadowsEnforcerMaterial;
		private Water containingWater;
		private WaterSample waterSample;
		private float waterLevel;
		private SubmersionState submersionState;
		private bool isInsideSubtractiveVolume;
		private bool isInsideAdditiveVolume;

		static public event System.Action<WaterCamera> OnPreRender;

		static private Dictionary<Camera, WaterCamera> waterCamerasCache = new Dictionary<Camera, WaterCamera>();
		static private List<WaterCamera> enabledWaterCameras = new List<WaterCamera>();
		static private Texture2D underwaterWhiteMask;

		void Awake()
		{
			waterDepthTextureId = Shader.PropertyToID("_WaterDepthTexture");
			underwaterMaskId = Shader.PropertyToID("_UnderwaterMask");
			additiveMaskId = Shader.PropertyToID("_AdditiveMask");
			subtractiveMaskId = Shader.PropertyToID("_SubtractiveMask");

			if(SystemInfo.graphicsShaderLevel >= 40 && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Depth))
			{
				waterDepthTextureFormat = RenderTextureFormat.Depth;			// only > 4.0 shader targets can copy depth textures
				blendedDepthTexturesFormat = RenderTextureFormat.Depth;
			}
			else
			{
				if(SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat) && baseEffectsQuality > 0.2f)
					blendedDepthTexturesFormat = RenderTextureFormat.RFloat;
				else if(SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RHalf))
					blendedDepthTexturesFormat = RenderTextureFormat.RHalf;
				else
					blendedDepthTexturesFormat = RenderTextureFormat.R8;

				if(SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Depth))
					waterDepthTextureFormat = RenderTextureFormat.Depth;
				else
					waterDepthTextureFormat = blendedDepthTexturesFormat;
			}
			
			OnValidate();
		}

		void OnEnable()
		{
			thisCamera = GetComponent<Camera>();
			waterCamerasCache[thisCamera] = this;

			if(!isEffectCamera)
			{
				enabledWaterCameras.Add(this);
				imageEffects = GetComponents<IWaterImageEffect>();

				foreach(var imageEffect in imageEffects)
					imageEffect.OnWaterCameraEnabled();
			}
        }

		void OnDisable()
		{
			if(!isEffectCamera)
				enabledWaterCameras.Remove(this);

			DisableEffects();

			if(effectCamera != null)
			{
				effectCamera.gameObject.Destroy();
				effectCamera = null;
			}

			if(depthMixerMaterial != null)
			{
				depthMixerMaterial.Destroy();
				depthMixerMaterial = null;
			}

			if(waterSample != null)
			{
				waterSample.Stop();
				waterSample = null;
            }

			containingWater = null;
		}

		void OnDestroy()
		{
			waterCamerasCache.Clear();
		}

		public bool RenderWaterDepth
		{
			get { return renderWaterDepth; }
			set { renderWaterDepth = value; }
		}

		public bool RenderVolumes
		{
			get { return renderVolumes; }
			set { renderVolumes = value; }
		}

		public bool IsEffectCamera
		{
			get { return isEffectCamera; }
			set { isEffectCamera = value; }
		}

		public WaterGeometryType GeometryType
		{
			get { return geometryType; }
			set { geometryType = value; }
		}

		public Rect LocalMapsRect
		{
			get { return localMapsRect; }
		}

		public Rect LocalMapsRectPrevious
		{
			get { return localMapsRectPrevious; }
		}

		public Vector4 LocalMapsShaderCoords
		{
			get { return new Vector4(-localMapsRect.xMin, -localMapsRect.yMin, 1.0f / localMapsRect.width, localMapsRect.width); }
		}

		public int ForcedVertexCount
		{
			get { return forcedVertexCount; }
		}
		
		public Water ContainingWater
		{
			get { return baseCamera == null ? (submersionState != SubmersionState.None ? containingWater : null) : baseCamera.ContainingWater; }
		}

		public float WaterLevel
		{
			get { return waterLevel; }
		}

		public SubmersionState SubmersionState
		{
			get { return submersionState; }
		}

		public Camera MainCamera
		{
			get { return mainCamera; }
		}

		static public List<WaterCamera> EnabledWaterCameras
		{
			get { return enabledWaterCameras; }
		}

		/// <summary>
		/// Ready to render alternative camera for effects.
		/// </summary>
		public Camera EffectsCamera
		{
			get
			{
				if(!isEffectCamera && effectCamera == null)
					CreateEffectsCamera();

				return effectCamera;
			}
		}

		public RenderTexture SubtractiveMask
		{
			get { return subtractiveMaskTexture; }
		}

		public WaterCameraEvent SubmersionStateChanged
		{
			get { return submersionStateChanged ?? (submersionStateChanged = new WaterCameraEvent()); }
		}
		
		void OnValidate()
		{
			if(depthBlitCopyShader == null)
				depthBlitCopyShader = Shader.Find("PlayWay Water/Depth/CopyMix");

			if(waterDepthShader == null)
				waterDepthShader = Shader.Find("PlayWay Water/Depth/Water Depth");

			if(volumeFrontShader == null)
				volumeFrontShader = Shader.Find("PlayWay Water/Volumes/Front");

			if(volumeBackShader == null)
				volumeBackShader = Shader.Find("PlayWay Water/Volumes/Back");

			if(volumeFrontFastShader == null)
				volumeFrontFastShader = Shader.Find("PlayWay Water/Volumes/Front Simple");

			if(shadowEnforcerShader == null)
				shadowEnforcerShader = Shader.Find("PlayWay Water/Utility/ShadowEnforcer");
        }
		
		void OnPreCull()
		{
			if(!enabled)
				return;

			if(OnPreRender != null)
				OnPreRender(this);

			if(!isEffectCamera)
				ToggleEffects();

			// compute resolution of basic effect buffers
			int baseEffectsWidth = Mathf.RoundToInt(thisCamera.pixelWidth * baseEffectsQuality);
			int baseEffectsHeight = Mathf.RoundToInt(thisCamera.pixelHeight * baseEffectsQuality);

			if(!isEffectCamera)
			{
				PrepareToRender();
				SetFallbackUnderwaterMask();
			}

			if(effectsEnabled)
				SetLocalMapCoordinates();

			RenderWater();

#if UNITY_EDITOR
			if(IsSceneViewCamera(thisCamera))
			{
				if(!isEffectCamera)
				{
					SetBlankWaterMasks();

					if(effectCamera != null)
					{
						DestroyImmediate(effectCamera.gameObject);
						effectCamera = null;
					}
				}

				return;
			}
#endif

			if(!effectsEnabled) return;

			if(renderVolumes)
				RenderWaterMasks(baseEffectsWidth, baseEffectsHeight);
			else
				SetBlankWaterMasks();

			if(renderWaterDepth)
				RenderWaterDepthBuffer(baseEffectsWidth, baseEffectsHeight);
			
			if(imageEffects != null && Application.isPlaying)
			{
				foreach(var imageEffect in imageEffects)
					imageEffect.OnWaterCameraPreCull();
			}

			if(shadowedWaterRect.xMin < shadowedWaterRect.xMax)
				RenderShadowEnforcers();
        }

		void OnPostRender()
		{
			if(waterDepthTexture != null)
			{
				RenderTexture.ReleaseTemporary(waterDepthTexture);
				waterDepthTexture = null;
			}
			
			if(subtractiveMaskTexture != null)
			{
				RenderTexture.ReleaseTemporary(subtractiveMaskTexture);
				subtractiveMaskTexture = null;
			}

			if(additiveMaskTexture != null)
			{
				RenderTexture.ReleaseTemporary(additiveMaskTexture);
				additiveMaskTexture = null;
            }

			var waters = WaterGlobals.Instance.Waters;
			int numWaterInstances = waters.Count;

			for(int waterIndex = 0; waterIndex < numWaterInstances; ++waterIndex)
				waters[waterIndex].Renderer.PostRender(thisCamera);
		}
		
		internal void ReportShadowedWaterMinMaxRect(Vector2 min, Vector2 max)
		{
			if(shadowedWaterRect.xMin > min.x)
				shadowedWaterRect.xMin = min.x;

			if(shadowedWaterRect.yMin > min.y)
				shadowedWaterRect.yMin = min.y;

			if(shadowedWaterRect.xMax < max.x)
				shadowedWaterRect.xMax = max.x;

			if(shadowedWaterRect.yMax < max.y)
				shadowedWaterRect.yMax = max.y;
		}

		/// <summary>
		/// Fast and allocation free way to get a WaterCamera component attached to camera.
		/// </summary>
		/// <param name="camera"></param>
		/// <returns></returns>
		static public WaterCamera GetWaterCamera(Camera camera, bool forceAdd = false)
		{
			WaterCamera waterCamera;

			if(!waterCamerasCache.TryGetValue(camera, out waterCamera))
			{
				waterCamera = camera.GetComponent<WaterCamera>();

				if(waterCamera != null)
					waterCamerasCache[camera] = waterCamera;
				else if(forceAdd)
					waterCamerasCache[camera] = camera.gameObject.AddComponent<WaterCamera>();
				else
					waterCamerasCache[camera] = waterCamera = null;         // force null reference (Unity uses custom null operator)
			}

			return waterCamera;
        }
		
		private void RenderWater()
		{
			var waters = WaterGlobals.Instance.Waters;
			int numWaterInstances = waters.Count;
			
			for(int waterIndex=0; waterIndex<numWaterInstances; ++waterIndex)
				waters[waterIndex].Renderer.Render(thisCamera, geometryType);
		}

		private void RenderWaterDepthBuffer(int baseEffectsWidth, int baseEffectsHeight)
		{
			if(waterDepthTexture == null)
			{
				waterDepthTexture = RenderTexture.GetTemporary(baseEffectsWidth, baseEffectsHeight, waterDepthTextureFormat == RenderTextureFormat.Depth ? 32 : 16, waterDepthTextureFormat, RenderTextureReadWrite.Linear);
				waterDepthTexture.filterMode = baseEffectsQuality > 0.98f ? FilterMode.Point : FilterMode.Bilinear;			// no need to filter it, if it's of screen size
				waterDepthTexture.wrapMode = TextureWrapMode.Clamp;
			}

			var effectCamera = EffectsCamera;
			effectCamera.CopyFrom(thisCamera);
			effectCamera.GetComponent<WaterCamera>().enabled = true;
			effectCamera.renderingPath = RenderingPath.Forward;
			effectCamera.clearFlags = CameraClearFlags.SolidColor;
			effectCamera.depthTextureMode = DepthTextureMode.None;
			effectCamera.backgroundColor = Color.white;
			effectCamera.targetTexture = waterDepthTexture;
			effectCamera.cullingMask = (1 << WaterProjectSettings.Instance.WaterLayer);
			effectCamera.RenderWithShader(waterDepthShader, "CustomType");
			effectCamera.targetTexture = null;

			Shader.SetGlobalTexture(waterDepthTextureId, waterDepthTexture);
		}

		private void RenderWaterMasks(int baseEffectsWidth, int baseEffectsHeight)
		{
			var waters = WaterGlobals.Instance.Waters;
			int numWaters = waters.Count;

			bool hasSubtractiveVolumes = false;
			bool hasAdditiveVolumes = false;
			bool hasFlatMasks = false;

			for(int i = 0; i < numWaters; ++i)
				waters[i].Renderer.OnSharedSubtractiveMaskRender(ref hasSubtractiveVolumes, ref hasAdditiveVolumes, ref hasFlatMasks);

			var effectCamera = EffectsCamera;
			effectCamera.CopyFrom(thisCamera);
			effectCamera.GetComponent<WaterCamera>().enabled = false;
			effectCamera.renderingPath = RenderingPath.Forward;
			effectCamera.depthTextureMode = DepthTextureMode.None;

			if(hasSubtractiveVolumes || hasFlatMasks)
			{
				if(subtractiveMaskTexture == null)
				{
					subtractiveMaskTexture = RenderTexture.GetTemporary(baseEffectsWidth, baseEffectsHeight, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
					subtractiveMaskTexture.filterMode = baseEffectsQuality > 0.98f ? FilterMode.Point : FilterMode.Bilinear;
					subtractiveMaskTexture.wrapMode = TextureWrapMode.Clamp;
				}

				Graphics.SetRenderTarget(subtractiveMaskTexture);
				GL.Clear(true, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));

				effectCamera.targetTexture = subtractiveMaskTexture;

				if(hasSubtractiveVolumes)
				{
					effectCamera.clearFlags = CameraClearFlags.Nothing;
					effectCamera.cullingMask = (1 << WaterProjectSettings.Instance.WaterLayer);
					effectCamera.RenderWithShader(isInsideSubtractiveVolume ? volumeFrontShader : volumeFrontFastShader, "");
					effectCamera.RenderWithShader(volumeBackShader, "");
				}

				if(hasFlatMasks && renderFlatMasks)
				{
					effectCamera.clearFlags = CameraClearFlags.Nothing;
					effectCamera.cullingMask = (1 << WaterProjectSettings.Instance.WaterTempLayer);
					effectCamera.Render();                  // may be merged with effectCamera.RenderWithShader(volumeFrontShader, "");
				}

				for(int i = 0; i < numWaters; ++i)
				{
					waters[i].WaterMaterial.SetTexture(subtractiveMaskId, subtractiveMaskTexture);
					waters[i].WaterBackMaterial.SetTexture(subtractiveMaskId, subtractiveMaskTexture);
				}
			}

			if(hasAdditiveVolumes)
			{
				for(int i = 0; i < numWaters; ++i)
					waters[i].Renderer.OnSharedMaskAdditiveRender();

				if(additiveMaskTexture == null)
				{
					additiveMaskTexture = RenderTexture.GetTemporary(baseEffectsWidth, baseEffectsHeight, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
					additiveMaskTexture.filterMode = baseEffectsQuality > 0.98f ? FilterMode.Point : FilterMode.Bilinear;
					additiveMaskTexture.wrapMode = TextureWrapMode.Clamp;
				}

				Graphics.SetRenderTarget(additiveMaskTexture);
				GL.Clear(true, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));

				effectCamera.clearFlags = CameraClearFlags.Nothing;
				effectCamera.targetTexture = additiveMaskTexture;
				effectCamera.cullingMask = (1 << WaterProjectSettings.Instance.WaterLayer);
				effectCamera.RenderWithShader(isInsideAdditiveVolume ? volumeFrontShader : volumeFrontFastShader, "");
				effectCamera.RenderWithShader(volumeBackShader, "");

				for(int i = 0; i < numWaters; ++i)
				{
					waters[i].WaterMaterial.SetTexture(additiveMaskId, additiveMaskTexture);
					waters[i].WaterBackMaterial.SetTexture(additiveMaskId, additiveMaskTexture);
				}
			}

			effectCamera.targetTexture = null;

			for(int i = 0; i < numWaters; ++i)
				waters[i].Renderer.OnSharedMaskPostRender();

			//Shader.SetGlobalTexture("_WaterMask", waterMasksTexture);
		}

		private void SetBlankWaterMasks()
		{
			var waters = WaterGlobals.Instance.Waters;
			int numWaters = waters.Count;
			
			for(int i = 0; i < numWaters; ++i)
			{
				var water = waters[i];
				water.WaterMaterial.SetTexture(subtractiveMaskId, underwaterWhiteMask);
				water.WaterMaterial.SetTexture(additiveMaskId, underwaterWhiteMask);
				water.WaterBackMaterial.SetTexture(subtractiveMaskId, underwaterWhiteMask);
				water.WaterBackMaterial.SetTexture(additiveMaskId, underwaterWhiteMask);
			}
		}
		
		private void AddDepthRenderingCommands()
		{
			pixelWidth = thisCamera.pixelWidth;
			pixelHeight = thisCamera.pixelHeight;

			if(depthMixerMaterial == null)
			{
				depthMixerMaterial = new Material(depthBlitCopyShader);
				depthMixerMaterial.hideFlags = HideFlags.DontSave;
			}

			var camera = GetComponent<Camera>();

			if(((camera.depthTextureMode | DepthTextureMode.Depth) != 0 && renderWaterDepth) || renderVolumes)
			{
				int depthRT = Shader.PropertyToID("_CameraDepthTexture2");
				int waterlessDepthRT = Shader.PropertyToID("_WaterlessDepthTexture");

				depthRenderCommands = new CommandBuffer();
				depthRenderCommands.name = "Apply Water Depth";
				depthRenderCommands.GetTemporaryRT(waterlessDepthRT, pixelWidth, pixelHeight, blendedDepthTexturesFormat == RenderTextureFormat.Depth ? 32 : 0, FilterMode.Point, blendedDepthTexturesFormat, RenderTextureReadWrite.Linear);

				if(!IsSceneViewCamera(camera))
					depthRenderCommands.Blit(BuiltinRenderTextureType.None, waterlessDepthRT, depthMixerMaterial, 0);
				else
				{
					depthRenderCommands.SetRenderTarget(waterlessDepthRT);
					depthRenderCommands.ClearRenderTarget(true, true, new Color(10000.0f, 10000.0f, 10000.0f, 10000.0f));
				}

				depthRenderCommands.GetTemporaryRT(depthRT, pixelWidth, pixelHeight, blendedDepthTexturesFormat == RenderTextureFormat.Depth ? 32 : 0, FilterMode.Point, blendedDepthTexturesFormat, RenderTextureReadWrite.Linear);
				depthRenderCommands.SetRenderTarget(depthRT);
				depthRenderCommands.ClearRenderTarget(true, true, Color.white);
				depthRenderCommands.Blit(BuiltinRenderTextureType.None, depthRT, depthMixerMaterial, 1);
				depthRenderCommands.SetGlobalTexture("_CameraDepthTexture", depthRT);

				cleanUpCommands = new CommandBuffer();
				cleanUpCommands.name = "Clean Water Buffers";
				cleanUpCommands.ReleaseTemporaryRT(depthRT);
				cleanUpCommands.ReleaseTemporaryRT(waterlessDepthRT);

				camera.depthTextureMode |= DepthTextureMode.Depth;

				camera.AddCommandBuffer(camera.actualRenderingPath == RenderingPath.Forward ? CameraEvent.AfterDepthTexture : CameraEvent.BeforeLighting, depthRenderCommands);
				camera.AddCommandBuffer(CameraEvent.AfterEverything, cleanUpCommands);
			}
		}
		
		private void RemoveDepthRenderingCommands()
		{
			if(depthRenderCommands != null)
			{
				thisCamera.RemoveCommandBuffer(CameraEvent.AfterDepthTexture, depthRenderCommands);
				thisCamera.RemoveCommandBuffer(CameraEvent.BeforeLighting, depthRenderCommands);
				depthRenderCommands.Dispose();
				depthRenderCommands = null;
            }

			if(cleanUpCommands != null)
			{
				thisCamera.RemoveCommandBuffer(CameraEvent.AfterEverything, cleanUpCommands);
				cleanUpCommands.Dispose();
				cleanUpCommands = null;
            }

			if(!sharedCommandBuffers)
				thisCamera.RemoveAllCommandBuffers();
        }

		private void EnableEffects()
		{
			if(isEffectCamera)
				return;
			
			effectsEnabled = true;
			AddDepthRenderingCommands();
        }

		private void DisableEffects()
		{
			effectsEnabled = false;
			RemoveDepthRenderingCommands();
		}
		
		private bool IsWaterPossiblyVisible()
		{
#if UNITY_EDITOR
			if(!Application.isPlaying)
				return true;
#endif

			var waters = WaterGlobals.Instance.Waters;
			return waters.Count != 0;
		}

		private void CreateEffectsCamera()
		{
			var effectCameraGo = new GameObject(name + " Water Effects Camera");
			effectCameraGo.hideFlags = HideFlags.HideAndDontSave;

			effectCamera = effectCameraGo.AddComponent<Camera>();
			effectCamera.enabled = false;

			var effectWaterCamera = effectCameraGo.AddComponent<WaterCamera>();
			effectWaterCamera.isEffectCamera = true;
			effectWaterCamera.mainCamera = thisCamera;
            effectWaterCamera.baseCamera = this;
            effectWaterCamera.waterDepthShader = waterDepthShader;

			enabledWaterCameras.Remove(effectWaterCamera);
        }

		private void RenderShadowEnforcers()
		{
			if(shadowsEnforcerMesh == null)
			{
				shadowsEnforcerMesh = new Mesh();
				shadowsEnforcerMesh.hideFlags = HideFlags.DontSave;
				shadowsEnforcerMesh.name = "Water Shadow Enforcer";
                shadowsEnforcerMesh.vertices = new Vector3[4];
				shadowsEnforcerMesh.SetIndices(new int[] { 0, 1, 2, 3 }, MeshTopology.Quads, 0);
				shadowsEnforcerMesh.UploadMeshData(true);

				shadowsEnforcerMaterial = new Material(shadowEnforcerShader);
				shadowsEnforcerMaterial.hideFlags = HideFlags.DontSave;
            }
			
			var bounds = new Bounds();
			
			float distance = QualitySettings.shadowDistance * 0.5f;
			Vector3 a = thisCamera.ViewportPointToRay(new Vector3(shadowedWaterRect.xMin, shadowedWaterRect.yMin, 1.0f)).GetPoint(distance);
			Vector3 b = thisCamera.ViewportPointToRay(new Vector3(shadowedWaterRect.xMax, shadowedWaterRect.yMax, 1.0f)).GetPoint(distance);
			SetBoundsMinMaxComponentWise(ref bounds, a, b);
            shadowsEnforcerMesh.bounds = bounds;

			Graphics.DrawMesh(shadowsEnforcerMesh, Matrix4x4.identity, shadowsEnforcerMaterial, 0);
		}

		private void SetBoundsMinMaxComponentWise(ref Bounds bounds, Vector3 a, Vector3 b)
		{
			if(a.x > b.x)
			{
				float t = b.x;
				b.x = a.x;
				a.x = t;
			}

			if(a.y > b.y)
			{
				float t = b.y;
				b.y = a.y;
				a.y = t;
			}

			if(a.z > b.z)
			{
				float t = b.z;
				b.z = a.z;
				a.z = t;
			}

			bounds.SetMinMax(a, b);
		}

		private void PrepareToRender()
		{
			// reset shadowed water rect
			shadowedWaterRect = new Rect(1.0f, 1.0f, -1.0f, -1.0f);

#if UNITY_EDITOR
			if(IsSceneViewCamera(thisCamera))
				return;                         // don't do any of the following stuff for editor cameras
#endif

			// find containing water
			float waterEnterTolerance = thisCamera.nearClipPlane * Mathf.Tan(thisCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 3.0f;
			var newWater = Water.FindWater(transform.position, waterEnterTolerance, out isInsideSubtractiveVolume, out isInsideAdditiveVolume);

			if(newWater != containingWater)
			{
				if(containingWater != null && submersionState != SubmersionState.None)
				{
					submersionState = SubmersionState.None;
					SubmersionStateChanged.Invoke(this);
				}

				containingWater = newWater;
				submersionState = SubmersionState.None;

				if(waterSample != null)
				{
					waterSample.Stop();
					waterSample = null;
				}

				if(newWater != null && newWater.Volume.Boundless)
				{
					waterSample = new WaterSample(containingWater, WaterSample.DisplacementMode.Height, 0.4f);
					waterSample.Start(transform.position);
				}
			}

			// determine submersion state
			SubmersionState newSubmersionState;

			if(waterSample != null)
			{
				waterLevel = waterSample.GetAndReset(transform.position, WaterSample.ComputationsMode.Normal).y;

				if(transform.position.y - waterEnterTolerance < waterLevel)
				{
					if(transform.position.y + waterEnterTolerance < waterLevel)
						newSubmersionState = SubmersionState.Full;
					else
						newSubmersionState = SubmersionState.Partial;
				}
				else
					newSubmersionState = SubmersionState.None;
			}
			else
			{
				newSubmersionState = containingWater != null ? SubmersionState.Partial : SubmersionState.None;			// for non-boundless water always use Partial state as determining this would be too costly
            }

			if(newSubmersionState != submersionState)
			{
				submersionState = newSubmersionState;
				SubmersionStateChanged.Invoke(this);
			}
		}
		
		private void SetFallbackUnderwaterMask()
		{
			if(underwaterWhiteMask == null)
			{
				var color = new Color(0.0f, 0.0f, 0.0f, 0.0f);
                underwaterWhiteMask = new Texture2D(2, 2, TextureFormat.ARGB32, false);
				underwaterWhiteMask.hideFlags = HideFlags.DontSave;
				underwaterWhiteMask.SetPixel(0, 0, color);
				underwaterWhiteMask.SetPixel(1, 0, color);
				underwaterWhiteMask.SetPixel(0, 1, color);
				underwaterWhiteMask.SetPixel(1, 1, color);
				underwaterWhiteMask.Apply(false, true);
			}

			Shader.SetGlobalTexture(underwaterMaskId, underwaterWhiteMask);
		}

		private void ToggleEffects()
		{
			if(!effectsEnabled)
			{
				if(IsWaterPossiblyVisible())
					EnableEffects();
			}
			else if(!IsWaterPossiblyVisible())
				DisableEffects();

			if(effectsEnabled && (thisCamera.pixelWidth != pixelWidth || thisCamera.pixelHeight != pixelHeight))
			{
				DisableEffects();
				EnableEffects();
			}
		}

		private void SetLocalMapCoordinates()
		{
			int resolution = Mathf.NextPowerOfTwo((thisCamera.pixelWidth + thisCamera.pixelHeight) >> 1);
			float maxHeight = 0.0f;
			float maxWaterLevel = 0.0f;

			var waters = WaterGlobals.Instance.Waters;
			int numWaterInstances = waters.Count;

			for(int waterIndex = 0; waterIndex < numWaterInstances; ++waterIndex)
			{
				var water = waters[waterIndex];
				maxHeight += water.MaxVerticalDisplacement;

				float posY = water.transform.position.y;
				if(maxWaterLevel < posY)
					maxWaterLevel = posY;
			}

			// place camera
			Vector3 thisCameraPosition = thisCamera.transform.position;
			Vector3 screenSpaceDown = WaterUtilities.ViewportWaterPerpendicular(thisCamera);
			Vector3 worldSpaceDown = thisCamera.transform.localToWorldMatrix * WaterUtilities.RaycastPlane(thisCamera, maxWaterLevel, screenSpaceDown);
			Vector3 worldSpaceCenter = thisCamera.transform.localToWorldMatrix * WaterUtilities.RaycastPlane(thisCamera, maxWaterLevel, new Vector3(0.5f, 0.5f, 0.5f));
			
			Vector3 effectCameraPosition;

			if(worldSpaceDown.sqrMagnitude > worldSpaceCenter.sqrMagnitude)
				effectCameraPosition = new Vector3(thisCameraPosition.x + worldSpaceDown.x * 3.0f, 0.0f, thisCameraPosition.z + worldSpaceDown.z * 3.0f);
			else
				effectCameraPosition = new Vector3(thisCameraPosition.x + worldSpaceCenter.x * 3.0f, 0.0f, thisCameraPosition.z + worldSpaceCenter.z * 3.0f);

			Vector3 diff = effectCameraPosition - thisCameraPosition;

			if(diff.magnitude > thisCamera.farClipPlane * 0.5f)
			{
				effectCameraPosition = thisCameraPosition + diff.normalized * thisCamera.farClipPlane * 0.5f;
				effectCameraPosition.y = 0.0f;
			}

			Vector3 thisCameraForward = thisCamera.transform.forward;
			float forwardFactor = Mathf.Min(1.0f, thisCameraForward.y + 1.0f);

			float size1 = thisCameraPosition.y * (1.0f + 7.0f * Mathf.Sqrt(forwardFactor));
			float size2 = maxHeight * 2.5f;
			//float size3 = Vector3.Distance(effectCameraPosition, thisCameraPosition);
			//float size = size1 > size2 ? (size1 > size3 ? size1 : size3) : (size2 > size3 ? size2 : size3);
			float size = size1 > size2 ? size1 : size2;
			
			effectCameraPosition = new Vector3(thisCameraPosition.x + thisCameraForward.x * size * 0.4f, 0.0f, thisCameraPosition.z + thisCameraForward.z * size * 0.4f);

			localMapsRectPrevious = localMapsRect;

			float halfPixelSize = size / resolution;
			localMapsRect = new Rect((effectCameraPosition.x - size) + halfPixelSize, (effectCameraPosition.z - size) + halfPixelSize, 2.0f * size, 2.0f * size);

			Shader.SetGlobalVector("_LocalMapsCoordsPrevious", new Vector4(-localMapsRectPrevious.xMin, -localMapsRectPrevious.yMin, 1.0f / localMapsRectPrevious.width, localMapsRectPrevious.width));
			Shader.SetGlobalVector("_LocalMapsCoords", new Vector4(-localMapsRect.xMin, -localMapsRect.yMin, 1.0f / localMapsRect.width, localMapsRect.width));
		}

		static public bool IsSceneViewCamera(Camera camera)
		{
#if UNITY_EDITOR && !UNITY_5_0 && !UNITY_5_1 && !UNITY_5_2             // use 5.3 API for this
			return camera.cameraType == UnityEngine.CameraType.SceneView;
#elif UNITY_EDITOR                                                     // fallback
			var sceneViews = UnityEditor.SceneView.sceneViews;
			int numSceneViews = sceneViews.Count;

			for(int i = 0; i < numSceneViews; ++i)
			{
				if(((UnityEditor.SceneView)sceneViews[i]).camera == camera)
					return true;
			}

			return false;
#else
			return false;
#endif
		}

		[System.Serializable]
		public class WaterCameraEvent : UnityEvent<WaterCamera> { }
    }
}
