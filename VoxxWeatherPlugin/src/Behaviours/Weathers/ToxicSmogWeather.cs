using UnityEngine;
using DunGen;
using UnityEngine.Rendering.HighDefinition;
using VoxxWeatherPlugin.Utils;
using System.Collections.Generic;
using UnityEngine.AI;
using System.Linq;


namespace VoxxWeatherPlugin.Weathers
{
    internal class ToxicSmogWeather: BaseWeather
    {
        public static ToxicSmogWeather? Instance { get; private set; }
        [SerializeField]
        internal ToxicSmogVFXManager? VFXManager;
        private Bounds levelBounds;
        private System.Random? seededRandom;
        void Awake()
        {
            Instance = this;
        }
        
        void OnEnable()
        {
            seededRandom = new System.Random(StartOfRound.Instance.randomMapSeed);
            levelBounds = PlayableAreaCalculator.CalculateZoneSize(1.5f);
            VFXManager?.PopulateLevelWithVFX(levelBounds, seededRandom);
        }

        void OnDisable()
        {
            VFXManager?.Reset();
        }

        
    }

    internal class ToxicSmogVFXManager : BaseVFXManager
    {
        [Header("Smog")]
        [SerializeField]
        private float smogFreePath = 24f;
        private float MinFreePath => Configuration.MinFreePath.Value;
        private float MaxFreePath => Configuration.MaxFreePath.Value;
        [SerializeField]
        private LocalVolumetricFog? toxicVolumetricFog;

        [Header("Fumes")]
        [SerializeField]
        private int fumesAmount = 24;
        private int MinFumesAmount => Configuration.MinFumesAmount.Value;
        private int MaxFumesAmount => Configuration.MaxFumesAmount.Value;
        private float factoryAmountMultiplier => Configuration.FactoryAmountMultiplier.Value;
        [SerializeField]
        private int factoryFumesAmount = 12;
        [SerializeField]
        private GameObject? fumesContainerInside;
        [SerializeField]
        private GameObject? fumesContainerOutside;
        [SerializeField]
        internal GameObject? hazardPrefab; // Assign in the inspector
        private float spawnRadius = 20f;
        private float minDistanceBetweenHazards = 5f;
        private float minDistanceFromBlockers = 15f;
        private List<Vector3>? spawnedPositions;
        private int maxAttempts;

        void Awake()
        {
            hazardPrefab?.SetActive(false);
        }

        void OnEnable()
        {
            toxicVolumetricFog?.gameObject.SetActive(true);
            fumesContainerOutside?.SetActive(true);
            fumesContainerInside?.SetActive(false);
        }

        void OnDisable()
        {
            toxicVolumetricFog?.gameObject.SetActive(false);
            fumesContainerOutside?.SetActive(false);
            fumesContainerInside?.SetActive(true);
        }

        internal override void PopulateLevelWithVFX(Bounds levelBounds = default, System.Random? seededRandom = null)
        {
            if (toxicVolumetricFog == null)
            {
                GameObject toxicFogContainer = new GameObject("ToxicFog");
                toxicVolumetricFog = toxicFogContainer.AddComponent<LocalVolumetricFog>();
                toxicFogContainer.transform.SetParent(ToxicSmogWeather.Instance!.transform);
                toxicVolumetricFog.parameters.albedo = new Color(0.413f, 0.589f, 0.210f); //dark lime green
                toxicVolumetricFog.parameters.blendingMode = LocalVolumetricFogBlendingMode.Additive;
                toxicVolumetricFog.parameters.falloffMode = LocalVolumetricFogFalloffMode.Linear;
            }
            else
            {
                toxicVolumetricFog.gameObject.SetActive(true);
            }

            if (seededRandom == null)
            {
                seededRandom = new System.Random(StartOfRound.Instance.randomMapSeed);
            }

            // Find the dungeon scale
            float dungeonSize = StartOfRound.Instance.currentLevel.factorySizeMultiplier;

            // Randomly select density
            smogFreePath = seededRandom.NextDouble(MinFreePath, MaxFreePath);
            toxicVolumetricFog.parameters.meanFreePath = smogFreePath;
            // Position in the center of the level
            toxicVolumetricFog.parameters.size = levelBounds.size;
            toxicVolumetricFog.transform.position = levelBounds.center;
            toxicVolumetricFog.parameters.distanceFadeStart = levelBounds.size.x*0.9f;
            toxicVolumetricFog.parameters.distanceFadeEnd = levelBounds.size.x;

            fumesAmount = seededRandom.Next(MinFumesAmount, MaxFumesAmount);
            factoryFumesAmount = Mathf.CeilToInt(fumesAmount * factoryAmountMultiplier * dungeonSize);

            // Cache entrance positions and map objects
            EntranceTeleport[] entrances = FindObjectsOfType<EntranceTeleport>();
            Transform mapPropsContainer = GameObject.FindGameObjectWithTag("MapPropsContainer").transform;
            
            if (fumesContainerOutside == null)
            {
                fumesContainerOutside = new GameObject("FumesContainerOutside");
                fumesContainerOutside.transform.SetParent(mapPropsContainer);
            }

            // Use outside AI nodes as anchors
            List<Vector3> anchorPositions = RoundManager.Instance.outsideAINodes.Select(node => node.transform.position).ToList();
            // Use outside entrances as blockers
            List<Vector3> blockersPositions = entrances.Where(entrance => !entrance.isEntranceToBuilding).Select(entrance => entrance.transform.position).ToList();
            ///Add ship bounds to the list of blockers
            blockersPositions.AddRange([StartOfRound.Instance.shipBounds.transform.position, Vector3.zero]);
            Debug.LogDebug($"Outdoor fumes: Anchor positions: {anchorPositions.Count}, Blockers positions: {blockersPositions.Count}");
            SpawnFumes(anchorPositions, blockersPositions, fumesAmount, fumesContainerOutside!, seededRandom);

            if (fumesContainerInside == null)
            {
                fumesContainerInside = new GameObject("FumesContainerInside");
                fumesContainerInside.transform.SetParent(mapPropsContainer);
            }

            // Use item spawners AND AI nodes as anchors
            anchorPositions = RoundManager.Instance.spawnedSyncedObjects.Select(obj => obj.transform.position).ToList();
            anchorPositions.AddRange(RoundManager.Instance.insideAINodes.Select(obj => obj.transform.position));
            // Use entrances as blockers
            blockersPositions = entrances.Where(entrance => entrance.isEntranceToBuilding).Select(entrance => entrance.transform.position).ToList();
            Debug.LogDebug($"Indoor fumes: Anchor positions: {anchorPositions.Count}, Blockers positions: {blockersPositions.Count}");
            SpawnFumes(anchorPositions, blockersPositions, factoryFumesAmount, fumesContainerInside!, seededRandom);
        }

