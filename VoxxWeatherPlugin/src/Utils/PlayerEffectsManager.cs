using UnityEngine.Rendering;
using UnityEngine;
using System;
using VoxxWeatherPlugin.Patches;
using GameNetcodeStuff;

namespace VoxxWeatherPlugin.Utils
{
    internal class PlayerEffectsManager
    {
        public static bool isInHeatZone = false;
        public static bool isInColdZone = false;
        public static bool isPoisoned = false;
        public static float heatTransferRate = 1f;
        public static float normalizedTemperature = 0f; // 0 - room temperature, 1 - heatstroke, -1 - hypothermia
        public static float poisoningStrength = 0f;
        public static float HeatSeverity => Mathf.Clamp01(normalizedTemperature);
        public static float ColdSeverity => Mathf.Clamp01(-normalizedTemperature);

        internal static Volume? heatEffectVolume;
        internal static Volume? freezeEffectVolume;

        private static float HeatVisualMultiplier => Configuration.HeathazeFilterMultiplier.Value;
        private static float ColdVisualMultiplier => Configuration.frostbiteFilterMultiplier.Value;

        internal static void SetPlayerTemperature(float temperatureDelta)
        {
            normalizedTemperature = Mathf.Clamp(normalizedTemperature + temperatureDelta * heatTransferRate, -1, 1);

            if (heatEffectVolume != null)
            {
                heatEffectVolume.weight = HeatSeverity * HeatVisualMultiplier; // Only show heat effect if temperature > 0
            }
            if (freezeEffectVolume != null)
            {
                // Game shows overlay only at 0.9 weight for some reason, and we want it to be visible at frostbiteThreshold, so we need to remap the values:
                freezeEffectVolume.weight = ColdVisualMultiplier * Mathf.Min(ColdSeverity/SnowPatches.frostbiteThreshold*0.9f, (0.1f*ColdSeverity + 0.9f - SnowPatches.frostbiteThreshold)/(1f - SnowPatches.frostbiteThreshold));
            }
        }

        internal static void ResetPlayerTemperature(float temperatureDelta)
        {
            // Gradually reset temperature to 0
            SetPlayerTemperature(-Mathf.Sign(normalizedTemperature) * temperatureDelta);
        }

        internal static void SetPoisoningEffect(float poisonDelta)
        {
            // If poisonDelta is Time.deltaTime, then poisoningStrength will be increased by 0.2 per second, will reach 1 in 5 seconds
            poisoningStrength = Mathf.Clamp(poisoningStrength + poisonDelta*0.2f, 0, 1f);
            PlayerControllerB localPlayerController = GameNetworkManager.Instance.localPlayerController;
            float currentDrunknessFilterWeight = HUDManager.Instance.drunknessFilter.weight;
            float newDrunknessFilterWeight = Mathf.Max(currentDrunknessFilterWeight, poisoningStrength);
            // Set visual effect
            HUDManager.Instance.drunknessFilter.weight = newDrunknessFilterWeight;
            HUDManager.Instance.gasImageAlpha.alpha = HUDManager.Instance.drunknessFilter.weight * 1.5f;
            // Set audio effect
            SoundManager.Instance.playerVoicePitchTargets[localPlayerController.playerClientId] = newDrunknessFilterWeight <= 0.15f ? 1f : 1f + newDrunknessFilterWeight;
        }
        
    }
}
