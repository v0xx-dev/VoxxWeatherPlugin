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
                    playerController.drunknessInertia = Mathf.Clamp(playerController.drunknessInertia + Time.deltaTime / DrunknessPower * playerController.drunknessSpeed, 0.1f, 10f);
                    playerController.increasingDrunknessThisFrame = true;
                    if (damageTimer >= DamageTime)
                    {
                        playerController.DamagePlayer(DamageAmount, true, true, CauseOfDeath.Suffocation, 0, false, default);
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
            damageTimer = Mathf.Clamp(damageTimer - Time.deltaTime, 0, DamageTime);
        }
    }
}