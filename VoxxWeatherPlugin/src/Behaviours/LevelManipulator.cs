using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
using WeatherRegistry;

namespace VoxxWeatherPlugin.Behaviours
{
    public class LevelManipulator : MonoBehaviour
    {
        public static LevelManipulator Instance { get; private set; } = null!;
        
        #region Snowy Weather Configuration
        public bool IsSnowReady { get; private set; } = false;
        public event Action<bool>? OnSnowReady;
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
        internal float snowIntensity = 1.0f; // How much snow is on the ground
        [SerializeField]
        internal float finalSnowHeight = 2f;
        [SerializeField]
        internal float fullSnowNormalizedTime = 1f;

        [Header("Snow Occlusion")]
        [SerializeField]
        internal Camera? levelDepthmapCamera;
        private HDAdditionalCameraData? depthmapCameraData;
        [SerializeField]
        internal RenderTexture? levelDepthmap;
        [SerializeField]
        internal RenderTexture? levelDepthmapUnblurred;
        [SerializeField]
        internal int DepthmapResolution => Configuration.depthBufferResolution.Value;
        [SerializeField]
        internal int PCFKernelSize => Configuration.PCFKernelSize.Value;
        public Material? depthBakeMaterial;
        public int depthBlurRadius = 2; // Used for smoothing depth in VSM algorithm
        [SerializeField]
        internal float shadowBias = 0.001f;
        [SerializeField]
        internal float snowOcclusionBias = 0.005f;
        internal Matrix4x4? depthWorldToClipMatrix;

        [Header("Snow Tracks")]
        [SerializeField]
        internal VisualEffectAsset[]? footprintsTrackerVFX;
        internal static Dictionary <string, VisualEffectAsset>? snowTrackersDict;
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
        internal readonly int blurRadius = 2; // Used for smoothing normals in the baked snow masks
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
        internal List<QuicksandTrigger>? waterTriggerObjects;
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
        internal Weather currentWeather = null!;

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
            if (sizeMultiplier > 0f)
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

        internal void RefreshLevelCamera()
        {
            Vector3 cameraPosition = new(levelBounds.center.x,
                                        levelDepthmapCamera!.transform.position.y,
                                        levelBounds.center.z);
            // Set orthographic size to match the level bounds (max is 300)
            levelDepthmapCamera.orthographicSize = (int)Mathf.Clamp(Mathf.Max(levelBounds.size.x, levelBounds.size.z) / 2, 25, 300f);
            levelDepthmapCamera.transform.position = cameraPosition;
            depthWorldToClipMatrix = levelDepthmapCamera.projectionMatrix * levelDepthmapCamera.worldToCameraMatrix;
            Debug.LogDebug($"Depth camera position: {levelDepthmapCamera.transform.position}, Size: {levelDepthmapCamera.orthographicSize}, Barycenter: {levelBounds.center}");
        }

        internal IEnumerator RefreshDepthmapOnLanding()
        {
            yield return new WaitUntil(() => StartOfRound.Instance.shipHasLanded);

            BakeDepth();
        }

        private void RequestHDRPBuffersAccess(ref HDAdditionalCameraData.BufferAccess access)
        {
            access.RequestAccess(HDAdditionalCameraData.BufferAccessType.Depth);
        }

