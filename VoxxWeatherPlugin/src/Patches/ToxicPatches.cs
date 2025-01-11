using GameNetcodeStuff;
using HarmonyLib;
using LethalLib.Modules;
using UnityEngine;
using VoxxWeatherPlugin.Utils;
using VoxxWeatherPlugin.Weathers;

namespace VoxxWeatherPlugin.Patches
{
    [HarmonyPatch]
    internal class ToxicPatches
    {
        private static float DamageInterval => Configuration.ToxicDamageInterval.Value;
        private static int DamageAmount => Configuration.ToxicDamageAmount.Value;
        private static float PoisoningRemovalMultiplier => Configuration.PoisoningRemovalMultiplier.Value;

        private static float damageTimer = 0f;

        [HarmonyPatch(typeof(PlayerControllerB), "LateUpdate")]
        [HarmonyPostfix]
        private static void PoisoningPatchPrefix(PlayerControllerB __instance)
        {
            if (!(ToxicSmogWeather.Instance?.IsActive ?? false) || __instance != GameNetworkManager.Instance?.localPlayerController)
                return;
            
            if (__instance.isPlayerDead || __instance.isInHangarShipRoom || __instance.isInElevator)
            {
                PlayerEffectsManager.isPoisoned = false;
            }

            if (PlayerEffectsManager.isPoisoned)
            {
                damageTimer += Time.deltaTime;
                PlayerEffectsManager.SetPoisoningEffect(Time.deltaTime);
                if (damageTimer >= DamageInterval)
                {
                    __instance.DamagePlayer(DamageAmount, true, true, CauseOfDeath.Suffocation, 0, false, default);
                    damageTimer = 0;
                }
            }
            else
            {
                PlayerEffectsManager.SetPoisoningEffect(-Time.deltaTime * PoisoningRemovalMultiplier);
                if (damageTimer > 0)
                {
                    damageTimer -= Time.deltaTime * PoisoningRemovalMultiplier;
                }
            }

            PlayerEffectsManager.isPoisoned = false;
        }
    }
}
