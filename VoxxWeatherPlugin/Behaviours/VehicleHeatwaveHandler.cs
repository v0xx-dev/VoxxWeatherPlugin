using UnityEngine;
using VoxxWeatherPlugin.Utils;
using Unity.Netcode;
using System.Collections;

namespace VoxxWeatherPlugin.Behaviours
{
    
    public class VehicleHeatwaveHandler : NetworkBehaviour
    {
        public VehicleController vehicleController;
        public float normalizedTimeInHeatwave = 0f;
        public float overheatThreshold = 60f;
        public float engineDieIntervalMin = 10f;
        public float engineDieIntervalMax = 20f;
        public int engineDieDamage = 1;

        public bool isInHeatwave = false;
        private Coroutine turbSoundCoroutine; // Changed to Coroutine
        private float engineDieTimer = 0f;
        private System.Random seededRandom;

        private void Start()
        {
            seededRandom = new System.Random(StartOfRound.Instance.randomMapSeed);
            vehicleController = GetComponent<VehicleController>();
            engineDieTimer = seededRandom.NextDouble(engineDieIntervalMin, engineDieIntervalMax);
        }

        private void Update()
        {
            if (!IsServer) return;

            if (isInHeatwave && vehicleController.ignitionStarted)
            {
                normalizedTimeInHeatwave += Time.deltaTime / overheatThreshold;
                normalizedTimeInHeatwave = Mathf.Clamp01(normalizedTimeInHeatwave);
                
                if (normalizedTimeInHeatwave >= 1)
                {
                    PlayTurbulenceSoundClientRpc();
                    engineDieTimer -= Time.deltaTime;

                    if (engineDieTimer <= 0f)
                    {
                        StopTurbulenceSoundClientRpc();
                        vehicleController.CancelTryIgnitionClientRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId, true);
                        vehicleController.DealPermanentDamage(engineDieDamage);
                        engineDieTimer = seededRandom.NextDouble(engineDieIntervalMin, engineDieIntervalMax);
                    }
                }
            }
            else if (!vehicleController.ignitionStarted && normalizedTimeInHeatwave > 0)
            {
                normalizedTimeInHeatwave -= 6f * Time.deltaTime / overheatThreshold;
                normalizedTimeInHeatwave = Mathf.Max(normalizedTimeInHeatwave, 0f);
                if (engineDieTimer < engineDieIntervalMin)
                    engineDieTimer = seededRandom.NextDouble(engineDieIntervalMin, engineDieIntervalMax);
            }
        }

        [ClientRpc]
        private void PlayTurbulenceSoundClientRpc()
        {
            if (turbSoundCoroutine == null)
            {
                turbSoundCoroutine = StartCoroutine(TurbulenceSoundCoroutine());
            }
        }

        [ClientRpc]
        private void StopTurbulenceSoundClientRpc()
        {
            if (turbSoundCoroutine != null)
            {
                StopCoroutine(turbSoundCoroutine);
                turbSoundCoroutine = null;
            }
        }

        private IEnumerator TurbulenceSoundCoroutine()
        {
            while (true) // Loop to continuously play the sound
            {
                AudioClip turbulenceSound = vehicleController.turbulenceAudio.clip;
                vehicleController.engineAudio1.PlayOneShot(turbulenceSound, 1f);
                yield return new WaitForSeconds(turbulenceSound.length);
            }
        }
    }
}
