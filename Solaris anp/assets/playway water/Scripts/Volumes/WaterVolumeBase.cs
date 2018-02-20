using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PlayWay.Water
{
	[ExecuteInEditMode]
	abstract public class WaterVolumeBase : MonoBehaviour
	{
		[SerializeField]
		private Water water;

		[SerializeField]
		private WaterVolumeMode mode = WaterVolumeMode.PhysicsAndSurfaceRendering;

#if UNITY_EDITOR
		[HideInInspector]
		[SerializeField]
		private float version;

		[Obsolete]
		[HideInInspector]
		[Tooltip("Renders water around collider below water level. Example of use may be a glass bottle displacing sea water. If you will not use this and still use subtractive volumes, there will be a hole in the water behind the bottle.\n\nIn case of subtractive volumes this will be buggy unless \"WindWaves / FFT / Flatten Mode\" is set to \"Forced On\".")]
		[SerializeField]
		private bool renderDisplacedWater;
#endif

		private Collider[] colliders;
		private MeshRenderer[] volumeRenderers;
		private float radius;

		void OnEnable()
		{
			colliders = GetComponents<Collider>();
			gameObject.layer = WaterProjectSettings.Instance.WaterCollidersLayer;

			Register(water);

			if(mode >= WaterVolumeMode.PhysicsAndSurfaceRendering && water != null && Application.isPlaying)
				CreateRenderers();
		}

		void OnDisable()
		{
			DisposeRenderers();
			Unregister(water);
		}

		public Water Water
		{
			get { return water; }
		}

		public WaterVolumeMode Mode
		{
			get { return mode; }
		}

		public MeshRenderer[] VolumeRenderers
		{
			get { return volumeRenderers; }
		}

		virtual protected CullMode CullMode
		{
			get { return CullMode.Back; }
		}

		abstract protected void Register(Water water);
		abstract protected void Unregister(Water water);

		void OnValidate()
		{
			colliders = GetComponents<Collider>();

			for(int i=0; i<colliders.Length; ++i)
			{
				var collider = colliders[i];

				if(!collider.isTrigger)
					collider.isTrigger = true;
			}
			
			PerformUpdate();
		}

		public void AssignTo(Water water)
		{
			if(this.water == water)
				return;

			DisposeRenderers();
			Unregister(water);
			this.water = water;
			Register(water);

			if(mode >= WaterVolumeMode.PhysicsAndSurfaceRendering && water != null && Application.isPlaying)
				CreateRenderers();
		}

		public void EnableRenderers(bool forBorderRendering)
		{
			if(volumeRenderers != null)
			{
				bool enable = !forBorderRendering || mode == WaterVolumeMode.PhysicsAndFullRendering;

				for(int i = 0; i < volumeRenderers.Length; ++i)
					volumeRenderers[i].enabled = enable;
			}
		}

		public void DisableRenderers()
		{
			if(volumeRenderers != null)
			{
				for(int i = 0; i < volumeRenderers.Length; ++i)
					volumeRenderers[i].enabled = false;
			}
		}

		internal void SetLayer(int layer)
		{
			if(volumeRenderers != null)
			{
				for(int i = 0; i < volumeRenderers.Length; ++i)
					volumeRenderers[i].gameObject.layer = layer;
			}
		}

		public bool IsPointInside(Vector3 point)
		{
			foreach(var collider in colliders)
			{
				if(collider.IsPointInside(point))
					return true;
			}

			return false;
		}

		private void DisposeRenderers()
		{
			if(volumeRenderers != null)
			{
				foreach(var renderer in volumeRenderers)
				{
					if(renderer != null)
						Destroy(renderer.gameObject);
				}

				volumeRenderers = null;
			}
		}

		virtual protected void CreateRenderers()
		{
			int numVolumes = colliders.Length;
			volumeRenderers = new MeshRenderer[numVolumes];

			var material = CullMode == CullMode.Back ? water.WaterVolumeMaterial : water.WaterVolumeBackMaterial;

			for(int i = 0; i < numVolumes; ++i)
			{
				var collider = colliders[i];

				GameObject rendererGo;

				if(collider is BoxCollider)
				{
					rendererGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
					rendererGo.transform.localScale = (collider as BoxCollider).size;
				}
				else if(collider is MeshCollider)
				{
					rendererGo = new GameObject();
					rendererGo.hideFlags = HideFlags.DontSave;

					var mf = rendererGo.AddComponent<MeshFilter>();
					mf.sharedMesh = (collider as MeshCollider).sharedMesh;

					rendererGo.AddComponent<MeshRenderer>();
				}
				else if(collider is SphereCollider)
				{
					float d = (collider as SphereCollider).radius * 2;

					rendererGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
					rendererGo.transform.localScale = new Vector3(d, d, d);
				}
				else if(collider is CapsuleCollider)
				{
					var capsuleCollider = collider as CapsuleCollider;
					float height = capsuleCollider.height * 0.5f;
					float radius = capsuleCollider.radius * 2.0f;

					rendererGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);

					switch(capsuleCollider.direction)
					{
						case 0:
						{
							rendererGo.transform.localEulerAngles = new Vector3(0.0f, 0.0f, 90.0f);
							rendererGo.transform.localScale = new Vector3(height, radius, radius);
							break;
						}

						case 1:
						{
							rendererGo.transform.localScale = new Vector3(radius, height, radius);
							break;
						}

						case 2:
						{
							rendererGo.transform.localEulerAngles = new Vector3(90.0f, 0.0f, 0.0f);
							rendererGo.transform.localScale = new Vector3(radius, radius, height);
							break;
						}
					}
				}
				else
					throw new InvalidOperationException("Unsupported collider type.");

				rendererGo.hideFlags = HideFlags.DontSave;
				rendererGo.name = "Volume Renderer";
				rendererGo.layer = WaterProjectSettings.Instance.WaterLayer;
				rendererGo.transform.SetParent(transform, false);

				Destroy(rendererGo.GetComponent<Collider>());

				var renderer = rendererGo.GetComponent<MeshRenderer>();
				renderer.sharedMaterial = material;
				renderer.shadowCastingMode = ShadowCastingMode.Off;
				renderer.receiveShadows = false;

				volumeRenderers[i] = renderer;
			}
		}

		private void PerformUpdate()
		{
#if UNITY_EDITOR
			if(version >= WaterProjectSettings.CurrentVersion)
				return;

			if (version < 1.14f)
			{
#pragma warning disable 612
				if (renderDisplacedWater)
					mode = WaterVolumeMode.PhysicsAndFullRendering;
#pragma warning restore 612
			}

			version = WaterProjectSettings.CurrentVersion;
#endif
		}

		public enum WaterVolumeMode
		{
			Physics,
			PhysicsAndSurfaceRendering,
			PhysicsAndFullRendering
		}
	}
}
