using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections;

namespace PlayWay.Water
{
	[ExecuteInEditMode]
	[RequireComponent(typeof(Water))]
	[AddComponentMenu("Water/Planar Reflections", 1)]
	public class WaterPlanarReflection : MonoBehaviour, IWaterRenderAware
	{
		[HideInInspector]
		[SerializeField]
		private Shader utilitiesShader;

		[SerializeField]
		private Camera reflectionCamera;

		[SerializeField]
		private bool reflectSkybox = true;

		[Range(1, 8)]
		[SerializeField]
		private int downsample = 2;

		[Range(1, 8)]
		[Tooltip("Allows you to use more rational resolution of planar reflections on screens with very high dpi. Planar reflections should be blurred anyway.")]
		[SerializeField]
		private int retinaDownsample = 3;

		[SerializeField]
		private LayerMask reflectionMask = int.MaxValue;

		[SerializeField]
		private bool highQuality = true;

		[SerializeField]
		private float clipPlaneOffset = 0.07f;

		private Water water;
		private TemporaryRenderTexture currentTarget;
		private bool systemSupportsHDR;
		private int finalDivider;
		private int reflectionTexProperty;
		private bool renderPlanarReflections;
		private Material utilitiesMaterial;

		private Dictionary<Camera, TemporaryRenderTexture> temporaryTargets = new Dictionary<Camera, TemporaryRenderTexture>();

		void Start()
		{
			reflectionTexProperty = Shader.PropertyToID("_PlanarReflectionTex");
			systemSupportsHDR = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf);

			OnValidate();
		}

		public bool ReflectSkybox
		{
			get { return reflectSkybox; }
			set { reflectSkybox = value; }
		}

		public LayerMask ReflectionMask
		{
			get { return reflectionMask; }
			set
			{
				if(reflectionMask == value)
					return;

				reflectionMask = value;

				if(reflectionCamera != null)
					reflectionCamera.cullingMask = reflectionMask;
			}
		}

		void OnEnable()
		{
			water = GetComponent<Water>();
			water.ProfilesChanged.AddListener(OnProfilesChanged);
			OnProfilesChanged(water);

			UpdateMaterial(water, WaterQualitySettings.Instance.CurrentQualityLevel);

			WaterQualitySettings.Instance.Changed -= OnQualityChange;
			WaterQualitySettings.Instance.Changed += OnQualityChange;
		}

		void OnDisable()
		{
			water.InvalidateMaterialKeywords();

			WaterQualitySettings.Instance.Changed -= OnQualityChange;
		}

		void OnValidate()
		{
			if(utilitiesShader == null)
				utilitiesShader = Shader.Find("PlayWay Water/Utilities/PlanarReflection - Utilities");

			int finalDivider = Screen.dpi <= 220 ? downsample : retinaDownsample;

			if(this.finalDivider != finalDivider)
			{
				this.finalDivider = finalDivider;
				ClearRenderTextures();
			}

			if(reflectionCamera != null)
				ValidateReflectionCamera();

			UpdateMaterial(GetComponent<Water>(), WaterQualitySettings.Instance.CurrentQualityLevel);
		}

		void OnDestroy()
		{
			ClearRenderTextures();
		}

		void Update()
		{
			ClearRenderTextures();
        }
		
		public void OnWaterRender(Camera camera)
		{
			if(camera == reflectionCamera || !enabled || !camera.enabled || !renderPlanarReflections)
				return;

			if(!temporaryTargets.TryGetValue(camera, out currentTarget))
			{
				RenderReflection(camera);

				var material = water.WaterMaterial;

				if(material != null)
				{
					material.SetTexture(reflectionTexProperty, currentTarget);
					material.SetMatrix("_PlanarReflectionProj", (Matrix4x4.TRS(new Vector3(0.5f, 0.5f, 0.0f), Quaternion.identity, new Vector3(0.5f, 0.5f, 1.0f)) * reflectionCamera.projectionMatrix * reflectionCamera.worldToCameraMatrix));
					material.SetFloat("_PlanarReflectionMipBias", -Mathf.Log(finalDivider, 2));
				}
			}
		}

