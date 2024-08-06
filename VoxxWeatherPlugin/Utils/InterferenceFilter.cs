using UnityEngine;

namespace VoxxWeatherPlugin.Utils
{
    [RequireComponent(typeof(AudioSource))]
    public class InterferenceDistortionFilter : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] internal float noiseLevel = 0.2f;
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

        private void Start()
        {
            sampleRate = AudioSettings.outputSampleRate;
            phase = 0;
            modulationPhase = 0;
            isClarity = false;
            remainingClaritySamples = 0;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            int numSamples = data.Length;
            Debug.Log($"numSamples: {numSamples}");

            // Process audio samples
            for (int i = 0; i < numSamples; i++)
            {
                // Check if we need to start a new clarity window
                if (!isClarity && remainingClaritySamples <= 0)
                {
                    if (Random.value < -Mathf.Log(distortionChance + 1e-12f)/(numSamples * channels))
                    {
                        isClarity = true;
                        float clarityDuration = Random.Range(minClarityDuration, maxClarityDuration);
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

                    // Add noise
                    float noise = sample * Random.Range(-1f, 1f) * noiseLevel;

                    // Combine effects
                    sample += noise;

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
                    data[i] *= Random.Range(-1f, 1f);
                }
            }
        }
    }

    public class AudioFilterManager : MonoBehaviour
    {
        internal WalkieTalkie currentWalkie;
        void Update()
        {
            currentWalkie.audioSourcesReceiving = 
            // Loop through each AudioSource and add a filter
            foreach (AudioSource source in audioSources)
            {
                InterferenceDistortionFilter filter = source.gameObject.AddComponent<InterferenceDistortionFilter>();

            }
        }
    }
}