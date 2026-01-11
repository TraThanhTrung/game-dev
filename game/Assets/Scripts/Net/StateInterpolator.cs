using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Interpolates between server state snapshots for smooth 60 FPS rendering.
/// Used for REMOTE players only. Local player uses ClientPredictor.
/// </summary>
public class StateInterpolator : MonoBehaviour
{
    #region Constants
    private const string c_LogPrefix = "[StateInterpolator]";
    private const int c_DefaultMaxBufferSize = 5;
    private const float c_DefaultInterpolationDelay = 0.1f; // 100ms
    #endregion

    #region Structs
    /// <summary>
    /// A snapshot of entity state at a specific time.
    /// </summary>
    [System.Serializable]
    public struct StateSnapshot
    {
        public Vector3 position;
        public float timestamp;
        public int sequence;
        public int hp;
        public int maxHp;
        public string status;
    }
    #endregion

    #region Private Fields
    [Header("Settings")]
    [SerializeField, Range(0.05f, 0.3f)] private float m_InterpolationDelay = c_DefaultInterpolationDelay;
    [SerializeField, Range(3, 10)] private int m_MaxBufferSize = c_DefaultMaxBufferSize;
    [SerializeField] private bool m_EnableLogging = false;
    [SerializeField] private bool m_ApplyPositionInUpdate = true;
    [SerializeField] private bool m_IsLocalPlayer = false; // Disable for local player
    
    // State buffer (use List for better performance - avoid ToArray() allocations)
    private List<StateSnapshot> m_StateBuffer = new List<StateSnapshot>();
    
    // Last interpolated values
    private Vector3 m_InterpolatedPosition;
    private int m_InterpolatedHp;
    private int m_InterpolatedMaxHp;
    private string m_InterpolatedStatus = "idle";
    
    // Time tracking
    private float m_BaseClientTime;
    private float m_BaseServerTime;
    private bool m_TimeInitialized;
    #endregion

    #region Public Properties
    /// <summary>
    /// Get the interpolated position.
    /// </summary>
    public Vector3 InterpolatedPosition => m_InterpolatedPosition;
    
    /// <summary>
    /// Get the interpolated HP.
    /// </summary>
    public int InterpolatedHp => m_InterpolatedHp;
    
    /// <summary>
    /// Get the interpolated max HP.
    /// </summary>
    public int InterpolatedMaxHp => m_InterpolatedMaxHp;
    
    /// <summary>
    /// Get the interpolated status.
    /// </summary>
    public string InterpolatedStatus => m_InterpolatedStatus;
    
    /// <summary>
    /// Current buffer size.
    /// </summary>
    public int BufferSize => m_StateBuffer.Count;
    
