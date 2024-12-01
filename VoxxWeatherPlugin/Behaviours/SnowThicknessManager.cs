using UnityEngine;
using VoxxWeatherPlugin.Weathers;
using System.Linq;
using System.Collections.Generic;
using GameNetcodeStuff;
using VoxxWeatherPlugin.Utils;
using UnityEngine.UIElements.UIR;

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
        internal Vector3 feetPosition; // player's feet position

        //Compute buffers
        internal Dictionary<MonoBehaviour, int> entitySnowDataMap = new Dictionary<MonoBehaviour, int>();
        private Stack<int> freeIndices = new Stack<int>();
        internal EntitySnowData[]? entitySnowDataArray;
        private ComputeBuffer? entityDataComputeBuffer;

        // Mappings
        internal Dictionary<GameObject, int> groundToIndex = new Dictionary<GameObject, int>();
        internal HashSet<GameObject> iceObjects = new HashSet<GameObject>();

        [Header("Ground")]
        [SerializeField]
        internal string[] groundTags = {"Grass", "Gravel", "Snow", "Rock"};
        [SerializeField]
        internal bool isOnNaturalGround => isPlayerOnNaturalGround();
        [SerializeField]
        internal bool isOnIce = false;

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

            // Set static texture buffers
            snowThicknessComputeShader.SetTexture(kernelHandle, "_SnowTracksTex", snowfallData?.snowMasks);
            snowThicknessComputeShader.SetTexture(kernelHandle, "_FootprintsTex", snowfallData?.snowTracksMap);
        }

        internal void Reset()
        {
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
                snowThicknessComputeShader?.SetFloat("_MaximumSnowHeight", snowfallData.maxSnowHeight);
                snowThicknessComputeShader?.SetVector("_ShipLocation", snowfallData.shipPosition);
                inputNeedsUpdate = false;
            }

            // Update dynamic input parameters
            snowThicknessComputeShader?.SetFloat("_SnowNoisePower", snowfallData.snowIntensity);
            snowThicknessComputeShader?.SetMatrix("_FootprintsViewProjection", snowfallData.snowTracksCamera!.projectionMatrix * snowfallData.snowTracksCamera.worldToCameraMatrix);
                
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
            return GetEntityData(entity)?.textureIndex != -1;
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

            //Update fields for a local player
            if (entity == GameNetworkManager.Instance.localPlayerController) 
            {
                feetPosition = hit.point;
                isOnIce = iceObjects.Contains(hit.collider.gameObject);
            }
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

        internal void UpdatePositionData() // Maybe move this to player/enemy scripts and then pool here?
        {
            // isOnIce = false;
            // entitySnowDataList.Clear();
            // entitySnowDataList.Capacity = maxEntityCount;

            // RaycastHit[] hits = new RaycastHit[1];

            // foreach (PlayerControllerB playerScript in StartOfRound.Instance.allPlayerScripts) // move to GetCurrentMaterialStandingOn
            // {
            //     if (!IsEntityValidForSnow(playerScript))
            //     {
            //         continue;
            //     }

            //     entityToIndex[playerScript] = -1; // Reset entity index to -1, meaning it is not on the ground
            //     if (Physics.RaycastNonAlloc(playerScript.transform.position, -Vector3.up, hits, 3f,
            //                         LayerMask.GetMask("Room"), QueryTriggerInteraction.Ignore) > 0)
            //     {
            //         Collider collider = hits[0].collider;
            //         if (groundToIndex.ContainsKey(collider.gameObject)) // Check if the ground object is registered
            //         {
            //             int textureIndex = groundToIndex[collider.gameObject];
            //             entitySnowDataList.Add(new EntitySnowData
            //             {
            //                 uv = new Vector2(hits[0].textureCoord2.x, hits[0].textureCoord2.y),
            //                 textureIndex = textureIndex
            //             });
            //             entityToIndex[playerScript] = entitySnowDataList.Count - 1; // Update entity index
            //         }

            //         if (playerScript == GameNetworkManager.Instance.localPlayerController) //Update fields for local player
            //         {
            //             feetPosition = hits[0].point;

            //             if (iceObjects.Contains(collider.gameObject))
            //             {
            //                 isOnIce = true;
            //             }
            //         }
            //         Debug.LogDebug($"Feet position: {feetPosition}, isOnNaturalGround: {isOnNaturalGround}, isOnIce: {isOnIce}");
            //     }
            // }

            // foreach (EnemyAI enemy in RoundManager.Instance.SpawnedEnemies)
            // {
            //     if (!IsEntityValidForSnow(enemy))
            //     {
            //         continue;
            //     }

            //     if (entitySnowDataList.Count >= maxEntityCount)
            //     {
            //         break;
            //     }

            //     entityToIndex[enemy] = -1; // Reset entity index to -1, meaning it is not on the ground
            //     if (Physics.RaycastNonAlloc(enemy.transform.position, -Vector3.up, hits, 3f,
            //                         LayerMask.GetMask("Room"), QueryTriggerInteraction.Ignore) > 0)
            //     {
            //         Collider collider = hits[0].collider;
            //         if (groundToIndex.ContainsKey(collider.gameObject)) // Check if the ground object is registered
            //         {
            //             int textureIndex = groundToIndex[collider.gameObject];
            //             entitySnowDataList.Add(new EntitySnowData
            //             {
            //                 uv = new Vector2(hits[0].textureCoord2.x, hits[0].textureCoord2.y),
            //                 textureIndex = textureIndex
            //             });
            //             entityToIndex[enemy] = entitySnowDataList.Count - 1; // Update entity index
            //         }
            //     }
            // }
            // entityDataBuffer.SetData(entitySnowDataList.ToArray());
        }

        void OnDestroy()
        {
            // Clean up
            entityDataComputeBuffer?.Release();
        }
    }
}


