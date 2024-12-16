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
using GameNetcodeStuff;

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
        internal int depthmapResolution = 2048;
        [SerializeField]
        internal uint PCFKernelSize = 12;
        [SerializeField]
        internal float shadowBias = 0.001f;
        [SerializeField]
        internal float snowOcclusionBias = 0.005f;
        internal Matrix4x4? depthWorldToClipMatrix;

        [Header("Snow Tracks")]
        [SerializeField]
        internal GameObject? snowTrackerCameraContainer;
        [SerializeField]
        internal Camera? snowTracksCamera;
        [SerializeField]
        internal RenderTexture? snowTracksMap;
        [SerializeField]
        internal int tracksMapResolution = 256;
        internal Matrix4x4? tracksWorldToClipMatrix;

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
        internal bool bakeMipmaps = false;

        [Header("Terrain Mesh Post-Processing")]
        [SerializeField]
        internal float baseEdgeLength = 5; // The target edge length for the mesh refinement
        [SerializeField]
        internal bool useBounds = true; // Use the level bounds to filter out-of-bounds vertices
        [SerializeField]
        internal bool subdivideMesh = true;
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

        [Header("General")]
        [SerializeField]
        internal Vector3 shipPosition;
        [SerializeField]
        internal float timeUntilFrostbite = 15f;
        [SerializeField]
        internal SnowfallVFXManager? VFXManager;
        [SerializeField]
        internal QuicksandTrigger[]? waterTriggerObjects;
        [SerializeField]
        internal List<GameObject> waterSurfaceObjects = new List<GameObject>();
        [SerializeField]
        internal List<GameObject> groundObjectCandidates = new List<GameObject>();
        internal string currentLevelName => StartOfRound.Instance?.currentLevel.sceneName ?? "";
        internal Bounds levelBounds;
        internal System.Random? seededRandom;
        private HDAdditionalLightData? sunLightData;

#if DEBUG

        public bool rebakeMaps = false;

