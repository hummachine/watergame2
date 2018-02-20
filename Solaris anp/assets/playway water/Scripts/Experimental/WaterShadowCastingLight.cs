using UnityEngine;
using UnityEngine.Rendering;

public class WaterShadowCastingLight : MonoBehaviour
{
	private CommandBuffer commandBuffer1;
	private int shadowmapId;

	void Start()
	{
		int shadowmapId = Shader.PropertyToID("_WaterShadowmap");

		commandBuffer1 = new CommandBuffer();
		commandBuffer1.name = "Water: Copy Shadowmap";
        commandBuffer1.GetTemporaryRT(shadowmapId, Screen.width, Screen.height, 32, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
		commandBuffer1.Blit(BuiltinRenderTextureType.CurrentActive, shadowmapId);

		var light = GetComponent<Light>();
		light.AddCommandBuffer(LightEvent.AfterScreenspaceMask, commandBuffer1);
	}
}
