using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Experimental.Rendering;
using VoxxWeatherPlugin.Behaviours;
using VoxxWeatherPlugin.Patches;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using DunGen;
using VoxxWeatherPlugin.Utils;
using GameNetcodeStuff;
using System;

namespace VoxxWeatherPlugin.Weathers
{
    public class SnowfallWeather: MonoBehaviour
    {
        public static SnowfallWeather Instance { get; private set; }

        [Header("Visuals")]
        [SerializeField]
        internal Material iceMaterial;
        [SerializeField]
        internal CustomPassVolume snowVolume;
        internal SnowRenderersCustomPass snowOverlayCustomPass;
        internal SnowRenderersCustomPass snowVertexCustomPass;
        [SerializeField]
        internal VisualEffect snowVFX;
        [SerializeField]
        internal Volume frostyFilter;
        [SerializeField]
        internal Volume underSnowFilter;

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
        internal uint PCFKernelSize = 6;
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
        public GameObject footprintsTrackerVFX;

        [Header("Tessellation Parameters")]
        [SerializeField]
        internal float baseTessellationFactor = 4.0f;
        [SerializeField]
        internal float maxTessellationFactor = 16.0f;

        [SerializeField]
        internal Vector3 shipPosition;
        [SerializeField]
        internal GameObject groundObject;
        internal string[] groundTags = {"Grass", "Gravel", "Snow", "Rock"};
        internal QuicksandTrigger[] waterObjects;

        internal SnowThicknessCalculator snowThicknessCalculator;
        internal System.Random seededRandom;

        internal void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

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

            levelDepthmapCamera.enabled = false; // Disable the camera to render manually

            levelDepthmap = new RenderTexture(2048, 
                                                2048,
                                                0, // Depth bits
                                                RenderTextureFormat.RHalf);
            levelDepthmap.wrapMode = TextureWrapMode.Clamp;
            levelDepthmap.useMipMap = false;
            levelDepthmap.enableRandomWrite = true;
            levelDepthmap.useDynamicScale = true;
            levelDepthmap.name = "Level Depthmap";
            levelDepthmap.Create();

            snowTracksMap = new RenderTexture(256,
                                            256,
                                            0, // Depth bits
                                            RenderTextureFormat.RHalf); 
            snowTracksMap.filterMode = FilterMode.Point;
            snowTracksMap.wrapMode = TextureWrapMode.Clamp;
            snowTracksMap.useMipMap = false;
            snowTracksMap.enableRandomWrite = true;
            snowTracksMap.useDynamicScale = true;
            snowTracksMap.name = "Snow Tracks Map";
            snowTracksMap.Create();


            levelDepthmapCamera.targetTexture = levelDepthmap;
            levelDepthmapCamera.aspect = 1.0f;
            
            levelDepthmapCamera.targetTexture = levelDepthmap;
            levelDepthmapCamera.aspect = 1.0f;
            snowTracksCamera.targetTexture = snowTracksMap;
            snowTracksCamera.aspect = 1.0f;
        }

        internal void OnEnable()
        {
            seededRandom = new System.Random(StartOfRound.Instance.randomMapSeed);
            
            maxSnowHeight = seededRandom.NextDouble(1.7f, 3f);
            snowScale = seededRandom.NextDouble(0.7f, 1.3f);
            maxSnowNormalizedTime = seededRandom.NextDouble(0.5f, 1f);

            snowThicknessCalculator.inputNeedsUpdate = true;
            FindGround();
            FreezeWater();
            SwitchTerrainInstancing(false);
            ModifyRenderMasks();
            RefreshFootprintTrackers();
            
            // Set the camera position to the center of the level
            (_ , Vector3 levelBarycenter) = PlayableAreaCalculator.CalculateZoneSize();
            Vector3 cameraPosition = new Vector3(levelBarycenter.x, levelDepthmapCamera.transform.position.y, levelBarycenter.z);
            levelDepthmapCamera.transform.position = cameraPosition;
            levelDepthmapCamera.enabled = false;
            levelDepthmapCamera.targetTexture = levelDepthmap;
            levelDepthmapCamera.aspect = 1.0f;
            levelDepthmapCamera.Render();
            levelDepthmapCamera.targetTexture = null;
            levelDepthmapCamera.enabled = true;
        }

        internal void OnDisable()
        {
            DisableFootprintTrackers();
        }

        internal void OnDestroy()
        {
            // Release the render textures
            levelDepthmap.Release();
            snowTracksMap.Release();
            SnowPatches.snowTrackersDict.Clear();
        }

        internal void FixedUpdate()
        {
            shipPosition = StartOfRound.Instance.shipBounds.bounds.center; //TODO find a better way to get the ship position. Done?
            snowIntensity = 10f * Mathf.Clamp01(maxSnowNormalizedTime - TimeOfDay.Instance.normalizedTimeOfDay);
        }

