﻿using HarmonyLib;
using VoxxWeatherPlugin.Weathers;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection.Emit;
using GameNetcodeStuff;
using UnityEngine.VFX;
using VoxxWeatherPlugin.Utils;
using VoxxWeatherPlugin.Behaviours;
using System;



namespace VoxxWeatherPlugin.Patches
{
    [HarmonyPatch]
    internal class SnowPatches
    {
        internal static bool allowClientCalculations = false;
        public static Dictionary<EnemyAI, (float, float)> agentSpeedCache = new Dictionary<EnemyAI, (float, float)>();
        public static float TimeToWarmUp => Configuration.timeToWarmUp.Value;   // Time to warm up from cold to room temperature
        internal static float FrostbiteDamageInterval => Configuration.frostbiteDamageInterval.Value;
        internal static float FrostbiteDamage => Configuration.frostbiteDamage.Value;
        internal static float frostbiteThreshold = 0.5f; // Severity at which frostbite starts to occur, should be below 0.9
        internal static float frostbiteTimer = 0f;
        internal static HashSet<Type> unaffectedEnemyTypes = new HashSet<Type> {typeof(ForestGiantAI), typeof(RadMechAI), typeof(DoublewingAI),
                                                                                typeof(ButlerBeesEnemyAI), typeof(DocileLocustBeesAI), typeof(RedLocustBees),
                                                                                typeof(DressGirlAI)};//, typeof(SandWormAI)}; TODO: Add SandWormAI if it's affected by snow hindrance

        

        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        [HarmonyTranspiler]
        [HarmonyPriority(Priority.VeryHigh)]
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
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(StartOfRound), "walkableSurfacesMask"))
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
            if (!(SnowfallWeather.Instance?.IsActive ?? false) || !__instance.IsOwner || !__instance.isPlayerControlled)
                return;

            // Gradually reduce heat severity when not in heat zone
            if (!PlayerTemperatureManager.isInColdZone)
            {
                PlayerTemperatureManager.ResetPlayerTemperature(Time.deltaTime / TimeToWarmUp);
            }
            else
            {
                PlayerTemperatureManager.SetPlayerTemperature(-Time.deltaTime / SnowfallWeather.Instance!.timeUntilFrostbite);
            }

            float severity = PlayerTemperatureManager.ColdSeverity;

            // Debug.LogDebug($"Severity: {severity}, inColdZone: {PlayerTemperatureManager.isInColdZone}, frostbiteTimer: {frostbiteTimer}, heatTransferRate: {PlayerTemperatureManager.heatTransferRate}");
            
            if (severity >= frostbiteThreshold)
            {
                frostbiteTimer += Time.deltaTime;
                int damage = Mathf.CeilToInt(FrostbiteDamage*severity);
                if (frostbiteTimer > FrostbiteDamageInterval && damage > 0)
                {
                    __instance.DamagePlayer(damage, causeOfDeath: CauseOfDeath.Unknown);
                    frostbiteTimer = 0f;
                }
            }
            else if (frostbiteTimer > 0)
            {
                frostbiteTimer -= Time.deltaTime;
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), "OnControllerColliderHit")]
        [HarmonyPostfix]
        private static void PlayerFeetPositionPatch(ControllerColliderHit hit)
        {
            if (SnowfallWeather.Instance?.IsActive ?? false && SnowThicknessManager.Instance != null)
            {
                SnowThicknessManager.Instance!.feetPositionY = hit.point.y;
            }
        }

        [HarmonyPatch(typeof(EnemyAI), "DoAIInterval")]
        [HarmonyPrefix]
        private static void EnemyGroundSamplerPatch(EnemyAI __instance)
        {
            if ((GameNetworkManager.Instance.isHostingGame || allowClientCalculations) && (SnowfallWeather.Instance?.IsActive ?? false) && __instance.isOutside)
            {
                // Check if enemy is affected by snow hindrance
                if (!unaffectedEnemyTypes.Contains(__instance.GetType()))
                {
                    if (Physics.Raycast(__instance.transform.position, -Vector3.up, out __instance.raycastHit, 6f, StartOfRound.Instance.walkableSurfacesMask, QueryTriggerInteraction.Ignore))
                    {
                        SnowThicknessManager.Instance?.UpdateEntityData(__instance, __instance.raycastHit);
                    }
                }
            }
        }

        //Generic patch for all enemies, we patch manually since each derived enemy type overrides the base implementation
        private static void EnemySnowHindrancePatch(EnemyAI __instance)
        {
            if (GameNetworkManager.Instance.isHostingGame && (SnowfallWeather.Instance?.IsActive ?? false) && __instance.isOutside)
            {
                // float snowThickness = SnowThicknessManager.Instance!.GetSnowThickness(__instance);
                // // Slow down if the entity in snow (only if snow thickness is above 0.4, caps at 2.5 height)
                // float snowMovementHindranceMultiplier = 1 + 5*Mathf.Clamp01((snowThickness - 0.4f)/2.1f);
                // if (agentSpeedCache.TryGetValue(__instance, out (float, float) cache))
                // {
                //     (float supposedAgentSpeed, float prevSnowHindranceMultiplier) = cache;
                //     // Check if the agent speed has changed and if the speed without snow hindrance is different from the cached value
                //     // EnemyAI agent speed can be changed elsewhere, so we need to do this check to be able to restore the intended speed
                //     if (!Mathf.Approximately(supposedAgentSpeed, __instance.agent.speed) && 
                //         !Mathf.Approximately(supposedAgentSpeed, __instance.agent.speed * prevSnowHindranceMultiplier))
                //     {
                //         supposedAgentSpeed = __instance.agent.speed;
                //     }
                //     __instance.agent.speed = supposedAgentSpeed / snowMovementHindranceMultiplier;
                //     agentSpeedCache[__instance] = (supposedAgentSpeed, snowMovementHindranceMultiplier);
                // }
                // else
                // {
                //     // Cache the agent speed and the hindrance multiplier to be able to restore original speed
                //     agentSpeedCache[__instance] = (__instance.agent.speed, snowMovementHindranceMultiplier);
                //     __instance.agent.speed /= snowMovementHindranceMultiplier;
                // }

                float snowThickness = SnowThicknessManager.Instance!.GetSnowThickness(__instance);
                // Slow down if the entity in snow (only if snow thickness is above 0.4, caps at 2.5 height)
                float snowMovementHindranceMultiplier = 1 + 5*Mathf.Clamp01((snowThickness - 0.4f)/2.1f);

                __instance.agent.speed /= snowMovementHindranceMultiplier;
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), "Start")]
        [HarmonyPrefix]
        private static void PlayerSnowTracksPatch(PlayerControllerB __instance)
        {
            SnowTrackersManager.AddFootprintTracker(__instance, 2.6f, 1f, 0.25f);
        }

        [HarmonyPatch(typeof(EnemyAI), "Start")]
        [HarmonyPrefix]
        private static void EnemySnowTracksPatch(EnemyAI __instance)
        {
            if (__instance is ForestGiantAI)
            {
                SnowTrackersManager.AddFootprintTracker(__instance, 10f, 0.167f, 0.2f);
            }
            else if (__instance is RadMechAI)
            {
                SnowTrackersManager.AddFootprintTracker(__instance, 8f, 0.167f, 0.2f);
            }
            else if (__instance is SandWormAI)
            {
                SnowTrackersManager.AddFootprintTracker(__instance, 10f, 0.167f, 1f);
            }
            else if (!unaffectedEnemyTypes.Contains(__instance.GetType()))
            {
                SnowTrackersManager.AddFootprintTracker(__instance, 2f, 0.167f, 0.2f);
            }
        }

        [HarmonyPatch(typeof(GrabbableObject), "Start")]
        [HarmonyPrefix]
        private static void GrabbableSnowTracksPatch(GrabbableObject __instance)
        {
            SnowTrackersManager.AddFootprintTracker(__instance, 2f, 0.167f, 0.7f);
        }
        
        [HarmonyPatch(typeof(VehicleController), "Start")]
        [HarmonyPrefix]
        private static void VehicleSnowTracksPatch(VehicleController __instance)
        {
            SnowTrackersManager.AddFootprintTracker(__instance, 6f, 0.75f, 1f);
        }
        
        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        [HarmonyPrefix]
        private static void PlayerSnowTracksUpdatePatch(PlayerControllerB __instance)
        {
            bool enableTracker = (SnowfallWeather.Instance?.IsActive ?? false) &&
                                    !__instance.isInsideFactory &&
                                    (SnowThicknessManager.Instance?.isEntityOnNaturalGround(__instance) ?? false);
            // We need this check to prevent updating tracker's position after player death, as players get moved out of bounds on their death, causing VFX to be culled
            if (!__instance.isPlayerDead) 
            {
                SnowTrackersManager.UpdateFootprintTracker(__instance, enableTracker, new Vector3(0, 0, -1f));
            }
            
        }

        [HarmonyPatch(typeof(EnemyAI), "Update")]
        [HarmonyPrefix]
        private static void EnemySnowTracksUpdatePatch(EnemyAI __instance)
        {
            // __instance.isOutside is a simplified check for clients, may cause incorrect behaviour in some cases
            bool enableTracker = (SnowfallWeather.Instance?.IsActive ?? false) &&
                                    (__instance.isOutside ||
                                    (SnowThicknessManager.Instance?.isEntityOnNaturalGround(__instance) ?? false));
            if (__instance is SandWormAI worm)
            {
                enableTracker &= worm.emerged || worm.inEmergingState;
            }
            SnowTrackersManager.UpdateFootprintTracker(__instance, enableTracker);
        }

        [HarmonyPatch(typeof(GrabbableObject), "Update")]
        [HarmonyPrefix]
        private static void GrabbableSnowTracksUpdatePatch(GrabbableObject __instance)
        {
            bool enableTracker = (SnowfallWeather.Instance?.IsActive ?? false) && !__instance.isInFactory;
            SnowTrackersManager.UpdateFootprintTracker(__instance, enableTracker);
        }

        [HarmonyPatch(typeof(VehicleController), "Update")]
        [HarmonyPrefix]
        private static void VehicleSnowTracksUpdatePatch(VehicleController __instance)
        {
            bool enableTracker = (SnowfallWeather.Instance?.IsActive ?? false) &&
                                    (__instance.FrontLeftWheel.isGrounded ||
                                    __instance.FrontRightWheel.isGrounded ||
                                    __instance.BackLeftWheel.isGrounded ||
                                    __instance.BackRightWheel.isGrounded);
            SnowTrackersManager.UpdateFootprintTracker(__instance, enableTracker, new Vector3(0, 0, 1.5f));
        }

        [HarmonyPatch(typeof(GrabbableObject), "PlayDropSFX")]
        [HarmonyPrefix]
        private static void GrabbableFallSnowPatch(GrabbableObject __instance)
        {
            SnowTrackersManager.PlayFootprintTracker(__instance, TrackerType.Item, !__instance.isInFactory);
        }

        [HarmonyPatch(typeof(Shovel), "ReelUpSFXClientRpc")]
        [HarmonyPrefix]
        private static void ShovelSnowPatch(Shovel __instance)
        {
            SnowTrackersManager.PlayFootprintTracker(__instance, TrackerType.Shovel, !__instance.isInFactory);
        }

        private static bool SurfaceSamplingOverride(PlayerControllerB playerScript)
        {
            bool isOnGround = Physics.Raycast(playerScript.interactRay, out playerScript.hit, 6f, StartOfRound.Instance.walkableSurfacesMask, QueryTriggerInteraction.Ignore);
            bool isSameSurface = isOnGround ? playerScript.hit.collider.CompareTag(StartOfRound.Instance.footstepSurfaces[playerScript.currentFootstepSurfaceIndex].surfaceTag) : true;
            bool snowOverride = false;

            if (!(SnowfallWeather.Instance?.IsActive ?? false))
            {
                return !isOnGround || isSameSurface;
            }

            if (SnowThicknessManager.Instance != null && isOnGround)
            {
                SnowThicknessManager.Instance.UpdateEntityData(playerScript, playerScript.hit);

                // Override footstep sound if snow is thick enough
                if (SnowfallVFXManager.snowFootstepIndex != -1 &&
                    SnowThicknessManager.Instance.isEntityOnNaturalGround(playerScript) &&
                     SnowThicknessManager.Instance.GetSnowThickness(playerScript) > 0.1f // offset is not applied here for nonlocal player so they would produce normal footstep sounds
                    )
                {
                    snowOverride = true;
                    playerScript.currentFootstepSurfaceIndex = SnowfallVFXManager.snowFootstepIndex;
                }
            }

            return !isOnGround || isSameSurface || snowOverride;
        }
        
    }

}


