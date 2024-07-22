using BepInEx;
using GameNetcodeStuff;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;
using System;
using UnityEngine.Rendering.HighDefinition;

namespace DerelictMoonPlugin
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class DerelictMoonPlugin : BaseUnityPlugin
    {
        public static DerelictMoonPlugin instance;

        private static void NetcodePatcher()
        {
            var types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
        }

        private void Awake()
        {
            instance = this;

            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            NetcodePatcher();
        }

    }



    public static class ListShuffler
    {
        public static void ShuffleInSync<T1, T2>(IList<T1> list1, IList<T2> list2, System.Random random)
        {
            if (list1.Count != list2.Count)
            {
                throw new System.ArgumentException("Lists must have the same length.");
            }

            int n = list1.Count;
            for (int i = n - 1; i > 0; i--)
            {
                int j = random.Next(0, i + 1);

                // Swap elements in both lists
                (list1[i], list1[j]) = (list1[j], list1[i]);
                (list2[i], list2[j]) = (list2[j], list2[i]);
            }
        }
    }

    public class RingPortalStormEvent : NetworkBehaviour
    {
        public List<float> deliveryTimes = new List<float>();

        
        [SerializeField] private float maxStartTimeDelay = 120f;
        [SerializeField] private GameObject shipmentsContainer;
        [SerializeField] private GameObject shipmentPositionsObject; // Assign in the inspector, contains positions where to drop shipments
        [SerializeField] private float maxRotationSpeed = 5f;
        [SerializeField] private float rotationSpeedChangeDuration = 10f;
        [SerializeField] private float cooldownDuration = 5f;
        [SerializeField] private float movementDuration = 30f; // Duration of movement between positions
        [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // For smooth movement
        [SerializeField] private float maxTiltAngle = 25f; // Maximum tilt angle in degrees
        [SerializeField] private float tiltChangeDuration = 30f;
        [SerializeField] private AudioClip[] startMovingSounds;
        [SerializeField] private AudioClip[] ringMovementSounds;
        [SerializeField] private AudioClip startSpinningSound;
        [SerializeField] private float fadeOutDuration = 1f;

        private AudioSource audioSource;
        private Coroutine soundCoroutine;
        private Animator animator;
        private Vector3 targetRotation;
        private System.Random seededRandom;
        private List<GameObject> shipments = new List<GameObject>();
        private List<Transform> shipmentPositions = new List<Transform>();
        private float timer = 0f;
        private float timeDelay = 0f;
        private int shipmentItemNum = 0; // Number of items in the shipment
        private bool isPortalOpenAnimationFinished = false;
        private bool isPortalCloseAnimationFinished = false;
        private bool isDelivering = false;

        private NetworkVariable<int> currentShipmentIndex = new NetworkVariable<int>(0);
        private NetworkVariable<int> sharedSeed = new NetworkVariable<int>(StartOfRound.Instance.randomMapSeed);
        private NetworkVariable<bool> shipmentSettledOnServer = new NetworkVariable<bool>(false);

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                sharedSeed.Value = StartOfRound.Instance.randomMapSeed + 42;
            }
        }

        private void Start()
        {
            Debug.Log("RingPortalStormEvent: Start method called");
            animator = GetComponent<Animator>();

            InitializeShipmentPositions();
            InitializeShipments();

            if (shipmentPositions.Count != shipments.Count)
            {
                Debug.LogError("RingPortalStormEvent: Mismatch in number of shipments and delivery locations!");
            }
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            seededRandom = new System.Random(sharedSeed.Value);

            timeDelay = (float)seededRandom.NextDouble() * maxStartTimeDelay;
            for (int i = 0; i < deliveryTimes.Count; i++)
            {
                deliveryTimes[i] += timeDelay;
            }

            // Shuffle the shipment positions and delivery times
            ListShuffler.ShuffleInSync(shipmentPositions, shipments, seededRandom);
        }

        private void Update()
        {
            if (currentShipmentIndex.Value >= deliveryTimes.Count)
            {
                Debug.Log("RingPortalStormEvent: All shipments delivered, disabling station");
                this.enabled = false;
            }

            if (!IsServer) return; // Only run on the server

            timer += Time.deltaTime;
            //float timer = TimeOfDay.Instance.normalizedTimeOfDay;

            if (currentShipmentIndex.Value < deliveryTimes.Count && timer >= deliveryTimes[currentShipmentIndex.Value] && !isDelivering)
            {
                Debug.Log($"RingPortalStormEvent: Starting delivery sequence for shipment {currentShipmentIndex.Value}");
                StartCoroutine(PerformDeliverySequence());
            }

        }

        private void InitializeShipments()
        {
            Debug.Log("RingPortalStormEvent: Initializing shipments");
            if (shipmentsContainer == null)
            {
                Debug.LogError("RingPortalStormEvent: Shipments container is not assigned!");
                return;
            }

            // Iterate through all children of the shipmentsContainer
            foreach (Transform child in shipmentsContainer.transform)
            {
                shipments.Add(child.gameObject);
                Debug.Log($"Added shipment: {child.name}");
            }

            Debug.Log($"Total shipments: {shipments.Count}");
        }

        private void InitializeShipmentPositions()
        {
            Debug.Log("RingPortalStormEvent: Initializing shipment positions");
            if (shipmentPositionsObject == null)
            {
                Debug.LogError("RingPortalStormEvent: ShipmentPositions object is not assigned!");
                return;
            }

            // Iterate through all children of the shipmentPositionsObject
            foreach (Transform child in shipmentPositionsObject.transform)
            {
                shipmentPositions.Add(child);
                Debug.Log($"Added shipment position: {child.name} at {child.position}");
            }

            Debug.Log($"Total shipment positions: {shipmentPositions.Count}");
        }

        [ClientRpc]
        private void PlayMovementSoundsClientRpc()
        {
            if (soundCoroutine != null)
            {
                StopCoroutine(soundCoroutine);
            }
            soundCoroutine = StartCoroutine(MovementSoundSequence());
        }

        private IEnumerator MovementSoundSequence()
        {
            // Play random start moving sound
            if (startMovingSounds.Length > 0)
            {
                AudioClip randomStartSound = startMovingSounds[seededRandom.Next(startMovingSounds.Length)];
                audioSource.PlayOneShot(randomStartSound);
                yield return new WaitForSeconds(randomStartSound.length);
            }

            // Start looping movement sounds
            while (true)
            {
                if (ringMovementSounds.Length > 0)
                {
                    AudioClip randomClip = ringMovementSounds[seededRandom.Next(ringMovementSounds.Length)];
                    audioSource.clip = randomClip;
                    audioSource.Play();
                    yield return new WaitForSeconds(randomClip.length);
                }
                else
                {
                    yield return null;
                }
            }
        }

        [ClientRpc]
        private void StopMovementSoundsClientRpc()
        {
            if (soundCoroutine != null)
            {
                StopCoroutine(soundCoroutine);
                soundCoroutine = null;
            }
            StartCoroutine(FadeOutSound());

        }

        private IEnumerator FadeOutSound()
        {
            float startVolume = audioSource.volume;
            float deltaVolume = startVolume * Time.deltaTime / fadeOutDuration;

            while (audioSource.volume > 0)
            {
                audioSource.volume -= deltaVolume;
                yield return null;
            }

            audioSource.Stop();
            audioSource.volume = startVolume;
        }

        [ClientRpc]
        private void PlayStartSpinningSoundClientRpc()
        {
            if (startSpinningSound != null)
            {
                audioSource.clip = startSpinningSound;
                audioSource.Play();
            }
        }

        private IEnumerator PerformDeliverySequence()
        {

            Debug.Log("RingPortalStormEvent: Starting delivery sequence");
            isDelivering = true;
            shipmentSettledOnServer.Value = false;

            animator.SetBool("isPortalActive", false);
            animator.SetBool("isPortalOpenFinished", false);
            animator.SetBool("isPortalCloseFinished", false);
            isPortalOpenAnimationFinished = false;
            isPortalCloseAnimationFinished = false;

            // Start playing movement sounds immediately
            PlayMovementSoundsClientRpc();

            // Move to next position
            Debug.Log("RingPortalStormEvent: Moving to next position");
            yield return StartCoroutine(MoveToNextPosition());

            // Stop movement sounds with fade out
            StopMovementSoundsClientRpc();

            // Wait for fade out to complete
            yield return new WaitForSeconds(fadeOutDuration + 0.5f);

            // Play start spinning sound
            PlayStartSpinningSoundClientRpc();

            // Increase rotation speed
            Debug.Log("RingPortalStormEvent: Increasing rotation speed");
            yield return StartCoroutine(IncreaseRotationSpeed());

            // Activate portal and wait for animation to finish
            Debug.Log("RingPortalStormEvent: Activating portal");
            animator.SetBool("isPortalActive", true);
            animator.SetBool("isPortalOpenFinished", false);

            Debug.Log("RingPortalStormEvent: Waiting for portal open animation to finish");
            yield return new WaitUntil(() => isPortalOpenAnimationFinished);
            Debug.Log("RingPortalStormEvent: Portal open animation finished");

            yield return StartCoroutine(DecreaseRotationSpeed());

            yield return new WaitForSeconds(cooldownDuration);

            Debug.Log("RingPortalStormEvent: Spawning and dropping shipment");
            yield return StartCoroutine(SpawnAndDropShipmentServer());

            yield return new WaitForSeconds(cooldownDuration);

            Debug.Log("RingPortalStormEvent: Closing portal");
            animator.SetBool("isPortalActive", false);
            animator.SetBool("isPortalCloseFinished", false);

            Debug.Log("RingPortalStormEvent: Waiting for portal close animation to finish");
            yield return new WaitUntil(() => isPortalCloseAnimationFinished);
            Debug.Log("RingPortalStormEvent: Portal close animation finished");

            Debug.Log($"RingPortalStormEvent: Preparing for next delivery. Current index: {currentShipmentIndex}");
            currentShipmentIndex.Value++;
            yield return StartCoroutine(SetRandomTilt());

            Debug.Log("RingPortalStormEvent: Delivery sequence completed");
            isDelivering = false;
        }

        private IEnumerator MoveToNextPosition()
        {
            int nextPositionIndex = currentShipmentIndex.Value % shipmentPositions.Count;
            Vector3 startPosition = transform.position;
            Vector3 targetPosition = shipmentPositions[nextPositionIndex].position;

            // Preserve the current Y coordinate
            targetPosition.y = startPosition.y;

            // Set target rotation to zero for X and Z
            Vector3 startRotation = transform.rotation.eulerAngles;
            Vector3 levelRotation = new Vector3(0f, startRotation.y, 0f);

            float elapsedTime = 0f;

            while (elapsedTime < movementDuration)
            {
                float t = elapsedTime / movementDuration;
                float curveValue = movementCurve.Evaluate(t);

                // Move
                Vector3 newPosition = Vector3.Slerp(startPosition, targetPosition, curveValue);
                transform.position = newPosition;

                // Rotate
                Vector3 newRotation = Vector3.Slerp(startRotation, levelRotation, curveValue);
                transform.rotation = Quaternion.Euler(newRotation);

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // Ensure we end up exactly at the target position and rotation
            transform.position = targetPosition;
            transform.rotation = Quaternion.Euler(levelRotation);

            Debug.Log("RingPortalStormEvent: Finished moving to next position");

        }

        private IEnumerator IncreaseRotationSpeed()
        {
            Debug.Log("RingPortalStormEvent: Starting to increase rotation speed");
            float elapsedTime = 0f;

            while (elapsedTime < rotationSpeedChangeDuration)
            {
                float t = elapsedTime / rotationSpeedChangeDuration;
                float outerSpeed = Mathf.Lerp(1f, maxRotationSpeed, t);
                float innerSpeed = Mathf.Lerp(0.5f, maxRotationSpeed * 0.75f, t);

                animator.SetFloat("RotSpeedOuter", outerSpeed);
                animator.SetFloat("RotSpeedInner", innerSpeed);

                elapsedTime += Time.deltaTime;
                yield return null;
            }
            Debug.Log("RingPortalStormEvent: Finished increasing rotation speed");
        }

        private IEnumerator DecreaseRotationSpeed()
        {
            Debug.Log("RingPortalStormEvent: Starting to decrease rotation speed");

            float elapsedTime = 0f;

            while (elapsedTime < (rotationSpeedChangeDuration * 0.2f))
            {
                float t = elapsedTime / (rotationSpeedChangeDuration * 0.2f);
                float outerSpeed = Mathf.Lerp(maxRotationSpeed, 1f, t);
                float innerSpeed = Mathf.Lerp(maxRotationSpeed * 0.75f, 0.5f, t);

                animator.SetFloat("RotSpeedOuter", outerSpeed);
                animator.SetFloat("RotSpeedInner", innerSpeed);

                elapsedTime += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator SetRandomTilt()
        {
            Debug.Log("RingPortalStormEvent: tilting the station");

            // Choose random tilt angles
            float targetTiltX = (float)seededRandom.NextDouble() * maxTiltAngle;
            float targetTiltZ = (float)seededRandom.NextDouble() * maxTiltAngle;
            targetRotation = new Vector3(targetTiltX, transform.rotation.eulerAngles.y, targetTiltZ);

            float elapsedTime = 0f;
            Vector3 startRotation = transform.rotation.eulerAngles;

            while (elapsedTime < tiltChangeDuration)
            {
                float t = elapsedTime / tiltChangeDuration;

                // Gradually change rotation
                Vector3 newRotation = Vector3.Slerp(startRotation, targetRotation, t);
                transform.rotation = Quaternion.Euler(newRotation);

                elapsedTime += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator SpawnAndDropShipmentServer()
        {
            int settledObjectCount = 0;
            Action<GameObject> onObjectSettled = (obj) =>
            {
                settledObjectCount++;
            };

            PrepareShipmentClientRpc();

            ShipmentCollisionHandler.OnObjectSettled += onObjectSettled;

            // Wait until all objects have settled
            yield return new WaitUntil(() => settledObjectCount == shipmentItemNum - 1); // -1 to exclude the parent object itself
            Debug.Log("RingPortalStormEvent: Shipment dropped");
            ShipmentCollisionHandler.OnObjectSettled -= onObjectSettled; // Unsubscribe to prevent memory leaks
            shipmentSettledOnServer.Value = true;
        }

        private IEnumerator SpawnAndDropShipmentClient()
        {
            // Wait until all objects have settled on server
            yield return new WaitUntil(() => shipmentSettledOnServer.Value);

            Debug.Log("RingPortalStormEvent: Shipment dropped");
        }

        [ClientRpc]
        private void PrepareShipmentClientRpc()
        {
            Debug.Log($"RingPortalStormEvent: Spawning shipment {currentShipmentIndex.Value % shipments.Count}");
            GameObject shipmentPrefab = shipments[currentShipmentIndex.Value % shipments.Count];
            AudioSource teleportAudio = shipmentPrefab.GetComponent<AudioSource>();
            AlignShipment(shipmentPrefab);
            Transform[] shipmentItems = shipmentPrefab.GetComponentsInChildren<Transform>(true);
            foreach (Transform item in shipmentItems)
            {
                ShipmentCollisionHandler shipmentCollisionHandler = item.gameObject.GetComponent<ShipmentCollisionHandler>();
                if (shipmentCollisionHandler != null)
                {
                    shipmentCollisionHandler.enabled = true;
                }
            }
            teleportAudio?.Play();
            shipmentItemNum = shipmentItems.Length;
        }

        private void AlignShipment(GameObject shipment)
        {
            Transform shipmentTransform = shipment.transform;
            // Get the shipment's local position and rotation relative to its parent.
            Vector3 localPosition = shipmentsContainer.transform.InverseTransformPoint(shipmentTransform.position);
            Quaternion localRotation = Quaternion.Inverse(shipmentsContainer.transform.rotation) * shipmentTransform.rotation;

            // Calculate the desired world position and rotation based on the ring's transform.
            Vector3 desiredWorldPosition = this.transform.TransformPoint(localPosition);
            Quaternion desiredWorldRotation = this.transform.rotation * localRotation;

            //Apply the calculated world position and rotation to the shipment.
            shipmentTransform.position = desiredWorldPosition;
            shipmentTransform.rotation = desiredWorldRotation;
        }

        public void OnPortalOpenAnimationFinished()
        {
            Debug.Log("RingPortalStormEvent: Portal open animation finished");
            animator.SetBool("isPortalOpenFinished", true);
            isPortalOpenAnimationFinished = true;
        }
        public void OnPortalCloseAnimationFinished()
        {
            Debug.Log("RingPortalStormEvent: Portal close animation finished");
            animator.SetBool("isPortalCloseFinished", true);
            isPortalCloseAnimationFinished = true;
        }
    }


    public class ShipmentCollisionHandler : NetworkBehaviour
    {
        public static event Action<GameObject> OnObjectSettled;

        [SerializeField] private float settlementThreshold = 0.1f;
        [SerializeField] private float initialCheckDelay = 0.5f;
        [SerializeField] private float checkInterval = 0.1f;
        [SerializeField] private float maxTimeToSettle = 15f;
        [SerializeField] private float killVelocityThreshold = 1f;

        private bool hasCollided = false;
        private MeshCollider meshCollider;
        private BoxCollider boxCollider;
        private Rigidbody rb;
        private AudioSource impactSound;
        private NavMeshObstacle navMeshObstacle;
        private ParticleSystem smokeExplosion;
        private NetworkTransform networkTransform;

        private Vector3 previousPosition;
        private Vector3 velocity;

        private void Start()
        {
            rb = GetComponent<Rigidbody>();
            impactSound = GetComponent<AudioSource>();
            meshCollider = GetComponent<MeshCollider>();
            boxCollider = GetComponent<BoxCollider>();
            navMeshObstacle = GetComponent<NavMeshObstacle>();
            smokeExplosion = GetComponent<ParticleSystem>();
            networkTransform = GetComponent<NetworkTransform>();
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

            if (meshRenderer != null)
            {
                meshRenderer.enabled = true;
            }

            if (networkTransform != null)
            {
                networkTransform.enabled = true;
            }

            if (navMeshObstacle != null)
            {
                navMeshObstacle.carving = false;
            }

            if (meshCollider != null)
            {
                meshCollider.convex = true;
                meshCollider.enabled = true;
            }

            if (boxCollider != null && IsServer)
            {
                boxCollider.enabled = true;
                boxCollider.isTrigger = true;
            }

            StartCoroutine(WaitAndEnablePhysics(1f)); // Wait for sync before enabling physics
        }

        private IEnumerator WaitAndEnablePhysics(float delay)
        {
            yield return new WaitForSeconds(delay); 

            if (rb != null)
            {
                if (IsServer)
                {
                    rb.useGravity = true;
                    rb.isKinematic = false;
                }
                else
                {
                    rb.useGravity = false;
                    rb.isKinematic = true;
                }

                previousPosition = rb.position;
            }
        }

        private void FixedUpdate()
        {
            if (IsServer)
            {
                velocity = rb.velocity;
            }
            else
            {
                Vector3 currentPosition = rb.position;
                velocity = (currentPosition - previousPosition) / Time.fixedDeltaTime;
                previousPosition = currentPosition;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!hasCollided && (collision.gameObject.CompareTag("Grass") || collision.gameObject.CompareTag("Aluminum")))
            {
                OnCollisionEnterServerRpc();
            }
            else if (collision.gameObject.CompareTag("Player"))
            {
                //Kill player
                PlayerControllerB playerController = collision.gameObject.GetComponent<PlayerControllerB>();
                if (playerController != null && velocity.magnitude > killVelocityThreshold)
                {
                    NotifyPlayerKillClientRpc(playerController.playerClientId, velocity);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer == LayerMask.NameToLayer("Enemies") && IsServer)
            {
                // Kill enemies
                EnemyAI enemyAI = other.gameObject.GetComponent<EnemyAI>();
                if (enemyAI != null)
                {
                    Debug.Log($"ShipmentCollisionHandler: Enemy crushed by falling debris");
                    enemyAI.KillEnemyOnOwnerClient(false);
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void OnCollisionEnterServerRpc()
        {
            hasCollided = true;
            PlayEffectsClientRpc();
            StartCoroutine(CheckIfSettled());
        }

        [ClientRpc]
        private void PlayEffectsClientRpc()
        {
            impactSound?.Play();
            smokeExplosion?.Play();
        }

        [ClientRpc]
        private void NotifyPlayerKillClientRpc(ulong clientId, Vector3 killVelocity)
        {
            Debug.Log($"ShipmentCollisionHandler: Player #{clientId} was crushed by falling debris");
            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
            if (localPlayer == null)
                return;
            
            if (localPlayer.playerClientId == clientId)
                localPlayer.KillPlayer(bodyVelocity: killVelocity, spawnBody: true,
                                       causeOfDeath: CauseOfDeath.Crushing, deathAnimation: 0);
        }

        private IEnumerator CheckIfSettled()
        {
            float elapsedTime = initialCheckDelay;
            yield return new WaitForSeconds(initialCheckDelay);

            while (velocity.magnitude > settlementThreshold && elapsedTime < maxTimeToSettle)
            {
                yield return new WaitForSeconds(checkInterval);
                elapsedTime += checkInterval;
            }

            SetObjectSettledClientRpc();
        }

        [ClientRpc]
        private void SetObjectSettledClientRpc()
        {
            rb.useGravity = false;
            rb.isKinematic = true;

            if (meshCollider != null)
            {
                meshCollider.convex = false;
            }

            if (boxCollider != null)
            {
                boxCollider.isTrigger = false;
                boxCollider.enabled = false;
            }

            if (navMeshObstacle != null)
            {
                navMeshObstacle.carving = true;
            }

            if (networkTransform != null)
            {
                networkTransform.enabled = false;
            }

            if (IsServer)
            {
                OnObjectSettled?.Invoke(gameObject);
            }

            this.enabled = false;
        }
    }

    internal class ToxicFumes : MonoBehaviour
    {
        [SerializeField] protected float damageTime = 3f;
        [SerializeField] protected float drunknessPower = 1.5f;
        [SerializeField] protected int damageAmount = 5;

        protected float damageTimer = 0f;

        protected virtual void ApplyDamage(PlayerControllerB playerController)
        {
            playerController.DamagePlayer(damageAmount, true, true, CauseOfDeath.Suffocation, 0, false, default(Vector3));
        }

        internal void ApplyToxicEffect(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                PlayerControllerB playerController = other.gameObject.GetComponent<PlayerControllerB>();

                if (playerController != null && playerController == GameNetworkManager.Instance.localPlayerController && !playerController.isInHangarShipRoom)
                {
                    damageTimer += Time.deltaTime;
                    playerController.drunknessInertia = Mathf.Clamp(playerController.drunknessInertia + Time.deltaTime / drunknessPower * playerController.drunknessSpeed, 0.1f, 10f);
                    playerController.increasingDrunknessThisFrame = true;
                    if (damageTimer >= damageTime)
                    {
                        ApplyDamage(playerController);
                        damageTimer = 0;
                    }
                }
            }
        }

        protected virtual void OnTriggerStay(Collider other)
        {
            ApplyToxicEffect(other);
        }
    }

    internal class ToxicFogWeather : ToxicFumes
    {
        [SerializeField] private float damageProb = .25f;
        private System.Random seededRandom;
        private LocalVolumetricFog toxicVolumetricFog;
        private bool isToxified = false;

        private void Start()
        {
            seededRandom = new System.Random(StartOfRound.Instance.randomMapSeed + 42);
            toxicVolumetricFog = GetComponent<LocalVolumetricFog>();
        }

        protected override void ApplyDamage(PlayerControllerB playerController)
        {
            if (seededRandom.NextDouble() < damageProb)
            {
                playerController.DamagePlayer(damageAmount, true, true, CauseOfDeath.Suffocation, 0, false, default(Vector3));
            }
        }
        protected override void OnTriggerStay(Collider other)
        {
            if (isToxified)
            {
                ApplyToxicEffect(other);
            }
        }

        private void ToxifyFog()
        {
            // Disable vanilla fog
            TimeOfDay.Instance.foggyWeather.enabled = false;
            // Enable toxic fog
            toxicVolumetricFog.parameters.meanFreePath = (float)seededRandom.Next((int)TimeOfDay.Instance.currentWeatherVariable, (int)TimeOfDay.Instance.currentWeatherVariable2);
            toxicVolumetricFog.enabled = true;
            isToxified = true;
        }

        private void PurifyFog()
        {
            // Disable toxic fog
            toxicVolumetricFog.enabled = false;
            isToxified = false;
        }

        private void Update()
        {
            if (TimeOfDay.Instance.currentLevelWeather == LevelWeatherType.Foggy && !isToxified)
            {
                ToxifyFog();
            }
            else if (TimeOfDay.Instance.currentLevelWeather != LevelWeatherType.Foggy && isToxified)
            {
                PurifyFog();
            }
        }

        private void OnDestroy()
        {
            PurifyFog();
        }
    }
}

