using HarmonyLib;
using VoxxWeatherPlugin.Weathers;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection.Emit;
using GameNetcodeStuff;
using UnityEngine.VFX;
using VoxxWeatherPlugin.Utils;
using System;

namespace VoxxWeatherPlugin.Patches
{
    [HarmonyPatch]
    internal class SnowPatches
    {
        public static Dictionary<MonoBehaviour, VisualEffect> snowTrackersDict = new Dictionary<MonoBehaviour, VisualEffect>();
        public static Dictionary<MonoBehaviour, VisualEffect> snowShovelDict = new Dictionary<MonoBehaviour, VisualEffect>();
        public static float timeToWarm = 17f;   // Time to warm up from cold to room temperature
        internal static float frostbiteTimer = 0f;
        internal static float frostbiteDamageInterval = 10f;
        internal static int frostbiteDamage = 10;
        internal static float frostbiteThreshold = -0.5f; // Severity at which frostbite starts to occur

        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        [HarmonyTranspiler]
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
                new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(SnowfallVFXManager), "snowMovementHindranceMultiplier")),
                new CodeInstruction(OpCodes.Div),        // Divide num3 by hindrance multiplier
                new CodeInstruction(OpCodes.Stloc_S, num3Index)  // Store the modified value back
            );
            Debug.Log("Patched PlayerControllerB.Update to include snow hindrance!");
            return codeMatcher.InstructionEnumeration();
        }

        [HarmonyPatch(typeof(PlayerControllerB), "LateUpdate")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        private static void FrostbiteLatePostfix(PlayerControllerB __instance)
        {
            //PlayerTemperatureManager.heatTransferRate = 1f; // should be 1 if heatwave patches work correctly

            // Gradually reduce heat severity when not in heat zone
            if (!PlayerTemperatureManager.isInColdZone)
            {
                PlayerTemperatureManager.ResetPlayerTemperature(Time.deltaTime / timeToWarm);
            }

            float severity = PlayerTemperatureManager.normalizedTemperature;

            Debug.LogDebug($"Severity: {severity}, inColdZone: {PlayerTemperatureManager.isInColdZone}, frostbiteTimer: {frostbiteTimer}");
            if (severity <= frostbiteThreshold)
            {
                frostbiteTimer += Time.deltaTime;
                if (frostbiteTimer > frostbiteDamageInterval)
                {
                    __instance.DamagePlayer(Mathf.CeilToInt(frostbiteDamage*Mathf.Abs(severity)), causeOfDeath: CauseOfDeath.Unknown);
                    frostbiteTimer = 0f;
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), "GetCurrentMaterialStandingOn")]
        [HarmonyPrefix]
        private static bool SnowFootstepsSoundPatch(PlayerControllerB __instance)
        {
            // TODO Currently will switch the sounds only if the player is the local player
            if (SnowfallVFXManager.snowThickness > 0.1f && __instance == GameNetworkManager.Instance.localPlayerController)
            {
                if (SnowfallVFXManager.snowFootstepIndex != -1)
                {
                    __instance.currentFootstepSurfaceIndex = SnowfallVFXManager.snowFootstepIndex;
                    return false;
                }
            }
            return true;
        }

        [HarmonyPatch(typeof(PlayerControllerB), "Start")]
        [HarmonyPostfix]
        private static void PlayerSnowTracksPatch(PlayerControllerB __instance)
        {
            AddFootprintTracker(__instance, 3f, 1f, 0.2f);
        }

        [HarmonyPatch(typeof(EnemyAI), "Start")]
        [HarmonyPostfix]
        private static void EnemySnowTracksPatch(EnemyAI __instance)
        {
            AddFootprintTracker(__instance, 2f, 0.167f, 0.2f);
        }

        [HarmonyPatch(typeof(GrabbableObject), "Start")]
        [HarmonyPostfix]
        private static void GrabbableSnowTracksPatch(GrabbableObject __instance)
        {
            AddFootprintTracker(__instance, 2f, 0.167f, 0.7f);
        }
        
        [HarmonyPatch(typeof(VehicleController), "Start")]
        [HarmonyPostfix]
        private static void VehicleSnowTracksPatch(VehicleController __instance)
        {
            AddFootprintTracker(__instance, 6f, 0.75f, 1f);
        }
        
        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        [HarmonyPostfix]
        private static void PlayerSnowTracksUpdatePatch(PlayerControllerB __instance)
        {
            //Maybe create a more robust condition for when to track footprints (see SnowThicknessCalculator.isOnNaturalGround)
            PauseFootprintTracker(__instance, !__instance.isInsideFactory && __instance.thisController.isGrounded);
        }

        [HarmonyPatch(typeof(EnemyAI), "Update")]
        [HarmonyPostfix]
        private static void EnemySnowTracksUpdatePatch(EnemyAI __instance)
        {
            PauseFootprintTracker(__instance, __instance.isOutside && __instance.agent.isOnNavMesh);
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

        [HarmonyPatch(typeof(GrabbableObject), "FallToGround")]
        [HarmonyPrefix]
        private static void GrabbableFallSnowPatch(GrabbableObject __instance)
        {
            PlayFootprintTracker(__instance, snowTrackersDict, !__instance.isInFactory);
        }

        [HarmonyPatch(typeof(Shovel), "HitShovelClientRpc")]
        [HarmonyPostfix]
        private static void ShovelSnowPatch(Shovel __instance)
        {
            PlayFootprintTracker(__instance, snowShovelDict, !__instance.isInFactory && SnowfallVFXManager.snowThickness > 0.25f);
        }

        public static void AddFootprintTracker(MonoBehaviour obj, float particleSize, float lifetimeMultiplier, float footprintStrength)
        {
            //load different footprints for player and other objects use switch case
            GameObject footprintsTrackerVariant = obj switch
            {
                EnemyAI _ => SnowfallVFXManager.lowcapFootprintsTrackerVFX,
                GrabbableObject _ => SnowfallVFXManager.itemTrackerVFX,
                _ => SnowfallVFXManager.footprintsTrackerVFX //PlayerControllerB and VehicleController
            };

            GameObject footprintsTracker = GameObject.Instantiate(footprintsTrackerVariant,
                                                                    position: obj.transform.position,
                                                                    rotation: Quaternion.identity,
                                                                    parent: obj.transform);
            VisualEffect footprintsTrackerVFX = footprintsTracker.GetComponent<VisualEffect>();
            footprintsTrackerVFX.SetFloat("particleSize", particleSize);
            footprintsTrackerVFX.SetFloat("lifetimeMultiplier", lifetimeMultiplier);
            footprintsTrackerVFX.SetFloat("footprintStrength", footprintStrength);
            snowTrackersDict.Add(obj, footprintsTrackerVFX);

            if (obj is Shovel shovel)
            {
                GameObject shovelVFXObject = GameObject.Instantiate(SnowfallVFXManager.shovelVFX,
                                                            position: obj.transform.position,
                                                            rotation: Quaternion.identity,
                                                            parent: obj.transform);
                VisualEffect shovelVFX = shovelVFXObject.GetComponent<VisualEffect>();
                snowShovelDict.Add(shovel, shovelVFX);
            }
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

        public static void PlayFootprintTracker(MonoBehaviour obj, Dictionary<MonoBehaviour, VisualEffect> dictForVFX, bool playCondition = false)
        {
            if (dictForVFX.TryGetValue(obj, out VisualEffect footprintsTrackerVFX) && playCondition)
            {
                footprintsTrackerVFX.Play();
            }
        }
    }

}


