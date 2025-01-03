using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;
using UnityEngine.Experimental.Rendering;

namespace VoxxWeatherPlugin.Behaviours
{
    // TODO MAKE FULLSCREEN PASS
    [Serializable, VolumeComponentMenu("Post-processing/Custom/Glitch Effect")]
    public sealed class GlitchEffect : CustomPass
    {
        [Tooltip("Overall glitch intensity.")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("Strength of block glitching.")]
        public ClampedFloatParameter blockStrength = new ClampedFloatParameter(.7f, 0f, 1f);

        [Tooltip("Horizontal drift intensity.")]
        public ClampedFloatParameter drift = new ClampedFloatParameter(1.5f, 0f, 2f);

        [Tooltip("Vertical jump intensity.")]
        public ClampedFloatParameter jump = new ClampedFloatParameter(.4f, 0f, 1f);

        [Tooltip("Random shaking intensity.")]
        public ClampedFloatParameter shake = new ClampedFloatParameter(.7f, 0f, 1f);

        [Tooltip("Shader material.")]
        public Material? m_Material;

        float m_PrevTime;
        float m_JumpTime;
        int m_BlockSeed1 = 71;
        int m_BlockSeed2 = 113;
        int m_BlockStride = 1;
        float m_BlockTime;

        RTHandle? tempColorBuffer = null;

        static class ShaderIDs
        {
            internal static readonly int InputTexture = Shader.PropertyToID("_InputTexture");
            internal static readonly int BlockStrength = Shader.PropertyToID("_BlockStrength");
            internal static readonly int BlockStride = Shader.PropertyToID("_BlockStride");
            internal static readonly int BlockSeed1 = Shader.PropertyToID("_BlockSeed1");
            internal static readonly int BlockSeed2 = Shader.PropertyToID("_BlockSeed2");
            internal static readonly int Drift = Shader.PropertyToID("_Drift");
            internal static readonly int Jump = Shader.PropertyToID("_Jump");
            internal static readonly int Shake = Shader.PropertyToID("_Shake");
            internal static readonly int Seed = Shader.PropertyToID("_Seed");
        }

        protected override bool executeInSceneView => true;

        protected override void Execute(CustomPassContext ctx)
        {
            if (m_Material == null)
            {
                Debug.LogError("Missing shader for Glitch Effect.");
                return;
            }

            if (DebugCheckBuffers(ctx))
            {
                return;
            }

            // TODO THREAD BLOCKING: This is a blocking call, it will wait for the GPU to finish rendering
            // Try to change to fullscreen pass
            HDUtils.BlitCameraTexture(ctx.cmd, ctx.cameraColorBuffer, tempColorBuffer);
            //Async Blit
            // CommandBuffer copyCmd = new CommandBuffer();
            // copyCmd.CopyTexture(ctx.cameraColorBuffer, tempColorBuffer);
            // Graphics.ExecuteCommandBufferAsync(copyCmd, ComputeQueueType.Background);
            // Doesn't work

            float time = Time.time;
            float delta = time - m_PrevTime;
            m_JumpTime += delta * jump.value * 11.3f;
            m_PrevTime = time;

            // Block parameters
            float block3 = blockStrength.value * blockStrength.value * blockStrength.value;

            // Shuffle block parameters every 1/30 seconds.
            m_BlockTime += delta * 60;
            if (m_BlockTime > 1)
            {
                if (UnityEngine.Random.value < 0.09f) m_BlockSeed1 += 251;
                if (UnityEngine.Random.value < 0.29f) m_BlockSeed2 += 373;
                if (UnityEngine.Random.value < 0.25f) m_BlockStride = UnityEngine.Random.Range(1, 32);
                m_BlockTime = 0;
            }

            m_Material.SetFloat(ShaderIDs.BlockStrength, block3 * intensity.value);
            m_Material.SetInt(ShaderIDs.BlockStride, m_BlockStride);
            m_Material.SetInt(ShaderIDs.BlockSeed1, m_BlockSeed1);
            m_Material.SetInt(ShaderIDs.BlockSeed2, m_BlockSeed2);
            m_Material.SetVector(ShaderIDs.Drift, new Vector2(time * 606.11f % (Mathf.PI * 2), drift.value * 0.04f * intensity.value));
            m_Material.SetVector(ShaderIDs.Jump, new Vector2(m_JumpTime, jump.value * intensity.value));
            m_Material.SetFloat(ShaderIDs.Shake, shake.value * 0.2f * intensity.value);
            m_Material.SetInt(ShaderIDs.Seed, (int)(time * 10000));
            m_Material.SetTexture(ShaderIDs.InputTexture, tempColorBuffer);

            CoreUtils.SetRenderTarget(ctx.cmd, ctx.cameraColorBuffer, ClearFlag.None);
            CoreUtils.DrawFullScreen(ctx.cmd, m_Material, ctx.propertyBlock, shaderPassId: 0);
        }

