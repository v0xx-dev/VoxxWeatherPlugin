using UnityEngine;
using VoxxWeatherPlugin.Weathers;
using System.Linq;
using System.Collections.Generic;
using GameNetcodeStuff;
using VoxxWeatherPlugin.Utils;
using UnityEngine.Rendering.HighDefinition;
using DunGen;
using VoxxWeatherPlugin.Patches;
using UnityEngine.VFX;

namespace VoxxWeatherPlugin.Behaviours
{
    public class SnowThicknessManager : MonoBehaviour
    {
        internal static SnowThicknessManager? Instance;

        [Header("Compute Shader")]
        [SerializeField]
        internal ComputeShader? snowThicknessComputeShader;
        [SerializeField]
        internal int maxEntityCount = 64;
        [SerializeField]
        internal SnowfallWeather? snowfallData => SnowfallWeather.Instance;
        private int kernelHandle;
        [SerializeField]
        internal bool inputNeedsUpdate = true;
        [SerializeField]
        internal Vector3 feetPosition; // player's feet position

        //Compute buffers
        internal EntitySnowData[]? entitySnowDataArray;
        private ComputeBuffer? entityDataComputeBuffer;

        // Mappings
        internal Dictionary<MonoBehaviour, int> entitySnowDataMap = new Dictionary<MonoBehaviour, int>();
        private Stack<int> freeIndices = new Stack<int>();
        internal Dictionary<GameObject, int> groundToIndex = new Dictionary<GameObject, int>();
        internal HashSet<GameObject> iceObjects = new HashSet<GameObject>();

        [Header("Ground")]
        [SerializeField]
        internal string[] groundTags = {"Grass", "Gravel", "Snow", "Rock"};
        [SerializeField]
        internal bool isOnNaturalGround => isPlayerOnNaturalGround();
        [SerializeField]
        internal bool isOnIce = false;

#if DEBUG
        [Header("Debug")]
        public List<string> groundsInfo = new List<string>();
        public List<string> entityInfo = new List<string>();
        private Dictionary<MonoBehaviour, RaycastHit> entityHitData = new Dictionary<MonoBehaviour, RaycastHit>();
        public List<string> entityHitInfo = new List<string>();
        public SerializableDictionary<GameObject, SnowTrackerData> snowTrackerData = new SerializableDictionary<GameObject, SnowTrackerData>();
        public SerializableDictionary<GameObject, float> entitySpeeds = new SerializableDictionary<GameObject, float>();
        public AudioReverbTrigger? reverbTrigger;

#endif
        public void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            kernelHandle = snowThicknessComputeShader!.FindKernel("CSMain");

