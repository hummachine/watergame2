using UnityEngine;

namespace PlayWay.Water
{
	public struct TemporaryRenderTexture : System.IDisposable
	{
		private RenderTexture renderTexture;
		private RenderTexturesCache renderTexturesCache;
		
		internal TemporaryRenderTexture(RenderTexturesCache renderTexturesCache)
		{
			this.renderTexturesCache = renderTexturesCache;
			this.renderTexture = renderTexturesCache.GetTemporaryDirect();
        }

		public RenderTexture Texture
		{
			get { return renderTexture; }
		}

		public void Dispose()
		{
			if(renderTexture != null)
			{
				renderTexturesCache.ReleaseTemporaryDirect(renderTexture);
				renderTexture = null;
			}
        }

		static public implicit operator RenderTexture(TemporaryRenderTexture that)
		{
			return that.Texture;
		}
	}
}
