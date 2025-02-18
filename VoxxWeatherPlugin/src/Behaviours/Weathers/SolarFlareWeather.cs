using System;
using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.Rendering.HighDefinition;
using VoxxWeatherPlugin.Utils;
using VoxxWeatherPlugin.Behaviours;
using System.Linq;
using System.Collections;
using UnityEngine.Rendering;
using System.Collections.Generic;
using TerraMesh;
using VoxxWeatherPlugin.Compatibility;

namespace VoxxWeatherPlugin.Weathers
{
    public enum FlareIntensity
    {
        Weak,
        Mild,
        Average,
        Strong
    }

    [Serializable]
    public class FlareData
    {
        public float ScreenDistortionIntensity;
        public float RadioDistortionIntensity;
        public float RadioBreakthroughLength;
        public float RadioFrequencyShift;
        public float FlareSize;
        [ColorUsage(true, true)]
        public Color AuroraColor1;
        [ColorUsage(true, true)]
        public Color AuroraColor2;
        public bool IsDoorMalfunction;
        public bool IsTurretMalfunction;
        public bool IsRadMechMalfunction;
    }

    internal class ElectricMalfunctionData
    {
        internal MonoBehaviour? malfunctionObject;
        internal ParticleSystem StaticParticles = null!;
        internal AudioSource ElectricAudio = null!;
        internal Coroutine? MalfunctionCoroutine;
        internal Coroutine? FadeAudioCoroutine;
        internal float MalfunctionDuration;
    }

    internal class SolarFlareWeather : BaseWeather
    {
        public static SolarFlareWeather? Instance { get; private set; }
        [SerializeField]
        internal Material? glitchMaterial;
        internal Dictionary<Camera?, GlitchEffect?> glitchPasses = [];
        [SerializeField]
        internal FlareData[]? flareTypes;
        internal FlareData? flareData;
        [SerializeField]
        internal SolarFlareVFXManager? VFXManager;

        [Header("Electric Malfunction Stuff")]
        private Coroutine? electricMalfunctionCoroutine = null;
        private Coroutine? doorMalfunctionCoroutine = null;
        private Coroutine? lightFlickerCoroutine = null;
        [SerializeField]
        internal AudioClip? staticElectricitySound;
        internal Dictionary<MonoBehaviour, ElectricMalfunctionData> electricMalfunctionData = [];
        internal TerminalAccessibleObject[]? bigDoors;

        internal float TurretMalfunctionDelay => Configuration.TurretMalfunctionDelay.Value; // 4
        internal float RadMechReactivateDelay => Configuration.RadMechReactivateDelay.Value; // 7 
        internal float RadMechStunDuration => Configuration.RadMechStunDuration.Value; //4
        internal float TurretMalfunctionChance => Configuration.TurretMalfunctionChance.Value;
        internal float RadMechReactivationChance => Configuration.RadMechReactivationChance.Value;
        internal float RadMechMalfunctionChance => Configuration.RadMechMalfunctionChance.Value;
        internal float DoorMalfunctionChance => Configuration.DoorMalfunctionChance.Value;
        internal float LandmineMalfunctionChance => Configuration.LandmineMalfunctionChance.Value;

        internal bool IsDoorMalfunctionEnabled => Configuration.DoorMalfunctionEnabled.Value;
        internal bool IsRadMechMalfunctionEnabled => Configuration.RadMechMalfunctionEnabled.Value;
        internal bool IsTurretMalfunctionEnabled => Configuration.TurretMalfunctionEnabled.Value;
        internal bool IsLandmineMalfunctionEnabled => Configuration.LandmineMalfunctionEnabled.Value;
        private Mesh? turretMeshReadable;

        private void Awake()
        {
            Instance = this;

            if (OpenBodyCamsCompat.IsActive)
            {
                OpenBodyCamsCompat.GlitchBodyCameras();
            }
        }

