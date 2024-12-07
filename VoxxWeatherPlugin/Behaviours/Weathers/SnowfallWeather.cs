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
using System;
using System.Collections;
using LethalLib.Modules;

namespace VoxxWeatherPlugin.Weathers
{
    internal class SnowfallWeather: BaseWeather
    {
        public static SnowfallWeather? Instance { get; private set;}

        [Header("Snow Overlay Volume")]
        [SerializeField]
        internal CustomPassVolume? snowVolume;
        internal SnowOverlayCustomPass? snowOverlayCustomPass;
        [Header("Visuals")]
        [SerializeField]
        internal Material? snowOverlayMaterial;
        [SerializeField]
        internal Material? snowVertexMaterial;
        [SerializeField]
        internal Material? iceMaterial;
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
        internal Camera? levelDepthmapCamera;
        [SerializeField]
        internal RenderTexture? levelDepthmap;
        [SerializeField]
        internal uint PCFKernelSize = 9;
        [SerializeField]
        internal float shadowBias = 0.01f;
        [SerializeField]
        internal float snowOcclusionBias = 0.01f;

        [Header("Snow Tracks")]
        [SerializeField]
        internal GameObject? snowTrackerCameraContainer;
        [SerializeField]
        internal Camera? snowTracksCamera;
        [SerializeField]
        internal RenderTexture? snowTracksMap;

        [Header("Tessellation Parameters")]
        [SerializeField]
        internal float baseTessellationFactor = 4.0f;
        [SerializeField]
        internal float maxTessellationFactor = 16.0f;
        [SerializeField]
        internal int isAdaptiveTessellation = 0; // 0 for fixed tessellation, 1 for adaptive tessellation

        [Header("Baking Parameters")]
        [SerializeField]
        internal Material? bakeMaterial;
        [SerializeField]
        internal int bakeResolution = 1024;
        [SerializeField]
        internal float blurRadius = 3.0f;
        [SerializeField]
        internal Texture2DArray? snowMasks; // Texture2DArray to store the snow masks

        [Header("Terrain Mesh Post-Processing")]
        [SerializeField]
        internal float baseEdgeLength = 5;
        [SerializeField]
        internal bool useBounds = true;
        [SerializeField]
        internal bool subdivideMesh= true;
        [SerializeField]
        internal bool smoothMesh = true;
        [SerializeField]
        internal int smoothingIterations = 1;
        [SerializeField]
        internal bool replaceUvs = false;
        [SerializeField]
        internal bool constrainEdges = true;

        [Header("TerraMesh Parameters")]
        [SerializeField]
        internal Shader? terraMeshShader;
        [SerializeField]
        internal bool refineMesh = false;
        [SerializeField]
        internal bool carveHoles = false;
        [SerializeField]
        internal bool copyTrees = false;
        [SerializeField]
        internal bool copyDetail = false;
        [SerializeField]
        internal bool useMeshCollider = false;
        
        [SerializeField]
        internal int targetVertexCount = -1;
        [SerializeField]
        internal int minMeshStep = 1;
        [SerializeField]
        internal int maxMeshStep = 16;
        [SerializeField]
        internal float falloffSpeed = 2f;

        [Header("Ground")]
        internal bool swappedToSnow = false;

        [Header("General")]
        [SerializeField]
        internal Vector3 shipPosition;
        [SerializeField]
        internal float timeUntilFrostbite = 15f;
        [SerializeField]
        internal SnowfallVFXManager? VFXManager;
        [SerializeField]
        internal QuicksandTrigger[]? waterObjects; // TODO Remove
        [SerializeField]
        internal List<GameObject> groundObjectCandidates = new List<GameObject>(); // TODO Remove
        internal string currentSceneName = "None";
        internal Bounds levelBounds;
        internal System.Random? seededRandom;

