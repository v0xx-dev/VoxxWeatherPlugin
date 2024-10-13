using GameNetcodeStuff;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;
using VoxxWeatherPlugin.Utils;
using UnityEngine.AI;

namespace VoxxWeatherPlugin.Weathers
{
    internal class HeatwaveWeather: MonoBehaviour
    {
        internal VolumeProfile heatwaveFilter; // Filter for visual effects
        internal HeatwaveVFXManager heatwaveVFXManager; // Manager for heatwave visual effects
        private Volume exhaustionFilter; // Filter for visual effects
        private BoxCollider heatwaveTrigger; // Trigger collider for the heatwave zone
        private Vector3 heatwaveZoneSize; // Size of the heatwave zone
        private Vector3 heatwaveZoneLocation; //Center of the heatwave zone

        private System.Random seededRandom;

        private float timeUntilStrokeMin => VoxxWeatherPlugin.TimeUntilStrokeMin.Value; // Minimum time until a heatstroke occurs
        private float timeUntilStrokeMax => VoxxWeatherPlugin.TimeUntilStrokeMax.Value; // Maximum time until a heatstroke occurs
        [SerializeField] private float timeInHeatZoneMax = 50f; // Time before maximum effects are applied

        private void Awake()
        {
            // Add a BoxCollider component as a trigger to the GameObject
            if (heatwaveTrigger == null)
                heatwaveTrigger = gameObject.AddComponent<BoxCollider>();
            heatwaveTrigger.isTrigger = true;
            // Attach a Volume component to the GameObject
            Volume volumeComponent = gameObject.AddComponent<Volume>();
            // Set the volume profile from heatwaveFilter
            volumeComponent.profile = heatwaveFilter;
            volumeComponent.weight = 0f;
            exhaustionFilter = volumeComponent;
            PlayerTemperatureManager.heatEffectVolume = exhaustionFilter;
        }

        private void OnEnable()
        {
            seededRandom = new System.Random(StartOfRound.Instance.randomMapSeed);
            (heatwaveZoneSize, heatwaveZoneLocation) = PlayableAreaCalculator.CalculateZoneSize();
            Debug.LogDebug($"Heatwave zone size: {heatwaveZoneSize}. Placed at {heatwaveZoneLocation}");
            heatwaveVFXManager.CalculateEmitterRadius();
            heatwaveVFXManager.PopulateLevelWithVFX(ref heatwaveZoneSize, ref heatwaveZoneLocation, seededRandom);
            SetupHeatwaveWeather();
        }

        private void OnDisable()
        {
            Destroy(heatwaveVFXManager.heatwaveVFXContainer);
            heatwaveVFXManager.heatwaveVFXContainer = null;
            Debug.LogDebug("Heatwave VFX container destroyed.");
            PlayerTemperatureManager.isInHeatZone = false;
            PlayerTemperatureManager.heatSeverityMultiplier = 1f;
        }

        private void SetupHeatwaveWeather()
        {
            // Set the size, position and rotation of the trigger zone
            heatwaveTrigger.size = heatwaveZoneSize;
            heatwaveTrigger.transform.position = heatwaveZoneLocation;
            heatwaveTrigger.transform.rotation = Quaternion.identity;
            Debug.LogDebug($"Heatwave zone placed!");

            // Set exhaustion time for the player
            timeInHeatZoneMax = seededRandom.NextDouble(timeUntilStrokeMin, timeUntilStrokeMax);
            Debug.LogDebug($"Set time until heatstroke: {timeInHeatZoneMax} seconds");
        }

        private void OnTriggerStay(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                PlayerControllerB playerController = other.gameObject.GetComponent<PlayerControllerB>();

                if (playerController != GameNetworkManager.Instance.localPlayerController)
                    return;

                if (PlayerTemperatureManager.isInHeatZone)
                {
                    PlayerTemperatureManager.SetHeatSeverity(Time.deltaTime / timeInHeatZoneMax);
                }
                else
                {
                    PlayerTemperatureManager.isInHeatZone = true;
                }
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

                PlayerTemperatureManager.heatSeverityMultiplier = 1f;
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
    }

    public class HeatwaveVFXManager: MonoBehaviour
    {
        public GameObject heatwaveParticlePrefab; // Prefab for the heatwave particle effect
        public GameObject heatwaveVFXContainer; // GameObject for the particles

        // Variables for emitter placement
        private float emitterSize;
        private float raycastHeight = 500f; // Height from which to cast rays

        internal void CalculateEmitterRadius()
        {
            Transform transform = heatwaveParticlePrefab.transform;
            emitterSize = Mathf.Max(transform.localScale.x, transform.localScale.z) * 5f;
        }

        internal void PopulateLevelWithVFX(ref Vector3 heatwaveZoneSize, ref Vector3 heatwaveZoneLocation, System.Random seededRandom)
        {
            
            if (heatwaveVFXContainer == null)
                heatwaveVFXContainer = new GameObject("HeatwaveVFXContainer");

            int placedEmittersNum = 0;

            int xCount = Mathf.CeilToInt(heatwaveZoneSize.x / emitterSize);
            int zCount = Mathf.CeilToInt(heatwaveZoneSize.z / emitterSize);
            Debug.LogDebug($"Placing {xCount * zCount} emitters...");

            Vector3 startPoint = heatwaveZoneLocation - heatwaveZoneSize * 0.5f;

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
                        emitter.transform.parent = heatwaveVFXContainer.transform; // Parent the emitter to the VFX container
                        placedEmittersNum++;

                        minY = Mathf.Min(minY, position.y);
                        maxY = Mathf.Max(maxY, position.y);
                    }
                }
            }
            //Adjust the height of the heatwave zone based on the placed emitters
            heatwaveZoneSize.y = (maxY - minY) * 1.1f;
            heatwaveZoneLocation.y = (minY + maxY) / 2;

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
    }
}
