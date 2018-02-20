using System.Collections.Generic;
using UnityEngine;

namespace PlayWay.Water
{
	public class WaterOverlays : MonoBehaviour, IWaterRenderAware
	{
		[HideInInspector]
		[SerializeField]
		private Shader mapLocalDisplacementsShader;

		[SerializeField]
		private int antialiasing = 2;

		[SerializeField]
		private LayerMask interactionMask = int.MaxValue;

		private Water water;
		private Dictionary<Camera, WaterOverlaysData> buffers = new Dictionary<Camera, WaterOverlaysData>();
		private List<Camera> lostCameras = new List<Camera>();
		private IOverlaysRenderer[] overlayRenderers;
		private Material mapLocalDisplacementsMaterial;

		static private List<IWaterInteraction> shorelineRenderers = new List<IWaterInteraction>();

		void OnEnable()
		{
			water = GetComponent<Water>();
			ValidateWaterComponents();

			if(mapLocalDisplacementsMaterial == null)
			{
				mapLocalDisplacementsMaterial = new Material(mapLocalDisplacementsShader);
				mapLocalDisplacementsMaterial.hideFlags = HideFlags.DontSave;
            }
        }

		void OnDisable()
		{
			var enumerator = buffers.GetEnumerator();
			while(enumerator.MoveNext())
				enumerator.Current.Value.Dispose();
			buffers.Clear();
		}

		void OnValidate()
		{
			if(mapLocalDisplacementsShader == null)
				mapLocalDisplacementsShader = Shader.Find("PlayWay Water/Utility/Map Local Displacements");
        }

		void Update()
		{
			int frameIndex = Time.frameCount - 3;

			var enumerator = buffers.GetEnumerator();
			while(enumerator.MoveNext())
			{
				if(enumerator.Current.Value.lastFrameUsed < frameIndex)
				{
					enumerator.Current.Value.Dispose();
					lostCameras.Add(enumerator.Current.Key);
				}
			}
			
			for(int i=0; i<lostCameras.Count; ++i)
				buffers.Remove(lostCameras[i]);

			lostCameras.Clear();
        }

		public void ValidateWaterComponents()
		{
			overlayRenderers = GetComponents<IOverlaysRenderer>();
			int[] priorities = new int[overlayRenderers.Length];

			for(int i=0; i<priorities.Length; ++i)
			{
				var type = overlayRenderers[i].GetType();
				var attributes = type.GetCustomAttributes(typeof(OverlayRendererOrderAttribute), true);

				if(attributes.Length != 0)
					priorities[i] = ((OverlayRendererOrderAttribute)attributes[0]).Priority;
			}

			System.Array.Sort(priorities, overlayRenderers);
        }

		public void UpdateMaterial(Water water, WaterQualityLevel qualityLevel)
		{
			
		}

		public void BuildShaderVariant(ShaderVariant variant, Water water, WaterQualityLevel qualityLevel)
		{
			variant.SetWaterKeyword("_WATER_OVERLAYS", enabled);
		}

		public void OnWaterRender(Camera camera)
		{
			var waterCamera = camera.GetComponent<WaterCamera>();

			if(waterCamera == null || waterCamera.IsEffectCamera || !enabled || !Application.isPlaying || WaterCamera.IsSceneViewCamera(camera))
				return;

			var overlays = GetCameraOverlaysData(camera);
			overlays.lastFrameUsed = Time.frameCount;

			overlays.ClearOverlays();

			RenderInteractions(overlays);

			for(int i=0; i<overlayRenderers.Length; ++i)
				overlayRenderers[i].RenderOverlays(overlays);
			
			water.WaterMaterial.SetTexture("_LocalDisplacementMap", overlays.DynamicDisplacementMap);
			water.WaterMaterial.SetTexture("_LocalSlopeMap", overlays.SlopeMap);
			water.WaterMaterial.SetTexture("_TotalDisplacementMap", overlays.GetTotalDisplacementMap());
			water.WaterBackMaterial.SetTexture("_LocalDisplacementMap", overlays.DynamicDisplacementMap);
			water.WaterBackMaterial.SetTexture("_LocalSlopeMap", overlays.SlopeMap);
			water.WaterBackMaterial.SetTexture("_TotalDisplacementMap", overlays.GetTotalDisplacementMap());
		}

		public void RenderTotalDisplacementMap(RenderTexture renderTexture)
		{
			mapLocalDisplacementsMaterial.CopyPropertiesFromMaterial(water.WaterMaterial);
            Graphics.Blit(null, renderTexture, mapLocalDisplacementsMaterial, 0);
		}

		public void OnWaterPostRender(Camera camera)
		{
			
		}

		public WaterOverlaysData GetCameraOverlaysData(Camera camera)
		{
			WaterOverlaysData overlaysData;

			if(!buffers.TryGetValue(camera, out overlaysData))
			{
				int resolution = Mathf.NextPowerOfTwo((camera.pixelWidth + camera.pixelHeight) >> 1);
				buffers[camera] = overlaysData = new WaterOverlaysData(this, WaterCamera.GetWaterCamera(camera), resolution, antialiasing);

				RenderInteractions(overlaysData);
				overlaysData.SwapSlopeMaps();

				for(int i = 0; i < overlayRenderers.Length; ++i)
					overlayRenderers[i].RenderOverlays(overlaysData);

				overlaysData.Initialization = false;
			}

			return overlaysData;
        }

		private void RenderInteractions(WaterOverlaysData overlays)
		{
			int numRenderers = shorelineRenderers.Count;

			if(numRenderers == 0)
				return;

			Rect rect = overlays.Camera.LocalMapsRect;

			if(rect.width == 0.0f)
				return;
			
			var effectsCamera = overlays.Camera.EffectsCamera;
			effectsCamera.enabled = false;
			effectsCamera.depthTextureMode = DepthTextureMode.None;
			effectsCamera.orthographic = true;
			effectsCamera.orthographicSize = rect.width * 0.5f;
			effectsCamera.cullingMask = 1 << WaterProjectSettings.Instance.WaterTempLayer;
			effectsCamera.farClipPlane = 2000.0f;
			effectsCamera.clearFlags = CameraClearFlags.Nothing;
			effectsCamera.transform.position = new Vector3(rect.center.x, 1000.0f, rect.center.y);
			effectsCamera.transform.rotation = Quaternion.LookRotation(new Vector3(0.0f, -1.0f, 0.0f), new Vector3(0.0f, 0.0f, 1.0f));
			effectsCamera.targetTexture = overlays.SlopeMap;
			
			for(int i = 0; i < numRenderers; ++i)
			{
				if(((1 << shorelineRenderers[i].Layer) & interactionMask) != 0)
					shorelineRenderers[i].InteractionRenderer.enabled = true;
			}

			effectsCamera.Render();

			for(int i = 0; i < numRenderers; ++i)
				shorelineRenderers[i].InteractionRenderer.enabled = false;
		}
		
		static public void RegisterInteraction(IWaterInteraction renderer)
		{
			shorelineRenderers.Add(renderer);
		}

		static public void UnregisterInteraction(IWaterInteraction renderer)
		{
			shorelineRenderers.Remove(renderer);
		}
	}
}
