using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using VoxxWeatherPlugin.Utils;

namespace VoxxWeatherPlugin.Patches
{
    [HarmonyPatch]
    internal class PlayerControllerBTemperaturePatch
    {
        private static float prevSprintMeter;
        private static float severityInfluenceMultiplier = 1.25f;
        private static float timeToCool = 17f;

        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)]
        private static void HeatStrokePatchPrefix(PlayerControllerB __instance)
        {
            if (!((NetworkBehaviour)__instance).IsOwner || !__instance.isPlayerControlled)
                return;
            PlayerControllerBTemperaturePatch.prevSprintMeter = __instance.sprintMeter;
        }

        [HarmonyPatch(typeof(PlayerControllerB), "LateUpdate")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        private static void HeatStrokePatchLatePostfix(PlayerControllerB __instance)
        {
            if (!((NetworkBehaviour)__instance).IsOwner || !__instance.isPlayerControlled)
                return;

            if (__instance.isInsideFactory)
            {
                PlayerHeatManager.heatSeverityMultiplier = 1f;
                PlayerHeatManager.isInHeatZone = false;
            }

            if (!PlayerHeatManager.isInHeatZone)
            {
                PlayerHeatManager.SetHeatSeverity(-Time.deltaTime / timeToCool);
            }


            float severity = PlayerHeatManager.heatSeverity;

            //Debug.Log($"Severity: {severity}, inHeatZone: {PlayerHeatManager.isInHeatZone}, heatMultiplier {PlayerHeatManager.heatSeverityMultiplier}, isInside {__instance.isInsideFactory}");

            if (severity > 0)
            {
                float delta = __instance.sprintMeter - PlayerControllerBTemperaturePatch.prevSprintMeter;
                if (delta < 0.0) //Stamina consumed
                    __instance.sprintMeter = Mathf.Max(PlayerControllerBTemperaturePatch.prevSprintMeter + delta * (1 + severity * severityInfluenceMultiplier), 0.0f);
                else if (delta > 0.0) //Stamina regenerated
                    __instance.sprintMeter = Mathf.Min(PlayerControllerBTemperaturePatch.prevSprintMeter + delta / (1 + severity * severityInfluenceMultiplier), 1f);
            }
        }
    }
}
