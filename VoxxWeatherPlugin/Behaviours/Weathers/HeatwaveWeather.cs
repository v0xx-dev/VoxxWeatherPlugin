using GameNetcodeStuff;
using UnityEngine.Rendering;
using UnityEngine;
using VoxxWeatherPlugin.Utils;
using UnityEngine.AI;
using UnityEngine.VFX;
using UnityEngine.Rendering.HighDefinition;

namespace VoxxWeatherPlugin.Weathers
{
    internal class HeatwaveWeather: BaseWeather
    {
        internal static HeatwaveWeather? Instance { get; private set; }
        [SerializeField]
        internal HeatwaveVFXManager? VFXManager; // Manager for heatwave visual effects
        [SerializeField]
        internal Volume? exhaustionFilter; // Filter for visual effects
        private BoxCollider? heatwaveTrigger; // Trigger collider for the heatwave zone
        private Bounds levelBounds; // Size of the playable area

        private System.Random? seededRandom;

        private float timeUntilStrokeMin => VoxxWeatherPlugin.TimeUntilStrokeMin.Value; // Minimum time until a heatstroke occurs
        private float timeUntilStrokeMax => VoxxWeatherPlugin.TimeUntilStrokeMax.Value; // Maximum time until a heatstroke occurs
        [SerializeField]
        internal float timeInHeatZoneMax = 50f; // Time before maximum effects are applied
        [SerializeField]
        internal float timeOfDayFactor = 1f; // Factor for the time of day

        private void Awake()
        {
            Instance = this;
            // Add a BoxCollider component as a trigger to the GameObject
            if (heatwaveTrigger == null)
                heatwaveTrigger = gameObject.AddComponent<BoxCollider>();
            heatwaveTrigger.isTrigger = true;
            PlayerTemperatureManager.heatEffectVolume = exhaustionFilter;
        }

        private void OnEnable()
        {
            seededRandom = new System.Random(StartOfRound.Instance.randomMapSeed);
            levelBounds = PlayableAreaCalculator.CalculateZoneSize(1.75f);
            Debug.LogDebug($"Heatwave zone size: {levelBounds.size}. Placed at {levelBounds.center}");
            VFXManager?.PopulateLevelWithVFX(levelBounds, seededRandom);
            SetupHeatwaveWeather();
            timeOfDayFactor = VFXManager?.CooldownHeatwaveVFX() ?? 1f;
        }

        private void OnDisable()
        {
            VFXManager?.Reset();
        }

        private void SetupHeatwaveWeather()
        {
            // Set the size, position and rotation of the trigger zone
            heatwaveTrigger!.size = levelBounds.size;
            heatwaveTrigger.transform.position = levelBounds.center;
            heatwaveTrigger.transform.rotation = Quaternion.identity;
            Debug.LogDebug($"Heatwave zone placed!");

            // Set exhaustion time for the player
            timeInHeatZoneMax = seededRandom!.NextDouble(timeUntilStrokeMin, timeUntilStrokeMax);
            Debug.LogDebug($"Set time until heatstroke: {timeInHeatZoneMax} seconds");
        }

        private void OnTriggerStay(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                PlayerControllerB playerController = other.gameObject.GetComponent<PlayerControllerB>();

                if (playerController != GameNetworkManager.Instance.localPlayerController)
                    return;

                PlayerTemperatureManager.isInHeatZone = true;
            }
            // else if (other.CompareTag("Aluminum") && LayerMask.LayerToName(other.gameObject.layer) == "Vehicle")
            // {
            //     if (other.TryGetComponent(out VehicleHeatwaveHandler cruiserHandler))
            //     {
            //         cruiserHandler.isInHeatwave = true;
            //     }
            // }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                PlayerControllerB playerController = other.gameObject.GetComponent<PlayerControllerB>();

                if (playerController != GameNetworkManager.Instance.localPlayerController)
                    return;