		public void OnWaterPostRender(Camera camera)
		{
			TemporaryRenderTexture renderTexture;

			if(temporaryTargets.TryGetValue(camera, out renderTexture))
			{
				temporaryTargets.Remove(camera);
				renderTexture.Dispose();
			}
		}

		private void RenderReflection(Camera camera)
		{
			if(!enabled)
				return;

			if(reflectionCamera == null)
			{
				var reflectionCameraGo = new GameObject(name + " Reflection Camera");
				reflectionCameraGo.transform.parent = transform;

				reflectionCamera = reflectionCameraGo.AddComponent<Camera>();
				ValidateReflectionCamera();
			}

			reflectionCamera.hdr = systemSupportsHDR && camera.hdr;
			reflectionCamera.backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);

			currentTarget = GetRenderTexture(camera.pixelWidth, camera.pixelHeight);
			temporaryTargets[camera] = currentTarget;

			var target = RenderTexturesCache.GetTemporary(currentTarget.Texture.width, currentTarget.Texture.height, 16, currentTarget.Texture.format, true, false, false);
			reflectionCamera.targetTexture = target;
			reflectionCamera.aspect = camera.aspect;

			Vector3 cameraEuler = camera.transform.eulerAngles;
			reflectionCamera.transform.eulerAngles = new Vector3(-cameraEuler.x, cameraEuler.y, cameraEuler.z);
			reflectionCamera.transform.position = camera.transform.position;

			Vector3 cameraPosition = camera.transform.position;
			cameraPosition.y = transform.position.y - cameraPosition.y;
			reflectionCamera.transform.position = cameraPosition;

			float d = -transform.position.y - clipPlaneOffset;
			Vector4 reflectionPlane = new Vector4(0, 1, 0, d);

			Matrix4x4 reflection = Matrix4x4.zero;
			reflection = CalculateReflectionMatrix(reflection, reflectionPlane);
			Vector3 newpos = reflection.MultiplyPoint(camera.transform.position);

			reflectionCamera.worldToCameraMatrix = camera.worldToCameraMatrix * reflection;

			Vector4 clipPlane = CameraSpacePlane(reflectionCamera, transform.position, new Vector3(0, 1, 0), 1.0f);

			var matrix = camera.projectionMatrix;
			matrix = CalculateObliqueMatrix(matrix, clipPlane);
			reflectionCamera.projectionMatrix = matrix;

			reflectionCamera.transform.position = newpos;
			Vector3 cameraEulerB = camera.transform.eulerAngles;
			reflectionCamera.transform.eulerAngles = new Vector3(-cameraEulerB.x, cameraEulerB.y, cameraEulerB.z);

			reflectionCamera.clearFlags = reflectSkybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;

			GL.invertCulling = true;
			reflectionCamera.Render();
			GL.invertCulling = false;

			reflectionCamera.targetTexture = null;

			if(utilitiesMaterial == null)
			{
				utilitiesMaterial = new Material(utilitiesShader);
				utilitiesMaterial.hideFlags = HideFlags.DontSave;
			}

