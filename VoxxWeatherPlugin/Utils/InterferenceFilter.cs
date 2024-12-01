using UnityEngine;
using System;

namespace VoxxWeatherPlugin.Utils
{
    [RequireComponent(typeof(AudioSource))]
    public class InterferenceDistortionFilter : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] internal float noiseLevel = 0.005f;
        [SerializeField, Range(0f, 1f)] internal float distortionChance = 0.35f;
        [SerializeField] internal float maxFrequencyShift = 250f;
        [SerializeField] internal float freqModulationPeriod = 30f;
        [SerializeField] internal float minClarityDuration = 0.01f;
        [SerializeField] internal float maxClarityDuration = 1f;
        [SerializeField] internal float freqShiftMultiplier = 1.5f;
        
        private float phase = 0;
        private float freqPhase = 0;
        private bool isClarity = false;
        private int remainingClaritySamples = 0;
        private int sampleRate;
        private System.Random? random;

        private void Start()
        {
            sampleRate = AudioSettings.outputSampleRate;
            random = new System.Random(42);
            noiseLevel = VoxxWeatherPlugin.NoiseStaticLevel.Value;
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
                    int averageClarityDuration = (int)(1 + (minClarityDuration + maxClarityDuration) / 2 * sampleRate * channels);
                    float windowChance = -(1 - 1 / (distortionChance + float.Epsilon)) / averageClarityDuration;
                    if (random?.NextDouble() <  windowChance)
                    {
                        isClarity = true;
                        float clarityDuration = random.NextDouble(minClarityDuration, maxClarityDuration);
                        remainingClaritySamples = (int)(clarityDuration * sampleRate * channels);
                    }
                }

                if (isClarity)
                {
                    float sample = data[i];

                    // Apply amplitude modulation
                    freqPhase += 1 / (sampleRate * 2 * freqModulationPeriod);
                    freqPhase %= 1;
                    float frequencyShift = maxFrequencyShift * Mathf.Cos(2 * Mathf.PI * freqPhase);

                    phase += frequencyShift / sampleRate;
                    phase %= 1;
                    sample *= freqShiftMultiplier * Mathf.Cos(2 * Mathf.PI* phase);

                    // Modulate with noise
                    sample *= 1 - distortionChance + (random?.NextDouble(0, distortionChance) ?? distortionChance/2);

                    // Clamp to -1 to 1
                    data[i] = Mathf.Clamp(sample, -1f, 1f);

                    remainingClaritySamples--;
                    if (remainingClaritySamples <= 0)
                    {
                        isClarity = false;
                    }
                }
                else
                {
                    data[i] = random?.NextDouble(-noiseLevel, noiseLevel) ?? 0;
                }
            }
        }
    }
}
