using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Newtonsoft.Json;

namespace VoxxWeatherPlugin.Utils
{
    public static class Configuration
    {
        public static ConfigFile Config { get; private set; }
        
        // Config entries
        #region Weather
        public static ConfigEntry<bool> EnableHeatwaveWeather; //
        public static ConfigEntry<bool> EnableSolarFlareWeather; //
        public static ConfigEntry<bool> EnableSnowfallWeather; //
        public static ConfigEntry<bool> EnableBlizzardWeather; //
        #endregion
        
        #region Heatwave
        public static ConfigEntry<float> HeatwaveParticlesSpawnRate; //
        public static ConfigEntry<float> TimeUntilStrokeMin; //
        public static ConfigEntry<float> TimeUntilStrokeMax; //
        public static ConfigEntry<float> HeathazeDistortionStrength; //
        public static ConfigEntry<float> HeathazeFilterMultiplier; //
        #endregion

        #region Solar Flare
        public static ConfigEntry<uint> AuroraHeight; //
        public static ConfigEntry<float> AuroraSpawnAreaBox; //
        public static ConfigEntry<float> AuroraVisibilityThreshold; //
        public static ConfigEntry<float> AuroraSpawnRate; //
        public static ConfigEntry<float> AuroraSize; //
        public static ConfigEntry<bool> DistortOnlyVoiceDuringSolarFlare; //
        public static ConfigEntry<float> BatteryDrainMultiplier; //
        public static ConfigEntry<bool> DrainBatteryInFacility; //
        public static ConfigEntry<bool> DoorMalfunctionEnabled; //
        public static ConfigEntry<float> DoorMalfunctionChance; //
        public static ConfigEntry<float> NoiseStaticLevel; //
        #endregion

        #region Snowfall
        public static ConfigEntry<float>  minSnowHeight; //
        public static ConfigEntry<float>  maxSnowHeight; //
        public static ConfigEntry<float>  minTimeToFullSnow; //
        public static ConfigEntry<float>  maxTimeToFullSnow; //
        public static ConfigEntry<bool>   freezeWater; //
        public static ConfigEntry<float>  underSnowFilterMultiplier; //
        public static ConfigEntry<float>  frostbiteFilterMultiplier; //
        public static ConfigEntry<int>  frostbiteDamage; //
        public static ConfigEntry<float>  frostbiteDamageInterval; //
        public static ConfigEntry<float>  timeToWarmUp; //
        public static ConfigEntry<bool>  enableEasterEgg; //
        #endregion

        #region Blizzard
        public static ConfigEntry<float>  minTimeUntilFrostbite; //
        public static ConfigEntry<float>  maxTimeUntilFrostbite; //
        public static ConfigEntry<float>  minWindForce; //
        public static ConfigEntry<float>  maxWindForce; //
        public static ConfigEntry<float>  minWaveInterval; //
        public static ConfigEntry<float>  maxWaveInterval; //
        public static ConfigEntry<int>    minWaveCount; //
        public static ConfigEntry<int>    maxWaveCount; //
        public static ConfigEntry<int>  chillingWaveDamage; //
        #endregion

        #region Snow & Blizzard Graphics
        public static ConfigEntry<bool> useOpaqueSnowMaterial; //
        // public static ConfigEntry<bool> fixPosterizationForSnowOverlay; //
        public static ConfigEntry<bool> addFootprints; //
        public static ConfigEntry<int> trackedEntityNumber; //
        public static ConfigEntry<int>  depthBufferResolution; //
        public static ConfigEntry<int>  trackerMapResolution; //
        public static ConfigEntry<int>  snowDepthMapResolution; //
        public static ConfigEntry<bool>  bakeSnowDepthMipmaps; //
        public static ConfigEntry<int>  PCFKernelSize; //
        public static ConfigEntry<int>  BlurKernelSize; //
        public static ConfigEntry<int>  minTesselationFactor; //
        public static ConfigEntry<int>  maxTesselationFactor; //
        public static ConfigEntry<bool>  adaptiveTesselation; //
        public static ConfigEntry<bool>  softSnowEdges;
        public static ConfigEntry<bool>  enableSnowTracks;
        public static ConfigEntry<bool>  enableVFXCollisions;
        #endregion

