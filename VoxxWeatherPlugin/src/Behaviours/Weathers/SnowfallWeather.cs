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

namespace VoxxWeatherPlugin.Weathers
{
    internal class SnowfallWeather: BaseWeather
    {
        public static SnowfallWeather? Instance { get; protected set;}
        internal override string WeatherName => "Snowfall";
        
        internal float MinSnowHeight => Configuration.minSnowHeight.Value;
        internal float MaxSnowHeight => Configuration.maxSnowHeight.Value;
        internal float MinSnowNormalizedTime => Configuration.minTimeToFullSnow.Value;
        internal float MaxSnowNormalizedTime => Configuration.maxTimeToFullSnow.Value;
        
        [Header("General")]
        [SerializeField]
        internal float timeUntilFrostbite = 0.6f * Configuration.minTimeUntilFrostbite.Value;
        [SerializeField]
        internal SnowfallVFXManager? VFXManager;

        internal void Awake()
        {   
            Instance = this;

            LevelManipulator.Instance.InitializeSnowVariables();
        }

        internal void OnFinish()
        {
            LevelManipulator.Instance.ResetLevelProperties();
            LevelManipulator.Instance.ResetSnowVariables();
            PlayerEffectsManager.normalizedTemperature = 0f;
        }

        internal virtual void OnEnable()
        {
            Instance = this; // Change the global reference to this instance (for patches)
            LevelManipulator.Instance.InitializeLevelProperties(1.5f);
            LevelManipulator.Instance.SetupLevelForSnow(snowHeightRange: (MinSnowHeight, MaxSnowHeight),
                                                        snowNormalizedTimeRange: (MinSnowNormalizedTime, MaxSnowNormalizedTime),
                                                        snowScaleRange: (0.7f, 1.3f),
                                                        fogStrengthRange: (0f, 15f));
            VFXManager?.PopulateLevelWithVFX();
        }

        internal virtual void OnDisable()
        {
            OnFinish();
            VFXManager?.Reset();
        }

        internal virtual void Update()
        {
            LevelManipulator.Instance.UpdateLevelProperties();
            LevelManipulator.Instance.UpdateSnowVariables();
            SetColdZoneState();
        }

        internal virtual void SetColdZoneState()
        {
            PlayerEffectsManager.isInColdZone = VFXManager!.isUnderSnowPreviousFrame;
        }

    }

    public class SnowfallVFXManager: BaseVFXManager
    {
        [SerializeField]
        internal bool addedVanillaFootprints = false;

        [Header("Snow VFX")]
        [SerializeField]
        internal GameObject? snowVFXContainer;

        [SerializeField]
        internal Volume? frostbiteFilter;
        [SerializeField]
        internal Volume? frostyFilter;
        [SerializeField]
        internal Volume? underSnowFilter;

        internal static float snowMovementHindranceMultiplier = 1f;
        internal static int snowFootstepIndex = -1;

        private float targetWeight = 0f;
        private float currentWeight = 0f;
        private float UnderSnowVisualMultiplier => Configuration.underSnowFilterMultiplier.Value;
        private readonly float fadeSpeed = 2f; // Units per second
        private bool isFading = false;
        internal bool isUnderSnowPreviousFrame = false;
        [SerializeField]
        internal float eyeBias = 0.43f;

        [Header("Snow Tracker VFX")]
        
        [SerializeField]
        internal VisualEffectAsset[]? footprintsTrackerVFX;
        internal static Dictionary <string, VisualEffectAsset>? snowTrackersDict;

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
            PlayerEffectsManager.freezeEffectVolume = frostbiteFilter;
            
            frostbiteFilter!.enabled = true;
            frostyFilter!.enabled = true;
            underSnowFilter!.enabled = true;
            
            if (SnowfallWeather.Instance != null)
            {
                LevelManipulator.Instance.snowVolume!.enabled = true;
                LevelManipulator.Instance.snowTrackerCameraContainer?.SetActive(true);
            }
            // if (sunLightData != null)
            // {
            //     sunLightData.lightUnit = LightUnit.Lux;
            // }
        }

        internal virtual void OnDisable()
        {
            snowVFXContainer?.SetActive(false);
            // frostbiteFilter!.enabled = false;
            frostyFilter!.enabled = false;
            underSnowFilter!.enabled = false;
            LevelManipulator.Instance!.snowVolume!.enabled = false;
            LevelManipulator.Instance.snowTrackerCameraContainer?.SetActive(false);
            snowMovementHindranceMultiplier = 1f;
            PlayerEffectsManager.isInColdZone = false;
            isUnderSnowPreviousFrame = false;
        }

