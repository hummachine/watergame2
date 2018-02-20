
namespace PlayWay.Water
{
	/// <summary>
	/// A class that adds stuff to shader collection during its building process.
	/// </summary>
	public interface IShaderCollectionClient
	{
		void Write(ShaderCollection collection);
	}
}
