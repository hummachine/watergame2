using UnityEngine;
using System.Linq;
using System.IO;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PlayWay.Water
{
	/// <summary>
	/// Builds shader collections. It's separated to editor script because it runs in less restrictive .net environment.
	/// </summary>
	public class EditorShaderCollectionBuilder : IShaderCollectionBuilder
	{
		static private string localKeywordDefinitionFormat = "#define {0} 1\r\n";
		static private string sharedKeywordDefinitionFormat = "#pragma multi_compile {0}\r\n";

		[InitializeOnLoadMethod]
		static public void RegisterShaderCollectionBuilder()
		{
			var instance = new EditorShaderCollectionBuilder();
			ShaderCollection.shaderCollectionBuilder = instance;
		}

		public void BuildSceneShaderCollection(ShaderCollection shaderCollection)
		{
			shaderCollection.Clear();

			var transforms = Object.FindObjectsOfType<Transform>();

			foreach(var root in transforms)
			{
				if(root.parent == null)     // if that's really a root
				{
					var writers = root.GetComponentsInChildren<IShaderCollectionClient>(true);

					foreach(var writer in writers)
						writer.Write(shaderCollection);
				}
			}
		}

		public Shader BuildShaderVariant(string[] localKeywords, string[] sharedKeywords, string additionalCode, string keywordsString, bool volume)
		{
			string shaderPath;
            string shaderCodeTemplate = File.ReadAllText(!volume ? WaterPackageUtilities.WaterPackagePath + "/Shaders/Water/PlayWay Water.shader" : WaterPackageUtilities.WaterPackagePath + "/Shaders/Water/PlayWay Water - Volume.shader");
			string shaderCode = BuildShader(shaderCodeTemplate, localKeywords, sharedKeywords, additionalCode, volume, keywordsString);
			
			if(!volume)
				shaderPath = WaterPackageUtilities.WaterPackagePath + "/Shaders/Water/PlayWay Water Variation #" + HashString(keywordsString) + ".shader";
			else
				shaderPath = WaterPackageUtilities.WaterPackagePath + "/Shaders/Water/PlayWay Water Volume Variation #" + HashString(keywordsString) + ".shader";

			File.WriteAllText(shaderPath, shaderCode);
			AssetDatabase.Refresh();

			var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
			return shader;
		}

		public void CleanUpUnusedShaders()
		{
			List<string> files = new List<string>(
				Directory.GetFiles(WaterPackageUtilities.WaterPackagePath + "/Shaders/Water/")
				.Where(f => f.Contains(" Variation ") && !f.EndsWith(".meta"))
			);

			string[] guids = AssetDatabase.FindAssets("t:ShaderCollection", null);

			foreach(string guid in guids)
			{
				var shaderCollection = AssetDatabase.LoadAssetAtPath<ShaderCollection>(AssetDatabase.GUIDToAssetPath(guid));
				var shaders = shaderCollection.GetShadersDirect();

				if(shaders != null)
				{
					foreach(var shader in shaders)
					{
						string shaderPath = AssetDatabase.GetAssetPath(shader);
						files.Remove(shaderPath);
					}
				}
			}

			foreach(string file in files)
				AssetDatabase.DeleteAsset(file);
		}
		
		private string BuildShader(string code, string[] localKeywords, string[] sharedKeywords, string additionalCode, bool volume, string keywordsString)
		{
			string[] localKeywordsCode = localKeywords.Select(k => string.Format(localKeywordDefinitionFormat, k)).ToArray();
			string[] sharedKeywordsCode = sharedKeywords.Select(k => string.Format(sharedKeywordDefinitionFormat, k)).ToArray();

			string keywordsCode = string.Join("\t\t\t", localKeywordsCode) + "\r\n\t\t\t" + string.Join("\t\t\t", sharedKeywordsCode);

			return code.Replace("PlayWay Water/Standard" + (volume ? " Volume" : ""), "PlayWay Water/Variations/Water " + (volume ? "Volume " : "") + keywordsString)
				.Replace("#define PLACE_KEYWORDS_HERE", keywordsCode)
				.Replace("// PLACE_CODE_HERE", additionalCode);
		}

		static private int HashString(string text)
		{
			int len = text.Length;
			int hash = 23;

			for(int i = 0; i < len; ++i)
				hash = hash * 31 + text[i];

			return hash;
		}
	}

	public class WaterShadersCleanupTask : UnityEditor.AssetModificationProcessor
	{
		public static string[] OnWillSaveAssets(string[] paths)
		{
			var shaderCollectionBuilder = (EditorShaderCollectionBuilder)ShaderCollection.shaderCollectionBuilder;
			shaderCollectionBuilder.CleanUpUnusedShaders();

			return paths;
		}
	}
}