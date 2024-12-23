using UnityEngine;
using VoxxWeatherPlugin.Weathers;
using System.Linq;
using System.Collections.Generic;
using GameNetcodeStuff;
using VoxxWeatherPlugin.Utils;
using UnityEngine.Rendering;
using Unity.Collections;

namespace VoxxWeatherPlugin.Behaviours
{
    public class SnowThicknessManager : MonoBehaviour
    {
        internal static SnowThicknessManager? Instance;
        internal int errorCount = 0;

        [Header("Compute Shader")]
        [SerializeField]
        internal ComputeShader? snowThicknessComputeShader;
        [SerializeField]
        internal int MaxEntityCount => Configuration.trackedEntityNumber.Value;
        [SerializeField]
        internal SnowfallWeather? snowfallData => SnowfallWeather.Instance;
        private int kernelHandle;
        [SerializeField]
        internal bool inputNeedsUpdate = false;

        //Compute buffers
        [SerializeField]
        private EntitySnowData[]? entitySnowDataInArray;
        [SerializeField]
        private EntitySnowData[]? entitySnowDataOutArray;
        
        private ComputeBuffer? entityDataComputeBuffer;
        private AsyncGPUReadbackRequest _readbackRequest;
        [SerializeField]
        private bool canDispatch = true;

