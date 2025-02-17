using BepInEx;
using BepInEx.Configuration;

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
        public static ConfigEntry<bool> EnableToxicSmogWeather; //
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
        public static ConfigEntry<float> NoiseStaticLevel; //
        public static ConfigEntry<bool> DoorMalfunctionEnabled; //
        public static ConfigEntry<bool> RadMechMalfunctionEnabled; //
        public static ConfigEntry<bool> TurretMalfunctionEnabled; //
        public static ConfigEntry<bool> LandmineMalfunctionEnabled; //
        public static ConfigEntry<float> RadMechMalfunctionChance; //
        public static ConfigEntry<float> RadMechReactivationChance; //
        public static ConfigEntry<float> TurretMalfunctionChance; //
        public static ConfigEntry<float> RadMechStunDuration; //
        public static ConfigEntry<float> RadMechReactivateDelay; //
        public static ConfigEntry<float> TurretMalfunctionDelay; //
        public static ConfigEntry<float> DoorMalfunctionChance; //
        public static ConfigEntry<float> LandmineMalfunctionChance; //
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
        public static ConfigEntry<bool>  patchModdedEnemies; //
        public static ConfigEntry<bool>  snowAffectsEnemies; //
        public static ConfigEntry<string>  enemySnowBlacklist; //
        #endregion

        #region Blizzard
        public static ConfigEntry<float>  minSnowHeightBlizzard; //
        public static ConfigEntry<float>  maxSnowHeightBlizzard; //
        public static ConfigEntry<float>  minTimeToFullSnowBlizzard; //
        public static ConfigEntry<float>  maxTimeToFullSnowBlizzard; //
        public static ConfigEntry<float>  minTimeUntilFrostbite; //
        public static ConfigEntry<float>  maxTimeUntilFrostbite; //
        public static ConfigEntry<float>  minWindForce; //
        public static ConfigEntry<float>  maxWindForce; //
        public static ConfigEntry<float>  minWaveInterval; //
        public static ConfigEntry<float>  maxWaveInterval; //
        public static ConfigEntry<int>    minWaveCount; //
        public static ConfigEntry<int>    maxWaveCount; //
        public static ConfigEntry<int>  chillingWaveDamage; //
        public static ConfigEntry<float>  blizzardAmbientVolume; //
        public static ConfigEntry<float>  blizzardWaveVolume; //
        public static ConfigEntry<float>  blizzardFogMeanFreePathMin; //
        public static ConfigEntry<float>  blizzardFogMeanFreePathMax; //
        #endregion

        #region Snow & Blizzard Graphics
        public static ConfigEntry<float> snowParticlesMultiplier; //
        public static ConfigEntry<float> blizzardWaveParticlesMultiplier; //
        public static ConfigEntry<bool> useParticleBlizzardFog; //
        public static ConfigEntry<bool> useVolumetricBlizzardFog; //
        public static ConfigEntry<bool> snowVfxLighting; //
        public static ConfigEntry<bool> blizzardWaveVfxLighting; //
        public static ConfigEntry<bool> useOpaqueSnowMaterial; //
        
        public static ConfigEntry<bool> snowCastsShadows; //
        public static ConfigEntry<bool> addFootprints; //
        public static ConfigEntry<int> trackedEntityNumber; //
        public static ConfigEntry<int>  depthBufferResolution; //
        public static ConfigEntry<int>  trackerMapResolution; //
        public static ConfigEntry<int>  snowDepthMapResolution; //
        public static ConfigEntry<bool>  bakeSnowDepthMipmaps; //
        public static ConfigEntry<int>  PCFKernelSize; //
        public static ConfigEntry<int>  minTesselationFactor; //
        public static ConfigEntry<int>  maxTesselationFactor; //
        public static ConfigEntry<bool>  adaptiveTesselation; //
        public static ConfigEntry<bool>  softSnowEdges; //
        public static ConfigEntry<bool>  enableSnowTracks; //
        public static ConfigEntry<bool>  enableVFXCollisions; //
        #endregion

        #region Snow & Blizzard Mesh and Terrain Processing
        public static ConfigEntry<bool> asyncProcessing; //
        public static ConfigEntry<string>  meshProcessingWhitelist; //
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

        #region Toxic Smog
        public static ConfigEntry<float> ToxicDamageInterval; //
        public static ConfigEntry<int> ToxicDamageAmount; //
        public static ConfigEntry<float> PoisoningRemovalMultiplier; //
        public static ConfigEntry<float> MinFreePath; //
        public static ConfigEntry<float> MaxFreePath; //
        public static ConfigEntry<int> MinFumesAmount; //
        public static ConfigEntry<int> MaxFumesAmount; //
        public static ConfigEntry<float> FactoryAmountMultiplier; //
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
            EnableToxicSmogWeather = Config.Bind("Weather", "EnableToxicSmogWeather", true, "Enable or disable Toxic Smog weather");
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
            NoiseStaticLevel = Config.Bind("SolarFlare",
                                            "NoiseStaticLevel",
                                            0.001f,
                                            new ConfigDescription("Level of static noise from the walkie talkie during Solar Flare. This is signal amplitude, the actual volume in dB will follow a logarithmic scale. For example the volume for value 0.1 relative to 0.2 is not reduced by 100%, it's actually by ~log10(0.2/0.1) %",
                                                                    new AcceptableValueRange<float>(0, 1f)));
            DoorMalfunctionEnabled = Config.Bind("SolarFlare",
                                                "DoorMalfunctionEnabled",
                                                true,
                                                "Enable or disable door malfunction during Mild, Average and Strong Solar Flare");

            RadMechMalfunctionEnabled = Config.Bind("SolarFlare",
                                                "RadMechMalfunctionEnabled",
                                                true,
                                                "Enable or disable robot malfunction during Average and Strong Solar Flare");

            TurretMalfunctionEnabled = Config.Bind("SolarFlare",
                                                "TurretMalfunctionEnabled",
                                                true,
                                                "Enable or disable turret malfunction during all types of Solar Flare");

            LandmineMalfunctionEnabled = Config.Bind("SolarFlare",
                                                "LandmineMalfunctionEnabled",
                                                true,
                                                "Enable or disable landmine malfunction during all types of Solar Flare");

            DoorMalfunctionChance = Config.Bind("SolarFlare",
                                                "DoorMalfunctionChance",
                                                0.5f,
                                                new ConfigDescription("Chance of metal doors opening/closing by themselves during Solar Flare. 0.1 is 10% chance, 0.5 is 50% chance, 1.0 is 100% chance. Low chance might cause you to get soft locked behind a door in the facility! All chances are rolled independently for each door.",
                                                new AcceptableValueRange<float>(0, 1f)));

            RadMechMalfunctionChance = Config.Bind("SolarFlare",
                                                "RadMechMalfunctionChance",
                                                0.4f,
                                                new ConfigDescription("Chance of RadMechs malfunctioning during Solar Flare. 0.1 is 10% chance, 0.5 is 50% chance, 1.0 is 100% chance. All chances are rolled independently for each RadMech.",
                                                                    new AcceptableValueRange<float>(0, 1f)));

            RadMechReactivationChance = Config.Bind("SolarFlare",
                                                "RadMechReactivationChance",
                                                0.25f,
                                                new ConfigDescription("Chance of RadMechs reactivating during Solar Flare. 0.1 is 10% chance, 0.5 is 50% chance, 1.0 is 100% chance. All chances are rolled independently for each RadMech.",
                                                                    new AcceptableValueRange<float>(0, 1f)));

            TurretMalfunctionChance = Config.Bind("SolarFlare",
                                                "TurretMalfunctionChance",
                                                0.3f,
                                                new ConfigDescription("Chance of turrets malfunctioning during Solar Flare. 0.1 is 10% chance, 0.5 is 50% chance, 1.0 is 100% chance. All chances are rolled independently for each turret.",
                                                                    new AcceptableValueRange<float>(0, 1f)));

            RadMechStunDuration = Config.Bind("SolarFlare",
                                                "RadMechStunDuration",
                                                4f,
                                                new ConfigDescription("Time in seconds RadMechs are stunned randomly during Solar Flare.",
                                                                    new AcceptableValueRange<float>(0, 60f)));

            RadMechReactivateDelay = Config.Bind("SolarFlare",
                                                "RadMechReactivateDelay",
                                                5f,
                                                new ConfigDescription("Delay in seconds before RadMechs reactivate",
                                                                    new AcceptableValueRange<float>(0, 60f)));

            TurretMalfunctionDelay = Config.Bind("SolarFlare",
                                                "TurretMalfunctionDelay",
                                                5f,
                                                new ConfigDescription("Delay in seconds before turrets start to malfunction during Solar Flare.",
                                                                    new AcceptableValueRange<float>(0, 60f)));

            LandmineMalfunctionChance = Config.Bind("SolarFlare",
                                                "LandmineMalfunctionChance",
                                                0.3f,
                                                new ConfigDescription("Chance of landmines malfunctioning during Solar Flare. 0.1 is 10% chance, 0.5 is 50% chance, 1.0 is 100% chance. All chances are rolled independently for each landmine.",
                                                                    new AcceptableValueRange<float>(0, 1f)));

            #endregion

            #region Snowfall
            minSnowHeight = Config.Bind("Snowfall",
                                        "minSnowHeight",
                                        1.7f,
                                        new ConfigDescription("Minimum snow height at the end of the day in meters.",
                                                            new AcceptableValueRange<float>(0, 10f)));
            maxSnowHeight = Config.Bind("Snowfall",
                                        "maxSnowHeight",
                                        3f,
                                        new ConfigDescription("Maximum snow height at the end of the day in meters. Actual snow height is random between min and max.",
                                                            new AcceptableValueRange<float>(0, 10f)));  

            minTimeToFullSnow = Config.Bind("Snowfall",
                                            "minTimeToFullSnow",
                                            0.7f,
                                            new ConfigDescription("Minimum fraction of the day until snow reaches max height. Actual time is random between min and max.",
                                                                new AcceptableValueRange<float>(0, 1f)));
            maxTimeToFullSnow = Config.Bind("Snowfall",
                                            "maxTimeToFullSnow",
                                            1f,
                                            new ConfigDescription("Maximum fraction of the day until snow reaches max height. Actual time is random between min and max.",
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
            patchModdedEnemies = Config.Bind("Snowfall",
                                            "patchModdedEnemies",
                                            false,
                                            "Attempt to patch modded enemies to be affected by snowfall weather (only works if they inherit from vanilla EnemyAI class)).");
            
            snowAffectsEnemies = Config.Bind("Snowfall",
                                            "snowAffectsEnemies",
                                            true,
                                            "HOST ONLY! Allow snowfall weather to affect enemies. If disabled enemies will not be slowed down by snow.");
            
            enemySnowBlacklist = Config.Bind("Snowfall",
                                            "enemySpawnBlacklist",
                                            "Docile Locust Bees;Red Locust Bees;Leaf boy;Ogopogo;Vermin",
                                            "List of OUTSIDE enemies that will be blocked from spawning during snowfall AND blizzard. Separate with a semicolon, exact match (including spaces, but not case sensitive).");

            #endregion

            #region Blizzard
            minSnowHeightBlizzard = Config.Bind("Blizzard",
                                                "minSnowHeightBlizzard",
                                                1.0f,
                                                new ConfigDescription("Minimum snow height at the end of the day in meters during blizzard weather. Actual snow height is random between min and max.",
                                                                    new AcceptableValueRange<float>(0, 10f)));
            
            maxSnowHeightBlizzard = Config.Bind("Blizzard",
                                                "maxSnowHeightBlizzard",
                                                1.8f,
                                                new ConfigDescription("Maximum snow height at the end of the day in meters during blizzard weather. Actual snow height is random between min and max.",
                                                                    new AcceptableValueRange<float>(0, 10f)));
            
            minTimeToFullSnowBlizzard = Config.Bind("Blizzard",
                                                    "minTimeToFullSnowBlizzard",
                                                    0.4f,
                                                    new ConfigDescription("Minimum fraction of the day until snow reaches max height during blizzard weather. Actual time is random between min and max.",
                                                                        new AcceptableValueRange<float>(0, 1f)));
            
            maxTimeToFullSnowBlizzard = Config.Bind("Blizzard",
                                                    "maxTimeToFullSnowBlizzard",
                                                    0.7f,
                                                    new ConfigDescription("Maximum fraction of the day until snow reaches max height during blizzard weather. Actual time is random between min and max.",
                                                                        new AcceptableValueRange<float>(0, 1f)));

            
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
                                        0.37f,
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
            
            blizzardAmbientVolume = Config.Bind("Blizzard",
                                                "blizzardAmbientVolume",
                                                1f,
                                                new ConfigDescription("Volume of the blizzard ambient sound. 0 is silent, 1 is full volume.",
                                                                    new AcceptableValueRange<float>(0, 1f)));
            
            blizzardWaveVolume = Config.Bind("Blizzard",
                                            "blizzardWaveVolume",
                                            1f,
                                            new ConfigDescription("Volume of the blizzard wave sound. 0 is silent, 1 is full volume.",
                                                                new AcceptableValueRange<float>(0, 1f)));
            
            blizzardFogMeanFreePathMin = Config.Bind("Blizzard",
                                                    "blizzardFogMeanFreePathMin",
                                                    5f,
                                                    new ConfigDescription("Minimum mean free path for blizzard fog in meters. Actual mean free path is random between min and max. Mean free path is the distance where visibility is reduced by 63%",
                                                                        new AcceptableValueRange<float>(0.1f, 100)));
            
            blizzardFogMeanFreePathMax = Config.Bind("Blizzard",
                                                    "blizzardFogMeanFreePathMax",
                                                    15f,
                                                    new ConfigDescription("Maximum mean free path for blizzard fog in meters. Actual mean free path is random between min and max.",
                                                                        new AcceptableValueRange<float>(0.1f, 100)));  
            #endregion

            #region Snow & Blizzard Graphics
            snowParticlesMultiplier = Config.Bind("Snow Graphics",
                                            "snowParticlesMultiplier",
                                            1f,
                                            new ConfigDescription("Multiplier for the amount of snow/blizzard particles. Lower values may reduce performance impact but also visual quality.",
                                                                new AcceptableValueRange<float>(0, 10f)));
            
            blizzardWaveParticlesMultiplier = Config.Bind("Snow Graphics",
                                                        "blizzardWaveParticlesMultiplier",
                                                        1f,
                                                        new ConfigDescription("Multiplier for the amount of blizzard wave particles. Lower values may reduce performance impact but also reduce density of particles.",
                                                                            new AcceptableValueRange<float>(0, 10f)));  
            useParticleBlizzardFog = Config.Bind("Snow Graphics",
                                            "useParticleBlizzardFog",
                                            false,
                                            "Enable particle based blizzard fog effect. Disabling this can improve performance, at the cost of visual quality.");
            
            useVolumetricBlizzardFog = Config.Bind("Snow Graphics",
                                                "useVolumetricBlizzardFog",
                                                true,
                                                "Enable volumetric blizzard fog effect. Disabling this can improve performance, at the cost of visual quality. More performance friendly than particle based fog.");
            
            snowVfxLighting = Config.Bind("Snow Graphics",
                                        "snowVfxLighting",
                                        false,
                                        "Determines if snow particles are affected by shadows and lighting. Disabling this will make snow particles appear the same regardless of lighting conditions, but significantly improves the performance.");
            
            blizzardWaveVfxLighting = Config.Bind("Snow Graphics",
                                                "blizzardWaveVfxLighting",
                                                false,
                                                "Determines if blizzard wave particles are affected by shadows and lighting. Disabling this will make blizzard wave particles appear the same regardless of lighting conditions, but significantly improves the performance.");
            
            useOpaqueSnowMaterial = Config.Bind("Snow Graphics",
                                                "useOpaqueSnowMaterial",
                                                false,
                                                "Use opaque snow material. Disabling this will use a transparent snow material, which will allow for more realistic snow overlay rendering, but will not work with the posterization effect.");
            
            snowCastsShadows = Config.Bind("Snow Graphics",
                                        "snowCastsShadows",
                                        false,
                                        "Snow will cast shadows on the terrain. Disabling this will improve performance, this is quite resource intensive!");
            
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
                                        new ConfigDescription("Kernel size for Percentage Closer Filtering. Higher values increase will produce smoother snow 'shadows' under objects, but will slow baking and thus level loading times.",
                                                            new AcceptableValueRange<int>(1, 50)));
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
            asyncProcessing = Config.Bind("Mesh & Terrain Processing",
                                        "asyncProcessing",
                                        true,
                                        "Enable asynchronous mesh and terrain processing. Disabling this will process everything in the main thread, which will increase loading times.");
                                        
            meshProcessingWhitelist = Config.Bind("Mesh & Terrain Processing",
                                                "meshProcessingWhitelist",
                                                "Offense;Assurance;Artifice;Experimentation;March",
                                                "List of moons that will be included for additional mesh processing. Only put moons that have MESH terrains in this list and you see visual artifacts with snow on them (like thin triangles, spikes or disjoined surfaces). Separate with a semicolon, exact match (including spaces, but not case sensitive).");
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

            #region Toxic Smog
            ToxicDamageInterval = Config.Bind("Toxic Smog",
                                            "ToxicDamageInterval",
                                            2f,
                                            new ConfigDescription("Time in seconds between toxic smog damage ticks.",
                                                                new AcceptableValueRange<float>(0, 9999f)));
            ToxicDamageAmount = Config.Bind("Toxic Smog",
                                            "ToxicDamageAmount",
                                            4,
                                            new ConfigDescription("Amount of damage dealt by toxic smog effect.",
                                                                new AcceptableValueRange<int>(0, 100)));
            PoisoningRemovalMultiplier = Config.Bind("Toxic Smog",
                                                    "PoisoningRemovalMultiplier",
                                                    0.5f,
                                                    new ConfigDescription("Multiplier for the rate at which poisoning effect is removed. 1.0 is normal rate, 0.5 is half rate, 2.0 is double rate, 0 no removal. Values are in comparison to gain rate.",
                                                                        new AcceptableValueRange<float>(0, 10)));
            MinFreePath = Config.Bind("Toxic Smog",
                                    "MinFreePath",
                                    8f,
                                    new ConfigDescription("Minimum free path length for toxic smog in meters. Actual free path length is random between min and max.",
                                                        new AcceptableValueRange<float>(0, 256f)));
            MaxFreePath = Config.Bind("Toxic Smog",
                                    "MaxFreePath",
                                    25f,
                                    new ConfigDescription("Maximum free path length for toxic smog in meters. Actual free path length is random between min and max.",
                                                        new AcceptableValueRange<float>(0, 256f)));
            MinFumesAmount = Config.Bind("Toxic Smog",
                                        "MinFumesAmount",
                                        40,
                                        new ConfigDescription("Minimum amount of fumes spawned outside. Actual amount is random between min and max.",
                                                            new AcceptableValueRange<int>(0, 256)));
            MaxFumesAmount = Config.Bind("Toxic Smog",
                                        "MaxFumesAmount",
                                        75,
                                        new ConfigDescription("Maximum amount of fumes spawned outside. Actual amount is random between min and max.",
                                                            new AcceptableValueRange<int>(0, 256)));
            FactoryAmountMultiplier = Config.Bind("Toxic Smog",
                                                "FactoryAmountMultiplier",
                                                0.5f,
                                                new ConfigDescription("Multiplier for the amount of fumes placed in the interior with respect to outside. Keep in mind that their amount is also multiplied by a dungeon size!",
                                                                    new AcceptableValueRange<float>(0, 10f)));
            #endregion
                                                            
        }
    }
}