using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;

namespace VoxxWeatherPlugin.Behaviours
{
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

            CoreUtils.SetRenderTarget(ctx.cmd, ctx.cameraColorBuffer, ClearFlag.None);
            CoreUtils.DrawFullScreen(ctx.cmd, m_Material, ctx.propertyBlock, shaderPassId: 0);
        }

        protected override void Cleanup()
        {
            base.Cleanup();
        }
    }
}