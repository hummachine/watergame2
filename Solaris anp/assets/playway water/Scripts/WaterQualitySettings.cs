using System.Collections.Generic;
using UnityEngine;

namespace PlayWay.Water
{
	public class WaterQualitySettings : ScriptableObjectSingleton
	{
		[SerializeField]
		private WaterQualityLevel[] qualityLevels;
		
		[SerializeField]
		private bool synchronizeWithUnity = true;

		[SerializeField]
		private int savedCustomQualityLevel;

		public event System.Action Changed;

		private int waterQualityIndex;
		private WaterQualityLevel currentQualityLevel;          // working copy of the current quality level free for temporary modifications

		static private WaterQualitySettings instance;

		static public WaterQualitySettings Instance
		{
			get
			{
				if((object)instance == null)
				{
					instance = LoadSingleton<WaterQualitySettings>();
					instance.Changed = null;
					instance.waterQualityIndex = -1;
					instance.SynchronizeQualityLevel();
				}

				return instance;
			}
		}

		public string[] Names
		{
			get
			{
				string[] names = new string[qualityLevels.Length];

				for(int i = 0; i < qualityLevels.Length; ++i)
					names[i] = qualityLevels[i].name;

				return names;
			}
		}

		public int MaxSpectrumResolution
		{
			get { return currentQualityLevel.maxSpectrumResolution; }
			set
			{
				if(currentQualityLevel.maxSpectrumResolution == value)
					return;

				currentQualityLevel.maxSpectrumResolution = value;
				OnChange();
			}
		}

		public float TileSizeScale
		{
			get { return currentQualityLevel.tileSizeScale; }
			set
			{
				if(currentQualityLevel.tileSizeScale == value)
					return;

				currentQualityLevel.tileSizeScale = value;
				OnChange();
			}
		}

		public bool AllowHighPrecisionTextures
		{
			get { return currentQualityLevel.allowHighPrecisionTextures; }
			set
			{
				if(currentQualityLevel.allowHighPrecisionTextures == value)
					return;

				currentQualityLevel.allowHighPrecisionTextures = value;
				OnChange();
			}
		}

		public bool AllowHighQualitySlopeMaps
		{
			get { return currentQualityLevel.allowHighQualitySlopeMaps; }
			set
			{
				if(currentQualityLevel.allowHighQualitySlopeMaps == value)
					return;

				currentQualityLevel.allowHighQualitySlopeMaps = value;
				OnChange();
			}
		}

		public WaterWavesMode WavesMode
		{
			get { return currentQualityLevel.wavesMode; }
			set
			{
				if(currentQualityLevel.wavesMode == value)
					return;

				currentQualityLevel.wavesMode = value;
				OnChange();
			}
		}

		public bool AllowVolumetricLighting
		{
			get { return currentQualityLevel.allowVolumetricLighting; }
			set
			{
				if(currentQualityLevel.allowVolumetricLighting == value)
					return;

				currentQualityLevel.allowVolumetricLighting = value;
				OnChange();
            }
		}

		public float MaxTesselationFactor
		{
			get { return currentQualityLevel.maxTesselationFactor; }
			set
			{
				if(currentQualityLevel.maxTesselationFactor == value)
					return;

				currentQualityLevel.maxTesselationFactor = value;
				OnChange();
			}
		}

		public int MaxVertexCount
		{
			get { return currentQualityLevel.maxVertexCount; }
			set
			{
				if(currentQualityLevel.maxVertexCount == value)
					return;

				currentQualityLevel.maxVertexCount = value;
				OnChange();
			}
		}

		public int MaxTesselatedVertexCount
		{
			get { return currentQualityLevel.maxTesselatedVertexCount; }
			set
			{
				if(currentQualityLevel.maxTesselatedVertexCount == value)
					return;

				currentQualityLevel.maxTesselatedVertexCount = value;
				OnChange();
			}
		}

		public bool AllowAlphaBlending
		{
			get { return currentQualityLevel.allowAlphaBlending; }
			set
			{
				if(currentQualityLevel.allowAlphaBlending == value)
					return;

				currentQualityLevel.allowAlphaBlending = value;
				OnChange();
            }
		}

		public bool AllowHighQualityReflections
		{
			get { return currentQualityLevel.allowHighQualityReflections; }
			set
			{
				if(currentQualityLevel.allowHighQualityReflections == value)
					return;

				currentQualityLevel.allowHighQualityReflections = value;
				OnChange();
			}
		}

		/// <summary>
		/// Are water quality settings synchronized with Unity?
		/// </summary>
		public bool SynchronizeWithUnity
		{
			get { return synchronizeWithUnity; }
		}

		public int GetQualityLevel()
		{
			return waterQualityIndex;
		}

		public void SetQualityLevel(int index)
		{
			if(!Application.isPlaying)
				savedCustomQualityLevel = index;

			currentQualityLevel = qualityLevels[index];
			waterQualityIndex = index;

			OnChange();
        }
		
