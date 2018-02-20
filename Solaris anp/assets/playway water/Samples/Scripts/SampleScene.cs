using PlayWay.Water;
using UnityEngine;

namespace PlayWay.WaterSamples
{
	public class SampleScene : MonoBehaviour
	{
		[SerializeField]
		private Water.Water water;

		[SerializeField]
		private WaterProfile calmShoreWater;

		[SerializeField]
		private WaterProfile calmSeaWater;

		[SerializeField]
		private WaterProfile choppySeaWater;

		[SerializeField]
		private WaterProfile breezeSeaWater;

		[SerializeField]
		private WaterProfile stormSeaWater;

		[SerializeField]
		private ReflectionProbe reflectionProbe;

		[SerializeField]
		private Light sun;

		[SerializeField]
		private AmbientGradient ambient1;

		[SerializeField]
		private AmbientGradient ambient2;

		private WaterProfile source, target;
		private float sourceSunIntensity, targetSunIntensity;
		private float sourceExposure, targetExposure;
		private float profileChangeTime = float.PositiveInfinity;
		private float transitionDuration;
		private AmbientGradient sourceAmbient, targetAmbient;

		private bool environmentDirty;

		void Start()
		{
			WaterQualitySettings.Instance.Changed -= OnQualitySettingsChanged;
			WaterQualitySettings.Instance.Changed += OnQualitySettingsChanged;

			// ensure that there won't be a hiccup when profiles are to be used for the first time
			water.CacheProfiles(calmShoreWater, calmSeaWater, choppySeaWater, stormSeaWater, breezeSeaWater);

			//DisableIMEs();
		}

		public void ChangeProfile0A()
		{
			water.SetProfiles(new Water.Water.WeightedProfile(breezeSeaWater, 1.0f));

			// duplicate skybox material to not carry on later changes from play mode
			RenderSettings.skybox = Instantiate(RenderSettings.skybox);
			sun.transform.RotateAround(Vector3.zero, Vector3.up, 10);
			RenderSettings.skybox.SetFloat("_Rotation", 290.0f);        // we will do some fake sky darkening later /// it's better to move the sun out of the view ;)

		}

		public void ChangeProfile0()
		{
			TweenProfiles(choppySeaWater, calmSeaWater, sun.intensity, RenderSettings.skybox.GetFloat("_Exposure"), null, 0.01f);

			RenderSettings.fog = false;
		}

		public void ChangeProfile1()
		{
			TweenProfiles(calmSeaWater, choppySeaWater, 0.75f, 0.78f, ambient1, 2.0f);
        }

		public void ChangeProfile2()
		{
			TweenProfiles(choppySeaWater, stormSeaWater, 0.55f, 0.54f, ambient2, 2.0f);
		}
		
		private void TweenProfiles(WaterProfile source, WaterProfile target, float sunIntensity, float exposure, AmbientGradient ambientGradient, float transitionDuration)
		{
			this.sourceAmbient = new AmbientGradient(RenderSettings.ambientGroundColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientSkyColor);
			this.targetAmbient = ambientGradient;
			this.transitionDuration = transitionDuration;
            this.sourceSunIntensity = sun.intensity;
			this.targetSunIntensity = sunIntensity;
			this.sourceExposure = RenderSettings.skybox.GetFloat("_Exposure");
			this.targetExposure = exposure;
            this.source = source;
			this.target = target;
			water.SetProfiles(new Water.Water.WeightedProfile(source, 1.0f), new Water.Water.WeightedProfile(target, 0.0f));
			profileChangeTime = Time.time;
		}

		void Update()
		{
			if(Time.time >= profileChangeTime)
			{
				// animated transition between profiles
				float t = (Time.time - profileChangeTime) / transitionDuration;

				if(t > 1.0f) t = 1.0f;

				water.SetProfiles(new Water.Water.WeightedProfile(source, 1.0f - t), new Water.Water.WeightedProfile(target, t));
				sun.intensity = Mathf.Lerp(sourceSunIntensity, targetSunIntensity, t);
				RenderSettings.skybox.SetFloat("_Exposure", Mathf.Lerp(sourceExposure, targetExposure, t));

				if(targetAmbient != null)
				{
					RenderSettings.ambientGroundColor = Color.Lerp(sourceAmbient.groundColor, targetAmbient.groundColor, t);
					RenderSettings.ambientEquatorColor = Color.Lerp(sourceAmbient.equatorColor, targetAmbient.equatorColor, t);
					RenderSettings.ambientSkyColor = Color.Lerp(sourceAmbient.skyColor, targetAmbient.skyColor, t);
				}

				if(t != 1.0f)
				{
					environmentDirty = true;
                }
				else
					profileChangeTime = float.PositiveInfinity;
            }
				
			// update environment every 4th frame // it's kinda slow
			if(environmentDirty && Time.frameCount % 4 == 0)
				RefreshEnvironment();
		}

		void OnDestroy()
		{
			WaterQualitySettings.Instance.Changed -= OnQualitySettingsChanged;
		}

		private void OnQualitySettingsChanged()
		{
			// ensure that there won't be a hiccup when profiles are to be used for the first time
			water.CacheProfiles(calmShoreWater, calmSeaWater, choppySeaWater, stormSeaWater, breezeSeaWater);
		}

		private void RefreshEnvironment()
		{
			reflectionProbe.RenderProbe();
			environmentDirty = false;
        }

		private void DisableIMEs()
		{
			/*var scions = GetComponentsInChildren<ScionEngine.ScionPostProcess>(true);

			foreach(var scion in scions)
				scion.enabled = false;

			var dofs = GetComponentsInChildren<UnityStandardAssets.ImageEffects.DepthOfField>(true);

			foreach(var dof in dofs)
				dof.enabled = false;

			var antialiasings = GetComponentsInChildren<UnityStandardAssets.ImageEffects.Antialiasing>(true);

			foreach(var antialiasing in antialiasings)
				antialiasing.enabled = false;

			var sunShafts = GetComponentsInChildren<UnityStandardAssets.ImageEffects.SunShafts>(true);

			foreach(var sunShaft in sunShafts)
				sunShaft.enabled = false;

			var underwaters = GetComponentsInChildren<UnderwaterIME>(true);

			foreach(var underwater in underwaters)
				underwater.enabled = false;*/
		}

		[System.Serializable]
		public class AmbientGradient
		{
			public Color groundColor;
			public Color equatorColor;
			public Color skyColor;

			public AmbientGradient(Color groundColor, Color equatorColor, Color skyColor)
			{
				this.groundColor = groundColor;
				this.equatorColor = equatorColor;
				this.skyColor = skyColor;
			}
		}
	}
}