        protected override void Cleanup()
        {
            tempColorBuffer?.Release();
            base.Cleanup();
        }

        internal bool DebugCheckBuffers(CustomPassContext ctx)
        {
            bool hasIssue = false;

            // Check tempColorBuffer
            if (tempColorBuffer == null)
            {
                Debug.LogWarning($"[Frame {Time.frameCount}] tempColorBuffer is null, allocating new buffer");
                UpdateTempBuffer(ctx);
                hasIssue = true;
            }
            else if (tempColorBuffer.rt == null)
            {
                Debug.LogWarning($"[Frame {Time.frameCount}] tempColorBuffer.rt is null");
                UpdateTempBuffer(ctx);
                hasIssue = true;
            }

            // Check ctx.cameraColorBuffer
            if (ctx.cameraColorBuffer == null)
            {
                Debug.LogError($"[Frame {Time.frameCount}] ctx.cameraColorBuffer is null");
                hasIssue = true;
            }
            else if (ctx.cameraColorBuffer.rt == null)
            {
                Debug.LogError($"[Frame {Time.frameCount}] ctx.cameraColorBuffer.rt is null");
                hasIssue = true;
            }

            // Check camera information
            if (ctx.hdCamera == null || ctx.hdCamera?.camera == null)
            {
                Debug.LogError($"[Frame {Time.frameCount}] ctx.hdCamera or ctx.hdCamera.camera is null. " +
                               $"ctx.hdCamera: {ctx.hdCamera == null}, ctx.hdCamera.camera: {ctx.hdCamera?.camera == null}");
                hasIssue = true;
            }
            else if (!ctx.hdCamera.camera.enabled)
            {
                Debug.LogError($"[Frame {Time.frameCount}] Camera {ctx.hdCamera.camera.name} is disabled");
                hasIssue = true;
            }

            // Check if dimensions match
            if (tempColorBuffer?.rt != null && ctx.cameraColorBuffer?.rt != null)
            {
                if (tempColorBuffer.rt.width != ctx.cameraColorBuffer.rt.width ||
                    tempColorBuffer.rt.height != ctx.cameraColorBuffer.rt.height)
                {
                    Debug.LogWarning($"[Frame {Time.frameCount}] Buffer dimensions do not match! " +
                                    $"tempColorBuffer: {tempColorBuffer.rt.width}x{tempColorBuffer.rt.height}, " +
                                    $"cameraColorBuffer: {ctx.cameraColorBuffer.rt.width}x{ctx.cameraColorBuffer.rt.height}");
                    hasIssue = true;
                    UpdateTempBuffer(ctx);
                }
            }

            return hasIssue;
        }

        private void UpdateTempBuffer(CustomPassContext ctx)
        {
            if (tempColorBuffer != null)
            {
                Debug.LogDebug("GlitchPass: Releasing temp color buffer!");
                tempColorBuffer.Release();
            }

            tempColorBuffer = RTHandles.Alloc(
                ctx.cameraColorBuffer.rt.width,
                ctx.cameraColorBuffer.rt.height,
                TextureXR.slices,
                dimension: TextureXR.dimension,
                colorFormat: GraphicsFormat.B10G11R11_UFloatPack32,
                useDynamicScale: true,
                name: "Glitch Buffer"
            );

            Debug.LogDebug("GlitchPass: Allocated temp color buffer!");
        }
    }
}