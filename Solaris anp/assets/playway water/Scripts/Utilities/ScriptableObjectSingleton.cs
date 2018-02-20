using UnityEngine;

namespace PlayWay.Water
{
	public class ScriptableObjectSingleton : ScriptableObject
	{
		static protected T LoadSingleton<T>() where T : ScriptableObject
		{
			var instance = Resources.Load<T>(typeof(T).Name);

#if UNITY_EDITOR
			if(instance == null)
			{
				instance = CreateInstance<T>();

				string path = WaterPackageUtilities.WaterPackagePath + "/Resources/" + typeof(T).Name + ".asset";
				UnityEditor.AssetDatabase.CreateAsset(instance, path);
			}
#endif

			return instance;
		}
	}
}
