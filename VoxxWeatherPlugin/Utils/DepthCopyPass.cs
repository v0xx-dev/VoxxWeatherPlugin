using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;

namespace VoxxWeatherPlugin.Utils
{
    public class DepthCopyPass : CustomPass
    {
        [SerializeField]
        internal RenderTexture depthMap;
        protected override bool executeInSceneView => true;

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
        }

        protected override void Execute(CustomPassContext ctx)
        {
            if (depthMap == null)
            {
                Debug.LogError("DepthCopyPass: Depth map is null");
                return;
            }
            // Get the depth buffer from the target camera
            ctx.cmd.Blit(ctx.cameraDepthBuffer, depthMap); 
        }
    }
}