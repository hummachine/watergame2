using System.Collections.Generic;
using UnityEngine;

namespace PlayWay.Water
{
	/// <summary>
	/// Renders water.
	/// <seealso cref="Water.Renderer"/>
	/// </summary>
	[System.Serializable]
	public class WaterRenderer
	{
		[HideInInspector]
		[SerializeField]
		private Shader volumeFrontShader;

		[HideInInspector]
		[SerializeField]
		private Shader volumeBackShader;

		[SerializeField]
		private Transform reflectionProbeAnchor;

		[SerializeField]
		private bool useSharedMask = true;
		
        private Water water;
		//private RenderTexture maskTexture;
		private List<Renderer> masks = new List<Renderer>();

		internal void OnEnable(Water water)
		{
			this.water = water;
			this.useSharedMask = true;			// forced for now

#if UNITY_EDITOR
			Camera.onPreCull -= OnSomeCameraPreCull;
			Camera.onPreCull += OnSomeCameraPreCull;
#endif
		}

		internal void OnDisable()
		{
			Camera.onPreCull -= OnSomeCameraPreCull;
			ReleaseTemporaryBuffers();
        }

		internal void Update()
		{
			ReleaseTemporaryBuffers();
		}

		public int MaskCount
		{
			get { return masks.Count; }
		}

		public Transform ReflectionProbeAnchor
		{
			get { return reflectionProbeAnchor; }
			set { reflectionProbeAnchor = value; }
		}

		public void AddMask(Renderer mask)
		{
			mask.enabled = false;
			masks.Add(mask);
		}

		public void RemoveMask(Renderer mask)
		{
			masks.Remove(mask);
		}

		internal void OnValidate(Water water)
		{
			if(volumeFrontShader == null)
				volumeFrontShader = Shader.Find("PlayWay Water/Volumes/Front");

			if(volumeBackShader == null)
				volumeBackShader = Shader.Find("PlayWay Water/Volumes/Back");
        }

		private Dictionary<Camera, MaterialPropertyBlock> propertyBlocks = new Dictionary<Camera, MaterialPropertyBlock>();

		private MaterialPropertyBlock GetMaterialPropertyBlock(Camera camera)
		{
			MaterialPropertyBlock block;

			if(!propertyBlocks.TryGetValue(camera, out block))
				propertyBlocks[camera] = block = new MaterialPropertyBlock();

			return block;
		}

		public void Render(Camera camera, WaterGeometryType geometryType)
		{
			if(water == null || water.WaterMaterial == null || !water.isActiveAndEnabled)
				return;

			if((camera.cullingMask & (1 << water.gameObject.layer)) == 0)
				return;

			var waterCamera = WaterCamera.GetWaterCamera(camera);

			if((!water.Volume.Boundless && water.Volume.HasRenderableAdditiveVolumes) && ((object)waterCamera == null || !waterCamera.RenderVolumes))
				return;

			MaterialPropertyBlock propertyBlock;

			if((object)waterCamera == null || !waterCamera.IsEffectCamera || waterCamera.MainCamera == null)
			{
				propertyBlock = GetMaterialPropertyBlock(camera);
				//RenderMasks(camera, waterCamera, propertyBlock);
			}
			else
			{
				propertyBlock = GetMaterialPropertyBlock(waterCamera.MainCamera);
			}

			if((object)waterCamera != null && water.ReceiveShadows)
			{
				Vector2 min = new Vector2(0.0f, 0.0f);
				Vector2 max = new Vector2(1.0f, 1.0f);
				waterCamera.ReportShadowedWaterMinMaxRect(min, max);
            }

			water.OnWaterRender(camera);
			
			Matrix4x4 matrix;
			var meshes = water.Geometry.GetTransformedMeshes(camera, out matrix, geometryType, false, (object)waterCamera != null ? waterCamera.ForcedVertexCount : 0);

			for(int i = 0; i < meshes.Length; ++i)
			{
				Graphics.DrawMesh(meshes[i], matrix, water.WaterMaterial, water.gameObject.layer, camera, 0, propertyBlock, water.ShadowCastingMode, false, reflectionProbeAnchor == null ? water.transform : reflectionProbeAnchor);

				if((object)waterCamera == null || (waterCamera.ContainingWater != null && !waterCamera.IsEffectCamera))
					Graphics.DrawMesh(meshes[i], matrix, water.WaterBackMaterial, water.gameObject.layer, camera, 0, propertyBlock, water.ShadowCastingMode, false, reflectionProbeAnchor == null ? water.transform : reflectionProbeAnchor);
			}
		}

