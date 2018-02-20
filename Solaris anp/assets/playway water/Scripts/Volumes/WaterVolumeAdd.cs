using UnityEngine;

namespace PlayWay.Water
{
	/// <summary>
	/// Extends water volume.
	/// </summary>
	public class WaterVolumeAdd : WaterVolumeBase
	{
		override protected void Register(Water water)
		{
			if(water != null)
				water.Volume.AddVolume(this);
		}

		override protected void Unregister(Water water)
		{
			if(water != null)
				water.Volume.RemoveVolume(this);
		}
	}
}
