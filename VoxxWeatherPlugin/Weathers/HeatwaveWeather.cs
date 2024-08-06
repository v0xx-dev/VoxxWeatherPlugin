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
        public VolumeProfile heatwaveFilter; // Filter for visual effects

        internal HeatwaveVFXManager heatwaveVFXManager; // Manager for heatwave visual effects
        private Volume exhaustionFilter; // Filter for visual effects
        private BoxCollider heatwaveTrigger; // Trigger collider for the heatwave zone
        private Vector3 heatwaveZoneSize; // Size of the heatwave zone
        private Vector3 heatwaveZoneLocation; //Center of the heatwave zone

        private System.Random seededRandom;

        private float timeUntilStrokeMin = 40f; // Minimum time until a heatstroke occurs
        private float timeUntilStrokeMax = 80f; // Maximum time until a heatstroke occurs
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
            CalculateZoneSize();
            heatwaveVFXManager.CalculateEmitterRadius();
            heatwaveVFXManager.PopulateLevelWithVFX(ref heatwaveZoneSize, ref heatwaveZoneLocation, seededRandom);
            SetupHeatwaveWeather();
        }

        private void OnDisable()
        {
            Destroy(heatwaveVFXManager.heatwaveVFXContainer);
            heatwaveVFXManager.heatwaveVFXContainer = null;
            Debug.Log("Heatwave VFX container destroyed.");
            PlayerTemperatureManager.isInHeatZone = false;
            PlayerTemperatureManager.heatSeverityMultiplier = 1f;
        }

        private void SetupHeatwaveWeather()
        {
            // Set the size, position and rotation of the trigger zone
            heatwaveTrigger.size = heatwaveZoneSize;
            heatwaveTrigger.transform.position = heatwaveZoneLocation;
            heatwaveTrigger.transform.rotation = Quaternion.identity;
            Debug.Log($"Heatwave zone placed!");

            // Set exhaustion time for the player
            timeInHeatZoneMax = (float)seededRandom.Next((int)timeUntilStrokeMin, (int)timeUntilStrokeMax);
            Debug.Log($"Set time until heatstroke: {timeInHeatZoneMax} seconds");
        }

        private void CalculateZoneSize()
        {
            List<Vector3> keyLocationCoords = new List<Vector3>();
            keyLocationCoords.Add(Vector3.zero);

            // Store positions of all the outside AI nodes in the scene
            foreach (GameObject node in RoundManager.Instance.outsideAINodes)
            {
                if (node == null)
                    continue;
                keyLocationCoords.Add(node.transform.position);
            }

            // Find all Entrances in the scene
            EntranceTeleport[] entranceTeleports = FindObjectsOfType<EntranceTeleport>();

            foreach (EntranceTeleport entranceTeleport in entranceTeleports)
            {
                if (entranceTeleport == null)
                    continue;
                // Check if the entrance is on the outside
                if (entranceTeleport.isEntranceToBuilding)
                {
                    Vector3 entrancePointCoords = entranceTeleport.entrancePoint.position;
                    keyLocationCoords.Add(entrancePointCoords);
                }
            }

            // Calculate the size of the heatwave zone based on the key locations
            Vector3 minCoords = keyLocationCoords[0];
            Vector3 maxCoords = keyLocationCoords[0];

            foreach (Vector3 coords in keyLocationCoords)
            {
                minCoords = Vector3.Min(minCoords, coords);
                maxCoords = Vector3.Max(maxCoords, coords);
            }

            Vector3 zoneSize = maxCoords - minCoords;
            Vector3 zoneCenter = (minCoords + maxCoords) / 2f;

            Debug.Log($"Heatwave zone size: {zoneSize}");

            heatwaveZoneSize = zoneSize*1.25f;
            heatwaveZoneLocation = zoneCenter;

        }

        public bool CheckConditionsForHeatingPause(PlayerControllerB playerController)
        {
            return playerController.inSpecialInteractAnimation || (bool)(UnityEngine.Object)playerController.inAnimationWithEnemy || playerController.isClimbingLadder || ((UnityEngine.Object)playerController.physicsParent != (UnityEngine.Object)null);
        }

        public bool CheckConditionsForHeatingStop(PlayerControllerB playerController)
        {
            return playerController.beamUpParticle.isPlaying || playerController.isInElevator || playerController.isInHangarShipRoom;
        }

        private void OnTriggerStay(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                PlayerControllerB playerController = other.gameObject.GetComponent<PlayerControllerB>();

                if (playerController != GameNetworkManager.Instance.localPlayerController)
                    return;

                if (playerController.isPlayerDead)
                {
                    PlayerTemperatureManager.heatSeverityMultiplier = 1f;
                    PlayerTemperatureManager.isInHeatZone = false;
                    PlayerTemperatureManager.SetHeatSeverity(-PlayerTemperatureManager.heatSeverity);
                    return;
                }

                if (CheckConditionsForHeatingStop(playerController))
                {
                    PlayerTemperatureManager.heatSeverityMultiplier = 1f;
                    PlayerTemperatureManager.isInHeatZone = false;
                    return;
                }

                if (CheckConditionsForHeatingPause(playerController))
                    PlayerTemperatureManager.heatSeverityMultiplier = .33f; //heat slower when in special interact animation and in a car
                else
                    PlayerTemperatureManager.heatSeverityMultiplier = 1f;

                if (PlayerTemperatureManager.isInHeatZone)
                {
                    PlayerTemperatureManager.SetHeatSeverity(Time.deltaTime / timeInHeatZoneMax);
                }
                else
                {
                    PlayerTemperatureManager.isInHeatZone = true;
                }
            }
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
        }
    }

    public class HeatwaveVFXManager: MonoBehaviour
    {
        public GameObject heatwaveParticlePrefab; // Prefab for the heatwave particle effect
        public GameObject heatwaveVFXContainer; // GameObject for the particles

        // Variables for emitter placement
        private float emitterSize;
        private float raycastHeight = 500f; // Height from which to cast rays
        private Vector4 shipXZBounds = new Vector4(-13f, 23f, -24f, -4f); // MinX, MaxX, MinZ, MaxZ

        internal void CalculateEmitterRadius()
        {
            Transform transform = heatwaveParticlePrefab.transform;
            emitterSize = Mathf.Max(transform.localScale.x, transform.localScale.z) * 5f;
            Debug.Log($"Emitter size: {emitterSize}");
        }

        internal void PopulateLevelWithVFX(ref Vector3 heatwaveZoneSize, ref Vector3 heatwaveZoneLocation, System.Random seededRandom)
        {
            
            if (heatwaveVFXContainer == null)
                heatwaveVFXContainer = new GameObject("HeatwaveVFXContainer");

            int placedEmittersNum = 0;

            int xCount = Mathf.CeilToInt(heatwaveZoneSize.x / emitterSize);
            int zCount = Mathf.CeilToInt(heatwaveZoneSize.z / emitterSize);
            //Debug.Log($"Placing {xCount * zCount} emitters...");

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
                    //Debug.Log($"Raycast origin: {rayOrigin}");
                    (Vector3 position, Vector3 normal) = CastRayAndSampleNavMesh(rayOrigin);
                    //Debug.Log($"NavMesh hit position and normal: {position}, {normal}");

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
                    if (!IsPointWithinShipBounds(navHit.position))
                        return (navHit.position, hit.normal);
                }
            }
            return (Vector3.zero, Vector3.up);
        }

        bool IsPointWithinShipBounds(Vector3 point)
        {
            return (point.x >= shipXZBounds.x && point.x <= shipXZBounds.y &&
                    point.z >= shipXZBounds.z && point.z <= shipXZBounds.w);
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
