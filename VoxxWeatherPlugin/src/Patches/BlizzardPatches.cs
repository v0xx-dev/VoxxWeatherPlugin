using HarmonyLib;
using VoxxWeatherPlugin.Weathers;
using UnityEngine;
using System.Runtime.CompilerServices;

namespace VoxxWeatherPlugin.Patches
{
    [HarmonyPatch]
    internal class BlizzardPatches
    {
        private static SpawnableEnemyWithRarity? cachedBees;
        
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

        [HarmonyPatch(typeof(StartOfRound), "StartGame")]
        [HarmonyPrefix]
        private static void RemoveBeesSnowPatch(StartOfRound __instance)
        {
            if (!__instance.IsHost || !(SnowfallWeather.Instance is BlizzardWeather blizzardWeather && blizzardWeather.IsActive))
                return;
                
            for (int i = 0; i < __instance.currentLevel.DaytimeEnemies.Count; i++)
            {
                if (__instance.currentLevel.DaytimeEnemies[i].enemyType.name == "Red Locust Bees")
                {
                    // Cache the bees enemy to restore it after the blizzard and remove it from the list
                    cachedBees = __instance.currentLevel.DaytimeEnemies[i];
                    __instance.currentLevel.DaytimeEnemies.RemoveAt(i);
                    break;
                }
            }
        }

        [HarmonyPatch(typeof(StartOfRound), "EndOfGame")]
        [HarmonyPrefix]
        private static void RestoreBeesSnowPatch(StartOfRound __instance)
        {
            if (!__instance.IsHost || !(SnowfallWeather.Instance is BlizzardWeather blizzardWeather && blizzardWeather.IsActive) || cachedBees == null)
                return;
            
            __instance.currentLevel.DaytimeEnemies.Add(cachedBees);
            cachedBees = null;
        }
    }

}


