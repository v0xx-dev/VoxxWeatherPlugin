using System;
using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.Rendering.HighDefinition;
using VoxxWeatherPlugin.Utils;
using System.Linq;
using Unity.Collections;
using static Steamworks.InventoryItem;

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
        internal FlareIntensity Intensity { get; private set; }
        internal float ScreenDistortionIntensity { get; private set; }
        internal float RadioDistortionIntensity { get; private set; }
        internal float RadioBreakthroughLength { get; private set; }
        internal float FlareSize { get; private set; }
        internal float dischargeSpeed { get; private set; }
        internal Color AuroraColor1 { get; private set; }
        internal Color AuroraColor2 { get; private set; }
        internal bool IsDoorMalfunction { get; private set; }

        public FlareData(FlareIntensity intensity)
        {
            Intensity = intensity;

            switch (intensity)
            {
                case FlareIntensity.Weak:
                    ScreenDistortionIntensity = 0.3f;
                    RadioDistortionIntensity = 0.1f;
                    RadioBreakthroughLength = 1f;
                    AuroraColor1 = new Color(0f, 11.98f, 0.69f, 1f); 
                    AuroraColor2 = new Color(0.29f, 8.33f, 8.17f, 1f);
                    FlareSize = 1f;
                    dischargeSpeed = 180f;
                    IsDoorMalfunction = false;
                    break;
                case FlareIntensity.Mild:
                    ScreenDistortionIntensity = 0.5f;
                    RadioDistortionIntensity = 0.25f;
                    RadioBreakthroughLength = 0.25f;
                    AuroraColor1 = new Color(0.13f, 8.47f, 8.47f, 1f);
                    AuroraColor2 = new Color(9.46f, 0.25f, 15.85f, 1f);
                    FlareSize = 1.1f;
                    dischargeSpeed = 120f;
                    IsDoorMalfunction = false;
                    break;
                case FlareIntensity.Average:
                    ScreenDistortionIntensity = 0.8f;
                    RadioDistortionIntensity = 0.5f;
                    RadioBreakthroughLength = 0.1f;
                    AuroraColor1 = new Color(0.38f, 6.88f, 0f, 1f);
                    AuroraColor2 = new Color(15.55f, 0.83f, 7.32f, 1f);
                    FlareSize = 1.25f;
                    dischargeSpeed = 60f;
                    IsDoorMalfunction = true;
                    break;
                case FlareIntensity.Strong:
                    ScreenDistortionIntensity = 1f;
                    RadioDistortionIntensity = 0.75f;
                    RadioBreakthroughLength = 0.05f;
                    AuroraColor1 = new Color(5.92f, 0f, 11.98f, 1f);
                    AuroraColor2 = new Color(8.65f, 0.83f, 1.87f, 1f);
                    FlareSize = 1.4f;
                    dischargeSpeed = 30f;
                    IsDoorMalfunction = true;
                    break;
            }
        }
    }

    internal class SolarFlareWeather : MonoBehaviour
    {
        [SerializeField]
        internal static Material glitchMaterial;
        internal static FlareData flareData;
        [SerializeField]
        internal GlitchEffect glitchPass;
        TerminalAccessibleObject[] bigDoors;

        internal void GlitchRadarMap()
        {
            //Enable glitch effect for the radar camera
            GameObject radarCameraObject = GameObject.Find("Systems/GameSystems/ItemSystems/MapCamera");
            if (radarCameraObject != null)
            {
                HDAdditionalCameraData radarCameraData = radarCameraObject.GetComponent<HDAdditionalCameraData>();
                FrameSettings radarCameraSettings = radarCameraData.renderingPathCustomFrameSettings;
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
                    CustomPassVolume glitchVolume = volumeMainTransform.gameObject.AddComponent<CustomPassVolume>();
                    glitchVolume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;
                    glitchVolume.isGlobal = false;

                    // Create a new GlitchEffect pass.
                    glitchPass = new GlitchEffect();
                    glitchPass.name = "Glitch Pass";
                    glitchPass.m_Material = glitchMaterial;

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
            if (glitchPass == null)
            {
                GlitchRadarMap();
            }

            System.Random seededRandom = new System.Random(StartOfRound.Instance.randomMapSeed);

            FlareIntensity[] flareIntensities = (FlareIntensity[])Enum.GetValues(typeof(FlareIntensity));
            FlareIntensity randomIntensity = flareIntensities[seededRandom.Next(flareIntensities.Length)];
            flareData = new FlareData(randomIntensity);

            if (glitchPass != null)
            {
                glitchPass.intensity.value = flareData.ScreenDistortionIntensity;
                glitchPass.enabled = true;
            }
            TerminalAccessibleObject[] terminalObjects = FindObjectsOfType<TerminalAccessibleObject>();
            bigDoors = terminalObjects.Where(obj => obj.isBigDoor).ToArray();
            SolarFlareVFXManager.PopulateLevelWithVFX();
        }

        private void OnDisable()
        {
            if (glitchPass != null)
            {
                glitchPass.enabled = false;
            }
            flareData = null;
            SolarFlareVFXManager.ResetVFX();
        }

        private void Update()
        {
            if (TimeOfDay.Instance.normalizedTimeOfDay % 0.03f < 1e-4)
            {
                foreach (Animator poweredLight in RoundManager.Instance.allPoweredLightsAnimators)
                {
                    poweredLight.SetTrigger("Flicker");
                }
            }
            if (TimeOfDay.Instance.normalizedTimeOfDay % 0.1f < 1e-4 && flareData.IsDoorMalfunction && bigDoors != null && GameNetworkManager.Instance.isHostingGame)
            {
                foreach (TerminalAccessibleObject door in bigDoors)
                {
                    bool open = UnityEngine.Random.value < 0.5f;
                    door.SetDoorLocalClient(open);
                }
            }
        }
    }

    internal class SolarFlareVFXManager : MonoBehaviour
    {
        public static GameObject flarePrefab; // Prefab for the flare effect
        public static GameObject auroraPrefab; // Prefab for the aurora effect
        [SerializeField]
        internal static GameObject flareObject; // GameObject for the flare
        [SerializeField]
        internal static GameObject auroraObject; // GameObject for the particles

        internal float auroraSunThreshold = 8f; // Threshold for sun luminosity in lux to enable aurora

        // Variables for emitter placement

        internal static void PopulateLevelWithVFX()
        {
            GameObject sunTexture = null;
            Transform animatedSun = TimeOfDay.Instance.sunDirect.transform.parent;
            foreach (Transform child in animatedSun)
            {
                if (child.name == "SunTexture")
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

            if (sunTexture != null)
            {
                flareObject = Instantiate(flarePrefab, sunTexture.transform.position, sunTexture.transform.rotation);
                flareObject.transform.SetParent(sunTexture.transform);
                flareObject.transform.localScale = Vector3.one * SolarFlareWeather.flareData.FlareSize;
                Texture2D mainTexture = sunTexture.GetComponent<Renderer>().material.mainTexture as Texture2D;
                if (mainTexture == null)
                {
                    Debug.LogError("sunTexture does not have a texture assigned!");
                }
                
                // Get the average color of the sun texture
                Color averageTextureColor = GetAverageTextureColor(mainTexture);
                Color baseColor = sunTexture.GetComponent<Renderer>().material.color;
                Color finalColor = Color.Lerp(baseColor, averageTextureColor, baseColor.a);
                Color coronaColor2 = finalColor;
                coronaColor2.r += .15f; // Increase red channel
                float factor = Mathf.Pow(2, 1.3f);
                finalColor = new Color(finalColor.r * factor, finalColor.g * factor, finalColor.b * factor, 1f);
                factor = Mathf.Pow(2, 2.7f);
                coronaColor2 = new Color(coronaColor2.r * factor, coronaColor2.g * factor, coronaColor2.b * factor, 1f);

                VisualEffect coronaVFX = flareObject.GetComponent<VisualEffect>();
                coronaVFX.SetVector4("coronaColor", finalColor);
                coronaVFX.SetVector4("coronaColor2", coronaColor2);
                flareObject.SetActive(true);
            }
            else
            {
                Debug.LogError("Sun texture not found!");
            }
        }

        internal void Update()
        {
            if (auroraObject == null || TimeOfDay.Instance.sunDirect == null)
            {
                return;
            }

            HDAdditionalLightData lightData = TimeOfDay.Instance.sunDirect.GetComponent<HDAdditionalLightData>();

            if (lightData.intensity < auroraSunThreshold) //add check for sun's position relative to horizon???
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
            s = Mathf.Clamp01(s + 0.5f); // Increase saturation, clamp to 0-1 range
            Color finalColor = Color.HSVToRGB(h, s, v);
            finalColor.a = 1f;
            return finalColor; 
        }

        internal static void ResetVFX()
        {
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
