
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;

namespace VoxxWeatherPlugin.Utils
{
    public class DepthVSMPass : CustomPass
    {
        public RenderTexture? depthRenderTexture;
        public Material? depthMaterial;
        public int blurRadius = 4; // The radius of the blur kernel for VSM averaging

        protected override bool executeInSceneView => true;

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
        }

        protected override void Execute(CustomPassContext ctx)
        {
            if (depthMaterial == null || depthRenderTexture == null)
            {
                Debug.LogError("Depth material, texture or baking camera is not assigned.");
                return;
            }
            depthMaterial.SetFloat("_BlurKernelSize", blurRadius);
            // Set the aspect ratio of the baking camera to match the render texture
            ctx.hdCamera.camera.aspect = (float)depthRenderTexture.width / (float)depthRenderTexture.height;
            //create temporary exact copy
            RenderTexture tempTexture1 = RenderTexture.GetTemporary(depthRenderTexture.width, depthRenderTexture.height, 0, depthRenderTexture.format);
            RenderTexture tempTexture2 = RenderTexture.GetTemporary(depthRenderTexture.width, depthRenderTexture.height, 0, depthRenderTexture.format);
            // Copy the depth map to a temporary texture
            ctx.cmd.Blit(ctx.cameraDepthBuffer, tempTexture1, depthMaterial, 0);
            // Blur the depth map (Horizontal)
            depthMaterial.SetTexture("_MainTex", tempTexture1);
            ctx.cmd.Blit(tempTexture1, tempTexture2, depthMaterial, 1);
            // Blur the depth map (Vertical)
            depthMaterial.SetTexture("_MainTex", tempTexture2);
            ctx.cmd.Blit(tempTexture2, depthRenderTexture, depthMaterial, 2);

            RenderTexture.ReleaseTemporary(tempTexture1);
            RenderTexture.ReleaseTemporary(tempTexture2);
        }
    }
}