using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using WeatherRegistry;
using UnityEngine.Rendering;
using VoxxWeatherPlugin.Weathers;

namespace VoxxWeatherPlugin
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("mrov.WeatherRegistry", BepInDependency.DependencyFlags.HardDependency)]
    public class VoxxWeatherPlugin : BaseUnityPlugin
    {
        private Harmony harmony;
        public static VoxxWeatherPlugin instance;

        private void Awake()
        {
            instance = this;

            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            //Apply Harmony patch
            this.harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            this.harmony.PatchAll();
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} patched PlayerControllerB!");
            WeatherTypeLoader.RegisterHeatwaveWeather();
            Logger.LogInfo($"{PluginInfo.PLUGIN_GUID}: Heatwave weather registered!");
        }
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

            string dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
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
            GameObject vfxPrefab = WeatherAssetLoader.LoadAsset<GameObject>(bundleName, "HeatwaveParticle");
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

            ImprovedWeatherEffect heatwaveEffect = new(effectObject, effectPermanentObject);

            Weather HeatwaveWeather = new Weather("Heatwave", heatwaveEffect)
            {
                DefaultLevelFilters = new[] {"Experimentation", "Assurance", "Offense"},
                LevelFilteringOption = FilteringOption.Include,
                Color = new Color(1f, 0.5f, 0f),
                ScrapAmountMultiplier = 1.2f,
                ScrapValueMultiplier = 0.9f,
                DefaultWeight = 400
            };

            WeatherRegistry.WeatherManager.RegisterWeather(HeatwaveWeather);
        }
    }
}