#endif

        internal void Awake()
        {   
            Instance = this;

            // Restore shaders (otherwise there might be issues with incorrectly set flags)
            // snowOverlayMaterial?.RestoreShader();
            // snowVertexMaterial?.RestoreShader();

            snowOverlayCustomPass = snowVolume!.customPasses[0] as SnowOverlayCustomPass;
            snowOverlayCustomPass!.snowOverlayMaterial = snowOverlayMaterial;
            snowOverlayCustomPass.snowVertexMaterial = snowVertexMaterial;

            levelDepthmap = new RenderTexture(depthmapResolution, 
                                            depthmapResolution,
                                            0, // Depth bits
                                            RenderTextureFormat.RGHalf
                                            );
            
            levelDepthmap.dimension = TextureDimension.Tex2DArray; // Single layer array for compat with VFX Graph binding
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

            snowTracksMap = new RenderTexture(tracksMapResolution,
                                            tracksMapResolution,
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
            sunLightData = TimeOfDay.Instance.sunDirect?.GetComponent<HDAdditionalLightData>();
            levelBounds = PlayableAreaCalculator.CalculateZoneSize(1.5f);
            
            maxSnowHeight = seededRandom.NextDouble(1.7f, 3f);
            snowScale = seededRandom.NextDouble(0.7f, 1.3f);
            maxSnowNormalizedTime = seededRandom.NextDouble(0.5f, 1f);
            ModifyRenderMasks();
            FindAndSetupGround();
            FreezeWater();
            ModifyScrollingFog();
            UpdateLevelDepthmap();
            VFXManager?.PopulateLevelWithVFX();
            StartCoroutine(RefreshDepthmapCoroutine(levelDepthmapCamera!, levelDepthmap!, bakeSnowMaps: false, waitForLanding: true));
        }

        internal virtual void OnDisable()
        {
            Destroy(snowMasks);
            groundObjectCandidates.Clear();
            waterSurfaceObjects.Clear();
            VFXManager?.Reset();
        }

        internal void OnDestroy()
        {
            // Release the render textures
            levelDepthmap?.Release();
            snowTracksMap?.Release();
        }

        internal virtual void Update()
        {
            // Update for moving ship and compat for different ship landing positions
            shipPosition = StartOfRound.Instance.shipBounds.bounds.center;
            // Accumulate snow on the ground
            float normalizedSnowTimer =  Mathf.Clamp01(maxSnowNormalizedTime - TimeOfDay.Instance.normalizedTimeOfDay);
            snowIntensity = 10f * normalizedSnowTimer;
            // Update the snow glow based on the sun intensity
            float sunIntensity = sunLightData?.intensity ?? 0f;
            emissionMultiplier = Mathf.Clamp01(sunIntensity/40f)*0.3f;
            UpdateCameraPosition(snowTrackerCameraContainer, snowTracksCamera);
            SetColdZoneState();
            // Update the snow thickness (host must constantly update for enemies, clients only when not in factory)
            if (GameNetworkManager.Instance.isHostingGame || !GameNetworkManager.Instance.localPlayerController.isInsideFactory)
            {
                SnowThicknessManager.Instance!.CalculateThickness(); // Could be moved to FixedUpdate to save performance?
            }

#if DEBUG
            if (rebakeMaps)
            {
                rebakeMaps = false;
                StartCoroutine(RefreshDepthmapCoroutine(levelDepthmapCamera!, levelDepthmap!, bakeSnowMaps: true));
            }
#endif
        }

        internal virtual void SetColdZoneState()
        {
            PlayerTemperatureManager.isInColdZone = VFXManager!.isUnderSnowPreviousFrame;
        }

        internal void UpdateCameraPosition(GameObject? cameraContainer, Camera? camera)
        {
            if (cameraContainer == null || camera == null)
            {
                Debug.LogError("Camera container, camera or render texture is null!");
                return;
            }
            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
            // Update the camera position to follow the player or the spectated player
            Vector3 playerPosition = localPlayer.isPlayerDead ? localPlayer.spectatedPlayerScript?.transform.position ?? default : localPlayer.transform.position;

            if (Vector3.Distance(playerPosition, cameraContainer.transform.position) >= camera.orthographicSize / 2f)
            {
                cameraContainer.transform.position = playerPosition;
                tracksWorldToClipMatrix = camera.projectionMatrix * camera.worldToCameraMatrix;
            }

        }

        internal void UpdateLevelDepthmap()
        {
            Debug.LogDebug("Updating level depthmap");

            Vector3 cameraPosition = new Vector3(levelBounds.center.x,
                                                levelDepthmapCamera!.transform.position.y,
                                                levelBounds.center.z);
            levelDepthmapCamera.transform.position = cameraPosition;
            depthWorldToClipMatrix = levelDepthmapCamera.projectionMatrix * levelDepthmapCamera.worldToCameraMatrix;
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
            waterTriggerObjects = FindObjectsOfType<QuicksandTrigger>().Where(x => x.gameObject.activeSelf && x.isWater && x.gameObject.scene.name == currentLevelName).ToArray(); //&& !x.isInsideWater TODO
            HashSet<GameObject> iceObjects = new HashSet<GameObject>();

            foreach (QuicksandTrigger waterObject in waterTriggerObjects)
            {
                // Disable sinking
                waterObject.enabled = false;
            }
            foreach (GameObject waterSurface in waterSurfaceObjects)
            {
                MeshFilter meshFilter = waterSurface.GetComponent<MeshFilter>();
                Mesh meshCopy = meshFilter.sharedMesh.MakeReadableCopy();
                meshFilter.sharedMesh = meshCopy;
                //Check for collider and if it exists, consider it a false positive, otherwise add a mesh collider
                if (!waterSurface.TryGetComponent<Collider>(out Collider collider))
                {
                    MeshCollider meshCollider = waterSurface.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = meshCopy;
                }
                else continue;

                //Get renderer component, we know it exists since we checked it in the CheckIfObjectIsTerrain method
                Renderer renderer = waterSurface.GetComponent<Renderer>();
                renderer.sharedMaterial = iceMaterial;

                // Rise slightly
                waterSurface.transform.position += 0.75f*Vector3.up;
                
                // Change footstep sounds
                waterSurface.tag = "Rock";
                iceObjects.Add(waterSurface);
            }
            // Store the ice objects
            SnowThicknessManager.Instance!.iceObjects = iceObjects;
        }

        internal void ModifyScrollingFog()
        {
            LocalVolumetricFog[] fogArray = GameObject.FindObjectsOfType<LocalVolumetricFog>();
            fogArray = fogArray.Where(x => x.gameObject.activeSelf && x.gameObject.scene.name == currentLevelName).ToArray();
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
            // TODO: PARALLELIZE THIS

            // Stores mesh terrains and actual Unity terrains to keep track of of walkable ground objects and their texture index in the baked masks
            Dictionary <GameObject, int> groundToIndex = new Dictionary<GameObject, int>(); 
            
            int textureIndex = 0;
            // Process possible mesh terrains to render snow on
            foreach (GameObject meshTerrain in groundObjectCandidates)
            {
                // Process the mesh to remove thin triangles and smooth the mesh
                // TODO ADD OPTION TO FILTER BY LEVEL NAME
                meshTerrain.PostprocessMeshTerrain(levelBounds, this);
                // Setup the index in the material property block for the snow masks
                PrepareMeshForSnow(meshTerrain, textureIndex);
                // Add the terrain to the dictionary
                groundToIndex.Add(meshTerrain, textureIndex);

                textureIndex++;
            }
            
            Terrain[] terrains = Terrain.activeTerrains;
            
            foreach (Terrain terrain in terrains)
            {
                //Check if terrain data is null
                if (terrain.terrainData == null)
                {
                    continue;
                }
                // Turn the terrain into a mesh
                GameObject meshTerrain = terrain.Meshify(this, useBounds ? levelBounds : null);
                // Setup the Lit terrain material
                Material terrainMaterial = meshTerrain.GetComponent<MeshRenderer>().sharedMaterial;
                terrainMaterial.SetupMaterialFromTerrain(terrain);
                // Setup the index in the material property block for the snow masks
                PrepareMeshForSnow(meshTerrain, textureIndex);
                if (useMeshCollider)
                {
                    // Use mesh terrain for snow thickness calculation
                    groundToIndex.Add(meshTerrain, textureIndex);
                }
                else
                {
                    // Use terrain collider for snow thickness calculation
                    groundToIndex.Add(terrain.gameObject, textureIndex);
                }
                
                // Store the mesh terrain in the list for baking later
                groundObjectCandidates.Add(meshTerrain);

                textureIndex++;
            }

            // Store the ground objects mapping
            SnowThicknessManager.Instance!.groundToIndex = groundToIndex;
        }

        internal void PrepareMeshForSnow(GameObject meshTerrain, int texId)
        {
            // Duplicate the mesh and set the snow vertex material
            GameObject snowGround = meshTerrain.Duplicate(disableShadows: true, removeCollider: true);
            MeshRenderer meshRenderer = snowGround.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = snowVertexMaterial;
            // Deselect snow OVERLAY rendering layers from vertex snow objects
            meshRenderer.renderingLayerMask &= ~(uint)snowOverlayCustomPass!.renderingLayers;
            // Upload the mesh to the GPU to save RAM. TODO: Prevents NavMesh baking
            // snowGround.GetComponent<MeshFilter>().sharedMesh.UploadMeshData(true);
            // Override the material property block to set the object ID to sample from a single Texture2DArray
            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetFloat(SnowfallShaderIDs.TexIndex, texId);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        internal void BakeSnowMasks()
        {
            //TODO PARALLELIZE THIS

            if (groundObjectCandidates.Count == 0)
            {
                Debug.LogDebug("No ground objects to bake snow masks for!");
                return;
            }

            // Bake the snow masks into a Texture2DArray
            snowMasks = new Texture2DArray(bakeResolution,
                                           bakeResolution,
                                           groundObjectCandidates.Count,
                                           TextureFormat.RGBAFloat,
                                           bakeMipmaps,
                                           false,
                                           true);
            snowMasks.filterMode = FilterMode.Trilinear;
            snowMasks.wrapMode = TextureWrapMode.Clamp;
            
            for (int texIndex = 0; texIndex < groundObjectCandidates.Count; texIndex++)
            {
                groundObjectCandidates[texIndex].BakeMask(this, texIndex);
            }

            snowMasks.Apply(updateMipmaps: bakeMipmaps, makeNoLongerReadable: true); // Move to the GPU

            snowVertexMaterial?.SetTexture(SnowfallShaderIDs.SnowMasks, snowMasks);
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
            // Set threshold to 1/3 of distance from the ship to the top of the dungeon
            float heightThreshold = -Mathf.Abs(dungeonAnchor.transform.position.y/3); 

            LayerMask mask = LayerMask.GetMask("Default", "Room", "Terrain", "Foliage");
            List<GameObject> objectsAboveThreshold = new List<GameObject>();

            // Get the target scene
            Scene scene = SceneManager.GetSceneByName(currentLevelName);

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
            bool IsValidTerrain = parent.gameObject.activeInHierarchy && CheckIfObjectIsTerrain(parent.gameObject);
            //Try to find possible mesh terrain objects to render snow on
            if (IsValidTerrain)
            {
                groundObjectCandidates.Add(parent.gameObject);
            }

            if (parent.position.y > heightThreshold && mask == (mask | (1 << parent.gameObject.layer)) || IsValidTerrain)
            {
                results.Add(parent.gameObject);
            }

            foreach (Transform child in parent)
            {
                FindObjectsAboveThresholdRecursive(child, results, heightThreshold, mask);
            }
        }

        // This method checks if an object is a terrain object based on its name, material or mesh and ALSO collects water surface objects
        private bool CheckIfObjectIsTerrain(GameObject obj)
        {
            if (!obj.activeInHierarchy)
            {
                return false;
            }

            string nameString;

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

                    // Collect water surface objects
                    if (nameString.Contains("water"))
                    {
                        waterSurfaceObjects.Add(obj);
                    }
                }
            }
            else
            {
                return false;
            }
            
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
            else
            {
                return false;
            }

            return  (isTerrainInName || isTerrainInMaterial || isTerrainInMesh) &&
                    !(isOutOfBoundsInName || isOutOfBoundsInMaterial || isOutOfBoundsInMesh) ||
                    isDecalLayerMatched;
        }

        internal void RefreshBakeMaterial()
        {
            // Set shader properties
            bakeMaterial?.SetFloat(SnowfallShaderIDs.SnowNoiseScale, snowScale);
            bakeMaterial?.SetFloat(SnowfallShaderIDs.ShadowBias, shadowBias);
            bakeMaterial?.SetFloat(SnowfallShaderIDs.PCFKernelSize, PCFKernelSize);
            bakeMaterial?.SetFloat(SnowfallShaderIDs.BlurKernelSize, blurRadius);
            bakeMaterial?.SetFloat(SnowfallShaderIDs.SnowOcclusionBias, snowOcclusionBias);
            bakeMaterial?.SetVector(SnowfallShaderIDs.ShipPosition, shipPosition);
            
            if (levelDepthmap != null)
            {
                bakeMaterial?.SetTexture(SnowfallShaderIDs.DepthTex, levelDepthmap);
            }

            // Set projection matrix from camera
            if (levelDepthmapCamera != null)
            {
                bakeMaterial?.SetMatrix(SnowfallShaderIDs.LightViewProjection, depthWorldToClipMatrix ?? Matrix4x4.identity);
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

        private float targetWeight = 0f;
        private float currentWeight = 0f;
        private float underSnowVisualMultiplier = 1f;
        private float fadeSpeed = 2f; // Units per second
        private bool isFading = false;
        internal bool isUnderSnowPreviousFrame = false;
        [SerializeField]
        internal float eyeBias = 0.3f;

        [Header("Snow Tracker VFX")]
        
        [SerializeField]
        internal VisualEffectAsset[]? footprintsTrackerVFX;
        internal static Dictionary <string, VisualEffectAsset>? snowTrackersDict;

        internal void Start()
        {
            snowFootstepIndex = Array.FindIndex(StartOfRound.Instance.footstepSurfaces, surface => surface.surfaceTag == "Snow");
        }

        internal virtual void OnEnable()
        {
            snowVFXContainer?.SetActive(true);
            PlayerTemperatureManager.freezeEffectVolume = frostbiteFilter;
            
            frostbiteFilter!.enabled = true;
            frostyFilter!.enabled = true;
            underSnowFilter!.enabled = true;
            
            if (SnowfallWeather.Instance != null)
            {
                SnowfallWeather.Instance.snowVolume!.enabled = true;
                SnowfallWeather.Instance.snowTrackerCameraContainer?.SetActive(true);
            }
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
            PlayerTemperatureManager.isInColdZone = false;
            isUnderSnowPreviousFrame = false;
        }

        internal void Update()
        {   
            
            if ((SnowThicknessManager.Instance?.isOnNaturalGround ?? false) && GameNetworkManager.Instance.localPlayerController.physicsParent == null)
            {
                float snowThickness = SnowThicknessManager.Instance.GetSnowThickness(GameNetworkManager.Instance.localPlayerController);
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

                // Debug.LogDebug($"Hindrance multiplier: {snowMovementHindranceMultiplier}, isUnderSnow: {isUnderSnow}, snowThickness: {snowThickness}");
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
                // Debug.LogDebug("Not on natural ground");
            }
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

                underSnowFilter!.weight = currentWeight * underSnowVisualMultiplier;
            }
        }

        internal override void Reset()
        {
            PlayerTemperatureManager.isInColdZone = false;
            SnowThicknessManager.Instance?.Reset();
            SnowPatches.CleanupFootprintTrackers(SnowPatches.snowTrackersDict);
            SnowPatches.CleanupFootprintTrackers(SnowPatches.snowShovelDict);
            SnowPatches.ToggleFootprintTrackers(false);
        }

        internal override void PopulateLevelWithVFX(Bounds levelBounds = default, System.Random? seededRandom = null)
        {
            if (!(SnowfallWeather.Instance is BlizzardWeather)) // to avoid setting the depth texture for blizzard
            {
                HDRPCameraOrTextureBinder depthBinder = snowVFXContainer!.GetComponent<HDRPCameraOrTextureBinder>();
                depthBinder.depthTexture = SnowfallWeather.Instance!.levelDepthmap; // bind the baked depth texture
            }

            SnowPatches.ToggleFootprintTrackers(true);

            SnowfallWeather.Instance.snowVolume!.enabled = true;
            SnowfallWeather.Instance.snowTrackerCameraContainer?.SetActive(true);
        }

        internal void OnDestroy()
        {
            SnowPatches.snowTrackersDict.Clear();
            SnowPatches.snowShovelDict.Clear();
        }
    }
}