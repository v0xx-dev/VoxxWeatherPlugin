using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using WeatherRegistry;
using VoxxWeatherPlugin.Weathers;

namespace VoxxWeatherPlugin.Utils
{
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
                DefaultWeight = 100
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