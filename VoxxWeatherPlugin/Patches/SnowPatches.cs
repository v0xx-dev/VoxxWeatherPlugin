﻿using HarmonyLib;
using VoxxWeatherPlugin.Weathers;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection.Emit;
using GameNetcodeStuff;
using UnityEngine.VFX;
using VoxxWeatherPlugin.Utils;
using System;
using VoxxWeatherPlugin.Behaviours;

namespace VoxxWeatherPlugin.Patches
{
    [HarmonyPatch]
    internal class SnowPatches
    {
        public static Dictionary<MonoBehaviour, VisualEffect> snowTrackersDict = new Dictionary<MonoBehaviour, VisualEffect>();
        public static Dictionary<MonoBehaviour, VisualEffect> snowShovelDict = new Dictionary<MonoBehaviour, VisualEffect>();
        public static Dictionary<EnemyAI, (float, float)> agentSpeedCache = new Dictionary<EnemyAI, (float, float)>();
        public static float timeToWarm = 17f;   // Time to warm up from cold to room temperature
        internal static float frostbiteTimer = 0f;
        internal static float frostbiteDamageInterval = 10f;
        internal static int frostbiteDamage = 10;
        internal static float frostbiteThreshold = 0.5f; // Severity at which frostbite starts to occur

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

