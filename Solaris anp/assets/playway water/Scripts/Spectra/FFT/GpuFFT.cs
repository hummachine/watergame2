using System.Collections.Generic;
using UnityEngine;

namespace PlayWay.Water
{
	abstract public class GpuFFT
	{
		private Texture2D butterfly;
		
		protected RenderTexturesCache renderTexturesSet;
		
		protected int resolution;
		protected int numButterflies;
		protected int numButterfliesPow2;
		protected bool twoChannels;

		private bool highPrecision;
		private bool usesUAV;

		public GpuFFT(int resolution, bool highPrecision, bool twoChannels, bool usesUAV)
		{
			this.resolution = resolution;
			this.highPrecision = highPrecision;
			this.numButterflies = (int)(Mathf.Log((float)resolution) / Mathf.Log(2.0f));
			this.numButterfliesPow2 = Mathf.NextPowerOfTwo(numButterflies);
			this.twoChannels = twoChannels;
			this.usesUAV = usesUAV;

			RetrieveRenderTexturesSet();
			CreateTextures();
        }

		public Texture2D Butterfly
		{
			get { return butterfly; }
		}
		
		public int Resolution
		{
			get { return resolution; }
		}

		abstract public void SetupMaterials();
		abstract public void ComputeFFT(Texture tex, RenderTexture target);

		virtual public void Dispose()
		{
			if(butterfly != null)
			{
				butterfly.Destroy();
				butterfly = null;
			}
		}

		private void CreateTextures()
		{
			CreateButterflyTexture();
		}

		private void RetrieveRenderTexturesSet()
		{
			var format = twoChannels ?
				(highPrecision ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGBHalf) :
				(highPrecision ? RenderTextureFormat.RGFloat : RenderTextureFormat.RGHalf);

			renderTexturesSet = RenderTexturesCache.GetCache(resolution << 1, resolution << 1, 0, format, true, usesUAV);
		}

		virtual protected void FillButterflyTexture(Texture2D butterfly, int[][] indices, Vector2[][] weights)
		{
			float floatResolutionx2 = resolution << 1;

			for(int row = 0; row < numButterflies; ++row)
			{
				for(int scaleIndex = 0; scaleIndex < 2; ++scaleIndex)
				{
					int offset = scaleIndex == 0 ? 0 : resolution;

					for(int col = 0; col < resolution; ++col)
					{
						Color c;

						int indexX = numButterflies - row - 1;
						int indexY = (col << 1);

						c.r = (indices[indexX][indexY] + offset + 0.5f) / floatResolutionx2;
						c.g = (indices[indexX][indexY + 1] + offset + 0.5f) / floatResolutionx2;

						c.b = weights[row][col].x;
						c.a = weights[row][col].y;

						butterfly.SetPixel(offset + col, row, c);
					}
				}
			}
		}

		private void CreateButterflyTexture()
		{
			butterfly = new Texture2D(resolution << 1, numButterfliesPow2, highPrecision ? TextureFormat.RGBAFloat : TextureFormat.RGBAHalf, false, true);
			butterfly.hideFlags = HideFlags.DontSave;
			butterfly.filterMode = FilterMode.Point;
			butterfly.wrapMode = TextureWrapMode.Clamp;

			int[][] indices;
			Vector2[][] weights;
			ButterflyFFTUtility.ComputeButterfly(resolution, numButterflies, out indices, out weights);
			FillButterflyTexture(butterfly, indices, weights);

			butterfly.Apply();
		}
	}
}
