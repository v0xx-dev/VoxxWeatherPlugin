using HarmonyLib;
using VoxxWeatherPlugin.Weathers;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine.AI;
using VoxxWeatherPlugin.Behaviours;
using GameNetcodeStuff;
using VoxxWeatherPlugin.Utils;

namespace VoxxWeatherPlugin.Patches
{
    [HarmonyPatch]
    internal class FlarePatches
    {
        internal static System.Random random = new System.Random();
        internal static System.Random seededRandom = new System.Random(42);
        internal static Transform originalTeleporterPosition;
        internal static float batteryDrainMultiplier => Mathf.Clamp(VoxxWeatherPlugin.BatteryDrainMultiplier.Value, 0, 99);
        internal static bool drainBatteryInFacility => VoxxWeatherPlugin.DrainBatteryInFacility.Value;
        internal static bool doorMalfunctionEnabled => VoxxWeatherPlugin.DoorMalfunctionEnabled.Value;


        [HarmonyPatch(typeof(PlayerVoiceIngameSettings), "OnDisable")]
        [HarmonyPrefix]
        private static void FilterCacheCleanerPatch(PlayerVoiceIngameSettings __instance)
        {
           if (__instance.voiceAudio != null)
           {
               WalkieDistortionManager.ClearFilterCache(__instance.voiceAudio);
           }
        }

