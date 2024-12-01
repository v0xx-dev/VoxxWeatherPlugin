using UnityEngine;
using GameNetcodeStuff;
using VoxxWeatherPlugin.Utils;
using System.Collections;

namespace VoxxWeatherPlugin.Behaviours
{
    public class ChillWaveTrigger: MonoBehaviour
    {
        [SerializeField]
        internal int waveDamage = 19;
        [SerializeField]
        internal float waveForce = 10f;
        internal Coroutine? temperatureChangeCoroutine;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                PlayerControllerB playerController = other.gameObject.GetComponent<PlayerControllerB>();
                if (playerController != GameNetworkManager.Instance.localPlayerController)
                    return;
                if (PlayerTemperatureManager.isInColdZone)
                {
                    if (temperatureChangeCoroutine == null)
                    {
                        temperatureChangeCoroutine = StartCoroutine(TemperatureChangeCoroutine());
                    }
                    playerController.DamagePlayer(waveDamage, causeOfDeath: CauseOfDeath.Unknown);
                    playerController.externalForces += transform.forward * waveForce;
                    HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
                }
                else if (temperatureChangeCoroutine != null)
                {
                    StopCoroutine(temperatureChangeCoroutine);
                    temperatureChangeCoroutine = null;
                }
            }
        }

        internal IEnumerator TemperatureChangeCoroutine()
        {
            float targetTemperature = -0.8f;
            float initialTemperature = PlayerTemperatureManager.normalizedTemperature;
            if (initialTemperature > targetTemperature)
            {
                float duration = 0.25f; // quarter of a second
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
    }
}