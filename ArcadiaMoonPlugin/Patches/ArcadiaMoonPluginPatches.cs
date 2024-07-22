using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace ArcadiaMoonPlugin.Patches
{
    [HarmonyPatch]
    internal class PlayerControllerBHeatStrokePatch
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
            PlayerControllerBHeatStrokePatch.prevSprintMeter = __instance.sprintMeter;
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
                float delta = __instance.sprintMeter - PlayerControllerBHeatStrokePatch.prevSprintMeter;
                if (delta < 0.0) //Stamina consumed
                    __instance.sprintMeter = Mathf.Max(PlayerControllerBHeatStrokePatch.prevSprintMeter + delta * (1 + severity * severityInfluenceMultiplier), 0.0f);
                else if (delta > 0.0) //Stamina regenerated
                    __instance.sprintMeter = Mathf.Min(PlayerControllerBHeatStrokePatch.prevSprintMeter + delta / (1 + severity * severityInfluenceMultiplier), 1f);
            }
        }



    }
}


