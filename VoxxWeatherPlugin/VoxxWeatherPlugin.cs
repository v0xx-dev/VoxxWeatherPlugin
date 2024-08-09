using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using WeatherRegistry;
using UnityEngine.Rendering;
using VoxxWeatherPlugin.Weathers;
using VoxxWeatherPlugin.Utils;
using UnityEngine.Rendering.HighDefinition;
using System;
using UnityEngine.VFX;
using System.Xml.Linq;

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
            Logger.LogInfo($"{PluginInfo.PLUGIN_GUID} patches successfully applied!");
            WeatherTypeLoader.RegisterHeatwaveWeather();
            WeatherTypeLoader.RegisterFlareWeather();
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

            ImprovedWeatherEffect heatwaveEffect = ScriptableObject.CreateInstance<ImprovedWeatherEffect>();
            heatwaveEffect.EffectObject = effectObject;
            heatwaveEffect.WorldObject = effectPermanentObject;
            heatwaveEffect.SunAnimatorBool = "";

            Weather HeatwaveWeather = ScriptableObject.CreateInstance<Weather>();
            HeatwaveWeather.Name = "Heatwave";
            HeatwaveWeather.Effect = heatwaveEffect;
            HeatwaveWeather.DefaultLevelFilters = new[] { "Gordion" };
            HeatwaveWeather.LevelFilteringOption = FilteringOption.Exclude;
            HeatwaveWeather.Color = Color.yellow;
            HeatwaveWeather.ScrapAmountMultiplier = 1.05f;
            HeatwaveWeather.ScrapValueMultiplier = 1.25f;
            HeatwaveWeather.DefaultWeight = 1000;

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
            GameObject.DontDestroyOnLoad(auroraVFXObject);
            auroraVFXObject.transform.SetParent(effectObject.transform);

            GameObject coronaVFXObject = new GameObject("CoronaVFX");
            coronaVFXObject.SetActive(false);
            loadedVFX = coronaVFXObject.AddComponent<VisualEffect>();
            loadedVFX.visualEffectAsset = coronaVFX;
            coronaVFXObject.transform.SetParent(effectObject.transform);

            SolarFlareVFXManager VFXmanager = effectObject.AddComponent<SolarFlareVFXManager>();
            SolarFlareVFXManager.flarePrefab = coronaVFXObject;
            SolarFlareVFXManager.auroraPrefab = auroraVFXObject;
            effectObject.hideFlags = HideFlags.HideAndDontSave;
            GameObject.DontDestroyOnLoad(effectObject);

            GameObject flareEffect = new GameObject("SolarFlareEffect");
            flareEffect.SetActive(false);
            GameObject effectPermanentObject = GameObject.Instantiate(flareEffect);
            SolarFlareWeather flareScript = effectPermanentObject.AddComponent<SolarFlareWeather>();
            SolarFlareWeather.glitchMaterial = glitchPassMaterial;
            effectPermanentObject.hideFlags = HideFlags.HideAndDontSave;
            GameObject.DontDestroyOnLoad(effectPermanentObject);

            ImprovedWeatherEffect flareWeatherEffect = ScriptableObject.CreateInstance<ImprovedWeatherEffect>();
            flareWeatherEffect.EffectObject = effectObject;
            flareWeatherEffect.WorldObject = effectPermanentObject;
            flareWeatherEffect.SunAnimatorBool = "";

            //Weather FlareWeather = new Weather("Solar Flare", flareWeatherEffect)
            Weather FlareWeather = ScriptableObject.CreateInstance<Weather>();
            FlareWeather.Name = "Solar Flare";
            FlareWeather.Effect = flareWeatherEffect;
            FlareWeather.DefaultLevelFilters = new[] { "Gordion" };
            FlareWeather.LevelFilteringOption = FilteringOption.Exclude;
            FlareWeather.Color = Color.yellow;
            FlareWeather.ScrapAmountMultiplier = 1.05f;
            FlareWeather.ScrapValueMultiplier = 1.25f;
            FlareWeather.DefaultWeight = 1000;

            WeatherManager.RegisterWeather(FlareWeather);
            Debug.Log($"{PluginInfo.PLUGIN_GUID}: Solar flare weather registered!");
        }
    }


}
