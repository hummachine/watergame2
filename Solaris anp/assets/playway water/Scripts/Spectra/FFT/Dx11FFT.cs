using UnityEngine;

namespace PlayWay.Water
{
	/// <summary>
	/// Performs FFT with compute shaders (fast). The in/out resolution cannot exceed 1024.
	/// </summary>
	public class Dx11FFT : GpuFFT
	{
		private ComputeShader shader;
		private int kernelIndex;

		public Dx11FFT(ComputeShader shader, int resolution, bool highPrecision, bool twoChannels) : base(resolution, highPrecision, twoChannels, true)
		{
			this.shader = shader;

			kernelIndex = (numButterflies - 5) << 1;

			if(twoChannels)
				kernelIndex += 10;
		}

		public override void SetupMaterials()
		{
			// nothing to do
		}

		override public void ComputeFFT(Texture tex, RenderTexture target)
		{
			var rt1 = renderTexturesSet.GetTemporary();

			if(!target.IsCreated())
			{
				target.enableRandomWrite = true;
				target.Create();
			}

			shader.SetTexture(kernelIndex, "_ButterflyTex", Butterfly);
			shader.SetTexture(kernelIndex, "_SourceTex", tex);
			shader.SetTexture(kernelIndex, "_TargetTex", rt1);
			shader.Dispatch(kernelIndex, 1, tex.height, 1);

			shader.SetTexture(kernelIndex + 1, "_ButterflyTex", Butterfly);
			shader.SetTexture(kernelIndex + 1, "_SourceTex", rt1);
			shader.SetTexture(kernelIndex + 1, "_TargetTex", target);
			shader.Dispatch(kernelIndex + 1, 1, tex.height, 1);

			rt1.Dispose();
		}

		override protected void FillButterflyTexture(Texture2D butterfly, int[][] indices, Vector2[][] weights)
		{
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

						c.r = indices[indexX][indexY] + offset;
						c.g = indices[indexX][indexY + 1] + offset;

						c.b = weights[row][col].x;
						c.a = weights[row][col].y;

						butterfly.SetPixel(offset + col, row, c);
					}
				}
			}
		}
	}
}