        internal void Awake()
        {   
            Instance = this;

            snowOverlayCustomPass = snowVolume!.customPasses[0] as SnowOverlayCustomPass;
            snowOverlayCustomPass!.snowOverlayMaterial = snowOverlayMaterial;
            snowOverlayCustomPass.snowVertexMaterial = snowVertexMaterial;

            levelDepthmap = new RenderTexture(2048, 
                                            2048,
                                            0, // Depth bits
                                            RenderTextureFormat.RGHalf
                                            );

            levelDepthmap.wrapMode = TextureWrapMode.Clamp;
            levelDepthmap.filterMode = FilterMode.Trilinear;
            levelDepthmap.useMipMap = false;
            levelDepthmap.enableRandomWrite = true;
            levelDepthmap.useDynamicScale = true;
            levelDepthmap.name = "Level Depthmap";
            levelDepthmap.Create();
            // Set the camera target texture
            levelDepthmapCamera!.targetTexture = levelDepthmap;
            levelDepthmapCamera.aspect = 1.0f;
            levelDepthmapCamera.enabled = false;

            // DepthVSMPass? depthVSMPass = levelDepthmapCamera.GetComponent<CustomPassVolume>().customPasses[0] as DepthVSMPass;
            // depthVSMPass!.depthRenderTexture = levelDepthmap;

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
            snowTracksCamera!.targetTexture = snowTracksMap;
            snowTracksCamera.aspect = 1.0f;
        }

        internal virtual void OnEnable()
        {
            Instance = this; // Change the global reference to this instance (for patches)

            seededRandom = new System.Random(StartOfRound.Instance.randomMapSeed);
            
            levelBounds = PlayableAreaCalculator.CalculateZoneSize();
            
            maxSnowHeight = seededRandom.NextDouble(1.7f, 3f);
            snowScale = seededRandom.NextDouble(0.7f, 1.3f);
            maxSnowNormalizedTime = seededRandom.NextDouble(0.5f, 1f);
            swappedToSnow = false;
            ModifyRenderMasks();
            FindAndSetupGround();
            FreezeWater();
            ModifyScrollingFog();
            UpdateLevelDepthmap();
            VFXManager?.PopulateLevelWithVFX();
            // StartCoroutine(RefreshLevelDepthmapCoroutine(true));
        }

        internal virtual void OnDisable()
        {
            Destroy(snowMasks);
            VFXManager?.Reset();
        }

        internal void OnDestroy()
        {
            // Release the render textures
            levelDepthmap?.Release();
            snowTracksMap?.Release();
        }

        internal virtual void FixedUpdate()
        {
            shipPosition = StartOfRound.Instance.shipBounds.bounds.center;
            float normalizedSnowTimer =  Mathf.Clamp01(maxSnowNormalizedTime - TimeOfDay.Instance.normalizedTimeOfDay);
            snowIntensity = 10f * normalizedSnowTimer;
            // if (!swappedToSnow && normalizedSnowTimer <= maxSnowNormalizedTime/3f) //TODO Remove if the patch works
            // {
            //     foreach (KeyValuePair<GameObject, int> entry in SnowThicknessManager.Instance!.groundToIndex)
            //     {
            //         entry.Key.tag = "Snow";
            //     }
            //     swappedToSnow = true;
            // }
            UpdateCameraPosition(snowTrackerCameraContainer, snowTracksCamera);
        }

        internal void UpdateCameraPosition(GameObject? cameraContainer, Camera? camera)
        {
            if (cameraContainer == null || camera == null)
            {
                Debug.LogError("Camera container, camera or render texture is null!");
                return;
            }
            Vector3 playerPosition = GameNetworkManager.Instance.localPlayerController.transform.position;

            if (Vector3.Distance(playerPosition, cameraContainer.transform.position) >= camera.orthographicSize / 2f)
            {
                cameraContainer.transform.position = playerPosition;
            }

        }

        internal void UpdateLevelDepthmap()
        {
            Debug.LogDebug("Updating level depthmap");

            Vector3 cameraPosition = new Vector3(levelBounds.center.x,
                                                levelDepthmapCamera!.transform.position.y,
                                                levelBounds.center.z);
            levelDepthmapCamera.transform.position = cameraPosition;
            Debug.LogDebug($"Camera position: {levelDepthmapCamera.transform.position}, Barycenter: {levelBounds.center}");
            
            StartCoroutine(RefreshDepthmapCoroutine(levelDepthmapCamera, levelDepthmap!, bakeSnowMaps: true));

            Debug.LogDebug("Masks and level depthmap rendered!");
        }

