using UnityEngine;
using VoxxWeatherPlugin.Utils;
using System.Collections;
using GameNetcodeStuff;
using UnityEngine.Rendering;

namespace VoxxWeatherPlugin.Weathers
{
    internal class BlizzardWeather: SnowfallWeather
    {
        [Header("Visuals")]
        [SerializeField]
        internal Camera blizzardCollisionCamera;
        // [SerializeField]
        // internal Camera blizzardWaveCamera;
        // [SerializeField]
        // internal Volume frostbiteVolume;

        [Header("Wind")]
        [SerializeField]
        internal Vector3 windDirection = new Vector3(0, 0, 1);
        [SerializeField]
        internal float timeUntilWindChange = 0;
        [SerializeField]
        internal float windChangeInterval = 20f;
        [SerializeField]
        internal float windForce = 10f;
        internal Coroutine windChangeCoroutine;

        [Header("Chill Waves")]
        [SerializeField]
        internal float numOfWaves;
        [SerializeField]
        internal float timeUntilWave;
        [SerializeField]
        internal float waveSpeed;
        [SerializeField]
        internal float waveInterval = 90f;
        internal bool isChillWaveActive = false;
        internal Coroutine chillWaveCoroutine;
        [SerializeField]
        internal new BlizzardVFXManager VFXManager;

        internal override void OnEnable()
        {
            base.OnEnable();
            waveInterval = seededRandom.NextDouble(60f, 180f);
            windForce = seededRandom.NextDouble(10f, 20f);
            maxSnowHeight = seededRandom.NextDouble(1.0f, 1.7f);
            maxSnowNormalizedTime = seededRandom.NextDouble(0.1f, 0.3f);
            timeUntilFrostbite = seededRandom.NextDouble(60f, 120f);
            
        }

        internal override void FixedUpdate()
        {
            base.FixedUpdate();
            if (chillWaveCoroutine == null)
            {
                timeUntilWave += Time.fixedDeltaTime;
                if (timeUntilWave > waveInterval)
                {
                    chillWaveCoroutine = StartCoroutine(GenerateChillWaveCoroutine());
                    timeUntilWave = 0f;
                }
            }

            if (windChangeCoroutine == null && !isChillWaveActive)
            {
                timeUntilWindChange += Time.fixedDeltaTime;
                if (timeUntilWindChange > windChangeInterval)
                {
                    windChangeCoroutine = StartCoroutine(ChangeWindDirectionCoroutine());
                    timeUntilWindChange = 0f;
                }
            }
        
            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
            Vector3 playerHeadPos = localPlayer.gameplayCamera.transform.position;
            // Could be optimized since the camera position is constant relative to the player, TODO?
            // Vector3 nearestPointOnPlane = GetNearestPointOnPlane(playerHeadPos, blizzardCollisionCamera.transform.position, windDirection);
            
            // Calculate the direction vector from playerHeadPos to blizzard source
            Vector3 directionToSource = (blizzardCollisionCamera.transform.position - playerHeadPos).normalized;

            // Calculate the point 20 meters away in the direction of the blizzard source
            Vector3 targetPoint = playerHeadPos + directionToSource * 20f;
            
            bool isPlayerInBlizzard = !Physics.Linecast(playerHeadPos, targetPoint,
                                        LayerMask.GetMask("Room", "Terrain", "Default", "NavigationSurface"),
                                            QueryTriggerInteraction.Ignore);

#if DEBUG
            // Visualize the Linecast
            UnityEngine.Debug.DrawLine(playerHeadPos, targetPoint, Color.red, Time.fixedDeltaTime);
            // Visualize the Wind Direction
            UnityEngine.Debug.DrawRay(blizzardCollisionCamera.transform.position, windDirection * 10, Color.blue, Time.fixedDeltaTime);
            // If linecast hits, visualize the hit point by doing a raycast and drawing a line
            if (!isPlayerInBlizzard)
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

            if (IsWindAllowed(localPlayer) && isPlayerInBlizzard)
            {
                localPlayer.externalForces += windDirection * windForce * Time.fixedDeltaTime;
                PlayerTemperatureManager.isInColdZone = true;
                PlayerTemperatureManager.SetPlayerTemperature(-Time.fixedDeltaTime / timeUntilFrostbite);
            }
            else
            {
                PlayerTemperatureManager.isInColdZone = false;
            }

        }