        // Mappings
        public Dictionary<MonoBehaviour, int> entitySnowDataMap = new Dictionary<MonoBehaviour, int>();
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
        [SerializeField]
        internal float feetPositionY = 0f; // player's feet position
        [SerializeField]
        internal float snowThicknessOffset = 0f;

#if DEBUG
        [Header("Debug")]
        public List<string> groundsInfo = new List<string>();
        public string[]? entityInfo;
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
            entityDataComputeBuffer = new ComputeBuffer(MaxEntityCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(EntitySnowData)));
            entitySnowDataInArray = new EntitySnowData[MaxEntityCount];
            entitySnowDataOutArray = new EntitySnowData[MaxEntityCount];
            freeIndices = new Stack<int>(Enumerable.Range(0, MaxEntityCount).Reverse());
            snowThicknessComputeShader.SetBuffer(kernelHandle, "_EntityData", entityDataComputeBuffer);
        }

        internal void Reset()
        {
            errorCount = 0;
            inputNeedsUpdate = false;
            entitySnowDataMap.Clear();
            groundToIndex.Clear();
            iceObjects.Clear();
            isOnIce = false;
            freeIndices = new Stack<int>(Enumerable.Range(0, MaxEntityCount).Reverse());
        }

        internal void CalculateThickness()
        {
            if (errorCount > 10)
            {
                // Too many errors, stop trying
                return;
            }
            if (snowfallData == null)
            {
                Debug.LogError("Snowfall Weather is null, cannot calculate snow thickness!");
                errorCount++;
                return;
            }

            if (groundToIndex.Count == 0)
            {
                Debug.LogError("No ground object is registered, cannot calculate snow thickness!");
                errorCount++;
                return;
            }


            if (inputNeedsUpdate)
            {
                if (snowfallData.snowMasks == null)
                {
                    Debug.LogError("Snow masks texture is null, cannot calculate snow thickness!");
                    errorCount++;
                    return;
                }

                // Update static input parameters
                snowThicknessComputeShader!.SetFloat(SnowfallShaderIDs.MaxSnowHeight, snowfallData.finalSnowHeight);
                // Set static texture buffers
                snowThicknessComputeShader.SetTexture(kernelHandle, SnowfallShaderIDs.SnowMasks, snowfallData.snowMasks);
                snowThicknessComputeShader.SetTexture(kernelHandle, SnowfallShaderIDs.FootprintsTex, snowfallData.snowTracksMap);
                inputNeedsUpdate = false;
#if DEBUG
                reverbTrigger = GameNetworkManager.Instance.localPlayerController.currentAudioTrigger;
                groundsInfo = new List<string>();
                foreach (KeyValuePair<GameObject, int> kvp in groundToIndex)
                {
                    groundsInfo.Add($"{kvp.Key.name} : {kvp.Value}");
                }

                entityInfo = new string[entitySnowDataOutArray!.Length];
                foreach (KeyValuePair<MonoBehaviour, int> kvp in entitySnowDataMap)
                {
                    if (kvp.Key == null)
                    {
                        continue;
                    }
                    EntitySnowData snowData = entitySnowDataOutArray[kvp.Value];
                    entityInfo[kvp.Value] = $"{kvp.Key.gameObject.name}: wPos {snowData.w}, texPos {snowData.uv}, texIndex {snowData.textureIndex}, snowThickness {GetSnowThickness(kvp.Key)}";
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
            snowThicknessComputeShader?.SetMatrix(SnowfallShaderIDs.FootprintsViewProjection, snowfallData.tracksWorldToClipMatrix ?? Matrix4x4.identity);

            if (canDispatch)
            {
                // Send data to GPU
                entityDataComputeBuffer?.SetData(entitySnowDataInArray);
                // Dispatch compute shader
                int threadGroupSizeX = Mathf.CeilToInt(Mathf.Sqrt(MaxEntityCount));
                int threadGroupSizeY = Mathf.CeilToInt(Mathf.Sqrt(MaxEntityCount));
                snowThicknessComputeShader?.Dispatch(kernelHandle, threadGroupSizeX, threadGroupSizeY, 1);

                // Read result

                // // Super slow and blocks main thread
                // entityDataComputeBuffer?.GetData(entitySnowDataOutArray);

                // Request data asynchronously
                _readbackRequest = AsyncGPUReadback.Request(entityDataComputeBuffer, OnCompleteReadback);

                canDispatch = false;
            }
        }

        private void OnCompleteReadback(AsyncGPUReadbackRequest request)
        {
            if (!request.done) // Not really necessary, but just in case
            {
                return;
            }

            if (request.hasError)
            {
                Debug.LogError("GPU readback request failed!");
                canDispatch = true;
                return;
            }

            // Copy data back to CPU
            NativeArray<EntitySnowData> data = request.GetData<EntitySnowData>();
            data.CopyTo(entitySnowDataOutArray);
            data.Dispose();

            canDispatch = true;
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
            if (!data.HasValue)
            {
                return 0f;
            }
            if (entity == GameNetworkManager.Instance.localPlayerController)
            {
                return Mathf.Clamp(data.Value.snowThickness - snowThicknessOffset, 0, 2*SnowfallWeather.Instance?.finalSnowHeight ?? 0f);
            }
            return data.Value.snowThickness;
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
                return entitySnowDataOutArray?[index];
            }
            return null;
        }

        internal void UpdateEntityData(MonoBehaviour entity, RaycastHit hit)
        {
            if (!GetSnowDataIndex(entity, out int index))
            {
                return;
            }

            bool hitGround = StoreSnowData(entity, index, hit);

            if (entity == GameNetworkManager.Instance.localPlayerController) 
            {
                snowThicknessOffset = 0f;
                isOnIce = iceObjects.Contains(hit.collider.gameObject);
            }

            if (!hitGround &&
                entity is PlayerControllerB player &&
                !player.isInsideFactory &&
                SnowfallWeather.Instance != null)
            {
                // For players, check the objects below the first hit object because snow might protrude through it due to precision errors in the depth buffer
                RaycastHit[] hits = Physics.RaycastAll(hit.point - 0.05f * Vector3.up, Vector3.down, SnowfallWeather.Instance.finalSnowHeight);
                foreach (RaycastHit hitBelow in hits)
                {
                    if (groundToIndex.ContainsKey(hitBelow.collider.gameObject))
                    {
                        StoreSnowData(entity, index, hitBelow);
                        if (player == GameNetworkManager.Instance.localPlayerController) 
                        {
                            // For the local player, calculate the snow thickness offset based on the difference between the player's feet position and the hit point
                            snowThicknessOffset = Mathf.Clamp(feetPositionY - hitBelow.point.y, 0f, SnowfallWeather.Instance?.finalSnowHeight ?? 0f);
                        }
                        break;
                    }
                }
            }

#if DEBUG
            // Copy the hit data for debugging
            RaycastHit hitCopy = hit;
            entityHitData[entity] = hitCopy;
#endif 
        }

        internal bool GetSnowDataIndex(MonoBehaviour entity, out int index)
        {
            index = -1; // To cause an error if not set and we try to use it
            if (entitySnowDataInArray == null)
            {
                Debug.LogError("Entity snow data array is null, cannot update entity data!");
                return false;
            }

            if (!entitySnowDataMap.ContainsKey(entity))
            {
                if (freeIndices.Count > 0)
                {
                    index = freeIndices.Pop();
                }
                else
                {
                    Debug.LogWarning("Entity count for snow tracking exceeds the maximum limit, won't add more entities!");
                    return false;
                }
                entitySnowDataMap[entity] = index;
                entitySnowDataInArray[index] = new EntitySnowData();
            }
            else
            {
                index = entitySnowDataMap[entity];
            }

            return true;
        }

        private bool StoreSnowData(MonoBehaviour entity, int index, RaycastHit hit)
        {
            EntitySnowData data = entitySnowDataInArray![index];
            bool isHitValid = false;
            if (IsEntityValidForSnow(entity) && groundToIndex.ContainsKey(hit.collider.gameObject))
            {
                int textureIndex = groundToIndex[hit.collider.gameObject];
                data!.w = hit.point;
                data.uv = new Vector2(hit.textureCoord2.x, hit.textureCoord2.y);
                data.textureIndex = textureIndex;
                isHitValid = true;
            }
            else
            {
                data.Reset();
            }

            entitySnowDataInArray[index] = data;

            return isHitValid;
        }

        internal void RemoveEntityData(MonoBehaviour entity)
        {
            if (entitySnowDataMap.TryGetValue(entity, out int index))
            {
                entitySnowDataMap.Remove(entity);
                entitySnowDataInArray?[index].Reset();
                entitySnowDataOutArray?[index].Reset();
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


