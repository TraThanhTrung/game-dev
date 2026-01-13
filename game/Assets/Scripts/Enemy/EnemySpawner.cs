using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;

/// <summary>
/// Handles spawning and managing enemy GameObjects based on server state.
/// Subscribes to state polling via ServerStateApplier and spawns/updates enemies.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    #region Constants
    private const string c_EnemyPrefabPath = "Prefabs/Enemies/"; // Path relative to Assets folder
    #endregion

    #region Private Fields
    [SerializeField] private bool m_EnableLogging = true;
    [SerializeField] private float m_PositionLerpSpeed = 10f; // Speed for interpolating enemy positions

    private Dictionary<Guid, GameObject> m_SpawnedEnemies = new Dictionary<Guid, GameObject>();
    private Dictionary<Guid, EnemyState> m_EnemyStates = new Dictionary<Guid, EnemyState>();
    private Dictionary<Guid, Vector3> m_TargetPositions = new Dictionary<Guid, Vector3>(); // Target positions for interpolation
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (m_EnableLogging)
            Debug.Log("[EnemySpawner] Awake called");
    }

    private void Start()
    {
        if (m_EnableLogging)
            Debug.Log("[EnemySpawner] Start called");
    }

    private void OnDestroy()
    {
        // Clean up all spawned enemies
        foreach (var enemy in m_SpawnedEnemies.Values)
        {
            if (enemy != null)
                Destroy(enemy);
        }
        m_SpawnedEnemies.Clear();
        m_EnemyStates.Clear();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Called by ServerStateApplier when new state is received from server.
    /// </summary>
    public void OnStateReceived(StateResponse state)
    {
        if (state == null || state.enemies == null)
        {
            // Clear all enemies if state is null (disconnected)
            if (state == null)
            {
                ClearAllEnemies();
            }
            return;
        }

        // Log enemy count from server (only occasionally to avoid spam)
        if (m_EnableLogging && Time.frameCount % 300 == 0)
        {
            Debug.Log($"[EnemySpawner] Server state: {state.enemies.Length} enemies, local: {m_SpawnedEnemies.Count} enemies");
        }

        var activeEnemyIds = new HashSet<Guid>();

        foreach (var enemySnapshot in state.enemies)
        {
            if (string.IsNullOrEmpty(enemySnapshot.id) || !Guid.TryParse(enemySnapshot.id, out var enemyId))
            {
                if (m_EnableLogging)
                    Debug.LogWarning($"[EnemySpawner] Invalid enemy ID: {enemySnapshot.id}");
                continue;
            }

            activeEnemyIds.Add(enemyId);

            // Check if enemy exists and is still alive
            if (!m_SpawnedEnemies.ContainsKey(enemyId))
            {
                // Spawn new enemy (or respawn after death)
                SpawnEnemy(enemySnapshot);
            }
            else
            {
                // Check if enemy GameObject still exists (might have been destroyed)
                var existingEnemy = m_SpawnedEnemies[enemyId];
                if (existingEnemy == null)
                {
                    // Enemy was destroyed but still in dictionary, clean up and respawn if alive
                    if (m_EnableLogging)
                        Debug.Log($"[EnemySpawner] Enemy {enemyId} GameObject was destroyed, checking respawn...");

                    m_SpawnedEnemies.Remove(enemyId);
                    m_EnemyStates.Remove(enemyId);

                    // If enemy is alive (respawned by server), spawn it again
                    if (enemySnapshot.hp > 0)
                    {
                        if (m_EnableLogging)
                            Debug.Log($"[EnemySpawner] Enemy {enemyId} respawned by server (HP: {enemySnapshot.hp}), spawning...");
                        SpawnEnemy(enemySnapshot);
                    }
                }
                else
                {
                    // Enemy GameObject exists, check if it respawned (HP went from 0 to > 0)
                    var enemyState = m_EnemyStates.TryGetValue(enemyId, out var previousState) ? previousState : null;
                    bool wasDead = enemyState != null && enemyState.Hp <= 0;
                    bool isNowAlive = enemySnapshot.hp > 0;

                    if (wasDead && isNowAlive)
                    {
                        // Enemy respawned: respawn at spawn position
                        if (m_EnableLogging)
                            Debug.Log($"[EnemySpawner] Enemy {enemyId} ({enemySnapshot.typeId}) respawned at ({enemySnapshot.x}, {enemySnapshot.y})");

                        // Update HP and position for respawn
                        var enemyHealth = existingEnemy.GetComponent<Enemy_Health>();
                        if (enemyHealth != null)
                        {
                            enemyHealth.currentHealth = enemySnapshot.hp;
                            enemyHealth.maxHealth = enemySnapshot.maxHp;
                        }

                        // Reset position to spawn position
                        existingEnemy.transform.position = new Vector3(enemySnapshot.x, enemySnapshot.y, 0);

                        // Update stored state
                        if (m_EnemyStates.ContainsKey(enemyId))
                        {
                            m_EnemyStates[enemyId].Hp = enemySnapshot.hp;
                            m_EnemyStates[enemyId].MaxHp = enemySnapshot.maxHp;
                        }
                    }
                    else if (enemySnapshot.hp > 0)
                    {
                        // Update existing alive enemy (only HP, position managed by AI)
                        UpdateEnemy(enemySnapshot);
                    }
                    else
                    {
                        // Enemy is dead (HP <= 0), destroy it
                        UpdateEnemy(enemySnapshot); // This will handle death and destroy
                    }
                }
            }
        }

        // Remove dead/despawned enemies
        var toRemove = m_SpawnedEnemies.Keys.Where(id => !activeEnemyIds.Contains(id)).ToList();
        foreach (var id in toRemove)
        {
            DestroyEnemy(id);
        }
    }

    /// <summary>
    /// Clear all spawned enemies (called on disconnect).
    /// </summary>
    public void ClearAllEnemies()
    {
        if (m_EnableLogging)
            Debug.Log("[EnemySpawner] Clearing all enemies");

        foreach (var enemy in m_SpawnedEnemies.Values)
        {
            if (enemy != null)
                Destroy(enemy);
        }

        m_SpawnedEnemies.Clear();
        m_EnemyStates.Clear();
        m_TargetPositions.Clear();
    }
    #endregion

    #region Private Methods
    private void SpawnEnemy(EnemySnapshot snapshot)
    {
        if (!Guid.TryParse(snapshot.id, out var enemyId))
        {
            if (m_EnableLogging)
                Debug.LogError($"[EnemySpawner] Failed to parse enemy ID: {snapshot.id}");
            return;
        }

        // Load enemy prefab by typeId
        var prefab = LoadEnemyPrefab(snapshot.typeId);
        if (prefab == null)
        {
            if (m_EnableLogging)
                Debug.LogError($"[EnemySpawner] Prefab not found for typeId: {snapshot.typeId}");
            return;
        }

        // Instantiate at position from snapshot
        var position = new Vector3(snapshot.x, snapshot.y, 0);
        var enemyObject = Instantiate(prefab, position, Quaternion.identity);

        if (enemyObject == null)
        {
            if (m_EnableLogging)
                Debug.LogError($"[EnemySpawner] Failed to instantiate enemy prefab: {snapshot.typeId}");
            return;
        }

        // Add EnemyIdentity component to store server-assigned ID
        var enemyIdentity = enemyObject.GetComponent<EnemyIdentity>();
        if (enemyIdentity == null)
        {
            enemyIdentity = enemyObject.AddComponent<EnemyIdentity>();
        }
        enemyIdentity.Initialize(enemyId, snapshot.typeId);

        // Set enemy type ID in Enemy_Health component (must be done before Start() is called)
        var enemyHealth = enemyObject.GetComponent<Enemy_Health>();
        if (enemyHealth != null)
        {
            // Set typeId first (this will call LoadFromConfig)
            enemyHealth.SetEnemyTypeId(snapshot.typeId);

            // Then set HP from snapshot (overrides the maxHealth set by SetEnemyTypeId)
            enemyHealth.currentHealth = snapshot.hp;
            enemyHealth.maxHealth = snapshot.maxHp;
        }

        // Store reference and state
        m_SpawnedEnemies[enemyId] = enemyObject;
        m_EnemyStates[enemyId] = new EnemyState
        {
            Id = enemyId,
            TypeId = snapshot.typeId,
            Hp = snapshot.hp,
            MaxHp = snapshot.maxHp
        };

        // Store target position for interpolation
        m_TargetPositions[enemyId] = position;

        // Add StateInterpolator component for smooth position sync in multiplayer
        if (NetClient.Instance != null && NetClient.Instance.IsConnected)
        {
            var interpolator = enemyObject.GetComponent<StateInterpolator>();
            if (interpolator == null)
            {
                interpolator = enemyObject.AddComponent<StateInterpolator>();
            }
            // Force initial position
            interpolator.ForcePosition(position);
        }

        if (m_EnableLogging)
            Debug.Log($"[EnemySpawner] Spawned enemy {enemyId} ({snapshot.typeId}) at ({snapshot.x}, {snapshot.y})");
    }

    private void UpdateEnemy(EnemySnapshot snapshot)
    {
        if (!Guid.TryParse(snapshot.id, out var enemyId))
            return;

        if (!m_SpawnedEnemies.TryGetValue(enemyId, out var enemyObject) || enemyObject == null)
        {
            // Enemy was destroyed but still in dictionary, remove it
            m_SpawnedEnemies.Remove(enemyId);
            m_EnemyStates.Remove(enemyId);
            m_TargetPositions.Remove(enemyId);
            return;
        }

        // Check if multiplayer mode is active
        bool isMultiplayer = NetClient.Instance != null && NetClient.Instance.IsConnected;

        // Update position from server in multiplayer mode (server-authoritative)
        if (isMultiplayer)
        {
            Vector3 serverPosition = new Vector3(snapshot.x, snapshot.y, 0);

            // Update target position
            m_TargetPositions[enemyId] = serverPosition;

            // Use StateInterpolator if available for smooth movement
            var interpolator = enemyObject.GetComponent<StateInterpolator>();
            if (interpolator != null)
            {
                // Use current time as server time (polling mode doesn't have precise server timestamp)
                float serverTime = Time.time;
                int sequence = 0; // StateResponse doesn't have sequence per enemy, use 0
                interpolator.AddSnapshot(serverPosition, serverTime, sequence, snapshot.hp, snapshot.maxHp, snapshot.status);
            }
            else
            {
                // Fallback: direct position update with lerp
                Vector3 currentPos = enemyObject.transform.position;
                float distance = Vector3.Distance(currentPos, serverPosition);

                // Only update if position changed significantly (avoid micro-jitter)
                if (distance > 0.01f)
                {
                    enemyObject.transform.position = Vector3.Lerp(currentPos, serverPosition, Time.deltaTime * m_PositionLerpSpeed);
                }
            }

            // Sync animation state from server status
            var enemyMovement = enemyObject.GetComponent<Enemy_Movement>();
            if (enemyMovement != null && !string.IsNullOrEmpty(snapshot.status))
            {
                enemyMovement.SyncStateFromServer(snapshot.status);
            }
        }
        // In single-player mode, position is handled by local AI (Enemy_Movement)

        // Update HP (server-authoritative for damage/healing only)
        var enemyHealth = enemyObject.GetComponent<Enemy_Health>();
        if (enemyHealth != null)
        {
            // Only update HP if it changed (server-side damage was applied)
            if (enemyHealth.currentHealth != snapshot.hp || enemyHealth.maxHealth != snapshot.maxHp)
            {
                int oldHp = enemyHealth.currentHealth;
                enemyHealth.currentHealth = snapshot.hp;
                enemyHealth.maxHealth = snapshot.maxHp;

                // Handle death (HP dropped to 0 or below)
                if (snapshot.hp <= 0 && oldHp > 0)
                {
                    if (m_EnableLogging)
                        Debug.Log($"[EnemySpawner] Enemy {enemyId} ({snapshot.typeId}) died (HP: {oldHp} -> {snapshot.hp})");

                    // Trigger OnMonsterDefeated event before destroying (for backward compatibility)
                    // Note: Rewards are now automatically awarded by server when enemy dies
                    // but event is still triggered for any other scripts that need it
                    if (enemyHealth != null)
                    {
                        enemyHealth.TriggerDefeatEvent();
                        if (m_EnableLogging)
                            Debug.Log($"[EnemySpawner] Triggered defeat event for {enemyHealth.EnemyTypeId}");
                    }

                    // Enemy just died, destroy it immediately
                    DestroyEnemy(enemyId);
                    return; // Early return, enemy is destroyed
                }
            }
        }

        // Update stored state
        if (m_EnemyStates.ContainsKey(enemyId))
        {
            m_EnemyStates[enemyId].Hp = snapshot.hp;
            m_EnemyStates[enemyId].MaxHp = snapshot.maxHp;
        }
    }

    private void DestroyEnemy(Guid enemyId)
    {
        if (m_SpawnedEnemies.TryGetValue(enemyId, out var enemyObject))
        {
            if (enemyObject != null)
            {
                if (m_EnableLogging)
                    Debug.Log($"[EnemySpawner] Destroying enemy {enemyId}");

                Destroy(enemyObject);
            }
            m_SpawnedEnemies.Remove(enemyId);
            m_EnemyStates.Remove(enemyId);
            m_TargetPositions.Remove(enemyId);
        }
    }

    private GameObject LoadEnemyPrefab(string typeId)
    {
        if (string.IsNullOrEmpty(typeId))
            return null;

        GameObject prefab = null;

        // Try Resources folder first: Resources/Prefabs/Enemies/{typeId}
        prefab = Resources.Load<GameObject>($"Prefabs/Enemies/{typeId}");
        if (prefab != null)
            return prefab;

        // Try with capital case (Slime, Goblin, etc.) in Resources
        var capitalTypeId = char.ToUpperInvariant(typeId[0]) + typeId.Substring(1);
        prefab = Resources.Load<GameObject>($"Prefabs/Enemies/{capitalTypeId}");
        if (prefab != null)
            return prefab;

        // Fallback: Try direct path (if prefabs are in Assets/Prefabs/Enemies/ but not in Resources)
        // Note: This requires prefabs to be in a Resources folder. If not, we need to create Resources/Prefabs/Enemies/
        // For now, try without "Resources" prefix since Unity may auto-detect
        prefab = Resources.Load<GameObject>($"Enemies/{typeId}");
        if (prefab != null)
            return prefab;

        prefab = Resources.Load<GameObject>($"Enemies/{capitalTypeId}");
        if (prefab != null)
            return prefab;

        if (m_EnableLogging)
            Debug.LogWarning($"[EnemySpawner] Prefab not found for typeId: {typeId}. " +
                $"Please ensure prefabs are in Resources/Prefabs/Enemies/ or Resources/Enemies/ folder. " +
                $"Tried paths: Prefabs/Enemies/{typeId}, Prefabs/Enemies/{capitalTypeId}, Enemies/{typeId}, Enemies/{capitalTypeId}");

        return null;
    }
    #endregion

    #region Private Classes
    private class EnemyState
    {
        public Guid Id { get; set; }
        public string TypeId { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
    }
    #endregion
}
