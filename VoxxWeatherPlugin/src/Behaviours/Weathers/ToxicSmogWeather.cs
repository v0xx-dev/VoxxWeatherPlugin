using UnityEngine;
using System;
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
        ToxicSmogVFXManager? VFXManager;
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

        
    }

    internal class ToxicSmogVFXManager : BaseVFXManager
    {
        [Header("Smog")]
        private float MinFreePath = 12f;
        private float MaxFreePath = 36f;
        [SerializeField]
        private float smogFreePath = 24f;
        [SerializeField]
        private LocalVolumetricFog? toxicVolumetricFog;

        [Header("Fumes")]
        private int MinFumesAmount = 12;
        private int MaxFumesAmount = 36;
        [SerializeField]
        private int fumesAmount = 24;
        private float factoryAmountMultiplier = 0.5f;
        [SerializeField]
        private int factoryFumesAmount = 12;
        [SerializeField]
        private GameObject? fumesContainerInside;
        [SerializeField]
        private GameObject? fumesContainerOutside;
        [SerializeField]
        private GameObject hazardPrefab; // Assign in the inspector
        private float spawnRadius = 20f;
        private float minDistanceBetweenHazards = 5f;
        private float minDistanceFromEntrances = 15f;
        private int numberOfHazards;
        private List<Vector3>? spawnedPositions;
        private int maxAttempts;

        internal override void PopulateLevelWithVFX(Bounds levelBounds = default, System.Random? seededRandom = null)
        {
            if (toxicVolumetricFog == null)
            {
                GameObject toxicFogContainer = new GameObject("ToxicFog");
                toxicVolumetricFog = toxicFogContainer.AddComponent<LocalVolumetricFog>();
                toxicFogContainer.transform.SetParent(ToxicSmogWeather.Instance!.transform);
                toxicVolumetricFog.parameters.albedo = Color.green;
                toxicVolumetricFog.parameters.blendingMode = LocalVolumetricFogBlendingMode.Additive;
                toxicVolumetricFog.parameters.falloffMode = LocalVolumetricFogFalloffMode.Linear;
            }

            // Randomly select density
            smogFreePath = seededRandom.NextDouble(MinFreePath, MaxFreePath);
            toxicVolumetricFog.parameters.meanFreePath = smogFreePath;
            // Position in the center of the level
            toxicVolumetricFog.parameters.size = levelBounds.size;
            toxicVolumetricFog.transform.position = levelBounds.center;
            toxicVolumetricFog.parameters.distanceFadeStart = levelBounds.size.x*0.9f;
            toxicVolumetricFog.parameters.distanceFadeEnd = levelBounds.size.x;

            fumesAmount = seededRandom.Next(MinFumesAmount, MaxFumesAmount);
            factoryFumesAmount = Mathf.CeilToInt(fumesAmount * factoryAmountMultiplier);
            if (fumesContainerInside = null)
            {
                fumesContainerInside = new GameObject("FumesContainerInside");
            }


            if (fumesContainerOutside == null)
            {
                fumesContainerOutside = new GameObject("fumesContainerOutside");
            }


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
        }
    }

    internal class InteriorHazardSpawner : MonoBehaviour
    {
        public GameObject hazardPrefab; // Assign in the inspector
        public int minNumberOfHazards = 10;
        public int maxNumberOfHazards = 20;
        public float spawnRadius = 20f;
        public float minDistanceBetweenHazards = 5f;
        public float minDistanceFromEntrances = 15f;

        private int numberOfHazards;
        private List<Vector3> spawnedPositions;
        private Vector3[] randomMapObjectsPositions;
        private Vector3[] entrancePositions;
        private System.Random random;
        private int maxAttempts;

        void Start()
        {
            if (hazardPrefab == null)
            {
                Debug.LogError("InteriorHazardSpawner: Hazard prefab is not assigned!");
                return;
            }

            StartOfRound.Instance.StartNewRoundEvent.AddListener(OnNewRound);
        }

        private void OnNewRound()
        {
            InitializeSpawner();
            SpawnHazards();
            StartOfRound.Instance.StartNewRoundEvent.RemoveListener(OnNewRound);
            this.enabled = false;
        }
        
        private void InitializeSpawner()
        {
            random = new System.Random(StartOfRound.Instance.randomMapSeed + 422);

            numberOfHazards = random.Next(minNumberOfHazards, maxNumberOfHazards + 1);
            spawnedPositions = new List<Vector3>(numberOfHazards);
            maxAttempts = numberOfHazards * 3;

            // Cache entrance positions and map objects
            EntranceTeleport[] interiorEntrances = FindObjectsOfType<EntranceTeleport>().Where(entrance => !entrance.isEntranceToBuilding).ToArray();
            SpawnSyncedObject[] randomMapObjects = FindObjectsOfType<SpawnSyncedObject>();
            entrancePositions = new Vector3[interiorEntrances.Length];
            for (int i = 0; i < interiorEntrances.Length; i++)
            {
                entrancePositions[i] = interiorEntrances[i].transform.position;
            }

            randomMapObjectsPositions = new Vector3[randomMapObjects.Length];
            for (int i = 0; i < randomMapObjects.Length; i++)
            {
                randomMapObjectsPositions[i] = randomMapObjects[i].transform.position;
            }
        }

        private void SpawnHazards()
        {
            NavMeshHit navHit = new NavMeshHit();

            for (int i = 0; i < maxAttempts && spawnedPositions.Count < numberOfHazards; i++)
            {
                // Randomly select an object to spawn around
                int randomObjectIndex = random.Next(randomMapObjectsPositions.Length);
                Vector3 objectPosition = randomMapObjectsPositions[randomObjectIndex];

                Vector3 potentialPosition = GetValidSpawnPosition(objectPosition, ref navHit);
                if (potentialPosition != Vector3.zero)
                {
                    GameObject spawnedHazard = Instantiate(hazardPrefab, potentialPosition, Quaternion.identity);
                    spawnedHazard.transform.SetParent(transform);
                    spawnedPositions.Add(potentialPosition);
                }
            }

            Debug.Log($"InteriorHazardSpawner: Spawned {spawnedPositions.Count} hazards out of {numberOfHazards}");
        }

        private Vector3 GetValidSpawnPosition(Vector3 objectPosition, ref NavMeshHit navHit)
        {
            Vector3 potentialPosition = RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(
                objectPosition, spawnRadius, navHit, random, NavMesh.AllAreas);

            if (IsPositionValid(potentialPosition))
            {
                return potentialPosition;
            }

            return Vector3.zero;
        }

        private bool IsPositionValid(Vector3 position)
        {
            float sqrMinDistanceBetweenHazards = minDistanceBetweenHazards * minDistanceBetweenHazards;
            float sqrMinDistanceFromEntrances = minDistanceFromEntrances * minDistanceFromEntrances;

            // Check distance from other hazards
            for (int i = 0; i < spawnedPositions.Count; i++)
            {
                if ((position - spawnedPositions[i]).sqrMagnitude < sqrMinDistanceBetweenHazards)
                {
                    return false;
                }
            }

            // Check distance from EntranceTeleport objects
            for (int i = 0; i < entrancePositions.Length; i++)
            {
                if ((position - entrancePositions[i]).sqrMagnitude < sqrMinDistanceFromEntrances)
                {
                    return false;
                }
            }

            return true;
        }
    }
}