        #region Snow & Blizzard Mesh and Terrain Processing
        public static ConfigEntry<bool> subdivideMesh; //
        public static ConfigEntry<bool> smoothMesh; //
        public static ConfigEntry<bool> useLevelBounds; //
        // TerraMesh related
        public static ConfigEntry<bool> refineMesh; //
        public static ConfigEntry<bool> carveHoles; //
        public static ConfigEntry<bool> useMeshCollider; //
        public static ConfigEntry<int> targetVertexCount; //
        public static ConfigEntry<int> minMeshStep; //
        public static ConfigEntry<int> maxMeshStep; //
        public static ConfigEntry<float> falloffRatio; //
        #endregion
        
        internal static void Initialize(BepInPlugin metadata)
        {
            string configRoot = Paths.ConfigPath; 
			Config = new ConfigFile(Utility.CombinePaths(configRoot, metadata.GUID + ".cfg"), false, metadata);
            
            #region Weather
            EnableHeatwaveWeather = Config.Bind("Weather", "EnableHeatwaveWeather", true, "Enable or disable Heatwave weather");
            EnableSolarFlareWeather = Config.Bind("Weather", "EnableSolarFlareWeather", true, "Enable or disable Solar Flare weather");
            EnableSnowfallWeather = Config.Bind("Weather", "EnableSnowfallWeather", true, "Enable or disable Snowfall weather");
            EnableBlizzardWeather = Config.Bind("Weather", "EnableBlizzardWeather", true, "Enable or disable Blizzard weather");
            #endregion
            
            #region Heatwave
            HeatwaveParticlesSpawnRate = Config.Bind("Heatwave",
                                                    "ParticlesSpawnRate",
                                                    20f,
                                                    new ConfigDescription("Spawn rate of Heatwave particles. Particles per second",
                                                                            new AcceptableValueRange<float>(0, 42)));
            TimeUntilStrokeMin = Config.Bind("Heatwave",
                                            "TimeUntilStrokeMin",
                                            40f,
                                            new ConfigDescription("Minimal time in seconds until heatstroke (min)",
                                                                    new AcceptableValueRange<float>(1, 9999f)));
            TimeUntilStrokeMax = Config.Bind("Heatwave",
                                            "TimeUntilStrokeMax",
                                            80f,
                                            new ConfigDescription("Maximal time in seconds until heatstroke (max). Must be higher than min! Actual time is random between min and max",
                                                                    new AcceptableValueRange<float>(1, 9999f)));
            
            HeathazeDistortionStrength = Config.Bind("Heatwave",
                                                    "HeathazeDistortionStrength",
                                                    8f,
                                                    new ConfigDescription("Strength of the heat haze distortion effect. Higher values make the distortion more intense",
                                                                            new AcceptableValueRange<float>(0, 99f)));
            HeathazeFilterMultiplier = Config.Bind("Heatwave",
                                                    "HeathazeFilterMultiplier",
                                                    1f,
                                                    new ConfigDescription("Multiplier for the heat haze filter. Lower values make the filter less intense. 0 will disable the filter",
                                                                            new AcceptableValueRange<float>(0, 1f)));
            #endregion
            
            #region Solar Flare
            AuroraHeight = Config.Bind("SolarFlare",
                                        "AuroraHeight",
                                        (uint)120,
                                        "Height of the Aurora effect above the ground");
            AuroraSpawnAreaBox = Config.Bind("SolarFlare",
                                                "AuroraSpawnArea",
                                                500f,
                                                "Size of the Aurora spawn area. The Aurora effect will spawn randomly within this square area. VFX may disappear at certain angles if the area is too small or too large.");
            AuroraVisibilityThreshold = Config.Bind("SolarFlare",
                                                    "AuroraVisibilityThreshold",
                                                    9f,
                                                    "Light threshold when Aurora becomes visible (in Lux). Increase to make it more visible.");
            AuroraSpawnRate = Config.Bind("SolarFlare",
                                            "AuroraSpawnRate",
                                            0.1f,
                                            new ConfigDescription("Spawn rate of Aurora effects. Auroras per second",
                                                                    new AcceptableValueRange<float>(0, 32f)));
            AuroraSize = Config.Bind("SolarFlare",
                                    "AuroraSize",
                                    100f,
                                    "Size of the Aurora 'strips' in the sky");
            DistortOnlyVoiceDuringSolarFlare = Config.Bind("SolarFlare",
                                                            "DistortOnlyVoice",
                                                            true,
                                                            "Distort only player voice during Solar Flare (true) or all sounds (false) on a walkie-talkie");
            BatteryDrainMultiplier = Config.Bind("SolarFlare",
                                                "BatteryDrainMultiplier",
                                                1.0f,
                                                new ConfigDescription("Multiplier for additional battery drain during Solar Flare. 1.0 is normal drain, 0.5 is half drain, 2.0 is double drain, 0 no additional drain, etc. Default value is equal to 60 - 200 % faster drain depending on the type of flare.",
                                                                        new AcceptableValueRange<float>(0, 100f)));
            DrainBatteryInFacility = Config.Bind("SolarFlare",
                                                "DrainBatteryInFacility",
                                                false,
                                                "Drain item battery even when inside a facility during Solar Flare");
            DoorMalfunctionEnabled = Config.Bind("SolarFlare",
                                                "DoorMalfunctionEnabled",
                                                true,
                                                "Enable or disable door malfunction during Average and Strong Solar Flare");
            DoorMalfunctionChance = Config.Bind("SolarFlare",
                                                "DoorMalfunctionChance",
                                                0.5f,
                                                new ConfigDescription("Chance of metal doors opening/closing by themselves during Solar Flare. 0.1 is 10% chance, 0.5 is 50% chance, 1.0 is 100% chance. Low chance might cause you to get soft locked behind a door in the facility!",
                                                new AcceptableValueRange<float>(0, 1f)));
            NoiseStaticLevel = Config.Bind("SolarFlare",
                                            "NoiseStaticLevel",
                                            0.001f,
                                            new ConfigDescription("Level of static noise from the walkie talkie during Solar Flare. This is signal amplitude, the actual volume in dB will follow a logarithmic scale. For example the volume for value 0.1 relative to 0.2 is not reduced by 100%, it's actually by ~log10(0.2/0.1) %",
                                                                    new AcceptableValueRange<float>(0, 1f)));
            #endregion

            #region Snowfall
            minSnowHeight = Config.Bind("Snowfall",
                                        "minSnowHeight",
                                        1.7f,
                                        new ConfigDescription("Minimum snow height at the end of the day in meters. For blizzard weather only 60% of this value is used.",
                                                            new AcceptableValueRange<float>(0, 10f)));
            maxSnowHeight = Config.Bind("Snowfall",
                                        "maxSnowHeight",
                                        3f,
                                        new ConfigDescription("Maximum snow height at the end of the day in meters. For blizzard weather only 60% of this value is used. Actual snow height is random between min and max.",
                                                            new AcceptableValueRange<float>(0, 10f)));  

            minTimeToFullSnow = Config.Bind("Snowfall",
                                            "minTimeToFullSnow",
                                            0.5f,
                                            new ConfigDescription("Minimum fraction of the day until snow reaches max height. Actual time is random between min and max. Blizzard weather will only use 20% of this value.",
                                                                new AcceptableValueRange<float>(0, 1f)));
            maxTimeToFullSnow = Config.Bind("Snowfall",
                                            "maxTimeToFullSnow",
                                            0.8f,
                                            new ConfigDescription("Maximum fraction of the day until snow reaches max height. Actual time is random between min and max. Blizzard weather will only use 20% of this value.",
                                                                new AcceptableValueRange<float>(0, 1f)));
            freezeWater = Config.Bind("Snowfall",
                                    "freezeWater",
                                    true,
                                    "Freeze water during snowfall and blizzard weather");
            underSnowFilterMultiplier = Config.Bind("Snowfall",
                                                    "underSnowFilterMultiplier",
                                                    1f,
                                                    new ConfigDescription("Multiplier for the effect visible when player's head is covered in snow. Lower values make the filter less intense. 0 will disable the filter",
                                                                        new AcceptableValueRange<float>(0, 1f)));
            frostbiteFilterMultiplier = Config.Bind("Snowfall",
                                                    "frostbiteFilterMultiplier",
                                                    1f,
                                                    new ConfigDescription("Multiplier for the frostbite filter. Lower values make the filter less intense. 0 will disable the filter",
                                                                        new AcceptableValueRange<float>(0, 1f)));
            frostbiteDamage = Config.Bind("Snowfall",
                                        "frostbiteDamage",
                                        10,
                                        new ConfigDescription("Maximum damage dealt by frostbite effect. When first signs of frostbite occur damage is set to 50% of this value and then grows to 100%.",
                                                            new AcceptableValueRange<int>(0, 99)));
            frostbiteDamageInterval = Config.Bind("Snowfall",
                                                "frostbiteDamageInterval",
                                                20f,
                                                new ConfigDescription("Time in seconds between frostbite damage ticks.",
                                                                    new AcceptableValueRange<float>(0, 9999f)));
            timeToWarmUp = Config.Bind("Snowfall",
                                    "timeToWarmUp",
                                    20f,
                                    new ConfigDescription("Time in seconds to warm up from the MAXIMUM frostbite effect.",
                                                        new AcceptableValueRange<float>(0, 9999f)));
            enableEasterEgg = Config.Bind("Snowfall",
                                        "enableEasterEgg",
                                        true,
                                        "Allow festivities during snowfall weather during special time of the year.");   
            
            #endregion

            #region Blizzard

            minTimeUntilFrostbite = Config.Bind("Blizzard",
                                                "minTimeUntilFrostbite",
                                                40f,
                                                new ConfigDescription("Minimum time in seconds until frostbite reaches full intensity, effect starts to affect the player at 50% of chosen time. Actual time is random between min and max. Snowfall weather will only use 40% of this value as a constant.",
                                                                    new AcceptableValueRange<float>(0, 9999f)));
            maxTimeUntilFrostbite = Config.Bind("Blizzard",
                                                "maxTimeUntilFrostbite",
                                                100f,
                                                new ConfigDescription("Maximum time in seconds until frostbite reaches full intensity, effect starts to affect the player at 50% of chosen time. Actual time is random between min and max.",
                                                                    new AcceptableValueRange<float>(0, 9999f)));
            minWindForce = Config.Bind("Blizzard",
                                        "minWindForce",
                                        0.25f,
                                        new ConfigDescription("Minimum wind force during blizzard weather. Actual wind force is random between min and max.",
                                                            new AcceptableValueRange<float>(0, 1f))); 
            maxWindForce = Config.Bind("Blizzard",
                                        "maxWindForce",
                                        0.6f,
                                        new ConfigDescription("Maximum wind force during blizzard weather. At very high values you might not be able to move while standing in deep snow! Actual wind force is random between min and max.",
                                                            new AcceptableValueRange<float>(0, 1f)));
            minWaveInterval = Config.Bind("Blizzard",
                                        "minWaveInterval",
                                        60f,
                                        new ConfigDescription("Minimum time in seconds between chilling waves of frost. Actual time is random between min and max.",
                                                            new AcceptableValueRange<float>(0, 9999f)));  
            maxWaveInterval = Config.Bind("Blizzard",
                                        "maxWaveInterval",
                                        180f,
                                        new ConfigDescription("Maximum time in seconds between chilling waves of frost. Actual time is random between min and max.",
                                                            new AcceptableValueRange<float>(0, 9999f)));
            minWaveCount = Config.Bind("Blizzard", 
                                        "minWaveCount",
                                        1,
                                        new ConfigDescription("Minimum number of chilling waves of frost that will strike in succession. Actual number is random between min and max.",
                                                            new AcceptableValueRange<int>(0, 99)));
            maxWaveCount = Config.Bind("Blizzard",
                                        "maxWaveCount",
                                        5,
                                        new ConfigDescription("Maximum number of chilling waves of frost that will strike in succession. Actual number is random between min and max.",
                                                            new AcceptableValueRange<int>(0, 99)));
            chillingWaveDamage = Config.Bind("Blizzard",
                                            "chillingWaveDamage",
                                            20,
                                            new ConfigDescription("Damage dealt by each chilling wave of frost if you get caught in one.",
                                                                new AcceptableValueRange<int>(0, 99)));
            #endregion

            #region Snow & Blizzard Graphics
            
            useOpaqueSnowMaterial = Config.Bind("Snow Graphics",
                                                "useOpaqueSnowMaterial",
                                                false,
                                                "Use opaque snow material. Disabling this will use a transparent snow material, which will allow for more realistic snow overlay rendering, but will not work with the posterization effect.");
            // fixPosterizationForSnowOverlay = Config.Bind("Snow Graphics",
            //                                             "fixPosterizationForSnowOverlay",
            //                                             false,
            //                                             "Allows Zeekerss' posterization effect to work with the snow overlay shader (on non-terrain objects). Enabling this will change the rendering path and might cause incompatibilities with other mods that use custom passes.");
            
            addFootprints = Config.Bind("Snow Graphics",
                                        "addFootprints",
                                        false,
                                        "Override level settings and enable vanilla footprints during the weather. Disabling this will use the level settings for footprints.");
            
            trackedEntityNumber = Config.Bind("Snow Graphics",
                                            "trackedEntityNumber",
                                            64,
                                            new ConfigDescription("Number of entities that will be tracked for snow depth, INCLUDING all players. If there are more entities than this number their speed won't be affected by snow. For a better efficiency should be an even number.",
                                                                new AcceptableValueRange<int>(1, 256)));
            depthBufferResolution = Config.Bind("Snow Graphics",
                                                "depthBufferResolution",
                                                2048,
                                                new ConfigDescription("Resolution of the depth buffer used to capture the level and remove snow under objects. Higher values increase quality but also memory usage. MUST be a power of 2 : 512, 1024, 2048, etc.",
                                                                    new AcceptableValueRange<int>(256, 8192)));
            trackerMapResolution = Config.Bind("Snow Graphics",
                                                "trackerMapResolution",
                                                256,
                                                new ConfigDescription("Resolution of the map used to render snow tracks. Higher values increase quality but also memory usage. MUST be a power of 2!",
                                                                    new AcceptableValueRange<int>(32, 1024)));
            snowDepthMapResolution = Config.Bind("Snow Graphics",
                                                "snowDepthMapResolution",
                                                1024,
                                                new ConfigDescription("Resolution of the baked texture used to store snow depth data. Higher values increase quality but also memory usage. MUST be a power of 2!",
                                                                    new AcceptableValueRange<int>(256, 8192)));
            bakeSnowDepthMipmaps = Config.Bind("Snow Graphics",
                                                "bakeSnowDepthMipmaps",
                                                false,
                                                "Generate mipmaps for the snow depth map. Disabling this will reduce memory usage at the cost of quality.");
            PCFKernelSize = Config.Bind("Snow Graphics",
                                        "PCFKernelSize",
                                        12,
                                        new ConfigDescription("Kernel size for Percentage Closer Filtering. Higher values increase will produce smoother snow 'shadows' under objects. High values will baking and thus level loading times.",
                                                            new AcceptableValueRange<int>(1, 50)));
            BlurKernelSize = Config.Bind("Snow Graphics",
                                        "BlurKernelSize",
                                        3,
                                        new ConfigDescription("Kernel size for the depth buffer blur used for VSM 'shadow' mapping, that is used for snow overlay rendering (non fluffy snow). Higher values will produce smoother snow transitions under objects, but will lower accuracy of VFX collisions.",
                                                            new AcceptableValueRange<int>(1, 32)));
            minTesselationFactor = Config.Bind("Snow Graphics",
                                                "minTesselationFactor",
                                                4,
                                                new ConfigDescription("Minimum tesselation factor for snow material. Higher values increase quality but also memory usage. Number represents how many times each triangle is divided, and how detailed snow geometry will appear.",
                                                                    new AcceptableValueRange<int>(1, 32)));
            maxTesselationFactor = Config.Bind("Snow Graphics",
                                                "maxTesselationFactor",
                                                16,
                                                new ConfigDescription("Maximum tesselation factor for snow material. Higher values increase quality but also memory usage. Number represents how many times each triangle is divided.",
                                                                    new AcceptableValueRange<int>(1, 32)));
            adaptiveTesselation = Config.Bind("Snow Graphics",
                                                "adaptiveTesselation",
                                                true,
                                                "Enable adaptive tesselation for snow material. This will apply max tesselation factor only to areas with rapid snow height changes, like around snow tracks, trees or roofs.");
            softSnowEdges = Config.Bind("Snow Graphics",
                                        "softSnowEdges",
                                        false,
                                        "Enable soft snow edges. This will use depth buffer to blend snow with covered objects better. May cause visual artifacts in areas of rapid relief changes.");
            enableSnowTracks = Config.Bind("Snow Graphics",
                                            "enableSnowTracks",
                                            true,
                                            "Enable snow tracks. This will render snow tracks on the ground where player or enemies walk and will affect walking speed. Disabling this will improve performance.");
            enableVFXCollisions = Config.Bind("Snow Graphics",
                                            "Enable VFX Collisions",
                                            true,
                                            "Enable VFX collisions for blizzard wind. This will render an additional depth buffer to make snow particles collide with the terrain and objects. Disabling this will improve performance.");
            #endregion

            #region Mesh & Terrain processing
            subdivideMesh = Config.Bind("Mesh & Terrain Processing",
                                        "subdivideMesh",
                                        true,
                                        "Subdivide EXISTING terrain meshes to create more detailed snow geometry. Disabling this will slightly reduce memory usage but may cause visual artifacts.");
            smoothMesh = Config.Bind("Mesh & Terrain Processing",
                                    "smoothMesh",
                                    true,
                                    "Smooth EXISTING terrain meshes to create a better vertex distribution for tesselation. Disabling this if collision glitches on steep terrain appear.");
            useLevelBounds = Config.Bind("Mesh & Terrain Processing",
                                        "useLevelBounds",
                                        true,
                                        "Use level bounds to limit mesh and terrain processing to the playable area. Disabling this will process the whole level, which will improve the entire levels geometry, but will increase loading times");
            // TerraMesh related
            refineMesh = Config.Bind("Mesh & Terrain Processing",
                                    "refineMesh",
                                    true,
                                    "TerraMesh. Refine the mesh produced FROM TERRAIN to remove possible thin triangles. Disabling this may slightly speed up loading, but will cause degraded mesh quality.");
            carveHoles = Config.Bind("Mesh & Terrain Processing",
                                    "carveHoles",
                                    true,
                                    "TerraMesh. Copy holes from terrain onto the mesh. Disabling this will cause terrain holes to be filled instead.");
            useMeshCollider = Config.Bind("Mesh & Terrain Processing",
                                        "useMeshCollider",
                                        false,
                                        "TerraMesh. Use mesh collider for the produced mesh, this will also copy trees from terrain. Disabling this will use the default terrain collider.");
            targetVertexCount = Config.Bind("Mesh & Terrain Processing",
                                            "targetVertexCount",
                                            -1,
                                            new ConfigDescription("TerraMesh. Target vertex count for the produced mesh within specified bounds. -1 means minMeshStep is used to determine quality of the mesh, at values > 0 minMeshStep will be recalculated, so that the number of vertices could match this target value.",
                                                                new AcceptableValueRange<int>(-1, 500000)));
            minMeshStep = Config.Bind("Mesh & Terrain Processing",
                                    "minMeshStep",
                                    1,
                                    new ConfigDescription("TerraMesh. Minimum step size for the produced mesh. Higher values increase speed but reduce quality. 1 is the highest quality and will copy terrain exactly. 2 will skip every second vertex, etc.",
                                                        new AcceptableValueRange<int>(1, 128)));
            maxMeshStep = Config.Bind("Mesh & Terrain Processing",
                                    "maxMeshStep",
                                    32,
                                    new ConfigDescription("TerraMesh. Maximum step size for the produced mesh. Outside of calculated playable level bounds the step size will be gradually increased to this value. Step size will rounded up to the nearest power of 2.",
                                                        new AcceptableValueRange<int>(1, 128)));
            falloffRatio = Config.Bind("Mesh & Terrain Processing",
                                    "falloffRatio",
                                    3f,
                                    new ConfigDescription("TerraMesh. How fast the step size increases outside of calculated playable level bounds. Higher values increase speed but reduce quality. 1 is linear falloff, 2 is quadratic, 3 is cubic, etc.",
                                                        new AcceptableValueRange<float>(0, 16f)));
            #endregion
        }
    }
}