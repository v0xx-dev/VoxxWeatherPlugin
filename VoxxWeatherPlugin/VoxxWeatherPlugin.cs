using BepInEx;
using HarmonyLib;
using UnityEngine;
using VoxxWeatherPlugin.Patches;
using VoxxWeatherPlugin.Utils;
using System.Reflection;
using BepInEx.Logging;
using BepInEx.Configuration;

namespace VoxxWeatherPlugin
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("mrov.WeatherRegistry", BepInDependency.DependencyFlags.HardDependency)]
    public class VoxxWeatherPlugin : BaseUnityPlugin
    {
        private Harmony harmony;
        public static VoxxWeatherPlugin instance;
        internal static ManualLogSource StaticLogger;

        // Config entries
        public static ConfigEntry<bool> EnableHeatwaveWeather;
        public static ConfigEntry<bool> EnableSolarFlareWeather;
        public static ConfigEntry<uint> AuroraHeight;
        public static ConfigEntry<float> AuroraSpawnAreaBox;
        public static ConfigEntry<float> AuroraVisibilityThreshold;
        public static ConfigEntry<float> AuroraSpawnRate;
        public static ConfigEntry<float> AuroraSize;
        public static ConfigEntry<uint> HeatwaveParticlesSpawnRate;
        public static ConfigEntry<bool> DistortOnlyVoiceDuringSolarFlare;

        private void Awake()
        {
            instance = this;
            StaticLogger = Logger; 
            
            NetcodePatcher();

            InitializeConfig();
            
            harmony = new Harmony(PluginInfo.PLUGIN_GUID);

            if (VoxxWeatherPlugin.EnableSolarFlareWeather.Value)    
            {
                WeatherTypeLoader.RegisterFlareWeather();
                harmony.PatchAll(typeof(FlarePatches));
                if (!VoxxWeatherPlugin.DistortOnlyVoiceDuringSolarFlare.Value)
                {
                    harmony.PatchAll(typeof(FlareOptionalWalkiePatches));
                    Logger.LogInfo($"{PluginInfo.PLUGIN_GUID} optional solar flare patches successfully applied!");
                }
                Logger.LogInfo($"{PluginInfo.PLUGIN_GUID} solar flare patches successfully applied!");
            }

            if (VoxxWeatherPlugin.EnableHeatwaveWeather.Value)
            {
                WeatherTypeLoader.RegisterHeatwaveWeather();
                harmony.PatchAll(typeof(HeatwavePatches));
                Logger.LogInfo($"{PluginInfo.PLUGIN_GUID} heatwave patches successfully applied!");
            }

            // WeatherTypeLoader.RegisterBlizzardWeather();
            // WeatherTypeLoader.RegisterSnowfallWeather();

            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }

        private static void NetcodePatcher()
        {
            var types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
        }

        private void InitializeConfig()
        {
            EnableHeatwaveWeather = Config.Bind("Weather", "EnableHeatwaveWeather", true, "Enable or disable Heatwave weather");
            EnableSolarFlareWeather = Config.Bind("Weather", "EnableSolarFlareWeather", true, "Enable or disable Solar Flare weather");
            HeatwaveParticlesSpawnRate = Config.Bind("Heatwave", "ParticlesSpawnRate", (uint)20, "Spawn rate of Heatwave particles. Particles per second. Capped at 42");
            AuroraHeight = Config.Bind("SolarFlare", "AuroraHeight", (uint)120, "Height of the Aurora effect above the ground");
            AuroraSpawnAreaBox = Config.Bind("SolarFlare", "AuroraSpawnArea", 500f, "Size of the Aurora spawn area. The Aurora effect will spawn randomly within this square area. VFX may disappear at certain angles if the area is too small or too large.");
            AuroraVisibilityThreshold = Config.Bind("Aurora", "AuroraVisibilityThreshold", 8f, "Light threshold when Aurora becomes visible (in Lux). Increase to make it more visible.");
            AuroraSpawnRate = Config.Bind("SolarFlare", "AuroraSpawnRate", 0.1f, "Spawn rate of Aurora effects. Auroras per second. Capped at 32");
            AuroraSize = Config.Bind("SolarFlare", "AuroraSize", 100f, "Size of the Aurora 'strips' in the sky");
            DistortOnlyVoiceDuringSolarFlare = Config.Bind("SolarFlare", "DistortOnlyVoice", true, "Distort only player voice during Solar Flare (true) or all sounds (false) on a walkie-talkie");
        }
    }

    public static class Debug
    {
        private static ManualLogSource Logger => VoxxWeatherPlugin.StaticLogger;

        public static void Log(string message) => Logger.LogInfo(message);
        public static void LogError(string message) => Logger.LogError(message);
        public static void LogWarning(string message) => Logger.LogWarning(message);
        public static void LogDebug(string message) => Logger.LogDebug(message);
        public static void LogMessage(string message) => Logger.LogMessage(message);
        public static void LogFatal(string message) => Logger.LogFatal(message);
    }

}
