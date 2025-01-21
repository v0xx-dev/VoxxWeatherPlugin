using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DunGen;
using GameNetcodeStuff;
using TerraMesh;
using TerraMesh.Utils;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;
using VoxxWeatherPlugin.Compatibility;
using VoxxWeatherPlugin.Utils;
using VoxxWeatherPlugin.Weathers;

namespace VoxxWeatherPlugin.Behaviours
{
    public class LevelManipulator : MonoBehaviour
    {
        public static LevelManipulator? Instance { get; private set; }
        
        #region Snowy Weather Configuration
        internal bool isSnowReady = false;
        public GameObject? snowModule;
        [Header("Snow Overlay Volume")]
        [SerializeField]
        internal CustomPassVolume snowVolume = null!;
        internal SnowOverlayCustomPass? snowOverlayCustomPass;
        [Header("Visuals")]
        [SerializeField]
        [ColorUsage(true, true)]
        internal Color snowColor;
        [SerializeField]
        [ColorUsage(true, true)]
        internal Color snowOverlayColor;
        [SerializeField]
        [ColorUsage(true, true)]
        internal Color blizzardFogColor;
        [SerializeField]
        [ColorUsage(true, true)]
        internal Color blizzardCrystalsColor;
        [SerializeField]
        internal Material? snowOverlayMaterial;
        [SerializeField]
        internal Material? snowVertexMaterial;
        [SerializeField]
        internal Material? snowVertexOpaqueMaterial;
        internal Material? CurrentSnowVertexMaterial => Configuration.useOpaqueSnowMaterial.Value ? snowVertexOpaqueMaterial : snowVertexMaterial;
        [SerializeField]
        internal Material? iceMaterial;
        [SerializeField]
        internal float emissionMultiplier;
        private List<GameObject> snowGroundObjects = [];

        [Header("Base Snow Thickness")]
        [SerializeField]
        internal float snowScale = 1.0f;
        [SerializeField, Tooltip("The snow intensity is the power of the exponential function, so 0.0f is full snow.")]
        internal float snowIntensity = 10.0f; // How much snow is on the ground
        [SerializeField]
        internal float finalSnowHeight = 2f;
        [SerializeField]
        internal float fullSnowNormalizedTime = 1f;

        [Header("Snow Occlusion")]
        [SerializeField]
        internal Camera? levelDepthmapCamera;
        [SerializeField]
        internal RenderTexture? levelDepthmap;
        [SerializeField]
        internal RenderTexture? levelDepthmapUnblurred;
        [SerializeField]
        internal int DepthmapResolution => Configuration.depthBufferResolution.Value;
        [SerializeField]
        internal int PCFKernelSize => Configuration.PCFKernelSize.Value;
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
        internal int TracksMapResolution => Configuration.trackerMapResolution.Value;
        internal Matrix4x4? tracksWorldToClipMatrix;

        // [Header("Tessellation Parameters")]
        [SerializeField]
        internal float BaseTessellationFactor => Configuration.minTesselationFactor.Value;
        [SerializeField]
        internal float MaxTessellationFactor => Configuration.maxTesselationFactor.Value;
        [SerializeField]
        internal int IsAdaptiveTessellation => Convert.ToInt32(Configuration.adaptiveTesselation.Value); // 0 for fixed tessellation, 1 for adaptive tessellation

        [Header("Baking Parameters")]
        [SerializeField]
        internal Material? bakeMaterial;
        [SerializeField]
        internal int BakeResolution => Configuration.snowDepthMapResolution.Value;
        internal readonly int blurRadius = 2;
        [SerializeField]
        internal Texture2DArray? snowMasks; // Texture2DArray to store the snow masks
        internal bool BakeMipmaps => Configuration.bakeSnowDepthMipmaps.Value;
        
        #endregion

        #region TerraMesh Configuration

        [Header("TerraMesh Parameters")]

        [SerializeField]
        internal TerraMeshConfig terraMeshConfig;
        string[] moonProcessingWhitelist = [];
        [SerializeField]
        internal float heightThreshold = -100f; // Under this y coordinate, objects will not be considered for snow rendering
        [SerializeField]
        internal QuicksandTrigger[]? waterTriggerObjects;
        [SerializeField]
        internal List<GameObject> waterSurfaceObjects = [];
        [SerializeField]
        internal List<GameObject> groundObjectCandidates = [];

        [Header("Enemies")]
        internal HashSet<string>? enemySnowBlacklist;

        #endregion

        #region General Level Properties

        [Header("General")]
        [SerializeField]
        internal Vector3 shipPosition ;
        internal System.Random? seededRandom;
        internal HDAdditionalLightData? sunLightData;
        internal string CurrentSceneName => StartOfRound.Instance?.currentLevel.sceneName ?? "";

        #endregion

#if DEBUG

        public bool rebakeMaps = false;

#endif
        public Bounds levelBounds; // Current level bounds

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(Instance);
            }
            
            Instance = this;

