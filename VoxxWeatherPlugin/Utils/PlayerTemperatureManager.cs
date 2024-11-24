using UnityEngine.Rendering;
using UnityEngine;
using System;
using VoxxWeatherPlugin.Patches;

namespace VoxxWeatherPlugin.Utils
{
    internal class PlayerTemperatureManager
    {
        public static bool isInHeatZone = false;
        public static bool isInColdZone = false;
        public static float heatTransferRate = 1f;
        public static float normalizedTemperature = 0f; // 0 - room temperature, 1 - heatstroke, -1 - hypothermia

        internal static Volume heatEffectVolume;
        internal static Volume freezeEffectVolume;

        internal static void SetPlayerTemperature(float temperatureDelta)
        {
            normalizedTemperature = Mathf.Clamp(normalizedTemperature + temperatureDelta * heatTransferRate, -1, 1);

            if (heatEffectVolume != null)
            {
                heatEffectVolume.weight = Mathf.Clamp01(normalizedTemperature); // Only show heat effect if temperature > 0
            }
            if (freezeEffectVolume != null)
            {
                freezeEffectVolume.weight = Mathf.InverseLerp(SnowPatches.frostbiteThreshold, -1, normalizedTemperature); // Only show freeze effect if temperature < threshold
            }
        }

        internal static void ResetPlayerTemperature(float temperatureDelta)
        {
            // Gradually reset temperature to 0
            SetPlayerTemperature(-Mathf.Sign(normalizedTemperature) * temperatureDelta);
        }
        
    }
}
