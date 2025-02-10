using HarmonyLib;
using VoxxWeatherPlugin.Behaviours;
using WeatherRegistry;

namespace VoxxWeatherPlugin.Patches
{
    [HarmonyPatch]
    public class BasicPatches
    {
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ChangeLevel))]
        [HarmonyPostfix]
        private static void CacheWeatherPatch()
        {
            CacheWeather();
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.StartGame))]
        [HarmonyPostfix]
        private static void ReCacheWeatherPatch()
        {
            CacheWeather();
        }

        private static void CacheWeather()
        {
            if (LevelManipulator.Instance != null)
            {
                LevelManipulator.Instance.currentWeather = WeatherManager.GetCurrentLevelWeather();
            }
        }
    }
}