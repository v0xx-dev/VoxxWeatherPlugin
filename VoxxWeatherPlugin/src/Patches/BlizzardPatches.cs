﻿using HarmonyLib;
using VoxxWeatherPlugin.Weathers;
using UnityEngine;
using System.Collections.Generic;
using GameNetcodeStuff;
using VoxxWeatherPlugin.Utils;

namespace VoxxWeatherPlugin.Patches
{
    [HarmonyPatch]
    internal class BlizzardPatches
    {
        private static List<SpawnableEnemyWithRarity> cachedBees = [];
        
        [HarmonyPatch(typeof(MouthDogAI), "DetectNoise")]
        [HarmonyPrefix]
        private static void DogSoundMufflingPatch(MouthDogAI __instance, ref float noiseLoudness)
        {
            if (SnowfallWeather.Instance is BlizzardWeather blizzardWeather &&
                 blizzardWeather.IsActive &&
                 __instance.isOutside)
            {
                // Muffle dogs hearing during blizzard, depending on wind force. Muffled by 35% at wind force > 0.5, not muffled at wind force = 0
                noiseLoudness *= Mathf.Clamp(1 - blizzardWeather.windForce, 0.65f, 1f);
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), "CancelSpecialTriggerAnimations")]
        [HarmonyPrefix]
        private static bool LadderWindPatch(PlayerControllerB __instance)
        {
            // When player is too cold, players can't climb ladders during blizzards
            if (SnowfallWeather.Instance is BlizzardWeather blizzardWeather &&
                 blizzardWeather.IsActive &&
                 __instance.isClimbingLadder &&
                 blizzardWeather.isLocalPlayerInWind &&
                 PlayerEffectsManager.ColdSeverity < 0.8f)
            {
                return false;
            }
            return true;
        }

        // [HarmonyPatch(typeof(StartOfRound), "StartGame")]
        // [HarmonyPrefix]
        // private static void RemoveBeesSnowPatch(StartOfRound __instance)
        // {
        //     if (!__instance.IsHost || WeatherRegistry.WeatherManager.GetCurrentLevelWeather().Name == "Blizzard")
        //         return;

        //     cachedBees.Clear();
                
        //     for (int i = __instance.currentLevel.DaytimeEnemies.Count - 1; i >= 0; i--)
        //     {
        //         if (__instance.currentLevel.DaytimeEnemies[i].enemyType.name.ToLower().Contains("bees"))
        //         {
        //             // Cache the bees enemy to restore it after the blizzard and remove it from the list
        //             cachedBees.Add(__instance.currentLevel.DaytimeEnemies[i]);
        //             __instance.currentLevel.DaytimeEnemies.RemoveAt(i);
        //             Debug.LogDebug("Removing bees due to blizzard.");
        //         }
        //     }
        // }

        // [HarmonyPatch(typeof(StartOfRound), "EndOfGame")]
        // [HarmonyPrefix]
        // private static void RestoreBeesSnowPatch(StartOfRound __instance)
        // {
        //     if (!__instance.IsHost || !(SnowfallWeather.Instance is BlizzardWeather && cachedBees.Count > 0))
        //         return;
            
        //     __instance.currentLevel.DaytimeEnemies.AddRange(cachedBees);
        //     cachedBees.Clear();
        // }
    }

}