        internal void SwitchTerrainInstancing(bool enable)
        {
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
            waterObjects = GameObject.FindObjectsOfType<QuicksandTrigger>().Where(x => x.gameObject.activeSelf && x.isWater && !x.isInsideWater).ToArray();
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
                icePosition.y += 0.1f;
                waterObject.transform.position= icePosition;
                if (waterObject.TryGetComponent<Collider>(out Collider collider))
                {
                    collider.isTrigger = false;
                }
                waterObject.enabled = false; //disable sinking
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
            Vector3 flattenedShipPosition = new Vector3(shipPosition.x, 0, shipPosition.z);
            
            Terrain mainTerrain;

            Terrain[] terrains = Terrain.activeTerrains;
            terrains = terrains.Where(x => x.gameObject.activeSelf).ToArray();
            terrains = terrains.OrderBy(terrain => {
                                                    TerrainData terrainData = terrain.terrainData;
                                                    Vector3 terrainCenter = terrain.transform.position + terrainData.size / 2f;
                                                    return Vector3.Distance(terrainCenter, shipPosition);
                                                    }).ToArray();

            mainTerrain = terrains.Length > 0 ? terrains[0] : null;

            if (mainTerrain != null)
            {
                groundObject = mainTerrain.gameObject;
                return;
            }
            
            List<GameObject> groundCandidates = new List<GameObject>();
            foreach (string tag in groundTags)
            {
                groundCandidates.AddRange(GameObject.FindGameObjectsWithTag(tag));
            }
            
            groundCandidates = groundCandidates.Where(x => x.gameObject.activeSelf).ToList();
            // Filter by name???
            groundCandidates = groundCandidates.Where(x => x.name.ToLower().Contains("terrain")).ToList();
            groundCandidates = groundCandidates.OrderBy(x => {
                                                        if (x.TryGetComponent<MeshRenderer>(out MeshRenderer meshRenderer))
                                                        {
                                                            return Vector3.Distance(meshRenderer.bounds.center, shipPosition); //tranform.position returns pivot point, bounds.center returns center of mesh
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
                    meshRenderer.renderingLayerMask = (uint)snowOverlayCustomPass.renderingLayers;
                }
            }

            if (groundObject.TryGetComponent<MeshRenderer>(out MeshRenderer groundRenderer))
            {
                groundRenderer.renderingLayerMask = (uint)snowVertexCustomPass.renderingLayers;
            }
            else if (groundObject.TryGetComponent<Terrain>(out Terrain terrain))
            {
                terrain.renderingLayerMask = (uint)snowVertexCustomPass.renderingLayers;
            }
            else
            {
                Debug.LogError("Could not find a renderer on the ground object!");
            }

        }

        public List<GameObject> GetObjectsAboveThreshold()
        {
            RuntimeDungeon dungeon = GameObject.FindAnyObjectByType<RuntimeDungeon>();
            // Weird way to obtain scene name without dependencies
            string targetSceneName = dungeon.gameObject.scene.name;
            // Set threshold to 1/3 of distance from the ship to the top of the dungeon
            float heightThreshold = -Mathf.Abs(dungeon.transform.position.y/3); 
            LayerMask mask = LayerMask.GetMask("Default", "Room", "Terrain", "Foliage");
            List<GameObject> objectsAboveThreshold = new List<GameObject>();

            // Get the target scene
            Scene scene = SceneManager.GetSceneByName(targetSceneName);

            // Iterate through all root GameObjects in the scene
            foreach (GameObject rootGameObject in scene.GetRootGameObjects())
            {
                // Recursively search for objects with a Y position above the threshold
                FindObjectsAboveThresholdRecursive(rootGameObject.transform, objectsAboveThreshold, heightThreshold, mask);
            }

            Debug.Log("Found " + objectsAboveThreshold.Count + " objects above the threshold.");

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
    
        internal void RefreshFootprintTrackers()
        {
            List<MonoBehaviour> keysToRemove = new List<MonoBehaviour>();

            // Filter out the null keys and set the remaining trackers to active
            foreach (var kvp in SnowPatches.snowTrackersDict)
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
                SnowPatches.snowTrackersDict.Remove(key);
            }
        }

        internal void DisableFootprintTrackers()
        {
            foreach (var kvp in SnowPatches.snowTrackersDict)
            {
                kvp.Value.gameObject?.SetActive(false);
            }
        }
    }

    public class SnowfallVFXManager: MonoBehaviour
    {
        [SerializeField]
        internal SnowfallWeather snowfallWeather;
        internal static float snowMovementHindranceMultiplier = 1f;

        internal void OnEnable()
        {
            snowfallWeather.snowVFX.enabled = true;
            snowfallWeather.snowVolume.enabled = true;
            snowfallWeather.frostyFilter.enabled = true;
            snowfallWeather.underSnowFilter.enabled = true;
            snowfallWeather.snowTrackerCameraContainer.SetActive(true);
        }

        internal void OnDisable()
        {
            snowfallWeather.snowVFX.enabled = false;
            snowfallWeather.snowVolume.enabled = false;
            snowfallWeather.frostyFilter.enabled = false;
            snowfallWeather.underSnowFilter.enabled = false;
            snowfallWeather.snowTrackerCameraContainer.SetActive(false);
            snowMovementHindranceMultiplier = 1f;
        }

        internal void Update()
        {   
            // PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
            if (snowfallWeather.snowThicknessCalculator.isOnNaturalGround)
            {
                // White out the screen if the player is under snow
                float localPlayerEyeY = GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position.y;
                bool isUnderSnow = snowfallWeather.snowThicknessCalculator.snowPositionY >= localPlayerEyeY;
                snowfallWeather.underSnowFilter.weight = isUnderSnow ? 1f : 0f;
                // Slow down the player if they are in snow (only if snow thickness is above 0.4, caps at 2.5)
                snowMovementHindranceMultiplier = 1 + Mathf.Clamp01((snowfallWeather.snowThicknessCalculator.snowThicknessData[0] - 0.4f)/2.1f);

                Debug.Log($"Hindrance multiplier: {snowMovementHindranceMultiplier}, localPlayerEyeY: {localPlayerEyeY}, snowPositionY: {snowfallWeather.snowThicknessCalculator.snowPositionY}, isUnderSnow: {isUnderSnow}");
            }
            else
            {
                // snowfallWeather.underSnowFilter.weight = 0f;
                snowMovementHindranceMultiplier = 1f;
                Debug.Log("Not on natural ground");
            }
        }
    }
}