using UnityEngine;

/// <summary>
/// Global input blocking mechanism.
/// Blocks all player input until loading is complete.
/// Attach to a persistent GameObject (e.g., GameManager or create singleton).
/// </summary>
public class InputBlocker : MonoBehaviour
{
    #region Constants
    private const string c_LogPrefix = "[InputBlocker]";
    #endregion

    #region Private Fields
    [SerializeField] private bool m_InputBlocked = true; // Start blocked by default
    [SerializeField] private bool m_EnableLogging = true;
    #endregion

    #region Public Properties
    public static InputBlocker Instance { get; private set; }
    
    /// <summary>
    /// Returns true if input is currently blocked.
    /// </summary>
    public bool IsInputBlocked => m_InputBlocked;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Initialized. Input blocked by default.");
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
    /// Block all player input.
    /// Call this when entering loading screens or cutscenes.
    /// </summary>
    public void BlockInput()
    {
        if (!m_InputBlocked)
        {
            m_InputBlocked = true;
            
            if (m_EnableLogging)
            {
                Debug.Log($"{c_LogPrefix} Input BLOCKED");
            }
        }
    }
    
    /// <summary>
    /// Unblock player input.
    /// Call this when loading is complete and game is ready.
    /// </summary>
    public void UnblockInput()
    {
        if (m_InputBlocked)
        {
            m_InputBlocked = false;
            
            if (m_EnableLogging)
            {
                Debug.Log($"{c_LogPrefix} Input UNBLOCKED - Game ready");
            }
        }
    }
    
    /// <summary>
    /// Toggle input blocking state.
    /// </summary>
    public void ToggleInput()
    {
        m_InputBlocked = !m_InputBlocked;
        
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Input {(m_InputBlocked ? "BLOCKED" : "UNBLOCKED")}");
        }
    }
    
    /// <summary>
    /// Create InputBlocker if it doesn't exist.
    /// </summary>
    public static void EnsureExists()
    {
        if (Instance == null)
        {
            var go = new GameObject("InputBlocker");
            go.AddComponent<InputBlocker>();
        }
    }
    #endregion
}