        private void BakeDepth(bool blur = true)
        {
            if (levelDepthmapCamera == null)
            {
                Debug.LogError("Level depthmap camera is not set!");
                return;
            }

            if (depthBakeMaterial == null)
            {
                Debug.LogError("Depth material is not assigned.");
                return;
            }

            if (levelDepthmap == null || levelDepthmapUnblurred == null)
            {
                Debug.LogError("Level depthmap is not assigned.");
                return;
            }

            depthBakeMaterial.SetFloat("_BlurKernelSize", depthBlurRadius);
            // Render the depth map
            levelDepthmapCamera?.Render();
            // Get the depth buffer handle
            RTHandle? depthBufferHandle = depthmapCameraData?.GetGraphicsBuffer(HDAdditionalCameraData.BufferAccessType.Depth);

            if (depthBufferHandle == null || depthBufferHandle.rt == null)
            {
                Debug.LogError("Depth buffer handle is null! Cannot bake depth.");
                return;
            }

            // Store the unblurred depth map (use Blit instead of CopyTexture to avoid format mismatch)
            Graphics.Blit(depthBufferHandle.rt, levelDepthmapUnblurred);
            
            if (blur)
            {
                RenderTexture tempBuffer = RenderTexture.GetTemporary(levelDepthmap.descriptor);
                // Blur the depth map (Horizontal + Vertical)
                Graphics.Blit(depthBufferHandle.rt, tempBuffer, depthBakeMaterial, 0);
#if DEBUG
                string savePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                tempBuffer.SaveToFile(Path.Combine(savePath, "LevelDepthmapBlurredTemp.png"));
#endif
                Graphics.Blit(tempBuffer, levelDepthmap, depthBakeMaterial, 1);
                RenderTexture.ReleaseTemporary(tempBuffer);
            }

            Debug.LogDebug($"Level depthmap rendered! Blurred: {blur}");

#if DEBUG
            string assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            levelDepthmapUnblurred.SaveToFile(Path.Combine(assemblyPath, "LevelDepthmapUnblurred.png"));
            if (blur)
                levelDepthmap.SaveToFile(Path.Combine(assemblyPath, "LevelDepthmapBlurred.png"));

            Debug.LogDebug($"Depthmaps saved to: {assemblyPath}");
#endif
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
            // Create a primitive cube and parent to the collider
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = levelBounds.center;
            cube.transform.localScale = levelBounds.size;
            cube.transform.parent = debugCube.transform;
            // Replace the material with default HDRP Unlit
            cube.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("HDRP/Unlit"));
            
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
            camera.LimitFrameRate(Configuration.tracksCameraFPS.Value);
            if (Vector3.Distance(playerPosition, cameraContainer.transform.position) >= camera.orthographicSize / 2f)
            {
                // To render the frame when position changed
                camera.LimitFrameRate(-1f);
                cameraContainer.transform.position = playerPosition;
                tracksWorldToClipMatrix = camera.projectionMatrix * camera.worldToCameraMatrix;
            }

        }

        #region Snow Weather Setup

        internal void InitializeSnowVariables()
        {
            snowOverlayCustomPass = snowVolume!.customPasses[0] as SnowOverlayCustomPass;
            snowOverlayCustomPass!.snowOverlayMaterial = snowOverlayMaterial;
            snowOverlayCustomPass.SetupMaterial(snowOverlayMaterial);
            snowOverlayCustomPass.SetupMaterial(CurrentSnowVertexMaterial);

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
                                            )
            {
                dimension = TextureDimension.Tex2DArray, // Single layer array for compat with VFX Graph binding
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                useMipMap = false,
                enableRandomWrite = true,
                useDynamicScale = true,
                name = "Level Depthmap"
            };
            levelDepthmap.Create();
            // Set the camera target texture
            levelDepthmapCamera!.targetTexture = levelDepthmapUnblurred;
            levelDepthmapCamera.aspect = 1.0f;
            levelDepthmapCamera.enabled = false;
            depthmapCameraData ??= levelDepthmapCamera.GetComponent<HDAdditionalCameraData>();
            if (depthmapCameraData != null)
                depthmapCameraData.requestGraphicsBuffer += RequestHDRPBuffersAccess;
            // Create buffer for unblurred depthmap
            levelDepthmapUnblurred = new RenderTexture(DepthmapResolution,
                                            DepthmapResolution,
                                            0, // Depth bits
                                            RenderTextureFormat.RFloat
                                            )
            {
                dimension = TextureDimension.Tex2DArray, // Single layer array for compat with VFX Graph binding
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                useMipMap = false,
                enableRandomWrite = true,
                useDynamicScale = true,
                name = "Level Depthmap Unblurred"
            };
            levelDepthmapUnblurred.Create();

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

