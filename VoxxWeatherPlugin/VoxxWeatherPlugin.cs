using BepInEx;
using HarmonyLib;
using UnityEngine;
using VoxxWeatherPlugin.Patches;
using VoxxWeatherPlugin.Utils;
using BepInEx.Logging;
using System.Reflection;
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
        public static ConfigEntry<float> HeatwaveParticlesSpawnRate;
        public static ConfigEntry<float> TimeUntilStrokeMin;
        public static ConfigEntry<float> TimeUntilStrokeMax;
        public static ConfigEntry<bool> DistortOnlyVoiceDuringSolarFlare;
        public static ConfigEntry<float> BatteryDrainMultiplier;
        public static ConfigEntry<bool> DrainBatteryInFacility;
        public static ConfigEntry<bool> DoorMalfunctionEnabled;
        public static ConfigEntry<float> DoorMalfunctionChance;
        public static ConfigEntry<float> NoiseStaticLevel;

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

            if (true)
            {
                WeatherTypeLoader.RegisterSnowfallWeather();
                // WeatherTypeLoader.RegisterBlizzardWeather();
                harmony.PatchAll(typeof(SnowPatches));
                Logger.LogInfo($"{PluginInfo.PLUGIN_GUID} snow patches successfully applied!");
            }

            //WeatherTypeLoader.RegisterMeteorWeather();

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
            // Weather
            EnableHeatwaveWeather = Config.Bind("Weather", "EnableHeatwaveWeather", true, "Enable or disable Heatwave weather");
            EnableSolarFlareWeather = Config.Bind("Weather", "EnableSolarFlareWeather", true, "Enable or disable Solar Flare weather");
            // Heatwave
            HeatwaveParticlesSpawnRate = Config.Bind("Heatwave", "ParticlesSpawnRate", 20f, new ConfigDescription("Spawn rate of Heatwave particles. Particles per second", new AcceptableValueRange<float>(0, 42)));
            TimeUntilStrokeMin = Config.Bind("Heatwave", "TimeUntilStrokeMin", 40f, new ConfigDescription("Minimal time in seconds until heatstroke (min)", new AcceptableValueRange<float>(1, 9999f)));
            TimeUntilStrokeMax = Config.Bind("Heatwave", "TimeUntilStrokeMax", 80f, new ConfigDescription("Maximal time in seconds until heatstroke (max). Must be higher than min! Actual time is random between min and max", new AcceptableValueRange<float>(1, 9999f)));
            // Solar Flare
            AuroraHeight = Config.Bind("SolarFlare", "AuroraHeight", (uint)120, "Height of the Aurora effect above the ground");
            AuroraSpawnAreaBox = Config.Bind("SolarFlare", "AuroraSpawnArea", 500f, "Size of the Aurora spawn area. The Aurora effect will spawn randomly within this square area. VFX may disappear at certain angles if the area is too small or too large.");
            AuroraVisibilityThreshold = Config.Bind("SolarFlare", "AuroraVisibilityThreshold", 9f, "Light threshold when Aurora becomes visible (in Lux). Increase to make it more visible.");
            AuroraSpawnRate = Config.Bind("SolarFlare", "AuroraSpawnRate", 0.1f, new ConfigDescription("Spawn rate of Aurora effects. Auroras per second", new AcceptableValueRange<float>(0, 32f)));
            AuroraSize = Config.Bind("SolarFlare", "AuroraSize", 100f, "Size of the Aurora 'strips' in the sky");
            DistortOnlyVoiceDuringSolarFlare = Config.Bind("SolarFlare", "DistortOnlyVoice", true, "Distort only player voice during Solar Flare (true) or all sounds (false) on a walkie-talkie");
            BatteryDrainMultiplier = Config.Bind("SolarFlare", "BatteryDrainMultiplier", 1.0f, new ConfigDescription("Multiplier for additional battery drain during Solar Flare. 1.0 is normal drain, 0.5 is half drain, 2.0 is double drain, 0 no additional drain, etc. Default value is equal to 60 - 200 % faster drain depending on the type of flare.", new AcceptableValueRange<float>(0, 100f)));
            DrainBatteryInFacility = Config.Bind("SolarFlare", "DrainBatteryInFacility", false, "Drain item battery even when inside a facility during Solar Flare");
            DoorMalfunctionEnabled = Config.Bind("SolarFlare", "DoorMalfunctionEnabled", true, "Enable or disable door malfunction during Average and Strong Solar Flare");
            DoorMalfunctionChance = Config.Bind("SolarFlare", "DoorMalfunctionChance", 0.5f, new ConfigDescription("Chance of metal doors opening/closing by themselves during Solar Flare. 0.1 is 10% chance, 0.5 is 50% chance, 1.0 is 100% chance. Low chance might cause you to get soft locked behind a door in the facility!", new AcceptableValueRange<float>(0, 1f)));
            NoiseStaticLevel = Config.Bind("SolarFlare", "NoiseStaticLevel", 0.001f, new ConfigDescription("Level of static noise from the walkie talkie during Solar Flare. This is signal amplitude, the actual volume in dB will follow a logarithmic scale. For example the volume for value 0.1 relative to 0.2 is not reduced by 100%, it's actually by ~log10(0.2/0.1) %", new AcceptableValueRange<float>(0, 1f)));
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