    /// <summary>
    /// Interpolation delay in seconds.
    /// </summary>
    public float InterpolationDelay
    {
        get => m_InterpolationDelay;
        set => m_InterpolationDelay = Mathf.Clamp(value, 0.05f, 0.3f);
    }
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        m_InterpolatedPosition = transform.position;
    }

    private void Update()
    {
        // Skip interpolation for local player (uses ClientPredictor instead)
        if (m_IsLocalPlayer)
            return;
        
        // Interpolate position each frame (60 FPS)
        if (m_StateBuffer.Count > 0 && m_ApplyPositionInUpdate)
        {
            m_InterpolatedPosition = GetInterpolatedPosition();
            transform.position = m_InterpolatedPosition;
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Add a new state snapshot to the buffer.
    /// Call this when receiving state update from server.
    /// </summary>
    /// <param name="position">Server position</param>
    /// <param name="serverTime">Server timestamp</param>
    /// <param name="sequence">Server sequence number</param>
    /// <param name="hp">Current HP (optional)</param>
    /// <param name="maxHp">Max HP (optional)</param>
    /// <param name="status">Current status (optional)</param>
    public void AddSnapshot(Vector3 position, float serverTime, int sequence, 
        int hp = -1, int maxHp = -1, string status = null)
    {
        // Skip for local player
        if (m_IsLocalPlayer)
            return;
        
        // Initialize time mapping on first snapshot
        if (!m_TimeInitialized)
        {
            m_BaseClientTime = Time.time;
            m_BaseServerTime = serverTime;
            m_TimeInitialized = true;
        }
        
        // Convert server time to client time
        float clientTime = m_BaseClientTime + (serverTime - m_BaseServerTime);
        
        var snapshot = new StateSnapshot
        {
            position = position,
            timestamp = clientTime,
            sequence = sequence,
            hp = hp >= 0 ? hp : m_InterpolatedHp,
            maxHp = maxHp >= 0 ? maxHp : m_InterpolatedMaxHp,
            status = status ?? m_InterpolatedStatus
        };
        
        m_StateBuffer.Add(snapshot);
        
        // Keep buffer size reasonable (remove oldest)
        while (m_StateBuffer.Count > m_MaxBufferSize)
        {
            m_StateBuffer.RemoveAt(0);
        }
        
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Snapshot added: seq={sequence}, pos={position}, buffer={m_StateBuffer.Count}");
        }
    }

    /// <summary>
    /// Get interpolated position at current time (with delay).
    /// </summary>
    public Vector3 GetInterpolatedPosition()
    {
        int count = m_StateBuffer.Count;
        
        if (count == 0)
        {
            return transform.position;
        }
        
        if (count == 1)
        {
            // Only one snapshot, return its position
            return m_StateBuffer[0].position;
        }
        
        // Target time (current time - interpolation delay)
        float targetTime = Time.time - m_InterpolationDelay;
        
        // Find two snapshots to interpolate between (no allocation)
        int olderIndex = -1;
        int newerIndex = -1;
        
        for (int i = 0; i < count; i++)
        {
            if (m_StateBuffer[i].timestamp <= targetTime)
            {
                olderIndex = i;
            }
            else
            {
                newerIndex = i;
                break;
            }
        }
        
        // If no older snapshot, use the first one
        if (olderIndex < 0)
        {
            return m_StateBuffer[0].position;
        }
        
        // If no newer snapshot, use the last one (extrapolate)
        if (newerIndex < 0)
        {
            // Update non-position state from latest
            UpdateNonPositionState(m_StateBuffer[count - 1]);
            return m_StateBuffer[count - 1].position;
        }
        
        var older = m_StateBuffer[olderIndex];
        var newer = m_StateBuffer[newerIndex];
        
        // Interpolate between older and newer
        float timeDiff = newer.timestamp - older.timestamp;
        float t = timeDiff > 0.0001f ? (targetTime - older.timestamp) / timeDiff : 0f;
        t = Mathf.Clamp01(t);
        
        // Update non-position state from newer snapshot
        UpdateNonPositionState(newer);
        
        return Vector3.Lerp(older.position, newer.position, t);
    }

    /// <summary>
    /// Force set position (for teleport, spawn, etc).
    /// Clears the buffer.
    /// </summary>
    public void ForcePosition(Vector3 position)
    {
        m_StateBuffer.Clear();
        m_InterpolatedPosition = position;
        transform.position = position;
        m_TimeInitialized = false;
        
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Position forced to {position}");
        }
    }

    /// <summary>
    /// Clear the state buffer.
    /// </summary>
    public void ClearBuffer()
    {
        m_StateBuffer.Clear();
        m_TimeInitialized = false;
        
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Buffer cleared");
        }
    }

    /// <summary>
    /// Get the latest snapshot in buffer.
    /// </summary>
    public StateSnapshot? GetLatestSnapshot()
    {
        int count = m_StateBuffer.Count;
        if (count == 0)
            return null;
        
        return m_StateBuffer[count - 1];
    }
    
    /// <summary>
    /// Set whether this is the local player (disables interpolation).
    /// </summary>
    public void SetIsLocalPlayer(bool isLocal)
    {
        m_IsLocalPlayer = isLocal;
        if (isLocal)
        {
            m_ApplyPositionInUpdate = false; // Don't move local player via interpolation
        }
    }
    #endregion

    #region Private Methods
    private void UpdateNonPositionState(StateSnapshot snapshot)
    {
        m_InterpolatedHp = snapshot.hp;
        m_InterpolatedMaxHp = snapshot.maxHp;
        m_InterpolatedStatus = snapshot.status;
    }
    #endregion
}

