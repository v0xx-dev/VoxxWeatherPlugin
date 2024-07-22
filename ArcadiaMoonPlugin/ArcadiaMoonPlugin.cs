using BepInEx;
using BepInEx.Configuration;
using GameNetcodeStuff;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace ArcadiaMoonPlugin
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class ArcadiaMoonPlugin : BaseUnityPlugin
    {
        private Harmony harmony;
        public static ArcadiaMoonPlugin instance;

        public static ConfigEntry<bool> ForceSpawnFlowerman { get; private set; }
        public static ConfigEntry<bool> ForceSpawnBaboon { get; private set; }
        public static ConfigEntry<bool> ForceSpawnRadMech { get; private set; }

        private void Awake()
        {
            instance = this;

            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            // Configuration entries
            ForceSpawnFlowerman = Config.Bind("Spawning", "ForceSpawnFlowerman", true, "Enable custom deterministic spawner for Bracken");
            ForceSpawnBaboon = Config.Bind("Spawning", "ForceSpawnBaboon", true, "Enable custom deterministic spawner for Baboon hawk");
            ForceSpawnRadMech = Config.Bind("Spawning", "ForceSpawnRadMech", true, "Enable custom deterministic spawner for Old Bird");

            //Apply Harmony patch
            this.harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            this.harmony.PatchAll();
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} patched PlayerControllerB!");
        }

    }

    public class TimeAnimSyncronizer : MonoBehaviour
    {
        private Animator timeSyncAnimator;

        private void Start()
        {
            timeSyncAnimator = GetComponent<Animator>();
            if (timeSyncAnimator == null)
            {
                Debug.LogError("There is no Animator component attached to this object!");
            }

        }

        private void Update()
        {
            if (timeSyncAnimator != null && TimeOfDay.Instance.timeHasStarted)
            {
                timeSyncAnimator.SetFloat("timeOfDay", Mathf.Clamp(TimeOfDay.Instance.normalizedTimeOfDay, 0f, 0.99f));
            }
        }
    }

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

    internal class PlayerHeatManager : MonoBehaviour
    {
        public static bool isInHeatZone = false;
        public static float heatSeverityMultiplier = 1f;
        public static float heatSeverity = 0f;

        private static Volume heatEffectVolume;

        public static void SetEffectsVolume(Volume volume)
        {
            if (volume != null)
            {
                heatEffectVolume = volume;
            }
        }

        internal static void SetHeatSeverity(float heatSeverityDelta)
        {
            heatSeverity = Mathf.Clamp01(heatSeverity + heatSeverityDelta * heatSeverityMultiplier);
            if (heatEffectVolume != null)
            {
                heatEffectVolume.weight = heatSeverity; // Adjust intensity of the visual effect
            }
        }

    }

    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private string enemyName = "RadMech";
        [SerializeField] private float timer = 0.5f; // Normalized time of day to start spawning enemies

        private EnemyType enemyType;
        private GameObject nestPrefab;

        private List<GameObject> spawnedNests = new List<GameObject>();
        private System.Random random = new System.Random(StartOfRound.Instance.randomMapSeed + 42);

        private void LoadResources(string enemyName)
        {
            // Find all EnemyType assets
            var allEnemyTypes = Resources.FindObjectsOfTypeAll<EnemyType>().Distinct();

            // Find the specific EnemyType by name
            enemyType = allEnemyTypes.FirstOrDefault(e => e.enemyName == enemyName);

            if (enemyType != null)
            {
                nestPrefab = enemyType.nestSpawnPrefab;
                Debug.Log($"{enemyType.enemyName} and its prefab loaded successfully!");
            }
            else
            {
                Debug.LogError("Failed to load EnemyType!");

            }
        }

        private void Start()
        {
            //Don't spawn if not a host
            if (!GameNetworkManager.Instance.isHostingGame)
            {
                return;
            }
            LoadResources(enemyName);

            // Check if forced spawning is enabled for the current enemy type
            if (!IsSpawningEnabled())
            {
                Debug.Log($"Forced spawning for {enemyName} is disabled in the config.");
                enabled = false;
                return;
            }
            // Spawn nests at the positions of child objects
            foreach (Transform child in transform)
            {
                if (nestPrefab != null)
                {
                    Debug.Log("Started nest prefab spawning routine!");
                    Vector3 position = RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(child.position, 10f, default(NavMeshHit), random,
                                                                                                      RoundManager.Instance.GetLayermaskForEnemySizeLimit(enemyType));
                    position = RoundManager.Instance.PositionEdgeCheck(position, enemyType.nestSpawnPrefabWidth);
                    GameObject nest = Instantiate(nestPrefab, position, Quaternion.identity);
                    nest.transform.Rotate(Vector3.up, random.Next(-180, 180), Space.World);
                    spawnedNests.Add(nest);
                    if (nest.GetComponentInChildren<NetworkObject>())
                    {
                        nest.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
                        Debug.Log($"Spawned an {enemyName} nest prefab!");
                    }
                    else
                    {
                        Debug.LogError($"Nest prefab of {enemyName} does not have a NetworkObject component. Desync possible!");
                    }
                }
            }
        }

        private void Update()
        {
            if (TimeOfDay.Instance.normalizedTimeOfDay > timer && TimeOfDay.Instance.timeHasStarted && GameNetworkManager.Instance.isHostingGame)
            {
                // Destroy previously spawned nests and spawn enemies in their place
                if (nestPrefab != null)
                {
                    foreach (GameObject nest in spawnedNests)
                    {
                        Vector3 nest_position = nest.transform.position;
                        float nest_angle = nest.transform.rotation.eulerAngles.y;
                        Destroy(nest);
                        SpawnEnemyAtPosition(nest_position, nest_angle);
                        Debug.Log($"Spawned enemy {enemyName} in place of a nest prefab!");
                    }
                    spawnedNests.Clear();
                    Debug.Log($"Destroyed all spawned enemy nest prefabs of {enemyType.enemyName}!");
                }
                else
                {
                    foreach (Transform child in transform)
                    {
                        SpawnEnemyAtPosition(child.position, 0f);
                        Debug.Log("Force spawned an enemy!");
                    }
                }
                enabled = false;
            }
        }

        private void SpawnEnemyAtPosition(Vector3 position, float yRot = 0f)
        {
            Debug.Log($"Current enemy type for force spawn is {enemyType.enemyName}");
            if (enemyType.enemyPrefab == null)
            {
                Debug.LogError($"{enemyType.enemyName} does not have a valid enemy prefab to spawn.");
                return;
            }
            RoundManager.Instance.SpawnEnemyGameObject(position, yRot, -1, enemyType);
        }

        private bool IsSpawningEnabled()
        {
            switch (enemyName.ToLower())
            {
                case "flowerman":
                    return ArcadiaMoonPlugin.ForceSpawnFlowerman.Value;
                case "baboon hawk":
                    return ArcadiaMoonPlugin.ForceSpawnBaboon.Value;
                case "radmech":
                    return ArcadiaMoonPlugin.ForceSpawnRadMech.Value;
                default:
                    return true; // Default to true if the enemy type is not explicitly handled
            }
        }
    }
}