			Graphics.Blit(target, currentTarget, utilitiesMaterial, 0);
			target.Dispose();
		}

		private void ValidateReflectionCamera()
		{
			reflectionCamera.enabled = false;
			reflectionCamera.cullingMask = reflectionMask;
			reflectionCamera.renderingPath = RenderingPath.Forward;
			reflectionCamera.depthTextureMode = DepthTextureMode.None;
		}

		static Matrix4x4 CalculateReflectionMatrix(Matrix4x4 reflectionMat, Vector4 plane)
		{
			reflectionMat.m00 = (1.0f - 2.0f * plane[0] * plane[0]);
			reflectionMat.m01 = (-2.0f * plane[0] * plane[1]);
			reflectionMat.m02 = (-2.0f * plane[0] * plane[2]);
			reflectionMat.m03 = (-2.0f * plane[3] * plane[0]);

			reflectionMat.m10 = (-2.0f * plane[1] * plane[0]);
			reflectionMat.m11 = (1.0f - 2.0f * plane[1] * plane[1]);
			reflectionMat.m12 = (-2.0f * plane[1] * plane[2]);
			reflectionMat.m13 = (-2.0f * plane[3] * plane[1]);

			reflectionMat.m20 = (-2.0f * plane[2] * plane[0]);
			reflectionMat.m21 = (-2.0f * plane[2] * plane[1]);
			reflectionMat.m22 = (1.0f - 2.0f * plane[2] * plane[2]);
			reflectionMat.m23 = (-2.0f * plane[3] * plane[2]);

			reflectionMat.m30 = 0.0f;
			reflectionMat.m31 = 0.0f;
			reflectionMat.m32 = 0.0f;
			reflectionMat.m33 = 1.0f;

			return reflectionMat;
		}

		Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
		{
			Vector3 offsetPos = pos + normal * clipPlaneOffset;
			Matrix4x4 m = cam.worldToCameraMatrix;
			Vector3 cpos = m.MultiplyPoint(offsetPos);
			Vector3 cnormal = m.MultiplyVector(normal).normalized * sideSign;

			return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
		}

		static Matrix4x4 CalculateObliqueMatrix(Matrix4x4 projection, Vector4 clipPlane)
		{
			Vector4 q = projection.inverse * new Vector4(Mathf.Sign(clipPlane.x), Mathf.Sign(clipPlane.y), 1.0f, 1.0f);
			Vector4 c = clipPlane * (2.0f / (Vector4.Dot(clipPlane, q)));
			
			projection[2] = c.x - projection[3];
			projection[6] = c.y - projection[7];
			projection[10] = c.z - projection[11];
			projection[14] = c.w - projection[15];

			if (UnityEngine.VR.VRSettings.enabled)
			{
				// it seems that there is some bug in Unity code that causes weird behaviour when any of these components is set to non-zero
				projection[2] = 0.0f;
				projection[6] = 0.0f;
			}

			return projection;
		}

		private TemporaryRenderTexture GetRenderTexture(int width, int height)
		{
			int adaptedWidth = Mathf.ClosestPowerOfTwo(width / finalDivider);
			int adaptedHeight = Mathf.ClosestPowerOfTwo(height / finalDivider);

			var renderTexture = RenderTexturesCache.GetTemporary(adaptedWidth, adaptedHeight, 0, reflectionCamera.hdr && systemSupportsHDR ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32, true, false, true);
			renderTexture.Texture.filterMode = FilterMode.Trilinear;
			renderTexture.Texture.wrapMode = TextureWrapMode.Clamp;

			return renderTexture;
		}

		private void ClearRenderTextures()
		{
			var enumerator = temporaryTargets.GetEnumerator();
			while(enumerator.MoveNext())
				enumerator.Current.Value.Dispose();

			temporaryTargets.Clear();
		}

		private void OnProfilesChanged(Water water)
		{
			var profiles = water.Profiles;

			if(profiles == null)
				return;

			float intensity = 0.0f;

			foreach(var weightedProfile in profiles)
			{
				var profile = weightedProfile.profile;
				float weight = weightedProfile.weight;

				intensity += profile.PlanarReflectionIntensity * weight;
			}

			renderPlanarReflections = intensity > 0.0f;
		}

		private void OnQualityChange()
		{
			UpdateMaterial(water, WaterQualitySettings.Instance.CurrentQualityLevel);
		}

		public void UpdateMaterial(Water water, WaterQualityLevel qualityLevel)
		{
			
		}

		public void BuildShaderVariant(ShaderVariant variant, Water water, WaterQualityLevel qualityLevel)
		{
			variant.SetWaterKeyword("_PLANAR_REFLECTIONS", enabled && (!highQuality || !qualityLevel.allowHighQualityReflections));
			variant.SetWaterKeyword("_PLANAR_REFLECTIONS_HQ", enabled && highQuality && qualityLevel.allowHighQualityReflections);
		}
	}
}