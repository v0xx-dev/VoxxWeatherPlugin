using UnityEngine;
using VoxxWeatherPlugin.Utils;
using System.Collections;
using GameNetcodeStuff;
using UnityEngine.Events;
using VoxxWeatherPlugin.Behaviours;
using UnityEngine.Rendering.HighDefinition;

namespace VoxxWeatherPlugin.Weathers
{
    internal class BlizzardWeather: SnowfallWeather
    {
        public new static BlizzardWeather? Instance { get; internal set; }
        // Overrides   
        internal override float MinSnowHeight => Configuration.minSnowHeightBlizzard.Value;
        internal override float MaxSnowHeight => Configuration.maxSnowHeightBlizzard.Value;
        internal override float MinSnowNormalizedTime => Configuration.minTimeToFullSnowBlizzard.Value;
        internal override float MaxSnowNormalizedTime => Configuration.maxTimeToFullSnowBlizzard.Value;

        [Header("Wind")]
        [SerializeField]
        internal Vector3 windDirection = new Vector3(0, 0, 1);
        [SerializeField]
        internal float timeSinceWindChange = 0;
        [SerializeField]
        internal float windChangeInterval = 30f;
        [SerializeField]
        internal float windForce = 0.25f;
        internal float MinWindForce => Configuration.minWindForce.Value;
        internal float MaxWindForce => Configuration.maxWindForce.Value;
        internal float MinTimeUntilFrostbite => Configuration.minTimeUntilFrostbite.Value;
        internal float MaxTimeUntilFrostbite => Configuration.maxTimeUntilFrostbite.Value;
        internal Coroutine? windChangeCoroutine;
        [SerializeField]
        internal bool isLocalPlayerInWind = false;
        [SerializeField]
        internal bool isPlayerInBlizzard = false;

        [Header("Chill Waves")]
        [SerializeField]
        internal int numOfWaves;
        internal int MinWaveCount => Configuration.minWaveCount.Value;
        internal int MaxWaveCount => Configuration.maxWaveCount.Value;
        [SerializeField]
        internal float timeSinceWave;
        [SerializeField]
        internal float waveSpeed;
        internal float MinWaveInterval => Configuration.minWaveInterval.Value;
        internal float MaxWaveInterval => Configuration.maxWaveInterval.Value;
        [SerializeField]
        internal float waveInterval = 90f;
        internal Coroutine? chillWaveCoroutine;
        [SerializeField]
        internal new BlizzardVFXManager VFXManager;
        // Used to implicitly sync the time between the server and the client via TimeOfDay
        private float timeAtStart = -1f;

        internal override void Awake()
        {
            Instance = this;
        }

        internal override void OnEnable()
        {
            LevelManipulator.Instance.InitializeLevelProperties(1.5f);
            LevelManipulator.Instance.SetupLevelForSnow(snowHeightRange: (MinSnowHeight, MaxSnowHeight),
                                                        snowNormalizedTimeRange: (MinSnowNormalizedTime, MaxSnowNormalizedTime),
                                                        snowScaleRange: (0.7f, 1.3f),
                                                        fogStrengthRange: (-5f, 5f));

            waveInterval = SeededRandom.NextDouble(MinWaveInterval, MaxWaveInterval);
            numOfWaves = SeededRandom.Next(MinWaveCount, MaxWaveCount);
            // If no ground objects are found, the wind force will be set to the maximum
            windForce = LevelManipulator.Instance.groundObjectCandidates.Count > 0 ? SeededRandom.NextDouble(MinWindForce, MaxWindForce) : MaxWindForce;
            timeUntilFrostbite = SeededRandom.NextDouble(MinTimeUntilFrostbite, MaxTimeUntilFrostbite);
            TimeOfDay.Instance.onTimeSync.AddListener(new UnityAction(OnGlobalTimeSync));     
            VFXManager?.PopulateLevelWithVFX();
        }