        [HarmonyPatch(typeof(StartOfRound), "UpdatePlayerVoiceEffects")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> VoiceDistorterPatch(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);
            codeMatcher.MatchForward(false,
                new CodeMatch(OpCodes.Stloc_S),
                new CodeMatch(OpCodes.Ldloc_1),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerControllerB), "isPlayerDead")),
                new CodeMatch(OpCodes.Brfalse)
            );

            if (codeMatcher.IsValid)
            {
                codeMatcher.Advance(1).Insert(
                    new CodeInstruction(OpCodes.Ldloc_0),  // Load voiceChatAudioSource
                    new CodeInstruction(OpCodes.Ldloc_1),  // Load allPlayerScript
                    new CodeInstruction(OpCodes.Ldloc_S, 4),  // Load walkie talkie flag 
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(WalkieDistortionManager), "UpdateVoiceChatDistortion"))
                );
            }
            
            return codeMatcher.InstructionEnumeration();
        }

        [HarmonyPatch(typeof(GrabbableObject), "Update")]
        [HarmonyPrefix]
        private static void GrabbableDischargePatch(GrabbableObject __instance)
        {
            if (SolarFlareWeather.flareData != null)
            {
                if (__instance.IsOwner && __instance.hasBeenHeld && __instance.itemProperties.requiresBattery && (!__instance.isInFactory || drainBatteryInFacility))
                {
                    if (__instance.insertedBattery.charge > 0.0 && !__instance.itemProperties.itemIsTrigger)
                    {
                        __instance.insertedBattery.charge -= 2 * SolarFlareWeather.flareData.ScreenDistortionIntensity * batteryDrainMultiplier * Time.deltaTime / __instance.itemProperties.batteryUsage;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(HUDManager), "UseSignalTranslatorClientRpc")]
        [HarmonyPrefix]
        private static void SignalTranslatorDistortionPatch(ref string signalMessage)
        {
            if (SolarFlareWeather.flareData != null)
            {
                float distortionIntensity = SolarFlareWeather.flareData.RadioDistortionIntensity * 0.5f;
                char[] messageChars = signalMessage.ToCharArray();

                for (int i = 0; i < messageChars.Length; i++)
                {
                    if (random.NextDouble() < distortionIntensity)
                    {
                        messageChars[i] = (char)random.Next(32, 127); // Random ASCII printable character
                    }
                }

                signalMessage = new string(messageChars);
            }
        }

        [HarmonyPatch(typeof(ShipTeleporter), "PressTeleportButtonClientRpc")]
        [HarmonyPrefix]
        public static void TeleporterDistortionPrefix(ShipTeleporter __instance)
        {
            if (SolarFlareWeather.flareData != null)
            {
                // Store the original teleporter position
                originalTeleporterPosition = __instance.teleporterPosition;

                // Randomly teleport to an AI node >:D
                GameObject[] outsideAINodes = RoundManager.Instance.outsideAINodes;
                if (outsideAINodes.Length > 0)
                {
                    int randomIndex = seededRandom.Next(0, outsideAINodes.Length);
                    Transform distortedPosition = outsideAINodes[randomIndex].transform;
                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(distortedPosition.position, out hit, 10f, NavMesh.AllAreas))
                    {
                        distortedPosition.position = hit.position;
                        __instance.teleporterPosition = distortedPosition;
                    }
                }
            } 
        }

        [HarmonyPatch(typeof(ShipTeleporter), "PressTeleportButtonClientRpc")]
        [HarmonyPostfix]
        public static void TeleporterDistortionPostfix(ShipTeleporter __instance)
        {
            if (SolarFlareWeather.flareData != null)
            {
                // Restore the original teleporter position
                __instance.teleporterPosition = originalTeleporterPosition;
            }
        }

        [HarmonyPatch(typeof(TerminalAccessibleObject), "CallFunctionFromTerminal")]
        [HarmonyPrefix]
        public static bool DoorTerminalBlocker(TerminalAccessibleObject __instance)
        {
            if (SolarFlareWeather.flareData != null && doorMalfunctionEnabled)
            {
                if (SolarFlareWeather.flareData.IsDoorMalfunction && __instance.isBigDoor && seededRandom.NextDouble() < 0.9f)
                {
                    return false;
                }
            }
            return true;
        }

        [HarmonyPatch(typeof(RadarBoosterItem), "EnableRadarBooster")]
        [HarmonyPrefix]
        public static void SignalBoosterPrefix(RadarBoosterItem __instance, ref bool enable)
        {
            if (SolarFlareWeather.flareData != null)
            {
                if (enable)
                {
                    // Decrease the distortion intensity
                    SolarFlareWeather.flareData.RadioDistortionIntensity /= 3f;
                    SolarFlareWeather.flareData.ScreenDistortionIntensity /= 3f;
                    SolarFlareWeather.flareData.RadioFrequencyShift /= 4f;
                    SolarFlareWeather.flareData.RadioBreakthroughLength += 0.25f;
                }
                else if (__instance.radarEnabled)
                {
                    // Restore the original values
                    SolarFlareWeather.flareData.RadioDistortionIntensity *= 3f;
                    SolarFlareWeather.flareData.ScreenDistortionIntensity *= 3f;
                    SolarFlareWeather.flareData.RadioFrequencyShift *= 4f;
                    SolarFlareWeather.flareData.RadioBreakthroughLength -= 0.25f;
                }
            }
        }
    }

    [HarmonyPatch]
    internal class FlareOptionalWalkiePatches
    {
        [HarmonyPatch(typeof(WalkieTalkie), "Start")]
        [HarmonyPrefix]
        private static void WalkieDistortionPatch(WalkieTalkie __instance)
        {
           __instance.gameObject.AddComponent<WalkieDistortionManager>();
        }

        [HarmonyPatch(typeof(WalkieTalkie), "TimeAllAudioSources")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> RadioDistorterPatch(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);

            // Replace audio source creation logic
            codeMatcher = codeMatcher.MatchForward(true,
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(WalkieTalkie), "audioSourcesReceiving")),
                new CodeMatch(OpCodes.Ldloc_3),
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(WalkieTalkie), "target")),
                new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(Component), "get_gameObject"))
            );

            codeMatcher.Advance(1);

            codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FlareOptionalWalkiePatches), "SplitWalkieTarget")));

            // Replace audio source disposal logic
            codeMatcher = codeMatcher.MatchForward(true,
                new CodeMatch(OpCodes.Ldloc_1),
                new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Object), "Destroy", new[] { typeof(UnityEngine.Object) }))
            )
            .Repeat(matcher => {
                matcher.SetInstruction(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FlareOptionalWalkiePatches), "DisposeWalkieTarget",
                     new[] { typeof(AudioSource), typeof(GameObject) })));
                matcher.Insert(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Component), "get_gameObject")));
                matcher.Insert(new CodeInstruction(OpCodes.Ldarg_0));
                ;
             });

            return codeMatcher.InstructionEnumeration();
        }

        internal static AudioSource SplitWalkieTarget(GameObject target)
        {
            WalkieDistortionManager subTargetsManager = target.transform.parent.gameObject.GetComponent<WalkieDistortionManager>();
            return subTargetsManager.SplitWalkieTarget(target);
        }

        internal static void DisposeWalkieTarget(AudioSource audioSource, GameObject walkieObject)
        {
            WalkieDistortionManager subTargetsManager = walkieObject.GetComponent<WalkieDistortionManager>();
            subTargetsManager.DisposeWalkieTarget(audioSource);
        }
    }

}