		/// <summary>
		/// Synchronizes current water quality level with the one set in Unity quality settings.
		/// </summary>
		public void SynchronizeQualityLevel()
		{
#if UNITY_EDITOR
			if(!Application.isPlaying && synchronizeWithUnity)
				SynchronizeLevelNames();
#endif

			int currentQualityIndex = -1;

			if(synchronizeWithUnity)
				currentQualityIndex = FindQualityLevel(QualitySettings.names[QualitySettings.GetQualityLevel()]);

			if(currentQualityIndex == -1)
				currentQualityIndex = savedCustomQualityLevel;

			currentQualityIndex = Mathf.Clamp(currentQualityIndex, 0, qualityLevels.Length - 1);
			
			if(currentQualityIndex != waterQualityIndex)
				SetQualityLevel(currentQualityIndex);
		}

		internal WaterQualityLevel[] GetQualityLevelsDirect()
		{
			return qualityLevels;
		}

		internal WaterQualityLevel CurrentQualityLevel
		{
			get { return currentQualityLevel; }
		}

		private void OnChange()
		{
			if(Changed != null)
				Changed();
		}

		private int FindQualityLevel(string name)
		{
			for(int i=0; i<qualityLevels.Length; ++i)
			{
				if(qualityLevels[i].name == name)
					return i;
			}

			return -1;
		}

		/// <summary>
		/// Ensures that water quality settings are named the same way that Unity ones.
		/// </summary>
		private void SynchronizeLevelNames()
		{
#if UNITY_EDITOR
			if(qualityLevels == null)
				qualityLevels = new WaterQualityLevel[0];

			string[] unityNames = QualitySettings.names;
			var currentNames = Names;
			int index = 0;
			bool noChanges = true;

			foreach(var name in currentNames)
			{
				if(unityNames[index] != name)
				{
					noChanges = false;
					break;
				}
			}

			if(noChanges)
				return;

			var matches = new List<WaterQualityLevel>();
			var availableLevels = new List<WaterQualityLevel>(qualityLevels);
			var availableUnityLevels = new List<string>(unityNames);

			// keep existing levels with matching names
			for(int i=0; i<availableLevels.Count; ++i)
			{
				var level = availableLevels[i];

				if(availableUnityLevels.Contains(level.name))
				{
					matches.Add(level);
					availableLevels.RemoveAt(i--);
					availableUnityLevels.Remove(level.name);
                }
			}

			// use non-matched levels as-is // possibly just their name or order has changed
			while(availableLevels.Count > 0 && availableUnityLevels.Count > 0)
			{
				var level = availableLevels[0];
				level.name = availableUnityLevels[0];

				matches.Add(level);

				availableLevels.RemoveAt(0);
				availableUnityLevels.RemoveAt(0);
			}

			// create new levels if there is more of them left
			while(availableUnityLevels.Count > 0)
			{
				var level = new WaterQualityLevel();
				level.ResetToDefaults();
                level.name = availableUnityLevels[0];

				matches.Add(level);

				availableUnityLevels.RemoveAt(0);
			}

			// create new list with the same order as in Unity
			qualityLevels = new WaterQualityLevel[unityNames.Length];
			
			for(int i=0; i<qualityLevels.Length; ++i)
				qualityLevels[i] = matches.Find(l => l.name == unityNames[i]);

			UnityEditor.EditorUtility.SetDirty(this);
#endif
		}
	}

	[System.Serializable]
	public struct WaterQualityLevel
	{
		[SerializeField]
		public string name;

		[SerializeField]
		public int maxSpectrumResolution;

		[SerializeField]
		public bool allowHighPrecisionTextures;

		[SerializeField]
		public bool allowHighQualitySlopeMaps;

		[Range(0.0f, 1.0f)]
		[SerializeField]
		public float tileSizeScale;

		[SerializeField]
		public WaterWavesMode wavesMode;

		[SerializeField]
		public bool allowSpray;

		[Range(0.0f, 1.0f)]
		[SerializeField]
		public float foamQuality;

		[SerializeField]
		public bool allowVolumetricLighting;

		[Range(0.0f, 1.0f)]
		[SerializeField]
		public float maxTesselationFactor;

		[SerializeField]
		public int maxVertexCount;

		[SerializeField]
		public int maxTesselatedVertexCount;

		[SerializeField]
		public bool allowAlphaBlending;

		[SerializeField]
		public bool allowHighQualityReflections;

		public void ResetToDefaults()
		{
			name = "";
			maxSpectrumResolution = 256;
			allowHighPrecisionTextures = true;
			tileSizeScale = 1.0f;
			wavesMode = WaterWavesMode.AllowAll;
			allowSpray = true;
			foamQuality = 1.0f;
			allowVolumetricLighting = true;
			maxTesselationFactor = 1.0f;
			maxVertexCount = 500000;
			maxTesselatedVertexCount = 120000;
			allowAlphaBlending = true;
			allowHighQualityReflections = false;
        }
	}

	public enum WaterWavesMode
	{
		AllowAll,
		AllowSlopeFFT,
		AllowGerstner,
		DisallowAll
	}
}