        private void OnEnable()
        {
            LevelManipulator.Instance?.InitializeLevelProperties(1.2f);

            GlitchRadarMap();

            if (staticElectricitySound == null)
            {
                staticElectricitySound = StartOfRound.Instance.allItemsList.itemsList
                    .FirstOrDefault(item => item.name == "ZapGun")?
                    .spawnPrefab?
                    .transform.Find("AimDirection")?
                    .gameObject.GetComponent<AudioSource>()?
                    .clip;
            }

            FlareIntensity[] flareIntensities = (FlareIntensity[])Enum.GetValues(typeof(FlareIntensity));
            FlareIntensity randomIntensity = flareIntensities[SeededRandom.Next(flareIntensities.Length)];

            if (flareTypes == null)
            {
                Debug.LogWarning("Flare types not set up correctly! Likely FixPluginTypesSerialization is not installed!");
                return;
            }

            flareData = flareTypes[(int)randomIntensity];
            
            RefreshGlitchCameras();

            TerminalAccessibleObject[] terminalObjects = FindObjectsOfType<TerminalAccessibleObject>();
            bigDoors = terminalObjects.Where(obj => obj.isBigDoor).ToArray();

            var radMechNests = FindObjectsOfType<EnemyAINestSpawnObject>().Where(obj => obj.enemyType.enemyName == "RadMech").ToArray();
            foreach (EnemyAINestSpawnObject radMechNest in radMechNests)
            {
                CreateStaticParticle(radMechNest);
            }

            var mines = FindObjectsOfType<Landmine>();
            foreach (Landmine mine in mines)
            {
                CreateStaticParticle(mine);
            }

            VFXManager?.PopulateLevelWithVFX();

            StartCoroutine(DisplayTipDelayed("Solar Flare Warning", $"{randomIntensity} solar energy burst detected!", 7f));
        }

        private IEnumerator DisplayTipDelayed(string title, string message, float delay)
        {
            yield return new WaitForSeconds(delay);
            HUDManager.Instance.DisplayTip(title, message);
        }

        private void RefreshGlitchCameras()
        {
            List<Camera> staleCameras = [];
           
            foreach ((Camera? camera, GlitchEffect? glitchPass) in glitchPasses)
            {
                if (camera == null || glitchPass == null)
                {
                    staleCameras.Add(camera);
                    continue;
                }
                glitchPass.enabled = true;
                glitchPass.intensity.value = flareData.ScreenDistortionIntensity;
            }

            foreach (Camera staleCamera in staleCameras)
            {
                glitchPasses.Remove(staleCamera);
            }
        }

        private void OnDisable()
        {
            LevelManipulator.Instance?.ResetLevelProperties();

            foreach (GlitchEffect? glitchPass in glitchPasses.Values)
            {
                if (glitchPass == null)
                {
                    continue;
                }
                glitchPass.enabled = false;
            }

            flareData = null;
            bigDoors = null;
            electricMalfunctionData?.Clear();
            VFXManager?.Reset();
        }

        private void Update()
        {
            if (flareData == null)
            {
                return;
            }

            if (TimeOfDay.Instance.normalizedTimeOfDay % 0.03f < 1e-4)
            {
                if (lightFlickerCoroutine != null)
                {
                    StopCoroutine(lightFlickerCoroutine);
                }

                lightFlickerCoroutine = StartCoroutine(LightFlickerCoroutine());

                if (electricMalfunctionCoroutine == null)
                {
                    electricMalfunctionCoroutine = StartCoroutine(ElectricalMalfunctionCoroutine());
                }
            }
            
            if (TimeOfDay.Instance.normalizedTimeOfDay % 0.06f < 1e-4)
            {
               if (doorMalfunctionCoroutine != null)
                {
                    StopCoroutine(doorMalfunctionCoroutine);
                }
                doorMalfunctionCoroutine = StartCoroutine(DoorMalfunctionCoroutine()); 
            }
        }

        private IEnumerator LightFlickerCoroutine()
        {
            foreach (Animator poweredLight in RoundManager.Instance.allPoweredLightsAnimators)
            {
                poweredLight?.SetTrigger("Flicker");
                yield return null;
            }

            lightFlickerCoroutine = null;
        }