        [HarmonyPatch(typeof(PlayerControllerB), "GetCurrentMaterialStandingOn")]
        [HarmonyTranspiler]
        [HarmonyDebug]
        private static IEnumerable<CodeInstruction> GroundSamplingTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);
            codeMatcher.MatchForward(false,
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerControllerB), "interactRay")),
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldflda, AccessTools.Field(typeof(PlayerControllerB), "hit")),
                new CodeMatch(OpCodes.Ldc_R4), // 6f
                new CodeMatch(OpCodes.Call, AccessTools.PropertyGetter(typeof(StartOfRound), "Instance")),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(StartOfRound), "walkableSurfacesMask")),
                new CodeMatch(OpCodes.Ldc_I4_1), // QueryTriggerInteraction.Ignore
                new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Physics), nameof(Physics.Raycast), new[] { typeof(Ray), typeof(RaycastHit).MakeByRefType(), typeof(float), typeof(int), typeof(QueryTriggerInteraction) })),
                new CodeMatch(OpCodes.Brfalse_S) // Branch if Raycast fails
            );

            if (!codeMatcher.IsValid)
            {
                Debug.LogError("Failed to match code in GroundSamplingTranspiler");
                return instructions;
            }
            
            codeMatcher.RemoveInstructions(20);
            codeMatcher.InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SnowPatches), nameof(SurfaceSamplingOverride)))
            );

            Debug.Log("Patched PlayerControllerB.GetCurrentMaterialStandingOn to include snow thickness!");
            return codeMatcher.InstructionEnumeration();
        }

        [HarmonyPatch(typeof(PlayerControllerB), "LateUpdate")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        private static void FrostbiteLatePostfix(PlayerControllerB __instance)
        {
            //PlayerTemperatureManager.heatTransferRate = 1f; // should be 1 if heatwave patches work correctly

            if (!(SnowfallWeather.Instance?.IsActive ?? false) && Mathf.Approximately(PlayerTemperatureManager.coldSeverity, 0))
            {
                return;
            }

            // Gradually reduce heat severity when not in heat zone
            if (!PlayerTemperatureManager.isInColdZone)
            {
                PlayerTemperatureManager.ResetPlayerTemperature(Time.deltaTime / timeToWarm);
            }

            float severity = PlayerTemperatureManager.coldSeverity;

            Debug.LogDebug($"Severity: {severity}, inColdZone: {PlayerTemperatureManager.isInColdZone}, frostbiteTimer: {frostbiteTimer}");
            if (severity >= frostbiteThreshold)
            {
                frostbiteTimer += Time.deltaTime;
                if (frostbiteTimer > frostbiteDamageInterval)
                {
                    __instance.DamagePlayer(Mathf.CeilToInt(frostbiteDamage*Mathf.Abs(severity)), causeOfDeath: CauseOfDeath.Unknown);
                    frostbiteTimer = 0f;
                }
            }
        }

        [HarmonyPatch(typeof(EnemyAI), "DoAIInterval")]
        [HarmonyPrefix]
        private static void EnemyGroundSamplerPatch(EnemyAI __instance)
        {
            if (GameNetworkManager.Instance.isHostingGame && (SnowfallWeather.Instance?.IsActive ?? false))
            {
                if (Physics.Raycast(__instance.transform.position, -Vector3.up, out __instance.raycastHit, 6f, StartOfRound.Instance.walkableSurfacesMask, QueryTriggerInteraction.Ignore))
                {
                    SnowThicknessManager.Instance?.UpdateEntityData(__instance, __instance.raycastHit);
                }
            }
        }

        [HarmonyPatch(typeof(EnemyAI), "LateUpdate")]
        [HarmonyPrefix]
        private static void EnemySnowHindrancePatch(EnemyAI __instance)
        {
            if (GameNetworkManager.Instance.isHostingGame && (SnowfallWeather.Instance?.IsActive ?? false))
            {
                float snowThickness = SnowThicknessManager.Instance!.GetSnowThickness(__instance);
                // Slow down if the entity in snow (only if snow thickness is above 0.4, caps at 2.5)
                float snowMovementHindranceMultiplier = 1 + 5*Mathf.Clamp01((snowThickness - 0.4f)/2.1f);
                if (agentSpeedCache.TryGetValue(__instance, out (float, float) cache))
                {
                    float supposedAgentSpeed = cache.Item1;
                    float prevSnowHindranceMultiplier = cache.Item2;
                    // Check if the agent speed has changed and if the speed without snow hindrance is different from the cached value
                    // EnemyAI agent speed can be changed elsewhere, so we need to do this check to be able to restore the intended speed
                    if (!Mathf.Approximately(supposedAgentSpeed, __instance.agent.speed) && 
                        !Mathf.Approximately(supposedAgentSpeed, __instance.agent.speed * prevSnowHindranceMultiplier))
                    {
                        cache.Item1 = __instance.agent.speed;
                    }
                    cache.Item2 = snowMovementHindranceMultiplier;
                    __instance.agent.speed = cache.Item1 / snowMovementHindranceMultiplier;
                }
                else
                {
                    // Cache the agent speed and the hindrance multiplier to be able to restore original speed
                    agentSpeedCache[__instance] = (__instance.agent.speed, snowMovementHindranceMultiplier);
                    __instance.agent.speed /= snowMovementHindranceMultiplier;
                }   
            }
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

        // Not required since player scripts are never destroyed
        // [HarmonyPatch(typeof(PlayerControllerB), "OnDestroy")]
        // [HarmonyPostfix]
        // private static void PlayerSnowTracksRemovePatch(PlayerControllerB __instance)
        // {
        //     snowTrackersDict.Remove(__instance);
        // }

        [HarmonyPatch(typeof(EnemyAI), "OnDestroy")]
        [HarmonyPrefix]
        private static void EnemySnowCleanupPatch(EnemyAI __instance)
        {
            snowTrackersDict.Remove(__instance);
            if (GameNetworkManager.Instance.isHostingGame)
            {
                agentSpeedCache.Remove(__instance);
                SnowThicknessManager.Instance?.RemoveEntityData(__instance);
            }
        }

        [HarmonyPatch(typeof(GrabbableObject), "OnDestroy")]
        [HarmonyPrefix]
        private static void GrabbableSnowTracksCleanupPatch(GrabbableObject __instance)
        {
            snowTrackersDict.Remove(__instance);
            if (__instance is Shovel shovel)
            {
                snowShovelDict.Remove(shovel);
            }
        }
        
        [HarmonyPatch(typeof(VehicleController), "OnDestroy")]
        [HarmonyPrefix]
        private static void VehicleSnowTracksCleanupPatch(VehicleController __instance)
        {
            snowTrackersDict.Remove(__instance);
        }
        
        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        [HarmonyPostfix]
        private static void PlayerSnowTracksUpdatePatch(PlayerControllerB __instance)
        {
            bool pauseCondition = (SnowfallWeather.Instance?.IsActive ?? false) &&
                                    !__instance.isInsideFactory &&
                                    __instance.thisController.isGrounded &&
                                    (SnowThicknessManager.Instance?.isEntityOnNaturalGround(__instance) ?? false);
            PauseFootprintTracker(__instance, pauseCondition);
        }

        [HarmonyPatch(typeof(EnemyAI), "Update")]
        [HarmonyPostfix]
        private static void EnemySnowTracksUpdatePatch(EnemyAI __instance)
        {
            bool pauseCondition = (SnowfallWeather.Instance?.IsActive ?? false) &&
                                    (!__instance.isOutside ||
                                    !__instance.agent.isOnNavMesh ||
                                    !(SnowThicknessManager.Instance?.isEntityOnNaturalGround(__instance) ?? false));
            PauseFootprintTracker(__instance, pauseCondition);
        }

        [HarmonyPatch(typeof(GrabbableObject), "Update")]
        [HarmonyPostfix]
        private static void GrabbableSnowTracksUpdatePatch(GrabbableObject __instance)
        {
            bool pauseCondition = (SnowfallWeather.Instance?.IsActive ?? false) && !__instance.isInFactory;
            PauseFootprintTracker(__instance, pauseCondition);
        }

        [HarmonyPatch(typeof(VehicleController), "Update")]
        [HarmonyPostfix]
        private static void VehicleSnowTracksUpdatePatch(VehicleController __instance)
        {
            bool pauseCondition = (SnowfallWeather.Instance?.IsActive ?? false) &&
                                    (__instance.FrontLeftWheel.isGrounded ||
                                    __instance.FrontRightWheel.isGrounded ||
                                    __instance.BackLeftWheel.isGrounded ||
                                    __instance.BackRightWheel.isGrounded);
            PauseFootprintTracker(__instance, pauseCondition);
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
            PlayFootprintTracker(__instance, snowShovelDict, !__instance.isInFactory);
        }

        public static void AddFootprintTracker(MonoBehaviour obj, float particleSize, float lifetimeMultiplier, float footprintStrength)
        {
            //Load different footprints for player and other objects
            GameObject? footprintsTrackerVariant = obj switch
            {
                EnemyAI _ => SnowfallVFXManager.snowTrackersDict?["lowcapFootprintsTrackerVFX"],
                GrabbableObject _ => SnowfallVFXManager.snowTrackersDict?["itemTrackerVFX"],
                _ => SnowfallVFXManager.snowTrackersDict?["footprintsTrackerVFX"] //PlayerControllerB and VehicleController
            };


            GameObject? footprintsTracker = GameObject.Instantiate(footprintsTrackerVariant,
                                                                    position: obj.transform.position,
                                                                    rotation: Quaternion.identity,
                                                                    parent: obj.transform);
            VisualEffect? footprintsTrackerVFX = footprintsTracker?.GetComponent<VisualEffect>();
            footprintsTrackerVFX?.SetFloat("particleSize", particleSize);
            footprintsTrackerVFX?.SetFloat("lifetimeMultiplier", lifetimeMultiplier);
            footprintsTrackerVFX?.SetFloat("footprintStrength", footprintStrength);
            
            if (footprintsTrackerVFX != null)
            {
                snowTrackersDict.Add(obj, footprintsTrackerVFX);
            }

            if (obj is Shovel shovel)
            {
                GameObject? shovelVFXObject = GameObject.Instantiate(SnowfallVFXManager.snowTrackersDict?["shovelVFX"],
                                                            position: obj.transform.position,
                                                            rotation: Quaternion.identity,
                                                            parent: obj.transform);
                VisualEffect? shovelVFX = shovelVFXObject?.GetComponent<VisualEffect>();
                if (shovelVFX != null)
                {
                    snowShovelDict.Add(shovel, shovelVFX);
                }
            }
        }

        private static bool SurfaceSamplingOverride(PlayerControllerB playerScript)
        {
            bool isOnGround = Physics.Raycast(playerScript.interactRay, out playerScript.hit, 6f, StartOfRound.Instance.walkableSurfacesMask, QueryTriggerInteraction.Ignore);
            bool isSameSurface = playerScript.hit.collider.CompareTag(StartOfRound.Instance.footstepSurfaces[playerScript.currentFootstepSurfaceIndex].surfaceTag);
            bool snowOverride = false;

            if (!(SnowfallWeather.Instance?.IsActive ?? false))
            {
                return !isOnGround || isSameSurface;
            }

            if (SnowThicknessManager.Instance != null)
            {
                SnowThicknessManager.Instance.UpdateEntityData(playerScript, playerScript.hit);

                // Override footstep sound if snow is thick enough
                if (SnowfallVFXManager.snowFootstepIndex != -1 &&
                    SnowThicknessManager.Instance.isEntityOnNaturalGround(playerScript) &&
                    SnowfallVFXManager.snowThickness > 0.1f
                    )
                {
                    snowOverride = true;
                    playerScript.currentFootstepSurfaceIndex = SnowfallVFXManager.snowFootstepIndex;
                }
            }

            return !isOnGround || isSameSurface || snowOverride;
        }

        public static void PauseFootprintTracker(MonoBehaviour obj, bool pauseCondition)
        {
            if (snowTrackersDict.TryGetValue(obj, out VisualEffect footprintsTrackerVFX))
            {
                bool trackingNeedsUpdating = footprintsTrackerVFX.GetBool("isTracking") ^ pauseCondition;
                if (trackingNeedsUpdating)
                {
                    footprintsTrackerVFX?.SetBool("isTracking", pauseCondition);
                }
            }
        }

        public static void PlayFootprintTracker(MonoBehaviour obj, Dictionary<MonoBehaviour, VisualEffect> dictForVFX, bool playCondition = false)
        {
            if (dictForVFX.TryGetValue(obj, out VisualEffect footprintsTrackerVFX) && playCondition)
            {
                footprintsTrackerVFX?.Play();
            }
        }
    }

}