        internal IEnumerator RefreshDepthmapCoroutine(Camera camera, RenderTexture renderTexture, bool bakeSnowMaps = false, bool waitForLanding = false)
        {
            if (waitForLanding)
            {
                yield return new WaitUntil(() => StartOfRound.Instance.shipHasLanded);
            }

            camera.targetTexture = renderTexture;
            camera.aspect = 1.0f;
            camera.enabled = true;
            
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            
            camera.targetTexture = null;
            camera.enabled = false;

            if (bakeSnowMaps)
            {
                BakeSnowMasks();
            }

            SnowThicknessManager.Instance!.inputNeedsUpdate = true;
        }

        internal void FreezeWater()
        {
            waterObjects = FindObjectsOfType<QuicksandTrigger>().Where(x => x.gameObject.activeSelf && x.isWater && x.gameObject.scene.name == currentSceneName).ToArray(); //&& !x.isInsideWater
            HashSet<GameObject> iceObjects = new HashSet<GameObject>();

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
                // Rise slightly above the water plane
                icePosition.y += 1f;
                waterObject.transform.position = icePosition;
                if (waterObject.TryGetComponent<Collider>(out Collider collider))
                {
                    collider.isTrigger = false;
                }
                // Disable sinking
                waterObject.enabled = false;
                // Change footstep sounds
                waterObject.gameObject.tag = "Rock"; // TODO Check why this is not working
                iceObjects.Add(waterObject.gameObject);
            }
            // Store the ice objects
            SnowThicknessManager.Instance!.iceObjects = iceObjects;
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

        internal void FindAndSetupGround()
        {
            // MAKE THIS PARALLELILIZABLE

            Dictionary <GameObject, int> groundToIndex = new Dictionary<GameObject, int>(); 

            Terrain[] terrains = Terrain.activeTerrains;
            if (terrains.Length > 0)
            {
                foreach (Terrain terrain in terrains)
                {
                    // Turn the terrain into a mesh
                    GameObject meshTerrain = terrain.Meshify(this, levelBounds);
                    // Setup the Lit terrain material
                    Material terrainMaterial = meshTerrain.GetComponent<MeshRenderer>().sharedMaterial;
                    terrainMaterial.SetupMaterialFromTerrain(terrain);
                    // Add the terrain to the dictionary
                    PrepareMeshForSnow(meshTerrain, groundToIndex);
                    if (!useMeshCollider)
                    {
                        groundToIndex.Add(terrain.gameObject, groundToIndex.Count);
                    }
                }
            }

            // Process possible mesh terrains to render snow on
            if (groundObjectCandidates.Count > 0)
            {
                foreach (GameObject meshTerrain in groundObjectCandidates)
                {
                    // Process the mesh to remove thin triangles and smooth the mesh
                    // TODO ADD OPTION TO FILTER BY LEVEL NAME
                    meshTerrain.PostprocessMeshTerrain(levelBounds, this);
                    // Add the terrain to the dictionary
                    PrepareMeshForSnow(meshTerrain, groundToIndex);
                }
            }
            
            // Store the ground objects mapping
            SnowThicknessManager.Instance!.groundToIndex = groundToIndex;
        }

        internal void PrepareMeshForSnow(GameObject meshTerrain, Dictionary <GameObject, int> groundToIndex)
        {
            // Duplicate the mesh and set the snow vertex material
            GameObject snowGround = meshTerrain.Duplicate(disableShadows: true, removeCollider: true);
            MeshRenderer meshRenderer = snowGround.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = snowVertexMaterial;
            // Deselect snow OVERLAY rendering layers from vertex snow objects
            meshRenderer.renderingLayerMask &= ~(uint)snowOverlayCustomPass!.renderingLayers;
            // Upload the mesh to the GPU to save RAM
            snowGround.GetComponent<MeshFilter>().sharedMesh.UploadMeshData(true);
            // Override the material property block to set the object ID to sample from a single Texture2DArray
            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetFloat("_TexIndex", groundToIndex.Count);
            meshRenderer.SetPropertyBlock(propertyBlock);
            // Add the terrain and material to the dictionary
            groundToIndex.Add(meshTerrain, groundToIndex.Count);
        }

