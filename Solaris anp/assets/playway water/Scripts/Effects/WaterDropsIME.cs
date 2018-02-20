using UnityEngine;

namespace PlayWay.Water
{
	[RequireComponent(typeof(UnderwaterIME))]
	[ExecuteInEditMode]
	public class WaterDropsIME : MonoBehaviour, IWaterImageEffect
	{
		[HideInInspector]
		[SerializeField]
		private Shader waterDropsShader;

		[Header("Drops")]
		[SerializeField]
		private Texture2D normalMap;

		[SerializeField]
		private float intensity = 1.0f;

		[Header("Blur")]
		[Tooltip("Replace water drops effect with a temporary blur, if you prefer to simulate human vision reaction.")]
		[SerializeField]
		private bool useBlur;

		[SerializeField]
		private float blurFadeSpeed = 1.0f;

		[SerializeField]
		private Blur blur;

		private Material overlayMaterial;
		private RenderTexture maskA;
		private RenderTexture maskB;
		private WaterCamera waterCamera;
		private UnderwaterIME underwaterIME;
		private float disableTime;
		private float blurIntensity;
		private bool effectEnabled = true;
		
		void Awake()
		{
			waterCamera = GetComponent<WaterCamera>();
			underwaterIME = GetComponent<UnderwaterIME>();
			OnValidate();
		}

		public bool EffectEnabled
		{
			get { return effectEnabled; }
			set { effectEnabled = value; }
		}
		
		public float Intensity
		{
			get { return intensity; }
			set { intensity = value; }
		}

		public Texture2D NormalMap
		{
			get { return normalMap; }
			set
			{
				normalMap = value;

				if(overlayMaterial != null)
					overlayMaterial.SetTexture("_NormalMap", normalMap);
			}
		}

		void OnValidate()
		{
			if(waterDropsShader == null)
				waterDropsShader = Shader.Find("PlayWay Water/IME/Water Drops");

			blur.Validate("PlayWay Water/Utilities/Blur (VisionBlur)");
		}
		
		void OnRenderImage(RenderTexture source, RenderTexture destination)
		{
			CheckResources();

			if(!useBlur)
			{
				Graphics.Blit(maskA, maskB, overlayMaterial, 0);

				overlayMaterial.SetFloat("_Intensity", intensity);
				overlayMaterial.SetTexture("_Mask", maskB);
				overlayMaterial.SetTexture("_SubtractiveMask", waterCamera.SubtractiveMask);

#if UNITY_EDITOR
				overlayMaterial.SetTexture("_NormalMap", normalMap);
#endif

				Graphics.Blit(source, destination, overlayMaterial, 1);
			}
			else
			{
				float blurSize = blur.Size;
				blur.Size *= blurIntensity;
				blur.Apply(source);
				blur.Size = blurSize;

				Graphics.Blit(source, destination);
			}

			SwapMasks();
		}

		private void CheckResources()
		{
			if(overlayMaterial == null)
			{
				overlayMaterial = new Material(waterDropsShader);
				overlayMaterial.hideFlags = HideFlags.DontSave;
				overlayMaterial.SetTexture("_NormalMap", normalMap);
			}

			if(maskA == null || maskA.width != Screen.width >> 1 || maskA.height != Screen.height >> 1)
			{
				maskA = CreateMaskRT();
				maskB = CreateMaskRT();
			}
		}

		private RenderTexture CreateMaskRT()
		{
			var renderTexture = new RenderTexture(Screen.width >> 1, Screen.height >> 1, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
			renderTexture.hideFlags = HideFlags.DontSave;
			renderTexture.filterMode = FilterMode.Bilinear;

			Graphics.SetRenderTarget(renderTexture);
			GL.Clear(false, true, Color.black);

			return renderTexture;
		}

		private void SwapMasks()
		{
			var t = maskA;
			maskA = maskB;
			maskB = t;
		}

		public void OnWaterCameraEnabled()
		{
			
		}

		public void OnWaterCameraPreCull()
		{
			if(underwaterIME.enabled)
				disableTime = Time.time + 6.0f;
			
			if(useBlur)
			{
				blurIntensity += Mathf.Max(0.0f, waterCamera.WaterLevel - transform.position.y);
				blurIntensity *= 1.0f - Time.deltaTime * blurFadeSpeed;
				
				if(blurIntensity > 1.0f) blurIntensity = 1.0f;
				else if(blurIntensity < 0.0f) blurIntensity = 0.0f;

				enabled = blurIntensity > 0.004f && effectEnabled;
            }
			else
				enabled = intensity > 0 && Time.time <= disableTime && effectEnabled;
		}
	}
}
