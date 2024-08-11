using UnityEngine;
using VoxxWeatherPlugin.Weathers;
using System.Collections.Generic;
using VoxxWeatherPlugin.Utils;
using GameNetcodeStuff;

namespace VoxxWeatherPlugin.Behaviours
{
    public class WalkieDistortionManager: MonoBehaviour
    {
        internal Dictionary<AudioSource, GameObject> walkieSubTargets = new Dictionary<AudioSource, GameObject>();
        private static Dictionary<AudioSource, InterferenceDistortionFilter> cachedFilters = new Dictionary<AudioSource, InterferenceDistortionFilter>();

        internal AudioSource SplitWalkieTarget(GameObject target)
        {
            if (SolarFlareWeather.flareData != null)
            {
                GameObject subTarget = new GameObject("SubTarget");
                subTarget.transform.position = target.transform.position;
                subTarget.transform.SetParent(gameObject.transform);
                AudioSource audioSource = subTarget.AddComponent<AudioSource>();
                InterferenceDistortionFilter interferenceFilter = subTarget.AddComponent<InterferenceDistortionFilter>();
                interferenceFilter.distortionChance = SolarFlareWeather.flareData.RadioDistortionIntensity;
                interferenceFilter.maxClarityDuration = SolarFlareWeather.flareData.RadioBreakthroughLength;
                interferenceFilter.maxFrequencyShift = SolarFlareWeather.flareData.RadioFrequencyShift;
                walkieSubTargets.Add(audioSource, subTarget);
                return audioSource;
            }
            else
            { 
                return target.AddComponent<AudioSource>();
            }
        }

        internal void DisposeWalkieTarget(AudioSource audioSource)
        {
            if (SolarFlareWeather.flareData != null)
            {
                if (walkieSubTargets.TryGetValue(audioSource, out GameObject subTarget))
                {
                    walkieSubTargets.Remove(audioSource);
                    Destroy(subTarget);
                }
                else
                    Debug.LogError("Failed to dispose walkie target: target not found in dictionary.");
            }
            else
                Destroy(audioSource);
        }

        public static void UpdateVoiceChatDistortion(AudioSource voiceSource, PlayerControllerB allPlayerScript, bool isUsingWalkieTalkie)
        {
            bool shouldEnableDistortion = !allPlayerScript.isPlayerDead 
                                        && isUsingWalkieTalkie 
                                        && SolarFlareWeather.flareData != null;

            InterferenceDistortionFilter interferenceFilter = GetOrAddFilter(voiceSource);
            if (interferenceFilter == null)
            {
                return;
            }

            if (shouldEnableDistortion)
            {
                EnableVoiceChatDistortion(interferenceFilter);
            }
            else
            {
                DisableVoiceChatDistortion(interferenceFilter);
            }
        }

        private static void EnableVoiceChatDistortion(InterferenceDistortionFilter interferenceFilter)
        {
            interferenceFilter.enabled = true;
            interferenceFilter.distortionChance = SolarFlareWeather.flareData.RadioDistortionIntensity;
            interferenceFilter.maxClarityDuration = SolarFlareWeather.flareData.RadioBreakthroughLength;
            interferenceFilter.maxFrequencyShift = SolarFlareWeather.flareData.RadioFrequencyShift;
        }

        private static void DisableVoiceChatDistortion(InterferenceDistortionFilter interferenceFilter)
        {
            interferenceFilter.enabled = false;
        }

        private static InterferenceDistortionFilter GetOrAddFilter(AudioSource voiceSource)
        {
            if (voiceSource == null)
            {
                Debug.LogError("Attempted to get or add filter for a null voice source!");
                return null;
            }

            if (!cachedFilters.TryGetValue(voiceSource, out InterferenceDistortionFilter filter))
            {
                filter = voiceSource.GetComponent<InterferenceDistortionFilter>();
                if (filter == null)
                {
                    filter = voiceSource.gameObject.AddComponent<InterferenceDistortionFilter>();
                }
                cachedFilters[voiceSource] = filter;
            }
            return filter;
        }

        public static void ClearFilterCache(AudioSource voiceSource = null)
        {
            if (voiceSource != null)
            {
                cachedFilters.Remove(voiceSource);
            }
            else
            {
                cachedFilters.Clear();
            }
        }
    }
}