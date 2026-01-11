using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Client-side prediction for local player movement.
/// Applies input immediately (no network delay) and stores prediction history for reconciliation.
/// ONLY for local player - remote players use StateInterpolator only.
/// </summary>
public class ClientPredictor : MonoBehaviour
{
    #region Constants
    private const string c_LogPrefix = "[ClientPredictor]";
    private const int c_MaxHistorySize = 30; // Max inputs to keep in history
    private const float c_DefaultCorrectionThreshold = 0.5f; // Units before correction
    #endregion

    #region Structs
    /// <summary>
    /// Snapshot of an input command with sequence number for reconciliation.
    /// </summary>
    [System.Serializable]
    public struct InputSnapshot
    {
        public int sequence;
        public float moveX;
        public float moveY;
        public float timestamp;
        public Vector3 predictedPosition; // Position after this input was applied
    }
    #endregion

    #region Private Fields
    [Header("Settings")]
    [SerializeField, Range(0.1f, 2f)] private float m_CorrectionThreshold = c_DefaultCorrectionThreshold;
    [SerializeField] private bool m_EnableLogging = false;
    [SerializeField] private bool m_SmoothCorrections = true;
    [SerializeField, Range(5f, 30f)] private float m_CorrectionSpeed = 15f;
    
    // Input history for reconciliation (use List to avoid GC from Queue.ToArray)
    private List<InputSnapshot> m_InputHistory = new List<InputSnapshot>();
    private int m_PredictionSequence = 0;
    
    // Position tracking
    private Vector3 m_PredictedPosition;
    private Vector3 m_ServerPosition;
    private Vector3 m_CorrectionTarget;
    private bool m_IsCorrecting;
    
    // Component references
    private Rigidbody2D m_Rigidbody;
    private StatsManager m_StatsManager;
    #endregion

    #region Public Properties
    /// <summary>
    /// Get the next sequence number for input.
    /// </summary>
    public int NextSequence => m_PredictionSequence;
    
    /// <summary>
    /// Get current predicted position.
    /// </summary>
    public Vector3 PredictedPosition => m_PredictedPosition;
    
    /// <summary>
    /// Get last known server position.
    /// </summary>
    public Vector3 ServerPosition => m_ServerPosition;
    
