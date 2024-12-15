using UnityEngine;
using GameNetcodeStuff;
using VoxxWeatherPlugin.Utils;
using System.Collections;
using VoxxWeatherPlugin.Weathers;

namespace VoxxWeatherPlugin.Behaviours
{
    public class ChillWaveTrigger: MonoBehaviour
    {
        [SerializeField]
        internal int waveDamage = 20;
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
                if (playerController != GameNetworkManager.Instance.localPlayerController || collidedWithLocalPlayer)
                    return;
                if (PlayerTemperatureManager.isInColdZone)
                {
                    if (temperatureChangeCoroutine == null)
                    {
                        temperatureChangeCoroutine = StartCoroutine(TemperatureChangeCoroutine());
                    }
                    playerController.DamagePlayer(waveDamage, causeOfDeath: CauseOfDeath.Unknown);
                    playerController.externalForceAutoFade += transform.forward * waveForce;
                    BlizzardVFXManager? blizzardVFX = SnowfallWeather.Instance?.VFXManager as BlizzardVFXManager; // TODO: super cursed, but ok for now
                    blizzardVFX?.PlayWavePassSFX();
                    collidedWithLocalPlayer = true;
                }
            }
        }

        // Decrease the player's temperature to simulate the cold wave effect (0.5 seconds duration)
        internal IEnumerator TemperatureChangeCoroutine()
        {
            float targetTemperature = -0.8f;
            float initialTemperature = PlayerTemperatureManager.normalizedTemperature;
            if (initialTemperature > targetTemperature)
            {
                float duration = 0.5f; // half of a second
                float elapsedTime = 0f;

                while (elapsedTime < duration)
                {
                    float newTemperature = Mathf.Lerp(initialTemperature, targetTemperature, elapsedTime / duration);
                    // Calculate the delta to reach the new temperature
                    float temperatureDelta = newTemperature - PlayerTemperatureManager.normalizedTemperature;
                    PlayerTemperatureManager.SetPlayerTemperature(temperatureDelta); 
                    yield return null;
                }

                float finalDelta = targetTemperature - PlayerTemperatureManager.normalizedTemperature;
                PlayerTemperatureManager.SetPlayerTemperature(finalDelta);
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
    }
}