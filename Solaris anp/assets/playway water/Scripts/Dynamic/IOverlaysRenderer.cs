
namespace PlayWay.Water
{
	/// <summary>
	/// Implement this interface to create components that render into water local maps.
	/// </summary>
	public interface IOverlaysRenderer
	{
		void RenderOverlays(WaterOverlaysData overlays);
	}
}
