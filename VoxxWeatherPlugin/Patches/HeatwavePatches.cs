using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Unity.Netcode;
using UnityEngine;
using VoxxWeatherPlugin.Utils;

namespace VoxxWeatherPlugin.Patches
{
    [HarmonyPatch]
    internal class HeatwavePatches
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
            HeatwavePatches.prevSprintMeter = __instance.sprintMeter;
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
                PlayerTemperatureManager.heatSeverityMultiplier = 1f;
                PlayerTemperatureManager.isInHeatZone = false;
            }

            if (!PlayerTemperatureManager.isInHeatZone || __instance)
            {
                PlayerTemperatureManager.SetHeatSeverity(-Time.deltaTime / timeToCool);
            }

            float severity = PlayerTemperatureManager.heatSeverity;

            //Debug.Log($"Severity: {severity}, inHeatZone: {PlayerTemperatureManager.isInHeatZone}, heatMultiplier {PlayerTemperatureManager.heatSeverityMultiplier}, isInside {__instance.isInsideFactory}");

            if (severity > 0)
            {
                float delta = __instance.sprintMeter - HeatwavePatches.prevSprintMeter;
                if (delta < 0.0) //Stamina consumed
                    __instance.sprintMeter = Mathf.Max(HeatwavePatches.prevSprintMeter + delta * (1 + severity * severityInfluenceMultiplier), 0.0f);
                else if (delta > 0.0) //Stamina regenerated
                    __instance.sprintMeter = Mathf.Min(HeatwavePatches.prevSprintMeter + delta / (1 + severity * severityInfluenceMultiplier), 1f);
            }
        }

        [HarmonyPatch(typeof(SoundManager), "SetAudioFilters")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> HeatstrokeAudioPatch(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);
            
            for (int i = 0; i < codes.Count - 2; i++)
            {
                if (codes[i].opcode == OpCodes.Ldfld && 
                    codes[i].operand.ToString().Contains("drunkness") &&
                    codes[i + 1].opcode == OpCodes.Callvirt &&
                    codes[i + 1].operand.ToString().Contains("Evaluate") &&
                    codes[i + 2].opcode == OpCodes.Ldc_R4 &&
                    (float)codes[i + 2].operand == 0.6f)
                {
                    // Store the original jump target
                    object originalJumpTarget = codes[i + 3].operand;
                    // Replace the original ble.un.s with bgt.s
                    Label jumpTarget = generator.DefineLabel();
                    codes[i + 3] = new CodeInstruction(OpCodes.Bgt_S, jumpTarget);

                    // Insert the additional condition for PlayerTemperatureManager.heatSeverity
                    codes.InsertRange(i + 4, new[]
                    {
                        new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(PlayerTemperatureManager), "heatSeverity")),
                        new CodeInstruction(OpCodes.Ldc_R4, 0.85f),
                        new CodeInstruction(OpCodes.Ble_Un_S, originalJumpTarget)
                    });
                    codes[i + 7].labels.Add(jumpTarget); 

                    break;
                }
            }

            return codes;
        }
    }
}