        internal override void OnDisable()
        {
            OnFinish();
            timeAtStart = -1f;
            TimeOfDay.Instance.onTimeSync.RemoveListener(new UnityAction(OnGlobalTimeSync));
            VFXManager?.Reset();
        }

        internal void OnGlobalTimeSync()
        {
            if (timeAtStart == -1f)
            {
                timeAtStart = TimeOfDay.Instance.globalTime;
            }

            // Using synced global time to avoid making this a NetworkBehaviour
            timeSinceWave += TimeOfDay.Instance.globalTime - timeAtStart; 
            timeSinceWindChange += TimeOfDay.Instance.globalTime - timeAtStart;
            
            if (chillWaveCoroutine != null || windChangeCoroutine != null)
            {
                return;
            }

            // To keep the chill waves less frequent than the wind changes
            if (timeSinceWave >= waveInterval && SeededRandom!.NextDouble() < 0.5f)
            {
                chillWaveCoroutine = StartCoroutine(GenerateChillWaveCoroutine());
                return;
            }

            if (timeSinceWindChange >= windChangeInterval)
            {
                windChangeCoroutine = StartCoroutine(ChangeWindDirectionCoroutine());
                return;
            }
        }
        
        internal void FixedUpdate()
        {
            Vector3 playerHeadPos = GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position;
            
            // Calculate the direction vector from playerHeadPos to blizzard source
            Vector3 directionToSource = (VFXManager.blizzardCollisionCamera.transform.position - playerHeadPos).normalized;

            // Calculate the point 20 meters away in the direction of the blizzard source
            Vector3 targetPoint = playerHeadPos + directionToSource * 20f;
            
            isLocalPlayerInWind = !Physics.Linecast(playerHeadPos, targetPoint,
                                        LayerMask.GetMask("Room", "Terrain", "Default", "NavigationSurface"),
                                            QueryTriggerInteraction.Ignore);

#if DEBUG
            // Visualize the Linecast
            UnityEngine.Debug.DrawLine(playerHeadPos, targetPoint, Color.red, Time.fixedDeltaTime);
            // Visualize the Wind Direction
            UnityEngine.Debug.DrawRay(VFXManager.blizzardCollisionCamera.transform.position, windDirection * 10, Color.blue, Time.fixedDeltaTime);
            // If linecast hits, visualize the hit point by doing a raycast and drawing a line
            if (!isLocalPlayerInWind)
            {
                RaycastHit hit;
                if (Physics.Raycast(playerHeadPos, targetPoint - playerHeadPos, out hit, 99f,
                                    LayerMask.GetMask("Room", "Terrain", "Default", "NavigationSurface"),
                                        QueryTriggerInteraction.Ignore))
                {
                    UnityEngine.Debug.DrawLine(playerHeadPos, hit.point, Color.green, Time.fixedDeltaTime);
                    // Debug.LogDebug($"Hit object: {hit.collider.gameObject.name}");
                }
            }       
#endif
        }

        internal override void Update()
        {
            base.Update();

            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;

            if (IsWindAllowed(localPlayer) && isLocalPlayerInWind)
            {
                if (localPlayer.physicsParent != null)
                {
                    PlayerEffectsManager.heatTransferRate = 0.05f; // Cooling down slower in vehicles
                }
                else
                {
                    PlayerEffectsManager.heatTransferRate = 1f; // Cooling down faster outdoors
                }

                localPlayer.externalForces += windDirection * windForce;
                isPlayerInBlizzard = true;
            }
            else
            {
                isPlayerInBlizzard = false;
            }
        }
            
        internal override void SetColdZoneState()
        {
            PlayerEffectsManager.isInColdZone = PlayerEffectsManager.isUnderSnow || isPlayerInBlizzard;
        }

