using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages remote player instances in a multiplayer session.
/// Creates, updates, and destroys remote player GameObjects.
/// </summary>
public class RemotePlayerManager : MonoBehaviour
{
    #region Constants
    private const string c_LogPrefix = "[RemotePlayerManager]";
    #endregion

    #region Private Fields
    [Header("Prefabs")]
    [SerializeField] private GameObject m_RemotePlayerLancerPrefab;
    [SerializeField] private GameObject m_RemotePlayerWarriousPrefab;
    [SerializeField] private GameObject m_DefaultRemotePlayerPrefab;
    
    [Header("Settings")]
    [SerializeField] private Transform m_RemotePlayerContainer;
    [SerializeField] private bool m_EnableLogging = true;
    
    // Remote player instances (keyed by player ID)
    private Dictionary<string, RemotePlayerSync> m_RemotePlayers = new Dictionary<string, RemotePlayerSync>();
    
    // Local player ID (to exclude from remote players)
    private string m_LocalPlayerId;
    #endregion

    #region Public Properties
    public static RemotePlayerManager Instance { get; private set; }
    
    /// <summary>
    /// Get all remote player instances.
    /// </summary>
    public IReadOnlyDictionary<string, RemotePlayerSync> RemotePlayers => m_RemotePlayers;
    