            // Create buffers
            entityDataComputeBuffer = new ComputeBuffer(maxEntityCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(EntitySnowData)));
            entitySnowDataArray = new EntitySnowData[maxEntityCount];
            freeIndices = new Stack<int>(Enumerable.Range(0, maxEntityCount));
            snowThicknessComputeShader.SetBuffer(kernelHandle, "_EntityData", entityDataComputeBuffer);
        }

        internal void Reset()
        {
            inputNeedsUpdate = true;
            entitySnowDataMap.Clear();
            freeIndices.Clear();
            groundToIndex.Clear();
            iceObjects.Clear();
            isOnIce = false;
            freeIndices = new Stack<int>(Enumerable.Range(0, maxEntityCount));
        }

        internal void CalculateThickness()
        {
            if (snowfallData == null)
            {
                Debug.LogError("Snowfall Weather is null, cannot calculate snow thickness!");
                return;
            }

            if (groundToIndex.Count == 0)
            {
                Debug.LogError("No ground object is registered, cannot calculate snow thickness!");
                return;
            }

            if (inputNeedsUpdate)
            {
                // Update static input parameters
                snowThicknessComputeShader!.SetFloat(SnowfallShaderIDs.MaxSnowHeight, snowfallData.maxSnowHeight);
                // Set static texture buffers
                snowThicknessComputeShader.SetTexture(kernelHandle, SnowfallShaderIDs.SnowMasks, snowfallData?.snowMasks);
                snowThicknessComputeShader.SetTexture(kernelHandle, SnowfallShaderIDs.FootprintsTex, snowfallData?.snowTracksMap);
                inputNeedsUpdate = false;
#if DEBUG
                reverbTrigger = GameNetworkManager.Instance.localPlayerController.currentAudioTrigger;
                groundsInfo = new List<string>();
                foreach (KeyValuePair<GameObject, int> kvp in groundToIndex)
                {
                    groundsInfo.Add($"{kvp.Key.name} : {kvp.Value}");
                }

                entityInfo = new List<string>();
                foreach (KeyValuePair<MonoBehaviour, int> kvp in entitySnowDataMap)
                {
                    if (kvp.Key == null)
                    {
                        continue;
                    }
                    EntitySnowData snowData = entitySnowDataArray[kvp.Value];
                    entityInfo.Add($"{kvp.Key.gameObject.name}: wPos {snowData.w}, texPos {snowData.uv}, texIndex {snowData.textureIndex}, snowThickness {snowData.snowThickness}");
                }

                entityHitInfo = new List<string>();
                foreach (KeyValuePair<MonoBehaviour, RaycastHit> kvp in entityHitData)
                {
                    if (kvp.Key == null)
                    {
                        continue;
                    }
                    RaycastHit hit = kvp.Value;
                    string hitString = $"{kvp.Key.gameObject.name}: Valid for snow: {IsEntityValidForSnow(kvp.Key)}, Standing on: {hit.collider.gameObject.name}, On registered ground: {groundToIndex.ContainsKey(hit.collider.gameObject)})";
                    entityHitInfo.Add(hitString + $", wPos {hit.point}, texPos2 {hit.textureCoord2}");
                }

                snowTrackerData = new SerializableDictionary<GameObject, SnowTrackerData>();

                foreach (KeyValuePair<MonoBehaviour, VisualEffect> kvp in SnowPatches.snowTrackersDict)
                {
                    if (kvp.Key == null)
                    {
                        continue;
                    }
                    SnowTrackerData data = new SnowTrackerData();
                    data.isActive = kvp.Value.GetBool("isTracking");
                    data.particleSize = kvp.Value.GetFloat("particleSize");
                    data.lifetimeMultiplier = kvp.Value.GetFloat("lifetimeMultiplier");
                    data.footprintStrength = kvp.Value.GetFloat("footprintStrength");
                    data.particleNumber = kvp.Value.aliveParticleCount;
                    snowTrackerData[kvp.Key.gameObject] = data;
                }

                foreach (KeyValuePair<MonoBehaviour, int> kvp in entitySnowDataMap)
                {
                    if (kvp.Key is EnemyAI enemy)
                    {
                        entitySpeeds[enemy.gameObject] = enemy.agent.velocity.magnitude;
                    }
                }
#endif
            }

            // Update dynamic input parameters
            snowThicknessComputeShader?.SetVector(SnowfallShaderIDs.ShipPosition, snowfallData.shipPosition);
            snowThicknessComputeShader?.SetFloat(SnowfallShaderIDs.SnowNoisePower, snowfallData.snowIntensity);
            snowThicknessComputeShader?.SetMatrix(SnowfallShaderIDs.FootprintsViewProjection, snowfallData.snowTracksCamera!.projectionMatrix * snowfallData.snowTracksCamera.worldToCameraMatrix);
                
            // Update texture space positions
            // UpdatePositionData();
            entityDataComputeBuffer?.SetData(entitySnowDataArray);
            
            // Dispatch compute shader
            int threadGroupSizeX = Mathf.CeilToInt(Mathf.Sqrt(maxEntityCount));
            int threadGroupSizeY = Mathf.CeilToInt(Mathf.Sqrt(maxEntityCount));
            snowThicknessComputeShader?.Dispatch(kernelHandle, threadGroupSizeX, threadGroupSizeY, 1);

            // Read result
            entityDataComputeBuffer?.GetData(entitySnowDataArray);
        }

        internal bool isPlayerOnNaturalGround()
        {
            return isEntityOnNaturalGround(GameNetworkManager.Instance.localPlayerController);
        }

        internal bool isEntityOnNaturalGround(MonoBehaviour entity)
        {
            EntitySnowData? data = GetEntityData(entity);
            return data.HasValue ? data.Value.textureIndex != -1 : false;
        }

        internal float GetSnowThickness(MonoBehaviour entity)
        {
            EntitySnowData? data = GetEntityData(entity);
            return data.HasValue ? data.Value.snowThickness : 0f;
        }

        internal bool IsEntityValidForSnow(MonoBehaviour entity)
        {
            if (entity is PlayerControllerB player)
            {
                return player.isPlayerControlled && !player.isPlayerDead && !player.isInsideFactory;
            }
            else if (entity is EnemyAI enemy)
            {
                return enemy.isOutside && !enemy.isEnemyDead;
            }
            return false;
        }
        
        internal EntitySnowData? GetEntityData(MonoBehaviour entity)
        {
            if (entitySnowDataMap.TryGetValue(entity, out int index))
            {
                return entitySnowDataArray?[index];
            }
            return null;
        }

        internal void UpdateEntityData(MonoBehaviour entity, RaycastHit hit)
        {
            if (entitySnowDataArray == null)
            {
                Debug.LogError("Entity snow data array is null, cannot update entity data!");
                return;
            }

            int index;
            if (!entitySnowDataMap.ContainsKey(entity))
            {
                if (freeIndices.Count > 0)
                {
                    index = freeIndices.Pop();
                }
                else
                {
                    Debug.LogDebug("Entity count for snow tracking exceeds the maximum limit, won't add more entities!");
                    return;
                }

                entitySnowDataMap[entity] = index;
                entitySnowDataArray[index] = new EntitySnowData();
            }
            else
            {
                index = entitySnowDataMap[entity];
            }

            EntitySnowData data = entitySnowDataArray[index];

            if (IsEntityValidForSnow(entity) && groundToIndex.ContainsKey(hit.collider.gameObject))
            {
                int textureIndex = groundToIndex[hit.collider.gameObject];
                data!.w = hit.point;
                data.uv = new Vector2(hit.textureCoord2.x, hit.textureCoord2.y);
                data.textureIndex = textureIndex;
            }
            else
            {
                data.Reset();
            }

            entitySnowDataArray[index] = data;

            //Update fields for a local player
            if (entity == GameNetworkManager.Instance.localPlayerController) 
            {
                feetPosition = hit.point;
                isOnIce = iceObjects.Contains(hit.collider.gameObject);
            }

#if DEBUG
            // Copy the hit data for debugging
            RaycastHit hitCopy = hit;
            entityHitData[entity] = hitCopy;
#endif 

        }

        internal void RemoveEntityData(MonoBehaviour entity)
        {
            if (entitySnowDataMap.TryGetValue(entity, out int index))
            {
                entitySnowDataMap.Remove(entity);
                entitySnowDataArray?[index].Reset();
                freeIndices.Push(index);
            }
        }

        void OnDestroy()
        {
            // Clean up
            entityDataComputeBuffer?.Release();
        }
    }
}


