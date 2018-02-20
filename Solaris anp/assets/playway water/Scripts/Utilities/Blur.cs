using UnityEngine;

namespace PlayWay.Water
{
	[System.Serializable]
	public class Blur
	{
		[HideInInspector]
		[SerializeField]
		private Shader blurShader;

		[Range(0, 10)]
		[SerializeField]
		private int iterations = 1;

		[SerializeField]
		private float size = 0.005f;

		private Material blurMaterial;

		private int passIndex;
		private int offsetHash;

		public Blur()
		{

		}

        public int Iterations
        {
            get { return iterations; }
            set
			{
				// preserve total blur size
				float totalSize = TotalSize;
				iterations = value;
				TotalSize = totalSize;
			}
        }

        public float Size
        {
            get { return size; }
            set { size = value; }
        }

		public float TotalSize
		{
			get { return size * iterations; }
			set { size = value / iterations; }
		}

        public Material BlurMaterial
		{
			get
			{
				if(blurMaterial == null)
				{
					if(blurShader == null)
						Validate("PlayWay Water/Utilities/Blur");

					blurMaterial = new Material(blurShader);
					blurMaterial.hideFlags = HideFlags.DontSave;
					offsetHash = Shader.PropertyToID("_Offset");
				}

				return blurMaterial;
			}

			set
			{
				blurMaterial = value;
			}
		}

		public int PassIndex
		{
			get { return passIndex; }
			set { passIndex = value; }
		}

		public void Apply(RenderTexture tex)
		{
			if(iterations == 0)
				return;

			var blurMaterial = BlurMaterial;

			var originalFilterMode = tex.filterMode;
			tex.filterMode = FilterMode.Bilinear;

			var temp = RenderTexture.GetTemporary(tex.width, tex.height, 0, tex.format);
			temp.filterMode = FilterMode.Bilinear;

			for(int i = 0; i < iterations; ++i)
			{
				float blurSize = size * (1.0f + i * 0.5f);

				blurMaterial.SetVector(offsetHash, new Vector4(blurSize, 0.0f, 0.0f, 0.0f));
				Graphics.Blit(tex, temp, blurMaterial, passIndex);

				blurMaterial.SetVector(offsetHash, new Vector4(0.0f, blurSize, 0.0f, 0.0f));
				Graphics.Blit(temp, tex, blurMaterial, passIndex);
			}

			tex.filterMode = originalFilterMode;

			RenderTexture.ReleaseTemporary(temp);
		}

		public void Validate(string shaderName)
		{
			blurShader = Shader.Find(shaderName);
		}

		public void Dispose()
		{
			if(blurMaterial != null)
				Object.DestroyImmediate(blurMaterial);
		}
	}
}