                PlayerTemperatureManager.heatTransferRate = 1f;
                PlayerTemperatureManager.isInHeatZone = false;
            }
            // else if (other.CompareTag("Aluminum") && LayerMask.LayerToName(other.gameObject.layer) == "Vehicle")
            // {
            //     if (other.TryGetComponent(out VehicleHeatwaveHandler cruiserHandler))
            //     {
            //         cruiserHandler.isInHeatwave = false;
            //     }
            // }
        }

        private void Update()
        {
            // Cooldown the heatwave VFX based on the sun intensity
            if (TimeOfDay.Instance.normalizedTimeOfDay % 0.1f < 0.01f)
            {
                timeOfDayFactor = VFXManager?.CooldownHeatwaveVFX() ?? 1f;
            }
        }
    }


    public class HeatwaveVFXManager: BaseVFXManager
    {
        public GameObject? heatwaveParticlePrefab; // Prefab for the heatwave particle effect
        public GameObject? heatwaveVFXContainer; // GameObject for the particles
        [SerializeField]
        internal AnimationCurve? heatwaveIntensityCurve; // Curve for the intensity of the heatwave

        // Variables for emitter placement
        private float emitterSize;
        private float raycastHeight = 500f; // Height from which to cast rays
        private float maxSunLuminosity = 20f; // Sun luminosity in lux when the heatwave is at its peak
        private HDAdditionalLightData? sunLightData; // Light data for the sun
        private int spawnRatePropertyID; // Property ID for the spawn rate of the particles

        internal void Awake()
        {
            spawnRatePropertyID = Shader.PropertyToID("particleSpawnRate");
        }

        internal void CalculateEmitterRadius()
        {
            Transform transform = heatwaveParticlePrefab!.transform;
            emitterSize = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) * 5f;
        }

        internal override void PopulateLevelWithVFX(Bounds levelBounds, System.Random? seededRandom)
        {
            sunLightData = TimeOfDay.Instance.sunDirect.GetComponent<HDAdditionalLightData>();

            if (levelBounds == null || seededRandom == null || heatwaveParticlePrefab == null)
            {
                Debug.LogError("Level bounds, random seed or heatwave particle prefab not set!");
                return;
            }
            
            CalculateEmitterRadius();

            if (heatwaveVFXContainer == null)
                heatwaveVFXContainer = new GameObject("HeatwaveVFXContainer");
                heatwaveVFXContainer.transform.parent = HeatwaveWeather.Instance!.transform; // Parent the container to the weather instance

            int placedEmittersNum = 0;

            int xCount = Mathf.CeilToInt(levelBounds.size.x / emitterSize);
            int zCount = Mathf.CeilToInt(levelBounds.size.z / emitterSize);
            Debug.LogDebug($"Placing {xCount * zCount} emitters...");

            Vector3 startPoint = levelBounds.center - levelBounds.size * 0.5f;

            float minY = -1f;
            float maxY = 1f;

            for (int x = 0; x < xCount; x++)
            {
                for (int z = 0; z < zCount; z++)
                {
                    // Randomize the position of the emitter within the grid cell
                    float dx = (float)seededRandom.NextDouble() - 0.5f;
                    float dz = (float)seededRandom.NextDouble() - 0.5f;
                    Vector3 rayOrigin = startPoint + new Vector3((x + dx) * emitterSize, raycastHeight, (z + dz) * emitterSize);
                    //Debug.LogDebug($"Raycast origin: {rayOrigin}");
                    (Vector3 position, Vector3 normal) = CastRayAndSampleNavMesh(rayOrigin);
                    //Debug.LogDebug($"NavMesh hit position and normal: {position}, {normal}");

                    if (position != Vector3.zero)
                    {
                        float randomRotation = (float)seededRandom.NextDouble() * 360f;
                        Quaternion rotation = Quaternion.AngleAxis(randomRotation, normal) * Quaternion.LookRotation(normal);
                        //position.y -= 0.5f; // Offset the emitter slightly below the ground
                        GameObject emitter = Instantiate(heatwaveParticlePrefab, position, rotation);
                        emitter.SetActive(true);
                        emitter.transform.parent = heatwaveVFXContainer.transform; // Parent the emitter to the VFX container
                        placedEmittersNum++;

                        minY = Mathf.Min(minY, position.y);
                        maxY = Mathf.Max(maxY, position.y);
                    }
                }
            }
            
            //Adjust the height of the heatwave zone based on the placed emitters
            float newHeight = (maxY - minY) * 1.1f;
            float newYPos = (minY + maxY) / 2;

            levelBounds.size = new Vector3(levelBounds.size.x, newHeight, levelBounds.size.z);
            levelBounds.center = new Vector3(levelBounds.center.x, newYPos, levelBounds.center.z);

            Debug.Log($"Placed {placedEmittersNum} emitters.");
        }

        private (Vector3, Vector3) CastRayAndSampleNavMesh(Vector3 rayOrigin)
        {
            int layerMask = (1 << LayerMask.NameToLayer("Room")) | (1 << LayerMask.NameToLayer("Default"));

            RaycastHit hit;
            if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 1000, layerMask, QueryTriggerInteraction.Ignore))
            {
                NavMeshHit navHit;
                if (NavMesh.SamplePosition(hit.point, out navHit, 3f, -1)) //places only where player can walk
                {
                    Bounds doubledBounds = new Bounds(StartOfRound.Instance.shipBounds.bounds.center, 
                                  StartOfRound.Instance.shipBounds.bounds.size * 2f);
                    if (!doubledBounds.Contains(navHit.position))
                        return (navHit.position, hit.normal);
                }
            }
            return (Vector3.zero, Vector3.up);
        }

        internal override void Reset()
        {
            if (heatwaveVFXContainer != null)
            {
                Destroy(heatwaveVFXContainer);
            }
            heatwaveVFXContainer = null;
            Debug.LogDebug("Heatwave VFX container destroyed.");
            
            PlayerTemperatureManager.isInHeatZone = false;
            PlayerTemperatureManager.heatTransferRate = 1f;
        }

        private void OnEnable()
        {
            if (heatwaveVFXContainer != null)
            {
                heatwaveVFXContainer.SetActive(true);
            }
        }

        private void OnDisable()
        {
            if (heatwaveVFXContainer != null)
                heatwaveVFXContainer.SetActive(false);
        }

        
        internal float CooldownHeatwaveVFX()
        {
            float reductionFactor = 1f;
            if (heatwaveVFXContainer != null && sunLightData != null)
            {
                reductionFactor = Mathf.Clamp01(sunLightData.intensity / maxSunLuminosity);
                reductionFactor = heatwaveIntensityCurve!.Evaluate(reductionFactor); // Min value in curve is 0.001 to avoid division by zero
                foreach (Transform child in heatwaveVFXContainer.transform)
                {
                    if (child.TryGetComponent(out VisualEffect vfx))
                    {
                        vfx.SetFloat(spawnRatePropertyID, VoxxWeatherPlugin.HeatwaveParticlesSpawnRate.Value * reductionFactor); 
                    }
                }
            }
            // Clamp the reduction factor to a non-zero value to avoid division errors
            return reductionFactor;
        }
    }
}
