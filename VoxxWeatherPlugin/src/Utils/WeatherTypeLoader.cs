using UnityEngine;
using System.Linq;
using UnityEngine.VFX;
using WeatherRegistry;
using VoxxWeatherPlugin.Weathers;
using VoxxWeatherPlugin.Behaviours;
using UnityEngine.Rendering;

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
            heatwaveContainer.hideFlags = HideFlags.HideAndDontSave;
            GameObject.DontDestroyOnLoad(heatwaveContainer);

            HeatwaveWeather heatwaveWeatherController = heatwaveContainer.GetComponentInChildren<HeatwaveWeather>(true);
            GameObject effectPermanentObject = heatwaveWeatherController.gameObject;
            effectPermanentObject.SetActive(false);

            HeatwaveVFXManager heatwaveVFXManager = heatwaveContainer.GetComponentInChildren<HeatwaveVFXManager>(true);
            //Possibly setup vfx configuration here
            GameObject effectObject = heatwaveVFXManager.gameObject;
            effectObject.SetActive(false);

            heatwaveWeatherController.VFXManager = heatwaveVFXManager;

            // Fix broken references (WHY, UNITY, WHY)

            VisualEffectAsset? heatwaveVFXAsset = WeatherAssetLoader.LoadAsset<VisualEffectAsset>(bundleName, "HeatwaveVFX");

            if (heatwaveVFXAsset == null)
            {
                Debug.LogError("Failed to load Heatwave Weather visual assets. Weather registration failed.");
                return;
            }

            VisualEffect heatwaveVFX = heatwaveVFXManager.heatwaveParticlePrefab!.GetComponent<VisualEffect>();
            heatwaveVFX.visualEffectAsset = heatwaveVFXAsset;

            // Configure VFX settings
            heatwaveVFX.SetFloat("particleSpawnRate", Configuration.HeatwaveParticlesSpawnRate.Value);
            heatwaveVFX.SetFloat("distortionScale", Configuration.HeathazeDistortionStrength.Value);

            heatwaveContainer.SetActive(true);

            ImprovedWeatherEffect heatwaveEffect = new(effectObject, effectPermanentObject) {
                SunAnimatorBool = "",
                };

            Weather HeatwaveWeather = new Weather(heatwaveWeatherController.WeatherName, heatwaveEffect)
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
            flareContainer.hideFlags = HideFlags.HideAndDontSave;
            GameObject.DontDestroyOnLoad(flareContainer);

            SolarFlareWeather flareWeatherController = flareContainer.GetComponentInChildren<SolarFlareWeather>(true);
            GameObject effectPermanentObject = flareWeatherController.gameObject;
            effectPermanentObject.SetActive(false);

            SolarFlareVFXManager flareVFXManager = flareContainer.GetComponentInChildren<SolarFlareVFXManager>(true);
            GameObject effectObject = flareVFXManager.gameObject;
            effectObject.SetActive(false);

            flareWeatherController.VFXManager = flareVFXManager;

            // Fix broken references (WHY, UNITY, WHY)

            VisualEffectAsset? flareVFXAsset = WeatherAssetLoader.LoadAsset<VisualEffectAsset>(bundleName, "CoronaVFX");
            VisualEffectAsset? auroraVFXAsset = WeatherAssetLoader.LoadAsset<VisualEffectAsset>(bundleName, "AuroraVFX");
            if (flareVFXAsset == null || auroraVFXAsset == null)
            {
                Debug.LogError("Failed to load Solar Flare Weather visual assets. Weather registration failed.");
                return;
            }

            flareVFXManager.flareObject!.GetComponent<VisualEffect>().visualEffectAsset = flareVFXAsset;

            VisualEffect auroraVFX = flareVFXManager.auroraObject!.GetComponent<VisualEffect>();
            auroraVFX.visualEffectAsset = auroraVFXAsset;

            // Configure VFX settings

            auroraVFX.SetUInt("spawnHeight", Configuration.AuroraHeight.Value);
            auroraVFX.SetFloat("spawnBoxSize", Configuration.AuroraSpawnAreaBox.Value);
            auroraVFX.SetFloat("auroraSize", Configuration.AuroraSize.Value);
            auroraVFX.SetFloat("particleSpawnRate", Configuration.AuroraSpawnRate.Value);

            flareContainer.SetActive(true);

            ImprovedWeatherEffect flareWeatherEffect = new(effectObject, effectPermanentObject)
            {
                SunAnimatorBool = "",
            };

            Weather FlareWeather = new Weather(flareWeatherController.WeatherName, flareWeatherEffect)
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
            blizzardContainer.hideFlags = HideFlags.HideAndDontSave;
            GameObject.DontDestroyOnLoad(blizzardContainer);

            BlizzardWeather blizzardWeatherController = blizzardContainer.GetComponentInChildren<BlizzardWeather>(true);
            BlizzardWeather.Instance = blizzardWeatherController;
            GameObject effectPermanentObject = blizzardWeatherController.gameObject;
            effectPermanentObject.SetActive(false);

            BlizzardVFXManager blizzardVFXManager = blizzardContainer.GetComponentInChildren<BlizzardVFXManager>(true);
            //Possibly setup vfx configuration here
            GameObject effectObject = blizzardVFXManager.gameObject;
            effectObject.SetActive(false);

            blizzardWeatherController.VFXManager = blizzardVFXManager;

            // Fix broken references (WHY, UNITY, WHY)

            VisualEffectAsset? blizzardVFXAsset = Configuration.snowVFXLighting.Value ?
                WeatherAssetLoader.LoadAsset<VisualEffectAsset>(bundleName, "BlizzardVFXLit") :
                WeatherAssetLoader.LoadAsset<VisualEffectAsset>(bundleName, "BlizzardVFX");
            VisualEffectAsset? blizzardWaveVFXAsset = Configuration.blizzardWaveVFXLighting.Value ?
                WeatherAssetLoader.LoadAsset<VisualEffectAsset>(bundleName, "BlizzardWaveVFXLit") :
                WeatherAssetLoader.LoadAsset<VisualEffectAsset>(bundleName, "BlizzardWaveVFX");

            if (blizzardVFXAsset == null || blizzardWaveVFXAsset == null)
            {
                Debug.LogError("Failed to load Blizzard Weather visual assets. Weather registration failed.");
                return;
            }

            VisualEffect blizzardVFX = blizzardVFXManager.snowVFXContainer!.GetComponent<VisualEffect>();
            blizzardVFX.visualEffectAsset = blizzardVFXAsset;
            blizzardVFX.SetFloat("spawnRateMultiplier", Configuration.snowParticlesMultiplier.Value);
            blizzardVFX.SetBool("isCollisionEnabled", Configuration.enableVFXCollisions.Value);
            blizzardVFX.SetBool("fogEnabled", Configuration.enableBlizzardFog.Value);
            Camera blizzardCamera = blizzardVFX.GetComponentInChildren<Camera>(true);
            blizzardCamera.enabled = Configuration.enableVFXCollisions.Value;
            VisualEffect chillWaveVFX = blizzardVFXManager.blizzardWaveContainer!.GetComponentInChildren<VisualEffect>(true);
            chillWaveVFX.visualEffectAsset = blizzardWaveVFXAsset;
            chillWaveVFX.SetFloat("spawnRateMultiplier", Configuration.blizzardWaveParticlesMultiplier.Value);
            chillWaveVFX.SetBool("isCollisionEnabled", Configuration.enableVFXCollisions.Value);
            chillWaveVFX.SetBool("fogEnabled", Configuration.enableBlizzardFog.Value);
            Camera chillWaveCamera = blizzardVFXManager.blizzardWaveContainer!.GetComponentInChildren<Camera>(true);
            chillWaveCamera.enabled = Configuration.enableVFXCollisions.Value;
            AudioSource blizzardAudio = blizzardVFXManager.GetComponent<AudioSource>();
            blizzardAudio.volume = Configuration.blizzardAmbientVolume.Value;
            AudioSource waveAudio = blizzardVFXManager.blizzardWaveContainer.GetComponentInChildren<AudioSource>(true);
            waveAudio.volume = Configuration.blizzardWaveVolume.Value;

            blizzardContainer.SetActive(true);

            ImprovedWeatherEffect blizzardWeatherEffect = new(effectObject, effectPermanentObject)
            {
                SunAnimatorBool = "overcast",
            };

            Weather BlizzardWeatherType = new Weather(blizzardWeatherController.WeatherName, blizzardWeatherEffect)
            {
                DefaultLevelFilters = ["Gordion", "Experimentation", "Assurance", "Offense", "Embrion",
                                            "EGypt", "Penumbra", "EchoReach", "Infernis", "Atlantica",
                                            "Gloom", "Orion", "Vertigo", "RelayStation", "Vaporization",
                                            "Praetor", "Lithium", "Arcadia", "Sector", "Ichor", "AtlasAbyss",
                                            "Asteroid13", "Asteroid14", "Fray", "Desolation", "Cosmocos",
                                            "Junic", "Detritus", "CaltPrime", "Vow", "Makron",
                                            "Attenuation", "Argent", "Humidity", "Sierra", "Black Mesa"],
                LevelFilteringOption = FilteringOption.Exclude,
                Color = Color.cyan,
                ScrapAmountMultiplier = 1.4f,
                ScrapValueMultiplier = 0.9f,
                DefaultWeight = 75
            };

            WeatherManager.RegisterWeather(BlizzardWeatherType);
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
            snowfallContainer.hideFlags = HideFlags.HideAndDontSave;
            GameObject.DontDestroyOnLoad(snowfallContainer);

            SnowfallWeather snowfallWeatherController = snowfallContainer.GetComponentInChildren<SnowfallWeather>(true);
            SnowfallWeather.Instance = snowfallWeatherController;
            GameObject effectPermanentObject = snowfallWeatherController.gameObject;
            effectPermanentObject.SetActive(false);

            SnowfallVFXManager snowfallVFXManager = snowfallContainer.GetComponentInChildren<SnowfallVFXManager>(true);
            
            GameObject effectObject = snowfallVFXManager.gameObject;
            effectObject.SetActive(false);

            snowfallWeatherController.VFXManager = snowfallVFXManager;
            //Create a dictionary of the snowfall VFX variants                                                                        
            string[] keys = new[] {"footprintsTrackerVFX", "lowcapFootprintsTrackerVFX", "itemTrackerVFX", "shovelVFX" };
            SnowfallVFXManager.snowTrackersDict = keys.Zip(snowfallVFXManager.footprintsTrackerVFX,
                                                            (k, v) => new { k, v })
                                                            .ToDictionary(x => x.k, x => x.v);         

            VisualEffectAsset? snowVFXAsset = Configuration.snowVFXLighting.Value ?
                WeatherAssetLoader.LoadAsset<VisualEffectAsset>(bundleName, "SnowVFXLit") :
                WeatherAssetLoader.LoadAsset<VisualEffectAsset>(bundleName, "SnowVFX");

            if (snowVFXAsset == null)
            {
                Debug.LogError("Failed to load Snowfall Weather visual assets. Weather registration failed.");
                return;
            }

            
            VisualEffect snowVFX = snowfallVFXManager.snowVFXContainer!.GetComponent<VisualEffect>();
            snowVFX.visualEffectAsset = snowVFXAsset;
            snowVFX.SetFloat("spawnRateMultiplier", Configuration.snowParticlesMultiplier.Value);

            snowfallContainer.SetActive(true);

            ImprovedWeatherEffect snowyWeatherEffect = new(effectObject, effectPermanentObject)
            {
                SunAnimatorBool = "overcast",
            };

            Weather SnowfallWeatherEffect = new Weather(snowfallWeatherController.WeatherName, snowyWeatherEffect)
            {
                DefaultLevelFilters = ["Gordion", "Assurance", "Embrion", "Sierra",
                                        "EGypt", "Penumbra", "EchoReach", "Infernis", "Atlantica",
                                        "Gloom", "Orion", "Vertigo", "RelayStation", "Vaporization",
                                        "Praetor", "Lithium", "Arcadia", "Sector", "Ichor", "AtlasAbyss",
                                        "Asteroid13", "Asteroid14", "Fray", "Desolation", "Cosmocos",
                                        "Junic", "Detritus", "CaltPrime", "Submersion", "Maritopia",
                                        "Cambrian", "Halation", "Black Mesa", "Baykal"],
                LevelFilteringOption = FilteringOption.Exclude,
                Color = Color.blue,
                ScrapAmountMultiplier = 1.5f,
                ScrapValueMultiplier = 0.75f,
                DefaultWeight = 100
            };

            WeatherManager.RegisterWeather(SnowfallWeatherEffect);
            Debug.Log($"{PluginInfo.PLUGIN_GUID}: Snowfall weather registered!");

        }

        public static bool LoadLevelManipulator()
        {
            GameObject? levelManipulatorPrefab = WeatherAssetLoader.LoadAsset<GameObject>(bundleName, "LevelManipulatorContainer");
            if (levelManipulatorPrefab == null)
            {
                Debug.LogError("Failed to load Level Manipulator assets. Disabling weather effects.");
                return false;
            }

            levelManipulatorPrefab.SetActive(true);
            GameObject levelManipulator = GameObject.Instantiate(levelManipulatorPrefab);
            GameObject.DontDestroyOnLoad(levelManipulator);
            levelManipulator.hideFlags = HideFlags.HideAndDontSave;

            LevelManipulator levelManipulatorController = levelManipulator.GetComponent<LevelManipulator>();
            
            // Assign the snow effects to the PlayerEffectsManager
            foreach (Transform child in levelManipulatorController.snowVolume.transform.parent)
            {
                if (child.name == "FrostbiteFilter")
                {
                    PlayerEffectsManager.freezeEffectVolume = child.gameObject.GetComponent<Volume>();
                    continue;
                }
                
                if (child.name == "UnderSnowFilter")
                {
                    PlayerEffectsManager.underSnowVolume = child.gameObject.GetComponent<Volume>();
                    continue;
                }
            }

            // Fix broken references (WHY, UNITY, WHY)

            Shader? overlayShader = WeatherAssetLoader.LoadAsset<Shader>(bundleName, "SnowLitPass");
            Shader? vertexSnowShader = WeatherAssetLoader.LoadAsset<Shader>(bundleName, "SnowLitVertBakedPass");
            Shader? opaqueVertexSnowShader = WeatherAssetLoader.LoadAsset<Shader>(bundleName, "SnowLitVertBakedOpaquePass");

            if (overlayShader == null || vertexSnowShader == null || opaqueVertexSnowShader == null)
            {
                Debug.LogError("Failed to restore Snow visual assets. Visual effects may not work correctly.");
                return false;
            }

            levelManipulatorController.snowOverlayMaterial!.shader = overlayShader;
            levelManipulatorController.snowVertexMaterial!.shader = vertexSnowShader;
            levelManipulatorController.snowVertexOpaqueMaterial!.shader = opaqueVertexSnowShader;

            levelManipulatorController.snowVertexMaterial.SetFloat(SnowfallShaderIDs.IsDepthFade, Configuration.softSnowEdges.Value ? 1f : 0f);
            
            return true;   
        }

        public static void RegisterToxicSmogWeather()
        {
            GameObject? toxicSmogPrefab = WeatherAssetLoader.LoadAsset<GameObject>(bundleName, "ToxicSmogWeatherContainer");
            if (toxicSmogPrefab == null)
            {
                Debug.LogError("Failed to load Toxic Fog Weather assets. Weather registration failed.");
                return;
            }
            toxicSmogPrefab.SetActive(false);
            GameObject toxicSmogContainer = GameObject.Instantiate(toxicSmogPrefab);
            toxicSmogContainer.hideFlags = HideFlags.HideAndDontSave;
            GameObject.DontDestroyOnLoad(toxicSmogContainer);

            ToxicSmogWeather toxicSmogWeatherController = toxicSmogContainer.GetComponentInChildren<ToxicSmogWeather>(true);
            GameObject effectPermanentObject = toxicSmogWeatherController.gameObject;
            effectPermanentObject.SetActive(false);

            ToxicSmogVFXManager toxicSmogVFXManager = toxicSmogContainer.GetComponentInChildren<ToxicSmogVFXManager>(true);
            //Possibly setup vfx configuration here
            GameObject effectObject = toxicSmogVFXManager.gameObject;
            effectObject.SetActive(false);

            toxicSmogWeatherController.VFXManager = toxicSmogVFXManager;      

            // Fix broken references (WHY, UNITY, WHY)
            VisualEffectAsset? toxicFumesVFXAsset = WeatherAssetLoader.LoadAsset<VisualEffectAsset>(bundleName, "ToxicFumesVFX");

            if (toxicFumesVFXAsset == null)
            {
                Debug.LogError("Failed to load Toxic Fog Weather visual assets. Weather registration failed.");
                return;
            }

            VisualEffect? toxicFumesVFX = toxicSmogVFXManager.hazardPrefab?.GetComponent<VisualEffect>();
            toxicFumesVFX.visualEffectAsset = toxicFumesVFXAsset;

            toxicSmogContainer.SetActive(true);

            ImprovedWeatherEffect toxicWeatherEffect = new(effectObject, effectPermanentObject)
            {
                SunAnimatorBool = "",
            };

            Weather ToxicSmogWeatherEffect = new Weather(toxicSmogWeatherController.WeatherName, toxicWeatherEffect)
            {
                DefaultLevelFilters = ["Gordion", "Derelict"],
                LevelFilteringOption = FilteringOption.Exclude,
                Color = new Color(0.413f, 0.589f, 0.210f), // dark lime green
                ScrapAmountMultiplier = 1.3f,
                ScrapValueMultiplier = 0.7f,
                DefaultWeight = 100
            };

            WeatherManager.RegisterWeather(ToxicSmogWeatherEffect);
            Debug.Log($"{PluginInfo.PLUGIN_GUID}: Toxic Smog weather registered!");

        }
    }
}