using HarmonyLib;
using VoxxWeatherPlugin.Utils;
using VoxxWeatherPlugin.Weathers;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection.Emit;
using System;
using System.Linq;
using System.Reflection;
using static UnityEngine.GraphicsBuffer;

namespace VoxxWeatherPlugin.Patches
{
    [HarmonyPatch]
    internal class FlarePatches
    {
        internal static System.Random random = new System.Random();
        internal static System.Random seededRandom = new System.Random(42);
        internal static Transform originalTeleporterPosition;

        [HarmonyPatch(typeof(WalkieTalkie), "Start")]
        [HarmonyPrefix]
        private static void WalkieDistortionPatch(WalkieTalkie __instance)
        {
           __instance.gameObject.AddComponent<WalkieTargetsManager>();
        }

        [HarmonyPatch(typeof(WalkieTalkie), "TimeAllAudioSources")]
        [HarmonyTranspiler]
        [HarmonyDebug]
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

            codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FlarePatches), "SplitWalkieTarget")));

            // Replace audio source disposal logic
            codeMatcher = codeMatcher.MatchForward(true,
                new CodeMatch(OpCodes.Ldloc_1),
                new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Object), "Destroy", new[] { typeof(UnityEngine.Object) }))
            )
            .Repeat(matcher => {
                matcher.SetInstruction(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FlarePatches), "DisposeWalkieTarget",
                     new[] { typeof(AudioSource), typeof(GameObject) })));
                matcher.Insert(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Component), "get_gameObject")));
                matcher.Insert(new CodeInstruction(OpCodes.Ldarg_0));
                ;
             });

            return codeMatcher.InstructionEnumeration();
        }

        internal static AudioSource SplitWalkieTarget(GameObject target)
        {
            WalkieTargetsManager subTargetsManager = target.transform.parent.gameObject.GetComponent<WalkieTargetsManager>();
            return subTargetsManager.SplitWalkieTarget(target);
        }

        internal static void DisposeWalkieTarget(AudioSource audioSource, GameObject walkieObject)
        {
            WalkieTargetsManager subTargetsManager = walkieObject.GetComponent<WalkieTargetsManager>();
            subTargetsManager.DisposeWalkieTarget(audioSource);
        }


        [HarmonyPatch(typeof(HUDManager), "UseSignalTranslatorClientRpc")]
        [HarmonyPrefix]
        private static void SignalTranslatorDistortionPatch(ref string signalMessage)
        {
            if (SolarFlareWeather.flareData != null)
            {
                float distortionIntensity = SolarFlareWeather.flareData.RadioDistortionIntensity * 0.5f;
                char[] messageChars = signalMessage.Substring(0, Mathf.Min(signalMessage.Length, 10)).ToCharArray();

                for (int i = 0; i < messageChars.Length; i++)
                {
                    if (random.NextDouble() < distortionIntensity)
                    {
                        messageChars[i] = (char)random.Next(32, 127); // Random ASCII printable character
                    }
                }

                signalMessage = new string(messageChars);
            }
            Debug.Log($"Corrupted message: {signalMessage} ");
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
                    distortedPosition.position += 2 * Vector3.up;
                    __instance.teleporterPosition = distortedPosition;
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
        public static void DoorTerminalBlocker(TerminalAccessibleObject __instance)
        {
            if (SolarFlareWeather.flareData != null)
            {
                if (SolarFlareWeather.flareData.IsDoorMalfunction && __instance.isBigDoor && seededRandom.NextDouble()<0.9f)
                {
                    return;
                }
            }
        }


    }
}