        internal bool IsWindAllowed(PlayerControllerB localPlayer)
        {
#if DEBUG
            if (localPlayer.physicsParent != null)
            {
                Debug.LogDebug("Player is in vehicle");
            }
            if (localPlayer.isInHangarShipRoom)
            {
                Debug.LogDebug("Player is in hangar ship room");
            }
            if (localPlayer.isInsideFactory)
            {
                Debug.LogDebug("Player is inside factory");
            }
            if (localPlayer.isInElevator)
            {
                Debug.LogDebug("Player is in elevator");
            }
            if (localPlayer.inAnimationWithEnemy)
            {
                Debug.LogDebug("Player is in animation with enemy");
            }
            if (localPlayer.isPlayerDead)
            {
                Debug.LogDebug("Player is dead");
            }
            if (localPlayer.currentAudioTrigger?.insideLighting ?? false)
            {
                Debug.LogDebug("Player is inside interior lighting");
            }
#endif
            return !(localPlayer.physicsParent != null || 
                    localPlayer.isInHangarShipRoom || localPlayer.isInsideFactory ||
                     localPlayer.isInElevator || localPlayer.inAnimationWithEnemy ||
                      localPlayer.isPlayerDead || (localPlayer.currentAudioTrigger?.insideLighting ?? false));
        }

        internal IEnumerator ChangeWindDirectionCoroutine()
        {
            GameObject windContainer = VFXManager.snowVFXContainer;
            GameObject chillWaveContainer = VFXManager.blizzardWaveContainer;

            // Generate a random angle
            float randomAngle = SeededRandom.NextDouble(-5f, 20f); // So it would tend to change direction clockwise
            
            Debug.LogDebug("Changing wind direction by " + randomAngle + " degrees");
            
            // Get the initial rotation.
            Quaternion startRotation = windContainer.transform.rotation;

            // Calculate the target rotation.
            Quaternion targetRotation = Quaternion.Euler(startRotation.eulerAngles.x, startRotation.eulerAngles.y + randomAngle, startRotation.eulerAngles.z);
            
            Quaternion interpolatedRotation;
            // Rotate over time.
            float elapsedTime = 0f;
            float startTime = TimeOfDay.Instance.globalTime;
            while (elapsedTime < windChangeInterval)
            {
                interpolatedRotation = Quaternion.Slerp(startRotation, targetRotation, elapsedTime / windChangeInterval);
                windContainer.transform.rotation = interpolatedRotation;
                chillWaveContainer.transform.rotation = interpolatedRotation;
                elapsedTime += TimeOfDay.Instance.globalTime - startTime; // Using synced global time to avoid making this a NetworkBehaviour
                yield return null;
            }

            // Ensure the final rotation is exact.
            windContainer.transform.rotation = targetRotation;
            chillWaveContainer.transform.rotation = targetRotation;

            windDirection = Quaternion.Euler(0f, randomAngle, 0f) * windDirection;
            windDirection.Normalize();
            windForce = SeededRandom.NextDouble(MinWindForce, MaxWindForce);
            timeSinceWindChange = 0;
            windChangeCoroutine = null;
        }

