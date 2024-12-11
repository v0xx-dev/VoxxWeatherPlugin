using HarmonyLib;
using VoxxWeatherPlugin.Weathers;
using UnityEngine;

namespace VoxxWeatherPlugin.Patches
{
    [HarmonyPatch]
    internal class BlizzardPatches
    {
        
        [HarmonyPatch(typeof(MouthDogAI), "DetectNoise")]
        [HarmonyPrefix]
        private static void DogSoundMufflingPatch(MouthDogAI __instance, ref float noiseLoudness)
        {
            if (SnowfallWeather.Instance is BlizzardWeather blizzardWeather &&
                 blizzardWeather.IsActive &&
                 __instance.isOutside)
            {
                // Muffle dogs hearing during blizzard, depending on wind force. Muffled by 50% at wind force > 0.5, not muffled at wind force = 0
                noiseLoudness *= Mathf.Clamp(1 - blizzardWeather.windForce, 0.5f, 1f);
            }
        }
    }

}