        private void SpawnFumes(List<Vector3> anchors, List<Vector3> blockedPositions, int amount, GameObject container, System.Random random)
        {
            if (hazardPrefab == null)
            {
                Debug.LogError("Hazard Spawner: hazardPrefab is not set");
                return;
            }

            if (container == null)
            {
                Debug.LogError("Hazard Spawner: container is not set");
                return;
            }

            spawnedPositions = new List<Vector3>(amount);

            maxAttempts = amount * 3;

            NavMeshHit navHit = new NavMeshHit();

            for (int i = 0; i < maxAttempts && spawnedPositions.Count < amount; i++)
            {
                // Randomly select an object to spawn around
                int randomObjectIndex = random.Next(anchors.Count);
                Vector3 objectPosition = anchors[randomObjectIndex];

                Vector3 potentialPosition = GetValidSpawnPosition(objectPosition, blockedPositions, ref navHit, random);
                if (potentialPosition != Vector3.zero)
                {
                    GameObject spawnedHazard = Instantiate(hazardPrefab, potentialPosition, Quaternion.identity, container.transform);
                    spawnedHazard.SetActive(true);
                    spawnedPositions.Add(potentialPosition);
                }
            }

            Debug.LogDebug($"Spawned {spawnedPositions.Count} hazards out of {amount}");
        }

        private Vector3 GetValidSpawnPosition(Vector3 objectPosition, List<Vector3> blockedPositions, ref NavMeshHit navHit, System.Random random)
        {
            Vector3 potentialPosition = RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(
                                                objectPosition, spawnRadius, navHit, random, NavMesh.AllAreas);

            if (IsPositionValid(potentialPosition, blockedPositions))
            {
                return potentialPosition;
            }

            return Vector3.zero;
        }

        private bool IsPositionValid(Vector3 position, List<Vector3> blockedPositions)
        {
            float sqrMinDistanceBetweenHazards = minDistanceBetweenHazards * minDistanceBetweenHazards;
            float sqrMinDistanceFromBlockers = minDistanceFromBlockers * minDistanceFromBlockers;

            // Check distance from other hazards
            for (int i = 0; i < spawnedPositions!.Count; i++)
            {
                if ((position - spawnedPositions[i]).sqrMagnitude < sqrMinDistanceBetweenHazards)
                {
                    return false;
                }
            }

            // Check distance from EntranceTeleport objects
            for (int i = 0; i < blockedPositions.Count; i++)
            {
                if ((position - blockedPositions[i]).sqrMagnitude < sqrMinDistanceFromBlockers)
                {
                    return false;
                }
            }

            return true;
        }

        internal override void Reset()
        {
            if (fumesContainerInside != null)
            {
                Destroy(fumesContainerInside);
            }

            if (fumesContainerOutside != null)
            {
                Destroy(fumesContainerOutside);
            }

            fumesContainerInside = null;
            fumesContainerOutside = null;

            toxicVolumetricFog?.gameObject.SetActive(false);
        }
    }
}