            // Assign the snow effects to the PlayerEffectsManager
            foreach (Transform child in snowVolume.transform.parent)
            {
                if (child.name == "FrostbiteFilter")
                {
                    PlayerEffectsManager.freezeEffectVolume = child.gameObject.GetComponent<Volume>();
                    continue;
                }
                
                if (child.name == "UnderSnowFilter")
                {
                    PlayerEffectsManager.underSnowVolume = child.gameObject.GetComponent<Volume>();
                    continue;
                }
            }
        }

        internal void UpdateSnowVariables()
        {
            // Accumulate snow on the ground (0 is full snow)
            snowIntensity =  Mathf.Clamp01(fullSnowNormalizedTime - TimeOfDay.Instance.normalizedTimeOfDay + 0.1f); // Account for the fact that at landing time is ~ 0.1f
            // Update the snow glow based on the sun intensity
            float sunIntensity = sunLightData?.intensity ?? 0f;
            emissionMultiplier = Mathf.Clamp01(sunIntensity/40f)*0.3f;
            // Update tracking camera position
            UpdateCameraPosition(snowTrackerCameraContainer, snowTracksCamera);
#if DEBUG
            if (rebakeMaps)
            {
                rebakeMaps = false;
                RefreshMaskTexture(groundObjectCandidates.Count);
                BakeDepth(blur: true);
                BakeSnowMasks();
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
            IsSnowReady = false;
            OnSnowReady?.Invoke(IsSnowReady);
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

            if (LLLCompat.IsActive)
            {
                LLLCompat.TagRecolorSnow();
            }

            //Activate snow module
            snowVolume.transform.parent.gameObject.SetActive(true);

            snowScale = seededRandom.NextDouble(snowScaleRange.Item1, snowScaleRange.Item2); // Snow patchy-ness
            finalSnowHeight = seededRandom.NextDouble(snowHeightRange.Item1, snowHeightRange.Item2);
            fullSnowNormalizedTime = seededRandom.NextDouble(snowNormalizedTimeRange.Item1, snowNormalizedTimeRange.Item2);
            // Here we also try to find mesh terrain objects
            List<GameObject> surfaceObjects = GetSurfaceObjects();
            ModifyRenderMasks(surfaceObjects, terraMeshConfig.renderingLayerMask);
            ModifyScrollingFog(fogStrengthRange.Item1, fogStrengthRange.Item2);
            RefreshLevelCamera();
            StartCoroutine(FinishSnowSetupCoroutine(Configuration.asyncProcessing.Value));
            StartCoroutine(RefreshDepthmapOnLanding());
        }

        internal IEnumerator FinishSnowSetupCoroutine(bool isAsync = false)
        {
            if (isAsync)
            {
                // Masks are baked sequentially here
                yield return SetupGroundForSnowAsync().AsCoroutine();
                // Bake the VSM depth map again (as this point might execute after ship landing)
                BakeDepth();
            }
            else
            {
                SetupGroundForSnow();
                BakeSnowMasks();
            }

            FreezeWater();

            IsSnowReady = true;
            OnSnowReady?.Invoke(IsSnowReady);
            SnowThicknessManager.Instance!.inputNeedsUpdate = true;
        }

        internal IEnumerator SnowMeltCoroutine()
        {
            // Melt snow on the ground gradually when the weather changes mid-round
            float meltSpeed = 0.01f;
            while (snowIntensity <= 1f)
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

            if (SnowfallWeather.Instance?.IsActive ?? false)
            {
                VisualEffect? snowVFX = SnowfallWeather.Instance?.VFXManager?.snowVFXContainer?.GetComponent<VisualEffect>();
                snowVFX?.SetVector4(SnowfallShaderIDs.SnowColor, snowColor);
            }
            else
            {
                VisualEffect? blizzardVFX = BlizzardWeather.Instance?.VFXManager?.snowVFXContainer?.GetComponent<VisualEffect>();
                blizzardVFX?.SetVector4(SnowfallShaderIDs.SnowColor, snowColor);
                blizzardVFX?.SetVector4(SnowfallShaderIDs.BlizzardFogColor, blizzardFogColor);

                VisualEffect? waveVFX = BlizzardWeather.Instance?.VFXManager?.blizzardWaveContainer?.GetComponentInChildren<VisualEffect>(true);
                waveVFX?.SetVector4(SnowfallShaderIDs.SnowColor, blizzardCrystalsColor);
                waveVFX?.SetVector4(SnowfallShaderIDs.BlizzardFogColor, blizzardFogColor);

                if (BlizzardWeather.Instance?.VFXManager?.blizzardFog != null)
                {
                    BlizzardWeather.Instance.VFXManager.blizzardFog.parameters.albedo = blizzardFogColor;
                }
            }
        }

        internal async Task SetupGroundForSnowAsync()
        {
            // Stores mesh terrains and actual Unity terrains to keep track of of walkable ground objects and their texture index in the baked masks
            Dictionary <GameObject, int> groundToIndex = [];
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
            
            Terrain[] terrains = Terrain.activeTerrains.Where(terrain => terrain.terrainData != null && terrain.drawHeightmap && terrain.gameObject.activeInHierarchy).ToArray();
            
            // Create a combined list of all possible ground objects and sort them in asc. order by distance to the ship
            List<UnityEngine.Object> groundObjectsAll = [.. groundObjectCandidates, .. terrains];
            groundObjectsAll.Sort((a, b) => Vector3.SqrMagnitude(shipPosition - GetGroundCenter(a)).CompareTo(Vector3.SqrMagnitude(shipPosition - GetGroundCenter(b))));

            (RenderTexture tempRT, RenderTexture blurRT1, RenderTexture blurRT2, Texture2D maskLayer) = RefreshMaskTexture(groundObjectsAll.Count, getBuffers: true);
            System.Diagnostics.Stopwatch sw = new();

            // Thread-safe dictionaries to store the ground objects and their texture index
            ConcurrentDictionary<Task<GameObject?>, UnityEngine.Object> meshifyTaskDict = [];
            ConcurrentBag<Task> tasks = [];
            ConcurrentBag<GameObject> groundObjectCandidatesSafe = [.. groundObjectCandidates];
            ConcurrentDictionary<GameObject, int> groundToIndexSafe = [];
            var mainThreadScheduler = TaskScheduler.FromCurrentSynchronizationContext(); // To schedule the continuation task on the main thread
            int textureIndex = 0;

            // Process possible ground objects to render snow on
            foreach (UnityEngine.Object groundObject in groundObjectsAll)
            {
                Task<GameObject?> groundTask;

                if (groundObject is GameObject meshTerrain)
                {
                    // Process the mesh to remove thin triangles and smooth the mesh
                    groundTask = meshTerrain.PostprocessMeshTerrainAsync(terraMeshConfig);
                }
                else if (groundObject is Terrain terrain)
                {
                    // Turn the terrain into a mesh
                    groundTask = terrain.MeshifyAsync(terraMeshConfig);
                }
                else
                {
                    continue;
                }

                Task meshTerrainTask = groundTask.ContinueWith(ProcessMeshTerrainObject, mainThreadScheduler);
                meshifyTaskDict.TryAdd(groundTask, groundObject);
                tasks.Add(meshTerrainTask);
            }

            await Task.WhenAll(tasks);

            // Align thread-safe dictionaries with the main thread
            groundObjectCandidates = groundObjectCandidatesSafe.ToList();
            groundToIndex = groundToIndexSafe.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            // Store the ground objects mapping
            SnowThicknessManager.Instance!.groundToIndex = groundToIndex;

            // Cleanup after mask baking
            RenderTexture.ReleaseTemporary(tempRT);
            RenderTexture.ReleaseTemporary(blurRT1);
            RenderTexture.ReleaseTemporary(blurRT2);
            Destroy(maskLayer);

            snowMasks?.Apply(updateMipmaps: BakeMipmaps, makeNoLongerReadable: true); // Move to the GPU
            
            Vector3 GetGroundCenter(UnityEngine.Object groundObject)
            {
                switch (groundObject)
                {
                    case GameObject meshTerrain:
                        MeshRenderer? meshRenderer = meshTerrain.GetComponent<MeshRenderer>();
                        Vector3 center = meshRenderer != null ? meshRenderer.bounds.center : meshTerrain.transform.position;
                        return center;
                    case Terrain terrain:
                        TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
                        Vector3 terrainCenter = terrainCollider != null ? terrainCollider.bounds.center : terrain.transform.position;
                        return terrainCenter;
                    default:
                        return Vector3.zero;
                }
            }

            void ProcessMeshTerrainObject(Task<GameObject?> meshTerrainTask )
            {
                if (meshTerrainTask.IsFaulted)
                {
                    Debug.LogError($"Task {meshTerrainTask} failed with exception {meshTerrainTask.Exception}");
                }

                GameObject? meshTerrain = meshTerrainTask.Result;

                if (!meshifyTaskDict.TryGetValue(meshTerrainTask, out UnityEngine.Object sourceObj))
                {
                    Debug.LogError($"Could not find source object for task {meshTerrainTask}");
                    return;
                }

                if (meshTerrain == null)
                {
                    Debug.LogError($"Mesh terrain object is null for task {meshTerrainTask}");
                    return;
                }

                // textureIndex++ in a thread-safe manner
                int currentIndex = Interlocked.Increment(ref textureIndex) -1; 

                switch (sourceObj)
                {
                    case GameObject:
                        // Setup the index in the material property block for the snow masks
                        SetupMeshForSnow(meshTerrain, currentIndex, tempRT, blurRT1, blurRT2, maskLayer, sw);
                        // Add the terrain to the dictionary
                        groundToIndexSafe.TryAdd(meshTerrain, currentIndex);
                        break;
                    case Terrain terrain:
                        // Modify render mask to support overlay snow rendering
                        MeshRenderer meshRenderer = meshTerrain.GetComponent<MeshRenderer>();
                        meshRenderer.renderingLayerMask |= terraMeshConfig.renderingLayerMask;
                        // Setup the index in the material property block for the snow masks
                        SetupMeshForSnow(meshTerrain, currentIndex, tempRT, blurRT1, blurRT2, maskLayer, sw);
                        // Use mesh terrain or terrain collider for snow thickness calculation
                        GameObject colliderObject = terraMeshConfig.useMeshCollider ? meshTerrain : terrain.gameObject;
                        groundToIndexSafe.TryAdd(colliderObject, currentIndex);
                        // Store the mesh terrain in the list for baking later
                        groundObjectCandidatesSafe.Add(meshTerrain);
                        break;
                }
            }
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

            Terrain[] terrains = Terrain.activeTerrains.Where(terrain => terrain.terrainData != null && terrain.drawHeightmap && terrain.gameObject.activeInHierarchy).ToArray();
            int groundObjectCount = groundObjectCandidates.Count + terrains.Length;
            RefreshMaskTexture(groundObjectCount);

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

            foreach (Terrain terrain in terrains)
            {
                // Turn the terrain into a mesh
                GameObject meshTerrain = terrain.Meshify(terraMeshConfig);
                // Modify render mask to support overlay snow rendering
                MeshRenderer meshRenderer = meshTerrain.GetComponent<MeshRenderer>();
                meshRenderer.renderingLayerMask |= terraMeshConfig.renderingLayerMask;
                // Setup the index in the material property block for the snow masks
                SetupMeshForSnow(meshTerrain, textureIndex);
                // Use mesh terrain or terrain collider for snow thickness calculation
                GameObject colliderObject = terraMeshConfig.useMeshCollider ? meshTerrain : terrain.gameObject;
                groundToIndex.Add(colliderObject, textureIndex);
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
            snowVertexCopy.SetTexture(SnowfallShaderIDs.SnowMasks, snowMasks);
            snowOverlayCustomPass!.snowVertexMaterials.Add(snowVertexCopy);
            meshRenderer.sharedMaterial = snowVertexCopy;
            // Deselect snow OVERLAY rendering layers from vertex snow objects
            meshRenderer.renderingLayerMask &= ~terraMeshConfig.renderingLayerMask;
            // Upload the mesh to the GPU to save RAM. TODO: Prevents NavMesh baking
            // snowGround.GetComponent<MeshFilter>().sharedMesh.UploadMeshData(true);
            snowGroundObjects.Add(snowGround);
        }

        internal void SetupMeshForSnow(GameObject meshTerrain,
                                        int texId,
                                        RenderTexture tempRT,
                                        RenderTexture blurRT1,
                                        RenderTexture blurRT2,
                                        Texture2D maskLayer,
                                        System.Diagnostics.Stopwatch sw)
        {
            SetupMeshForSnow(meshTerrain, texId);
            BakeDepth(blur: false);
            RefreshBakeMaterial();
            snowMasks?.TryBakeMask(meshTerrain,
                                    bakeMaterial!,
                                    texId,
                                    tempRT,
                                    blurRT1,
                                    blurRT2,
                                    maskLayer,
                                    sw,
                                    BakeResolution);
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
                                                                                x.gameObject.scene.name == CurrentSceneName).ToList();
#else
            waterTriggerObjects = FindObjectsOfType<QuicksandTrigger>().Where(x => x.enabled &&
                                                                                x.gameObject.activeInHierarchy &&
                                                                                x.isWater &&
                                                                                x.gameObject.scene.name == CurrentSceneName &&
                                                                                !x.isInsideWater).ToList();
#endif
            HashSet<GameObject> iceObjects = new HashSet<GameObject>();

            List<NavMeshModifierVolume> navMeshModifiers = NavMeshModifierVolume.activeModifiers.Where(x => x.gameObject.activeInHierarchy &&
                                                                                    x.gameObject.scene.name == CurrentSceneName &&
                                                                                    x.transform.position.y > heightThreshold &&
                                                                                    x.enabled &&
                                                                                    x.area == 1).ToList(); // Layer 1 is not walkable

            
            Transform? navMeshContainer = GameObject.FindGameObjectWithTag("OutsideLevelNavMesh")?.transform;
            GameObject? randomWater = waterSurfaceObjects.FirstOrDefault();
            GameObject iceContainer = new GameObject("IceContainer");
            iceContainer.transform.SetParent(navMeshContainer ?? randomWater?.transform?.parent);

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

                // If area of bounds of mesh renderer are bigger than twice the LevelBounds, skip freezing
                if (meshRenderer.bounds.size.x * meshRenderer.bounds.size.z > 2 * levelBounds.size.x * levelBounds.size.z)
                {
                    Debug.LogDebug($"Skipping freezing water surface {waterSurface.name} because it's too big. Is it an ocean?");
                    continue;
                }

                if (waterSurface.TryGetComponent<Collider>(out Collider collider))
                {
                    Destroy(collider);
                }
                
                // Use mesh collider because some of the ice meshes are not just flat planes (e.g. on Vow)
                MeshCollider meshCollider = waterSurface.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshCopy;

                // Check if any bounds of NavMeshModifierVolume intersect with the water surface bounds and disable it in that case
                for (int i = navMeshModifiers.Count - 1; i >= 0; i--)
                {
                    NavMeshModifierVolume navMeshModifier = navMeshModifiers[i];
                    Bounds navVolumeBounds = new Bounds(navMeshModifier.center + navMeshModifier.transform.position, navMeshModifier.size);
                    Bounds waterBounds = meshRenderer.bounds;
                    // Enlarge along y axis since water surfaces are thin
                    waterBounds.size = new Vector3(waterBounds.size.x, 3f, waterBounds.size.z);
                    if (navVolumeBounds.Intersects(waterBounds))
                    {
                        Debug.LogDebug($"Disabling NavMeshModifierVolume {navMeshModifier.name} intersecting with water surface {waterSurface.name}");
                        navMeshModifier.enabled = false;
                        navMeshModifiers.RemoveAt(i);
                    }
                }

                for (int i = waterTriggerObjects.Count - 1; i >= 0; i--)
                {
                    QuicksandTrigger waterObject = waterTriggerObjects[i];
                    if (waterObject.TryGetComponent<Collider>(out collider) && collider.bounds.Intersects(meshRenderer.bounds))
                    {
                        // Disable sinking
                        waterObject.enabled = false;
                        collider.enabled = false;

                        //Remove the water trigger from the list
                        waterTriggerObjects.RemoveAt(i);

                    }
                }
                    
                meshRenderer.sharedMaterial = iceMaterial;

                // Rise slightly
                waterSurface.transform.position += 0.6f*Vector3.up;
                
                // Change footstep sounds
                waterSurface.tag = "Rock";
                waterSurface.layer = LayerMask.NameToLayer("Room");
                // Check all parents up to the scene root and if none of them is navmesh container, parent to ice container
                bool orphanedWater = true;
                foreach (Transform parent in waterSurface.transform.GetParents())
                {
                    if (parent == navMeshContainer)
                    {
                        orphanedWater = false;
                        break;
                    }
                }
                if (orphanedWater)
                {
                    waterSurface.transform.SetParent(iceContainer.transform);
                }
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
            if ((BlizzardWeather.Instance?.IsActive ?? false) && Configuration.useVolumetricBlizzardFog.Value)
                additionalMeanFreePath = 0;
            foreach (LocalVolumetricFog fog in fogArray)
            {
                fog.parameters.textureScrollingSpeed = Vector3.zero;
                fog.parameters.meanFreePath = Mathf.Max(5f, fog.parameters.meanFreePath + additionalMeanFreePath); 
            }
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

        internal (RenderTexture, RenderTexture, RenderTexture, Texture2D) RefreshMaskTexture(int groundObjectsCount,
                                                                                                    bool getBuffers = false)
        {
            if (groundObjectsCount == 0)
            {
                Debug.LogError("No ground objects to render snow on!");
                return (null!, null!, null!, null!);
            }
            
            RenderTexture tempRT = null!;
            RenderTexture blurRT1 = null!;
            RenderTexture blurRT2 = null!;
            Texture2D maskLayer = null!;

            if (getBuffers)
            {
                tempRT = RenderTexture.GetTemporary(BakeResolution, BakeResolution, 0, RenderTextureFormat.ARGBFloat);
                tempRT.wrapMode = TextureWrapMode.Clamp;
                tempRT.filterMode = FilterMode.Trilinear;

                blurRT1 = RenderTexture.GetTemporary(BakeResolution, BakeResolution, 0, RenderTextureFormat.ARGBFloat);
                blurRT1.wrapMode = TextureWrapMode.Clamp;
                blurRT1.filterMode = FilterMode.Trilinear;

                blurRT2 = RenderTexture.GetTemporary(BakeResolution, BakeResolution, 0, RenderTextureFormat.ARGBFloat);
                blurRT2.wrapMode = TextureWrapMode.Clamp;
                blurRT2.filterMode = FilterMode.Trilinear;
                
                maskLayer = new Texture2D(BakeResolution, BakeResolution, TextureFormat.RGBAFloat, false);
                maskLayer.wrapMode = TextureWrapMode.Clamp;
                maskLayer.filterMode = FilterMode.Trilinear;
            }

            // Bake the snow masks into a Texture2DArray
            snowMasks = new Texture2DArray(BakeResolution,
                                           BakeResolution,
                                           groundObjectsCount,
                                           TextureFormat.RGBAFloat,
                                           BakeMipmaps,
                                           false,
                                           true)
            {
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            return (tempRT, blurRT1, blurRT2, maskLayer);
        }

        private void BakeSnowMasks()
        {
            if (groundObjectCandidates.Count == 0 || snowMasks == null)
            {
                Debug.LogError("No ground objects to bake snow masks!");
                return;
            }
            BakeDepth(false);
            RefreshBakeMaterial();
            StartCoroutine(snowMasks.BakeMasks(groundObjectCandidates, bakeMaterial!, BakeMipmaps, BakeResolution));
        }

        #endregion
    }
}