    /// <summary>
    /// Number of remote players.
    /// </summary>
    public int RemotePlayerCount => m_RemotePlayers.Count;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        
        // Create container if not assigned
        if (m_RemotePlayerContainer == null)
        {
            var container = new GameObject("RemotePlayers");
            m_RemotePlayerContainer = container.transform;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Set the local player ID to exclude from remote players.
    /// </summary>
    public void SetLocalPlayerId(string playerId)
    {
        m_LocalPlayerId = playerId;
        
        // Remove existing if it was added as remote
        if (m_RemotePlayers.ContainsKey(playerId))
        {
            RemoveRemotePlayer(playerId);
        }
    }

    /// <summary>
    /// Update remote players from server state snapshot.
    /// Creates new remote players and updates existing ones.
    /// </summary>
    /// <param name="players">List of player snapshots from server</param>
    /// <param name="serverTime">Server timestamp</param>
    /// <param name="sequence">Server sequence number</param>
    public void UpdateFromSnapshot(List<RemotePlayerSnapshot> players, float serverTime, int sequence)
    {
        if (players == null)
            return;
        
        var activePlayers = new HashSet<string>();
        
        foreach (var player in players)
        {
            // Skip local player
            if (player.id == m_LocalPlayerId)
                continue;
            
            activePlayers.Add(player.id);
            
            // Get or create remote player
            if (!m_RemotePlayers.TryGetValue(player.id, out var remotePlayer))
            {
                remotePlayer = CreateRemotePlayer(player.id, player.name, player.characterType);
            }
            
            // Update state
            if (remotePlayer != null)
            {
                remotePlayer.UpdateState(
                    player.x, player.y,
                    player.hp, player.maxHp,
                    player.status,
                    serverTime, sequence
                );
            }
        }
        
        // Remove players that are no longer in snapshot
        var playersToRemove = new List<string>();
        foreach (var kvp in m_RemotePlayers)
        {
            if (!activePlayers.Contains(kvp.Key))
            {
                playersToRemove.Add(kvp.Key);
            }
        }
        
        foreach (var id in playersToRemove)
        {
            RemoveRemotePlayer(id);
        }
    }

    /// <summary>
    /// Handle player joined event from SignalR.
    /// </summary>
    public void OnPlayerJoined(string playerId, string characterType)
    {
        if (playerId == m_LocalPlayerId)
            return;
        
        if (!m_RemotePlayers.ContainsKey(playerId))
        {
            CreateRemotePlayer(playerId, $"Player_{playerId.Substring(0, 4)}", characterType);
            
            if (m_EnableLogging)
            {
                Debug.Log($"{c_LogPrefix} Player joined: {playerId}");
            }
        }
    }

    /// <summary>
    /// Handle player left event from SignalR.
    /// </summary>
    public void OnPlayerLeft(string playerId)
    {
        if (m_RemotePlayers.ContainsKey(playerId))
        {
            RemoveRemotePlayer(playerId);
            
            if (m_EnableLogging)
            {
                Debug.Log($"{c_LogPrefix} Player left: {playerId}");
            }
        }
    }

    /// <summary>
    /// Get a specific remote player.
    /// </summary>
    public RemotePlayerSync GetRemotePlayer(string playerId)
    {
        m_RemotePlayers.TryGetValue(playerId, out var player);
        return player;
    }

    /// <summary>
    /// Clear all remote players.
    /// </summary>
    public void ClearAll()
    {
        foreach (var kvp in m_RemotePlayers)
        {
            if (kvp.Value != null && kvp.Value.gameObject != null)
            {
                Destroy(kvp.Value.gameObject);
            }
        }
        
        m_RemotePlayers.Clear();
        
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} All remote players cleared");
        }
    }
    #endregion

    #region Private Methods
    private RemotePlayerSync CreateRemotePlayer(string playerId, string playerName, string characterType)
    {
        // Select prefab based on character type
        GameObject prefab = GetPrefabForCharacterType(characterType);
        
        if (prefab == null)
        {
            Debug.LogError($"{c_LogPrefix} No prefab found for character type: {characterType}");
            return null;
        }
        
        // Instantiate
        var instance = Instantiate(prefab, m_RemotePlayerContainer);
        instance.name = $"RemotePlayer_{playerId.Substring(0, 8)}";
        
        // Set tag to differentiate from local player
        instance.tag = "RemotePlayer";
        
        // Get or add RemotePlayerSync
        var sync = instance.GetComponent<RemotePlayerSync>();
        if (sync == null)
        {
            sync = instance.AddComponent<RemotePlayerSync>();
        }
        
        // Initialize
        sync.Initialize(playerId, playerName, characterType);
        
        // Add to dictionary
        m_RemotePlayers[playerId] = sync;
        
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Created remote player: {playerId} ({characterType})");
        }
        
        return sync;
    }

    private void RemoveRemotePlayer(string playerId)
    {
        if (m_RemotePlayers.TryGetValue(playerId, out var player))
        {
            if (player != null && player.gameObject != null)
            {
                Destroy(player.gameObject);
            }
            
            m_RemotePlayers.Remove(playerId);
            
            if (m_EnableLogging)
            {
                Debug.Log($"{c_LogPrefix} Removed remote player: {playerId}");
            }
        }
    }

    private GameObject GetPrefabForCharacterType(string characterType)
    {
        switch (characterType?.ToLower())
        {
            case "lancer":
                return m_RemotePlayerLancerPrefab ?? m_DefaultRemotePlayerPrefab;
            case "warrious":
            case "warrior":
                return m_RemotePlayerWarriousPrefab ?? m_DefaultRemotePlayerPrefab;
            default:
                return m_DefaultRemotePlayerPrefab;
        }
    }
    #endregion
}

/// <summary>
/// Extended player snapshot for remote player management.
/// Uses same naming convention as NetClient PlayerSnapshot.
/// </summary>
[System.Serializable]
public class RemotePlayerSnapshot
{
    public string id;
    public string name;
    public string characterType;
    public float x;
    public float y;
    public int hp;
    public int maxHp;
    public int level;
    public string status;
    public int lastConfirmedInputSequence;
    
    /// <summary>
    /// Create from SignalR player snapshot.
    /// </summary>
    public static RemotePlayerSnapshot FromSignalR(SignalRPlayerSnapshot signalR)
    {
        return new RemotePlayerSnapshot
        {
            id = signalR.id,
            name = signalR.name,
            characterType = signalR.characterType,
            x = signalR.x,
            y = signalR.y,
            hp = signalR.hp,
            maxHp = signalR.maxHp,
            level = signalR.level,
            status = signalR.status,
            lastConfirmedInputSequence = signalR.lastConfirmedInputSequence
        };
    }
}

