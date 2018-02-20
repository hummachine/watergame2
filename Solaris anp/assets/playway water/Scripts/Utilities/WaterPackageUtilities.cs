using System.IO;

namespace PlayWay.Water
{
	/// <summary>
	/// Helps locating the PlayWay Water folder and find stuff in it.
	/// </summary>
	public class WaterPackageUtilities
	{
#if UNITY_EDITOR
		static private string waterSpecificPath = "PlayWay Water" + Path.DirectorySeparatorChar + "Textures";
		static private string waterPackagePath;
		
		static public string WaterPackagePath
		{
			get
			{
				if(waterPackagePath == null)
					waterPackagePath = Find("Assets" + Path.DirectorySeparatorChar, "");

				return waterPackagePath;
			}
		}

		static private string Find(string directory, string parentDirectory)
		{
			if(directory.EndsWith(waterSpecificPath))
				return parentDirectory.Replace(Path.DirectorySeparatorChar, '/');

			foreach(string subDirectory in Directory.GetDirectories(directory))
			{
				string result = Find(subDirectory, directory);

				if(result != null)
					return result;
			}

			return null;
		}
#endif
	}
}