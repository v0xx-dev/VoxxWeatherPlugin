using GameNetcodeStuff;
using HarmonyLib;
using LethalLib.Modules;
using System.Collections.Generic;
using System.Reflection.Emit;
using Unity.Netcode;
using UnityEngine;
using VoxxWeatherPlugin.Utils;
using VoxxWeatherPlugin.Weathers;

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
            if (!(HeatwaveWeather.Instance?.IsActive ?? false) || !__instance.IsOwner || !__instance.isPlayerControlled)
                return;
            prevSprintMeter = __instance.sprintMeter;
        }

        [HarmonyPatch(typeof(PlayerControllerB), "LateUpdate")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        private static void HeatStrokePatchLatePostfix(PlayerControllerB __instance)
        {
            // if (!((NetworkBehaviour)__instance).IsOwner || !__instance.isPlayerControlled)
            //     return;

            if (!(HeatwaveWeather.Instance?.IsActive ?? false) && Mathf.Approximately(PlayerTemperatureManager.heatSeverity, 0))
            {
                return;
            }

            if (PlayerTemperatureManager.isInHeatZone)
            {
                if (CheckConditionsForHeatingStop(__instance))
                {
                    PlayerTemperatureManager.heatTransferRate = 1f;
                    PlayerTemperatureManager.isInHeatZone = false;
                }
                else if (CheckConditionsForHeatingPause(__instance))
                {
                    PlayerTemperatureManager.heatTransferRate = .25f; //heat slower when in special interact animation and in a car
                }
                else
                {
                    PlayerTemperatureManager.heatTransferRate = 1f;
                }
            }

            // Gradually reduce heat severity when not in heat zone
            if (!PlayerTemperatureManager.isInHeatZone)
            {
                PlayerTemperatureManager.ResetPlayerTemperature(Time.deltaTime / timeToCool);
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

        internal static bool CheckConditionsForHeatingPause(PlayerControllerB playerController)
        {
            return playerController.inSpecialInteractAnimation || playerController.inAnimationWithEnemy || playerController.isClimbingLadder || playerController.physicsParent != null;
        }

        internal static bool CheckConditionsForHeatingStop(PlayerControllerB playerController)
        {
            return playerController.beamUpParticle.isPlaying || playerController.isInElevator || 
                    playerController.isInHangarShipRoom || playerController.isUnderwater ||
                     playerController.isPlayerDead || playerController.isInsideFactory ||
                     (playerController.currentAudioTrigger?.setInsideAtmosphere ?? false);
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
                    // Replace the original target
                    Label jumpTarget = generator.DefineLabel();
                    codes[i + 3] = new CodeInstruction(OpCodes.Bgt_S, jumpTarget);

                    // Insert the additional condition
                    codes.InsertRange(i + 4, new[]
                    {
                        new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(PlayerTemperatureManager), "heatSeverity")),
                        new CodeInstruction(OpCodes.Ldc_R4, 0.85f),
                        new CodeInstruction(OpCodes.Ble_Un_S, originalJumpTarget)
                    });
                    // Connect the new jump target
                    codes[i + 7].labels.Add(jumpTarget);

                    break;
                }
            }
            return codes;
        }

    //     [HarmonyPatch(typeof(VehicleController), "Start")]
    //     [HarmonyPrefix]
    //     private static void VehicleHeaterPatch(VehicleController __instance)
    //     {
    //         VehicleHeatwaveHandler vehicleHeater = __instance.gameObject.AddComponent<VehicleHeatwaveHandler>();
    //     }
    }
}
