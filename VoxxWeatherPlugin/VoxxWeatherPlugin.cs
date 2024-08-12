using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using WeatherRegistry;
using UnityEngine.Rendering;
using VoxxWeatherPlugin.Weathers;
using VoxxWeatherPlugin.Patches;
using System.Reflection;
using UnityEngine.VFX;
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
            
            //NetcodePatcher();

            //InitializeConfig();
            
            // harmony = new Harmony(PluginInfo.PLUGIN_GUID);

            // if (VoxxWeatherPlugin.EnableSolarFlareWeather.Value)    
            // {
            //     WeatherTypeLoader.RegisterFlareWeather();
            //     harmony.PatchAll(typeof(FlarePatches));
            //     if (!VoxxWeatherPlugin.DistortOnlyVoiceDuringSolarFlare.Value)
            //     {
            //         harmony.PatchAll(typeof(FlareOptionalWalkiePatches));
            //         Logger.LogInfo($"{PluginInfo.PLUGIN_GUID} optional solar flare patches successfully applied!");
            //     }
            //     Logger.LogInfo($"{PluginInfo.PLUGIN_GUID} solar flare patches successfully applied!");
            // }

            // if (VoxxWeatherPlugin.EnableHeatwaveWeather.Value)
            // {
            //     WeatherTypeLoader.RegisterHeatwaveWeather();
            //     harmony.PatchAll(typeof(HeatwavePatches));
            //     Logger.LogInfo($"{PluginInfo.PLUGIN_GUID} heatwave patches successfully applied!");
            // }

            WeatherTypeLoader.RegisterBlizzardWeather();
            WeatherTypeLoader.RegisterSnowfallWeather();

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

    public class WeatherAssetLoader
    {
        private static readonly Dictionary<string, AssetBundle> loadedBundles = new Dictionary<string, AssetBundle>();

        public static T LoadAsset<T>(string bundleName, string assetName) where T : UnityEngine.Object
        {
            AssetBundle bundle = LoadBundle(bundleName);
            if (bundle == null)
            {
                return null;
            }

            return bundle.LoadAsset<T>(assetName);
        }

        private static AssetBundle LoadBundle(string bundleName)
        {
            if (loadedBundles.ContainsKey(bundleName))
            {
                return loadedBundles[bundleName];
            }

            string dllPath = Assembly.GetExecutingAssembly().Location;
            string dllDirectory = System.IO.Path.GetDirectoryName(dllPath);
            string bundlePath = System.IO.Path.Combine(dllDirectory, bundleName);
            AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);

            if (bundle != null)
            {
                loadedBundles.Add(bundleName, bundle);
            }
            else
            {
                Debug.LogError($"Failed to load AssetBundle: {bundleName}");
            }

            return bundle;
        }

        public static void UnloadAllBundles()
        {
            foreach (var bundle in loadedBundles.Values)
            {
                bundle.Unload(true); // Unload assets as well
            }
            loadedBundles.Clear();
        }

        private void OnDisable()
        {
            UnloadAllBundles();
            Debug.Log("Unloaded assetbundles.");
        }
    }

    public class WeatherTypeLoader
    {
        internal static string bundleName = "voxxweather.assetbundle";
        
        public static void RegisterHeatwaveWeather()
        {
            GameObject vfxPrefab = WeatherAssetLoader.LoadAsset<GameObject>(bundleName, "HeatwaveParticlePrefab");
            VolumeProfile volumeProfile = WeatherAssetLoader.LoadAsset<VolumeProfile>(bundleName, "HeatExhaustionFilter");

            if (vfxPrefab == null || volumeProfile == null)
            {
                Debug.LogError("Failed to load Heatwave Weather assets. Weather registration failed.");
                return;
            }
            GameObject heatwaveVFX = new GameObject("HeatwaveVFX");
            heatwaveVFX.SetActive(false);
            GameObject effectObject = GameObject.Instantiate(heatwaveVFX);
            HeatwaveVFXManager VFXmanager = effectObject.AddComponent<HeatwaveVFXManager>();
            
            VisualEffect vfx = vfxPrefab.GetComponent<VisualEffect>();
            vfx.SetUInt("particleSpawnRate", VoxxWeatherPlugin.HeatwaveParticlesSpawnRate.Value);
            VFXmanager.heatwaveParticlePrefab = vfxPrefab;
            effectObject.hideFlags = HideFlags.HideAndDontSave;
            GameObject.DontDestroyOnLoad(effectObject);

            GameObject heatwaveZone = new GameObject("HeatwaveZone");
            heatwaveZone.SetActive(false);
            GameObject effectPermanentObject = GameObject.Instantiate(heatwaveZone);
            HeatwaveWeather heatwaveWeather = effectPermanentObject.AddComponent<HeatwaveWeather>();
            heatwaveWeather.heatwaveFilter = volumeProfile;
            heatwaveWeather.heatwaveVFXManager = VFXmanager;
            effectPermanentObject.hideFlags = HideFlags.HideAndDontSave;
            GameObject.DontDestroyOnLoad(effectPermanentObject);

            ImprovedWeatherEffect heatwaveEffect = new(effectObject, effectPermanentObject) {
                SunAnimatorBool = "",
                };

            Weather HeatwaveWeather = new Weather("Heatwave", heatwaveEffect)
            {
                DefaultLevelFilters = new[] {"Experimentation", "Assurance", "Offense", "Embrion", "Artifice",
                                            "EGypt", "Aquatis", "Affliction", "Penumbra", "EchoReach", "Harloth",
                                            "Celestria", "Derelict", "Infernis", "Etern", "Atlantica", "Junic",
                                            "Fission", "Mantif", "Sierra", "Cambrian", "Orion", "Vertigo",
                                            "Collateral", "Devastation", "RelayStation"},
                LevelFilteringOption = FilteringOption.Include,
                Color = new Color(1f, 0.5f, 0f),
                ScrapAmountMultiplier = 1.2f,
                ScrapValueMultiplier = 0.9f,
                DefaultWeight = 100
            };

            WeatherManager.RegisterWeather(HeatwaveWeather);
            Debug.Log($"{PluginInfo.PLUGIN_GUID}: Heatwave weather registered!");
        }

        public static void RegisterFlareWeather()
        {
            VisualEffectAsset auroraVFX = WeatherAssetLoader.LoadAsset<VisualEffectAsset>(bundleName, "AuroraVFX");
            VisualEffectAsset coronaVFX = WeatherAssetLoader.LoadAsset<VisualEffectAsset>(bundleName, "CoronaVFX");
            Material glitchPassMaterial = WeatherAssetLoader.LoadAsset<Material>(bundleName, "GlitchPassMaterial");

            if (auroraVFX == null || coronaVFX == null || glitchPassMaterial == null)
            {
                Debug.LogError("Failed to load Solar Flare Weather assets. Weather registration failed.");
                return;
            }

            GameObject flareVFXObject = new GameObject("SolarFlareVFX");
            flareVFXObject.SetActive(false);
            GameObject effectObject = GameObject.Instantiate(flareVFXObject);

            GameObject auroraVFXObject = new GameObject("AuroraVFX");
            auroraVFXObject.SetActive(false);
            VisualEffect loadedVFX = auroraVFXObject.AddComponent<VisualEffect>();
            loadedVFX.visualEffectAsset = auroraVFX;
            loadedVFX.SetUInt("spawnHeight", VoxxWeatherPlugin.AuroraHeight.Value);
            loadedVFX.SetFloat("spawnBoxSize", VoxxWeatherPlugin.AuroraSpawnAreaBox.Value);
            loadedVFX.SetFloat("auroraSize", VoxxWeatherPlugin.AuroraSize.Value);
            loadedVFX.SetFloat("particleSpawnRate", VoxxWeatherPlugin.AuroraSpawnRate.Value);
            GameObject.DontDestroyOnLoad(auroraVFXObject);
            auroraVFXObject.transform.SetParent(effectObject.transform);

            GameObject coronaVFXObject = new GameObject("CoronaVFX");
            coronaVFXObject.SetActive(false);
            loadedVFX = coronaVFXObject.AddComponent<VisualEffect>();
            loadedVFX.visualEffectAsset = coronaVFX;
            coronaVFXObject.transform.SetParent(effectObject.transform);

            SolarFlareVFXManager VFXmanager = effectObject.AddComponent<SolarFlareVFXManager>();
            VFXmanager.auroraSunThreshold = VoxxWeatherPlugin.AuroraVisibilityThreshold.Value;
            SolarFlareVFXManager.flarePrefab = coronaVFXObject;
            SolarFlareVFXManager.auroraPrefab = auroraVFXObject;
            effectObject.hideFlags = HideFlags.HideAndDontSave;
            GameObject.DontDestroyOnLoad(effectObject);

            GameObject flareEffect = new GameObject("SolarFlareEffect");
            flareEffect.SetActive(false);
            GameObject effectPermanentObject = GameObject.Instantiate(flareEffect);
            SolarFlareWeather _ = effectPermanentObject.AddComponent<SolarFlareWeather>();
            SolarFlareWeather.glitchMaterial = glitchPassMaterial;
            effectPermanentObject.hideFlags = HideFlags.HideAndDontSave;
            GameObject.DontDestroyOnLoad(effectPermanentObject);

            ImprovedWeatherEffect flareWeatherEffect = new(effectObject, effectPermanentObject)
            {
                SunAnimatorBool = "",
            };

            Weather FlareWeather = new Weather("Solar Flare", flareWeatherEffect)
            {
                DefaultLevelFilters = new[] {"Gordion"},
                LevelFilteringOption = FilteringOption.Exclude,
                Color = Color.yellow,
                ScrapAmountMultiplier = 0.95f,
                ScrapValueMultiplier = 1.25f,
                DefaultWeight = 1000
            };

            WeatherManager.RegisterWeather(FlareWeather);
            Debug.Log($"{PluginInfo.PLUGIN_GUID}: Solar flare weather registered!");
        }

        public static void RegisterBlizzardWeather()
        {
            GameObject blizzardVFXObject = new GameObject("BlizzardVFX");
            blizzardVFXObject.SetActive(false);
            GameObject effectObject = GameObject.Instantiate(blizzardVFXObject);
            effectObject.hideFlags = HideFlags.HideAndDontSave;
            GameObject.DontDestroyOnLoad(effectObject);

            GameObject blizzardEffect = new GameObject("BlizzardEffect");
            blizzardEffect.SetActive(false);
            GameObject effectPermanentObject = GameObject.Instantiate(blizzardEffect);
            effectPermanentObject.hideFlags = HideFlags.HideAndDontSave;
            GameObject.DontDestroyOnLoad(effectPermanentObject);

            ImprovedWeatherEffect blizzardWeatherEffect = new(effectObject, effectPermanentObject)
            {
                SunAnimatorBool = "overcast",
            };

            Weather BlizzardWeather = new Weather("Blizzard", blizzardWeatherEffect)
            {
                DefaultLevelFilters = new[] {"Rend", "Dine", "Artifice", "Titan"},
                LevelFilteringOption = FilteringOption.Include,
                Color = Color.cyan,
                ScrapAmountMultiplier = 1.25f,
                ScrapValueMultiplier = 1.2f,
                DefaultWeight = 1000
            };

            WeatherManager.RegisterWeather(BlizzardWeather);
            Debug.Log($"{PluginInfo.PLUGIN_GUID}: Blizzard weather registered!");

        }

        public static void RegisterSnowfallWeather()
        {
            GameObject blizzardVFXObject = new GameObject("SnowfallVFX");
            blizzardVFXObject.SetActive(false);
            GameObject effectObject = GameObject.Instantiate(blizzardVFXObject);
            effectObject.hideFlags = HideFlags.HideAndDontSave;
            GameObject.DontDestroyOnLoad(effectObject);

            GameObject blizzardEffect = new GameObject("SnowfallEffect");
            blizzardEffect.SetActive(false);
            GameObject effectPermanentObject = GameObject.Instantiate(blizzardEffect);
            effectPermanentObject.hideFlags = HideFlags.HideAndDontSave;
            GameObject.DontDestroyOnLoad(effectPermanentObject);

            ImprovedWeatherEffect blizzardWeatherEffect = new(effectObject, effectPermanentObject)
            {
                SunAnimatorBool = "overcast",
            };

            Weather SnowfallWeather = new Weather("Snowfall", blizzardWeatherEffect)
            {
                DefaultLevelFilters = new[] {"Vow", "March", "Adamance"},
                LevelFilteringOption = FilteringOption.Include,
                Color = Color.blue,
                ScrapAmountMultiplier = 1.25f,
                ScrapValueMultiplier = 1.2f,
                DefaultWeight = 1000
            };

            WeatherManager.RegisterWeather(SnowfallWeather);
            Debug.Log($"{PluginInfo.PLUGIN_GUID}: Snowfall weather registered!");

        }
    }


}
