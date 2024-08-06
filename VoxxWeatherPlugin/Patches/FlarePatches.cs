using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using VoxxWeatherPlugin.Utils;
using VoxxWeatherPlugin.Weathers;
using UnityEngine;

namespace VoxxWeatherPlugin.Patches
{
    [HarmonyPatch]
    internal class FlarePatches
    {
        internal static System.Random random = new System.Random();
        internal static System.Random seededRandom = new System.Random(42);
        internal static Transform originalTeleporterPosition;

        [HarmonyPatch(typeof(WalkieTalkie), "Start")]
        [HarmonyPostfix]
        private static void WalkieDistortionPatch(WalkieTalkie __instance)
        {
            __instance.target.gameObject.AddComponent<InterferenceDistortionFilter>();
        }

        [HarmonyPatch(typeof(WalkieTalkie), "GetAllAudioSourcesToReplay")]
        [HarmonyPostfix]
        private static void DistortionUpdatePatch(WalkieTalkie __instance)
        {
            InterferenceDistortionFilter distortionFilter = __instance.target.gameObject.GetComponent<InterferenceDistortionFilter>();
            AudioLowPassFilter lowPassFilter = __instance.target.gameObject.GetComponent<AudioLowPassFilter>();
            AudioHighPassFilter highPassFilter = __instance.target.gameObject.GetComponent<AudioHighPassFilter>();
            AudioDistortionFilter audioDistortionFilter = __instance.target.gameObject.GetComponent<AudioDistortionFilter>();
            if (distortionFilter != null)
            {
                if (SolarFlareWeather.flareData != null)
                {
                    distortionFilter.distortionChance = SolarFlareWeather.flareData.RadioDistortionIntensity;
                    distortionFilter.maxClarityDuration = SolarFlareWeather.flareData.RadioBreakthroughLength;
                    distortionFilter.enabled = true;
                    lowPassFilter.enabled = false;
                    highPassFilter.enabled = false;
                    audioDistortionFilter.enabled = false;
                }
                else
                {
                    distortionFilter.enabled = false;
                    lowPassFilter.enabled = true;
                    highPassFilter.enabled = true;
                    audioDistortionFilter.enabled = true;
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
