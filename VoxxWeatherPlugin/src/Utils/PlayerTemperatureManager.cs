using UnityEngine.Rendering;
using UnityEngine;
using System;
using VoxxWeatherPlugin.Patches;

namespace VoxxWeatherPlugin.Utils
{
    internal class PlayerEffectsManager
    {
        public static bool isInHeatZone = false;
        public static bool isInColdZone = false;
        public static bool isPoisoned = false;
        public static float heatTransferRate = 1f;
        public static float normalizedTemperature = 0f; // 0 - room temperature, 1 - heatstroke, -1 - hypothermia
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
        
    }
}
