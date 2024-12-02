using UnityEngine;
using System.Linq;
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
            GameObject? heatwavePrefab = WeatherAssetLoader.LoadAsset<GameObject>(bundleName, "HeatwaveWeatherContainer");

            if (heatwavePrefab == null)
            {
                Debug.LogError("Failed to load Heatwave Weather assets. Weather registration failed.");
                return;
            }

            heatwavePrefab.SetActive(false);
            GameObject heatwaveContainer = GameObject.Instantiate(heatwavePrefab);
            //snowfallContainer.hideFlags = HideFlags.HideAndDontSave;
            GameObject.DontDestroyOnLoad(heatwaveContainer);

            HeatwaveWeather heatwaveWeatherController = heatwaveContainer.GetComponentInChildren<HeatwaveWeather>(true);
            GameObject effectPermanentObject = heatwaveWeatherController.gameObject;
            effectPermanentObject.SetActive(false);

            HeatwaveVFXManager heatwaveVFXManager = heatwaveContainer.GetComponentInChildren<HeatwaveVFXManager>(true);
            //Possibly setup vfx configuration here
            GameObject effectObject = heatwaveVFXManager.gameObject;
            effectObject.SetActive(false);

            heatwaveWeatherController.VFXManager = heatwaveVFXManager;

            VisualEffect heatwaveVFX = heatwaveVFXManager.heatwaveParticlePrefab!.GetComponent<VisualEffect>();
            heatwaveVFX.SetFloat("particleSpawnRate", VoxxWeatherPlugin.HeatwaveParticlesSpawnRate.Value);
            // TODO add blurring strength configuration

            heatwaveContainer.SetActive(true);

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
                DefaultWeight = 100,
            };

            WeatherManager.RegisterWeather(HeatwaveWeather);
            Debug.Log($"{PluginInfo.PLUGIN_GUID}: Heatwave weather registered!");
        }

        public static void RegisterFlareWeather()
        {
            GameObject? flareWeatherPrefab = WeatherAssetLoader.LoadAsset<GameObject>(bundleName, "SolarFlareWeatherContainer");
            
            if (flareWeatherPrefab == null)
            {
                Debug.LogError("Failed to load Solar Flare Weather assets. Weather registration failed.");
                return;
            }

            flareWeatherPrefab.SetActive(false);
            GameObject flareContainer = GameObject.Instantiate(flareWeatherPrefab);
            //snowfallContainer.hideFlags = HideFlags.HideAndDontSave;
            GameObject.DontDestroyOnLoad(flareContainer);

            SolarFlareWeather flareWeatherController = flareContainer.GetComponentInChildren<SolarFlareWeather>(true);
            GameObject effectPermanentObject = flareWeatherController.gameObject;
            effectPermanentObject.SetActive(false);

            SolarFlareVFXManager flareVFXManager = flareContainer.GetComponentInChildren<SolarFlareVFXManager>(true);
            GameObject effectObject = flareVFXManager.gameObject;
            effectObject.SetActive(false);

            flareWeatherController.VFXManager = flareVFXManager;

            VisualEffect auroraVFX = flareVFXManager.auroraObject!.GetComponent<VisualEffect>();
            auroraVFX.SetUInt("spawnHeight", VoxxWeatherPlugin.AuroraHeight.Value);
            auroraVFX.SetFloat("spawnBoxSize", VoxxWeatherPlugin.AuroraSpawnAreaBox.Value);
            auroraVFX.SetFloat("auroraSize", VoxxWeatherPlugin.AuroraSize.Value);
            auroraVFX.SetFloat("particleSpawnRate", VoxxWeatherPlugin.AuroraSpawnRate.Value);

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
            GameObject? blizzardPrefab = WeatherAssetLoader.LoadAsset<GameObject>(bundleName, "BlizzardWeatherContainer");

            if (blizzardPrefab == null)
            {
                Debug.LogError("Failed to load Blizzard Weather assets. Weather registration failed.");
                return;
            }

            blizzardPrefab.SetActive(false);
            GameObject blizzardContainer = GameObject.Instantiate(blizzardPrefab);
            //snowfallContainer.hideFlags = HideFlags.HideAndDontSave;
            GameObject.DontDestroyOnLoad(blizzardContainer);

            BlizzardWeather blizzardWeatherController = blizzardContainer.GetComponentInChildren<BlizzardWeather>(true);
            GameObject effectPermanentObject = blizzardWeatherController.gameObject;
            effectPermanentObject.SetActive(false);

            BlizzardVFXManager blizzardVFXManager = blizzardContainer.GetComponentInChildren<BlizzardVFXManager>(true);
            //Possibly setup vfx configuration here
            GameObject effectObject = blizzardVFXManager.gameObject;
            effectObject.SetActive(false);

            blizzardWeatherController.VFXManager = blizzardVFXManager;
            
            // TODO add vfx configs

            blizzardContainer.SetActive(true);

            ImprovedWeatherEffect blizzardWeatherEffect = new(effectObject, effectPermanentObject)
            {
                SunAnimatorBool = "overcast",
            };

            Weather BlizzardWeather = new Weather("Blizzard", blizzardWeatherEffect)
            {
                DefaultLevelFilters = new[] {"Rend", "Dine", "Artifice", "Titan", "March"},
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
            GameObject? snowfallPrefab = WeatherAssetLoader.LoadAsset<GameObject>(bundleName, "SnowWeatherContainer");
            if (snowfallPrefab == null)
            {
                Debug.LogError("Failed to load Snowfall Weather assets. Weather registration failed.");
                return;
            }
            snowfallPrefab.SetActive(false);
            GameObject snowfallContainer = GameObject.Instantiate(snowfallPrefab);
            //snowfallContainer.hideFlags = HideFlags.HideAndDontSave;
            GameObject.DontDestroyOnLoad(snowfallContainer);

            SnowfallWeather snowfallWeatherController = snowfallContainer.GetComponentInChildren<SnowfallWeather>(true);
            GameObject effectPermanentObject = snowfallWeatherController.gameObject;
            effectPermanentObject.SetActive(false);

            SnowfallVFXManager snowfallVFXManager = snowfallContainer.GetComponentInChildren<SnowfallVFXManager>(true);
            //Possibly setup vfx configuration here
            GameObject effectObject = snowfallVFXManager.gameObject;
            effectObject.SetActive(false);

            snowfallWeatherController.VFXManager = snowfallVFXManager;
            //Create a dictionary of the snowfall VFX variants                                                                        
            string[] keys = new[] {"footprintsTrackerVFX", "lowcapFootprintsTrackerVFX", "itemTrackerVFX", "shovelVFX" };
            SnowfallVFXManager.snowTrackersDict = keys.Zip(snowfallVFXManager.footprintsTrackerVFX,
                                                            (k, v) => new { k, v })
                                                            .ToDictionary(x => x.k, x => x.v);
            

            snowfallContainer.SetActive(true);

            ImprovedWeatherEffect snowyWeatherEffect = new(effectObject, effectPermanentObject)
            {
                SunAnimatorBool = "overcast",
            };

            Weather SnowfallWeatherEffect = new Weather("Snowfall", snowyWeatherEffect)
            {
                DefaultLevelFilters = new[] {"Gordion"},
                LevelFilteringOption = FilteringOption.Exclude,
                Color = Color.blue,
                ScrapAmountMultiplier = 1.25f,
                ScrapValueMultiplier = 1.2f,
                DefaultWeight = 1000
            };

            WeatherManager.RegisterWeather(SnowfallWeatherEffect);
            Debug.Log($"{PluginInfo.PLUGIN_GUID}: Snowfall weather registered!");

        }

        // public static void RegisterMeteorWeather()
        // {
        //     GameObject meteorPrefab = WeatherAssetLoader.LoadAsset<GameObject>(bundleName, "MeteorContainer");
        //     meteorPrefab.SetActive(false);

        //     if (meteorPrefab == null)
        //     {
        //         Debug.LogError("Failed to load Meteor Weather assets. Weather registration failed.");
        //     }

        //     GameObject meteorEffect = new GameObject("MeteorEffect");
        //     meteorEffect.SetActive(false);
        //     MeteorWeather meteorWeatherController = meteorEffect.AddComponent<MeteorWeather>();
        //     meteorWeatherController.meteorPrefab = meteorPrefab;
        //     GameObject effectPermanentObject = GameObject.Instantiate(meteorEffect);
        //     GameObject.DontDestroyOnLoad(effectPermanentObject);
        //     effectPermanentObject.hideFlags = HideFlags.HideAndDontSave;

        //     GameObject effectObject = GameObject.Instantiate(new GameObject("MeteorVFX"));
        //     effectObject.SetActive(false);
        //     GameObject.DontDestroyOnLoad(effectObject);
        //     effectObject.hideFlags = HideFlags.HideAndDontSave;

        //     ImprovedWeatherEffect meteorWeatherEffect = new(effectObject, effectPermanentObject)
        //     {
        //         SunAnimatorBool = "eclipse",
        //     };

        //     Weather MeteorWeather = new Weather("MeteorTest", meteorWeatherEffect)
        //     {
        //         DefaultLevelFilters = new[] {"Gordion"},
        //         LevelFilteringOption = FilteringOption.Exclude,
        //         Color = Color.red,
        //         ScrapAmountMultiplier = 1.25f,
        //         ScrapValueMultiplier = 1.2f,
        //         DefaultWeight = 1000
        //     };

        //     WeatherManager.RegisterWeather(MeteorWeather);
        //     Debug.Log($"{PluginInfo.PLUGIN_GUID}: Meteor weather registered!");

        // }
    }
}