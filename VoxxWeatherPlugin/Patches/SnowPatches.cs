using HarmonyLib;
using VoxxWeatherPlugin.Weathers;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection.Emit;
using GameNetcodeStuff;
using UnityEngine.VFX;

namespace VoxxWeatherPlugin.Patches
{
    [HarmonyPatch]
    internal class SnowPatches
    {
        public static Dictionary<MonoBehaviour, VisualEffect> snowTrackersDict = new Dictionary<MonoBehaviour, VisualEffect>();

        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        [HarmonyTranspiler]
        [HarmonyDebug]
        private static IEnumerable<CodeInstruction> SnowHindranceTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);
            codeMatcher.MatchForward(true,
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerControllerB), "movementSpeed")),
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerControllerB), "carryWeight")),
                new CodeMatch(OpCodes.Div),
                new CodeMatch(OpCodes.Stloc_S)
            );
            if (!codeMatcher.IsValid)
            {
                Debug.LogError("Failed to match code in SnowHindranceTranspiler");
                return instructions;
            }
            
            var num3Index = ((LocalBuilder)codeMatcher.Operand).LocalIndex;

            codeMatcher.Advance(1); 

            codeMatcher.InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldloc_S, num3Index), // Load num3 onto the stack
                new CodeInstruction(OpCodes.Ldc_R4, SnowfallVFXManager.snowMovementHindranceMultiplier), // Load the multiplier onto the stack
                new CodeInstruction(OpCodes.Div),        // Divide num3 by hindrance multiplier
                new CodeInstruction(OpCodes.Stloc_S, num3Index)  // Store the modified value back
            );
            return codeMatcher.InstructionEnumeration();
        }

        [HarmonyPatch(typeof(EnemyAI), "Start")]
        [HarmonyPostfix]
        private static void EnemySnowTracksPatch(EnemyAI __instance)
        {
            AddFootprintTracker(__instance, 2f, 0.2f);
        }

        [HarmonyPatch(typeof(PlayerControllerB), "Start")]
        [HarmonyPostfix]
        private static void PlayerSnowTracksPatch(PlayerControllerB __instance)
        {
            AddFootprintTracker(__instance, 3f, 1f);
        }

        [HarmonyPatch(typeof(GrabbableObject), "Start")]
        [HarmonyPostfix]
        private static void GrabbableSnowTracksPatch(GrabbableObject __instance)
        {
            AddFootprintTracker(__instance, 1f, 0.1f);
        }
        
        [HarmonyPatch(typeof(VehicleController), "Start")]
        [HarmonyPostfix]
        private static void VehicleSnowTracksPatch(VehicleController __instance)
        {
            AddFootprintTracker(__instance, 6f, 0.75f);
        }

        [HarmonyPatch(typeof(EnemyAI), "Update")]
        [HarmonyPostfix]
        private static void EnemySnowTracksUpdatePatch(EnemyAI __instance)
        {
            PauseFootprintTracker(__instance, __instance.isOutside && __instance.agent.isOnNavMesh);
        }

        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        [HarmonyPostfix]
        private static void PlayerSnowTracksUpdatePatch(PlayerControllerB __instance)
        {
            //Maybe create a more robust condition for when to track footprints (see SnowThicknessCalculator.isOnNaturalGround)
            PauseFootprintTracker(__instance, !__instance.isInsideFactory && __instance.thisController.isGrounded);
        }

        [HarmonyPatch(typeof(GrabbableObject), "Update")]
        [HarmonyPostfix]
        private static void GrabbableSnowTracksUpdatePatch(GrabbableObject __instance)
        {
            PauseFootprintTracker(__instance, !__instance.isInFactory);
        }

        [HarmonyPatch(typeof(VehicleController), "Update")]
        [HarmonyPostfix]
        private static void VehicleSnowTracksUpdatePatch(VehicleController __instance)
        {
            PauseFootprintTracker(__instance, __instance.FrontLeftWheel.isGrounded || __instance.FrontRightWheel.isGrounded ||
                                              __instance.BackLeftWheel.isGrounded || __instance.BackRightWheel.isGrounded);
        }

        public static void AddFootprintTracker(MonoBehaviour obj, float particleSize, float lifetimeMultiplier)
        {
            GameObject footprintsTracker = GameObject.Instantiate(SnowfallWeather.Instance.footprintsTrackerVFX,
                                                                    position: obj.transform.position,
                                                                    rotation: Quaternion.identity,
                                                                    parent: obj.transform);
            VisualEffect footprintsTrackerVFX = footprintsTracker.GetComponent<VisualEffect>();
            footprintsTrackerVFX.SetFloat("particleSize", particleSize);
            footprintsTrackerVFX.SetFloat("lifetimeMultiplier", lifetimeMultiplier);
            footprintsTracker.SetActive(false);
            snowTrackersDict.Add(obj, footprintsTrackerVFX);
        }

        public static void PauseFootprintTracker(MonoBehaviour obj, bool pauseCondition)
        {
            if (snowTrackersDict.TryGetValue(obj, out VisualEffect footprintsTrackerVFX))
            {
                bool trackingNeedsUpdating = footprintsTrackerVFX.GetBool("isTracking") ^ pauseCondition;
                if (trackingNeedsUpdating)
                {
                    footprintsTrackerVFX.SetBool("isTracking", pauseCondition);
                }
            }
        }
    }

}