        private IEnumerator ElectricalMalfunctionCoroutine()
        {
            foreach (var malfunctionDataEntry in electricMalfunctionData)
            {
                MonoBehaviour malfunctionObject = malfunctionDataEntry.Key;
                ElectricMalfunctionData malfunctionData = malfunctionDataEntry.Value;

                if (malfunctionData.MalfunctionCoroutine != null)
                {
                    Debug.LogDebug($"Malfunction coroutine already running for {malfunctionObject.name}!");
                    continue;
                }

                if (!GameNetworkManager.Instance.isHostingGame)
                {
                    yield return null;
                }

                switch (malfunctionObject)
                {
                    case RadMechAI:
                        if (SeededRandom.NextDouble() < RadMechMalfunctionChance &&
                            IsRadMechMalfunctionEnabled &&
                            (flareData?.IsRadMechMalfunction ?? false))
                        {
                            WeatherEventSynchronizer.Instance?.StartMalfunction(malfunctionData);
                        }
                        break;
                    case Turret:
                        if (SeededRandom.NextDouble() < TurretMalfunctionChance &&
                            IsTurretMalfunctionEnabled &&
                            (flareData?.IsTurretMalfunction ?? false))
                        {
                            WeatherEventSynchronizer.Instance?.StartMalfunction(malfunctionData);
                        }
                        break;
                    case EnemyAINestSpawnObject:
                        if (SeededRandom.NextDouble() < RadMechReactivationChance &&
                            IsRadMechMalfunctionEnabled &&
                            (flareData?.IsRadMechMalfunction ?? false))
                        {
                            WeatherEventSynchronizer.Instance?.StartMalfunction(malfunctionData);
                        }
                        break;
                    case Landmine:
                        if (SeededRandom.NextDouble() < LandmineMalfunctionChance &&
                            IsLandmineMalfunctionEnabled)
                        {
                            WeatherEventSynchronizer.Instance?.StartMalfunction(malfunctionData);
                        }
                        break;
                }

                yield return null;
            }

            electricMalfunctionCoroutine = null;
        }

        internal IEnumerator ElectricMalfunctionCoroutine(ElectricMalfunctionData malfunctionData)
        {
            if (malfunctionData.malfunctionObject == null)
            {
                // Clean up the dictionary
                Debug.LogDebug("Malfunction object is null! Removing from dictionary.");
                electricMalfunctionData.Remove(malfunctionData.malfunctionObject);
                yield break;
            }

            Debug.LogError($"Starting electric malfunction for {malfunctionData.malfunctionObject.name}");

            if (malfunctionData.FadeAudioCoroutine != null)
            {
                StopCoroutine(malfunctionData.FadeAudioCoroutine);
                malfunctionData.FadeAudioCoroutine = null;
                malfunctionData.ElectricAudio.volume = 0f;
            }
            
            malfunctionData.FadeAudioCoroutine = StartCoroutine(FadeAudio(malfunctionData, malfunctionData.MalfunctionDuration * 0.1f, 0f, true));
            malfunctionData.StaticParticles?.Play();

            yield return new WaitForSeconds(malfunctionData.MalfunctionDuration);

            if (malfunctionData.malfunctionObject is RadMechAI radMechAI)
            {
                radMechAI.FlickerFace();
                radMechAI.SetEnemyStunned(true, RadMechStunDuration);
                yield return new WaitUntil(() => radMechAI.stunNormalizedTimer <= 0);
                radMechAI.EnableSpotlight();
                malfunctionData.StaticParticles?.Stop();
            }
            else if (malfunctionData.malfunctionObject is Turret turret)
            {
                if (GameNetworkManager.Instance.isHostingGame)
                {
                    (turret as IHittable).Hit(1, Vector3.down);
                }
                yield return new WaitForSeconds(2f); // Wait a bit so turret's mode will sync up 
                yield return new WaitUntil(() => turret.turretMode != TurretMode.Berserk);
                malfunctionData.StaticParticles?.Stop();
            }
            else if (malfunctionData.malfunctionObject is EnemyAINestSpawnObject radMechNest)
            {
                malfunctionData.StaticParticles?.Stop();
                EnemyType radMechType = radMechNest.enemyType;
                if (RoundManager.Instance.currentOutsideEnemyPower + radMechType.PowerLevel <= RoundManager.Instance.currentMaxOutsidePower)
                {
                    Vector3 nestPosition = radMechNest.transform.position;
                    float nestAngle = radMechNest.transform.rotation.eulerAngles.y;
                    RoundManager.Instance.enemyNestSpawnObjects.Remove(radMechNest);
                    Destroy(radMechNest.gameObject);
                    GameObject radMechNestPrefab = radMechType.nestSpawnPrefab;
                    radMechType.nestSpawnPrefab = null; // This is to prevent the spawned rad mech to teleport to a random nest
                    if (GameNetworkManager.Instance.isHostingGame)
                    {
                        var radMechNetworkReference = RoundManager.Instance.SpawnEnemyGameObject(nestPosition, nestAngle, -1, radMechType);
                        RoundManager.Instance.currentOutsideEnemyPower += radMechType.PowerLevel;
                    }
                    yield return new WaitForSeconds(2f); // Wait a bit to sync up
                    radMechType.nestSpawnPrefab = radMechNestPrefab;
                    electricMalfunctionData.Remove(radMechNest);
                }
            }
            else if (malfunctionData.malfunctionObject is Landmine mine)
            {
                if (GameNetworkManager.Instance.isHostingGame)
                {
                    (mine as IHittable).Hit(1, Vector3.down);
                }
                malfunctionData.StaticParticles?.Stop();
                electricMalfunctionData.Remove(mine);
            }
            
            if (malfunctionData.FadeAudioCoroutine != null)
            {
                StopCoroutine(malfunctionData.FadeAudioCoroutine);
                malfunctionData.FadeAudioCoroutine = null;
            }
            malfunctionData.FadeAudioCoroutine = StartCoroutine(FadeAudio(malfunctionData, malfunctionData.MalfunctionDuration * 0.1f, 0.5f, false));
            
            malfunctionData.MalfunctionCoroutine = null;
        }