		public void PostRender(Camera camera)
		{
			if(water != null)
				water.OnWaterPostRender(camera);
		}

		public void OnSharedSubtractiveMaskRender(ref bool hasSubtractiveVolumes, ref bool hasAdditiveVolumes, ref bool hasFlatMasks)
		{
			var boundingVolumes = water.Volume.GetVolumesDirect();
			int numBoundingVolumes = boundingVolumes.Count;

			for(int i = 0; i < numBoundingVolumes; ++i)
				boundingVolumes[i].DisableRenderers();

			var subtractiveVolumes = water.Volume.GetSubtractiveVolumesDirect();
			int numSubtractiveVolumes = subtractiveVolumes.Count;

			if(useSharedMask)
			{
				for(int i = 0; i < numSubtractiveVolumes; ++i)
					subtractiveVolumes[i].EnableRenderers(false);

				int numMasks = masks.Count;

				for(int i = 0; i < numMasks; ++i)
					masks[i].enabled = true;

				hasSubtractiveVolumes = hasSubtractiveVolumes || water.Volume.GetSubtractiveVolumesDirect().Count != 0;
				hasAdditiveVolumes = hasAdditiveVolumes || numBoundingVolumes != 0;
				hasFlatMasks = hasFlatMasks || numMasks != 0;
			}
			else
			{
				for(int i = 0; i < numSubtractiveVolumes; ++i)
					subtractiveVolumes[i].DisableRenderers();
			}
		}

		public void OnSharedMaskAdditiveRender()
		{
			if(useSharedMask)
			{
				var boundingVolumes = water.Volume.GetVolumesDirect();
				int numBoundingVolumes = boundingVolumes.Count;

				for(int i = 0; i < numBoundingVolumes; ++i)
					boundingVolumes[i].EnableRenderers(false);

				var subtractiveVolumes = water.Volume.GetSubtractiveVolumesDirect();
				int numSubtractiveVolumes = subtractiveVolumes.Count;

				for(int i = 0; i < numSubtractiveVolumes; ++i)
					subtractiveVolumes[i].DisableRenderers();
			}
		}

		public void OnSharedMaskPostRender()
		{
			var boundingVolumes = water.Volume.GetVolumesDirect();
			int numBoundingVolumes = boundingVolumes.Count;

			for(int i = 0; i < numBoundingVolumes; ++i)
				boundingVolumes[i].EnableRenderers(true);

			var subtractiveVolumes = water.Volume.GetSubtractiveVolumesDirect();
			int numSubtractiveVolumes = subtractiveVolumes.Count;

			for(int i = 0; i < numSubtractiveVolumes; ++i)
				subtractiveVolumes[i].EnableRenderers(true);

			if(useSharedMask)
			{
				int numMasks = masks.Count;

				for(int i = 0; i < numMasks; ++i)
					masks[i].enabled = false;
			}
		}
		
		private void OnSomeCameraPreCull(Camera camera)
		{
#if UNITY_EDITOR
			if(WaterCamera.IsSceneViewCamera(camera))
			{
				if(WaterCamera.GetWaterCamera(camera, true) == null)
				{
					// changing hierarchy here ensures that added water camera to scene view camera will function properly; possibly there is some other way
					var g = new GameObject();
					g.AddComponent<MeshFilter>();
					g.AddComponent<MeshRenderer>();
					Object.DestroyImmediate(g);
				}
            }
#endif
		}

		private void ReleaseTemporaryBuffers()
		{
			//if(maskTexture != null)
			//{
			//	RenderTexture.ReleaseTemporary(maskTexture);
			//	maskTexture = null;
			//}
		}
		