            if (Configuration.EnableBlizzardWeather.Value || Configuration.EnableSnowfallWeather.Value)
            {
                Debug.LogDebug("Initializing snow variables...");
                snowModule?.SetActive(true);
                InitializeSnowVariables();
            }
            else
            {
                Debug.LogDebug("Snow weather is disabled! Destroying snow module...");
                Destroy(snowModule);
            }
        }

        internal void OnDestroy()
        {
            // Release the render textures
            levelDepthmap?.Release();
            snowTracksMap?.Release();
        }

        internal void InitializeLevelProperties(float sizeMultiplier = 0f)
        {
            // Update random seed
            seededRandom = new System.Random(StartOfRound.Instance.randomMapSeed);
            // Update the sun light data
            sunLightData = TimeOfDay.Instance.sunDirect?.GetComponent<HDAdditionalLightData>();
            // Update the level bounds
            if (sizeMultiplier != 0f)
            {
                CalculateLevelSize(sizeMultiplier);
                terraMeshConfig.levelBounds = levelBounds;
            }
        }

        internal void UpdateLevelProperties()
        {
            // Update for moving ship and compat for different ship landing positions
            shipPosition = StartOfRound.Instance.shipBounds.bounds.center;
        }

        public void ResetLevelProperties()
        {
            seededRandom = null;
            sunLightData = null;
        }

        internal void UpdateLevelDepthmap(bool bakeSnowMaps = true, bool waitForLanding = false)
        {
            Debug.LogDebug("Updating level depthmap");

            Vector3 cameraPosition = new Vector3(levelBounds.center.x,
                                                levelDepthmapCamera!.transform.position.y,
                                                levelBounds.center.z);
            // Set orthographic size to match the level bounds (max is 300)
            levelDepthmapCamera.orthographicSize = (int)Mathf.Clamp(Mathf.Max(levelBounds.size.x, levelBounds.size.z) / 2, 0, 300f);
            levelDepthmapCamera.transform.position = cameraPosition;
            depthWorldToClipMatrix = levelDepthmapCamera.projectionMatrix * levelDepthmapCamera.worldToCameraMatrix;
            Debug.LogDebug($"Camera position: {levelDepthmapCamera.transform.position}, Barycenter: {levelBounds.center}");
            
            StartCoroutine(RefreshDepthmapCoroutine(bakeSnowMaps, waitForLanding));

        }

        internal IEnumerator RefreshDepthmapCoroutine(bool bakeSnowMaps = false, bool waitForLanding = false)
        {
            if (levelDepthmapCamera == null)
            {
                Debug.LogError("Level depthmap camera is not set!");
                yield break;
            }

            if (waitForLanding)
            {
                yield return new WaitUntil(() => StartOfRound.Instance.shipHasLanded);
            }

            levelDepthmapCamera.targetTexture = levelDepthmap;
            levelDepthmapCamera.aspect = 1.0f;
            levelDepthmapCamera.enabled = true;
            
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            
            levelDepthmapCamera.enabled = false;
            
            Debug.LogDebug("Level depthmap rendered!");

            if (bakeSnowMaps)
            {
                RefreshBakeMaterial();
                yield return StartCoroutine(BakeSnowMasksCoroutine());

                Debug.LogDebug("Snow masks baked!");
            }

            SnowThicknessManager.Instance!.inputNeedsUpdate = true;
        }

        public List<GameObject> GetSurfaceObjects()
        {
            GameObject dungeonAnchor = FindAnyObjectByType<RuntimeDungeon>().Root;
            // Set threshold to 1/3 of distance from the origin to the top of the dungeon
            heightThreshold = -Mathf.Abs(dungeonAnchor.transform.position.y/3); 

            LayerMask mask = LayerMask.GetMask("Default", "Room", "Terrain", "Foliage");
            List<GameObject> objectsAboveThreshold = new List<GameObject>();

            // Get the target scene
            Scene scene = SceneManager.GetSceneByName(CurrentSceneName);

            // Reset the list of ground objects to find possible mesh terrains to render snow on
            groundObjectCandidates = [];

            // Iterate through all root GameObjects in the scene
            foreach (GameObject rootGameObject in scene.GetRootGameObjects())
            {
                // Recursively search for objects with a Y position above the threshold
                FindSurfaceObjectsRecursive(rootGameObject.transform, objectsAboveThreshold, heightThreshold, mask);
            }

            Debug.LogDebug("Found " + objectsAboveThreshold.Count + " suitable surface objects!");

            return objectsAboveThreshold;
        }

        private void FindSurfaceObjectsRecursive(Transform parent, List<GameObject> results, float heightThreshold, LayerMask mask)
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
                FindSurfaceObjectsRecursive(child, results, heightThreshold, mask);
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

                    // Collect water surface objects. Must have "water" in the name and no active non-trigger collider
                    if (nameString.Contains("water"))
                    {   
                        Collider waterCollider = obj.GetComponent<Collider>();
                        if (waterCollider == null || !waterCollider.enabled || waterCollider.isTrigger)
                        {
                            //Check if scaled mesh bounds are big enough to be considered a water surface, but also thin enough
                            Bounds bounds = meshRenderer.bounds;
                            float sizeThreshold = 10f;
                            if (bounds.size.x > sizeThreshold && bounds.size.z > sizeThreshold && bounds.size.y < 2f)
                            {
                                waterSurfaceObjects.Add(obj);
                            }
                        }
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
            
            // Ground should be on the default or room layer
            int roomLayer = LayerMask.NameToLayer("Room");
            int defaultLayer = LayerMask.NameToLayer("Default");

            if (gameObject.layer != roomLayer && gameObject.layer != defaultLayer)
            {
                return false;
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
        
        internal void ModifyRenderMasks(List<GameObject> objectsToModify, uint renderingLayerMask = 0)
        {
            foreach (GameObject obj in objectsToModify)
            {
                if (obj.TryGetComponent<MeshRenderer>(out MeshRenderer meshRenderer))
                {
                    meshRenderer.renderingLayerMask |= renderingLayerMask;
                }
            }
        }

        public Bounds CalculateLevelSize(float sizeMultiplier = 1.2f)
        {
            levelBounds = new Bounds(Vector3.zero, Vector3.zero);
            levelBounds.Encapsulate(StartOfRound.Instance.shipInnerRoomBounds.bounds);

            // Store positions of all the outside AI nodes in the scene
            foreach (GameObject node in RoundManager.Instance.outsideAINodes)
            {
                if (node == null)
                    continue;
                levelBounds.Encapsulate(node.transform.position);
            }

            // Find all Entrances in the scene
            EntranceTeleport[] entranceTeleports = GameObject.FindObjectsOfType<EntranceTeleport>();

            foreach (EntranceTeleport entranceTeleport in entranceTeleports)
            {
                if (entranceTeleport == null)
                    continue;
                // Check if the entrance is on the outside
                if (entranceTeleport.isEntranceToBuilding)
                {
                    levelBounds.Encapsulate(entranceTeleport.entrancePoint.position);
                }
            }

            // Choose the largest dimension of the bounds and make it cube
            float maxDimension = Mathf.Max(levelBounds.size.x, levelBounds.size.z) * sizeMultiplier;
            levelBounds.size = new Vector3(maxDimension, maxDimension, maxDimension);

            Debug.LogDebug("Level bounds: " + levelBounds);
            
#if DEBUG
            GameObject debugCube = new GameObject("LevelBounds");
            BoxCollider box = debugCube.AddComponent<BoxCollider>();
            box.transform.position = levelBounds.center;
            box.size = levelBounds.size;
            box.isTrigger = true;
            GameObject.Instantiate(debugCube);
#endif

            return levelBounds;

        } 
        
        internal void UpdateCameraPosition(GameObject? cameraContainer, Camera? camera)
        {
            if (cameraContainer == null || camera == null)
            {
                Debug.LogError("Camera container or camera is null!");
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

        #region Snow Weather Setup

        internal void InitializeSnowVariables()
        {
            // No alpha test pass
            snowOverlayCustomPass = snowVolume!.customPasses[0] as SnowOverlayCustomPass;
            snowOverlayCustomPass!.snowOverlayMaterial = snowOverlayMaterial;
            snowOverlayCustomPass.SetupMaterial(snowOverlayMaterial);
            snowOverlayCustomPass.SetupMaterial(CurrentSnowVertexMaterial);
            // Alpha test pass
            SnowOverlayCustomPass? snowOverlayCustomPassAlpha = snowVolume!.customPasses[1] as SnowOverlayCustomPass;
            snowOverlayCustomPassAlpha!.snowOverlayMaterial = Instantiate(snowOverlayMaterial);

            terraMeshConfig = new TerraMeshConfig(
                            // Bounding box for target area
                            levelBounds : null,
                            useBounds : Configuration.useLevelBounds.Value, // Use the level bounds to filter out-of-bounds vertices
                            constrainEdges : true,
                            // Mesh subdivision
                            subdivideMesh : Configuration.subdivideMesh.Value, // Will also force the algorithm to refine mesh to remove thin triangles
                            baseEdgeLength : 5, // The target edge length for the mesh refinement
                            //Mesh smoothing
                            smoothMesh : Configuration.smoothMesh.Value,
                            smoothingIterations : 1,
                            // UVs
                            replaceUvs : false,
                            onlyUVs : false, //Will only update UV1 field on the mesh
                            // Renderer mask
                            renderingLayerMask : (uint)(snowOverlayCustomPass?.renderingLayers ?? 0),
                            // Terrain conversion
                            minMeshStep : Configuration.minMeshStep.Value,
                            maxMeshStep : Configuration.maxMeshStep.Value,
                            falloffSpeed : Configuration.falloffRatio.Value,
                            targetVertexCount : Configuration.targetVertexCount.Value,
                            carveHoles : Configuration.carveHoles.Value,
                            refineMesh : Configuration.refineMesh.Value,
                            useMeshCollider : Configuration.useMeshCollider.Value,
                            copyTrees : Configuration.useMeshCollider.Value,
                            copyDetail : false
                            );

            levelDepthmap = new RenderTexture(DepthmapResolution, 
                                            DepthmapResolution,
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
            levelDepthmapCamera!.targetTexture = levelDepthmapUnblurred;
            levelDepthmapCamera.aspect = 1.0f;
            levelDepthmapCamera.enabled = false;
            // Create buffer for unblurred depthmap
            levelDepthmapUnblurred = new RenderTexture(DepthmapResolution, 
                                            DepthmapResolution,
                                            0, // Depth bits
                                            RenderTextureFormat.RFloat
                                            );
            
            levelDepthmapUnblurred.dimension = TextureDimension.Tex2DArray; // Single layer array for compat with VFX Graph binding
            levelDepthmapUnblurred.wrapMode = TextureWrapMode.Clamp;
            levelDepthmapUnblurred.filterMode = FilterMode.Trilinear;
            levelDepthmapUnblurred.useMipMap = false;
            levelDepthmapUnblurred.enableRandomWrite = true;
            levelDepthmapUnblurred.useDynamicScale = true;
            levelDepthmapUnblurred.name = "Level Depthmap Unblurred";
            levelDepthmapUnblurred.Create();

            CustomPassVolume customPassVolume = levelDepthmapCamera.GetComponent<CustomPassVolume>();
            DepthVSMPass? depthVSMPass = customPassVolume.customPasses[0] as DepthVSMPass;
            depthVSMPass!.blurRadius = blurRadius;
            depthVSMPass!.depthUnblurred = levelDepthmapUnblurred;
            // This is because Diversity fucks up injection priorities
            customPassVolume.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;

            snowTracksMap = new RenderTexture(TracksMapResolution,
                                            TracksMapResolution,
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
            snowTracksCamera!.enabled = Configuration.enableSnowTracks.Value;
            if (!Configuration.enableSnowTracks.Value)
            {
                snowTracksMap.WhiteOut();
            }
            snowTracksCamera.targetTexture = snowTracksMap;
            snowTracksCamera.aspect = 1.0f;

            moonProcessingWhitelist = Configuration.meshProcessingWhitelist.Value.CleanMoonName().TrimEnd(';').Split(';');
            enemySnowBlacklist = Configuration.enemySnowBlacklist.Value.ToLower().TrimEnd(';').Split(';').ToHashSet();
        }

        internal void UpdateSnowVariables()
        {
            // Accumulate snow on the ground (0 is full snow)
            float normalizedSnowTimer =  Mathf.Clamp01(fullSnowNormalizedTime - TimeOfDay.Instance.normalizedTimeOfDay);
            snowIntensity = 10f * normalizedSnowTimer;
            // Update the snow glow based on the sun intensity
            float sunIntensity = sunLightData?.intensity ?? 0f;
            emissionMultiplier = Mathf.Clamp01(sunIntensity/40f)*0.3f;
            // Update tracking camera position
            UpdateCameraPosition(snowTrackerCameraContainer, snowTracksCamera);
#if DEBUG
            if (rebakeMaps)
            {
                rebakeMaps = false;
                StartCoroutine(RefreshDepthmapCoroutine(bakeSnowMaps: true));
            }
#endif
        }

        internal void ResetSnowVariables()
        {
            //Deactivate snow module
            snowVolume.transform.parent.gameObject.SetActive(false);
            // Reset snow intensity
            StartCoroutine(SnowMeltCoroutine());
            groundObjectCandidates.Clear();
            waterSurfaceObjects.Clear();
            isSnowReady = false;
        }

        /// <summary>
        /// Sets up the level for snowy weather
        /// </summary>
        /// <param name="snowHeightRange">The range of snow height on the ground</param>
        /// <param name="snowNormalizedTimeRange">The range of normalized time for full snow coverage</param>
        /// <param name="snowScaleRange">The range of snow patchiness</param>
        /// <param name="fogStrengthRange">The range of fog strength</param>
        /// <returns></returns>
        /// <remarks>
        /// This method sets up the level for snowy weather by modifying the ground objects to render snow on, freezing water surfaces, modifying the fog strength and updating the level depthmap for snow occlusion.
        /// </remarks>
        internal void SetupLevelForSnow((float, float) snowHeightRange,
                                            (float, float) snowNormalizedTimeRange,
                                            (float, float) snowScaleRange,
                                            (float, float) fogStrengthRange
                                            )
        {
            if (seededRandom == null)
            {
                Debug.LogError("Random seed is not set!");
                return;
            }

            if (LLLCompat.isActive)
            {
                LLLCompat.TagRecolorSnow();
            }

            //Activate snow module
            snowVolume.transform.parent.gameObject.SetActive(true);

            snowScale = seededRandom.NextDouble(snowScaleRange.Item1, snowScaleRange.Item2); // Snow patchy-ness
            finalSnowHeight = seededRandom.NextDouble(snowHeightRange.Item1, snowHeightRange.Item2);
            fullSnowNormalizedTime = seededRandom.NextDouble(snowNormalizedTimeRange.Item1, snowNormalizedTimeRange.Item2);
            List<GameObject> surfaceObjects = GetSurfaceObjects();
            ModifyRenderMasks(surfaceObjects, terraMeshConfig.renderingLayerMask);
            ModifyScrollingFog(fogStrengthRange.Item1, fogStrengthRange.Item2);
            if (Configuration.asyncProcessing.Value)
            {
                StartCoroutine(FinishSnowSetupCoroutine());
            }
            else
            {
                SetupGroundForSnow();
                FreezeWater();
                UpdateLevelDepthmap();
                StartCoroutine(RefreshDepthmapCoroutine(bakeSnowMaps: false, waitForLanding: true));
            }
        }

        internal IEnumerator FinishSnowSetupCoroutine()
        {
            yield return SetupGroundForSnowAsync().AsCoroutine();
            FreezeWater();
            UpdateLevelDepthmap();
            StartCoroutine(RefreshDepthmapCoroutine(bakeSnowMaps: false, waitForLanding: true));
        }

        internal IEnumerator SnowMeltCoroutine()
        {
            // Melt snow on the ground gradually when the weather changes mid-round
            float meltSpeed = 0.1f;
            while (snowIntensity < 10f)
            {
                if (StartOfRound.Instance?.inShipPhase ?? true)
                {
                    break;
                }

                snowIntensity += Time.deltaTime * meltSpeed;

                snowOverlayCustomPass?.RefreshAllSnowMaterials();

                yield return null;
            }

            foreach (GameObject snowGround in snowGroundObjects)
            {
                // Remove the terrain copy (check for null if the coroutine is running after departure from the level)
                if (snowGround != null)
                {
                    Destroy(snowGround);
                }
            }
            
            snowGroundObjects.Clear();
            
            foreach (Material snowMaterial in snowOverlayCustomPass?.snowVertexMaterials ?? [])
            {
                Destroy(snowMaterial);
            }

            snowOverlayCustomPass?.snowVertexMaterials?.Clear();

            Destroy(snowMasks);
        }

        internal void SetSnowColor(Color snowColor, Color snowOverlayColor, Color blizzardFogColor, Color blizzardCrystalsColor)
        {

            CurrentSnowVertexMaterial?.SetColor(SnowfallShaderIDs.SnowColor, snowColor);
            CurrentSnowVertexMaterial?.SetColor(SnowfallShaderIDs.SnowBaseColor, snowOverlayColor);
            snowOverlayCustomPass?.snowOverlayMaterial?.SetColor(SnowfallShaderIDs.SnowBaseColor, snowOverlayColor);
            SnowOverlayCustomPass? snowOverlayCustomPassAlpha = snowVolume!.customPasses[1] as SnowOverlayCustomPass;
            snowOverlayCustomPassAlpha!.snowOverlayMaterial?.SetColor(SnowfallShaderIDs.SnowBaseColor, snowOverlayColor);

            if (SnowfallWeather.Instance?.IsActive ?? false)
            {
                VisualEffect? snowVFX = SnowfallWeather.Instance?.VFXManager?.snowVFXContainer?.GetComponent<VisualEffect>();
                if (snowVFX != null)
                {
                    snowVFX.SetVector4(SnowfallShaderIDs.SnowColor, snowColor);
                }
            }
            else
            {
                VisualEffect? blizzardVFX = BlizzardWeather.Instance?.VFXManager?.snowVFXContainer?.GetComponent<VisualEffect>();
                if (blizzardVFX != null)
                {
                    blizzardVFX.SetVector4(SnowfallShaderIDs.SnowColor, snowColor);
                    blizzardVFX.SetVector4(SnowfallShaderIDs.BlizzardFogColor, blizzardFogColor);
                }

                VisualEffect? waveVFX = BlizzardWeather.Instance?.VFXManager?.blizzardWaveContainer?.GetComponentInChildren<VisualEffect>(true);
                if (waveVFX != null)
                {
                    waveVFX.SetVector4(SnowfallShaderIDs.SnowColor, blizzardCrystalsColor);
                    waveVFX.SetVector4(SnowfallShaderIDs.BlizzardFogColor, blizzardFogColor);
                }
            }
        }

        internal async Task SetupGroundForSnowAsync()
        {
            // Stores mesh terrains and actual Unity terrains to keep track of of walkable ground objects and their texture index in the baked masks
            Dictionary <GameObject, int> groundToIndex = new Dictionary<GameObject, int>();
            // Some moons used TerraMesh package and already have good mesh terrains, skip them
            bool skipMoonProcessing = true;
            foreach (string moon in moonProcessingWhitelist)
            {
                if (CurrentSceneName.CleanMoonName().Contains(moon))
                {
                    skipMoonProcessing = false;
                    break;
                }
            }
            // For Experimentation moon, UVs are broken and need to be replaced
            terraMeshConfig.replaceUvs = StartOfRound.Instance.currentLevel.name.CleanMoonName().Contains("experimentation");
            terraMeshConfig.onlyUVs = skipMoonProcessing;
            if (!skipMoonProcessing)
            {
                Debug.LogDebug($"Moon {StartOfRound.Instance.currentLevel.name} will have its mesh terrain processed to improve topology for snow rendering!");
            }
            if (terraMeshConfig.replaceUvs)
            {
                Debug.LogDebug($"Moon {StartOfRound.Instance.currentLevel.name} needs UV replacement for mesh postprocessing! Overriding...");
            }
            
            Dictionary<Task<GameObject?>, UnityEngine.Object> taskDict = [];
            
            // Process possible mesh terrains to render snow on
            foreach (GameObject meshTerrain in groundObjectCandidates)
            {
                // Process the mesh to remove thin triangles and smooth the mesh
                Task<GameObject?> meshTask = meshTerrain.PostprocessMeshTerrainAsync(terraMeshConfig);
                taskDict.TryAdd(meshTask, meshTerrain);
            }
            
            Terrain[] terrains = Terrain.activeTerrains;
            
            foreach (Terrain terrain in terrains)
            {
                //Check if terrain data is null and if it's being rendered
                if (terrain.terrainData == null || !terrain.drawHeightmap)
                {
                    continue;
                }
                // Turn the terrain into a mesh
                Task<GameObject?> terrainTask = terrain.MeshifyAsync(terraMeshConfig);

                taskDict.TryAdd(terrainTask, terrain);
            }

            await Task.WhenAll(taskDict.Keys);

            int textureIndex = 0;

            foreach (var (task, obj) in taskDict)
            {
                if (task.IsFaulted)
                {
                    Debug.LogError($"Task {task} failed with exception {task.Exception}");
                }

                GameObject? meshTerrain = task.Result;

                if (meshTerrain == null)
                {
                    continue;
                }

                // do different things based on the type of the object use switch case
                switch (obj)
                {
                    case GameObject:
                        // Setup the index in the material property block for the snow masks
                        SetupMeshForSnow(meshTerrain, textureIndex);
                        // Add the terrain to the dictionary
                        groundToIndex.Add(meshTerrain, textureIndex);
                        break;
                    case Terrain terrain:
                        // Modify render mask to support overlay snow rendering
                        MeshRenderer meshRenderer = meshTerrain.GetComponent<MeshRenderer>();
                        meshRenderer.renderingLayerMask |= terraMeshConfig.renderingLayerMask;
                        // Setup the Lit terrain material
                        Material terrainMaterial = meshRenderer.sharedMaterial;
                        terrainMaterial.SetupMaterialFromTerrain(terrain);
                        // Setup the index in the material property block for the snow masks
                        SetupMeshForSnow(meshTerrain, textureIndex);
                        if (terraMeshConfig.useMeshCollider)
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
                        break;
                }

                textureIndex++;
            }

            // Store the ground objects mapping
            SnowThicknessManager.Instance!.groundToIndex = groundToIndex;
        }

        internal void SetupGroundForSnow()
        {
            // Stores mesh terrains and actual Unity terrains to keep track of of walkable ground objects and their texture index in the baked masks
            Dictionary <GameObject, int> groundToIndex = new Dictionary<GameObject, int>();
            // Some moons used TerraMesh package and already have good mesh terrains, skip them
            bool skipMoonProcessing = true;
            foreach (string moon in moonProcessingWhitelist)
            {
                if (CurrentSceneName.CleanMoonName().Contains(moon))
                {
                    skipMoonProcessing = false;
                    break;
                }
            }
            // For Experimentation moon, UVs are broken and need to be replaced
            terraMeshConfig.replaceUvs = StartOfRound.Instance.currentLevel.name.CleanMoonName().Contains("experimentation");
            terraMeshConfig.onlyUVs = skipMoonProcessing;
            if (!skipMoonProcessing)
            {
                Debug.LogDebug($"Moon {StartOfRound.Instance.currentLevel.name} will have its mesh terrain processed to improve topology for snow rendering!");
            }
            if (terraMeshConfig.replaceUvs)
            {
                Debug.LogDebug($"Moon {StartOfRound.Instance.currentLevel.name} needs UV replacement for mesh postprocessing! Overriding...");
            }

            int textureIndex = 0;
            // Process possible mesh terrains to render snow on
            foreach (GameObject meshTerrain in groundObjectCandidates)
            {
                // Process the mesh to remove thin triangles and smooth the mesh
                meshTerrain.PostprocessMeshTerrain(terraMeshConfig);
                // Setup the index in the material property block for the snow masks
                SetupMeshForSnow(meshTerrain, textureIndex);
                // Add the terrain to the dictionary
                groundToIndex.Add(meshTerrain, textureIndex);

                textureIndex++;
            }
            
            Terrain[] terrains = Terrain.activeTerrains;
            
            foreach (Terrain terrain in terrains)
            {
                //Check if terrain data is null annd if it's being rendered
                if (terrain.terrainData == null || !terrain.drawHeightmap)
                {
                    continue;
                }
                // Turn the terrain into a mesh
                GameObject meshTerrain = terrain.Meshify(terraMeshConfig);
                // Modify render mask to support overlay snow rendering
                MeshRenderer meshRenderer = meshTerrain.GetComponent<MeshRenderer>();
                meshRenderer.renderingLayerMask |= terraMeshConfig.renderingLayerMask;
                // Setup the Lit terrain material
                Material terrainMaterial = meshRenderer.sharedMaterial;
                terrainMaterial.SetupMaterialFromTerrain(terrain);
                // Setup the index in the material property block for the snow masks
                SetupMeshForSnow(meshTerrain, textureIndex);
                if (terraMeshConfig.useMeshCollider)
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

        internal void SetupMeshForSnow(GameObject meshTerrain, int texId)
        {
            // Duplicate the mesh and set the snow vertex material
            GameObject snowGround = meshTerrain.Duplicate(disableShadows: !Configuration.snowCastsShadows.Value, removeCollider: true);
            MeshRenderer meshRenderer = snowGround.GetComponent<MeshRenderer>();
            // Duplicate the snow vertex material to allow SRP batching
            Material snowVertexCopy = Instantiate(CurrentSnowVertexMaterial!);
            snowVertexCopy.SetFloat(SnowfallShaderIDs.TexIndex, texId);
            snowOverlayCustomPass!.snowVertexMaterials.Add(snowVertexCopy);
            meshRenderer.sharedMaterial = snowVertexCopy;
            // Deselect snow OVERLAY rendering layers from vertex snow objects
            meshRenderer.renderingLayerMask &= ~terraMeshConfig.renderingLayerMask;
            // Upload the mesh to the GPU to save RAM. TODO: Prevents NavMesh baking
            // snowGround.GetComponent<MeshFilter>().sharedMesh.UploadMeshData(true);
            snowGroundObjects.Add(snowGround);
        }

        internal void FreezeWater()
        {
            if (!Configuration.freezeWater.Value)
            {
                return;
            }
            
            if (waterSurfaceObjects.Count == 0)
            {
                Debug.LogDebug("No water surfaces to freeze!");
                return;
            }

            // Measure time
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

#if DEBUG
            waterTriggerObjects = FindObjectsOfType<QuicksandTrigger>().Where(x => x.enabled &&
                                                                                x.gameObject.activeInHierarchy &&
                                                                                x.isWater &&
                                                                                x.gameObject.scene.name == CurrentSceneName).ToArray();
#else
            waterTriggerObjects = FindObjectsOfType<QuicksandTrigger>().Where(x => x.enabled &&
                                                                                x.gameObject.activeInHierarchy &&
                                                                                x.isWater &&
                                                                                x.gameObject.scene.name == CurrentSceneName &&
                                                                                !x.isInsideWater).ToArray();
#endif
            HashSet<GameObject> iceObjects = new HashSet<GameObject>();

            NavMeshModifierVolume[] navMeshModifiers = FindObjectsOfType<NavMeshModifierVolume>().Where(x => x.gameObject.activeInHierarchy &&
                                                                                    x.gameObject.scene.name == CurrentSceneName &&
                                                                                    x.transform.position.y > heightThreshold &&
                                                                                    x.enabled &&
                                                                                    x.area == 1 << 1).ToArray(); // Layer 1 is not walkable

            
            GameObject? navMeshContainer = GameObject.FindGameObjectWithTag("OutsideLevelNavMesh");
            GameObject? randomWater = waterSurfaceObjects.FirstOrDefault();
            GameObject iceContainer = new GameObject("IceContainer");
            iceContainer.transform.SetParent(navMeshContainer?.transform ?? randomWater?.transform?.parent);

            //Print names of all nav mesh modifier volumes
            foreach (NavMeshModifierVolume navMeshModifier in navMeshModifiers)
            {
                Debug.LogDebug($"NavMeshModifierVolume {navMeshModifier.name} found");
            }
            
            foreach (QuicksandTrigger waterObject in waterTriggerObjects)
            {
                // Disable sinking
                waterObject.enabled = false;
                if (waterObject.TryGetComponent<Collider>(out Collider collider))
                {
                    collider.enabled = false;
                }
            }

            foreach (GameObject waterSurface in waterSurfaceObjects)
            {
                //Get renderer component, we know it exists since we checked it in the CheckIfObjectIsTerrain method
                MeshRenderer meshRenderer = waterSurface.GetComponent<MeshRenderer>();
                MeshFilter meshFilter = waterSurface.GetComponent<MeshFilter>();
                Mesh mesh = meshFilter.sharedMesh;

                //Find which submesh is used for the water surface from the comparison with mesh.GetSubMesh(i).firstVertex
                int submeshIndex = meshRenderer.subMeshStartIndex;

                Mesh meshCopy = waterSurface.ExtractSubmesh(submeshIndex);
                meshFilter.sharedMesh = meshCopy;
                
                //Check for collider and if it exists, destroy it, otherwise add a mesh collider
                // Only freeze water surfaces above the height threshold
                if (waterSurface.transform.position.y <= heightThreshold)
                {
                    continue;
                }

                if (waterSurface.TryGetComponent<Collider>(out Collider collider))
                {
                    Destroy(collider);
                }

                MeshCollider meshCollider = waterSurface.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshCopy;

                // Check if any bounds of NavMeshModifierVolume intersect with the water surface bounds and disable it in that case
                foreach (NavMeshModifierVolume navMeshModifier in navMeshModifiers)
                {
                    Bounds bounds = new Bounds(navMeshModifier.center + navMeshModifier.transform.position, navMeshModifier.size);
                    Bounds waterBounds = meshRenderer.bounds;
                    // Enlarge along y axis since water surfaces are thin
                    waterBounds.size = new Vector3(waterBounds.size.x, 3f, waterBounds.size.z);
                    if (bounds.Intersects(waterBounds))
                    {
                        Debug.LogDebug($"Disabling NavMeshModifierVolume {navMeshModifier.name} intersecting with water surface {waterSurface.name}");
                        navMeshModifier.enabled = false;
                    }
                }
                    
                meshRenderer.sharedMaterial = iceMaterial;

                // Rise slightly
                waterSurface.transform.position += 0.6f*Vector3.up;
                
                // Change footstep sounds
                waterSurface.tag = "Rock";
                waterSurface.layer = LayerMask.NameToLayer("Room");
                waterSurface.transform.SetParent(iceContainer.transform);
                iceObjects.Add(waterSurface);
            }
            // Store the ice objects
            SnowThicknessManager.Instance!.iceObjects = iceObjects;
            // Rebake NavMesh
            
            stopwatch.Stop();
            Debug.LogDebug($"Freezing water took {stopwatch.ElapsedMilliseconds} ms");

            if (navMeshContainer != null)
            {
                NavMeshSurface navMesh = navMeshContainer.GetComponent<NavMeshSurface>();
                AsyncOperation rebuildOp = navMesh.UpdateNavMesh(navMesh.navMeshData);
                StartCoroutine(navMesh.NavMeshRebuildCoroutine(rebuildOp, stopwatch));
            }

        }

        internal void ModifyScrollingFog(float fogStrengthMin = 0f, float fogStrengthMax = 15f)
        {
            LocalVolumetricFog[] fogArray = FindObjectsOfType<LocalVolumetricFog>();
            fogArray = fogArray.Where(x => x.gameObject.activeSelf &&
                                        x.gameObject.scene.name == CurrentSceneName &&
                                        x.transform.position.y > heightThreshold).ToArray();
            float additionalMeanFreePath = seededRandom!.NextDouble(fogStrengthMin, fogStrengthMax); // Could be negative
            foreach (LocalVolumetricFog fog in fogArray)
            {
                fog.parameters.textureScrollingSpeed = Vector3.zero;
                fog.parameters.meanFreePath = Mathf.Max(5f, fog.parameters.meanFreePath + additionalMeanFreePath); 
            }
        }

        internal IEnumerator BakeSnowMasksCoroutine()
        {
            
            if (groundObjectCandidates.Count == 0)
            {
                Debug.LogDebug("No ground objects to bake snow masks for!");
                yield break;
            }

            // Bake the snow masks into a Texture2DArray
            snowMasks = new Texture2DArray(BakeResolution,
                                           BakeResolution,
                                           groundObjectCandidates.Count,
                                           TextureFormat.RGBAFloat,
                                           BakeMipmaps,
                                           false,
                                           true);
            snowMasks.filterMode = FilterMode.Trilinear;
            snowMasks.wrapMode = TextureWrapMode.Clamp;
            
            // Start the bake masks coroutine and wait for it to finish
            yield return StartCoroutine(snowMasks.BakeMasks(groundObjectCandidates, bakeMaterial!, BakeResolution));

            snowMasks.Apply(updateMipmaps: BakeMipmaps, makeNoLongerReadable: true); // Move to the GPU

            foreach (Material snowVertexMaterial in snowOverlayCustomPass!.snowVertexMaterials)
            {
                snowVertexMaterial?.SetTexture(SnowfallShaderIDs.SnowMasks, snowMasks);
            }

            isSnowReady = true;
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
            
            if (levelDepthmapUnblurred != null)
            {
                bakeMaterial?.SetTexture(SnowfallShaderIDs.DepthTex, levelDepthmapUnblurred);
            }

            // Set projection matrix from camera
            if (levelDepthmapCamera != null)
            {
                bakeMaterial?.SetMatrix(SnowfallShaderIDs.LightViewProjection, depthWorldToClipMatrix ?? Matrix4x4.identity);
            }
        }

        #endregion
    }
}