        internal void BakeSnowMasks()
        {
            // Bake the snow masks into a Texture2DArray
            snowMasks = new Texture2DArray(bakeResolution,
                                           bakeResolution,
                                           SnowThicknessManager.Instance!.groundToIndex.Count,
                                           TextureFormat.RGBAFloat,
                                           false, // TODO: Maybe optionally allow mipmaps (also see .Apply)
                                           false,
                                           true);
            snowMasks.filterMode = FilterMode.Trilinear;
            snowMasks.wrapMode = TextureWrapMode.Clamp;
            
            foreach (KeyValuePair<GameObject, int> entry in SnowThicknessManager.Instance.groundToIndex)
            {
                entry.Key.BakeMask(this, entry.Value);
            }

            snowMasks.Apply(updateMipmaps:false, makeNoLongerReadable: true); // Move to the GPU

            snowVertexMaterial?.SetTexture("_SnowMaskTex", snowMasks);
        }

        internal void ModifyRenderMasks()
        {
            List<GameObject> objectsAboveThreshold = GetObjectsAboveThreshold();

            foreach (GameObject obj in objectsAboveThreshold)
            {
                if (obj.TryGetComponent<MeshRenderer>(out MeshRenderer meshRenderer))
                {
                    meshRenderer.renderingLayerMask |= (uint)snowOverlayCustomPass!.renderingLayers;
                }
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

            // Reset the list of ground objects to find possible mesh terrains to render snow on
            groundObjectCandidates = new List<GameObject>();

            // Iterate through all root GameObjects in the scene
            foreach (GameObject rootGameObject in scene.GetRootGameObjects())
            {
                // Recursively search for objects with a Y position above the threshold
                FindObjectsAboveThresholdRecursive(rootGameObject.transform, objectsAboveThreshold, heightThreshold, mask);
            }

            Debug.LogDebug("Found " + objectsAboveThreshold.Count + " suitable object to render snow on!");

            return objectsAboveThreshold;
        }

        private void FindObjectsAboveThresholdRecursive(Transform parent, List<GameObject> results, float heightThreshold, LayerMask mask)
        {
            //Try to find possible mesh terrain objects to render snow on
            if (parent.gameObject.activeInHierarchy && CheckIfObjectIsTerrain(parent.gameObject))
            {
                groundObjectCandidates.Add(parent.gameObject);
            }

            if (parent.position.y > heightThreshold && mask == (mask | (1 << parent.gameObject.layer)))
            {
                results.Add(parent.gameObject);
            }

            foreach (Transform child in parent)
            {
                FindObjectsAboveThresholdRecursive(child, results, heightThreshold, mask);
            }
        }

        private bool CheckIfObjectIsTerrain(GameObject obj)
        {
            bool isTagMatched = false;

            foreach (string tag in SnowThicknessManager.Instance!.groundTags)
            {
                if (obj.CompareTag(tag))
                {
                    isTagMatched = true;
                    break;
                }
            }
            
            // Exit early if the object does not match the defined tags
            if (!isTagMatched)
            {
                return false;
            }

            string nameString;

            bool isTerrainInName = obj.name.ToLower().Contains("terrain");
            bool isOutOfBoundsInName = obj.name.ToLower().Contains("outofbounds");

            bool isTerrainInMesh = false;
            bool isOutOfBoundsInMesh = false;

            if (obj.TryGetComponent<MeshCollider>(out MeshCollider collider))
            {
                if (collider.sharedMesh != null)
                {
                    //If mesh has less than 10k vertices, it's most likely not a terrain mesh
                    if (collider.sharedMesh.vertexCount < 10000)
                    {
                        return false;
                    }

                    nameString = collider.sharedMesh.name.ToLower();
                    isTerrainInMesh = nameString.Contains("terrain");
                    isOutOfBoundsInMesh = nameString.Contains("outofbounds");
                }
            }

            bool isTerrainInMaterial = false;
            bool isOutOfBoundsInMaterial = false;
            bool isDecalLayerMatched = false;
            int decalLayerMask = 1 << 10; // Layer 10 (Quicksand Decal) bitmask

            if (obj.TryGetComponent<MeshRenderer>(out MeshRenderer meshRenderer))
            {
                if (meshRenderer.sharedMaterial != null)
                {
                    nameString = meshRenderer.sharedMaterial.name.ToLower();
                    isTerrainInMaterial = nameString.Contains("terrain");
                    isOutOfBoundsInMaterial = nameString.Contains("outofbounds");
                    isDecalLayerMatched = (meshRenderer.renderingLayerMask & decalLayerMask) != 0;
                }
            }

            return  (isTerrainInName || isTerrainInMaterial || isTerrainInMesh) &&
                    !(isOutOfBoundsInName || isOutOfBoundsInMaterial || isOutOfBoundsInMesh) ||
                    isDecalLayerMatched;
        }

        internal void RefreshBakeMaterial()
        {
            // Set shader properties
            bakeMaterial?.SetFloat("_SnowNoiseScale", snowScale);
            bakeMaterial?.SetFloat("_ShadowBias", shadowBias);
            bakeMaterial?.SetFloat("_PCFKernelSize", PCFKernelSize);
            bakeMaterial?.SetFloat("_BlurKernelSize", blurRadius);
            bakeMaterial?.SetFloat("_SnowOcclusionBias", snowOcclusionBias);
            bakeMaterial?.SetVector("_ShipLocation", shipPosition);
            
            if (levelDepthmap != null)
            {
                bakeMaterial?.SetTexture("DepthTex", levelDepthmap);
            }

            // Set projection matrix from camera
            if (levelDepthmapCamera != null)
            {
                Matrix4x4 viewMatrix = levelDepthmapCamera.worldToCameraMatrix;
                Matrix4x4 projMatrix = levelDepthmapCamera.projectionMatrix;
                bakeMaterial?.SetMatrix("_LightViewProjection", projMatrix * viewMatrix);
            }
        }
    }

