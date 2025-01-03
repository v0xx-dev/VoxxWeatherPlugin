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
using UnityEngine.AI;
using Unity.AI.Navigation;
using TerraMesh;

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
        internal Material? snowVertexOpaqueMaterial;
        internal Material? CurrentSnowVertexMaterial => Configuration.useOpaqueSnowMaterial.Value ? snowVertexOpaqueMaterial : snowVertexMaterial;
        [SerializeField]
        internal Material? iceMaterial;
        [SerializeField]
        internal float emissionMultiplier;

        [Header("Base Snow Thickness")]
        [SerializeField]
        internal float snowScale = 1.0f;
        [SerializeField, Tooltip("The snow intensity is the power of the exponential function, so 0.0f is full snow.")]
        internal float snowIntensity = 10.0f; // How much snow is on the ground
        internal float MinSnowHeight => Configuration.minSnowHeight.Value;
        internal float MaxSnowHeight => Configuration.maxSnowHeight.Value;
        [SerializeField]
        internal float finalSnowHeight = 2f;
        [SerializeField]
        internal float MinSnowNormalizedTime => Configuration.minTimeToFullSnow.Value;
        [SerializeField]
        internal float MaxSnowNormalizedTime => Configuration.maxTimeToFullSnow.Value;
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
        [SerializeField]
        internal int BlurRadius => Configuration.BlurKernelSize.Value;
        [SerializeField]
        internal Texture2DArray? snowMasks; // Texture2DArray to store the snow masks
        internal bool BakeMipmaps => Configuration.bakeSnowDepthMipmaps.Value;

        [Header("TerraMesh Parameters")]

        [SerializeField]
        internal TerraMeshConfig terraMeshConfig = new TerraMeshConfig(
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
            renderingLayerMask : 0,
            // Terrain conversion
            minMeshStep : Configuration.minMeshStep.Value,
            maxMeshStep : Configuration.maxMeshStep.Value,
            falloffSpeed : Configuration.falloffRatio.Value,
            targetVertexCount : Configuration.targetVertexCount.Value,
            carveHoles : Configuration.carveHoles.Value,
            refineMesh : Configuration.refineMesh.Value,
            useMeshCollider : Configuration.useMeshCollider.Value,
            copyTrees : false,
            copyDetail : false
        );
        
        [Header("General")]
        [SerializeField]
        internal Vector3 shipPosition;
        [SerializeField]
        internal float heightThreshold = -100f; // Under this y coordinate, objects will not be considered for snow rendering
        [SerializeField]
        internal float timeUntilFrostbite = 0.6f * (Configuration.minTimeUntilFrostbite?.Value ?? 30f);
        [SerializeField]
        internal SnowfallVFXManager? VFXManager;
        string[] moonProcessingBlacklist = [];
        [SerializeField]
        internal QuicksandTrigger[]? waterTriggerObjects;
        [SerializeField]
        internal List<GameObject> waterSurfaceObjects = [];
        [SerializeField]
        internal List<GameObject> groundObjectCandidates = [];
        internal string currentSceneName => StartOfRound.Instance?.currentLevel.sceneName ?? "";
        internal Bounds levelBounds;
        internal System.Random? seededRandom;
        private HDAdditionalLightData? sunLightData;


#if DEBUG

        public bool rebakeMaps = false;

