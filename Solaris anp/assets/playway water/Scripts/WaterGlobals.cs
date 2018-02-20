using System.Collections.Generic;
using UnityEngine;

namespace PlayWay.Water
{
	public class WaterGlobals
	{
		static private WaterGlobals instance;

		private List<Water> waters;
		private List<Water> boundlessWaters;
		
		static public WaterGlobals Instance
		{
			get
			{
				if(instance == null)
					instance = new WaterGlobals();

				return instance;
			}
		}

		private WaterGlobals()
		{
			waters = new List<Water>();
			boundlessWaters = new List<Water>();
		}

		public void AddWater(Water water)
		{
			if(!waters.Contains(water))
				waters.Add(water);

			if((water.Volume == null || water.Volume.Boundless) && !boundlessWaters.Contains(water))
				boundlessWaters.Add(water);
		}

		public void RemoveWater(Water water)
		{
			waters.Remove(water);
			boundlessWaters.Remove(water);
		}

		/// <summary>
		/// Enabled waters in the current scene.
		/// </summary>
		public List<Water> Waters
		{
			get { return waters; }
		}

		/// <summary>
		/// Enabled boundless waters in the current scene.
		/// </summary>
		public List<Water> BoundlessWaters
		{
            get { return boundlessWaters; }
		}
	}
}