    public class SnowfallVFXManager: BaseVFXManager
    {
        [SerializeField]
        internal GameObject? snowVFXContainer;

        [SerializeField]
        internal Volume? frostbiteFilter;
        [SerializeField]
        internal Volume? frostyFilter;
        [SerializeField]
        internal Volume? underSnowFilter;
        internal static float snowMovementHindranceMultiplier = 1f;
        internal static int snowFootstepIndex = -1;
        internal static float snowThickness = 0f;
        private float targetWeight = 0f;
        private float currentWeight = 0f;
        private float fadeSpeed = 2f; // Units per second
        private bool isFading = false;
        private bool isUnderSnowPreviousFrame = false;
        private HDAdditionalLightData? sunLightData;

        [Header("Snow Tracker VFX")]
        
        [SerializeField]
        internal VisualEffectAsset[]? footprintsTrackerVFX;
        internal static Dictionary <string, VisualEffectAsset>? snowTrackersDict;

        internal void Start()
        {
            snowFootstepIndex = Array.FindIndex(StartOfRound.Instance.footstepSurfaces, surface => surface.surfaceTag == "Snow");
            PlayerTemperatureManager.freezeEffectVolume = frostbiteFilter;
        }

        internal virtual void OnEnable()
        {
            snowVFXContainer?.SetActive(true);
            
            frostbiteFilter!.enabled = true;
            frostyFilter!.enabled = true;
            underSnowFilter!.enabled = true;
            
            SnowfallWeather.Instance!.snowVolume!.enabled = true;
            SnowfallWeather.Instance.snowTrackerCameraContainer?.SetActive(true);
            sunLightData = TimeOfDay.Instance.sunDirect?.GetComponent<HDAdditionalLightData>();
            // if (sunLightData != null)
            // {
            //     sunLightData.lightUnit = LightUnit.Lux;
            // }
        }

