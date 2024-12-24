using UnityEngine;
using GameNetcodeStuff;

namespace VoxxWeatherPlugin.Behaviours
{
    internal class ToxicFumes : MonoBehaviour
    {
        [SerializeField]
        private float damageTime = 3f;
        [SerializeField]
        private float drunknessPower = 1.5f;
        [SerializeField]
        private int damageAmount = 5;

        private float damageTimer = 0f;
        private bool isPoisoningLocalPlayer = false;

        protected virtual void OnTriggerStay(Collider other)
        {
            ApplyToxicEffect(other);
        }

        internal void ApplyToxicEffect(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                PlayerControllerB playerController = other.gameObject.GetComponent<PlayerControllerB>();

                if (playerController == GameNetworkManager.Instance.localPlayerController && !playerController.isInHangarShipRoom)
                {
                    if (playerController.isPlayerDead)
                    {
                        isPoisoningLocalPlayer = false;
                        return;
                    }

                    isPoisoningLocalPlayer = true;
                    damageTimer += Time.deltaTime;
                    playerController.drunknessInertia = Mathf.Clamp(playerController.drunknessInertia + Time.deltaTime / drunknessPower * playerController.drunknessSpeed, 0.1f, 10f);
                    playerController.increasingDrunknessThisFrame = true;
                    if (damageTimer >= damageTime)
                    {
                        playerController.DamagePlayer(damageAmount, true, true, CauseOfDeath.Suffocation, 0, false, default);
                        damageTimer = 0;
                    }
                }
            }
        }
        

        internal void OnTriggerExit(Collider other)
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

        protected virtual void Update()
        {
            if (isPoisoningLocalPlayer || damageTimer <= 0)
            {
                return;
            }
            damageTimer = Mathf.Clamp(damageTimer - Time.deltaTime, 0, damageTime);
        }
    }
}