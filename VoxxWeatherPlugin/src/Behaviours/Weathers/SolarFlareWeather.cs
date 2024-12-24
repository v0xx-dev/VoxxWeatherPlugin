using System;
using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.Rendering.HighDefinition;
using VoxxWeatherPlugin.Utils;
using VoxxWeatherPlugin.Behaviours;
using System.Linq;

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
    }

    internal class SolarFlareWeather : BaseWeather
    {
        public static SolarFlareWeather? Instance { get; private set; }
        [SerializeField]
        internal Material? glitchMaterial;
        [SerializeField]
        internal CustomPassVolume? glitchVolume;
        internal GlitchEffect? glitchPass;
        // [SerializeField]
        // internal  AudioClip staticElectricitySound;
        [SerializeField]
        internal FlareData[]? flareTypes;
        internal FlareData? flareData;
        internal TerminalAccessibleObject[]? bigDoors;
        [SerializeField]
        internal SolarFlareVFXManager? VFXManager;
        // internal Turret[] turrets;
        // internal EnemyAINestSpawnObject[] radMechNests;
        // internal float turretMalfunctionDelay = 1f;
        // internal float turretMalfunctionChance = 0.1f;
        // internal float radMechReactivateDelay = 1f;
        // internal float radMechStunDuration = 1f;
        
        // internal float radMechReactivationChance = 0.1f;
        // internal float radMechMalfunctionChance = 0.1f;
        internal bool isDoorMalfunctionEnabled => Configuration.DoorMalfunctionEnabled.Value;
        internal float doorMalfunctionChance => Mathf.Clamp01(Configuration.DoorMalfunctionChance.Value);

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            if (glitchVolume == null)
            {
                GlitchRadarMap();
            }

            // if (staticElectricitySound == null)
            // {
            //     staticElectricitySound = StartOfRound.Instance.allItemsList.itemsList
            //         .FirstOrDefault(item => item.name == "Zap gun")?
            //         .spawnPrefab?
            //         .transform.Find("AimDirection")?
            //         .gameObject.GetComponent<AudioSource>()?
            //         .clip;
            // }

            System.Random seededRandom = new System.Random(StartOfRound.Instance.randomMapSeed);

            FlareIntensity[] flareIntensities = (FlareIntensity[])Enum.GetValues(typeof(FlareIntensity));
            FlareIntensity randomIntensity = flareIntensities[seededRandom.Next(flareIntensities.Length)];
            if (flareTypes == null)
            {
                Debug.LogWarning("Flare types not set up correctly! Likely FixPluginTypesSerialization is not installed!");
                return;
            }
            flareData = flareTypes?[(int)randomIntensity];

            
            VFXManager?.PopulateLevelWithVFX();

            if (glitchPass != null)
            {
                glitchPass.intensity.value = flareData?.ScreenDistortionIntensity ?? 0f;
                glitchPass.enabled = true;
            }
            TerminalAccessibleObject[] terminalObjects = FindObjectsOfType<TerminalAccessibleObject>();
            bigDoors = terminalObjects.Where(obj => obj.isBigDoor).ToArray();

            // turrets = FindObjectsOfType<Turret>();
            // foreach (Turret turret in turrets)
            // {
            //     CreateStaticParticle(turret);
            // }
            
            // radMechNests = FindObjectsOfType<EnemyAINestSpawnObject>().Where(obj => obj.enemyType.enemyName == "RadMech").ToArray();
            // foreach (EnemyAINestSpawnObject radMechNest in radMechNests)
            // {
            //     CreateStaticParticle(radMechNest);
            // }
        }

        private void OnDisable()
        {
            if (glitchPass != null)
            {
                glitchPass.enabled = false;
            }

            flareData = null;
            bigDoors = null;
            // turrets = null;
            // radMechNests = null;
            VFXManager?.Reset();
        }

        private void Update()
        {
            if (glitchPass != null && flareData != null)
            {
                glitchPass.intensity.value = flareData.ScreenDistortionIntensity;
            }

            if (TimeOfDay.Instance.normalizedTimeOfDay % 0.03f < 1e-4)
            {
                // EnemyAI[] radMechs = RoundManager.Instance.SpawnedEnemies.Where(enemy => enemy is RadMechAI).ToArray();
                // foreach (RadMechAI radMech in radMechs)
                // {
                //     if (UnityEngine.Random.value < radMechMalfunctionChance && radMech != null)
                //     {
                //         StartCoroutine(ElectricMalfunctionCoroutine(radMech));
                //     }
                // }

                foreach (Animator poweredLight in RoundManager.Instance.allPoweredLightsAnimators)
                {
                    poweredLight?.SetTrigger("Flicker");
                }

            }
            if (TimeOfDay.Instance.normalizedTimeOfDay % 0.07f < 1e-4)
            {
                if ((flareData?.IsDoorMalfunction ?? false) && bigDoors != null && GameNetworkManager.Instance.isHostingGame && isDoorMalfunctionEnabled)
                {
                    foreach (TerminalAccessibleObject door in bigDoors)
                    {
                        bool open = UnityEngine.Random.value < doorMalfunctionChance;
                        door.SetDoorLocalClient(open);
                    }
                }

                // foreach (Turret turret in turrets)
                // {
                //     if (UnityEngine.Random.value < turretMalfunctionChance && turret != null)
                //     {
                //         StartCoroutine(ElectricMalfunctionCoroutine(turret));
                //     }
                // }

                // foreach (EnemyAINestSpawnObject radMechNest in radMechNests)
                // {
                //     if (UnityEngine.Random.value < radMechReactivationChance && radMechNest != null)
                //     {
                //         StartCoroutine(ElectricMalfunctionCoroutine(radMechNest));
                //     }
                // }
            }
        }

        // TODO: Add compatibility for OpenCams
        internal void GlitchRadarMap()
        {
            //Enable glitch effect for the radar camera
            GameObject radarCameraObject = GameObject.Find("Systems/GameSystems/ItemSystems/MapCamera");
            if (radarCameraObject != null)
            {
                HDAdditionalCameraData radarCameraData = radarCameraObject.GetComponent<HDAdditionalCameraData>();
                FrameSettingsOverrideMask radarCameraSettingsMask = radarCameraData.renderingPathCustomFrameSettingsOverrideMask;
                // Allow custom passes for the radar camera
                radarCameraSettingsMask.mask[(uint)FrameSettingsField.CustomPass] = false;
                radarCameraData.renderingPathCustomFrameSettingsOverrideMask = radarCameraSettingsMask;

                Transform? volumeMainTransform = null;
                foreach (Transform child in radarCameraObject.transform)
                {
                    if (child.name.StartsWith("VolumeMain"))
                    {
                        volumeMainTransform = child;
                        break;
                    }
                }

                if (volumeMainTransform != null)
                {
                    // Add a Local Custom Pass Volume component.
                    glitchVolume = volumeMainTransform.gameObject.AddComponent<CustomPassVolume>();
                    glitchVolume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;
                    glitchVolume.isGlobal = false;

                    // Create a new GlitchEffect pass.
                    glitchPass = new GlitchEffect{
                        name = "Glitch Pass",
                        m_Material = glitchMaterial
                    };

                    // Add the pass to the volume and disable it.
                    glitchVolume.customPasses.Add(glitchPass);
                    glitchPass.enabled = false;

                    Debug.LogDebug("Glitch Pass added to the Radar camera.");
                }
                else
                {
                    Debug.LogError("Radar camera volume not found!");
                }
            }
            else
            {
                Debug.LogError("Radar camera not found!");
            }
        }

        // internal GameObject CreateStaticParticle<T>(T inputClass) where T : MonoBehaviour
        // {
        //     ParticleSystem staticParticles = Instantiate(StartOfRound.Instance.magnetParticle, parent: inputClass.transform);
        //     staticParticles?.Stop();
        //     staticParticles.transform.localPosition = Vector3.zero;
        //     var main = staticParticles.main;
        //     main.playOnAwake = false;
        //     main.loop = true;
        //     var noise = staticParticles.noise;
        //     noise.enabled = false;

        //     AudioSource audioSource = staticParticles.gameObject.AddComponent<AudioSource>();
        //     audioSource.clip = staticElectricitySound;
        //     audioSource.volume = 0f;
        //     audioSource.loop = true;
        //     audioSource.playOnAwake = false;
        //     audioSource.spatialBlend = 1f;
        //     audioSource.rolloffMode = AudioRolloffMode.Linear;
        //     audioSource.maxDistance = 15f;
        //     audioSource.minDistance = 2f;

        //     for (int i = 0; i < staticParticles.transform.childCount; i++)
        //     {
        //         DestroyImmediate(staticParticles.transform.GetChild(i).gameObject);
        //     }
            
        //     var shapeModule = staticParticles.shape;
        //     switch (inputClass)
        //     {
        //         case RadMechAI radMechAI:
        //             if (radMechAI.skinnedMeshRenderers.Length > 0)
        //             {
        //                 shapeModule.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
        //                 shapeModule.skinnedMeshRenderer = radMechAI.skinnedMeshRenderers[0];
        //             }
        //             break;
        //         case Turret turret:
        //             turret.transform.parent.Find("MeshContainer/Mount").TryGetComponent(out MeshRenderer meshRenderer);
        //             if (meshRenderer != null)
        //             {
        //                 staticParticles.transform.localScale = Vector3.one * 0.4f;
        //                 shapeModule.shapeType = ParticleSystemShapeType.MeshRenderer;
        //                 shapeModule.meshRenderer = meshRenderer;
        //             }
        //             break;
        //         case EnemyAINestSpawnObject radMechNest:
        //             SkinnedMeshRenderer skinnedMeshRenderer = radMechNest.GetComponentInChildren<SkinnedMeshRenderer>();
        //             if (skinnedMeshRenderer != null)
        //             {
        //                 shapeModule.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
        //                 shapeModule.skinnedMeshRenderer = skinnedMeshRenderer;
        //             }
        //             break;
        //         default:
        //             Debug.LogWarning($"Unsupported type for CreateStaticParticle: {typeof(T)}");
        //             return null;
        //     }

        //     return staticParticles.gameObject;
        // }

        // internal IEnumerator ElectricMalfunctionCoroutine<T>(T electricalObject) where T : MonoBehaviour
        // {
        //     float malfunctionDuration = 0f;
        //     Action malfunctionAction = null;

        //     ParticleSystem staticParticles = null;
        //     AudioSource electricAudio = null;
        //     foreach (Transform child in electricalObject.transform)
        //     {
        //         if (child.name.StartsWith("MagnetParticle"))
        //         {
        //             staticParticles = child.GetComponent<ParticleSystem>();
        //             electricAudio = child.GetComponent<AudioSource>();
        //             break;
        //         }
        //     }

        //     if (staticParticles != null && staticParticles.isPlaying)
        //     {
        //         yield break; // Exit the coroutine if particles are already playing (meaning another coroutine is running)
        //     }

        //     if ((staticParticles == null || electricAudio == null) && electricalObject is not RadMechAI)
        //     {
        //         Debug.LogWarning("Static particles or audio not found for " + electricalObject.name);
        //     }

        //     switch (electricalObject)
        //     {
        //         case RadMechAI radMech:
        //             malfunctionDuration = radMechStunDuration;
        //             malfunctionAction = () => 
        //             {
        //                 radMech.SetEnemyStunned(true, radMechStunDuration);
        //                 staticParticles?.Stop(); 
        //             };
        //             break;

        //         case EnemyAINestSpawnObject radMechNest:
        //             malfunctionDuration = radMechReactivateDelay;
        //             malfunctionAction = () =>
        //             {
        //                 staticParticles?.Stop();
        //                 if (GameNetworkManager.Instance.isHostingGame)
        //                 {
        //                     Vector3 nestPosition = radMechNest.transform.position;
        //                     float nestAngle = radMechNest.transform.rotation.eulerAngles.y;
        //                     EnemyType radMechType = radMechNest.enemyType;
        //                     Destroy(radMechNest.gameObject);
        //                     RoundManager.Instance.SpawnEnemyGameObject(nestPosition, nestAngle, -1, radMechType);
        //                     RoundManager.Instance.currentOutsideEnemyPower += radMechType.PowerLevel;
        //                 }
        //             };
        //             break;

        //         case Turret turret:
        //             malfunctionDuration = turretMalfunctionDelay;
        //             malfunctionAction = () =>
        //             {
        //                 staticParticles?.Stop();
        //                 if (GameNetworkManager.Instance.isHostingGame)
        //                 {
        //                     (turret as IHittable).Hit(1, Vector3.down);
        //                 }
        //             };
        //             break;

        //         default:
        //             Debug.LogWarning("ElectricMalfunctionCoroutine called with an unsupported type: " + electricalObject.GetType());
        //             yield break;
        //     }

        //     if (malfunctionDuration > 0f) 
        //     {
        //         if (staticParticles == null) 
        //         {
        //             GameObject particleContainer = CreateStaticParticle(electricalObject); // You might need to adjust this method
        //             staticParticles = particleContainer.GetComponent<ParticleSystem>();
        //             electricAudio = particleContainer.GetComponent<AudioSource>();
        //         }

        //         StartCoroutine(FadeAudio(electricAudio, malfunctionDuration * 0.1f, 0f, true));
        //         staticParticles?.Play();
        //         yield return new WaitForSeconds(malfunctionDuration);
        //         StartCoroutine(FadeAudio(electricAudio, malfunctionDuration * 0.1f, malfunctionDuration * 0.95f, false));

        //         // Execute the type-specific action
        //         malfunctionAction?.Invoke(); 
        //     }
        // }

        // private IEnumerator FadeAudio(AudioSource audioSource, float duration, float delay, bool fadeIn)
        // {
        //     float targetVolume = fadeIn ? 1f : 0f;
        //     float currentTime = 0f;

        //     if (delay > 0)
        //     {
        //         yield return new WaitForSeconds(delay);
        //     }

        //     if (fadeIn)
        //     {
        //         audioSource.volume = 0f; // Start at zero volume for fade-in
        //         audioSource?.Play(); 
        //     }

        //     while (currentTime < duration && audioSource != null)
        //     {
        //         currentTime += Time.deltaTime;
        //         audioSource.volume = Mathf.Lerp(audioSource.volume, targetVolume, currentTime / duration);
        //         yield return null;
        //     }

        //     if (!fadeIn)
        //     {
        //         audioSource?.Stop(); // Stop playback after fading out
        //     }
        // }
    
    }

    

    internal class SolarFlareVFXManager : BaseVFXManager
    {
        [SerializeField]
        internal GameObject? flareObject; // GameObject for the flare
        [SerializeField]
        internal GameObject? auroraObject; // GameObject for the particles
        private GameObject? sunTexture; // GameObject for the sun texture
        internal HDAdditionalLightData? sunLightData;
        
        // Threshold for sun luminosity in lux to enable aurora
        internal float auroraSunThreshold => Configuration.AuroraVisibilityThreshold.Value; 

        // Variables for emitter placement

        internal override void PopulateLevelWithVFX(Bounds levelBounds = default, System.Random? seededRandom = null)
        {
            sunTexture = null;
            
            if (TimeOfDay.Instance.sunDirect == null)
            {
                Debug.LogWarning("Sun animator is null! Disabling Corona VFX. Aurora forcibly enabled.");
            }
            else foreach (Transform child in TimeOfDay.Instance.sunDirect.transform.parent)
            {
                if (child.name == "SunTexture" && child.gameObject.activeSelf)
                {
                    sunTexture = child.gameObject;
                    break;
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
                auroraObject.transform.position = new Vector3(levelBounds.center.x, StartOfRound.Instance.shipBounds.bounds.center.y, levelBounds.center.z);
                auroraObject.transform.rotation = Quaternion.identity;
                auroraVFX.SetVector4("auroraColor", SolarFlareWeather.Instance.flareData.AuroraColor1);
                auroraVFX.SetVector4("auroraColor2", SolarFlareWeather.Instance.flareData.AuroraColor2);
                Debug.LogDebug("Aurora VFX colored.");
            }

            // Set up the Corona VFX with the colors from the sun texture
            if (sunTexture != null)
            {
                flareObject.transform.parent = SolarFlareWeather.Instance!.transform; // to stop it from moving with the player
                Texture2D? mainTexture = sunTexture.GetComponent<Renderer>().sharedMaterial.mainTexture as Texture2D;
                if (mainTexture == null)
                {
                    Debug.LogWarning("sunTexture does not have a texture assigned!");
                }
                
                // Get the average color of the sun texture
                Color averageTextureColor = GetAverageTextureColor(mainTexture);
                Color baseColor = sunTexture.GetComponent<Renderer>().sharedMaterial.color;
                Color finalColor = Color.Lerp(baseColor, averageTextureColor, baseColor.a);
                Color coronaColor2 = finalColor;
                coronaColor2.r += .2f; // Increase red channel
                float factor = Mathf.Pow(2, 1.3f); //HDR color correction
                finalColor = new Color(finalColor.r * factor, finalColor.g * factor, finalColor.b * factor, 1f);
                factor = Mathf.Pow(2, 2.7f);
                coronaColor2 = new Color(coronaColor2.r * factor, coronaColor2.g * factor, coronaColor2.b * factor, 1f);

                // Set the color of the corona VFX
                VisualEffect coronaVFX = flareObject.GetComponent<VisualEffect>();
                coronaVFX.SetVector4("coronaColor", finalColor);
                coronaVFX.SetVector4("coronaColor2", coronaColor2);
                flareObject.SetActive(true);
                Debug.LogDebug("Corona VFX instantiated.");
            }
            else
            {
                Debug.LogWarning("Sun texture not found! Corona VFX disabled.");
            }
        }

        internal void Update()
        {
            float sunLuminosity = sunLightData != null ? sunLightData.intensity: 0;

            // Sync flare objects position, rotation and scale with the sun texture absolute values relative to the world
            if (sunTexture != null)
            {
                flareObject!.transform.position = sunTexture.transform.position;
                flareObject.transform.rotation = sunTexture.transform.rotation;
                flareObject.transform.localScale = sunTexture.transform.lossyScale * (SolarFlareWeather.Instance?.flareData?.FlareSize ?? 1.5f);
            }

            if (sunLuminosity < auroraSunThreshold && (!auroraObject?.activeInHierarchy ?? false)) //add check for sun's position relative to horizon???
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
            float r = 0, g = 0, b = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a < 0.5) continue; // Alpha thresholding
                r += pixels[i].r;
                g += pixels[i].g;
                b += pixels[i].b;
            }

            r /= pixels.Length;
            g /= pixels.Length;
            b /= pixels.Length;

            float h, s, v;

            Color.RGBToHSV(new Color(r, g, b), out h, out s, out v);
            s = Mathf.Clamp01(s + 0.65f); // Increase saturation, clamp to 0-1 range
            Color finalColor = Color.HSVToRGB(h, s, v);
            finalColor.a = 1f;
            return finalColor; 
        }

        internal override void Reset()
        {
            sunLightData = null;
            flareObject?.SetActive(false);
            auroraObject?.SetActive(false);
        }

        private void OnEnable()
        {
            if (TimeOfDay.Instance.sunDirect != null)
            {
                sunLightData = TimeOfDay.Instance.sunDirect.GetComponent<HDAdditionalLightData>();
            }
            
            flareObject?.SetActive(true);
        }

        private void OnDisable()
        {
            // TODO compat for OpenCams
            auroraObject?.SetActive(false);
            flareObject?.SetActive(false);
        }
    }
}
