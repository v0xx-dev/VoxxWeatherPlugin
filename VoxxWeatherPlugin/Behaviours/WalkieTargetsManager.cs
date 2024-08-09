using UnityEngine;
using VoxxWeatherPlugin.Weathers;
using System.Collections.Generic;
using VoxxWeatherPlugin.Utils;

namespace VoxxWeatherPlugin.Behaviours
{
    public class WalkieTargetsManager: MonoBehaviour
    {
        internal Dictionary<AudioSource, GameObject> walkieSubTargets = new Dictionary<AudioSource, GameObject>();

        internal AudioSource SplitWalkieTarget(GameObject target)
        {
            if (SolarFlareWeather.flareData != null)
            {
                GameObject subTarget = new GameObject("SubTarget");
                subTarget.transform.SetParent(gameObject.transform);
                AudioSource audioSource = subTarget.AddComponent<AudioSource>();
                InterferenceDistortionFilter interferenceFilter = subTarget.AddComponent<InterferenceDistortionFilter>();
                interferenceFilter.distortionChance = SolarFlareWeather.flareData.RadioDistortionIntensity;
                interferenceFilter.maxClarityDuration = SolarFlareWeather.flareData.RadioBreakthroughLength;
                walkieSubTargets.Add(audioSource, subTarget);
                return audioSource;
            }
            else 
                return target.AddComponent<AudioSource>();
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
    }
}