#endif

        internal void Awake()
        {   
            Instance = this;

            // Alpha test pass
            snowOverlayCustomPass = snowVolume!.customPasses[1] as SnowOverlayCustomPass;
            snowOverlayCustomPass!.snowOverlayMaterial = Instantiate(snowOverlayMaterial);
            // No alpha test pass
            snowOverlayCustomPass = snowVolume!.customPasses[0] as SnowOverlayCustomPass;
            snowOverlayCustomPass!.snowOverlayMaterial = snowOverlayMaterial;
            
            snowOverlayCustomPass.SetupMaterial(snowOverlayMaterial);
            snowOverlayCustomPass.SetupMaterial(CurrentSnowVertexMaterial);
            // if (Configuration.fixPosterizationForSnowOverlay.Value)
            // {
            //     // Increase normal strength and change color for snow overlay material
            //     snowOverlayMaterial!.SetFloat(SnowfallShaderIDs.NormalStrength, 10f);
            //     snowOverlayMaterial.SetColor(SnowfallShaderIDs.SnowColor, new Color(0.1657f, 0.1670f, 0.2075f, 1f));
            //     snowOverlayMaterial.SetFloat(SnowfallShaderIDs.Metallic, 1f);
            // }
            terraMeshConfig.renderingLayerMask = (uint)(snowOverlayCustomPass?.renderingLayers ?? 0);

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
            depthVSMPass!.blurRadius = BlurRadius;
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
            snowTracksCamera!.targetTexture = snowTracksMap;
            snowTracksCamera.aspect = 1.0f;

            moonProcessingBlacklist = Configuration.meshProcessingBlacklist.Value.CleanMoonName().TrimEnd(';').Split(';');
        }

        internal void OnStart()
        {
            Instance = this; // Change the global reference to this instance (for patches)
            seededRandom = new System.Random(StartOfRound.Instance.randomMapSeed);
            sunLightData = TimeOfDay.Instance.sunDirect?.GetComponent<HDAdditionalLightData>();
            snowScale = seededRandom.NextDouble(0.7f, 1.3f); // Snow patchy-ness
            finalSnowHeight = seededRandom.NextDouble(MinSnowHeight, MaxSnowHeight);
            fullSnowNormalizedTime = seededRandom.NextDouble(MinSnowNormalizedTime, MaxSnowNormalizedTime);
            levelBounds = PlayableAreaCalculator.CalculateZoneSize(1.5f);
            terraMeshConfig.levelBounds = levelBounds;
            ModifyRenderMasks();
            SetupGround();
            ModifyScrollingFog();
            if (Configuration.freezeWater.Value)
            {
                FreezeWater();
            }
            UpdateLevelDepthmap();
            StartCoroutine(RefreshDepthmapCoroutine(levelDepthmapCamera!, levelDepthmap!, bakeSnowMaps: false, waitForLanding: true));
        }

        internal void OnFinish()
        {
            Destroy(snowMasks);
            groundObjectCandidates.Clear();
            waterSurfaceObjects.Clear();
            snowOverlayCustomPass?.snowVertexMaterials.Clear();
            PlayerEffectsManager.normalizedTemperature = 0f;
        }

        internal virtual void OnEnable()
        {
            OnStart();
            VFXManager?.PopulateLevelWithVFX();
        }

        internal virtual void OnDisable()
        {
            OnFinish();
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
            float normalizedSnowTimer =  Mathf.Clamp01(fullSnowNormalizedTime - TimeOfDay.Instance.normalizedTimeOfDay);
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
            PlayerEffectsManager.isInColdZone = VFXManager!.isUnderSnowPreviousFrame;
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
            // Set orthographic size to half of the level bounds
            levelDepthmapCamera.orthographicSize = (int)Mathf.Max(levelBounds.size.x, levelBounds.size.z) / 2;
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
            
            camera.enabled = false;

            if (bakeSnowMaps)
            {
                BakeSnowMasks();
            }

            SnowThicknessManager.Instance!.inputNeedsUpdate = true;
        }

        internal void FreezeWater()
        {
            // Measure time
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            waterTriggerObjects = FindObjectsOfType<QuicksandTrigger>().Where(x => x.enabled &&
                                                                                x.gameObject.activeInHierarchy &&
                                                                                x.isWater &&
                                                                                x.gameObject.scene.name == currentSceneName &&
                                                                                !x.isInsideWater).ToArray();

            HashSet<GameObject> iceObjects = new HashSet<GameObject>();
            stopwatch.Restart();
            NavMeshModifierVolume[] navMeshModifiers = FindObjectsOfType<NavMeshModifierVolume>().Where(x => x.gameObject.activeInHierarchy &&
                                                                                    x.gameObject.scene.name == currentSceneName &&
                                                                                    x.transform.position.y > heightThreshold &&
                                                                                    x.enabled).ToArray();
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
                MeshFilter meshFilter = waterSurface.GetComponent<MeshFilter>();
                Mesh meshCopy = meshFilter.sharedMesh.MakeReadableCopy();
                meshFilter.sharedMesh = meshCopy;
                
                //Get renderer component, we know it exists since we checked it in the CheckIfObjectIsTerrain method
                Renderer renderer = waterSurface.GetComponent<Renderer>();

                //Check for collider and if it exists, destroy it, otherwise add a mesh collider
                // Only freeze water surfaces above the height threshold
                if (waterSurface.transform.position.y > heightThreshold)
                {
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
                        Bounds waterBounds = renderer.bounds;
                        // Enlarge along y axis since water surfaces are thin
                        waterBounds.size = new Vector3(waterBounds.size.x, 2f, waterBounds.size.z);
                        if (bounds.Intersects(waterBounds))
                        {
                            Debug.LogDebug($"Disabling NavMeshModifierVolume {navMeshModifier.name} intersecting with water surface {waterSurface.name}");
                            navMeshModifier.enabled = false;
                        }
                    }
                }
                else continue;

                renderer.sharedMaterial = iceMaterial;

                // Rise slightly
                waterSurface.transform.position += 0.6f*Vector3.up;
                
                // Change footstep sounds
                waterSurface.tag = "Rock";
                iceObjects.Add(waterSurface);
            }
            // Store the ice objects
            SnowThicknessManager.Instance!.iceObjects = iceObjects;
            // Rebake NavMesh
            GameObject navMeshContainer = GameObject.FindGameObjectWithTag("OutsideLevelNavMesh");
            if (navMeshContainer != null)
            {
                NavMeshSurface navMesh = navMeshContainer.GetComponent<NavMeshSurface>();
                navMesh.UpdateNavMesh(navMesh.navMeshData);
                Debug.LogDebug("NavMesh rebaked for ice!");
            }

            stopwatch.Stop();
            Debug.LogDebug($"Freezing water took {stopwatch.ElapsedMilliseconds} ms");
        }

        internal void ModifyScrollingFog()
        {
            LocalVolumetricFog[] fogArray = FindObjectsOfType<LocalVolumetricFog>();
            fogArray = fogArray.Where(x => x.gameObject.activeSelf &&
                                        x.gameObject.scene.name == currentSceneName &&
                                        x.transform.position.y > heightThreshold).ToArray();
            float additionalMeanFreePath = seededRandom!.NextDouble(5f, 15f);
            foreach (LocalVolumetricFog fog in fogArray)
            {
                fog.parameters.textureScrollingSpeed = Vector3.zero;
                fog.parameters.meanFreePath += additionalMeanFreePath; 
            }
        }

        private Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            GameObject.Destroy(temp);
            return mesh;
        }

        internal void SetupGround()
        {
            // TODO: Make this async

            // Stores mesh terrains and actual Unity terrains to keep track of of walkable ground objects and their texture index in the baked masks
            Dictionary <GameObject, int> groundToIndex = new Dictionary<GameObject, int>();
            // Some moons used TerraMesh package and already have good mesh terrains, skip them
            bool isMoonBlacklisted = false;
            foreach (string moon in moonProcessingBlacklist)
            {
                if (currentSceneName.CleanMoonName().Contains(moon))
                {
                    isMoonBlacklisted = true;
                    break;
                }
            }
            // For Experimentation moon, UVs are broken and need to be replaced
            terraMeshConfig.replaceUvs = StartOfRound.Instance.currentLevel.name.CleanMoonName().Contains("experimentation");
            terraMeshConfig.onlyUVs = isMoonBlacklisted;
            if (isMoonBlacklisted)
            {
                Debug.LogDebug($"Moon {StartOfRound.Instance.currentLevel.name} is blacklisted for mesh postprocessing! Skipping...");
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
                GameObject meshTerrain = terrain.Meshify(terraMeshConfig);
                // Modify render mask to support overlay snow rendering
                MeshRenderer meshRenderer = meshTerrain.GetComponent<MeshRenderer>();
                meshRenderer.renderingLayerMask |= terraMeshConfig.renderingLayerMask;
                // Setup the Lit terrain material
                Material terrainMaterial = meshRenderer.sharedMaterial;
                terrainMaterial.SetupMaterialFromTerrain(terrain);
                // Setup the index in the material property block for the snow masks
                PrepareMeshForSnow(meshTerrain, textureIndex);
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

        internal void PrepareMeshForSnow(GameObject meshTerrain, int texId)
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
            // Override the material property block to set the object ID to sample from a single Texture2DArray
            // This brakes batching
            // MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            // propertyBlock.SetFloat(SnowfallShaderIDs.TexIndex, texId);
            // meshRenderer.SetPropertyBlock(propertyBlock);
        }

        internal void BakeSnowMasks()
        {
            //TODO Make async

            if (groundObjectCandidates.Count == 0)
            {
                Debug.LogDebug("No ground objects to bake snow masks for!");
                return;
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
            
            for (int texIndex = 0; texIndex < groundObjectCandidates.Count; texIndex++)
            {
                groundObjectCandidates[texIndex].BakeMask(this, texIndex);
            }

            snowMasks.Apply(updateMipmaps: BakeMipmaps, makeNoLongerReadable: true); // Move to the GPU

            foreach (Material snowVertexMaterial in snowOverlayCustomPass!.snowVertexMaterials)
            {
                snowVertexMaterial?.SetTexture(SnowfallShaderIDs.SnowMasks, snowMasks);
            }
        }

        internal void ModifyRenderMasks()
        {
            List<GameObject> objectsAboveThreshold = GetObjectsAboveThreshold();

            foreach (GameObject obj in objectsAboveThreshold)
            {
                if (obj.TryGetComponent<MeshRenderer>(out MeshRenderer meshRenderer))
                {
                    meshRenderer.renderingLayerMask |= terraMeshConfig.renderingLayerMask;
                }
            }
        }

        public List<GameObject> GetObjectsAboveThreshold()
        {
            GameObject dungeonAnchor = FindAnyObjectByType<RuntimeDungeon>().Root;
            // Set threshold to 1/3 of distance from the origin to the top of the dungeon
            heightThreshold = -Mathf.Abs(dungeonAnchor.transform.position.y/3); 

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

                    // Collect water surface objects. Must have "water" in the name and no active collider
                    if (nameString.Contains("water") &&
                        !(obj.TryGetComponent<Collider>(out Collider waterCollider)
                        && waterCollider.enabled))
                    {   
                        //Check if scaled mesh bounds are big enough to be considered a water surface, but also thin enough
                        Bounds bounds = meshRenderer.bounds;
                        float sizeThreshold = 20f;
                        if (bounds.size.x > sizeThreshold && bounds.size.z > sizeThreshold && bounds.size.y < 2f)
                        {
                            waterSurfaceObjects.Add(obj);
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

        internal void RefreshBakeMaterial()
        {
            // Set shader properties
            bakeMaterial?.SetFloat(SnowfallShaderIDs.SnowNoiseScale, snowScale);
            bakeMaterial?.SetFloat(SnowfallShaderIDs.ShadowBias, shadowBias);
            bakeMaterial?.SetFloat(SnowfallShaderIDs.PCFKernelSize, PCFKernelSize);
            bakeMaterial?.SetFloat(SnowfallShaderIDs.BlurKernelSize, BlurRadius);
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
    }

    public class SnowfallVFXManager: BaseVFXManager
    {
        [SerializeField]
        internal bool addedVanillaFootprints = false;

        [Header("Snow VFX")]
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
        private float UnderSnowVisualMultiplier => Configuration.underSnowFilterMultiplier.Value;
        private readonly float fadeSpeed = 2f; // Units per second
        private bool isFading = false;
        internal bool isUnderSnowPreviousFrame = false;
        [SerializeField]
        internal float eyeBias = 0.43f;

        [Header("Snow Tracker VFX")]
        
        [SerializeField]
        internal VisualEffectAsset[]? footprintsTrackerVFX;
        internal static Dictionary <string, VisualEffectAsset>? snowTrackersDict;

        [Header("Christmas Event")]
        [SerializeField]
        private GameObject? christmasTreePrefab;
        private Item? giftBoxItem;

        internal void Start()
        {
            // Find the snow footstep index
            snowFootstepIndex = Array.FindIndex(StartOfRound.Instance.footstepSurfaces, surface => surface.surfaceTag == "Snow");
            // Find the gift box item in the item database
            giftBoxItem = StartOfRound.Instance.allItemsList.itemsList.FirstOrDefault(item => item.name == "GiftBox");
        }

        internal virtual void OnEnable()
        {
            snowVFXContainer?.SetActive(true);
            PlayerEffectsManager.freezeEffectVolume = frostbiteFilter;
            
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
            PlayerEffectsManager.isInColdZone = false;
            isUnderSnowPreviousFrame = false;
        }

        internal override void Reset()
        {
            addedVanillaFootprints = false;
            PlayerEffectsManager.isInColdZone = false;
            SnowThicknessManager.Instance?.Reset();
            SnowTrackersManager.CleanupFootprintTrackers(SnowTrackersManager.snowTrackersDict);
            SnowTrackersManager.CleanupFootprintTrackers(SnowTrackersManager.snowShovelDict);
            SnowTrackersManager.ToggleFootprintTrackers(false);
        }

        internal void Update()
        {   
            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
            
            if ((SnowThicknessManager.Instance?.isOnNaturalGround ?? false) &&
                    localPlayer.physicsParent == null &&
                    !localPlayer.isPlayerDead &&
                    localPlayer.thisController.isGrounded)
            {
                float snowThickness = SnowThicknessManager.Instance.GetSnowThickness(localPlayer);
                // White out the screen if the player is under snow
                float localPlayerEyeY = localPlayer.gameplayCamera.transform.position.y;
                bool isUnderSnow = SnowThicknessManager.Instance.feetPositionY + snowThickness >= localPlayerEyeY - eyeBias;

                if (isUnderSnow != isUnderSnowPreviousFrame)
                {
                    StartFade(isUnderSnow ? 1f : 0f);
                }

                isUnderSnowPreviousFrame = isUnderSnow;
                UpdateFade();

                // If the user decreases frostbite damage from the default value (10), add additional slowdown
                float metaSnowThickness = Mathf.Clamp01(1 - SnowPatches.FrostbiteDamage/10f) * PlayerEffectsManager.ColdSeverity;

                // Slow down the player if they are in snow (only if snow thickness is above 0.4, caps at 2.5 height)
                snowMovementHindranceMultiplier = 1 + 5*Mathf.Clamp01((snowThickness + metaSnowThickness - 0.4f)/2.1f);

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

            // If normalized snow timer is at 30% of fullSnowNormalizedTime, turn on vanilla footprints
            if (Configuration.addFootprints.Value &&
                !addedVanillaFootprints &&
                !StartOfRound.Instance.currentLevel.levelIncludesSnowFootprints
                && (SnowfallWeather.Instance?.snowIntensity ?? 10f) < 7)
            {
                StartOfRound.Instance.InstantiateFootprintsPooledObjects();
                addedVanillaFootprints = true;
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

                underSnowFilter!.weight = currentWeight * UnderSnowVisualMultiplier;
            }
        }

        internal override void PopulateLevelWithVFX(Bounds levelBounds = default, System.Random? seededRandom = null)
        {
            if (!(SnowfallWeather.Instance is BlizzardWeather)) // to avoid setting the depth texture for blizzard
            {
                HDRPCameraOrTextureBinder? depthBinder = snowVFXContainer!.GetComponent<HDRPCameraOrTextureBinder>();
                if (depthBinder != null)
                {
                    Debug.LogDebug("Binding depth texture to snow VFX");
                    depthBinder.depthTexture = SnowfallWeather.Instance!.levelDepthmapUnblurred; // bind the baked depth texture
                }
            }

            SnowTrackersManager.ToggleFootprintTrackers(true);

            SnowfallWeather.Instance.snowVolume!.enabled = true;
            SnowfallWeather.Instance.snowTrackerCameraContainer?.SetActive(true);

            // For blizzard weather prefab won't be set
            if (christmasTreePrefab == null || !Configuration.enableEasterEgg.Value)
            {
                return;
            }
            // If the current date is +- 2 days from 25th December, 31st December or 6th January, spawn a Christmas tree
            DateTime currentDate = DateTime.Now;
            HashSet<DateTime> christmasDates = GetChristmasDates(currentDate);
            if (christmasDates.Contains(currentDate))
            {
                JingleBells();
            }

#if DEBUG
            JingleBells();
#endif

        }

        private HashSet<DateTime> GetChristmasDates(DateTime currentDate)
        {
            // Get dates that are -2 days away from 25th December, 31st December or 6th January
            HashSet<DateTime> christmasDates =
            [
                new DateTime(currentDate.Year, 12, 23),
                new DateTime(currentDate.Year, 12, 24),
                new DateTime(currentDate.Year, 12, 25),
                new DateTime(currentDate.Year, 12, 29),
                new DateTime(currentDate.Year, 12, 30),
                new DateTime(currentDate.Year, 12, 31),
                new DateTime(currentDate.Year, 1, 4),
                new DateTime(currentDate.Year, 1, 5),
                new DateTime(currentDate.Year, 1, 6),
            ];

            return christmasDates;
        }

        private void JingleBells()
        {
            if (giftBoxItem == null)
            {
                Debug.LogError("Gift box item not found in the item database!");
                return;
            }
            
            Debug.Log("Merry Christmas!");
            
            System.Random randomizer = SnowfallWeather.Instance!.seededRandom!;

            int attempts = 24;
            bool treePlaced = false;
            Vector3 treePosition = Vector3.zero;
            while (attempts-- > 0)
            {
                // Select a random position in the level from RoundManager.Instance.outsideAINodes
                int randomIndex = randomizer.Next(0, RoundManager.Instance.outsideAINodes.Length);
                Vector3 anchor = RoundManager.Instance.outsideAINodes[randomIndex].transform.position;
                // Sample another random position using navmesh around the anchor where there is at least 10x10m of space
                Vector3 randomPosition = RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(anchor, 25f, randomSeed: randomizer);
                randomPosition = RoundManager.Instance.PositionEdgeCheck(randomPosition, 7f);
                if (randomPosition != Vector3.zero)
                {
                    treePosition = randomPosition;
                    treePlaced = true;
                    break;
                }
            }
            
            if (!treePlaced)
            {
                Debug.LogDebug("Failed to place a Christmas tree in the level, too many attempts!");
                return;
            }

            Quaternion randomRotation = Quaternion.Euler(0, randomizer.Next(0, 360), 0);
            // Spawn a Christmas tree
            _ = Instantiate(christmasTreePrefab!, treePosition, randomRotation);

            // Only host can spawn the presents
            if (!GameNetworkManager.Instance.isHostingGame)
            {
                return;
            }

            // Spawn a gift box for each player in the game. Cap at 4 gifts so users with more than 4 players don't get too many
            int numGifts = Mathf.Min(GameNetworkManager.Instance.connectedPlayers, 4);

            NavMeshHit hit;
            for (int i = 0; i < numGifts; i++)
            {
                int giftValue = randomizer.Next(1, 24);

                //Spawn gifts in a ring around the tree by sampling the NavMesh around it
                Vector3 giftPosition = treePosition + 2f * new Vector3(Mathf.Cos(i * 2 * Mathf.PI / numGifts), 0, Mathf.Sin(i * 2 * Mathf.PI / numGifts));
                if (NavMesh.SamplePosition(giftPosition, out hit, 2f, NavMesh.AllAreas))
                {
                    giftBoxItem.SpawnAtPosition(hit.position, giftValue);
                }
            }
        }

        internal void OnDestroy()
        {
            SnowTrackersManager.snowTrackersDict.Clear();
            SnowTrackersManager.snowShovelDict.Clear();
        }
    }
}