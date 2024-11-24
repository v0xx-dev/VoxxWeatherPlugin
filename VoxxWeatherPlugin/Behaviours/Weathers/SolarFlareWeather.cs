using System;
using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.Rendering.HighDefinition;
using VoxxWeatherPlugin.Utils;
using System.Linq;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

namespace VoxxWeatherPlugin.Weathers
{
    internal enum FlareIntensity
    {
        Weak,
        Mild,
        Average,
        Strong
    }

    internal class FlareData
    {
        public FlareIntensity Intensity { get; internal set; }
        public float ScreenDistortionIntensity { get; internal set; }
        public float RadioDistortionIntensity { get; internal set; }
        public float RadioBreakthroughLength { get; internal set; }
        public float RadioFrequencyShift { get; internal set; }
        public float FlareSize { get; internal set; }
        public Color AuroraColor1 { get; internal set; }
        public Color AuroraColor2 { get; internal set; }
        public bool IsDoorMalfunction { get; internal set; }

        public FlareData(FlareIntensity intensity)
        {
            Intensity = intensity;

            switch (intensity)
            {
                case FlareIntensity.Weak:
                    ScreenDistortionIntensity = 0.3f;
                    RadioDistortionIntensity = 0.25f;
                    RadioBreakthroughLength = 1.25f;
                    RadioFrequencyShift = 1000f;
                    AuroraColor1 = new Color(0f, 11.98f, 0.69f, 1f); 
                    AuroraColor2 = new Color(0.29f, 8.33f, 8.17f, 1f);
                    FlareSize = 1f;
                    IsDoorMalfunction = false;
                    break;
                case FlareIntensity.Mild:
                    ScreenDistortionIntensity = 0.5f;
                    RadioDistortionIntensity = 0.45f;
                    RadioBreakthroughLength = 0.75f;
                    RadioFrequencyShift = 250f;
                    AuroraColor1 = new Color(0.13f, 8.47f, 8.47f, 1f);
                    AuroraColor2 = new Color(9.46f, 0.25f, 15.85f, 1f);
                    FlareSize = 1.1f;
                    IsDoorMalfunction = false;
                    break;
                case FlareIntensity.Average:
                    ScreenDistortionIntensity = 0.8f;
                    RadioDistortionIntensity = 0.6f;
                    RadioBreakthroughLength = 0.5f;
                    RadioFrequencyShift = 50f;
                    AuroraColor1 = new Color(0.38f, 6.88f, 0f, 1f);
                    AuroraColor2 = new Color(15.55f, 0.83f, 7.32f, 1f);
                    FlareSize = 1.25f;
                    IsDoorMalfunction = true;
                    break;
                case FlareIntensity.Strong:
                    ScreenDistortionIntensity = 1f;
                    RadioDistortionIntensity = 0.85f;
                    RadioBreakthroughLength = 0.25f;
                    RadioFrequencyShift = 10f;
                    AuroraColor1 = new Color(5.92f, 0f, 11.98f, 1f);
                    AuroraColor2 = new Color(8.65f, 0.83f, 1.87f, 1f);
                    FlareSize = 1.4f;
                    IsDoorMalfunction = true;
                    break;
            }
        }
    }

    internal class SolarFlareWeather : MonoBehaviour
    {
        [SerializeField]
        internal static Material glitchMaterial;
        [SerializeField]
        internal GlitchEffect glitchPass;
        // [SerializeField]
        // internal  AudioClip staticElectricitySound;
        internal static FlareData flareData;
        internal CustomPassVolume glitchVolume;
        internal TerminalAccessibleObject[] bigDoors;

        internal SolarFlareVFXManager solarFlareVFXManager;
        // internal Turret[] turrets;
        // internal EnemyAINestSpawnObject[] radMechNests;
        // internal float turretMalfunctionDelay = 1f;
        // internal float turretMalfunctionChance = 0.1f;
        // internal float radMechReactivateDelay = 1f;
        // internal float radMechStunDuration = 1f;
        
        // internal float radMechReactivationChance = 0.1f;
        // internal float radMechMalfunctionChance = 0.1f;
        internal bool isDoorMalfunctionEnabled => VoxxWeatherPlugin.DoorMalfunctionEnabled.Value;
        internal float doorMalfunctionChance => Mathf.Clamp01(VoxxWeatherPlugin.DoorMalfunctionChance.Value);

