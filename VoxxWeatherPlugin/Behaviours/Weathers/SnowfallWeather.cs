using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.Rendering.HighDefinition;
using VoxxWeatherPlugin.Behaviours;
using VoxxWeatherPlugin.Patches;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using DunGen;
using VoxxWeatherPlugin.Utils;
using UnityEngine.Events;
using System;
using System.Collections;

namespace VoxxWeatherPlugin.Weathers
{
    public class SnowfallWeather: MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField]
        internal Material iceMaterial;
        [SerializeField]
        internal CustomPassVolume snowVolume;
        internal SnowRenderersCustomPass snowOverlayCustomPass;
        internal SnowRenderersCustomPass snowVertexCustomPass;
        [SerializeField]
        internal float emissionMultiplier;

        [Header("Base Snow Thickness")]
        [SerializeField]
        internal float snowScale = 1.0f;
        [SerializeField, Tooltip("The snow intensity is the power of the exponential function, so 0.0f is full snow.")]
        internal float snowIntensity = 10.0f;
        [SerializeField]
        internal float maxSnowHeight = 2.0f;
        [SerializeField]
        internal float maxSnowNormalizedTime = 1f;

        [Header("Snow Occlusion")]
        [SerializeField]
        internal Camera levelDepthmapCamera;
        [SerializeField]
        internal RenderTexture levelDepthmap;
        [SerializeField]
        internal uint PCFKernelSize = 9;
        [SerializeField]
        internal float shadowBias = 0.01f;
        [SerializeField]
        internal float snowOcclusionBias = 0.01f;

        [Header("Snow Tracks")]
        [SerializeField]
        internal GameObject snowTrackerCameraContainer;
        [SerializeField]
        internal Camera snowTracksCamera;
        [SerializeField]
        internal RenderTexture snowTracksMap;
        [SerializeField]
        internal GameObject footprintsTrackerVFX;
        [SerializeField]
        internal GameObject lowcapFootprintsTrackerVFX;
        [SerializeField]
        internal GameObject itemTrackerVFX;
        [SerializeField]
        internal GameObject shovelVFX;

        [Header("Tessellation Parameters")]
        [SerializeField]
        internal float baseTessellationFactor = 4.0f;
        [SerializeField]
        internal float maxTessellationFactor = 16.0f;
        [SerializeField]
        internal int isAdaptiveTessellation = 0; // 0 for fixed tessellation, 1 for adaptive tessellation

        [SerializeField]
        internal Vector3 shipPosition;
        [SerializeField]
        internal Vector3 prevSnowTrackerPosition = Vector3.zero;
        [Header("Ground")]
        [SerializeField]
        internal GameObject groundObject;
        [SerializeField]
        internal string[] groundTags = {"Grass", "Gravel", "Snow", "Rock"};
        internal bool swappedToSnow = false;
        internal QuicksandTrigger[] waterObjects;
        internal SnowRenderersCustomPass.RenderingLayers originalOverlayRenderingLayers;
        internal SnowThicknessCalculator snowThicknessCalculator;
        internal string currentSceneName = "None";
        internal System.Random seededRandom;

        internal void Awake()
        {
            snowThicknessCalculator = snowTrackerCameraContainer.GetComponent<SnowThicknessCalculator>();
            snowThicknessCalculator.snowfallWeather = this;
            if (snowVolume.customPasses.Count != 2)
            {
                Debug.LogError("SnowfallWeather requires exactly 2 custom passes in the snow volume!");
            }
            snowOverlayCustomPass = snowVolume.customPasses[0] as SnowRenderersCustomPass;
            snowVertexCustomPass = snowVolume.customPasses[1] as SnowRenderersCustomPass;
            snowOverlayCustomPass.snowfallWeather = this;
            snowVertexCustomPass.snowfallWeather = this;

            originalOverlayRenderingLayers = snowOverlayCustomPass.renderingLayers;
            snowOverlayCustomPass.renderingLayers |= snowVertexCustomPass.renderingLayers;

            levelDepthmapCamera.enabled = false; // Disable the camera to render manually

            levelDepthmap = new RenderTexture(2048, 
                                            2048,
                                            0, // Depth bits
                                            RenderTextureFormat.RHalf
                                            );
            levelDepthmap.wrapMode = TextureWrapMode.Clamp;
            levelDepthmap.filterMode = FilterMode.Trilinear;
            levelDepthmap.useMipMap = false;
            levelDepthmap.enableRandomWrite = true;
            levelDepthmap.useDynamicScale = true;
            levelDepthmap.name = "Level Depthmap";
            levelDepthmap.Create();
            // Set the camera target texture
            levelDepthmapCamera.targetTexture = levelDepthmap;
            levelDepthmapCamera.aspect = 1.0f;
            levelDepthmapCamera.enabled = false;

            snowTracksMap = new RenderTexture(256,
                                            256,
                                            0, // Depth bits
                                            RenderTextureFormat.RHalf); 
            snowTracksMap.filterMode = FilterMode.Trilinear;
            snowTracksMap.wrapMode = TextureWrapMode.Clamp;
            snowTracksMap.useMipMap = false;
            snowTracksMap.enableRandomWrite = true;
            snowTracksMap.useDynamicScale = true;
            snowTracksMap.name = "Snow Tracks Map";
            snowTracksMap.Create();
            // Set the camera target texture
            snowTracksCamera.targetTexture = snowTracksMap;
            snowTracksCamera.aspect = 1.0f;
        }

        internal void OnEnable()
        {
            seededRandom = new System.Random(StartOfRound.Instance.randomMapSeed);
            
            maxSnowHeight = seededRandom.NextDouble(1.7f, 3f);
            snowScale = seededRandom.NextDouble(0.7f, 1.3f);
            maxSnowNormalizedTime = seededRandom.NextDouble(0.5f, 1f);
            swappedToSnow = false;
            FindGround();
            ModifyRenderMasks();
            StartOfRound.Instance.StartNewRoundEvent.AddListener(new UnityAction(SwitchTerrainInstancing));
            FreezeWater();
            ModifyScrollingFog();
            UpdateLevelDepthmapCoroutine();
            snowThicknessCalculator.inputNeedsUpdate = true;
            RefreshFootprintTrackers(SnowPatches.snowTrackersDict);
            RefreshFootprintTrackers(SnowPatches.snowShovelDict);
            StartCoroutine(RefreshLevelDepthmapCoroutine(true));
        }

        internal void OnDisable()
        {
            StartOfRound.Instance.StartNewRoundEvent.RemoveListener(new UnityAction(SwitchTerrainInstancing));
            DisableFootprintTrackers(SnowPatches.snowTrackersDict);
            DisableFootprintTrackers(SnowPatches.snowShovelDict);
        }

        internal void OnDestroy()
        {
            // Release the render textures
            levelDepthmap.Release();
            snowTracksMap.Release();
            SnowPatches.snowTrackersDict.Clear();
            SnowPatches.snowShovelDict.Clear();
        }

        internal void FixedUpdate()
        {
            shipPosition = StartOfRound.Instance.shipBounds.bounds.center;
            float normalizedSnowTimer =  Mathf.Clamp01(maxSnowNormalizedTime - TimeOfDay.Instance.normalizedTimeOfDay);
            snowIntensity = 10f * normalizedSnowTimer;
            if (!swappedToSnow && normalizedSnowTimer <= maxSnowNormalizedTime/3f) //TODO Remove if the patch works
            {
                groundObject.tag = "Snow";
                swappedToSnow = true;
            }
            UpdateTrackerPosition();
        }

        internal void UpdateTrackerPosition()
        {
            Vector3 playerPosition = GameNetworkManager.Instance.localPlayerController.transform.position;
            if (Vector3.Distance(playerPosition, prevSnowTrackerPosition) >= snowTracksCamera.orthographicSize / 2f)
            {
                snowTrackerCameraContainer.transform.position = playerPosition;
                prevSnowTrackerPosition = playerPosition;
            }
        }

        internal void UpdateLevelDepthmapCoroutine()
        {
            Debug.Log("Updating level depthmap");
            (_, Vector3 levelBarycenter) = PlayableAreaCalculator.CalculateZoneSize();
            Vector3 cameraPosition = new Vector3(levelBarycenter.x, levelDepthmapCamera.transform.position.y, levelBarycenter.z);
            levelDepthmapCamera.transform.position = cameraPosition;
            Debug.Log($"Camera position: {levelDepthmapCamera.transform.position}, Barycenter: {levelBarycenter}");
            
            StartCoroutine(RefreshLevelDepthmapCoroutine());
        }

        internal IEnumerator RefreshLevelDepthmapCoroutine(bool waitForLanding = false)
        {
            if (waitForLanding)
            {
                yield return new WaitUntil(() => StartOfRound.Instance.shipHasLanded);
                snowThicknessCalculator.inputNeedsUpdate = true;
            }

            levelDepthmapCamera.targetTexture = levelDepthmap;
            levelDepthmapCamera.aspect = 1.0f;
            levelDepthmapCamera.enabled = true;
            
            yield return new WaitForEndOfFrame();
            
            //levelDepthmapCamera.Render();
            Debug.Log("Level depthmap rendered!");
            
            yield return new WaitForEndOfFrame();
            
            levelDepthmapCamera.targetTexture = null;
            levelDepthmapCamera.enabled = true;
        }

        internal void SwitchTerrainInstancing()
        {   
            bool enable = false;
            Debug.Log("Switching terrain instancing to " + enable);
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                terrain.enabled = false;
                terrain.heightmapPixelError = enable ? 5 : 80;
                terrain.drawInstanced = enable;
                terrain.enabled = true;
            }
        }

        internal void FreezeWater()
        {
            waterObjects = FindObjectsOfType<QuicksandTrigger>().Where(x => x.gameObject.activeSelf && x.isWater && x.gameObject.scene.name == currentSceneName).ToArray(); //&& !x.isInsideWater
            foreach (QuicksandTrigger waterObject in waterObjects)
            {
                //get renderer component or add one if it doesn't exist
                Renderer renderer = waterObject.GetComponent<Renderer>() ?? waterObject.gameObject.AddComponent<Renderer>();
                if (waterObject.GetComponent<MeshFilter>() == null)
                {
                    MeshFilter filter = waterObject.gameObject.AddComponent<MeshFilter>();
                    filter.mesh = GetPrimitiveMesh(PrimitiveType.Cube);
                }
                renderer.enabled = true;
                renderer.material = iceMaterial;
                Vector3 icePosition = waterObject.transform.position;
                icePosition.y += 0.5f;
                waterObject.transform.position = icePosition;
                if (waterObject.TryGetComponent<Collider>(out Collider collider))
                {
                    collider.isTrigger = false;
                }
                waterObject.enabled = false; //disable sinking
            }
        }

        internal void ModifyScrollingFog()
        {
            LocalVolumetricFog[] fogArray = GameObject.FindObjectsOfType<LocalVolumetricFog>();
            fogArray = fogArray.Where(x => x.gameObject.activeSelf && x.gameObject.scene.name == currentSceneName).ToArray();
            foreach (LocalVolumetricFog fog in fogArray)
            {
                fog.parameters.textureScrollingSpeed = Vector3.zero;
                fog.parameters.meanFreePath += 12f; 
            }
        }

        private Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            GameObject.DestroyImmediate(temp);
            return mesh;
        }

        internal void FindGround()
        {
            Vector3 flattenedShipPosition = Vector3.zero; //(shipPosition.x, 0, shipPosition.z);
            
            Terrain mainTerrain;

            Terrain[] terrains = Terrain.activeTerrains;
            terrains = terrains.Where(x => x.gameObject.activeSelf).ToArray();
            terrains = terrains.OrderBy(terrain => {
                                                    TerrainData terrainData = terrain.terrainData;
                                                    Vector3 terrainCenter = terrain.transform.position + terrainData.size / 2f;
                                                    return Vector3.Distance(terrainCenter, flattenedShipPosition);
                                                    }).ToArray();

            mainTerrain = terrains.Length > 0 ? terrains[0] : null;

            if (mainTerrain != null)
            {
                groundObject = mainTerrain.gameObject;
                return;
            }

            int decalLayerMask = 1 << 10; // Layer 10 (Decal) bitmask

            List<GameObject> groundCandidates = new List<GameObject>();
            foreach (string tag in groundTags)
            {
                groundCandidates.AddRange(GameObject.FindGameObjectsWithTag(tag));
            }
            
            groundCandidates = groundCandidates.Where(x => x.gameObject.activeSelf).ToList();
            // Filter by name (bad since names are inconsistent)
            // groundCandidates = groundCandidates.Where(x => x.name.ToLower().Contains("terrain")).ToList();

            // Exclude objects without a MeshRenderer or if they're not on the decal layer
            groundCandidates = groundCandidates.Where(x => {
                                                        if (x.TryGetComponent<MeshRenderer>(out MeshRenderer meshRenderer))
                                                        {
                                                            return (meshRenderer.renderingLayerMask & decalLayerMask) != 0;
                                                        }
                                                        return false; 
                                                        }).ToList();
            // Sort by distance to ship
            groundCandidates = groundCandidates.OrderBy(x => {
                                                        if (x.TryGetComponent<MeshRenderer>(out MeshRenderer meshRenderer))
                                                        {
                                                            return Vector3.Distance(meshRenderer.bounds.center, flattenedShipPosition); //tranform.position returns pivot point, bounds.center returns center of mesh
                                                        }
                                                        else
                                                        {
                                                            // Fallback: Move to the end of the list
                                                            return Mathf.Infinity; 
                                                        }
                                                        }).ToList();

            groundObject = groundCandidates.Count > 0 ? groundCandidates[0] : null;

            if (groundObject == null)
            {
                Debug.LogError("Could not find a suitable ground object for snowfall weather!");
            }
        }

        internal void ModifyRenderMasks()
        {
            List<GameObject> objectsAboveThreshold = GetObjectsAboveThreshold();

            foreach (GameObject obj in objectsAboveThreshold)
            {
                if (obj.TryGetComponent<MeshRenderer>(out MeshRenderer meshRenderer))
                {
                    meshRenderer.renderingLayerMask |= (uint)originalOverlayRenderingLayers;
                }
            }

            if (groundObject.TryGetComponent<MeshRenderer>(out MeshRenderer groundRenderer))
            {
                groundRenderer.renderingLayerMask |= (uint)snowVertexCustomPass.renderingLayers;
            }
            else if (groundObject.TryGetComponent<Terrain>(out Terrain terrain))
            {
                terrain.renderingLayerMask |= (uint)snowVertexCustomPass.renderingLayers;
            }
            else
            {
                Debug.LogError("Could not find a renderer on the ground object!");
            }

        }

        public List<GameObject> GetObjectsAboveThreshold()
        {
            GameObject dungeonAnchor = FindAnyObjectByType<RuntimeDungeon>().Root;
            // Weird way to obtain scene name without dependencies
            currentSceneName = dungeonAnchor.gameObject.scene.name;
            // Set threshold to 1/3 of distance from the ship to the top of the dungeon
            float heightThreshold = -Mathf.Abs(dungeonAnchor.transform.position.y/3); 
            LayerMask mask = LayerMask.GetMask("Default", "Room", "Terrain", "Foliage");
            List<GameObject> objectsAboveThreshold = new List<GameObject>();

            // Get the target scene
            Scene scene = SceneManager.GetSceneByName(currentSceneName);

            // Iterate through all root GameObjects in the scene
            foreach (GameObject rootGameObject in scene.GetRootGameObjects())
            {
                // Recursively search for objects with a Y position above the threshold
                FindObjectsAboveThresholdRecursive(rootGameObject.transform, objectsAboveThreshold, heightThreshold, mask);
            }

            Debug.Log("Found " + objectsAboveThreshold.Count + " suitable object to render snow on!");

            return objectsAboveThreshold;
        }

        private void FindObjectsAboveThresholdRecursive(Transform parent, List<GameObject> results, float heightThreshold, LayerMask mask)
        {
            if (parent.position.y > heightThreshold && mask == (mask | (1 << parent.gameObject.layer)))
            {
                results.Add(parent.gameObject);
            }

            foreach (Transform child in parent)
            {
                FindObjectsAboveThresholdRecursive(child, results, heightThreshold, mask);
            }
        }
    
        internal void RefreshFootprintTrackers(Dictionary <MonoBehaviour, VisualEffect> snowTrackersDict)
        {
            List<MonoBehaviour> keysToRemove = new List<MonoBehaviour>();

            // Filter out the null keys and set the remaining trackers to active
            foreach (var kvp in snowTrackersDict)
            {
                if (kvp.Key == null)
                {
                    keysToRemove.Add(kvp.Key); 
                }
                else
                {
                    kvp.Value.gameObject?.SetActive(true);
                }
            }

            // Remove the null keys
            foreach (var key in keysToRemove)
            {
                snowTrackersDict.Remove(key);
            }
        }

        internal void DisableFootprintTrackers(Dictionary <MonoBehaviour, VisualEffect> snowTrackersDict)
        {
            foreach (var kvp in snowTrackersDict)
            {
                kvp.Value.gameObject?.SetActive(false);
            }
        }
    }

    public class SnowfallVFXManager: MonoBehaviour
    {
        [SerializeField]
        internal SnowfallWeather snowfallWeather;
        [SerializeField]
        internal VisualEffect snowVFX;
        internal static GameObject footprintsTrackerVFX;
        internal static  GameObject lowcapFootprintsTrackerVFX;
        internal static  GameObject itemTrackerVFX;
        internal static  GameObject shovelVFX;
        [SerializeField]
        internal Volume frostyFilter;
        [SerializeField]
        internal Volume underSnowFilter;
        internal static float snowMovementHindranceMultiplier = 1f;
        internal static int snowFootstepIndex = -1;
        internal static float snowThickness = 0f;
        private float targetWeight = 0f;
        private float currentWeight = 0f;
        private float fadeSpeed = 2f; // Units per second
        private bool isFading = false;
        private bool isUnderSnowPreviousFrame = false;
        private HDAdditionalLightData sunLightData;

        internal void Start()
        {
            footprintsTrackerVFX = snowfallWeather.footprintsTrackerVFX;
            lowcapFootprintsTrackerVFX = snowfallWeather.lowcapFootprintsTrackerVFX;
            itemTrackerVFX = snowfallWeather.itemTrackerVFX;
            shovelVFX = snowfallWeather.shovelVFX;
            snowFootstepIndex = Array.FindIndex(StartOfRound.Instance.footstepSurfaces, surface => surface.surfaceTag == "Snow");
        }

        internal void OnEnable()
        {
            snowVFX.enabled = true;
            frostyFilter.enabled = true;
            underSnowFilter.enabled = true;
            snowfallWeather.snowVolume.enabled = true;
            snowfallWeather.snowTrackerCameraContainer.SetActive(true);
            sunLightData = TimeOfDay.Instance.sunDirect?.GetComponent<HDAdditionalLightData>();
            // if (sunLightData != null)
            // {
            //     sunLightData.lightUnit = LightUnit.Lux;
            // }
        }

        internal void OnDisable()
        {
            snowVFX.enabled = false;
            frostyFilter.enabled = false;
            underSnowFilter.enabled = false;
            snowfallWeather.snowVolume.enabled = false;
            snowfallWeather.snowTrackerCameraContainer.SetActive(false);
            snowMovementHindranceMultiplier = 1f;
            snowThickness = 0f;
        }

        internal void FixedUpdate()
        {   
            snowfallWeather.snowThicknessCalculator.CalculateThickness(); //TODO skip if not on natural ground
            // PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
            if (snowfallWeather.snowThicknessCalculator.isOnNaturalGround)
            {
                snowThickness = snowfallWeather.snowThicknessCalculator.snowThicknessData[0];
                float eyeBias = 0.3f;
                // White out the screen if the player is under snow
                float localPlayerEyeY = GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position.y;
                bool isUnderSnow = snowfallWeather.snowThicknessCalculator.snowPositionY >= localPlayerEyeY - eyeBias;

                if (isUnderSnow != isUnderSnowPreviousFrame)
                {
                    StartFade(isUnderSnow ? 1f : 0f);
                }

                isUnderSnowPreviousFrame = isUnderSnow;
                UpdateFade();

                // Slow down the player if they are in snow (only if snow thickness is above 0.4, caps at 2.5)
                snowMovementHindranceMultiplier = 1 + 5*Mathf.Clamp01((snowThickness - 0.4f)/2.1f);

                Debug.LogDebug($"Hindrance multiplier: {snowMovementHindranceMultiplier}, localPlayerEyeY: {localPlayerEyeY}, snowPositionY: {snowfallWeather.snowThicknessCalculator.snowPositionY}, isUnderSnow: {isUnderSnow}");
            }
            else
            {
                if (currentWeight > 0f)
                {
                    StartFade(0f);  // Fade to 0 if not on natural ground
                }
                UpdateFade(); // Continue updating the fade
                snowMovementHindranceMultiplier = 1f;
                snowThickness = 0f;
                Debug.LogDebug("Not on natural ground");
            }

            // Update the snow glow based on the sun intensity
            snowfallWeather.emissionMultiplier = sunLightData == null ? 0f : Mathf.Clamp01(sunLightData.intensity/40f)*0.3f;
        }

        private void StartFade(float target)
        {
            targetWeight = target;
            isFading = true;
        }

        private void UpdateFade()
        {
            if (isFading)
            {
                float weightDifference = targetWeight - currentWeight;
                float changeThisFrame = fadeSpeed * Time.deltaTime;

                if (Mathf.Abs(weightDifference) <= changeThisFrame)
                {
                    currentWeight = targetWeight;
                    isFading = false;
                }
                else
                {
                    currentWeight += Mathf.Sign(weightDifference) * changeThisFrame;
                }

                underSnowFilter.weight = currentWeight;
            }

        }
    }
}