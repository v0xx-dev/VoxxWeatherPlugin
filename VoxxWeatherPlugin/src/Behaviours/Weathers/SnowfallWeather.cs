using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.Rendering;
using UnityEngine.AI;
using VoxxWeatherPlugin.Behaviours;
using VoxxWeatherPlugin.Patches;
using VoxxWeatherPlugin.Utils;
using GameNetcodeStuff;
using UnityEngine.Rendering.HighDefinition;

namespace VoxxWeatherPlugin.Weathers
{
    internal class SnowfallWeather: BaseWeather
    {
        public static SnowfallWeather? Instance { get; internal set; }
        internal virtual float MinSnowHeight => Configuration.minSnowHeight.Value;
        internal virtual float MaxSnowHeight => Configuration.maxSnowHeight.Value;
        internal virtual float MinSnowNormalizedTime => Configuration.minTimeToFullSnow.Value;
        internal virtual float MaxSnowNormalizedTime => Configuration.maxTimeToFullSnow.Value;
        
        [Header("General")]
        [SerializeField]
        internal float timeUntilFrostbite = 0.6f * Configuration.minTimeUntilFrostbite.Value;
        [SerializeField]
        internal SnowfallVFXManager? VFXManager;

        internal virtual void Awake()
        {   
            Instance = this;
        }

        internal virtual void OnEnable()
        {
            LevelManipulator.Instance?.InitializeLevelProperties(1.5f);
            LevelManipulator.Instance?.SetupLevelForSnow(snowHeightRange: (MinSnowHeight, MaxSnowHeight),
                                                        snowNormalizedTimeRange: (MinSnowNormalizedTime, MaxSnowNormalizedTime),
                                                        snowScaleRange: (0.7f, 1.3f),
                                                        fogStrengthRange: (0f, 15f));
            VFXManager?.PopulateLevelWithVFX();
        }

        internal void OnFinish()
        {
            LevelManipulator.Instance?.ResetLevelProperties();
            LevelManipulator.Instance?.ResetSnowVariables();
            PlayerEffectsManager.normalizedTemperature = 0f;
        }

        internal virtual void OnDisable()
        {
            OnFinish();
            VFXManager?.Reset();
        }

        internal virtual void Update()
        {
            if (!LevelManipulator.Instance?.IsSnowReady ?? false)
            {
                return;
            }
            LevelManipulator.Instance?.UpdateLevelProperties();
            LevelManipulator.Instance?.UpdateSnowVariables();
            // Update the snow thickness (host must constantly update for enemies, clients only when not in factory)
            if (GameNetworkManager.Instance.isHostingGame || !GameNetworkManager.Instance.localPlayerController.isInsideFactory)
            {
                SnowThicknessManager.Instance!.CalculateThickness(); // TODO Could be moved to FixedUpdate to save performance?
            }
            SetColdZoneState();
        }

        internal virtual void SetColdZoneState()
        {
            PlayerEffectsManager.isInColdZone = PlayerEffectsManager.isUnderSnow;
        }

    }

    public class SnowfallVFXManager: BaseVFXManager
    {
        [SerializeField]
        internal bool addedVanillaFootprints = false;

        [Header("Snow VFX")]
        [SerializeField]
        internal GameObject? snowVFXContainer;

        internal static float snowMovementHindranceMultiplier = 1f;
        internal static int snowFootstepIndex = -1;
        internal static float eyeBias = 0.2f;

        [Header("Christmas Event")]
        [SerializeField]
        private GameObject? christmasTreePrefab;
        private Item? giftBoxItem;

        internal void Start()
        {
            // Find the snow footstep index
            snowFootstepIndex = Array.FindIndex(StartOfRound.Instance.footstepSurfaces, surface => surface.surfaceTag == "Snow");
            // Find the gift box item in the item database
            giftBoxItem = StartOfRound.Instance.allItemsList.itemsList.FirstOrDefault(item => item.name == "GiftBox");
        }

