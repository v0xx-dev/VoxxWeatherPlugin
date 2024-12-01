using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using System;
using VoxxWeatherPlugin.Weathers;

namespace VoxxWeatherPlugin.Utils
{
    public class SnowOverlayCustomPass : CustomPass
    {
        public enum ShaderPass
        {
            // Ordered by frame time in HDRP
            ///<summary>Object Depth pre-pass, only the depth of the object will be rendered.</summary>
            DepthPrepass = 1,
            ///<summary>Forward pass, render the object color.</summary>
            Forward = 0,
        }

        [Flags]
        public enum RenderingLayers
        {
            Layer0 = 1 << 0,
            Layer1 = 1 << 1,
            Layer2 = 1 << 2,
            Layer3 = 1 << 3,
            Layer4 = 1 << 4,
            Layer5 = 1 << 5,
            Layer6 = 1 << 6,
            Layer7 = 1 << 7,
            Layer8 = 1 << 8,
            Layer9 = 1 << 9,
            Layer10 = 1 << 10,
            Layer11 = 1 << 11,
            Layer12 = 1 << 12,
            Layer13 = 1 << 13,
            Layer14 = 1 << 14,
            Layer15 = 1 << 15,
            Layer16 = 1 << 16,
            Layer17 = 1 << 17,
            Layer18 = 1 << 18,
            Layer19 = 1 << 19,
            Layer20 = 1 << 20,
            Layer21 = 1 << 21,
            Layer22 = 1 << 22,
            Layer23 = 1 << 23,
            Layer24 = 1 << 24,
            Layer25 = 1 << 25,
            Layer26 = 1 << 26,
            Layer27 = 1 << 27,
            Layer28 = 1 << 28,
            Layer29 = 1 << 29,
            Layer30 = 1 << 30,
            Layer31 = 1 << 31,
        }

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
            internal static readonly int MaxSnowHeight = Shader.PropertyToID("_MaxSnowHeight");
            internal static readonly int SnowOcclusionBias = Shader.PropertyToID("_SnowOcclusionBias");
            internal static readonly int BaseTessellationFactor = Shader.PropertyToID("_BaseTessellationFactor");
            internal static readonly int MaxTessellationFactor = Shader.PropertyToID("_MaxTessellationFactor");
            internal static readonly int isAdaptiveTessellation = Shader.PropertyToID("_isAdaptiveTesselation");
            internal static readonly int ShipPosition = Shader.PropertyToID("_ShipPosition");
            internal static readonly int Emission = Shader.PropertyToID("_Emission");

        }

        // Used only for the UI to keep track of the toggle state
        internal bool filterFoldout;
        internal bool rendererFoldout;

        public RenderQueueType renderQueueType = RenderQueueType.AllOpaque;
        public LayerMask layerMask = 1; // Layer mask Default enabled
        public RenderingLayers renderingLayers = RenderingLayers.Layer1;
        public SortingCriteria sortingCriteria = SortingCriteria.CommonOpaque;

        // Override material
        public Material? overrideMaterial = null;
        public Material? snowVertexMaterial = null;
        [SerializeField] int overrideMaterialPassIndex = 0;
        public string overrideMaterialPassName = "Forward";

        // Override the depth state of the objects.
        public bool overrideDepthState = false;
        public CompareFunction depthCompareFunction = CompareFunction.LessEqual;
        public bool depthWrite = true;
        public bool forceClusteredLighting = true;

        /// Override the stencil state of the objects.
        internal bool overrideStencil = false;
        internal int stencilReferenceValue = (int)UserStencilUsage.UserBit0;
        internal int stencilWriteMask = (int)(UserStencilUsage.AllUserBits);
        internal int stencilReadMask = (int)(UserStencilUsage.AllUserBits);
        internal CompareFunction stencilCompareFunction = CompareFunction.Always;
        internal StencilOp stencilPassOperation;
        internal StencilOp stencilFailOperation;
        internal StencilOp stencilDepthFailOperation;

        internal ShaderPass shaderPass = ShaderPass.Forward;

        static ShaderTagId[]? forwardShaderTags;
        static ShaderTagId[]? depthShaderTags;

        // Cache the shaderTagIds so we don't allocate a new array each frame
        ShaderTagId[]? cachedShaderTagIDs;

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            // In case there was a pass index assigned, retrieve the name of this pass
            if (String.IsNullOrEmpty(overrideMaterialPassName) && overrideMaterial != null)
                overrideMaterialPassName = overrideMaterial.GetPassName(overrideMaterialPassIndex);

