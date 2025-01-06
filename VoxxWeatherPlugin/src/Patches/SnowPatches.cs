using HarmonyLib;
using VoxxWeatherPlugin.Weathers;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection.Emit;
using GameNetcodeStuff;
using VoxxWeatherPlugin.Utils;
using VoxxWeatherPlugin.Behaviours;
using System;
using System.Linq;
using WeatherRegistry;



namespace VoxxWeatherPlugin.Patches
{
    [HarmonyPatch]
    internal class SnowPatches
    {
        internal static bool SnowAffectsEnemies => Configuration.snowAffectsEnemies.Value;
        public static float TimeToWarmUp => Configuration.timeToWarmUp.Value;   // Time to warm up from cold to room temperature
        internal static float FrostbiteDamageInterval => Configuration.frostbiteDamageInterval.Value;
        internal static float FrostbiteDamage => Configuration.frostbiteDamage.Value;
        internal static float frostbiteThreshold = 0.5f; // Severity at which frostbite starts to occur, should be below 0.9
        internal static float frostbiteTimer = 0f;
        internal static HashSet<Type> unaffectedEnemyTypes = new HashSet<Type> {typeof(ForestGiantAI), typeof(RadMechAI), typeof(DoublewingAI),
                                                                                typeof(ButlerBeesEnemyAI), typeof(DocileLocustBeesAI), typeof(RedLocustBees),
                                                                                typeof(DressGirlAI), typeof(SandWormAI)};
        public static HashSet<string>? EnemySpawnBlacklist => LevelManipulator.Instance?.enemySnowBlacklist;
        public static HashSet<SpawnableEnemyWithRarity> enemiesToRestore = [];


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