        internal virtual void OnEnable()
        {
            snowVFXContainer?.SetActive(true);

            PlayerEffectsManager.freezeEffectVolume.enabled = true;
            PlayerEffectsManager.underSnowVolume.enabled = true;
            
            LevelManipulator.Instance!.snowVolume!.enabled = true;
            LevelManipulator.Instance.snowTrackerCameraContainer?.SetActive(true);
        }

        internal virtual void OnDisable()
        {
            snowVFXContainer?.SetActive(false);
            LevelManipulator.Instance!.snowVolume!.enabled = false;
            LevelManipulator.Instance.snowTrackerCameraContainer?.SetActive(false);
            snowMovementHindranceMultiplier = 1f;
            PlayerEffectsManager.isInColdZone = false;
            PlayerEffectsManager.underSnowVolume.enabled = false;
            PlayerEffectsManager.isUnderSnow = false;
        }

        internal override void Reset()
        {
            addedVanillaFootprints = false;
            PlayerEffectsManager.isInColdZone = false;
            SnowThicknessManager.Instance?.Reset();
            SnowTrackersManager.CleanupTrackers();
        }

        internal virtual void Update()
        {   
            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
            
            if ((SnowThicknessManager.Instance?.isOnNaturalGround ?? false) &&
                    localPlayer.physicsParent == null &&
                    !localPlayer.isPlayerDead &&
                    localPlayer.thisController.isGrounded)
            {
                float snowThickness = SnowThicknessManager.Instance.GetSnowThickness(localPlayer);
                // White out the screen if the player is under snow
                float localPlayerEyeY = localPlayer.gameplayCamera.transform.position.y;
                // TODO Possible to block visibility of ForestGiants based on this, need to calculate for all players
                PlayerEffectsManager.isUnderSnow = SnowThicknessManager.Instance.feetPositionY + snowThickness >= localPlayerEyeY - eyeBias;

                // If the user decreases frostbite damage from the default value (10), add additional slowdown
                float metaSnowThickness = Mathf.Clamp01(1 - SnowPatches.FrostbiteDamage/10f) * PlayerEffectsManager.ColdSeverity;

                // Slow down the player if they are in snow (only if snow thickness is above 0.4, caps at 2.5 height)
                snowMovementHindranceMultiplier = 1 + 5*Mathf.Clamp01((snowThickness + metaSnowThickness - 0.4f)/2.1f);
            }
            else
            {
                PlayerEffectsManager.isUnderSnow = false;
                snowMovementHindranceMultiplier = 1f;
            }

            // If normalized snow timer is at 30% of fullSnowNormalizedTime, turn on vanilla footprints
            if (Configuration.addFootprints.Value &&
                !addedVanillaFootprints &&
                !StartOfRound.Instance.currentLevel.levelIncludesSnowFootprints
                && (LevelManipulator.Instance?.snowIntensity ?? 10f) < 7)
            {
                StartOfRound.Instance.InstantiateFootprintsPooledObjects();
                addedVanillaFootprints = true;
            }
        }

        internal override void PopulateLevelWithVFX()
        {
            if (SnowfallWeather.Instance?.IsActive ?? false) // to avoid setting the depth texture for blizzard
            {
                HDRPCameraOrTextureBinder? depthBinder = snowVFXContainer!.GetComponent<HDRPCameraOrTextureBinder>();
                if (depthBinder != null)
                {
                    Debug.LogDebug("Binding depth texture to snow VFX");
                    depthBinder.depthTexture = LevelManipulator.Instance!.levelDepthmapUnblurred; // bind the baked depth texture
                    depthBinder.AdditionalData = LevelManipulator.Instance.levelDepthmapCamera!.GetComponent<HDAdditionalCameraData>(); // bind the camera data
                    depthBinder.SetCameraProperty("DepthCamera");
                }
            }

            SnowTrackersManager.CleanupTrackers(true);

            LevelManipulator.Instance!.snowVolume!.enabled = true;
            LevelManipulator.Instance.snowTrackerCameraContainer?.SetActive(true);

            // For blizzard weather prefab won't be set
            if (christmasTreePrefab == null || !Configuration.enableEasterEgg.Value)
            {
                return;
            }
            // If the current date is +- 2 days from 25th December, 31st December or 6th January, spawn a Christmas tree
            DateTime currentDate = DateTime.Now;
            HashSet<DateTime> christmasDates = GetChristmasDates(currentDate);
            if (christmasDates.Contains(currentDate))
            {
                JingleBells();
            }

#if DEBUG
            JingleBells();
#endif

        }

