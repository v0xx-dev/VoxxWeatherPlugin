using UnityEngine;

namespace VoxxWeatherPlugin.Utils
{
    static class SnowfallShaderIDs
    {
        internal static readonly int FadeValue = Shader.PropertyToID("_FadeValue"); 
        internal static readonly int DepthTex = Shader.PropertyToID("_DepthTex");
        internal static readonly int FootprintsTex = Shader.PropertyToID("_FootprintsTex");
        internal static readonly int LightViewProjection = Shader.PropertyToID("_LightViewProjection");
        internal static readonly int FootprintsViewProjection = Shader.PropertyToID("_FootprintsViewProjection");
        internal static readonly int ShadowBias = Shader.PropertyToID("_ShadowBias");
        // internal static readonly int LightDirection = Shader.PropertyToID("_LightDirection");
        internal static readonly int PCFKernelSize = Shader.PropertyToID("_PCFKernelSize");
        internal static readonly int SnowNoisePower = Shader.PropertyToID("_SnowNoisePower");
        internal static readonly int SnowNoiseScale = Shader.PropertyToID("_SnowNoiseScale");
        internal static readonly int SnowNoiseScaleOverlay = Shader.PropertyToID("_SnowNoiseScaleOverlay");
        internal static readonly int MaxSnowHeight = Shader.PropertyToID("_MaxSnowHeight");
        internal static readonly int SnowOcclusionBias = Shader.PropertyToID("_SnowOcclusionBias");
        internal static readonly int BaseTessellationFactor = Shader.PropertyToID("_BaseTessellationFactor");
        internal static readonly int MaxTessellationFactor = Shader.PropertyToID("_MaxTessellationFactor");
        internal static readonly int isAdaptiveTessellation = Shader.PropertyToID("_isAdaptiveTesselation");
        internal static readonly int ShipPosition = Shader.PropertyToID("_ShipPosition");
        internal static readonly int Emission = Shader.PropertyToID("_Emission");
        internal static readonly int SnowMasks = Shader.PropertyToID("_SnowMaskTex");
        internal static readonly int BlurKernelSize = Shader.PropertyToID("_BlurKernelSize");
        internal static readonly int TexIndex = Shader.PropertyToID("_TexIndex");
        internal static readonly int NormalStrength = Shader.PropertyToID("_NormalStrength");
        internal static readonly int SnowColor = Shader.PropertyToID("_SnowColor");
        //Also used for base color of the snow, i.e where it meets the ground
        internal static readonly int SnowBaseColor = Shader.PropertyToID("_SnowBaseColor");
        internal static readonly int BlizzardFogColor = Shader.PropertyToID("_BlizzardFogColor");
        internal static readonly int Metallic = Shader.PropertyToID("_Metallic");
        internal static readonly int IsDepthFade = Shader.PropertyToID("_EnableDepthFade");
    }

    static class ToxicShaderIDs
    {
        internal static readonly int FumesColor = Shader.PropertyToID("_FumesColor");
    }
}