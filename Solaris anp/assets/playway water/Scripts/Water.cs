using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace PlayWay.Water
{
	/// <summary>
	/// Main water component.
	/// </summary>
	[ExecuteInEditMode]
	[AddComponentMenu("Water/Water (Base Component)", -1)]
	public class Water : MonoBehaviour, IShaderCollectionClient
	{
		[SerializeField]
		private WaterProfile profile;

		[SerializeField]
		private Shader waterShader;

		[SerializeField]
		private Shader waterVolumeShader;

		[SerializeField]
		private bool refraction = true;

		[SerializeField]
		private bool blendEdges = true;

		[SerializeField]
		private bool volumetricLighting = true;

		[Tooltip("Affects direct light specular and diffuse components. Shadows currently work only for main directional light and you need to attach WaterShadowCastingLight script to it. Also it doesn't work at all on mobile platforms.")]
		[SerializeField]
		private bool receiveShadows;

		[SerializeField]
		private ShaderCollection shaderCollection;

		[SerializeField]
		private ShadowCastingMode shadowCastingMode;

		[SerializeField]
		private bool useCubemapReflections = true;

		[Tooltip("Set it to anything else than 0 if your game has multiplayer functionality or you want your water to behave the same way each time your game is played (good for intro etc.).")]
		[SerializeField]
		private int seed = 0;

		[Tooltip("May hurt performance on some systems.")]
		[Range(0.0f, 1.0f)]
		[SerializeField]
		private float tesselationFactor = 1.0f;

		[SerializeField]
		private float refractionMaxDepth = -1.0f;

		[SerializeField]
		private WaterUvAnimator uvAnimator;

		[SerializeField]
		private WaterVolume volume;

		[SerializeField]
		private WaterGeometry geometry;

		[SerializeField]
		private WaterRenderer waterRenderer;

		[SerializeField]
		private WaterEvent profilesChanged;

		[SerializeField]
		private Material waterMaterialPrefab;

		[SerializeField]
		private Material waterVolumeMaterialPrefab;

#if UNITY_EDITOR
#pragma warning disable 0414
		[SerializeField]
		private float version = 1.0f;
#pragma warning restore 0414

		// used to identify this water object for the purpose of shader collection building
		[SerializeField]
		private int sceneHash = -1;

		private int instanceId = -1;
#endif

		private WeightedProfile[] profiles;
		private bool profilesDirty;

		// keeping front/back materials is necessary for the time being, because setting culling mode from property blocks is not possible
		private Material waterMaterial;
		private Material waterBackMaterial;
		private Material waterVolumeMaterial;
		private Material waterVolumeBackMaterial;

		private float horizontalDisplacementScale;
		private float gravity;
		private float directionality;
		private float density;
		private float underwaterBlurSize;
		private float underwaterDistortionsIntensity;
		private float underwaterDistortionAnimationSpeed;
		private float time = -1;
		private Color underwaterAbsorptionColor;
		private float maxHorizontalDisplacement;
		private float maxVerticalDisplacement;
		private int waterId;
		private int surfaceOffsetId;
		private int activeSamplesCount;
		private WaterProfile runtimeProfile;
		private LaunchState launchState = LaunchState.Disabled;
		private Vector2 surfaceOffset = new Vector2(float.NaN, float.NaN);
		private IWaterRenderAware[] renderAwareComponents;
		private IWaterDisplacements[] displacingComponents;

		static private int nextWaterId = 1;

		static private string[] parameterNames = new string[] {
			"_AbsorptionColor", "_Color", "_SpecColor", "_DepthColor", "_EmissionColor", "_ReflectionColor", "_DisplacementsScale", "_Glossiness",
			"_SubsurfaceScatteringPack", "_WrapSubsurfaceScatteringPack", "_RefractionDistortion", "_SpecularFresnelBias", "_DetailFadeFactor",
			"_DisplacementNormalsIntensity", "_EdgeBlendFactorInv", "_PlanarReflectionPack", "_BumpScale", "_FoamTiling", "_LightSmoothnessMul",
			"_BumpMap", "_FoamTex", "_FoamNormalMap",
			"_FoamSpecularColor", "_RefractionMaxDepth"
		};

		static private string[] disallowedVolumeKeywords = new string[] {
			"_WAVES_FFT_SLOPE", "_WAVES_GERSTNER", "_WATER_FOAM_WS", "_PLANAR_REFLECTIONS", "_PLANAR_REFLECTIONS_HQ",
			"_INCLUDE_SLOPE_VARIANCE", "_NORMALMAP", "_PROJECTION_GRID", "_WATER_OVERLAYS", "_WAVES_ALIGN", "_TRIANGLES",
			"_BOUNDED_WATER"
		};

		static private string[] hardwareDependentKeywords = new string[] {
			/*"_INCLUDE_SLOPE_VARIANCE", */"_WATER_FOAM_WS", "_WATER_RECEIVE_SHADOWS"
		};

		private int[] parameterHashes;

		private ParameterOverride<Vector4>[] vectorOverrides;
		private ParameterOverride<Color>[] colorOverrides;
		private ParameterOverride<float>[] floatOverrides;
		private ParameterOverride<Texture>[] textureOverrides;

		void Awake()
		{
			waterId = nextWaterId++;

			bool inserted = (volume == null);

			CreateWaterManagers();

			if(inserted)
			{
				gameObject.layer = WaterProjectSettings.Instance.WaterLayer;

#if UNITY_EDITOR
				// add default components only in editor, out of play mode
				if(!Application.isPlaying)
					AddDefaultComponents();

				version = WaterProjectSettings.CurrentVersion;
#endif
			}

			CreateParameterHashes();
			renderAwareComponents = GetComponents<IWaterRenderAware>();
			displacingComponents = GetComponents<IWaterDisplacements>();

			if(!Application.isPlaying)
				return;

			if(profile == null)
			{
				Debug.LogError("Water profile is not set. You may assign it in the inspector.");
				gameObject.SetActive(false);
				return;
			}

			try
			{
				CreateMaterials();

				if(profiles == null)
				{
					profiles = new WeightedProfile[] { new WeightedProfile(profile, 1.0f) };
					ResolveProfileData(profiles);
				}

				uvAnimator.Start(this);
				profilesChanged.AddListener(OnProfilesChanged);
			}
			catch(System.Exception e)
			{
				Debug.LogError(e);
				gameObject.SetActive(false);
			}
		}

		void Start()
		{
			launchState = LaunchState.Started;
			SetupMaterials();

			if(profiles != null)
				ResolveProfileData(profiles);

			profilesDirty = true;
		}

		public int WaterId
		{
			get { return waterId; }
		}

		public Material WaterMaterial
		{
			get
			{
				if(waterMaterial == null)
					CreateMaterials();

				return waterMaterial;
			}
		}

		public Material WaterBackMaterial
		{
			get
			{
				if(waterBackMaterial == null)
					CreateMaterials();

				return waterBackMaterial;
			}
		}

		public Material WaterVolumeMaterial
		{
			get
			{
				if(waterVolumeMaterial == null)
					CreateMaterials();

				return waterVolumeMaterial;
			}
		}

		public Material WaterVolumeBackMaterial
		{
			get
			{
				if(waterVolumeBackMaterial == null)
					CreateMaterials();

				return waterVolumeBackMaterial;
			}
		}

		/// <summary>
		/// Currently set water profiles with their associated weights.
		/// </summary>
		public WeightedProfile[] Profiles
		{
			get { return profiles; }
		}

		public float HorizontalDisplacementScale
		{
			get { return horizontalDisplacementScale; }
		}

		public bool ReceiveShadows
		{
			get { return receiveShadows; }
		}

		public ShadowCastingMode ShadowCastingMode
		{
			get { return shadowCastingMode; }
		}

		public float Gravity
		{
			get { return gravity; }
		}

		public float Directionality
		{
			get { return directionality; }
		}

		public float UniformWaterScale
		{
			get { return transform.localScale.y; }
		}

		public Color UnderwaterAbsorptionColor
		{
			get { return underwaterAbsorptionColor; }
		}

		public bool VolumetricLighting
		{
			get { return volumetricLighting; }
		}

		public bool FinalVolumetricLighting
		{
			get { return volumetricLighting && WaterQualitySettings.Instance.AllowVolumetricLighting; }
		}

		/// <summary>
		/// Count of WaterSample instances targetted on this water.
		/// </summary>
		public int ComputedSamplesCount
		{
			get { return activeSamplesCount; }
		}

		/// <summary>
		/// Event invoked when profiles change.
		/// </summary>
		public WaterEvent ProfilesChanged
		{
			get { return profilesChanged; }
		}

		/// <summary>
		/// Retrieves a WaterVolume of this water. It's one of the classes providing basic water functionality.
		/// </summary>
		public WaterVolume Volume
		{
			get { return volume; }
		}

		/// <summary>
		/// Retrieves a WaterGeometry of this water. It's one of the classes providing basic water functionality.
		/// </summary>
		public WaterGeometry Geometry
		{
			get { return geometry; }
		}

		/// <summary>
		/// Retrieves a WaterRenderer of this water. It's one of the classes providing basic water functionality.
		/// </summary>
		public WaterRenderer Renderer
		{
			get { return waterRenderer; }
		}

		public int Seed
		{
			get { return seed; }
			set { seed = value; }
		}

		public float Density
		{
			get { return density; }
		}

		public float UnderwaterBlurSize
		{
			get { return underwaterBlurSize; }
		}

		public float UnderwaterDistortionsIntensity
		{
			get { return underwaterDistortionsIntensity; }
		}

		public float UnderwaterDistortionAnimationSpeed
		{
			get { return underwaterDistortionAnimationSpeed; }
		}

		public ShaderCollection ShaderCollection
		{
			get { return shaderCollection; }
		}

		public float MaxHorizontalDisplacement
		{
			get { return maxHorizontalDisplacement; }
		}

		public float MaxVerticalDisplacement
		{
			get { return maxVerticalDisplacement; }
		}

		public float Time
		{
			get { return time == -1 ? UnityEngine.Time.time : time; }
			set { time = value; }
		}

		public Vector2 SurfaceOffset
		{
			get { return float.IsNaN(surfaceOffset.x) ? new Vector2(-transform.position.x, -transform.position.z) : surfaceOffset; }
			set { surfaceOffset = value; }
		}

		public Color AbsorptionColor
		{
			get { return waterMaterial.GetColor(parameterHashes[(int)ColorParameter.AbsorptionColor]); }
			set { SetColor(ColorParameter.AbsorptionColor, value); }
		}

		public Color DiffuseColor
		{
			get { return waterMaterial.GetColor(parameterHashes[(int)ColorParameter.DiffuseColor]); }
			set { SetColor(ColorParameter.DiffuseColor, value); }
		}

		public Color SpecularColor
		{
			get { return waterMaterial.GetColor(parameterHashes[(int)ColorParameter.SpecularColor]); }
			set { SetColor(ColorParameter.SpecularColor, value); }
		}

		public Color DepthColor
		{
			get { return waterMaterial.GetColor(parameterHashes[(int)ColorParameter.DepthColor]); }
			set { SetColor(ColorParameter.DepthColor, value); }
		}

		public Color EmissionColor
		{
			get { return waterMaterial.GetColor(parameterHashes[(int)ColorParameter.EmissionColor]); }
			set { SetColor(ColorParameter.EmissionColor, value); }
		}

		public Color ReflectionColor
		{
			get { return waterMaterial.GetColor(parameterHashes[(int)ColorParameter.ReflectionColor]); }
			set { SetColor(ColorParameter.ReflectionColor, value); }
		}

		public float SubsurfaceScattering
		{
			get { return waterMaterial.GetVector(parameterHashes[(int)VectorParameter.SubsurfaceScatteringPack]).x; }
			set
			{
				Vector4 v = waterMaterial.GetVector(parameterHashes[(int)VectorParameter.SubsurfaceScatteringPack]);
				v.x = value;
				SetVector(VectorParameter.SubsurfaceScatteringPack, v);
			}
		}

		void OnEnable()
		{
			launchState = LaunchState.Started;

			CreateParameterHashes();
			ValidateShaders();

#if UNITY_EDITOR
			if(!IsNotCopied())
				shaderCollection = null;

			instanceId = GetInstanceID();
#else
			if(Application.isPlaying)
				shaderCollection = null;
#endif

			CreateMaterials();

			if(profiles == null && profile != null)
			{
				profiles = new WeightedProfile[] { new WeightedProfile(profile, 1.0f) };
				ResolveProfileData(profiles);
			}

			WaterQualitySettings.Instance.Changed -= OnQualitySettingsChanged;
			WaterQualitySettings.Instance.Changed += OnQualitySettingsChanged;

			WaterGlobals.Instance.AddWater(this);

			if(geometry != null)
			{
				geometry.OnEnable(this);
				waterRenderer.OnEnable(this);
				volume.OnEnable(this);
			}

			//if(Application.isPlaying)
			//	SetupMaterials();
		}

		void OnDisable()
		{
			WaterGlobals.Instance.RemoveWater(this);

			geometry.OnDisable();
			waterRenderer.OnDisable();
			volume.OnDisable();
		}

		void OnDestroy()
		{
			WaterQualitySettings.Instance.Changed -= OnQualitySettingsChanged;
		}

		/// <summary>
		/// Computes water displacement vector at a given coordinates. WaterSample class does the same thing asynchronously and is recommended.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="z"></param>
		/// <param name="spectrumStart"></param>
		/// <param name="spectrumEnd"></param>
		/// <param name="time"></param>
		/// <returns></returns>
		public Vector3 GetDisplacementAt(float x, float z, float spectrumStart, float spectrumEnd, float time)
		{
			Vector3 result = new Vector3();

			for(int i = 0; i < displacingComponents.Length; ++i)
				result += displacingComponents[i].GetDisplacementAt(x, z, spectrumStart, spectrumEnd, time);

			return result;
		}

		/// <summary>
		/// Computes horizontal displacement vector at a given coordinates. WaterSample class does the same thing asynchronously and is recommended.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="z"></param>
		/// <param name="spectrumStart"></param>
		/// <param name="spectrumEnd"></param>
		/// <param name="time"></param>
		/// <returns></returns>
		public Vector2 GetHorizontalDisplacementAt(float x, float z, float spectrumStart, float spectrumEnd, float time)
		{
			Vector2 result = new Vector2();

			for(int i = 0; i < displacingComponents.Length; ++i)
				result += displacingComponents[i].GetHorizontalDisplacementAt(x, z, spectrumStart, spectrumEnd, time);

			return result;
		}

		/// <summary>
		/// Computes height at a given coordinates. WaterSample class does the same thing asynchronously and is recommended.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="z"></param>
		/// <param name="spectrumStart"></param>
		/// <param name="spectrumEnd"></param>
		/// <param name="time"></param>
		/// <returns></returns>
		public float GetHeightAt(float x, float z, float spectrumStart, float spectrumEnd, float time)
		{
			float result = 0.0f;

			for(int i = 0; i < displacingComponents.Length; ++i)
				result += displacingComponents[i].GetHeightAt(x, z, spectrumStart, spectrumEnd, time);

			return result;
		}

		/// <summary>
		/// Computes forces and height at a given coordinates. WaterSample class does the same thing asynchronously and is recommended.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="z"></param>
		/// <param name="spectrumStart"></param>
		/// <param name="spectrumEnd"></param>
		/// <param name="time"></param>
		/// <returns></returns>
		public Vector4 GetHeightAndForcesAt(float x, float z, float spectrumStart, float spectrumEnd, float time)
		{
			Vector4 result = Vector4.zero;

			for(int i = 0; i < displacingComponents.Length; ++i)
				result += displacingComponents[i].GetForceAndHeightAt(x, z, spectrumStart, spectrumEnd, time);

			return result;
		}

		/// <summary>
		/// Caches profiles for later use to avoid hiccups.
		/// </summary>
		/// <param name="profiles"></param>
		public void CacheProfiles(params WaterProfile[] profiles)
		{
			var windWaves = GetComponent<WindWaves>();

			if(windWaves != null)
			{
				foreach(var profile in profiles)
					windWaves.SpectrumResolver.CacheSpectrum(profile.Spectrum);
			}
		}

		public void SetProfiles(params WeightedProfile[] profiles)
		{
			ValidateProfiles(profiles);

			this.profiles = profiles;
			profilesDirty = true;
		}

		public void InvalidateMaterialKeywords()
		{

		}

		private void CreateMaterials()
		{
			if(waterMaterial == null)
			{
				if(waterMaterialPrefab == null)
					waterMaterial = new Material(waterShader);
				else
					waterMaterial = Instantiate(waterMaterialPrefab);

				waterMaterial.hideFlags = HideFlags.DontSave;
				waterMaterial.SetVector("_WaterId", new Vector4(1 << waterId, 1 << (waterId + 1), 0, 0));
				waterMaterial.SetFloat("_WaterStencilId", waterId);
				waterMaterial.SetFloat("_WaterStencilIdInv", (~waterId) & 255);
			}

			if(waterBackMaterial == null)
			{
				if(waterMaterialPrefab == null)
					waterBackMaterial = new Material(waterShader);
				else
					waterBackMaterial = Instantiate(waterMaterialPrefab);

				waterBackMaterial.hideFlags = HideFlags.DontSave;
				UpdateBackMaterial();
			}

			bool updateVolumeMaterials = false;

			if(waterVolumeMaterial == null)
			{
				if(waterVolumeMaterialPrefab == null)
					waterVolumeMaterial = new Material(waterVolumeShader);
				else
					waterVolumeMaterial = Instantiate(waterVolumeMaterialPrefab);

				waterVolumeMaterial.hideFlags = HideFlags.DontSave;
				updateVolumeMaterials = true;
			}

			if(waterVolumeBackMaterial == null)
			{
				if(waterVolumeMaterialPrefab == null)
					waterVolumeBackMaterial = new Material(waterVolumeShader);
				else
					waterVolumeBackMaterial = Instantiate(waterVolumeMaterialPrefab);

				waterVolumeBackMaterial.hideFlags = HideFlags.DontSave;
				updateVolumeMaterials = true;
			}

			if(updateVolumeMaterials)
				UpdateWaterVolumeMaterials();
		}

		private void SetupMaterials()
		{
			if(launchState == LaunchState.Disabled)
				return;

			var waterQualitySettings = WaterQualitySettings.Instance;

			// front and back material
			var variant = new ShaderVariant();
			BuildShaderVariant(variant, waterQualitySettings.CurrentQualityLevel);
			
			ValidateShaderCollection(variant);

			if(shaderCollection != null)
				waterMaterial.shader = shaderCollection.GetShaderVariant(variant.GetWaterKeywords(), variant.GetUnityKeywords(), variant.GetAdditionalCode(), variant.GetKeywordsString(), false);
			else
				waterMaterial.shader = ShaderCollection.GetRuntimeShaderVariant(variant.GetKeywordsString(), false);

			waterMaterial.shaderKeywords = variant.GetUnityKeywords();
			UpdateMaterials();
			UpdateBackMaterial();

			// volume material
			foreach(string keyword in disallowedVolumeKeywords)
				variant.SetWaterKeyword(keyword, false);
			
			if(shaderCollection != null)
				waterVolumeMaterial.shader = shaderCollection.GetShaderVariant(variant.GetWaterKeywords(), variant.GetUnityKeywords(), variant.GetAdditionalCode(), variant.GetKeywordsString(), true);
			else
				waterVolumeMaterial.shader = ShaderCollection.GetRuntimeShaderVariant(variant.GetKeywordsString(), true);

			waterVolumeBackMaterial.shader = waterVolumeMaterial.shader;
            UpdateWaterVolumeMaterials();
			waterVolumeBackMaterial.shaderKeywords = waterVolumeMaterial.shaderKeywords = variant.GetUnityKeywords();
		}

		private void SetShader(ref Material material, Shader shader)
		{

		}

		private void ValidateShaderCollection(ShaderVariant variant)
		{
#if UNITY_EDITOR
			if(!Application.isPlaying && shaderCollection != null && !shaderCollection.ContainsShaderVariant(variant.GetKeywordsString()))
				RebuildShaders();
#endif
		}

		[ContextMenu("Rebuild Shaders")]
		private void RebuildShaders()
		{
#if UNITY_EDITOR
			if(shaderCollection == null)
			{
				Debug.LogError("You have to create a shader collection first.");
				return;
			}

			shaderCollection.Build();
#endif
		}

		private void UpdateBackMaterial()
		{
			if(waterBackMaterial != null)
			{
				waterBackMaterial.shader = waterMaterial.shader;
				waterBackMaterial.CopyPropertiesFromMaterial(waterMaterial);
				waterBackMaterial.SetFloat(parameterHashes[11], 0.0f);                       // air has IOR of 1.0, fresnel bias should be 0.0
				waterBackMaterial.EnableKeyword("_WATER_BACK");
				waterBackMaterial.SetFloat("_Cull", 1);
			}
		}

		private void UpdateWaterVolumeMaterials()
		{
			if(waterVolumeMaterial != null)
			{
				waterVolumeMaterial.CopyPropertiesFromMaterial(waterMaterial);
				waterVolumeBackMaterial.CopyPropertiesFromMaterial(waterMaterial);
				waterVolumeBackMaterial.renderQueue = waterVolumeMaterial.renderQueue = (refraction || blendEdges) ? 2991 : 2000;
				waterVolumeBackMaterial.SetFloat("_Cull", 1);
			}
		}

		internal void SetVectorDirect(int materialPropertyId, Vector4 vector)
		{
			waterMaterial.SetVector(materialPropertyId, vector);
			waterBackMaterial.SetVector(materialPropertyId, vector);
			waterVolumeMaterial.SetVector(materialPropertyId, vector);
		}

		internal void SetFloatDirect(int materialPropertyId, float value)
		{
			waterMaterial.SetFloat(materialPropertyId, value);
			waterBackMaterial.SetFloat(materialPropertyId, value);
			waterVolumeMaterial.SetFloat(materialPropertyId, value);
		}

		public bool SetKeyword(string keyword, bool enable)
		{
			if(waterMaterial != null)
			{
				if(enable)
				{
					if(!waterMaterial.IsKeywordEnabled(keyword))
					{
						waterMaterial.EnableKeyword(keyword);
						waterBackMaterial.EnableKeyword(keyword);
						waterVolumeMaterial.EnableKeyword(keyword);
						return true;
					}
				}
				else
				{
					if(waterMaterial.IsKeywordEnabled(keyword))
					{
						waterMaterial.DisableKeyword(keyword);
						waterBackMaterial.DisableKeyword(keyword);
						waterVolumeMaterial.DisableKeyword(keyword);
						return true;
					}
				}
			}

			return false;
		}

		public void OnValidate()
		{
			ValidateShaders();

			renderAwareComponents = GetComponents<IWaterRenderAware>();
			displacingComponents = GetComponents<IWaterDisplacements>();

			if(waterMaterial == null)
				return;                 // wait for OnEnable

			CreateParameterHashes();

			if(profiles != null && profiles.Length != 0 && runtimeProfile == profile)
				ResolveProfileData(profiles);
			else if(profile != null)
			{
				runtimeProfile = profile;
				ResolveProfileData(new WeightedProfile[] { new WeightedProfile(profile, 1.0f) });
			}

			geometry.OnValidate(this);
			waterRenderer.OnValidate(this);

			SetupMaterials();
		}

		private void ValidateShaders()
		{
			if(waterShader == null)
				waterShader = Shader.Find("PlayWay Water/Standard");

			if(waterVolumeShader == null)
				waterVolumeShader = Shader.Find("PlayWay Water/Standard Volume");
		}

		private void ResolveProfileData(WeightedProfile[] profiles)
		{
			WaterProfile topProfile = profiles[0].profile;
			float topWeight = 0.0f;

			foreach(var weightedProfile in profiles)
			{
				if(topProfile == null || topWeight < weightedProfile.weight)
				{
					topProfile = weightedProfile.profile;
					topWeight = weightedProfile.weight;
				}
			}

			horizontalDisplacementScale = 0.0f;
			gravity = 0.0f;
			directionality = 0.0f;
			density = 0.0f;
			underwaterBlurSize = 0.0f;
			underwaterDistortionsIntensity = 0.0f;
			underwaterDistortionAnimationSpeed = 0.0f;
			underwaterAbsorptionColor = new Color(0.0f, 0.0f, 0.0f);

			Color absorptionColor = new Color(0.0f, 0.0f, 0.0f);
			Color diffuseColor = new Color(0.0f, 0.0f, 0.0f);
			Color specularColor = new Color(0.0f, 0.0f, 0.0f);
			Color depthColor = new Color(0.0f, 0.0f, 0.0f);
			Color emissionColor = new Color(0.0f, 0.0f, 0.0f);
			Color reflectionColor = new Color(0.0f, 0.0f, 0.0f);
			Color foamSpecularColor = new Color(0.0f, 0.0f, 0.0f);

			float smoothness = 0.0f;
			float ambientSmoothness = 0.0f;
			float subsurfaceScattering = 0.0f;
			float refractionDistortion = 0.0f;
			float fresnelBias = 0.0f;
			float detailFadeDistance = 0.0f;
			float displacementNormalsIntensity = 0.0f;
			float edgeBlendFactor = 0.0f;
			float directionalWrapSSS = 0.0f;
			float pointWrapSSS = 0.0f;

			Vector3 planarReflectionPack = new Vector3();
			Vector2 foamTiling = new Vector2();
			var normalMapAnimation1 = new NormalMapAnimation();
			var normalMapAnimation2 = new NormalMapAnimation();
			
			for(int i=0; i<profiles.Length; ++i)
			{
				var profile = profiles[i].profile;
				float weight = profiles[i].weight;

				horizontalDisplacementScale += profile.HorizontalDisplacementScale * weight;
				gravity -= profile.Gravity * weight;
				directionality += profile.Directionality * weight;
				density += profile.Density * weight;
				underwaterBlurSize += profile.UnderwaterBlurSize * weight;
				underwaterDistortionsIntensity += profile.UnderwaterDistortionsIntensity * weight;
				underwaterDistortionAnimationSpeed += profile.UnderwaterDistortionAnimationSpeed * weight;
				underwaterAbsorptionColor += profile.UnderwaterAbsorptionColor * weight;

				absorptionColor += profile.AbsorptionColor * weight;
				diffuseColor += profile.DiffuseColor * weight;
				specularColor += profile.SpecularColor * weight;
				depthColor += profile.DepthColor * weight;
				emissionColor += profile.EmissionColor * weight;
				reflectionColor += profile.ReflectionColor * weight;
				foamSpecularColor += profile.FoamSpecularColor * weight;

				smoothness += profile.Smoothness * weight;
				ambientSmoothness += profile.AmbientSmoothness * weight;
				subsurfaceScattering += profile.SubsurfaceScattering * weight;
				refractionDistortion += profile.RefractionDistortion * weight;
				fresnelBias += profile.FresnelBias * weight;
				detailFadeDistance += profile.DetailFadeDistance * weight;
				displacementNormalsIntensity += profile.DisplacementNormalsIntensity * weight;
				edgeBlendFactor += profile.EdgeBlendFactor * weight;
				directionalWrapSSS += profile.DirectionalWrapSSS * weight;
				pointWrapSSS += profile.PointWrapSSS * weight;

				planarReflectionPack.x += profile.PlanarReflectionIntensity * weight;
				planarReflectionPack.y += profile.PlanarReflectionFlatten * weight;
				planarReflectionPack.z += profile.PlanarReflectionVerticalOffset * weight;

				foamTiling += profile.FoamTiling * weight;
				normalMapAnimation1 += profile.NormalMapAnimation1 * weight;
				normalMapAnimation2 += profile.NormalMapAnimation2 * weight;
			}

			var windWaves = GetComponent<WindWaves>();

			if(windWaves != null && windWaves.FinalRenderMode == WaveSpectrumRenderMode.GerstnerAndFFTSlope)
				displacementNormalsIntensity *= 0.5f;

			// apply to materials
			waterMaterial.SetColor(parameterHashes[0], absorptionColor);                    // _AbsorptionColor
			waterMaterial.SetColor(parameterHashes[1], diffuseColor);                       // _Color
			waterMaterial.SetColor(parameterHashes[2], specularColor);                      // _SpecColor
			waterMaterial.SetColor(parameterHashes[3], depthColor);                         // _DepthColor
			waterMaterial.SetColor(parameterHashes[4], emissionColor);                      // _EmissionColor
			waterMaterial.SetColor(parameterHashes[5], reflectionColor);                    // _ReflectionColor
			waterMaterial.SetColor(parameterHashes[22], foamSpecularColor);					// _FoamSpecularColor
			waterMaterial.SetFloat(parameterHashes[6], horizontalDisplacementScale);        // _DisplacementsScale

			waterMaterial.SetFloat(parameterHashes[7], ambientSmoothness);                         // _Glossiness
			waterMaterial.SetVector(parameterHashes[8], new Vector4(subsurfaceScattering, 0.15f, 1.65f, 0.0f));             // _SubsurfaceScatteringPack
			waterMaterial.SetVector(parameterHashes[9], new Vector4(directionalWrapSSS, 1.0f / (1.0f + directionalWrapSSS), pointWrapSSS, 1.0f / (1.0f + pointWrapSSS)));           // _WrapSubsurfaceScatteringPack
			waterMaterial.SetFloat(parameterHashes[10], refractionDistortion);               // _RefractionDistortion
			waterMaterial.SetFloat(parameterHashes[11], fresnelBias);                       // _SpecularFresnelBias
			waterMaterial.SetFloat(parameterHashes[12], detailFadeDistance);                // _DetailFadeFactor
			waterMaterial.SetFloat(parameterHashes[13], displacementNormalsIntensity);      // _DisplacementNormalsIntensity
			waterMaterial.SetFloat(parameterHashes[14], 1.0f / edgeBlendFactor);            // _EdgeBlendFactorInv
			waterMaterial.SetVector(parameterHashes[15], planarReflectionPack);             // _PlanarReflectionPack
			waterMaterial.SetVector(parameterHashes[16], new Vector4(normalMapAnimation1.Intensity, normalMapAnimation2.Intensity, -(normalMapAnimation1.Intensity + normalMapAnimation2.Intensity) * 0.5f, 0.0f));             // _BumpScale
			waterMaterial.SetVector(parameterHashes[17], new Vector2(foamTiling.x / normalMapAnimation1.Tiling.x, foamTiling.y / normalMapAnimation1.Tiling.y));                    // _FoamTiling
			waterMaterial.SetFloat(parameterHashes[18], smoothness / ambientSmoothness);    // _LightSmoothnessMul

			waterMaterial.SetTexture(parameterHashes[19], topProfile.NormalMap);            // _BumpMap
			waterMaterial.SetTexture(parameterHashes[20], topProfile.FoamDiffuseMap);       // _FoamTex
			waterMaterial.SetTexture(parameterHashes[21], topProfile.FoamNormalMap);        // _FoamNormalMap
			
			uvAnimator.NormalMapAnimation1 = normalMapAnimation1;
			uvAnimator.NormalMapAnimation2 = normalMapAnimation2;

			SetKeyword("_EMISSION", emissionColor.grayscale != 0);

			if(vectorOverrides != null)
				ApplyOverridenParameters();

			UpdateBackMaterial();
			UpdateWaterVolumeMaterials();
		}

		private void ApplyOverridenParameters()
		{
			for(int i = 0; i < vectorOverrides.Length; ++i)
				waterMaterial.SetVector(vectorOverrides[i].hash, vectorOverrides[i].value);

			for(int i = 0; i < floatOverrides.Length; ++i)
				waterMaterial.SetFloat(floatOverrides[i].hash, floatOverrides[i].value);

			for(int i = 0; i < colorOverrides.Length; ++i)
				waterMaterial.SetColor(colorOverrides[i].hash, colorOverrides[i].value);

			for(int i = 0; i < textureOverrides.Length; ++i)
				waterMaterial.SetTexture(textureOverrides[i].hash, textureOverrides[i].value);
		}

		void Update()
		{
			if(!Application.isPlaying) return;
			
			transform.eulerAngles = new Vector3(0.0f, transform.eulerAngles.y, 0.0f);

			UpdateStatisticalData();

			uvAnimator.Update();
			geometry.Update();
			waterRenderer.Update();

			FireEvents();

			if(launchState != LaunchState.Ready)
			{
				SetupMaterials();
				launchState = LaunchState.Ready;
			}

#if WATER_DEBUG
			if(Input.GetKeyDown(KeyCode.F10))
				WaterDebug.WriteAllMaps(this);
#endif
		}

		public void OnWaterRender(Camera camera)
		{
			if(!isActiveAndEnabled) return;

			Vector2 surfaceOffset2d = SurfaceOffset;
			Vector4 surfaceOffset = new Vector4(surfaceOffset2d.x, transform.position.y, surfaceOffset2d.y, UniformWaterScale);
			waterMaterial.SetVector(surfaceOffsetId, surfaceOffset);
			waterBackMaterial.SetVector(surfaceOffsetId, surfaceOffset);
			waterVolumeMaterial.SetVector(surfaceOffsetId, surfaceOffset);
			waterVolumeBackMaterial.SetVector(surfaceOffsetId, surfaceOffset);

			for(int i = 0; i < renderAwareComponents.Length; ++i)
			{
				var component = renderAwareComponents[i];

				if(((MonoBehaviour)component) != null && ((MonoBehaviour)component).enabled)
					component.OnWaterRender(camera);
			}
		}

		public void OnWaterPostRender(Camera camera)
		{
			for(int i=0; i<renderAwareComponents.Length; ++i)
			{
				var component = renderAwareComponents[i];

                if(((MonoBehaviour)component) != null && ((MonoBehaviour)component).enabled)
					component.OnWaterPostRender(camera);
			}
		}

		internal void OnSamplingStarted()
		{
			++activeSamplesCount;
		}

		internal void OnSamplingStopped()
		{
			--activeSamplesCount;
		}

		private void AddDefaultComponents()
		{
			if(GetComponent<WaterPlanarReflection>() == null)
				gameObject.AddComponent<WaterPlanarReflection>();

			if(GetComponent<WindWaves>() == null)
				gameObject.AddComponent<WindWaves>();

			if(GetComponent<WaterFoam>() == null)
				gameObject.AddComponent<WaterFoam>();
		}

		private bool IsNotCopied()
		{
#if UNITY_EDITOR
#if UNITY_5_2 || UNITY_5_1 || UNITY_5_0
			if(string.IsNullOrEmpty(UnityEditor.EditorApplication.currentScene))
#else
			if(!gameObject.scene.path.StartsWith("Assets"))         // check if that's not a temporary scene used for a build
#endif
				return true;

#if UNITY_5_2 || UNITY_5_1 || UNITY_5_0
			string sceneName = UnityEditor.EditorApplication.currentScene + "#" + name;
#else
			string sceneName = gameObject.scene.name;
#endif

			var md5 = System.Security.Cryptography.MD5.Create();
			var hash = md5.ComputeHash(System.Text.Encoding.ASCII.GetBytes(sceneName));
			return instanceId == GetInstanceID() || sceneHash == System.BitConverter.ToInt32(hash, 0);
#else
			return true;
#endif
		}

		private void OnQualitySettingsChanged()
		{
			OnValidate();
			profilesDirty = true;
		}

		private void FireEvents()
		{
			if(profilesDirty)
			{
				profilesDirty = false;
				profilesChanged.Invoke(this);
			}
		}

		void OnProfilesChanged(Water water)
		{
			ResolveProfileData(profiles);
		}

		private void ValidateProfiles(WeightedProfile[] profiles)
		{
			if(profiles.Length == 0)
				throw new System.ArgumentException("Water has to use at least one profile.");

			float tileSize = profiles[0].profile.TileSize;

			for(int i = 1; i < profiles.Length; ++i)
			{
				if(profiles[i].profile.TileSize != tileSize)
				{
					Debug.LogError("TileSize varies between used water profiles. It is the only parameter that you should keep equal on all profiles used at a time.");
					break;
				}
			}
		}

		private void CreateParameterHashes()
		{
			if(parameterHashes != null && parameterHashes.Length == parameterNames.Length)
				return;

			surfaceOffsetId = Shader.PropertyToID("_SurfaceOffset");

			int numParameters = parameterNames.Length;
			parameterHashes = new int[numParameters];

			for(int i = 0; i < numParameters; ++i)
				parameterHashes[i] = Shader.PropertyToID(parameterNames[i]);
		}

		private void BuildShaderVariant(ShaderVariant variant, WaterQualityLevel qualityLevel)
		{
			if(renderAwareComponents == null)
				return;             // still not properly initialized

			bool blendEdges = this.blendEdges && qualityLevel.allowAlphaBlending;
			bool refraction = this.refraction && qualityLevel.allowAlphaBlending;
			bool alphaBlend = (refraction || blendEdges);

			for(int i = 0; i < renderAwareComponents.Length; ++i)
				renderAwareComponents[i].BuildShaderVariant(variant, this, qualityLevel);

			variant.SetWaterKeyword("_WATER_REFRACTION", refraction);
			variant.SetWaterKeyword("_VOLUMETRIC_LIGHTING", volumetricLighting && qualityLevel.allowVolumetricLighting);
			variant.SetWaterKeyword("_CUBEMAP_REFLECTIONS", useCubemapReflections);
			variant.SetWaterKeyword("_NORMALMAP", waterMaterial.GetTexture("_BumpMap") != null);
			variant.SetWaterKeyword("_WATER_RECEIVE_SHADOWS", receiveShadows);

			//variant.SetWaterKeyword("_ALPHATEST_ON", false);
			variant.SetWaterKeyword("_ALPHABLEND_ON", alphaBlend);
			variant.SetWaterKeyword("_ALPHAPREMULTIPLY_ON", !alphaBlend);

			variant.SetUnityKeyword("_BOUNDED_WATER", !volume.Boundless && volume.HasRenderableAdditiveVolumes);
			variant.SetUnityKeyword("_TRIANGLES", geometry.Triangular);
		}

		private void UpdateMaterials()
		{
			var qualityLevel = WaterQualitySettings.Instance.CurrentQualityLevel;

			for(int i = 0; i < renderAwareComponents.Length; ++i)
				renderAwareComponents[i].UpdateMaterial(this, qualityLevel);

			bool blendEdges = this.blendEdges && qualityLevel.allowAlphaBlending;
			bool refraction = this.refraction && qualityLevel.allowAlphaBlending;
			bool alphaBlend = (refraction || blendEdges);

			waterMaterial.SetFloat("_Cull", 2);

			waterMaterial.SetOverrideTag("RenderType", alphaBlend ? "Transparent" : "Opaque");
			waterMaterial.SetFloat("_Mode", alphaBlend ? 2 : 0);
			waterMaterial.SetInt("_SrcBlend", (int)(alphaBlend ? BlendMode.SrcAlpha : BlendMode.One));
			waterMaterial.SetInt("_DstBlend", (int)(alphaBlend ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
			waterMaterial.renderQueue = alphaBlend ? 2990 : 2000;       // 2000 - geometry, 3000 - transparent

			float maxTesselationFactor = Mathf.Sqrt(2000000.0f / geometry.TesselatedBaseVertexCount);
			waterMaterial.SetFloat("_TesselationFactor", Mathf.Lerp(1.0f, maxTesselationFactor, Mathf.Min(tesselationFactor, qualityLevel.maxTesselationFactor)));

			waterMaterial.SetFloat(parameterHashes[23], refractionMaxDepth);            // _RefractionMaxDepth
		}

		private void AddShaderVariants(ShaderCollection collection)
		{
			var qualityLevels = WaterQualitySettings.Instance.GetQualityLevelsDirect();

            for(int i=0; i<qualityLevels.Length; ++i)
			{
				SetProgress((float)i / qualityLevels.Length);

				var qualityLevel = qualityLevels[i];

				var variant = new ShaderVariant();

				// main shader
				BuildShaderVariant(variant, qualityLevel);

				collection.GetShaderVariant(variant.GetWaterKeywords(), variant.GetUnityKeywords(), variant.GetAdditionalCode(), variant.GetKeywordsString(), false);

				AddFallbackVariants(variant, collection, false, 0);

				SetProgress((i + 0.5f) / qualityLevels.Length);

				// volume shader
				foreach(string keyword in disallowedVolumeKeywords)
					variant.SetWaterKeyword(keyword, false);

				collection.GetShaderVariant(variant.GetWaterKeywords(), variant.GetUnityKeywords(), variant.GetAdditionalCode(), variant.GetKeywordsString(), true);

				AddFallbackVariants(variant, collection, true, 0);
			}

			SetProgress(1.0f);
		}

		private void SetProgress(float progress)
		{
#if UNITY_EDITOR
			if(progress != 1.0f)
				UnityEditor.EditorUtility.DisplayProgressBar("Building water shaders...", "This may take a minute.", progress);
			else
				UnityEditor.EditorUtility.ClearProgressBar();
#endif
		}

		private void AddFallbackVariants(ShaderVariant variant, ShaderCollection collection, bool volume, int index)
		{
			if(index < hardwareDependentKeywords.Length)
			{
				string keyword = hardwareDependentKeywords[index];

				AddFallbackVariants(variant, collection, volume, index + 1);

				if(variant.IsWaterKeywordEnabled(keyword))
				{
					variant.SetWaterKeyword(keyword, false);
					AddFallbackVariants(variant, collection, volume, index + 1);
					variant.SetWaterKeyword(keyword, true);
				}
			}
			else
			{
				collection.GetShaderVariant(variant.GetWaterKeywords(), variant.GetUnityKeywords(), variant.GetAdditionalCode(), variant.GetKeywordsString(), volume);
			}
		}

		private void CreateWaterManagers()
		{
			if(uvAnimator == null)
				uvAnimator = new WaterUvAnimator();

			if(volume == null)
				volume = new WaterVolume();

			if(geometry == null)
				geometry = new WaterGeometry();

			if(waterRenderer == null)
				waterRenderer = new WaterRenderer();

			if(profilesChanged == null)
				profilesChanged = new WaterEvent();
		}

		public void Write(ShaderCollection collection)
		{
			if(collection == shaderCollection && waterMaterial != null)
				AddShaderVariants(collection);
		}

		private void UpdateStatisticalData()
		{
			maxHorizontalDisplacement = 0.0f;
			maxVerticalDisplacement = 0.0f;

			for(int i=0; i<displacingComponents.Length; ++i)
			{
				maxHorizontalDisplacement += displacingComponents[i].MaxHorizontalDisplacement;
				maxVerticalDisplacement += displacingComponents[i].MaxVerticalDisplacement;
			}
		}

		private void SetVector(VectorParameter parameter, Vector4 value)
		{
			InitializeOverrides();

			int hash = parameterHashes[(int)parameter];
			waterMaterial.SetVector(hash, value);
			waterBackMaterial.SetVector(hash, value);
			waterVolumeMaterial.SetVector(hash, value);
			waterVolumeBackMaterial.SetVector(hash, value);

			for(int i = 0; i < vectorOverrides.Length; ++i)
			{
				if(vectorOverrides[i].hash == hash)
				{
					vectorOverrides[i].value = value;
					return;
				}
			}

			System.Array.Resize(ref vectorOverrides, vectorOverrides.Length + 1);
			vectorOverrides[vectorOverrides.Length - 1] = new ParameterOverride<Vector4>(hash, value);
		}

		private void SetColor(ColorParameter parameter, Color value)
		{
			InitializeOverrides();

			int hash = parameterHashes[(int)parameter];
			waterMaterial.SetColor(hash, value);
			waterBackMaterial.SetColor(hash, value);
			waterVolumeMaterial.SetColor(hash, value);
			waterVolumeBackMaterial.SetColor(hash, value);

			for(int i = 0; i < colorOverrides.Length; ++i)
			{
				if(colorOverrides[i].hash == hash)
				{
					colorOverrides[i].value = value;
					return;
				}
			}

			System.Array.Resize(ref colorOverrides, colorOverrides.Length + 1);
			colorOverrides[colorOverrides.Length - 1] = new ParameterOverride<Color>(hash, value);
		}

		private void SetFloat(FloatParameter parameter, float value)
		{
			InitializeOverrides();

			int hash = parameterHashes[(int)parameter];
			waterMaterial.SetFloat(hash, value);
			waterBackMaterial.SetFloat(hash, value);
			waterVolumeMaterial.SetFloat(hash, value);
			waterVolumeBackMaterial.SetFloat(hash, value);

			for(int i = 0; i < floatOverrides.Length; ++i)
			{
				if(floatOverrides[i].hash == hash)
				{
					floatOverrides[i].value = value;
					return;
				}
			}

			System.Array.Resize(ref floatOverrides, floatOverrides.Length + 1);
			floatOverrides[floatOverrides.Length - 1] = new ParameterOverride<float>(hash, value);
		}

		private void SetTexture(TextureParameter parameter, Texture value)
		{
			InitializeOverrides();

			int hash = parameterHashes[(int)parameter];
			waterMaterial.SetTexture(hash, value);
			waterBackMaterial.SetTexture(hash, value);
			waterVolumeMaterial.SetTexture(hash, value);
			waterVolumeBackMaterial.SetTexture(hash, value);

			for(int i = 0; i < textureOverrides.Length; ++i)
			{
				if(textureOverrides[i].hash == hash)
				{
					textureOverrides[i].value = value;
					return;
				}
			}

			System.Array.Resize(ref textureOverrides, textureOverrides.Length + 1);
			textureOverrides[textureOverrides.Length - 1] = new ParameterOverride<Texture>(hash, value);
		}

		static private Collider[] collidersBuffer = new Collider[30];
		static private List<Water> possibleWaters = new List<Water>();
		static private List<Water> excludedWaters = new List<Water>();

		static public Water FindWater(Vector3 position, float radius)
		{
			bool unused1, unused2;
			return FindWater(position, radius, out unused1, out unused2);
		}

		static public Water FindWater(Vector3 position, float radius, out bool isInsideSubtractiveVolume, out bool isInsideAdditiveVolume)
		{
			isInsideSubtractiveVolume = false;
			isInsideAdditiveVolume = false;

#if UNITY_5_2 || UNITY_5_1 || UNITY_5_0
			var collidersBuffer = Physics.OverlapSphere(position, radius, 1 << WaterProjectSettings.Instance.WaterCollidersLayer, QueryTriggerInteraction.Collide);
			int numHits = collidersBuffer.Length;
#else
			int numHits = Physics.OverlapSphereNonAlloc(position, radius, collidersBuffer, 1 << WaterProjectSettings.Instance.WaterCollidersLayer, QueryTriggerInteraction.Collide);
#endif

			possibleWaters.Clear();
			excludedWaters.Clear();

			for(int i = 0; i < numHits; ++i)
			{
				var volume = collidersBuffer[i].GetComponent<WaterVolumeBase>();

				if(volume != null)
				{
					if(volume is WaterVolumeAdd)
					{
						isInsideAdditiveVolume = true;
						possibleWaters.Add(volume.Water);
					}
					else                // subtractive
					{
						isInsideSubtractiveVolume = true;
						excludedWaters.Add(volume.Water);
					}
				}
			}

			for(int i = 0; i < possibleWaters.Count; ++i)
			{
				if(!excludedWaters.Contains(possibleWaters[i]))
					return possibleWaters[i];
			}

			var boundlessWaters = WaterGlobals.Instance.BoundlessWaters;
			int numBoundlessWaters = boundlessWaters.Count;

			for(int i = 0; i < numBoundlessWaters; ++i)
			{
				if(boundlessWaters[i].Volume.IsPointInsideMainVolume(position, radius) && !excludedWaters.Contains(boundlessWaters[i]))
					return boundlessWaters[i];
			}

			return null;
		}

		private void InitializeOverrides()
		{
			if(vectorOverrides == null)
			{
				vectorOverrides = new ParameterOverride<Vector4>[0];
				floatOverrides = new ParameterOverride<float>[0];
				colorOverrides = new ParameterOverride<Color>[0];
				textureOverrides = new ParameterOverride<Texture>[0];
			}
		}

		[ContextMenu("Print Used Keywords")]
		protected void PrintUsedKeywords()
		{
			Debug.Log(waterMaterial.shader.name);
		}

		[System.Serializable]
		public class WaterEvent : UnityEvent<Water> { };

		public struct WeightedProfile
		{
			public WaterProfile profile;
			public float weight;

			public WeightedProfile(WaterProfile profile, float weight)
			{
				this.profile = profile;
				this.weight = weight;
			}
		}

		public struct ParameterOverride<T>
		{
			public int hash;
			public T value;

			public ParameterOverride(int hash, T value)
			{
				this.hash = hash;
				this.value = value;
			}
		}
		
		public enum ColorParameter
		{
			AbsorptionColor = 0,
			DiffuseColor = 1,
			SpecularColor = 2,
			DepthColor = 3,
			EmissionColor = 4,
			ReflectionColor = 5
		}

		public enum FloatParameter
		{
			DisplacementScale = 6,
			Glossiness = 7,
			RefractionDistortion = 10,
			SpecularFresnelBias = 11,
			DisplacementNormalsIntensity = 13,
			EdgeBlendFactorInv = 14,
			LightSmoothnessMultiplier = 18
		}

		public enum VectorParameter
		{
			/// <summary>
			/// x = subsurfaceScattering, y = 0.15f, z = 1.65f, w = unused
			/// </summary>
			SubsurfaceScatteringPack = 8,

			/// <summary>
			/// x = directionalWrapSSS, y = 1.0f / (1.0f + directionalWrapSSS), z = pointWrapSSS, w = 1.0f / (1.0f + pointWrapSSS)
			/// </summary>
			WrapSubsurfaceScatteringPack = 9,
			DetailFadeFactor = 12,
			PlanarReflectionPack = 15,
			BumpScale = 16,
			FoamTiling = 17
		}

		public enum TextureParameter
		{
			BumpMap = 19,
			FoamTex = 20,
			FoamNormalMap = 21
		}

		public enum LaunchState
		{
			Disabled,
			Started,
			Ready
		}
	}
}