        internal override void Reset()
        {
            addedVanillaFootprints = false;
            PlayerEffectsManager.isInColdZone = false;
            SnowThicknessManager.Instance?.Reset();
            SnowTrackersManager.CleanupFootprintTrackers(SnowTrackersManager.snowTrackersDict);
            SnowTrackersManager.CleanupFootprintTrackers(SnowTrackersManager.snowShovelDict);
            SnowTrackersManager.ToggleFootprintTrackers(false);
        }

        internal void Update()
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
                bool isUnderSnow = SnowThicknessManager.Instance.feetPositionY + snowThickness >= localPlayerEyeY - eyeBias;

                if (isUnderSnow != isUnderSnowPreviousFrame)
                {
                    StartFade(isUnderSnow ? 1f : 0f);
                }

                isUnderSnowPreviousFrame = isUnderSnow;
                UpdateFade();

                // If the user decreases frostbite damage from the default value (10), add additional slowdown
                float metaSnowThickness = Mathf.Clamp01(1 - SnowPatches.FrostbiteDamage/10f) * PlayerEffectsManager.ColdSeverity;

                // Slow down the player if they are in snow (only if snow thickness is above 0.4, caps at 2.5 height)
                snowMovementHindranceMultiplier = 1 + 5*Mathf.Clamp01((snowThickness + metaSnowThickness - 0.4f)/2.1f);

                // Debug.LogDebug($"Hindrance multiplier: {snowMovementHindranceMultiplier}, isUnderSnow: {isUnderSnow}, snowThickness: {snowThickness}");
            }
            else
            {
                if (currentWeight > 0f)
                {
                    StartFade(0f);  // Fade to 0 if not on natural ground
                }
                UpdateFade(); // Continue updating the fade
                isUnderSnowPreviousFrame = false;
                snowMovementHindranceMultiplier = 1f;
                // Debug.LogDebug("Not on natural ground");
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

        private void StartFade(float target)
        {
            targetWeight = target;
            isFading = true;
        }

        private void UpdateFade()
        {
            if (isFading)
            {
                float weightDifference = targetWeight - currentWeight;
                float changeThisFrame = fadeSpeed * Time.deltaTime;

                if (Mathf.Abs(weightDifference) <= changeThisFrame)
                {
                    currentWeight = targetWeight;
                    isFading = false;
                }
                else
                {
                    currentWeight += Mathf.Sign(weightDifference) * changeThisFrame;
                }

                underSnowFilter!.weight = currentWeight * UnderSnowVisualMultiplier;
            }
        }

        internal override void PopulateLevelWithVFX()
        {
            if (!(SnowfallWeather.Instance is BlizzardWeather)) // to avoid setting the depth texture for blizzard
            {
                HDRPCameraOrTextureBinder? depthBinder = snowVFXContainer!.GetComponent<HDRPCameraOrTextureBinder>();
                if (depthBinder != null)
                {
                    Debug.LogDebug("Binding depth texture to snow VFX");
                    depthBinder.depthTexture = LevelManipulator.Instance!.levelDepthmapUnblurred; // bind the baked depth texture
                }
            }

            SnowTrackersManager.ToggleFootprintTrackers(true);

            LevelManipulator.Instance.snowVolume!.enabled = true;
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
            
            System.Random randomizer = LevelManipulator.Instance.seededRandom!;

            int attempts = 24;
            bool treePlaced = false;
            Vector3 treePosition = Vector3.zero;
            while (attempts-- > 0)
            {
                // Select a random position in the level from RoundManager.Instance.outsideAINodes
                int randomIndex = randomizer.Next(0, RoundManager.Instance.outsideAINodes.Length);
                Vector3 anchor = RoundManager.Instance.outsideAINodes[randomIndex].transform.position;
                // Sample another random position using navmesh around the anchor where there is at least 10x10m of space
                Vector3 randomPosition = RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(anchor, 25f, randomSeed: randomizer);
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

            Quaternion randomRotation = Quaternion.Euler(0, randomizer.Next(0, 360), 0);
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
                int giftValue = randomizer.Next(1, 24);

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