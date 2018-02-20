using UnityEngine;

namespace PlayWay.Water
{
	/// <summary>
	/// Stores references to materials with chosen keywords to include them in builds.
	/// </summary>
	[System.Serializable]
	public class ShaderCollection : ScriptableObject
	{
		[SerializeField]
		private Shader[] shaders;

#if UNITY_EDITOR
		private bool rebuilding;
		static public IShaderCollectionBuilder shaderCollectionBuilder;
#endif

		static bool errorDisplayed;

		static public Shader GetRuntimeShaderVariant(string keywordsString, bool volume)
		{
			var shader = Shader.Find("PlayWay Water/Variations/Water " + (volume ? "Volume " : "") + keywordsString);

			if(shader == null && !errorDisplayed && Application.isPlaying)
			{
				Debug.LogError("Could not find proper water shader variation. Select your water and click \"Save Asset\" button to build proper shaders. Missing shader: \"" + "PlayWay Water/Variations/Water " + (volume ? "Volume " : "") + keywordsString + "\"");
				errorDisplayed = true;
            }

			return shader;
		}

		public Shader GetShaderVariant(string[] localKeywords, string[] sharedKeywords, string additionalCode, string keywordsString, bool volume)
		{
			System.Array.Sort(localKeywords);
			System.Array.Sort(sharedKeywords);
			string shaderNameEnd = (volume ? "Volume " : "") + keywordsString;

#if UNITY_EDITOR
			if(shaders != null)
			{
				foreach(var shader in shaders)
				{
					if(shader != null && shader.name.EndsWith(shaderNameEnd))
						return shader;                                 // already added
				}
			}

			if(!rebuilding)
			{
				var shader2 = Shader.Find("PlayWay Water/Variations/Water " + shaderNameEnd);

				if(shader2 != null)
				{
					AddShader(shader2);
					return shader2;
				}
			}

			if(shaderCollectionBuilder != null)
			{
				var shader = shaderCollectionBuilder.BuildShaderVariant(localKeywords, sharedKeywords, additionalCode, keywordsString, volume);
				AddShader(shader);

				return shader;
			}
			else
			{
				Debug.LogError("Shader Collection Builder is null in editor.");

				return null;
			}
#else
			return Shader.Find("PlayWay Water/Variations/Water " + shaderNameEnd);
#endif
		}

		public Shader[] GetShadersDirect()
		{
			return shaders;
		}

		public void Build()
		{
#if UNITY_EDITOR
			try
			{
				rebuilding = true;
				shaderCollectionBuilder.BuildSceneShaderCollection(this);
			}
			finally
			{
				rebuilding = false;
            }
#endif
		}

		public bool ContainsShaderVariant(string keywordsString)
		{
			if(shaders != null)
			{
				foreach(var shader in shaders)
				{
					if(shader != null && shader.name.EndsWith(keywordsString))
						return true;                                 // already added
				}
			}

			return false;
		}

		private void AddShader(Shader shader)
		{
			if(shaders != null)
			{
				System.Array.Resize(ref shaders, shaders.Length + 1);
				shaders[shaders.Length - 1] = shader;
			}
			else
				shaders = new Shader[] { shader };
		}

#if UNITY_EDITOR
		public void Clear()
		{
			shaders = new Shader[0];
			UnityEditor.EditorUtility.SetDirty(this);
		}
#endif
	}
}