        [HarmonyPatch(typeof(PlayerControllerB), "CalculateGroundNormal")]
        [HarmonyTranspiler]
        [HarmonyDebug]
        private static IEnumerable<CodeInstruction> GroundNormalTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);
            codeMatcher.MatchForward(true,
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldflda, AccessTools.Field(typeof(PlayerControllerB), "hit")),
                new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(RaycastHit), "get_normal")),
                new CodeMatch(OpCodes.Stfld, AccessTools.Field(typeof(PlayerControllerB), "playerGroundNormal"))
            );

            if (!codeMatcher.IsValid)
            {
                Debug.LogError("Failed to match code in GroundNormalTranspiler");
                return instructions;
            }
            codeMatcher.Advance(1);
            codeMatcher.Insert(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldloc_1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SnowPatches), nameof(LocalGroundUpdate)))
            );

            Debug.Log("Patched PlayerControllerB.CalculateGroundNormal to include snow thickness!");
            return codeMatcher.InstructionEnumeration();
        }

        [HarmonyPatch(typeof(RoundManager), "SpawnOutsideHazards")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> IceRebakeTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count - 4; i++)
            {
                if (codes[i].opcode == OpCodes.Ldloc_S &&
                    codes[i + 1].opcode == OpCodes.Ldc_I4_0 &&
                    codes[i + 2].opcode == OpCodes.Ble &&
                    codes[i + 3].opcode == OpCodes.Ldstr)
                {
                    // Get the original target of the fail jump
                    Label originalTarget = (Label)codes[i + 2].operand;

                    // Define a new label for the fail jump target
                    Label failJumpTarget = generator.DefineLabel();
                    // Insert an additional condition
                    codes.InsertRange(i,
                    [
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SnowPatches), nameof(DelayRebakeForIce))),
                        new CodeInstruction(OpCodes.Brfalse, failJumpTarget)
                    ]);

                    // Find the original jump target and add the new label
                    for (int j = i + 4; j < codes.Count; j++)
                    {
                        if (codes[j].labels.Contains(originalTarget))
                        {
                            codes[j].labels.Add(failJumpTarget);
                            break;
                        }
                    }

                    Debug.Log("Patched RoundManager.SpawnOutsideHazards to include ice rebake condition!");
                    break;
                }
            }
            return codes;
        }

        [HarmonyPatch(typeof(PlayerControllerB), "LateUpdate")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        private static void FrostbiteLatePostfix(PlayerControllerB __instance)
        {
            if (!IsSnowActive() || __instance != GameNetworkManager.Instance?.localPlayerController)
                return;

            // Gradually reduce heat severity when not in heat zone
            if (!PlayerEffectsManager.isInColdZone)
            {
                PlayerEffectsManager.ResetPlayerTemperature(Time.deltaTime / TimeToWarmUp);
            }
            else
            {
                PlayerEffectsManager.SetPlayerTemperature(-Time.deltaTime / SnowfallWeather.Instance!.timeUntilFrostbite);
            }

            if (PlayerEffectsManager.isUnderSnow)
            {
                PlayerEffectsManager.SetUnderSnowEffect(Time.deltaTime);
            }
            else
            {
                PlayerEffectsManager.SetUnderSnowEffect(-Time.deltaTime);
            }

            float severity = PlayerEffectsManager.ColdSeverity;

            // Debug.LogDebug($"Severity: {severity}, inColdZone: {PlayerEffectsManager.isInColdZone}, frostbiteTimer: {frostbiteTimer}, heatTransferRate: {PlayerEffectsManager.heatTransferRate}");
            
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
            if (IsSnowActive() && SnowThicknessManager.Instance != null)
            {
                SnowThicknessManager.Instance!.feetPositionY = hit.point.y;
            }
        }

        [HarmonyPatch(typeof(EnemyAI), "DoAIInterval")]
        [HarmonyPrefix]
        private static void EnemyGroundSamplerPatch(EnemyAI __instance)
        {
            if (SnowAffectsEnemies &&
                GameNetworkManager.Instance.isHostingGame &&
                IsSnowActive() &&
                __instance.isOutside)
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
            if (SnowAffectsEnemies &&
                GameNetworkManager.Instance.isHostingGame &&
                IsSnowActive() && 
                __instance.isOutside)
            {
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

        //TODO MaskedPlayerEnemy doesn't work for some reason
        [HarmonyPatch(typeof(EnemyAI), "Start")]
        [HarmonyPrefix]
        private static void EnemySnowTracksPatch(EnemyAI __instance)
        {
            switch (__instance)
            {
                case ForestGiantAI:
                    SnowTrackersManager.AddFootprintTracker(__instance, 10f, 0.167f, 0.35f);
                    break;
                case RadMechAI:
                    SnowTrackersManager.AddFootprintTracker(__instance, 8f, 0.167f, 0.35f);
                    break;
                case SandWormAI:
                    SnowTrackersManager.AddFootprintTracker(__instance, 25f, 0.167f, 1f);
                    break;
                default:
                    if (!unaffectedEnemyTypes.Contains(__instance.GetType()))
                    {
                        SnowTrackersManager.AddFootprintTracker(__instance, 2f, 0.167f, 0.35f);
                    }
                    break;
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
            bool enableTracker = IsSnowActive() &&
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
            bool enableTracker = IsSnowActive() &&
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
            bool enableTracker = IsSnowActive() && !__instance.isInFactory;
            SnowTrackersManager.UpdateFootprintTracker(__instance, enableTracker);
        }

        [HarmonyPatch(typeof(VehicleController), "Update")]
        [HarmonyPrefix]
        private static void VehicleSnowTracksUpdatePatch(VehicleController __instance)
        {
            bool enableTracker = IsSnowActive() &&
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

        //TODO BUSTED
        [HarmonyPatch(typeof(StartOfRound), "StartGame")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void RemoveEnemiesSnowPatch(StartOfRound __instance)
        {
            // Some enemies might not have been restored due to going to menu
            if (enemiesToRestore.Count > 0)
            {
                RestoreEnemies();
            }

            if (!__instance.IsHost || !IsSnowActive())
            {
                return;
            }

            foreach (SpawnableEnemyWithRarity enemy in RoundManager.Instance.currentLevel.DaytimeEnemies)
            {
                if (EnemySpawnBlacklist!.Contains(enemy.enemyType.enemyName.ToLower()))
                {
                    if (!enemy.enemyType.spawningDisabled)
                    {
                        Debug.LogDebug($"Removing {enemy.enemyType.enemyName} due to cold conditions.");
                        enemiesToRestore.Add(enemy);
                        enemy.enemyType.spawningDisabled = true;
                    }
                }
            }

            foreach (SpawnableEnemyWithRarity enemy in RoundManager.Instance.currentLevel.OutsideEnemies)
            {
                if (EnemySpawnBlacklist!.Contains(enemy.enemyType.enemyName.ToLower()))
                {
                    if (!enemy.enemyType.spawningDisabled)
                    {
                        Debug.LogDebug($"Removing {enemy.enemyType.enemyName} due to cold conditions.");
                        enemiesToRestore.Add(enemy);
                        enemy.enemyType.spawningDisabled = true;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(StartOfRound), "EndOfGame")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void RestoreBeesSnowPatch(StartOfRound __instance)
        {
            if (!__instance.IsHost || !IsSnowActive() || enemiesToRestore.Count == 0)
                return;
            
            RestoreEnemies();
        }

        private static void RestoreEnemies()
        {
            foreach (SpawnableEnemyWithRarity enemy in enemiesToRestore)
            {
                enemy.enemyType.spawningDisabled = false;
                Debug.LogDebug($"Restoring {enemy.enemyType.enemyName} after cold.");
            }

            enemiesToRestore.Clear();
        }

        private static bool SurfaceSamplingOverride(PlayerControllerB playerScript)
        {
            bool isOnGround = Physics.Raycast(playerScript.interactRay, out playerScript.hit, 6f, StartOfRound.Instance.walkableSurfacesMask, QueryTriggerInteraction.Ignore);
            bool isSameSurface = isOnGround ? playerScript.hit.collider.CompareTag(StartOfRound.Instance.footstepSurfaces[playerScript.currentFootstepSurfaceIndex].surfaceTag) : true;
            bool snowOverride = false;

            if (!IsSnowActive())
            {
                return !isOnGround || isSameSurface;
            }

            if (SnowThicknessManager.Instance != null && isOnGround)
            {
                // TODO for the local player update data in PlayerControllerB.CalculateGroundNormal
                SnowThicknessManager.Instance.UpdateEntityData(playerScript, playerScript.hit);

                // Override footstep sound if snow is thick enough
                if (SnowfallVFXManager.snowFootstepIndex != -1 &&
                    SnowThicknessManager.Instance.isEntityOnNaturalGround(playerScript) &&
                     SnowThicknessManager.Instance.GetSnowThickness(playerScript) > 0.1f // offset is not applied here for nonlocal player so they would produce normal footstep sounds at edge cases
                    )
                {
                    snowOverride = true;
                    playerScript.currentFootstepSurfaceIndex = SnowfallVFXManager.snowFootstepIndex;
                }
            }

            return !isOnGround || isSameSurface || snowOverride;
        }

        private static void LocalGroundUpdate(PlayerControllerB playerScript, int index)
        {
            if (IsSnowActive() && index == 0)
            {
                SnowThicknessManager.Instance?.UpdateEntityData(playerScript, playerScript.hit);
            }
        }

        // Patch for ice rebake condition
        // true if we should NOT delay rebaking navmesh for ice
        public static bool DelayRebakeForIce()
        {   
            bool delayRebake = IsSnowActive() &&
                                Configuration.freezeWater.Value;
            Debug.LogDebug($"Should we delay NavMesh rebaking for ice: {delayRebake}");
            return !delayRebake;
        }

        // TODO Check if this is working
        public static bool IsSnowActive()
        {
            return (SnowfallWeather.Instance?.IsActive ?? false) || (BlizzardWeather.Instance?.IsActive ?? false);
        }

        // public static void DebugSnowCheck()
        // {
        //     bool snowActive = SnowfallWeather.Instance?.gameObject.activeInHierarchy ?? false;
        //     bool blizzardActive = BlizzardWeather.Instance?.gameObject.activeInHierarchy ?? false;

        //     bool snowNameMatch = SnowfallWeather.Instance?.WeatherName.ToLower() == WeatherManager.GetCurrentLevelWeather().Name.ToLower();
        //     bool blizzardNameMatch = BlizzardWeather.Instance?.WeatherName.ToLower() == WeatherManager.GetCurrentLevelWeather().Name.ToLower();

        //     bool isLanding = !(StartOfRound.Instance?.inShipPhase ?? false);

        //     Debug.LogDebug($"SnowActive: {snowActive}, BlizzardActive: {blizzardActive}, SnowNameMatch: {snowNameMatch}, BlizzardNameMatch: {blizzardNameMatch}, InOrbit: {isLanding}");
        //     //Inspect names of the current weather and the weather in the level
        //     Debug.LogDebug($"Current weather: '{WeatherManager.GetCurrentLevelWeather().Name}', SnowfallWeather: '{SnowfallWeather.Instance?.WeatherName}', BlizzardWeather: '{BlizzardWeather.Instance?.WeatherName}'");
        // }
        
    }

}