		/*private void RenderMasks(Camera camera, WaterCamera waterCamera, MaterialPropertyBlock propertyBlock)
		{
			var subtractiveVolumes = water.Volume.GetVolumeSubtractorsDirect();
			var boundingVolumes = water.Volume.GetVolumesDirect();

			if((object)waterCamera == null || !waterCamera.RenderVolumes || (subtractiveVolumes.Count == 0 && boundingVolumes.Count == 0 && masks.Count == 0))
			{
				ReleaseTemporaryBuffers();
				return;
			}
			
			int tempLayer = WaterProjectSettings.Instance.WaterTempLayer;
			int waterLayer = WaterProjectSettings.Instance.WaterLayer;
			
			var effectsCamera = waterCamera.EffectsCamera;

			if(effectsCamera == null)
			{
				ReleaseTemporaryBuffers();
				return;
			}
			
			effectsCamera.CopyFrom(camera);
			effectsCamera.enabled = false;
			effectsCamera.GetComponent<WaterCamera>().enabled = false;
			effectsCamera.renderingPath = RenderingPath.Forward;
			effectsCamera.depthTextureMode = DepthTextureMode.None;
			effectsCamera.cullingMask = 1 << tempLayer;

			if(maskTexture == null)
				maskTexture = RenderTexture.GetTemporary(camera.pixelWidth, camera.pixelHeight, 16, SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat) ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear, 1);

			Graphics.SetRenderTarget(maskTexture);

			if(subtractiveVolumes.Count != 0)
			{
				int numsubtractiveVolumes = subtractiveVolumes.Count;
				for(int i = 0; i < numsubtractiveVolumes; ++i)
					subtractiveVolumes[i].SetLayer(tempLayer);

				var volumeFrontTexture = RenderTexturesCache.GetTemporary(camera.pixelWidth, camera.pixelHeight, 16, SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat) ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGBHalf, true, false);

				// render front pass of volumetric masks
				effectsCamera.clearFlags = CameraClearFlags.SolidColor;
				effectsCamera.backgroundColor = new Color(0.0f, 0.0f, 0.5f, 0.0f);
				effectsCamera.targetTexture = volumeFrontTexture;
				effectsCamera.RenderWithShader(volumeFrontShader, "CustomType");

				GL.Clear(true, true, new Color(0.0f, 0.0f, 0.0f, 0.0f), 0.0f);

				// render back pass of volumetric masks
				Shader.SetGlobalTexture("_VolumesFrontDepth", volumeFrontTexture);
				effectsCamera.clearFlags = CameraClearFlags.Nothing;
				effectsCamera.targetTexture = maskTexture;
				effectsCamera.RenderWithShader(volumeBackShader, "CustomType");

				volumeFrontTexture.Dispose();

				for(int i = 0; i < numsubtractiveVolumes; ++i)
					subtractiveVolumes[i].SetLayer(waterLayer);
			}

			if(boundingVolumes.Count != 0)
			{
				if(subtractiveVolumes.Count == 0)
					GL.Clear(true, true, new Color(0.0f, 0.0f, 0.0f, 0.0f), 1.0f);
				else
					GL.Clear(true, false, new Color(0.0f, 0.0f, 0.0f, 0.0f), 1.0f);

				int numBoundingVolumes = boundingVolumes.Count;
				for(int i = 0; i < numBoundingVolumes; ++i)
					boundingVolumes[i].SetLayer(tempLayer);

				// render additive volumes
				effectsCamera.clearFlags = CameraClearFlags.Nothing;
				effectsCamera.targetTexture = maskTexture;
				effectsCamera.RenderWithShader(volumeFrontAddShader, "CustomType");

				GL.Clear(true, false, new Color(0.0f, 0.0f, 0.0f, 0.0f), 0.0f);

				effectsCamera.clearFlags = CameraClearFlags.Nothing;
				effectsCamera.targetTexture = maskTexture;
				effectsCamera.RenderWithShader(volumeBackAddShader, "CustomType");
				
				for(int i=0; i<numBoundingVolumes; ++i)
					boundingVolumes[i].SetLayer(waterLayer);
			}

			if(masks.Count != 0)
			{
				if(subtractiveVolumes.Count == 0 && boundingVolumes.Count == 0)
					GL.Clear(false, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));

				int numMasks = masks.Count;
				for(int i=0; i<numMasks; ++i)
					masks[i].enabled = true;

				// render simple "screen-space" masks
				effectsCamera.clearFlags = CameraClearFlags.Nothing;
				effectsCamera.targetTexture = maskTexture;
				effectsCamera.Render();

				for(int i = 0; i < numMasks; ++i)
					masks[i].enabled = false;
			}

			effectsCamera.targetTexture = null;

			propertyBlock.SetTexture(waterMaskId, maskTexture);
			water.WaterVolumeMaterial.SetTexture(waterMaskId, maskTexture);
		}*/
	}
}
