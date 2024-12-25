using UnityEngine;
using GameNetcodeStuff;
using VoxxWeatherPlugin.Utils;

namespace VoxxWeatherPlugin.Behaviours
{
    internal class ToxicFumes : MonoBehaviour
    {
        [SerializeField]
        private float DamageTime => Configuration.ToxicDamageInterval.Value;
        [SerializeField]
        private float DrunknessPower => Configuration.ToxicPoisoningStrength.Value;
        [SerializeField]
        private int DamageAmount => Configuration.ToxicDamageAmount.Value;

        private static float damageTimer = 0f;
        private static bool isPoisoningLocalPlayer = false;

        protected virtual void OnTriggerStay(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                PlayerControllerB playerController = other.gameObject.GetComponent<PlayerControllerB>();

                if (playerController == GameNetworkManager.Instance.localPlayerController )
                {
                    isPoisoningLocalPlayer = true;
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
                    isPoisoningLocalPlayer = false;
                }
            }
        }

        private void Update()
        {
            PlayerControllerB playerController = GameNetworkManager.Instance.localPlayerController;

            if (playerController.isPlayerDead || playerController.isInHangarShipRoom)
            {
                isPoisoningLocalPlayer = false;
            }

            if (isPoisoningLocalPlayer)
            {
                damageTimer += Time.deltaTime;
                if (damageTimer >= DamageTime)
                {
                    playerController.DamagePlayer(DamageAmount, true, true, CauseOfDeath.Suffocation, 0, false, default);
                    damageTimer = 0;
                }
            }
            else if (damageTimer > 0)
            {
                damageTimer -= Time.deltaTime;
            }
            
        }
    }
}