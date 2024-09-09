using System;
using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace VoxxWeatherPlugin.Tests
{
    public class ConeTestScript : MonoBehaviour
    {
        public Material coneMaterial;
        public float coneHeight = 10f;
        public float coneRadius = 2f;
        public int pointCount = 40;
        public Color coneColor = Color.white;

        private void Start()
        {
            CreateCone();
        }

        private void CreateCone()
        {
            if (pointCount < 3 || pointCount >= 254)
                throw new System.ArgumentOutOfRangeException(nameof(pointCount));

            Vector3[] points = new Vector3[pointCount + 1];
            int[] indices = new int[pointCount * 3];

            points[0] = Vector3.zero;
            for (int i = 0; i < pointCount; i++)
            {
                float theta = (float)i / pointCount * Mathf.PI * 2f;
                Vector3 offset = new Vector3(Mathf.Cos(theta), Mathf.Sin(theta), 0) * coneRadius;
                points[i + 1] = new Vector3(0, 0, coneHeight) + offset;
                indices[i * 3 + 0] = 0;
                indices[i * 3 + 1] = 1 + ((i + 1) % pointCount);
                indices[i * 3 + 2] = i + 1;
            }

            GameObject coneObject = new GameObject("TestCone");
            coneObject.transform.SetParent(transform);

            MeshFilter meshFilter = coneObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = coneObject.AddComponent<MeshRenderer>();
            MeshCollider meshCollider = coneObject.AddComponent<MeshCollider>();

            Mesh mesh = new Mesh();
            meshFilter.mesh = mesh;
            mesh.vertices = points;
            mesh.triangles = indices;
            mesh.RecalculateNormals();

            meshRenderer.material = coneMaterial;
            meshRenderer.material.color = new Color(coneColor.r, coneColor.g, coneColor.b, 56f / 255f);

            meshCollider.sharedMesh = mesh;
            meshCollider.convex = true;
            meshCollider.isTrigger = true;

            //coneObject.AddComponent<ConeTriggerHandler>();
            coneObject.AddComponent<BetterCooldownTrigger>();
        }
    }

    public class ConeTriggerHandler : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                Debug.Log("Player entered the cone trigger!");
            }
        }

        // private void OnTriggerStay(Collider other)
        // {
        //     if (other.CompareTag("Player"))
        //     {
        //         Debug.Log("Player is staying in the cone trigger.");
        //     }
        // }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                Debug.Log("Player exited the cone trigger.");
            }
        }
    }

    public class BetterCooldownTrigger : NetworkBehaviour
    {
        #region Enums

        public enum DeathAnimation
        {
            Default,
            HeadBurst,
            Spring,
            Electrocuted,
            ComedyMask,
            TragedyMask,
            Burnt,
            Snipped,
            SliceHead
        }

        public enum ForceDirection
        {
            Forward,
            Backward,
            Up,
            Down,
            Left,
            Right,
            Center,
        }

        #endregion

        #region Fields
        [Header("Script Enabled")]
        [Tooltip("Whether this script is enabled.")]
        public bool enabledScript = true;
        [Header("Death Animation Settings")]
        [Tooltip("Different ragdoll body types that spawn after death.")]
        public DeathAnimation deathAnimation = DeathAnimation.Default;
        [Space(2)]
        [Header("Force Settings")]
        [Tooltip("The force direction of the damage.")]
        public ForceDirection forceDirection = ForceDirection.Forward;
        [Tooltip("The force magnitude of the damage.")]
        public float forceMagnitudeAfterDamage = 0f;
        [Tooltip("The force magnitude after death of player.")]
        public float forceMagnitudeAfterDeath = 0f;
        [Tooltip("If true, the force direction will be calculated from the object's transform. If false, the force direction will be calculated from the player's transform.")]
        public bool forceDirectionFromThisObject = true;
        [Space(2)]
        [Header("Cause of Death")]
        [Tooltip("Cause of death displayed in ScanNode after death.")]
        public CauseOfDeath causeOfDeath = CauseOfDeath.Unknown;
        [Space(2)]
        [Header("Trigger Settings")]
        [Tooltip("Whether to trigger for enemies.")]
        public bool triggerForEnemies = false;
        [Tooltip("Whether player/enemy can exit the trigger's effect.")]
        public bool canThingExit = true;
        [Tooltip("Whether to use shared cooldown between different GameObjects that use this script.")]
        public bool sharedCooldown = false;
        [Tooltip("Whether to play default player damage SFX when damage is dealt.")]
        public bool playDefaultPlayerDamageSFX = false;
        [Tooltip("Whether to play sound when damage is dealt to player that enemies can hear.")]
        public bool soundAttractsDogs = false;
        [Space(2)]
        [Header("Damage Settings")]
        [Tooltip("Timer in which the gameobject will disable itself, 0 will not disable itself after any point of time.")]
        public float damageDuration = 0f;
        [Tooltip("Damage to deal every interval for players.")]
        public int damageToDealForPlayers = 0;
        [Tooltip("Damage to deal every interval for enemies.")]
        public int damageToDealForEnemies = 0;
        [Tooltip("Cooldown to deal damage for players.")]
        public float damageIntervalForPlayers = 0.25f;
        [Tooltip("Cooldown to deal damage for enemies.")]
        public float damageIntervalForEnemies = 0.25f;
        [Space(2)]
        [Header("Audio Settings")]
        [Tooltip("Damage clip to play when damage is dealt to player/enemy.")]
        public List<AudioClip>? damageClip = null;
        [Tooltip("Damage audio sources to play when damage is dealt to player (picks the closest AudioSource to the player).")]
        public List<AudioSource>? damageAudioSources = null;
        [Space(2)]
        [Header("Death Prefab Settings")]
        [Tooltip("Prefab to spawn when the player dies.")]
        public GameObject? deathPrefabForPlayer = null;
        [Tooltip("Prefab to spawn when the enemy dies.")]
        public GameObject? deathPrefabForEnemy = null;
        [Space(2)]
        [Header("Particle System Settings")]
        [Tooltip("Use particle systems when damage is dealt to player/enemy.")]
        public bool useParticleSystems = false;
        [Tooltip("Teleport particle system to enemy/player when damage is dealt.")]
        public bool teleportParticles = false;
        [Tooltip("Particle system to play when the player dies.")]
        public List<ParticleSystem> deathParticlesForPlayer = new();
        [Tooltip("Particle system to play when the player is damaged.")]
        public List<ParticleSystem> damageParticlesForPlayer = new();
        [Tooltip("Particle system to play when the enemy dies.")]
        public List<ParticleSystem> deathParticlesForEnemy = new();
        [Tooltip("Particle system to play when the enemy is damaged.")]
        public List<ParticleSystem> damageParticlesForEnemy = new();

        #endregion

        #region Private Fields

        private static float lastDamageTime = -Mathf.Infinity; // Last time damage was dealt across all instances

        bool currentlyDamagingLocalPlayer = false;
        private Dictionary<EnemyAI, bool> enemyCoroutineStatus = new Dictionary<EnemyAI, bool>();
        private Dictionary<PlayerControllerB, AudioSource> playerClosestAudioSources = new Dictionary<PlayerControllerB, AudioSource>();
        private Dictionary<EnemyAI, AudioSource> enemyClosestAudioSources = new Dictionary<EnemyAI, AudioSource>();

        #endregion
        private void OnEnable()
        {
            Debug.Log($"[{gameObject.name}] BetterCooldownTrigger enabled");
            StartCoroutine(ManageDamageTimer());
        }

        private IEnumerator ManageDamageTimer()
        {
            if (damageDuration <= 0f)
            {
                Debug.Log($"[{gameObject.name}] Damage duration is 0 or negative, not disabling");
                yield break;
            }
            Debug.Log($"[{gameObject.name}] Starting damage timer for {damageDuration} seconds");
            yield return new WaitForSeconds(damageDuration);
            Debug.Log($"[{gameObject.name}] Damage duration expired, disabling GameObject");
            gameObject.SetActive(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"[{gameObject.name}] OnTriggerEnter called with {other.name}");
            if (!enabledScript)
            {
                Debug.Log($"[{gameObject.name}] Script is disabled, ignoring trigger enter");
                return;
            }

            if (other.CompareTag("Player") && other.TryGetComponent<PlayerControllerB>(out PlayerControllerB player) && player == GameNetworkManager.Instance.localPlayerController && !player.isPlayerDead)
            {
                Debug.Log($"[{gameObject.name}] Local player {player.name} entered trigger");
                if (!currentlyDamagingLocalPlayer)
                {
                    Debug.Log($"[{gameObject.name}] Starting to damage local player");
                    currentlyDamagingLocalPlayer = true;
                    if (damageAudioSources != null && damageAudioSources.Count > 0)
                    {
                        playerClosestAudioSources[player] = GetClosestAudioSource(player.transform);
                        Debug.Log($"[{gameObject.name}] Set closest audio source for player");
                    }
                    StartCoroutine(DamageLocalPlayerCoroutine());
                }
            }
            else if (triggerForEnemies)
            {
                Transform? parent = TryFindRoot(other.transform);
                if (parent != null && parent.TryGetComponent<EnemyAI>(out EnemyAI enemy) && !enemy.isEnemyDead && enemy.enemyType.enemyName != "Redwood Titan")
                {
                    Debug.Log($"[{gameObject.name}] Enemy {enemy.enemyType.enemyName} entered trigger");
                    if (!enemyCoroutineStatus.ContainsKey(enemy))
                    {
                        Debug.Log($"[{gameObject.name}] Starting to damage enemy {enemy.enemyType.enemyName}");
                        enemyCoroutineStatus[enemy] = true;
                        if (damageAudioSources != null && damageAudioSources.Count > 0)
                        {
                            enemyClosestAudioSources[enemy] = GetClosestAudioSource(enemy.transform);
                            Debug.Log($"[{gameObject.name}] Set closest audio source for enemy");
                        }
                        StartCoroutine(DamageEnemyCoroutine(enemy));
                    }
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            Debug.Log($"[{gameObject.name}] OnTriggerExit called with {other.name}");
            if (!enabledScript)
            {
                Debug.Log($"[{gameObject.name}] Script is disabled, ignoring trigger exit");
                return;
            }
            if (!canThingExit)
            {
                Debug.Log($"[{gameObject.name}] canThingExit is false, ignoring trigger exit");
                return;
            }

            if (other.CompareTag("Player") && other.TryGetComponent<PlayerControllerB>(out PlayerControllerB player) && player == GameNetworkManager.Instance.localPlayerController && !player.isPlayerDead)
            {
                Debug.Log($"[{gameObject.name}] Local player {player.name} exited trigger");
                currentlyDamagingLocalPlayer = false;
                playerClosestAudioSources.Remove(player);
                Debug.Log($"[{gameObject.name}] Stopped damaging local player and removed audio source");
            }
            else if (triggerForEnemies)
            {
                Transform? parent = TryFindRoot(other.transform);
                if (parent != null && parent.TryGetComponent<EnemyAI>(out EnemyAI enemy) && !enemy.isEnemyDead)
                {
                    Debug.Log($"[{gameObject.name}] Enemy {enemy.enemyType.enemyName} exited trigger");
                    enemyCoroutineStatus[enemy] = false;
                    enemyClosestAudioSources.Remove(enemy);
                    Debug.Log($"[{gameObject.name}] Stopped damaging enemy and removed audio source");
                }
            }
        }

        private IEnumerator DamageLocalPlayerCoroutine()
        {
            Debug.Log($"[{gameObject.name}] Started DamageLocalPlayerCoroutine");
            while (currentlyDamagingLocalPlayer)
            {
                if (sharedCooldown && Time.time < lastDamageTime + damageIntervalForPlayers)
                {
                    Debug.Log($"[{gameObject.name}] Waiting for shared cooldown, time left: {lastDamageTime + damageIntervalForPlayers - Time.time}");
                    yield return null;
                    continue;
                }

                lastDamageTime = Time.time;
                Debug.Log($"[{gameObject.name}] Applying damage to local player at time {Time.time}");
                ApplyDamageToLocalPlayer();
                yield return new WaitForSeconds(damageIntervalForPlayers);
            }
            Debug.Log($"[{gameObject.name}] Exited DamageLocalPlayerCoroutine");
        }
        
        private IEnumerator DamageEnemyCoroutine(EnemyAI enemy)
        {
            Debug.Log($"[{gameObject.name}] Started DamageEnemyCoroutine for {enemy.enemyType.enemyName}");
            while (enemyCoroutineStatus[enemy])
            {
                if (sharedCooldown && Time.time < lastDamageTime + damageIntervalForEnemies)
                {
                    Debug.Log($"[{gameObject.name}] Waiting for shared cooldown for enemy, time left: {lastDamageTime + damageIntervalForEnemies - Time.time}");
                    yield return null;
                    continue;
                }

                lastDamageTime = Time.time;
                Debug.Log($"[{gameObject.name}] Applying damage to enemy {enemy.enemyType.enemyName} at time {Time.time}");
                ApplyDamageToEnemy(enemy);
                yield return new WaitForSeconds(damageIntervalForEnemies);
            }
            Debug.Log($"[{gameObject.name}] Exited DamageEnemyCoroutine for {enemy.enemyType.enemyName}");
        }

        private void ApplyDamageToLocalPlayer()
        {
            PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;
            Debug.Log($"[{gameObject.name}] Applying damage to local player {player.name}");
            Vector3 calculatedForceAfterDamage = CalculateForceDirection(player, forceMagnitudeAfterDamage);
            Vector3 calculatedForceAfterDeath = CalculateForceDirection(player, forceMagnitudeAfterDeath);

            Debug.Log($"[{gameObject.name}] Calculated force after damage: {calculatedForceAfterDamage}, after death: {calculatedForceAfterDeath}");
            player.DamagePlayer(damageToDealForPlayers, playDefaultPlayerDamageSFX, true, causeOfDeath, (int)deathAnimation, false, calculatedForceAfterDeath);
            Debug.Log($"[{gameObject.name}] Damage applied to player, amount: {damageToDealForPlayers}");

            PlayDamageSound(player.transform, playerClosestAudioSources.ContainsKey(player) ? playerClosestAudioSources[player] : null);
            
            if (teleportParticles)
            {
                Debug.Log($"[{gameObject.name}] Teleporting particles to player position");
                foreach (ParticleSystem? particle in damageParticlesForPlayer)
                {
                    if (particle != null) particle.transform.position = player.transform.position;
                }
                foreach (ParticleSystem? particle in deathParticlesForPlayer)
                {
                    if (particle != null) particle.transform.position = player.transform.position;
                }
            }

            if (!player.isPlayerDead)
            {
                Debug.Log($"[{gameObject.name}] Player is not dead, applying external force");
                player.externalForces += calculatedForceAfterDamage;
            }
            else
            {
                Debug.Log($"[{gameObject.name}] Player is dead, spawning death prefab if available");
                if (deathPrefabForPlayer != null && deathPrefabForPlayer.GetComponent<NetworkObject>() != null)
                {
                    SpawnDeathPrefabServerRpc(player.transform.position, player.transform.rotation, true);
                }
                else if (deathPrefabForPlayer != null)
                {
                    Instantiate(deathPrefabForPlayer, player.transform.position, player.transform.rotation);
                    currentlyDamagingLocalPlayer = false;
                    playerClosestAudioSources.Remove(player);
                }
            }

            if (useParticleSystems)
            {
                Debug.Log($"[{gameObject.name}] Handling particle systems for player");
                HandleParticleSystemStuffServerRpc(player.transform.position, true, player.isPlayerDead);
            }
        }

        private void ApplyDamageToEnemy(EnemyAI enemy)
        {
            Debug.Log($"[{gameObject.name}] Applying damage to {enemy.enemyType.enemyName}, amount: {damageToDealForEnemies}");
            enemy.HitEnemy(damageToDealForEnemies, null, false, -1);
            PlayDamageSound(enemy.transform, enemyClosestAudioSources.ContainsKey(enemy) ? enemyClosestAudioSources[enemy] : null);

            if (enemy.isEnemyDead)
            {
                Debug.Log($"[{gameObject.name}] Enemy {enemy.enemyType.enemyName} is dead, spawning death prefab if available");
                if (deathPrefabForEnemy != null && deathPrefabForEnemy.GetComponent<NetworkObject>() != null)
                {
                    SpawnDeathPrefabServerRpc(enemy.transform.position, enemy.transform.rotation, false);
                }
                else if (deathPrefabForEnemy != null)
                {
                    Instantiate(deathPrefabForEnemy, enemy.transform.position, enemy.transform.rotation);
                }
                enemyCoroutineStatus[enemy] = false;
                enemyClosestAudioSources.Remove(enemy);
            }

            if (useParticleSystems)
            {
                Debug.Log($"[{gameObject.name}] Handling particle systems for enemy");
                HandleParticleSystemStuffServerRpc(enemy.transform.position, false, enemy.isEnemyDead);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void HandleParticleSystemStuffServerRpc(Vector3 position, bool forPlayer, bool isDead)
        {
            Debug.Log($"[{gameObject.name}] ServerRpc: Handling particle system stuff for player: {forPlayer}, isDead: {isDead}");
            HandleParticleSystemStuffClientRpc(position, forPlayer, isDead);
        }

        [ClientRpc]
        private void HandleParticleSystemStuffClientRpc(Vector3 position, bool forPlayer, bool isDead) {
            Debug.Log($"[{gameObject.name}] ClientRpc: Handling particle system stuff for player: {forPlayer}, isDead: {isDead}");
            if (teleportParticles) {
                if (forPlayer) {
                    foreach (ParticleSystem? particle in damageParticlesForPlayer) {
                        if (particle != null) particle.transform.position = position;
                    }
                    foreach (ParticleSystem? particle in deathParticlesForPlayer) {
                        if (particle != null) particle.transform.position = position;
                    }  
                } else {
                    foreach (ParticleSystem? particle in damageParticlesForEnemy) {
                        if (particle != null) particle.transform.position = position;
                    }
                    foreach (ParticleSystem? particle in deathParticlesForEnemy) {
                        if (particle != null) particle.transform.position = position;
                    }
                }
            }

            if (forPlayer) {
                if (!isDead && damageParticlesForPlayer.Count > 0) {
                    var particleSystem = damageParticlesForPlayer[Random.Range(0, damageParticlesForPlayer.Count)];
                    particleSystem.Play();
                } else if (isDead && deathParticlesForPlayer.Count > 0) {
                    var particleSystem = deathParticlesForPlayer[Random.Range(0, deathParticlesForPlayer.Count)];
                    particleSystem.Play();
                }
            } else {
                if (!isDead) {
                    var particleSystem = damageParticlesForEnemy[Random.Range(0, damageParticlesForEnemy.Count)];
                    particleSystem.Play();
                } else {
                    var particleSystem = deathParticlesForEnemy[Random.Range(0, deathParticlesForEnemy.Count)];
                    particleSystem.Play();
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void SpawnDeathPrefabServerRpc(Vector3 position, Quaternion rotation, bool forPlayer)
        {
            Debug.Log($"[{gameObject.name}] ServerRpc: Spawning death prefab for player: {forPlayer} at position {position}");
            if (forPlayer)
            {
                Instantiate(deathPrefabForPlayer, position, rotation);
                deathPrefabForPlayer?.GetComponent<NetworkObject>().Spawn();
            }
            else
            {
                Instantiate(deathPrefabForEnemy, position, rotation);
                deathPrefabForEnemy?.GetComponent<NetworkObject>().Spawn();
            }
        }


        private Vector3 CalculateForceDirection(PlayerControllerB player, float baseForce)
        {
            Vector3 forceDirectionVector = Vector3.zero;

            switch (forceDirection)
            {
                case ForceDirection.Forward:
                    forceDirectionVector = forceDirectionFromThisObject ? transform.forward : player.transform.forward;
                    break;
                case ForceDirection.Backward:
                    forceDirectionVector = forceDirectionFromThisObject ? -transform.forward : -player.transform.forward;
                    break;
                case ForceDirection.Up:
                    forceDirectionVector = Vector3.up;
                    break;
                case ForceDirection.Down:
                    forceDirectionVector = Vector3.down;
                    break;
                case ForceDirection.Left:
                    forceDirectionVector = forceDirectionFromThisObject ? -transform.right : -player.transform.right;
                    break;
                case ForceDirection.Right:
                    forceDirectionVector = forceDirectionFromThisObject ? transform.right : player.transform.right;
                    break;
                case ForceDirection.Center:
                    forceDirectionVector = forceDirectionFromThisObject ? (player.transform.position - transform.position).normalized : (transform.position - player.transform.position).normalized;
                    break;
            }

            Debug.Log($"[{gameObject.name}] Calculated force direction: {forceDirectionVector}, magnitude: {baseForce}");
            return forceDirectionVector.normalized * baseForce;
        }

        private void PlayDamageSound(Transform targetTransform, AudioSource? audioSource)
        {
            Debug.Log($"[{gameObject.name}] Playing damage sound at {targetTransform.position}");
            if (damageClip != null && audioSource != null)
            {
                if (soundAttractsDogs)
                {
                    Debug.Log($"[{gameObject.name}] Sound attracts dogs, playing audible noise");
                    RoundManager.Instance.PlayAudibleNoise(audioSource.transform.position, audioSource.maxDistance, audioSource.volume, 0, false, 0);
                }

                Debug.Log($"[{gameObject.name}] Transmitting one-shot audio");
                WalkieTalkie.TransmitOneShotAudio(audioSource, damageClip[Random.Range(0, damageClip.Count)], audioSource.volume);
                RoundManager.PlayRandomClip(audioSource, damageClip.ToArray(), true, audioSource.volume, 0, damageClip.Count);
            }
        }

        private AudioSource GetClosestAudioSource(Transform targetTransform)
        {
            Debug.Log($"[{gameObject.name}] Getting closest audio source for {targetTransform.name}");
            AudioSource closest = damageAudioSources![0];
            float closestDistance = Vector3.Distance(closest.transform.position, targetTransform.position);

            foreach (AudioSource source in damageAudioSources)
            {
                float distance = Vector3.Distance(source.transform.position, targetTransform.position);
                if (distance < closestDistance)
                {
                    closest = source;
                    closestDistance = distance;
                }
            }

            return closest;
        }

        public void OnDisable()
        {
            Debug.Log($"[{gameObject.name}] BetterCooldownTrigger disabled");
            StopAllCoroutines();
            currentlyDamagingLocalPlayer = false;
            enemyCoroutineStatus.Clear();
            playerClosestAudioSources.Clear();
            enemyClosestAudioSources.Clear();
        }

        public static Transform? TryFindRoot(Transform child)
        {
            Transform current = child;
            while (current != null)
            {
                if (current.GetComponent<NetworkObject>() != null)
                {
                    return current;
                }
                current = current.transform.parent;
            }
            return null;
        }
    }

    public static class TerrainInstancedToggle
    {
        public static void ToggleTerrainInstancing(bool enable)
        {
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                terrain.heightmapPixelError = enable ? 80 : 5;
                terrain.drawInstanced = enable;
            }
        }

    }
}


