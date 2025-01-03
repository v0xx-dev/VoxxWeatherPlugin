
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;

namespace VoxxWeatherPlugin.Behaviours
{
    // TODO ADD PRIORITY
    // Must be used with a camera that has a target texture
    public class DepthVSMPass : CustomPass
    {
        public Material? depthMaterial;
        public RenderTexture? depthUnblurred = null;
        public int blurRadius = 4; // The radius of the blur kernel for VSM averaging

        protected override bool executeInSceneView => true;

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
        }

        protected override void Execute(CustomPassContext ctx)
        {
            if (depthMaterial == null)
            {
                Debug.LogError("Depth material is not assigned.");
                return;
            }
            if (ctx.hdCamera.camera.targetTexture == null)
            {
                Debug.LogError("Camera target texture is not assigned for VSM.");
                return;
            }

            depthMaterial.SetFloat("_BlurKernelSize", blurRadius);
            // Set the aspect ratio of the baking camera to match the render texture
            int width = ctx.hdCamera.camera.pixelWidth;
            int height = ctx.hdCamera.camera.pixelHeight;
            RenderTextureFormat textureFormat = ctx.hdCamera.camera.targetTexture.format;
            //create temporary exact copy
            RenderTexture tempTexture1 = RenderTexture.GetTemporary(width, height, 0, textureFormat);
            RenderTexture tempTexture2 = RenderTexture.GetTemporary(width, height, 0, textureFormat);
            // Copy the depth map to a temporary texture
            ctx.cmd.Blit(ctx.cameraDepthBuffer, tempTexture1, depthMaterial, 0);
            if (depthUnblurred != null)
            {
                // Store the unblurred depth map
                ctx.cmd.Blit(ctx.cameraDepthBuffer, depthUnblurred, depthMaterial, 0);
            }
            // Blur the depth map (Horizontal)
            ctx.cmd.Blit(tempTexture1, tempTexture2, depthMaterial, 1);
            // Blur the depth map (Vertical)
            ctx.cmd.Blit(tempTexture2, ctx.cameraColorBuffer, depthMaterial, 2);

            RenderTexture.ReleaseTemporary(tempTexture1);
            RenderTexture.ReleaseTemporary(tempTexture2);
        }
    }
}