        internal void GlitchRadarMap()
        {
            //Enable glitch effect for the radar camera
            GameObject radarCameraObject = GameObject.Find("Systems/GameSystems/ItemSystems/MapCamera");
            if (radarCameraObject != null)
            {
                HDAdditionalCameraData radarCameraData = radarCameraObject.GetComponent<HDAdditionalCameraData>();
                FrameSettingsOverrideMask radarCameraSettingsMask = radarCameraData.renderingPathCustomFrameSettingsOverrideMask;
                radarCameraSettingsMask.mask[(uint)FrameSettingsField.CustomPass] = false;
                radarCameraData.renderingPathCustomFrameSettingsOverrideMask = radarCameraSettingsMask;

                Transform volumeMainTransform = null;
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

                    Debug.Log("Glitch Pass added to the Radar camera.");
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
            flareData = new FlareData(randomIntensity);
            
            solarFlareVFXManager.PopulateLevelWithVFX();

            if (glitchPass != null)
            {
                glitchPass.intensity.value = flareData.ScreenDistortionIntensity;
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
            solarFlareVFXManager.ResetVFX();
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
                    poweredLight.SetTrigger("Flicker");
                }

            }
            if (TimeOfDay.Instance.normalizedTimeOfDay % 0.07f < 1e-4)
            {
                if (flareData.IsDoorMalfunction && bigDoors != null && GameNetworkManager.Instance.isHostingGame && isDoorMalfunctionEnabled)
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

    

    internal class SolarFlareVFXManager : MonoBehaviour
    {
        public GameObject flarePrefab; // Prefab for the flare effect
        public GameObject auroraPrefab; // Prefab for the aurora effect
        [SerializeField]
        internal GameObject flareObject; // GameObject for the flare
        [SerializeField]
        internal GameObject auroraObject; // GameObject for the particles
        internal HDAdditionalLightData sunLightData;

        internal float auroraSunThreshold = 8f; // Threshold for sun luminosity in lux to enable aurora

        // Variables for emitter placement

        internal void PopulateLevelWithVFX()
        {
            GameObject sunTexture = null;
            
            if (TimeOfDay.Instance.sunDirect == null)
            {
                Debug.LogWarning("Sun animator is null! Disabling Corona VFX. Aurora force enabled.");
            }
            else foreach (Transform child in TimeOfDay.Instance.sunDirect.transform.parent)
            {
                if (child.name == "SunTexture" && child.gameObject.activeSelf)
                {
                    sunTexture = child.gameObject;
                    break;
                }
            }

            if (flarePrefab == null || auroraPrefab == null)
            {
                Debug.LogError("Flare or aurora prefab is null!");
                return;
            }

            auroraObject = Instantiate(auroraPrefab, Vector3.zero, Quaternion.identity);
            auroraObject.SetActive(false);
            VisualEffect auroraVFX = auroraObject.GetComponent<VisualEffect>();
            auroraVFX.SetVector4("auroraColor", SolarFlareWeather.flareData.AuroraColor1);
            auroraVFX.SetVector4("auroraColor2", SolarFlareWeather.flareData.AuroraColor2);
            Debug.LogDebug("Aurora VFX instantiated.");

            if (sunTexture != null)
            {
                flareObject = Instantiate(flarePrefab, sunTexture.transform.position, sunTexture.transform.rotation);
                flareObject.transform.SetParent(sunTexture.transform);
                flareObject.transform.localScale = Vector3.one * SolarFlareWeather.flareData.FlareSize;
                Texture2D mainTexture = sunTexture.GetComponent<Renderer>().material.mainTexture as Texture2D;
                if (mainTexture == null)
                {
                    Debug.LogWarning("sunTexture does not have a texture assigned!");
                }
                
                // Get the average color of the sun texture
                Color averageTextureColor = GetAverageTextureColor(mainTexture);
                Color baseColor = sunTexture.GetComponent<Renderer>().material.color;
                Color finalColor = Color.Lerp(baseColor, averageTextureColor, baseColor.a);
                Color coronaColor2 = finalColor;
                coronaColor2.r += .2f; // Increase red channel
                float factor = Mathf.Pow(2, 1.3f); //HDR color correction
                finalColor = new Color(finalColor.r * factor, finalColor.g * factor, finalColor.b * factor, 1f);
                factor = Mathf.Pow(2, 2.7f);
                coronaColor2 = new Color(coronaColor2.r * factor, coronaColor2.g * factor, coronaColor2.b * factor, 1f);

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
            if (auroraObject == null)
            {
                return;
            }

            float sunLuminosity = sunLightData != null ? sunLightData.intensity: 0;

            if (sunLuminosity < auroraSunThreshold) //add check for sun's position relative to horizon???
            {
                auroraObject.SetActive(true);
            }
            else
            {
                auroraObject.SetActive(false);
            }
        }

        internal static Color GetAverageTextureColor(Texture2D texture)
        {
            RenderTexture rt = RenderTexture.GetTemporary(
                texture.width,
                texture.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Linear
            );
            Graphics.Blit(texture, rt);
            Texture2D readableTexture = new Texture2D(texture.width, texture.height);
            RenderTexture.active = rt;
            readableTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            readableTexture.Apply();
            RenderTexture.active = null;
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

        internal void ResetVFX()
        {
            sunLightData = null;
            if (flareObject != null)
            {
                Destroy(flareObject);
                flareObject = null;
            }
            if (auroraObject != null)
            {
                Destroy(auroraObject);
                auroraObject = null;
            }
        }

        private void OnEnable()
        {
            if (TimeOfDay.Instance.sunDirect != null && sunLightData == null)
            {
                sunLightData = TimeOfDay.Instance.sunDirect.GetComponent<HDAdditionalLightData>();
            }
            if (auroraObject != null)
            {
                auroraObject.SetActive(true);
            }
            if (flareObject != null)
            {
                flareObject.SetActive(true);
            }
        }

        private void OnDisable()
        {
            if (auroraObject != null)
                auroraObject.SetActive(false);
            if (flareObject != null)
                flareObject.SetActive(false);
        }
    }
}
