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
        public static Dictionary<MonoBehaviour, VisualEffect> snowTrackersDict = new Dictionary<MonoBehaviour, VisualEffect>();
        public static Dictionary<MonoBehaviour, VisualEffect> snowShovelDict = new Dictionary<MonoBehaviour, VisualEffect>();
        public static Dictionary<EnemyAI, (float, float)> agentSpeedCache = new Dictionary<EnemyAI, (float, float)>();
        private static readonly int isTrackingID = Shader.PropertyToID("isTracking");
        public static float timeToWarm = 20f;   // Time to warm up from cold to room temperature
        internal static float frostbiteTimer = 0f;
        internal static float frostbiteDamageInterval = 20f;
        internal static float frostbiteDamage = 10;
        internal static float frostbiteThreshold = 0.5f; // Severity at which frostbite starts to occur, should be below 0.9
        internal static HashSet<Type> unaffectedEnemyTypes = new HashSet<Type> {typeof(ForestGiantAI), typeof(RadMechAI), typeof(DoublewingAI),
                                                                                typeof(ButlerBeesEnemyAI), typeof(DocileLocustBeesAI), typeof(RedLocustBees),
                                                                                typeof(DressGirlAI)};//, typeof(SandWormAI)}; TODO: Add SandWormAI if it's affected by snow hindrance


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

            if (!(SnowfallWeather.Instance?.IsActive ?? false) && Mathf.Approximately(PlayerTemperatureManager.coldSeverity, 0))
            {
                return;
            }

            // Gradually reduce heat severity when not in heat zone
            if (!PlayerTemperatureManager.isInColdZone)
            {
                PlayerTemperatureManager.ResetPlayerTemperature(Time.deltaTime / timeToWarm);
            }
            else
            {
                PlayerTemperatureManager.SetPlayerTemperature(-Time.deltaTime / SnowfallWeather.Instance!.timeUntilFrostbite);
            }

            float severity = PlayerTemperatureManager.coldSeverity;

            // Debug.LogDebug($"Severity: {severity}, inColdZone: {PlayerTemperatureManager.isInColdZone}, frostbiteTimer: {frostbiteTimer}, heatTransferRate: {PlayerTemperatureManager.heatTransferRate}");
            
            if (severity >= frostbiteThreshold)
            {
                frostbiteTimer += Time.deltaTime;
                if (frostbiteTimer > frostbiteDamageInterval)
                {
                    __instance.DamagePlayer(Mathf.CeilToInt(frostbiteDamage*severity), causeOfDeath: CauseOfDeath.Unknown);
                    frostbiteTimer = 0f;
                }
            }
            else if (frostbiteTimer > 0)
            {
                frostbiteTimer -= Time.deltaTime;
            }
        }

        [HarmonyPatch(typeof(EnemyAI), "DoAIInterval")]
        [HarmonyPrefix]
        private static void EnemyGroundSamplerPatch(EnemyAI __instance)
        {
            if (GameNetworkManager.Instance.isHostingGame && (SnowfallWeather.Instance?.IsActive ?? false) && __instance.isOutside)
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
            AddFootprintTracker(__instance, 2.6f, 1f, 0.25f, new Vector3(0, 0, -1f));
        }

        [HarmonyPatch(typeof(EnemyAI), "Start")]
        [HarmonyPrefix]
        private static void EnemySnowTracksPatch(EnemyAI __instance)
        {
            if (__instance is ForestGiantAI)
            {
                AddFootprintTracker(__instance, 10f, 0.167f, 0.2f);
            }
            else if (__instance is RadMechAI)
            {
                AddFootprintTracker(__instance, 8f, 0.167f, 0.2f);
            }
            else if (__instance is SandWormAI)
            {
                AddFootprintTracker(__instance, 10f, 0.167f, 1f);
            }
            else if (!unaffectedEnemyTypes.Contains(__instance.GetType()))
            {
                AddFootprintTracker(__instance, 2f, 0.167f, 0.2f);
            }
        }

        [HarmonyPatch(typeof(GrabbableObject), "Start")]
        [HarmonyPrefix]
        private static void GrabbableSnowTracksPatch(GrabbableObject __instance)
        {
            AddFootprintTracker(__instance, 2f, 0.167f, 0.7f);
        }
        
        [HarmonyPatch(typeof(VehicleController), "Start")]
        [HarmonyPrefix]
        private static void VehicleSnowTracksPatch(VehicleController __instance)
        {
            AddFootprintTracker(__instance, 6f, 0.75f, 1f, new Vector3(0, 0, 1.5f));
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

        // OnDestroy is not called for GrabbableObjects so we need to use a different method to remove them
        // [HarmonyPatch(typeof(GrabbableObject), "OnDestroy")]
        // [HarmonyPrefix]
        // private static void GrabbableSnowTracksCleanupPatch(GrabbableObject __instance)
        // {
        //     snowTrackersDict.Remove(__instance);
        //     if (__instance is Shovel shovel)
        //     {
        //         snowShovelDict.Remove(shovel);
        //     }
        // }
        
        [HarmonyPatch(typeof(VehicleController), "DestroyCar")]
        [HarmonyPrefix]
        private static void VehicleSnowTracksCleanupPatch(VehicleController __instance)
        {
            snowTrackersDict.Remove(__instance);
        }
        
        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        [HarmonyPrefix]
        private static void PlayerSnowTracksUpdatePatch(PlayerControllerB __instance)
        {
            bool enableTracker = (SnowfallWeather.Instance?.IsActive ?? false) &&
                                    !__instance.isInsideFactory &&
                                    (SnowThicknessManager.Instance?.isEntityOnNaturalGround(__instance) ?? false);
            PauseFootprintTracker(__instance, enableTracker);
        }

        [HarmonyPatch(typeof(EnemyAI), "Update")]
        [HarmonyPrefix]
        private static void EnemySnowTracksUpdatePatch(EnemyAI __instance)
        {
            // __instance.isOutside is a simplified check for clients, may cause incorrect behaviour in some cases
            bool enableTracker = (SnowfallWeather.Instance?.IsActive ?? false) &&
                                    (__instance.isOutside ||
                                    (SnowThicknessManager.Instance?.isEntityOnNaturalGround(__instance) ?? false));
            PauseFootprintTracker(__instance, enableTracker);
        }

        [HarmonyPatch(typeof(GrabbableObject), "Update")]
        [HarmonyPrefix]
        private static void GrabbableSnowTracksUpdatePatch(GrabbableObject __instance)
        {
            bool enableTracker = (SnowfallWeather.Instance?.IsActive ?? false) && !__instance.isInFactory;
            PauseFootprintTracker(__instance, enableTracker);
        }

        [HarmonyPatch(typeof(VehicleController), "Update")]
        [HarmonyPrefix]
        private static void VehicleSnowTracksUpdatePatch(VehicleController __instance)
        {
            // TODO wheels might not be grounded on client, need to check if this is an issue
            bool enableTracker = (SnowfallWeather.Instance?.IsActive ?? false) &&
                                    (__instance.FrontLeftWheel.isGrounded ||
                                    __instance.FrontRightWheel.isGrounded ||
                                    __instance.BackLeftWheel.isGrounded ||
                                    __instance.BackRightWheel.isGrounded);
            PauseFootprintTracker(__instance, enableTracker);
        }

        [HarmonyPatch(typeof(GrabbableObject), "PlayDropSFX")]
        [HarmonyPrefix]
        private static void GrabbableFallSnowPatch(GrabbableObject __instance)
        {
            PlayFootprintTracker(__instance, snowTrackersDict, !__instance.isInFactory);
        }

        [HarmonyPatch(typeof(Shovel), "ReelUpSFXClientRpc")]
        [HarmonyPrefix]
        private static void ShovelSnowPatch(Shovel __instance)
        {
            PlayFootprintTracker(__instance, snowShovelDict, !__instance.isInFactory);
        }

        public static void AddFootprintTracker(MonoBehaviour obj, float particleSize, float lifetimeMultiplier, float footprintStrength, Vector3 localOffset = default)
        {
            //Load different footprints for player and other objects
            VisualEffectAsset? footprintsTrackerVariant = obj switch
            {
                EnemyAI _ => SnowfallVFXManager.snowTrackersDict?["lowcapFootprintsTrackerVFX"],
                GrabbableObject _ => SnowfallVFXManager.snowTrackersDict?["itemTrackerVFX"],
                _ => SnowfallVFXManager.snowTrackersDict?["footprintsTrackerVFX"] //PlayerControllerB and VehicleController
            };

            GameObject trackerObj = new GameObject("FootprintsTracker");
            trackerObj.transform.SetParent(obj.transform);
            trackerObj.transform.localPosition = localOffset; // Offset the tracker for some objects (mainly for player and vehicle)
            trackerObj.transform.localRotation = Quaternion.identity;
            trackerObj.transform.localScale = Vector3.one;
            trackerObj.layer = LayerMask.NameToLayer("Vehicle"); // Must match the culling mask of the FootprintsTrackerCamera in SnowfallWeather
            VisualEffect footprintsTrackerVFX = trackerObj.AddComponent<VisualEffect>();
            footprintsTrackerVFX.visualEffectAsset = footprintsTrackerVariant;

            footprintsTrackerVFX?.SetFloat("particleSize", particleSize);
            footprintsTrackerVFX?.SetFloat("lifetimeMultiplier", lifetimeMultiplier);
            footprintsTrackerVFX?.SetFloat("footprintStrength", footprintStrength);
            
            if (footprintsTrackerVFX != null)
            {
                snowTrackersDict.Add(obj, footprintsTrackerVFX);
            }

            if (obj is Shovel shovel)
            {
                trackerObj = new GameObject("ShovelCleanerVFX"); // Create another tracker for shovel cleaning, since having two VisualEffects on the same object is not supported
                trackerObj.transform.SetParent(obj.transform);
                trackerObj.transform.localPosition = Vector3.zero;
                //rotate around local Y axis by 90 degrees to align with the player's camera
                trackerObj.transform.localRotation = Quaternion.Euler(0, 90, 0);
                trackerObj.transform.localScale = Vector3.one;
                trackerObj.layer = LayerMask.NameToLayer("Vehicle"); // Must match the culling mask of the FootprintsTrackerCamera in SnowfallWeather
            
                VisualEffect shovelVFX = trackerObj.AddComponent<VisualEffect>();
                shovelVFX.visualEffectAsset = SnowfallVFXManager.snowTrackersDict?["shovelVFX"];
                if (shovelVFX != null)
                {
                    snowShovelDict.Add(shovel, shovelVFX);
                }
            }
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

        public static void PauseFootprintTracker(MonoBehaviour obj, bool enableTracker)
        {
            if (snowTrackersDict.TryGetValue(obj, out VisualEffect footprintsTrackerVFX))
            {
                bool trackingNeedsUpdating = footprintsTrackerVFX.GetBool(isTrackingID) ^ enableTracker;
                if (trackingNeedsUpdating)
                {
                    footprintsTrackerVFX.SetBool(isTrackingID, enableTracker);
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

        // Removes null entries from the dictionary
        internal static void CleanupFootprintTrackers(Dictionary<MonoBehaviour, VisualEffect> trackersDict)
        {
            List<MonoBehaviour> keysToRemove = new List<MonoBehaviour>(); // Store keys to remove

            foreach (var key in trackersDict.Keys) 
            {
                if (key == null)
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                trackersDict.Remove(key);
            }
        }

        internal static void ToggleFootprintTrackers(Dictionary<MonoBehaviour, VisualEffect> trackersDict, bool enable)
        {
            foreach (var kvp in trackersDict)
            {
                if (kvp.Key != null)
                {
                    kvp.Value.gameObject.SetActive(enable);
                }
            }
        }

    }

}


