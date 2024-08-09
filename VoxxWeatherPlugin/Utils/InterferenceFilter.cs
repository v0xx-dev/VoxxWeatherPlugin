using UnityEngine;
using System;
using VoxxWeatherPlugin.Weathers;
using System.Collections.Generic;
namespace VoxxWeatherPlugin.Utils
{
    [RequireComponent(typeof(AudioSource))]
    public class InterferenceDistortionFilter : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] internal float noiseLevel = 0.02f;
        [SerializeField] internal float frequencyShift = 180f;
        [SerializeField] internal float modulationFrequency = 5f;
        [SerializeField, Range(0f, 1f)] internal float distortionChance = 0.35f;
        [SerializeField] internal float minClarityDuration = 0.01f;
        [SerializeField] internal float maxClarityDuration = 1f;

        private float phase;
        private float modulationPhase;
        private bool isClarity;
        private int sampleRate;
        private int remainingClaritySamples;
        private System.Random random;

        private void Start()
        {
            sampleRate = AudioSettings.outputSampleRate;
            phase = 0;
            modulationPhase = 0;
            isClarity = false;
            remainingClaritySamples = 0;
            random = new System.Random(42);
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            int numSamples = data.Length;

            // Process audio samples
            for (int i = 0; i < numSamples; i++)
            {
                // Check if we need to start a new clarity window
                if (!isClarity && remainingClaritySamples <= 0)
                {
                    if (random.NextDouble() < -Mathf.Log(distortionChance + 1e-12f) / (numSamples))
                    {
                        isClarity = true;
                        float clarityDuration = random.NextDouble(minClarityDuration, maxClarityDuration);
                        remainingClaritySamples = (int)(clarityDuration * sampleRate * channels);
                    }
                }

                if (isClarity)
                {
                    float sample = data[i];

                    // Apply frequency shift
                    phase += frequencyShift / sampleRate;
                    phase %= 1;
                    sample *= Mathf.Cos(2 * Mathf.PI * phase);

                    // Apply amplitude modulation
                    modulationPhase += modulationFrequency / sampleRate;
                    modulationPhase %= 1;
                    float modulation = 0.5f * (1 + Mathf.Sin(2 * Mathf.PI * modulationPhase));
                    sample *= modulation;

                    // Modulate with noise
                    sample *= random.NextDouble(0, 1f);

                    // Clamp to -1 to 1
                    sample = Mathf.Clamp(sample, -1f, 1f);

                    data[i] = sample;

                    remainingClaritySamples--;
                    if (remainingClaritySamples <= 0)
                    {
                        isClarity = false;
                    }
                }
                else
                {
                    data[i] *= random.NextDouble(-noiseLevel, noiseLevel);
                }
            }
        }
    }

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
