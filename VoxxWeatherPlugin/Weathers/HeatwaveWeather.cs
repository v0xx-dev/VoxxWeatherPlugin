using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using UnityEngine;
using VoxxWeatherPlugin.Utils;

namespace VoxxWeatherPlugin.Weathers
{
    internal class HeatwaveZoneInteract : MonoBehaviour
    {
        [SerializeField] private float timeInHeatZoneMax = 10f; // Time before maximum effects are applied
        [SerializeField] private Volume exhaustionFilter; // Filter for visual effects

        public bool CheckConditionsForHeatingPause(PlayerControllerB playerController)
        {
            return playerController.inSpecialInteractAnimation || (bool)(UnityEngine.Object)playerController.inAnimationWithEnemy || playerController.isClimbingLadder || ((UnityEngine.Object)playerController.physicsParent != (UnityEngine.Object)null);
        }

        public bool CheckConditionsForHeatingStop(PlayerControllerB playerController)
        {
            return playerController.beamUpParticle.isPlaying || playerController.isInElevator || playerController.isInHangarShipRoom;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                PlayerControllerB playerController = other.gameObject.GetComponent<PlayerControllerB>();

                if (playerController != GameNetworkManager.Instance.localPlayerController)
                    return;
                PlayerHeatManager.SetEffectsVolume(exhaustionFilter);
            }
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
                    PlayerHeatManager.heatSeverityMultiplier = 1f;
                    PlayerHeatManager.isInHeatZone = false;
                    PlayerHeatManager.SetHeatSeverity(-PlayerHeatManager.heatSeverity);
                    return;
                }

                if (CheckConditionsForHeatingStop(playerController))
                {
                    PlayerHeatManager.heatSeverityMultiplier = 1f;
                    PlayerHeatManager.isInHeatZone = false;
                    return;
                }

                if (CheckConditionsForHeatingPause(playerController))
                    PlayerHeatManager.heatSeverityMultiplier = .33f;
                else
                    PlayerHeatManager.heatSeverityMultiplier = 1f;

                if (PlayerHeatManager.isInHeatZone)
                {
                    PlayerHeatManager.SetHeatSeverity(Time.deltaTime / timeInHeatZoneMax);
                }
                else
                {
                    PlayerHeatManager.isInHeatZone = true;
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

                PlayerHeatManager.heatSeverityMultiplier = 1f;
                PlayerHeatManager.isInHeatZone = false;
            }
        }

        private void OnDestroy()
        {
            PlayerHeatManager.heatSeverityMultiplier = 1f;
            PlayerHeatManager.isInHeatZone = false;
            PlayerHeatManager.SetEffectsVolume(null);
        }
    }
    internal class HeatwaveWeather : MonoBehaviour
    {
        [SerializeField] private GameObject heatwaveParticlePrefab; // Prefab for the heatwave particle effect
        private Vector3 heatwaveZoneSize; // Size of the heatwave zone
        private Vector3 heatwaveZoneLocation; //Center of the heatwave zone

        private void Start()
        {
            CalculateZoneSize();
            SpawnHeatwaveTrigger();
            PopulateLevelWithParticleEmitters();
        }

        private void SpawnHeatwaveTrigger()
        {
            // Create a new empty GameObject
            GameObject heatwaveTrigger = new GameObject("HeatwaveTrigger");

            // Attach a BoxCollider component to the GameObject
            BoxCollider collider = heatwaveTrigger.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            // Set the size of the BoxCollider
            collider.size = heatwaveZoneSize;

            // Set the position and rotation of the GameObject
            heatwaveTrigger.transform.position = heatwaveZoneLocation;
            heatwaveTrigger.transform.rotation = Quaternion.identity;

            // Attach the HeatwaveZoneInteract script to the GameObject
            HeatwaveZoneInteract interactScript = heatwaveTrigger.AddComponent<HeatwaveZoneInteract>();
        }

        private void CalculateZoneSize()
        {
            List<Vector3> keyLocationCoords = new List<Vector3>();

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

            GameObject navMeshContainer = GameObject.FindGameObjectWithTag("OutsideLevelNavMesh");
            if (navMeshContainer != null)
            {
                Bounds navMeshBounds = navMeshContainer.GetComponent<NavMeshSurface>().navMeshData.sourceBounds;
                keyLocationCoords.Add(navMeshBounds.min);
                keyLocationCoords.Add(navMeshBounds.max);
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

            heatwaveZoneSize = zoneSize;
            heatwaveZoneLocation = zoneCenter;
        }

        private void PopulateLevelWithParticleEmitters()
        {
            // Instantiate the heatwave particle prefab at various positions in the level
            // Replace this with your own logic to populate the level with particle emitters
            // Example code to instantiate particle emitters:
            Instantiate(heatwaveParticlePrefab, new Vector3(0f, 0f, 0f), Quaternion.identity);
            Instantiate(heatwaveParticlePrefab, new Vector3(1f, 0f, 0f), Quaternion.identity);
            Instantiate(heatwaveParticlePrefab, new Vector3(0f, 1f, 0f), Quaternion.identity);
            Instantiate(heatwaveParticlePrefab, new Vector3(0f, 0f, 1f), Quaternion.identity);
        }
    }




}