        private IEnumerator DoorMalfunctionCoroutine()
        {
            if (!GameNetworkManager.Instance.isHostingGame)
            {
                doorMalfunctionCoroutine = null;
                yield break;
            }

            if ((flareData?.IsDoorMalfunction ?? false) &&
                    bigDoors != null &&
                    IsDoorMalfunctionEnabled)
            {
                foreach (TerminalAccessibleObject door in bigDoors)
                {
                    bool open = SeededRandom.NextDouble() < DoorMalfunctionChance;
                    door?.SetDoorLocalClient(open);
                    yield return null;
                }
            }

            doorMalfunctionCoroutine = null;
        }

        internal void GlitchRadarMap()
        {
            //Enable glitch effect for the radar camera
            GameObject? radarCameraObject = GameObject.Find("Systems/GameSystems/ItemSystems/MapCamera");

            if (radarCameraObject == null)
            {
                Debug.LogError("Radar camera not found! Cannot glitch radar map.");
                return;
            }

            Camera? radarCamera = radarCameraObject.GetComponent<Camera>();

            if (radarCamera != null)
            {
                GlitchCamera(radarCamera);
                //radarCameraObject.AddComponent<CameraDebugSettings>();
            }
            else
            {
                Debug.LogError("Radar camera component not found! Cannot glitch radar map.");
            }
        }

        internal GlitchEffect? GlitchCamera(Camera? camera)
        {
            if (glitchMaterial == null)
            {
                Debug.LogError("Glitch material not found! Cannot glitch camera.");
                return null;
            }

            if (camera == null)
                return null;

            if (glitchPasses.ContainsKey(camera))
            {
                return glitchPasses[camera];
            }

            HDAdditionalCameraData radarCameraData = camera.GetComponent<HDAdditionalCameraData>();
            FrameSettingsOverrideMask radarCameraSettingsMask = radarCameraData.renderingPathCustomFrameSettingsOverrideMask;
            
            void SetOverride(FrameSettingsField setting, bool enabled)
            {
                radarCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)setting] = true;
                radarCameraData.renderingPathCustomFrameSettings.SetEnabled(setting, enabled);
            }

            // Allow custom passes for the radar camera
            SetOverride(FrameSettingsField.CustomPass, true);
            // SetOverride(FrameSettingsField.Distortion, true);
            // SetOverride(FrameSettingsField.RoughDistortion, true);
            // SetOverride(FrameSettingsField.Refraction, true);
            // SetOverride(FrameSettingsField.RoughRefraction, true);

            radarCameraData.renderingPathCustomFrameSettingsOverrideMask = radarCameraSettingsMask;

            // Add a Local Custom Pass Volume component.
            CustomPassVolume glitchVolume = camera.gameObject.AddComponent<CustomPassVolume>();
            glitchVolume.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;
            glitchVolume.isGlobal = false;
            glitchVolume.targetCamera = camera;

            // Create a new GlitchEffect pass.
            GlitchEffect glitchPass = new GlitchEffect{name = "Glitch Pass", m_Material = glitchMaterial};

            // Add the pass to the volume and disable it.
            glitchVolume.customPasses.Add(glitchPass);
            glitchPass.enabled = false;

            glitchPasses.Add(camera, glitchPass);

            Debug.LogDebug("Glitch Pass added to the Radar camera.");