        internal virtual void OnDisable()
        {
            snowVFXContainer?.SetActive(false);
            // frostbiteFilter!.enabled = false;
            frostyFilter!.enabled = false;
            underSnowFilter!.enabled = false;
            SnowfallWeather.Instance!.snowVolume!.enabled = false;
            SnowfallWeather.Instance.snowTrackerCameraContainer?.SetActive(false);
            snowMovementHindranceMultiplier = 1f;
            snowThickness = 0f;
            PlayerTemperatureManager.isInColdZone = false;
            isUnderSnowPreviousFrame = false;
        }

        internal void FixedUpdate()
        {   
            SnowThicknessManager.Instance!.CalculateThickness(); 
            
            if (SnowThicknessManager.Instance.isOnNaturalGround)
            {
                snowThickness = SnowThicknessManager.Instance.GetSnowThickness(GameNetworkManager.Instance.localPlayerController);
                float eyeBias = 0.3f;
                // White out the screen if the player is under snow
                float localPlayerEyeY = GameNetworkManager.Instance.localPlayerController.playerEye.position.y;
                bool isUnderSnow = SnowThicknessManager.Instance.feetPosition.y + snowThickness >= localPlayerEyeY - eyeBias;

                if (isUnderSnow != isUnderSnowPreviousFrame)
                {
                    StartFade(isUnderSnow ? 1f : 0f);
                }

                isUnderSnowPreviousFrame = isUnderSnow;
                UpdateFade();

                // Slow down the player if they are in snow (only if snow thickness is above 0.4, caps at 2.5 height)
                snowMovementHindranceMultiplier = 1 + 5*Mathf.Clamp01((snowThickness - 0.4f)/2.1f);

                Debug.LogDebug($"Hindrance multiplier: {snowMovementHindranceMultiplier}, localPlayerEyeY: {localPlayerEyeY}, isUnderSnow: {isUnderSnow}");
            }
            else
            {
                if (currentWeight > 0f)
                {
                    StartFade(0f);  // Fade to 0 if not on natural ground
                }
                UpdateFade(); // Continue updating the fade
                isUnderSnowPreviousFrame = false;
                snowMovementHindranceMultiplier = 1f;
                snowThickness = 0f;
                Debug.LogDebug("Not on natural ground");
            }
            
            PlayerTemperatureManager.isInColdZone = isUnderSnowPreviousFrame;
            if (PlayerTemperatureManager.isInColdZone)
            {
                PlayerTemperatureManager.SetPlayerTemperature(-Time.fixedDeltaTime / SnowfallWeather.Instance!.timeUntilFrostbite);
            }
            // Update the snow glow based on the sun intensity
            SnowfallWeather.Instance!.emissionMultiplier = sunLightData == null ? 0f : Mathf.Clamp01(sunLightData.intensity/40f)*0.3f;
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

                underSnowFilter!.weight = currentWeight;
            }
        }

        internal void DisableFootprintTrackers(Dictionary <MonoBehaviour, VisualEffect> snowTrackersDict)
        {
            foreach (var kvp in snowTrackersDict)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.gameObject.SetActive(false);
                }
            }
        }

        internal override void Reset()
        {
            PlayerTemperatureManager.isInColdZone = false;
            SnowThicknessManager.Instance?.Reset();
            DisableFootprintTrackers(SnowPatches.snowTrackersDict);
            DisableFootprintTrackers(SnowPatches.snowShovelDict);
        }

        internal override void PopulateLevelWithVFX(Bounds levelBounds = default, System.Random? seededRandom = null)
        {
            if (snowVFXContainer == null)
            {
                Debug.LogError("Snow VFX container is null!");
                return;
            }
            HDRPCameraOrTextureBinder depthBinder = snowVFXContainer.GetComponent<HDRPCameraOrTextureBinder>();
            depthBinder.depthTexture = SnowfallWeather.Instance!.levelDepthmap; // bind the baked depth texture
        }

        internal void OnDestroy()
        {
            SnowPatches.snowTrackersDict.Clear();
            SnowPatches.snowShovelDict.Clear();
        }
    }
}