    /// <summary>
    /// Is currently correcting position.
    /// </summary>
    public bool IsCorrecting => m_IsCorrecting;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        m_Rigidbody = GetComponent<Rigidbody2D>();
        m_StatsManager = GetComponent<StatsManager>();
        m_PredictedPosition = transform.position;
        m_ServerPosition = transform.position;
    }

    private void Update()
    {
        // Smooth correction if needed
        if (m_IsCorrecting && m_SmoothCorrections)
        {
            ApplySmoothCorrection();
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Get the next sequence number and increment counter.
    /// Call this when preparing input to send.
    /// </summary>
    public int GetNextSequence()
    {
        return m_PredictionSequence++;
    }

    /// <summary>
    /// Apply input immediately (prediction) and store in history.
    /// Call this in FixedUpdate after capturing input.
    /// </summary>
    /// <param name="moveX">Horizontal input (-1 to 1)</param>
    /// <param name="moveY">Vertical input (-1 to 1)</param>
    /// <param name="sequence">Sequence number from GetNextSequence()</param>
    public void ApplyInput(float moveX, float moveY, int sequence)
    {
        // Predict movement locally (same physics as server)
        PredictMovement(moveX, moveY);
        
        // Store input snapshot for reconciliation
        var snapshot = new InputSnapshot
        {
            sequence = sequence,
            moveX = moveX,
            moveY = moveY,
            timestamp = Time.time,
            predictedPosition = m_PredictedPosition
        };
        
        m_InputHistory.Add(snapshot);
        
        // Keep history size reasonable (remove oldest)
        while (m_InputHistory.Count > c_MaxHistorySize)
        {
            m_InputHistory.RemoveAt(0);
        }
        
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Input applied: seq={sequence}, move=({moveX:F2}, {moveY:F2}), pos={m_PredictedPosition}");
        }
    }

    /// <summary>
    /// Reconcile with authoritative server state.
    /// Call this when receiving state update from server.
    /// </summary>
    /// <param name="serverX">Server X position</param>
    /// <param name="serverY">Server Y position</param>
    /// <param name="confirmedSequence">Last input sequence confirmed by server</param>
    public void Reconcile(float serverX, float serverY, int confirmedSequence)
    {
        m_ServerPosition = new Vector3(serverX, serverY, 0);
        
        // Remove confirmed inputs from history (remove from front)
        int removeCount = 0;
        for (int i = 0; i < m_InputHistory.Count; i++)
        {
            if (m_InputHistory[i].sequence <= confirmedSequence)
            {
                removeCount++;
            }
            else
            {
                break;
            }
        }
        if (removeCount > 0)
        {
            m_InputHistory.RemoveRange(0, removeCount);
        }
        
        // Check if correction is needed
        float distance = Vector3.Distance(m_PredictedPosition, m_ServerPosition);
        
        if (distance > m_CorrectionThreshold)
        {
            if (m_EnableLogging)
            {
                Debug.Log($"{c_LogPrefix} Correction needed: predicted={m_PredictedPosition}, server={m_ServerPosition}, diff={distance:F3}");
            }
            
            // Start correction
            CorrectPosition(m_ServerPosition);
            
            // Re-apply unconfirmed inputs
            ReplayUnconfirmedInputs();
        }
        else if (m_EnableLogging && distance > 0.1f)
        {
            Debug.Log($"{c_LogPrefix} Minor drift: {distance:F3} units (within threshold)");
        }
    }

    /// <summary>
    /// Force set position (for teleport, respawn, etc).
    /// Clears prediction history.
    /// </summary>
    public void ForcePosition(Vector3 position)
    {
        m_PredictedPosition = position;
        m_ServerPosition = position;
        m_CorrectionTarget = position;
        m_IsCorrecting = false;
        transform.position = position;
        
        // Clear history since we've teleported
        m_InputHistory.Clear();
        
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Position forced to {position}");
        }
    }

    /// <summary>
    /// Reset prediction state (for respawn or scene change).
    /// </summary>
    public void Reset()
    {
        m_InputHistory.Clear();
        m_PredictionSequence = 0;
        m_PredictedPosition = transform.position;
        m_ServerPosition = transform.position;
        m_IsCorrecting = false;
        
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Prediction state reset");
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Predict movement based on input (same physics as server).
    /// </summary>
    private void PredictMovement(float moveX, float moveY)
    {
        // Get movement speed from stats
        float speed = 5f; // Default speed
        if (m_StatsManager != null)
        {
            speed = m_StatsManager.speed;
        }
        
        // Calculate velocity
        Vector2 moveVector = new Vector2(moveX, moveY).normalized;
        
        // ALWAYS apply movement velocity - don't block during correction
        // Correction happens via position blending in Update(), not velocity
        if (m_Rigidbody != null)
        {
            m_Rigidbody.velocity = moveVector * speed;
        }
        
        // Update predicted position
        m_PredictedPosition = transform.position;
    }

    /// <summary>
    /// Start correcting position toward server position.
    /// </summary>
    private void CorrectPosition(Vector3 serverPosition)
    {
        m_CorrectionTarget = serverPosition;
        
        if (m_SmoothCorrections)
        {
            m_IsCorrecting = true;
        }
        else
        {
            // Instant correction
            transform.position = serverPosition;
            m_PredictedPosition = serverPosition;
        }
    }

    /// <summary>
    /// Apply smooth correction over time.
    /// </summary>
    private void ApplySmoothCorrection()
    {
        float distance = Vector3.Distance(transform.position, m_CorrectionTarget);
        
        if (distance < 0.01f)
        {
            // Close enough, finish correction
            transform.position = m_CorrectionTarget;
            m_PredictedPosition = m_CorrectionTarget;
            m_IsCorrecting = false;
            return;
        }
        
        // Lerp toward target
        transform.position = Vector3.Lerp(
            transform.position,
            m_CorrectionTarget,
            m_CorrectionSpeed * Time.deltaTime
        );
        
        m_PredictedPosition = transform.position;
    }

    /// <summary>
    /// Re-apply inputs that haven't been confirmed by server.
    /// Called after position correction.
    /// </summary>
    private void ReplayUnconfirmedInputs()
    {
        if (m_InputHistory.Count == 0)
            return;
        
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Replaying {m_InputHistory.Count} unconfirmed inputs");
        }
        
        // Re-apply each unconfirmed input
        foreach (var input in m_InputHistory)
        {
            PredictMovement(input.moveX, input.moveY);
        }
    }
    #endregion
}