        private HashSet<DateTime> GetChristmasDates(DateTime currentDate)
        {
            // Get dates that are -2 days away from 25th December, 31st December or 6th January
            HashSet<DateTime> christmasDates =
            [
                new DateTime(currentDate.Year, 12, 23),
                new DateTime(currentDate.Year, 12, 24),
                new DateTime(currentDate.Year, 12, 25),
                new DateTime(currentDate.Year, 12, 29),
                new DateTime(currentDate.Year, 12, 30),
                new DateTime(currentDate.Year, 12, 31),
                new DateTime(currentDate.Year, 1, 4),
                new DateTime(currentDate.Year, 1, 5),
                new DateTime(currentDate.Year, 1, 6),
            ];

            return christmasDates;
        }

        private void JingleBells()
        {
            if (giftBoxItem == null)
            {
                Debug.LogError("Gift box item not found in the item database!");
                return;
            }
            
            Debug.Log("Merry Christmas!");
            

            int attempts = 24;
            bool treePlaced = false;
            Vector3 treePosition = Vector3.zero;
            while (attempts-- > 0)
            {
                // Select a random position in the level from RoundManager.Instance.outsideAINodes
                int randomIndex = SeededRandom?.Next(0, RoundManager.Instance.outsideAINodes.Length) ?? 0;
                Vector3 anchor = RoundManager.Instance.outsideAINodes[randomIndex].transform.position;
                // Sample another random position using navmesh around the anchor where there is at least 10x10m of space
                Vector3 randomPosition = RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(anchor, 25f, randomSeed: SeededRandom);
                randomPosition = RoundManager.Instance.PositionEdgeCheck(randomPosition, 7f);
                if (randomPosition != Vector3.zero)
                {
                    treePosition = randomPosition;
                    treePlaced = true;
                    break;
                }
            }
            
            if (!treePlaced)
            {
                Debug.LogDebug("Failed to place a Christmas tree in the level, too many attempts!");
                return;
            }

            Quaternion randomRotation = Quaternion.Euler(0, SeededRandom?.Next(0, 360) ?? 0f, 0);
            // Spawn a Christmas tree
            _ = Instantiate(christmasTreePrefab!, treePosition, randomRotation);

            // Only host can spawn the presents
            if (!GameNetworkManager.Instance.isHostingGame)
            {
                return;
            }

            // Spawn a gift box for each player in the game. Cap at 4 gifts so users with more than 4 players don't get too many
            int numGifts = Mathf.Min(GameNetworkManager.Instance.connectedPlayers, 4);

            NavMeshHit hit;
            for (int i = 0; i < numGifts; i++)
            {
                int giftValue = SeededRandom?.Next(1, 24) ?? 1;

                //Spawn gifts in a ring around the tree by sampling the NavMesh around it
                Vector3 giftPosition = treePosition + 2f * new Vector3(Mathf.Cos(i * 2 * Mathf.PI / numGifts), 0, Mathf.Sin(i * 2 * Mathf.PI / numGifts));
                if (NavMesh.SamplePosition(giftPosition, out hit, 2f, NavMesh.AllAreas))
                {
                    giftBoxItem.SpawnAtPosition(hit.position, giftValue);
                }
            }
        }

        internal void OnDestroy()
        {
            SnowTrackersManager.snowTrackersDict.Clear();
            SnowTrackersManager.snowShovelDict.Clear();
        }
    }
}