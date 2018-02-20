using UnityEngine;
using System.Collections;

namespace PlayWay.Water
{
	public class WaterOverlaysData
	{
		private RenderTexture dynamicDisplacementMap;			// RGB - displacement xyz, A - flat foam during generation and subsurface scattering in final texture
		private RenderTexture slopeMapA;						// RG - slope xy, B - transformed foam, A - shore mask
		private RenderTexture slopeMapB;
		private RenderTexture totalDisplacementMap;
		private RenderTexture utilityMap;						// RG - spray direction and probability (vector length)
		private WaterCamera camera;
		private WaterOverlays waveOverlays;
		private bool totalDisplacementMapDirty;
		private bool initialization;

		internal int lastFrameUsed;

		public WaterOverlaysData(WaterOverlays waveOverlays, WaterCamera camera, int resolution, int antialiasing)
		{
			this.waveOverlays = waveOverlays;
			this.camera = camera;
			this.initialization = true;

			dynamicDisplacementMap = CreateOverlayRT("Water Overlay: Displacement", RenderTextureFormat.ARGBHalf, resolution, antialiasing);
			slopeMapA = CreateOverlayRT("Water Overlay: Slope A", RenderTextureFormat.ARGBHalf, resolution, antialiasing);
			slopeMapB = CreateOverlayRT("Water Overlay: Slope B", RenderTextureFormat.ARGBHalf, resolution, antialiasing);
			totalDisplacementMap = CreateOverlayRT("Water Overlay: Total Displacement", RenderTextureFormat.ARGBHalf, resolution, antialiasing);

			if(waveOverlays.GetComponent<WaterSpray>() != null)
				utilityMap = CreateOverlayRT("Water Overlay: Utility Map", RenderTextureFormat.RGHalf, resolution, antialiasing);

			Graphics.SetRenderTarget(slopeMapA);
			GL.Clear(false, true, new Color(0.0f, 0.0f, 0.0f, 1.0f));

			Graphics.SetRenderTarget(null);
		}

		/// <summary>
		/// RGB = XYZ displacement, A = foam
		/// </summary>
		public RenderTexture DynamicDisplacementMap
		{
			get { return dynamicDisplacementMap; }
		}

		public bool Initialization
		{
			get { return initialization; }
			set { initialization = value; }
		}

		/// <summary>
		/// RG = XY normal, B = foam (automatic, don't write here), A = shoreline mask
		/// </summary>
		public RenderTexture SlopeMap
		{
			get { return slopeMapA; }
		}

		public RenderTexture SlopeMapPrevious
		{
			get { return slopeMapB; }
		}

		public RenderTexture UtilityMap
		{
			get { return utilityMap; }
		}

		public WaterCamera Camera
		{
			get { return camera; }
		}

		public RenderTexture GetTotalDisplacementMap()
		{
			if(totalDisplacementMapDirty)
			{
				waveOverlays.RenderTotalDisplacementMap(totalDisplacementMap);
				totalDisplacementMapDirty = false;
			}

			return totalDisplacementMap;
		}

		public void Dispose()
		{
			if(dynamicDisplacementMap != null)
			{
				dynamicDisplacementMap.Destroy();
				dynamicDisplacementMap = null;
			}

			if(slopeMapA != null)
			{
				slopeMapA.Destroy();
				slopeMapA = null;
			}

			if(slopeMapB != null)
			{
				slopeMapB.Destroy();
				slopeMapB = null;
			}

			if(totalDisplacementMap != null)
			{
				totalDisplacementMap.Destroy();
				totalDisplacementMap = null;
			}

			if(utilityMap != null)
			{
				utilityMap.Destroy();
				utilityMap = null;
            }
		}

		public void ClearOverlays()
		{
			SwapSlopeMaps();

			Graphics.SetRenderTarget(dynamicDisplacementMap);
			GL.Clear(false, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));

			Graphics.SetRenderTarget(slopeMapA);
			GL.Clear(false, true, new Color(0.0f, 0.0f, 0.0f, 1.0f));

			if(utilityMap != null)
			{
				Graphics.SetRenderTarget(utilityMap);
				GL.Clear(false, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));
			}

			totalDisplacementMapDirty = true;
		}

		private RenderTexture CreateOverlayRT(string name, RenderTextureFormat format, int resolution, int antialiasing)
		{
			var rt = new RenderTexture(resolution, resolution, 0, format, RenderTextureReadWrite.Linear);
			rt.hideFlags = HideFlags.DontSave;
			rt.antiAliasing = antialiasing;
			rt.filterMode = FilterMode.Bilinear;
			rt.wrapMode = TextureWrapMode.Clamp;
			rt.name = name;

			Graphics.SetRenderTarget(rt);
			GL.Clear(false, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));

			return rt;
		}

		public void SwapSlopeMaps()
		{
			var t = slopeMapB;
			slopeMapB = slopeMapA;
			slopeMapA = t;
		}
	}
}