        Vector3 GetNearestPointOnPlane(Vector3 point, Vector3 planePoint, Vector3 planeNormal)
        {
            float distance = Vector3.Dot(planeNormal, planePoint - point);
            return point + planeNormal * distance;
        }

        internal bool IsWindAllowed(PlayerControllerB localPlayer)
        {
            return !(localPlayer.isClimbingLadder || localPlayer.physicsParent != null || 
                    localPlayer.isInHangarShipRoom || localPlayer.isInsideFactory ||
                     localPlayer.isInElevator || localPlayer.inAnimationWithEnemy ||
                      localPlayer.isPlayerDead || (localPlayer.currentAudioTrigger?.setInsideAtmosphere ?? false));
        }

        internal IEnumerator ChangeWindDirectionCoroutine()
        {
            GameObject windContainer = VFXManager.snowVFXContainer;
            GameObject chillWaveContainer = VFXManager.blizzardWaveContainer;

            // Generate a random angle
            float randomAngle = seededRandom.NextDouble(-45f, 45f);

            // Get the initial rotation.
            Quaternion startRotation = windContainer.transform.rotation;

            // Calculate the target rotation.
            Quaternion targetRotation = Quaternion.Euler(startRotation.eulerAngles.x, startRotation.eulerAngles.y + randomAngle, startRotation.eulerAngles.z);
            
            Quaternion interpolatedRotation;
            // Rotate over time.
            float elapsedTime = 0f;
            float t;
            while (elapsedTime < windChangeInterval)
            {
                //Pause if chill wave is active
                if (isChillWaveActive)
                {
                    yield return null;
                }

                t = elapsedTime / windChangeInterval;
                interpolatedRotation = Quaternion.Slerp(startRotation, targetRotation, t);
                windContainer.transform.rotation = interpolatedRotation;
                chillWaveContainer.transform.rotation = interpolatedRotation;
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // Ensure the final rotation is exact.
            windContainer.transform.rotation = targetRotation;
            chillWaveContainer.transform.rotation = targetRotation;

            windDirection = Quaternion.Euler(0f, randomAngle, 0f) * windDirection;
            windForce = seededRandom.NextDouble(10f, 20);
            windChangeCoroutine = null; 
        }

        internal IEnumerator GenerateChillWaveCoroutine()
        {
            GameObject chillWaveContainer = VFXManager.blizzardWaveContainer;
            float levelRadius = levelBounds.size.magnitude / 2;

            for (int i = 0; i < numOfWaves; i++)
            {
                isChillWaveActive = true;
                // Calculate initial position
                Vector3 initialDirection = -windDirection.normalized;
                Vector3 initialPosition = levelBounds.center + initialDirection * levelRadius;

                // Place and enable the chill wave
                chillWaveContainer.transform.position = initialPosition;
                chillWaveContainer.SetActive(true);

                // Calculate target position (diametrically opposite)
                Vector3 targetPosition = levelBounds.center - initialDirection * levelRadius;

                // Smoothly move the chill wave
                float distance = Vector3.Distance(initialPosition, targetPosition);
                float duration = distance / waveSpeed;  // Calculate duration based on speed and distance

                float elapsedTime = 0f;
                while (elapsedTime < duration)
                {
                    chillWaveContainer.transform.position = Vector3.Lerp(initialPosition, targetPosition, elapsedTime / duration);
                    elapsedTime += Time.deltaTime;
                    yield return null;
                }

                // Ensure the chill wave reaches the exact target position
                chillWaveContainer.transform.position = targetPosition;

                // Hide the chill wave
                chillWaveContainer.SetActive(false);

                isChillWaveActive = false;
                // Wait for a bit
                yield return new WaitForSeconds(5f);
            }

            waveInterval = seededRandom.NextDouble(60f, 180f);
            numOfWaves = seededRandom.Next(1, 5);
            chillWaveCoroutine = null;
            
        }
    }

    public class BlizzardVFXManager: SnowfallVFXManager
    {
        [SerializeField]
        internal GameObject blizzardWaveContainer;

        internal override void OnEnable()
        {
            base.OnEnable();
            blizzardWaveContainer.SetActive(true);
        }

        internal override void OnDisable()
        {
            base.OnDisable();
            blizzardWaveContainer.SetActive(false);
        }

        internal override void Reset()
        {
            base.Reset();
            blizzardWaveContainer.SetActive(false);
        }
    }
}