using UnityEngine;

namespace PlayWay.Water
{
	static public class WaterDebug
	{
		static public void WriteAllMaps(Water water)
		{
#if DEBUG && WATER_DEBUG
			var windWaves = water.GetComponent<WindWaves>();
            var wavesFFT = windWaves.WaterWavesFFT;
			SaveTexture(wavesFFT.GetDisplacementMap(0), "PlayWay Water - FFT Height Map 0.png");
			SaveTexture(wavesFFT.GetSlopeMap(0), "PlayWay Water - FFT Slope Map 0.png");
			SaveTexture(wavesFFT.GetDisplacementMap(1), "PlayWay Water - FFT Height Map 1.png");
			SaveTexture(wavesFFT.GetSlopeMap(1), "PlayWay Water - FFT Slope Map 1.png");
			SaveTexture(wavesFFT.GetDisplacementMap(2), "PlayWay Water - FFT Height Map 2.png");
			SaveTexture(wavesFFT.GetDisplacementMap(3), "PlayWay Water - FFT Height Map 3.png");

			SaveTexture(windWaves.SpectrumResolver.RenderHeightSpectrumAt(Time.time), "PlayWay Water - Timed Height Spectrum.png");

			SaveTexture(windWaves.SpectrumResolver.GetSpectrum(SpectrumResolver.SpectrumType.RawOmnidirectional), "PlayWay Water - Spectrum Raw Omnidirectional.png");
			SaveTexture(windWaves.SpectrumResolver.GetSpectrum(SpectrumResolver.SpectrumType.RawDirectional), "PlayWay Water - Spectrum Raw Directional.png");
			SaveTexture(windWaves.SpectrumResolver.GetSpectrum(SpectrumResolver.SpectrumType.Height), "PlayWay Water - Spectrum Height.png");
			SaveTexture(windWaves.SpectrumResolver.GetSpectrum(SpectrumResolver.SpectrumType.Slope), "PlayWay Water - Spectrum Slope.png");
			SaveTexture(windWaves.SpectrumResolver.GetSpectrum(SpectrumResolver.SpectrumType.Displacement), "PlayWay Water - Spectrum Displacement.png");
#endif
		}

		static public void SaveTexture(Texture tex, string name)
		{
#if DEBUG && WATER_DEBUG
			if(tex == null)
				return;

			var shader = Shader.Find("PlayWay Water/Editor/Inspect Texture");
			var material = new Material(shader);
			material.SetVector("_RangeR", new Vector2(0.0f, 0.1f));
			material.SetVector("_RangeG", new Vector2(0.0f, 0.1f));
			material.SetVector("_RangeB", new Vector2(0.0f, 0.1f));

			var tempRT = new RenderTexture(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
			Graphics.Blit(tex, tempRT, material);

			RenderTexture.active = tempRT;

			var tex2d = new Texture2D(tex.width, tex.height, TextureFormat.ARGB32, false);
			tex2d.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
			tex2d.Apply();

			RenderTexture.active = null;

			System.IO.File.WriteAllBytes(name, tex2d.EncodeToPNG());

			tex2d.Destroy();
			tempRT.Destroy();
#endif
		}
	}
}