        internal IEnumerator GenerateChillWaveCoroutine()
        {
            GameObject chillWaveContainer = VFXManager.blizzardWaveContainer!;
            float levelRadius = LevelBounds.size.magnitude; // Actually the diameter to make the wave go through the whole level
            Debug.LogDebug($"Generating {numOfWaves} chill waves");

            for (int i = 0; i < numOfWaves; i++)
            {
                // Shake screen and play sound
                VFXManager.PlaySonicBoomSFX();
                // Calculate initial position
                Vector3 initialDirection = -windDirection;
                Vector3 initialPosition = initialDirection * levelRadius;
                initialPosition.y = 0; // Ignore the vertical component (we use collider for positioning)

                // Place and enable the chill wave
                chillWaveContainer.transform.position = initialPosition;

                // Calculate target position (diametrically opposite)
                Vector3 targetPosition = - initialDirection * levelRadius;

                // Face the chill wave towards the target position
                chillWaveContainer.transform.LookAt(targetPosition);

                // Smoothly move the chill wave
                float distance = Vector3.Distance(initialPosition, targetPosition);
                float duration = distance / waveSpeed;  // Calculate duration based on speed and distance

                float elapsedTime = 0f;
                float startTime = TimeOfDay.Instance.globalTime;
                while (elapsedTime < duration)
                {
                    chillWaveContainer.SetActive(!GameNetworkManager.Instance?.localPlayerController?.isInsideFactory ?? false); // Only show the chill wave if the player is not inside the factory
                    chillWaveContainer.transform.position = Vector3.Lerp(initialPosition, targetPosition, elapsedTime / duration);
                    elapsedTime = TimeOfDay.Instance.globalTime - startTime; // Using synced global time to avoid making this a NetworkBehaviour
                    yield return null;
                }

                // Ensure the chill wave reaches the exact target position
                chillWaveContainer.transform.position = targetPosition;

                // Hide the chill wave
                chillWaveContainer.SetActive(false);
                
            }

            waveInterval = SeededRandom.NextDouble(MinWaveInterval, MaxWaveInterval);
            numOfWaves = SeededRandom.Next(MinWaveCount, MaxWaveCount);
            timeSinceWave = 0;
            chillWaveContainer.SetActive(false);
            chillWaveCoroutine = null;
        }
    }

    public class BlizzardVFXManager: SnowfallVFXManager
    {
        [Header("Blizzard Effects")]
        [SerializeField]
        internal GameObject? blizzardWaveContainer;
        [SerializeField]
        internal AudioSource? blizzardSFXPlayer;
        [SerializeField]
        internal AudioClip? sonicBoomSFX;
        [SerializeField]
        internal AudioClip? wavePassSFX;
        internal Camera? blizzardCollisionCamera;
        internal LocalVolumetricFog? blizzardFog;

        internal override void Start()
        {
            base.Start();
            blizzardSFXPlayer = GetComponent<AudioSource>();
            blizzardSFXPlayer.spatialBlend = 0; // 2D sound
            blizzardCollisionCamera = snowVFXContainer.GetComponentInChildren<Camera>();
        }

        internal override void OnEnable()
        {
            base.OnEnable();
        }

        internal override void OnDisable()
        {
            base.OnDisable();
            blizzardWaveContainer?.SetActive(false);
        }

        internal override void Update()
        {
            base.Update();
            blizzardCollisionCamera?.LimitFrameRate(Configuration.collisionCamerasFPS.Value);
        }

        internal override void Reset()
        {
            base.Reset();
            blizzardWaveContainer?.SetActive(false);
        }

        internal void PlayWavePassSFX()
        {
            HUDManager.Instance.ShakeCamera(ScreenShakeType.VeryStrong);
            blizzardSFXPlayer?.PlayOneShot(wavePassSFX);
        }

        internal void PlaySonicBoomSFX()
        {
            HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
            blizzardSFXPlayer?.PlayOneShot(sonicBoomSFX);
        }

        internal override void PopulateLevelWithVFX()
        {
            base.PopulateLevelWithVFX();

            if (blizzardWaveContainer == null)
            {
                Debug.LogError("Blizzard wave container is null!");
                return;
            }

            blizzardWaveContainer.SetActive(false);

            if (blizzardWaveContainer.TryGetComponent(out ChillWaveTrigger chillWaveTrigger))
            {
                chillWaveTrigger.SetupChillWave(LevelBounds);
                Debug.LogDebug("Chill wave trigger setup!");
            }

            blizzardFog ??= snowVFXContainer?.GetComponentInChildren<LocalVolumetricFog>(true);

            if (Configuration.useVolumetricBlizzardFog.Value && blizzardFog != null)
            {
                blizzardFog.gameObject.SetActive(true);
                blizzardFog.parameters.meanFreePath = SeededRandom.NextDouble(Configuration.blizzardFogMeanFreePathMin.Value, Configuration.blizzardFogMeanFreePathMax.Value);
            }
            
        }
    }
}