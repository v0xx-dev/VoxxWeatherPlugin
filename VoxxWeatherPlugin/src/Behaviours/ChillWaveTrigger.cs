using UnityEngine;
using GameNetcodeStuff;
using VoxxWeatherPlugin.Utils;
using System.Collections;
using VoxxWeatherPlugin.Weathers;

namespace VoxxWeatherPlugin.Behaviours
{
    public class ChillWaveTrigger: MonoBehaviour
    {
        public AudioSource? audioSourceTemplate;
        private AudioSource[]? audioSources;
        public Camera? collisionCamera;
        [SerializeField]
        internal int WaveDamage => Configuration.chillingWaveDamage.Value;
        [SerializeField]
        internal float waveForce = 40f;
        internal Coroutine? temperatureChangeCoroutine;
        [SerializeField]
        internal bool collidedWithLocalPlayer = false;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                PlayerControllerB playerController = other.gameObject.GetComponent<PlayerControllerB>();
                if (playerController != GameNetworkManager.Instance.localPlayerController || collidedWithLocalPlayer || playerController.isInsideFactory)
                    return;
                if (PlayerEffectsManager.isInColdZone)
                {
                    if (temperatureChangeCoroutine == null)
                    {
                        temperatureChangeCoroutine = StartCoroutine(TemperatureChangeCoroutine());
                    }
                    if (WaveDamage > 0)
                    {
                        playerController.DamagePlayer(WaveDamage, causeOfDeath: CauseOfDeath.Unknown);
                    }
                    playerController.externalForceAutoFade += transform.forward * waveForce;
                    BlizzardVFXManager? blizzardVFX = BlizzardWeather.Instance?.VFXManager;
                    blizzardVFX?.PlayWavePassSFX();
                    collidedWithLocalPlayer = true;
                }
            }
        }

        // Decrease the player's temperature to simulate the cold wave effect (0.5 seconds duration)
        internal IEnumerator TemperatureChangeCoroutine()
        {
            float targetTemperature = -0.8f;
            float initialTemperature = PlayerEffectsManager.normalizedTemperature;
            if (initialTemperature > targetTemperature)
            {
                float duration = 0.5f; // half of a second
                float elapsedTime = 0f;

                while (elapsedTime < duration)
                {
                    float newTemperature = Mathf.Lerp(initialTemperature, targetTemperature, elapsedTime / duration);
                    // Calculate the delta to reach the new temperature
                    float temperatureDelta = newTemperature - PlayerEffectsManager.normalizedTemperature;
                    PlayerEffectsManager.SetPlayerTemperature(temperatureDelta); 
                    yield return null;
                }

                float finalDelta = targetTemperature - PlayerEffectsManager.normalizedTemperature;
                PlayerEffectsManager.SetPlayerTemperature(finalDelta);
            }
        } 

        internal void OnDisable()
        {
            if (temperatureChangeCoroutine != null)
            {
                StopCoroutine(temperatureChangeCoroutine);
                temperatureChangeCoroutine = null;
            }

            collidedWithLocalPlayer = false;
        }

        internal void SetupChillWave(Bounds levelBounds)
        {
            if (audioSourceTemplate == null)
            {
                audioSourceTemplate = gameObject.GetComponentInChildren<AudioSource>(true);
                audioSourceTemplate.gameObject.SetActive(false);
            }
            if (collisionCamera == null)
            {
                collisionCamera = gameObject.GetComponentInChildren<Camera>(true);
            }

            BoxCollider waveCollider = gameObject.GetComponent<BoxCollider>();

            //Change the center and scale y size so the lower edge is at SnowfallWeather.Instance.heightThreshold level, but current top edge is preserved
            float newHeightSpan = levelBounds.extents.y - LevelManipulator.Instance!.heightThreshold;
            waveCollider.center = new Vector3(0f, LevelManipulator.Instance.heightThreshold + newHeightSpan / 2, waveCollider.center.z);
            waveCollider.size = new Vector3(levelBounds.size.x, newHeightSpan, waveCollider.size.z);

            float maxLength = Mathf.Max(waveCollider.size.x, waveCollider.size.y, waveCollider.size.z) / 2f;
            collisionCamera!.orthographicSize = maxLength;
            float audioRange = audioSourceTemplate.maxDistance;
            // Destroy previous audio sources
            if (audioSources != null)
            {
                foreach (var audioSource in audioSources)
                { 
                    if (audioSource != null)
                    {
                        Destroy(audioSource.gameObject);
                    }
                }
            }
            // Place audio sources along collider x axis so that their range covers the whole box with 10% overlap between them
            audioSources = new AudioSource[Mathf.CeilToInt(waveCollider.size.x / (0.9f*audioRange))];
            for (int i = 0; i < audioSources.Length; i++)
            {
                audioSources[i] = Instantiate(audioSourceTemplate, transform);
                audioSources[i].transform.localPosition = new Vector3(0.9f*audioRange * i - waveCollider.size.x / 2f, 0, 0);
                audioSources[i].maxDistance = audioRange;
                audioSources[i].gameObject.SetActive(true);
            }
        }
        
    }
}