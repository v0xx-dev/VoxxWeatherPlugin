using UnityEngine;
using GameNetcodeStuff;
using VoxxWeatherPlugin.Utils;

namespace VoxxWeatherPlugin.Behaviours
{
    internal class ToxicFumes : MonoBehaviour
    {   
        private static float DrunknessPower => Configuration.ToxicPoisoningStrength.Value;

        protected virtual void OnTriggerStay(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                PlayerControllerB playerController = other.gameObject.GetComponent<PlayerControllerB>();

                if (playerController == GameNetworkManager.Instance.localPlayerController )
                {
                    PlayerEffectsManager.isPoisoned = true;
                    // We want to increase drunkness only when player is in the toxic fumes area
                    playerController.drunknessInertia = Mathf.Clamp(playerController.drunknessInertia + Time.deltaTime / DrunknessPower * playerController.drunknessSpeed, 0.1f, 10f);
                    playerController.increasingDrunknessThisFrame = true;
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                PlayerControllerB playerController = other.gameObject.GetComponent<PlayerControllerB>();

                if (playerController == GameNetworkManager.Instance.localPlayerController)
                {
                    PlayerEffectsManager.isPoisoned = false;
                }
            }
        }
    }
}