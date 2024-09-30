using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Experimental.Rendering;
using VoxxWeatherPlugin.Behaviours;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using DunGen;
using VoxxWeatherPlugin.Utils;

namespace VoxxWeatherPlugin.Weathers
{
    public class SnowfallWeather: MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField]
        internal Material iceMaterial;
        [SerializeField]
        internal CustomPassVolume snowVolume;
        [SerializeField]
        internal CustomPassVolume depthCopyVolume;
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
        internal float snowIntensity = 0.0f;
        [SerializeField]
        internal float maxSnowHeight = 2.0f;

        [Header("Snow Occlusion")]
        [SerializeField]
        internal Camera levelDepthmapCamera;
        [SerializeField]
        internal RenderTexture levelDepthmap;
        [SerializeField]
        internal uint PCFKernelSize = 6;
        [SerializeField]
        internal float shadowBias = 0.001f;
        [SerializeField]
        internal float snowOcclusionBias = 0.01f;

        [Header("Snow Tracks")]
        [SerializeField]
        internal Camera snowTracksCamera;
        [SerializeField]
        internal RenderTexture snowTracksMap;

        [Header("Tessellation Parameters")]
        [SerializeField]
        internal float baseTessellationFactor = 4.0f;
        [SerializeField]
        internal float maxTessellationFactor = 12.0f;

        [SerializeField]
        internal Vector3 shipPosition;
        [SerializeField]
        internal GameObject groundObject;
        internal QuicksandTrigger[] waterObjects;

        internal SnowThicknessCalculator snowThicknessCalculator;

        internal void Start()
        {
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

            RenderTexture levelDepthmap = new RenderTexture(2048, 
                                                2048,
                                                16, // Depth bits
                                                RenderTextureFormat.Depth, // Use a depth format
                                                RenderTextureReadWrite.Linear); // Linear color space
            levelDepthmap.wrapMode = TextureWrapMode.Clamp;
            levelDepthmap.useMipMap = false;
            levelDepthmap.enableRandomWrite = true;
            levelDepthmap.useDynamicScale = true;
            levelDepthmap.name = "Level Depthmap";
            levelDepthmap.Create();

            snowTracksMap = new RenderTexture(256,
                                            256,
                                            16, // Depth bits
                                            RenderTextureFormat.Depth, // Use a depth format
                                            RenderTextureReadWrite.Linear); // Linear color space
            snowTracksMap.filterMode = FilterMode.Point;
            snowTracksMap.wrapMode = TextureWrapMode.Clamp;
            snowTracksMap.useMipMap = false;
            snowTracksMap.enableRandomWrite = true;
            snowTracksMap.useDynamicScale = true;
            snowTracksMap.name = "Snow Tracks Map";
            snowTracksMap.Create();

            levelDepthmapCamera.targetTexture = levelDepthmap;
            levelDepthmapCamera.aspect = 1.0f;
            snowTracksCamera.targetTexture = snowTracksMap;
            snowTracksCamera.aspect = 1.0f;

            DepthCopyPass depthCopyPass = depthCopyVolume.customPasses[0] as DepthCopyPass;
            if (depthCopyPass == null)
            {
                Debug.LogError("SnowfallWeather requires a DepthCopyPass in the depth copy volume!");
            }
            depthCopyPass.depthMap = levelDepthmap;
        }

        internal void OnEnable()
        {
            snowThicknessCalculator.inputNeedsUpdate = true;
            FindGround();
            FreezeWater();
            Debug.LogWarning("Disabling terrain instancing for snowfall weather!");
            SwitchTerrainInstancing(false);
            ModifyRenderMasks();
            // Set the camera position to the center of the level
            (_ , Vector3 levelBarycenter) = PlayableAreaCalculator.CalculateZoneSize();
            Vector3 cameraPosition = new Vector3(levelBarycenter.x, levelDepthmapCamera.transform.position.y, levelBarycenter.z);
            levelDepthmapCamera.transform.position = cameraPosition;
            levelDepthmapCamera.Render();
        }

        internal void OnDestroy()
        {
            // Release the render textures
            levelDepthmap.Release();
            snowTracksMap.Release();
        }

        internal void SwitchTerrainInstancing(bool enable)
        {
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
            waterObjects = GameObject.FindObjectsOfType<QuicksandTrigger>().Where(x => x.gameObject.activeSelf && x.isWater).ToArray();
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
                if (waterObject.TryGetComponent<BoxCollider>(out BoxCollider boxCollider))
                {
                    boxCollider.isTrigger = false;
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
            string[] groundTags = {"Grass", "Gravel", "Snow" };
            Terrain mainTerrain;

            Terrain[] terrains = Terrain.activeTerrains;
            terrains = terrains.Where(x => x.gameObject.activeSelf).ToArray();
            terrains = terrains.OrderBy(x => Vector3.Distance(x.transform.position, flattenedShipPosition)).ToArray();
            mainTerrain = terrains.Length > 0 ? terrains[0] : null;

            if (mainTerrain != null)
            {
                groundObject = mainTerrain.gameObject;
            }
            else
            {
                List<GameObject> groundCandidates = new List<GameObject>();
                foreach (string tag in groundTags)
                {
                    groundCandidates.AddRange(GameObject.FindGameObjectsWithTag(tag));
                }
                
                groundCandidates = groundCandidates.Where(x => x.gameObject.activeSelf).ToList();
                // Filter by name???
                //groundCandidates = groundCandidates.Where(x => x.name.Contains("Terrain")).ToList();
                groundCandidates = groundCandidates.OrderBy(x => Vector3.Distance(x.transform.position, shipPosition)).ToList();
                groundObject = groundCandidates.Count > 0 ? groundCandidates[0] : null;
            }

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
                Renderer renderer = obj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.renderingLayerMask = (uint)snowOverlayCustomPass.renderingLayers;
                }
            }
            groundObject.GetComponent<Renderer>().renderingLayerMask = (uint)snowVertexCustomPass.renderingLayers;
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
    }
}