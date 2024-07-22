using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Rendering;
using UnityEngine;

namespace VoxxWeatherPlugin.Utils
{
    internal class PlayerHeatManager
    {
        public static bool isInHeatZone = false;
        public static float heatSeverityMultiplier = 1f;
        public static float heatSeverity = 0f;

        private static Volume heatEffectVolume;

        public static void SetEffectsVolume(Volume volume)
        {
            if (volume != null)
            {
                heatEffectVolume = volume;
            }
        }

        internal static void SetHeatSeverity(float heatSeverityDelta)
        {
            heatSeverity = Mathf.Clamp01(heatSeverity + heatSeverityDelta * heatSeverityMultiplier);
            if (heatEffectVolume != null)
            {
                heatEffectVolume.weight = heatSeverity; // Adjust intensity of the visual effect
            }
        }
    }
}