            forwardShaderTags = new ShaderTagId[]
            {
                    HDShaderPassNames.s_ForwardName,            // HD Lit shader
                    HDShaderPassNames.s_ForwardOnlyName,        // HD Unlit shader
                    HDShaderPassNames.s_SRPDefaultUnlitName,    // Cross SRP Unlit shader
                    HDShaderPassNames.s_EmptyName,              // Add an empty slot for the override material
            };

            depthShaderTags = new ShaderTagId[]
            {
                    HDShaderPassNames.s_DepthForwardOnlyName,
                    HDShaderPassNames.s_DepthOnlyName,
                    HDShaderPassNames.s_EmptyName,              // Add an empty slot for the override material
            };

            if (overrideMaterial == null || snowVertexMaterial == null)
            {
                Debug.LogWarning("Attempt to call with an empty override material. Some variables will be set to default values");
                return;
            }

            SetupMaterial(overrideMaterial);
            SetupMaterial(snowVertexMaterial);
        }

        ShaderTagId[]? GetShaderTagIds()
        {
            if (shaderPass == ShaderPass.DepthPrepass)
                return depthShaderTags;
            else
                return forwardShaderTags;
        }

        protected override void Execute(CustomPassContext ctx)
        {
            var shaderPasses = GetShaderTagIds();
            if (overrideMaterial == null || snowVertexMaterial == null)
            {   
                Debug.LogWarning("Attempt to call with an empty override material. Skipping the call to avoid errors");
                return;
            }
            
            if (SnowfallWeather.Instance == null)
            {
                Debug.LogWarning("Attempt to call with an uninitialized SnowfallWeather. Skipping the call to avoid errors");
                return;
            }

            if (SnowfallWeather.Instance.levelDepthmap == null || SnowfallWeather.Instance.snowTracksMap == null)
            {
                Debug.LogWarning(" Attempt to call with uninitialized textures. Skipping the call to avoid errors");
            }

            if (shaderPasses == null)
            {
                Debug.LogWarning("Attempt to call with an empty shader passes. Skipping the call to avoid errors");
                return;
            }

            if (shaderPasses.Length == 0)
            {
                Debug.LogWarning("Attempt to call with an empty shader passes. Skipping the call to avoid errors");
                return;
            }

            shaderPasses[shaderPasses.Length - 1] = new ShaderTagId(overrideMaterialPassName);

            RefreshSnowMaterial(overrideMaterial);
            RefreshSnowMaterial(snowVertexMaterial);

            var mask = overrideDepthState ? RenderStateMask.Depth : 0;
            mask |= overrideDepthState && !depthWrite ? RenderStateMask.Stencil : 0;
            if (overrideStencil)
                mask |= RenderStateMask.Stencil;
            var stateBlock = new RenderStateBlock(mask)
            {
                depthState = new DepthState(depthWrite, depthCompareFunction),
                stencilState = new StencilState(overrideStencil, (byte)stencilReadMask, (byte)stencilWriteMask, stencilCompareFunction, stencilPassOperation, stencilFailOperation, stencilDepthFailOperation),
                stencilReference = overrideStencil ? stencilReferenceValue : 0,
            };

            PerObjectData renderConfig = HDUtils.GetRendererConfiguration(ctx.hdCamera.frameSettings.IsEnabled(FrameSettingsField.ProbeVolume), ctx.hdCamera.frameSettings.IsEnabled(FrameSettingsField.Shadowmask));

            var result = new UnityEngine.Rendering.RendererUtils.RendererListDesc(shaderPasses, ctx.cullingResults, ctx.hdCamera.camera)
            {
                rendererConfiguration = renderConfig,
                renderQueueRange = GetRenderQueueRange(renderQueueType),
                sortingCriteria = sortingCriteria,
                excludeObjectMotionVectors = false,
                overrideMaterial = overrideMaterial,
                overrideMaterialPassIndex = (overrideMaterial != null) ? overrideMaterial.FindPass(overrideMaterialPassName) : 0,
                stateBlock = stateBlock,
                layerMask = layerMask,
                renderingLayerMask = (uint)renderingLayers
            };

            var renderCtx = ctx.renderContext;
            var rendererList = renderCtx.CreateRendererList(result);
            bool opaque = renderQueueType == RenderQueueType.AllOpaque || renderQueueType == RenderQueueType.OpaqueAlphaTest || renderQueueType == RenderQueueType.OpaqueNoAlphaTest;


            RenderForwardRendererList(ctx.hdCamera.frameSettings, rendererList, opaque, ctx.renderContext, ctx.cmd);
        }

        internal void SetupMaterial(Material material)
        {
            material.SetFloat(SnowfallShaderIDs.PCFKernelSize, SnowfallWeather.Instance!.PCFKernelSize);
            material.SetFloat(SnowfallShaderIDs.BaseTessellationFactor, SnowfallWeather.Instance!.baseTessellationFactor);
            material.SetFloat(SnowfallShaderIDs.MaxTessellationFactor, SnowfallWeather.Instance!.maxTessellationFactor);
            material.SetInt(SnowfallShaderIDs.isAdaptiveTessellation, SnowfallWeather.Instance!.isAdaptiveTessellation);
            // material.SetFloat(SnowfallShaderIDs.ShadowBias, SnowfallWeather.Instance!.shadowBias);
            // material.SetFloat(SnowfallShaderIDs.SnowOcclusionBias, SnowfallWeather.Instance!.snowOcclusionBias);

        }

        
        internal void RefreshSnowMaterial(Material material)
        {
            material.SetFloat(SnowfallShaderIDs.FadeValue, fadeValue);
            material.SetTexture(SnowfallShaderIDs.DepthTex, SnowfallWeather.Instance!.levelDepthmap);
            material.SetTexture(SnowfallShaderIDs.FootprintsTex, SnowfallWeather.Instance!.snowTracksMap);
            material.SetMatrix(SnowfallShaderIDs.FootprintsViewProjection, SnowfallWeather.Instance!.snowTracksCamera!.projectionMatrix * SnowfallWeather.Instance!.snowTracksCamera.worldToCameraMatrix);
            material.SetMatrix(SnowfallShaderIDs.LightViewProjection, SnowfallWeather.Instance!.levelDepthmapCamera!.projectionMatrix * SnowfallWeather.Instance!.levelDepthmapCamera.worldToCameraMatrix);
            material.SetFloat(SnowfallShaderIDs.SnowNoisePower, SnowfallWeather.Instance!.snowIntensity);
            material.SetFloat(SnowfallShaderIDs.SnowNoiseScale, SnowfallWeather.Instance!.snowScale);
            material.SetFloat(SnowfallShaderIDs.MaxSnowHeight, SnowfallWeather.Instance!.maxSnowHeight);
            material.SetVector(SnowfallShaderIDs.ShipPosition, SnowfallWeather.Instance!.shipPosition);
            material.SetFloat(SnowfallShaderIDs.Emission, SnowfallWeather.Instance!.emissionMultiplier);
        }

        internal void RenderForwardRendererList(FrameSettings frameSettings,
        RendererList rendererList,
        bool opaque,
        ScriptableRenderContext renderContext,
        CommandBuffer cmd)
        {
            // Note: SHADOWS_SHADOWMASK keyword is enabled in HDRenderPipeline.cs ConfigureForShadowMask
            bool useFptl = opaque && frameSettings.IsEnabled(FrameSettingsField.FPTLForForwardOpaque) && !forceClusteredLighting;

            // say that we want to use tile/cluster light loop
            CoreUtils.SetKeyword(cmd, "USE_FPTL_LIGHTLIST", useFptl);
            CoreUtils.SetKeyword(cmd, "USE_CLUSTERED_LIGHTLIST", !useFptl);

            if (opaque)
                DrawOpaqueRendererList(renderContext, cmd, frameSettings, rendererList);
            else
                DrawTransparentRendererList(renderContext, cmd, frameSettings, rendererList);
        }

        internal void DrawOpaqueRendererList(in ScriptableRenderContext renderContext, CommandBuffer cmd, in FrameSettings frameSettings, RendererList rendererList)
        {
            if (!frameSettings.IsEnabled(FrameSettingsField.OpaqueObjects))
                return;

            CoreUtils.DrawRendererList(renderContext, cmd, rendererList);
        }

        internal void DrawTransparentRendererList(in ScriptableRenderContext renderContext, CommandBuffer cmd, in FrameSettings frameSettings, RendererList rendererList)
        {
            if (!frameSettings.IsEnabled(FrameSettingsField.TransparentObjects))
                return;

            CoreUtils.DrawRendererList(renderContext, cmd, rendererList);
        }
    }
}