            return glitchPass;
        }

        internal void CreateStaticParticle(MonoBehaviour inputClass)
        {
            if (inputClass == null)
            {
                Debug.LogError("Input class is null! Cannot create static particles.");
                return;
            }
            if (staticElectricitySound == null)
            {
                Debug.LogError("Static electricity sound not found! Cannot create static particles.");
                return;
            }

            ParticleSystem staticParticles = Instantiate(StartOfRound.Instance.magnetParticle, parent: inputClass.transform);
            AudioSource audioSource = staticParticles.gameObject.AddComponent<AudioSource>();

            if (staticParticles == null)
            {
                Debug.LogError("Static particles prefab not found!");
                return;
            }

            staticParticles.Stop();
            staticParticles.transform.localPosition = Vector3.zero;
            var mainModule = staticParticles.main;
            mainModule.playOnAwake = false;
            mainModule.loop = true;
            mainModule.startSize = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            mainModule.startColor = new ParticleSystem.MinMaxGradient(Color.white * Mathf.Pow(2, 1.5f));
            var noiseModule = staticParticles.noise;
            noiseModule.enabled = false;

            audioSource.clip = staticElectricitySound;
            audioSource.volume = 0f;
            audioSource.loop = true;
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.maxDistance = 30f;
            audioSource.minDistance = 5f;

            // for (int i = 0; i < staticParticles.transform.childCount; i++)
            // {
            //     DestroyImmediate(staticParticles.transform.GetChild(i).gameObject);
            // }
            
            var shapeModule = staticParticles.shape;
            ElectricMalfunctionData malfunctionData = new ElectricMalfunctionData();
            malfunctionData.StaticParticles = staticParticles;
            malfunctionData.ElectricAudio = audioSource;
            malfunctionData.malfunctionObject = inputClass;
            switch (inputClass)
            {
                case RadMechAI radMechAI:
                    if (radMechAI.skinnedMeshRenderers.Length > 0)
                    {
                        shapeModule.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
                        shapeModule.skinnedMeshRenderer = radMechAI.skinnedMeshRenderers[0];
                        malfunctionData.MalfunctionDuration = RadMechStunDuration;
                        electricMalfunctionData.Add(radMechAI, malfunctionData);
                    }
                    break;
                case Turret turret:
                    Transform turretMountTransform = turret.transform.parent.Find("MeshContainer/Mount");
                    if (turretMountTransform.TryGetComponent(out MeshRenderer meshRenderer))
                    {
                        staticParticles.transform.localScale = Vector3.one * 0.4f;
                        MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
                        turretMeshReadable ??= meshFilter.sharedMesh.MakeReadableCopy();
                        meshFilter.sharedMesh = turretMeshReadable;
                        shapeModule.shapeType = ParticleSystemShapeType.MeshRenderer;
                        shapeModule.meshRenderer = meshRenderer;
                        malfunctionData.MalfunctionDuration = TurretMalfunctionDelay;
                        electricMalfunctionData.Add(turret, malfunctionData);
                    }
                    break;
                case EnemyAINestSpawnObject radMechNest:
                    SkinnedMeshRenderer skinnedMeshRenderer = radMechNest.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (skinnedMeshRenderer != null)
                    {
                        shapeModule.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
                        shapeModule.skinnedMeshRenderer = skinnedMeshRenderer;
                        malfunctionData.MalfunctionDuration = RadMechReactivateDelay;
                        electricMalfunctionData.Add(radMechNest, malfunctionData);
                    }
                    break;
                case Landmine mine:
                    shapeModule.shapeType = ParticleSystemShapeType.Sphere;
                    shapeModule.radius = 0.5f;
                    malfunctionData.MalfunctionDuration = 1f; // Hardcoded >:D
                    electricMalfunctionData.Add(mine, malfunctionData);
                    break;
                default:
                    Debug.LogWarning($"Unsupported object for static particles: {inputClass.name}");
                    return;
            }
        }

        private IEnumerator FadeAudio(ElectricMalfunctionData malfunctionData, float duration, float delay, bool fadeIn)
        {
            AudioSource? audioSource = malfunctionData.ElectricAudio;
            float targetVolume = fadeIn ? 1f : 0f;
            float currentTime = 0f;

            if (delay > 0)
            {
                yield return new WaitForSeconds(delay);
            }

            if (fadeIn && audioSource != null)
            {
                audioSource.volume = 0f; // Start at zero volume for fade-in
                audioSource.Play(); 
            }

            while (currentTime < duration && audioSource != null)
            {
                currentTime += Time.deltaTime;
                audioSource.volume = Mathf.Lerp(audioSource.volume, targetVolume, currentTime / duration);
                yield return null;
            }

            if (!fadeIn)
            {
                audioSource?.Stop(); // Stop playback after fading out
            }

            malfunctionData.FadeAudioCoroutine = null;
        }
    
    }

    

    internal class SolarFlareVFXManager : BaseVFXManager
    {
        [SerializeField]
        internal GameObject? flareObject; // Prefab for the flare
        internal GameObject? flareObjectCopy; // GameObject for the flare that gets parented to the sun
        [SerializeField]
        internal GameObject? auroraObject; // GameObject for the particles
        private GameObject? sunTextureObject; // GameObject for the sun texture
        
        // Threshold for sun luminosity in lux to enable aurora
        internal float auroraSunThreshold => Configuration.AuroraVisibilityThreshold.Value; 

        // Variables for emitter placement

        internal override void PopulateLevelWithVFX()
        {
            (Color, Color) PrepareCoronaColor(Color baseCoronaColor)
            {
                Color.RGBToHSV(baseCoronaColor, out float h, out float s, out float v);
                s = Mathf.Clamp01(s + 0.65f); // Increase saturation, clamp to 0-1 range
                baseCoronaColor = Color.HSVToRGB(h, s, v);
                float factor = Mathf.Pow(2, 1.3f); //HDR color correction
                Color coronaColor1 = new Color(baseCoronaColor.r * factor, baseCoronaColor.g * factor, baseCoronaColor.b * factor, 1f);
                Color coronaColor2 = baseCoronaColor;
                coronaColor2.r += .2f; // Increase red channel
                factor = Mathf.Pow(2, 2.7f);
                coronaColor2 = new Color(coronaColor2.r * factor, coronaColor2.g * factor, coronaColor2.b * factor, 1f);
                return (coronaColor1, coronaColor2);
            }

            sunTextureObject = null;

            if (TimeOfDay.Instance.sunDirect == null)
            {
                Debug.LogWarning("Sun animator is null! Disabling Corona VFX. Aurora forcibly enabled.");
            }
            else 
            {
                foreach (Transform child in TimeOfDay.Instance.sunDirect.transform.parent)
                {
                    if (child.name == "SunTexture" && child.gameObject.activeInHierarchy)
                    {
                        sunTextureObject = child.gameObject;
                        if ((!child.GetComponent<MeshRenderer>()?.enabled ?? true) && child.childCount > 0)
                        {
                            sunTextureObject = null;
                            // Iterate until a child with an enabled mesh renderer is found
                            foreach (Transform sunChild in child)
                            {
                                if (sunChild.GetComponent<MeshRenderer>()?.enabled ?? false)
                                {
                                    sunTextureObject = sunChild.gameObject;
                                    break;
                                }
                            }
                        }
                        break;
                    }
                }
            }

            if (flareObject == null || auroraObject == null)
            {
                Debug.LogError("Flare or Aurora VFX not instantiated!");
                return;
            }

            // Set up the Aurora VFX with the colors from the SolarFlareWeather instance
            auroraObject.SetActive(false);
            VisualEffect auroraVFX = auroraObject.GetComponent<VisualEffect>();
            if (SolarFlareWeather.Instance?.flareData != null)
            {
                auroraObject.transform.parent = SolarFlareWeather.Instance.transform; // to stop it from moving with the player
                auroraObject.transform.position = new Vector3(LevelBounds.center.x, StartOfRound.Instance.shipBounds.bounds.center.y, LevelBounds.center.z);
                auroraObject.transform.rotation = Quaternion.identity;
                auroraVFX.SetVector4("auroraColor", SolarFlareWeather.Instance.flareData.AuroraColor1);
                auroraVFX.SetVector4("auroraColor2", SolarFlareWeather.Instance.flareData.AuroraColor2);
                Debug.LogDebug("Aurora VFX colored.");
            }

            // Set up the Corona VFX with the colors from the sun texture
            if (sunTextureObject != null)
            {
                MeshRenderer? sunMeshRenderer = sunTextureObject.GetComponent<MeshRenderer>();
                Texture2D? mainTexture = sunMeshRenderer?.sharedMaterial.mainTexture as Texture2D;
                if (mainTexture == null || sunMeshRenderer == null)
                {
                    Debug.LogWarning("Sun does not have a texture or renderer assigned!");
                    
                }
                else
                {
                    flareObjectCopy = Instantiate(flareObject);
                    flareObjectCopy.transform.position = sunTextureObject.transform.position;
                    flareObjectCopy.transform.rotation = sunTextureObject.transform.rotation;
                    flareObjectCopy.transform.localScale = sunTextureObject.transform.lossyScale * (SolarFlareWeather.Instance?.flareData?.FlareSize ?? 1.1f);
                    flareObjectCopy.transform.parent = sunTextureObject.transform; // to sync with the sun

                    // Get the average color of the sun texture
                    Color averageTextureColor = GetAverageTextureColor(mainTexture);
                    float averageAlphaColor = averageTextureColor.a;
                    averageTextureColor.a = 1f; // Set alpha to 1
                    
                    Color baseColor = sunMeshRenderer.sharedMaterial.color;
                    Color baseCoronaColor = Color.Lerp(baseColor, averageTextureColor, baseColor.a);

                    (Color coronaColor1, Color coronaColor2) = PrepareCoronaColor(baseCoronaColor);

                    // Calculate approximate visible radius of the sun
                    float sunRadiusNormalized = Mathf.Sqrt(averageAlphaColor);
                    Vector3 sunExtents = sunMeshRenderer.bounds.extents;

                    float sunRadius = Mathf.Max(sunExtents.x, sunExtents.y, sunExtents.z) * sunRadiusNormalized;

                    // Set the color and mask radius of the corona VFX
                    VisualEffect coronaVFX = flareObjectCopy.GetComponent<VisualEffect>();
                    coronaVFX.SetFloat("sunRadius", sunRadius);
                    coronaVFX.SetVector4("coronaColor", coronaColor1);
                    coronaVFX.SetVector4("coronaColor2", coronaColor2);
                    flareObjectCopy.SetActive(true);
                    Debug.LogDebug($"Corona VFX instantiated at mesh sun {sunTextureObject.name}.");
                    return;
                }
            }

            if (LevelManipulator.Instance.sunLightData != null)
            {
                bool isPhysicallyBasedSky = false;
                Debug.LogDebug("Sun texture not found! Trying Physically Based Sky for Corona VFX.");

                // Check every Volume component in the specific scene LevelManipulator.Instance.CurrentSceneName
                Volume[] volumes = FindObjectsOfType<Volume>();
                float invBloomStrength = 1f;
                foreach (Volume volume in volumes)
                {
                    if (!volume.enabled || !volume.gameObject.activeInHierarchy)
                        continue;

                    if (volume.gameObject.scene.name == LevelManipulator.Instance.CurrentSceneName &&
                        volume.profile.TryGet(out PhysicallyBasedSky physicallyBasedSky))
                    {
                        isPhysicallyBasedSky = true;
                    }
                    if (volume.gameObject.scene.name == LevelManipulator.Instance.CurrentSceneName &&
                        volume.profile.TryGet(out Bloom bloomEffect))
                    {
                        invBloomStrength = 1 - bloomEffect.intensity.value;
                    }
                }

                isPhysicallyBasedSky &= LevelManipulator.Instance.sunLightData.interactsWithSky;

                if (!isPhysicallyBasedSky)
                {
                    Debug.LogWarning("Physically Based Sky not found! Corona VFX disabled.");
                    return;
                }
                
                flareObjectCopy = Instantiate(flareObject);
                Transform flareTransform = flareObjectCopy.transform;

                Light directSun = TimeOfDay.Instance.sunDirect!;
                HDAdditionalLightData lightData = LevelManipulator.Instance.sunLightData;

                Vector3 sunDirection = -directSun.transform.forward;
                float farClipPlane = GameNetworkManager.Instance.localPlayerController.gameplayCamera.farClipPlane * 0.99f;
                Vector3 flarePosition = directSun.transform.position + sunDirection * farClipPlane;

                float sunAngularSizeRadians = (lightData.angularDiameter + lightData.flareSize) * Mathf.Deg2Rad;
                float directSunRadius = Mathf.Tan(sunAngularSizeRadians/2) * farClipPlane;
                float flareMeshRadius = 5f; // This is hardcoded, check mesh size in Unity
                float rescaleParameter = 2 * directSunRadius/flareMeshRadius;
                rescaleParameter /= Mathf.Clamp(invBloomStrength, 0.1f, 1f);
                rescaleParameter *= SolarFlareWeather.Instance?.flareData?.FlareSize ?? 1.1f;
                
                flareTransform.position = flarePosition;
                flareTransform.rotation = directSun.transform.rotation;
                flareTransform.SetParent(directSun.transform);
                flareTransform.localRotation = Quaternion.Euler(90f, 0, 0);
                flareTransform.localScale = new Vector3(1, 1, 1);
                Vector3 coronaWorldScale = flareTransform.lossyScale;
                flareTransform.localScale = new Vector3(rescaleParameter/ coronaWorldScale.x, rescaleParameter/ coronaWorldScale.y, rescaleParameter/ coronaWorldScale.z);

                Color baseCoronaColor = lightData.flareTint;
                Color temperatureColor = ColorFromTemperature(directSun.colorTemperature);
                (Color coronaColor1, Color coronaColor2) = PrepareCoronaColor(baseCoronaColor * temperatureColor);
                // Set the color and mask radius of the corona VFX
                VisualEffect coronaVFX = flareObjectCopy.GetComponent<VisualEffect>();
                coronaVFX.SetBool("enableVortex", false);
                coronaVFX.SetFloat("sunRadius", directSunRadius);
                coronaVFX.SetVector4("coronaColor", coronaColor1);
                coronaVFX.SetVector4("coronaColor2", coronaColor2);
                flareObjectCopy.SetActive(true);
                Debug.LogDebug("Corona VFX instantiated for physically based sky.");
                return;
            }
            else
            {
                Debug.LogWarning("Sun light not found! Corona VFX disabled.");
            }
        }

        public static Color ColorFromTemperature(float kelvin)
        {
            float temperature = kelvin / 100f;
            float red, green, blue;

            // Calculate Red:
            if (temperature <= 66)
            {
                red = 255;
            }
            else
            {
                red = temperature - 60;
                red = 329.698727446f * Mathf.Pow(red, -0.1332047592f);
                if (red < 0) red = 0;
                if (red > 255) red = 255;
            }

            // Calculate Green:
            if (temperature <= 66)
            {
                green = temperature;
                green = 99.4708025861f * Mathf.Log(green) - 161.1195681661f;
                if (green < 0) green = 0;
                if (green > 255) green = 255;
            }
            else
            {
                green = temperature - 60;
                green = 288.1221695283f * Mathf.Pow(green, -0.0755148492f);
                if (green < 0) green = 0;
                if (green > 255) green = 255;
            }

            // Calculate Blue:
            if (temperature >= 66)
            {
                blue = 255;
            }
            else
            {
                if (temperature <= 19)
                {
                    blue = 0;
                }
                else
                {
                    blue = temperature - 10;
                    blue = 138.5177312231f * Mathf.Log(blue) - 305.0447927307f;
                    if (blue < 0) blue = 0;
                    if (blue > 255) blue = 255;
                }
            }

            return new Color(red / 255f, green / 255f, blue / 255f);
        }

        internal void Update()
        {
            float sunLuminosity = LevelManipulator.Instance.sunLightData?.intensity ?? 0f;
            
            // TODO add check for sun's position relative to horizon???
            if (sunLuminosity < auroraSunThreshold && (!auroraObject?.activeInHierarchy ?? false)) 
            {
                auroraObject?.SetActive(true);
            }
        }

        internal static Color GetAverageTextureColor(Texture2D? texture)
        {
            if (texture == null)
            {
                Debug.LogError("Texture is null!");
                return Color.yellow; // Default color for the sun
            }

            RenderTexture rt = RenderTexture.GetTemporary(
                texture.width,
                texture.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Linear
            );
            
            Graphics.Blit(texture, rt);
            Texture2D readableTexture = new Texture2D(texture.width, texture.height);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            readableTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            readableTexture.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            Color[] pixels = readableTexture.GetPixels();
            float r = 0, g = 0, b = 0, a = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a < 0.2) continue; // Alpha thresholding
                r += pixels[i].r;
                g += pixels[i].g;
                b += pixels[i].b;
                a += pixels[i].a;

            }

            r /= pixels.Length;
            g /= pixels.Length;
            b /= pixels.Length;
            a /= pixels.Length;

            return new Color(r, g, b, a); 
        }

        internal override void Reset()
        {
            if (auroraObject != null)
            {
                auroraObject.SetActive(false);
            }
            if (flareObjectCopy != null)
            {
                flareObjectCopy.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (flareObjectCopy != null)
            {
                flareObjectCopy.SetActive(true);
            }
        }

        private void OnDisable()
        {
            // TODO compat for OpenCams
            if (auroraObject != null)
            {
                auroraObject.SetActive(false);
            }
            if (flareObjectCopy != null)
            {
                flareObjectCopy.SetActive(false);
            }
        }
    }
}
