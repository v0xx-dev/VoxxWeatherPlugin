using GameNetcodeStuff;
using HarmonyLib;
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

        private static float damageTimer = 0f;

        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        [HarmonyPrefix]
        private static void PoisoningPatchPrefix(PlayerControllerB __instance)
        {
            if (!(ToxicSmogWeather.Instance?.IsActive ?? false) || !__instance.IsOwner || !__instance.isPlayerControlled)
                return;
            
            if (__instance.isPlayerDead || __instance.isInHangarShipRoom)
            {
                PlayerEffectsManager.isPoisoned = false;
            }

            if (PlayerEffectsManager.isPoisoned)
            {
                damageTimer += Time.deltaTime;
                if (damageTimer >= DamageInterval)
                {
                    __instance.DamagePlayer(DamageAmount, true, true, CauseOfDeath.Suffocation, 0, false, default);
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
