using UnityEngine;
using TMPro;

/// <summary>
/// Synchronizes remote player state using interpolation only.
/// NO prediction - remote players cannot be controlled locally.
/// Attach this to remote player prefab instances.
/// </summary>
public class RemotePlayerSync : MonoBehaviour
{
    #region Constants
    private const string c_LogPrefix = "[RemotePlayerSync]";
    #endregion

    #region Private Fields
    [Header("References")]
    [SerializeField] private SpriteRenderer m_SpriteRenderer;
    [SerializeField] private Animator m_Animator;
    [SerializeField] private TextMeshPro m_NameText;
    [SerializeField] private Transform m_HealthBarFill;
    
    [Header("Settings")]
    [SerializeField] private bool m_EnableLogging = false;
    
    // Remote player data
    private string m_PlayerId;
    private string m_PlayerName;
    private string m_CharacterType;
    private int m_CurrentHp;
    private int m_MaxHp;
    private string m_Status;
    
    // Component references
    private StateInterpolator m_Interpolator;
    #endregion

    #region Public Properties
    public string PlayerId => m_PlayerId;
    public string PlayerName => m_PlayerName;
    public string CharacterType => m_CharacterType;
    public int CurrentHp => m_CurrentHp;
    public int MaxHp => m_MaxHp;
    public bool IsDead => m_CurrentHp <= 0;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Get or add StateInterpolator
        m_Interpolator = GetComponent<StateInterpolator>();
        if (m_Interpolator == null)
        {
            m_Interpolator = gameObject.AddComponent<StateInterpolator>();
        }
        
        // NO ClientPredictor - remote players don't predict!
    }

    private void Update()
    {
        // Update visual elements based on interpolated state
        UpdateVisuals();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Initialize remote player with basic info.
    /// </summary>
    public void Initialize(string playerId, string playerName, string characterType)
    {
        m_PlayerId = playerId;
        m_PlayerName = playerName;
        m_CharacterType = characterType;
        
        // Update name display
        if (m_NameText != null)
        {
            m_NameText.text = playerName;
        }
        
        // Set character appearance based on type
        UpdateCharacterAppearance();
        
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Initialized: id={playerId}, name={playerName}, char={characterType}");
        }
    }

    /// <summary>
    /// Update state from server snapshot.
    /// Uses StateInterpolator for smooth movement.
    /// </summary>
    /// <param name="x">Server X position</param>
    /// <param name="y">Server Y position</param>
    /// <param name="hp">Current HP</param>
    /// <param name="maxHp">Max HP</param>
    /// <param name="status">Current status</param>
    /// <param name="serverTime">Server timestamp</param>
    /// <param name="sequence">Server sequence number</param>
    public void UpdateState(float x, float y, int hp, int maxHp, string status, 
        float serverTime, int sequence)
    {
        // Add snapshot to interpolator
        if (m_Interpolator != null)
        {
            m_Interpolator.AddSnapshot(
                new Vector3(x, y, 0),
                serverTime,
                sequence,
                hp,
                maxHp,
                status
            );
        }
        else
        {
            // Fallback: direct position update
            transform.position = new Vector3(x, y, 0);
        }
        
        // Update HP and status immediately
        m_CurrentHp = hp;
        m_MaxHp = maxHp;
        m_Status = status;
    }

    /// <summary>
    /// Force position update (for spawn/teleport).
    /// </summary>
    public void ForcePosition(Vector3 position)
    {
        if (m_Interpolator != null)
        {
            m_Interpolator.ForcePosition(position);
        }
        else
        {
            transform.position = position;
        }
    }
    #endregion

    #region Private Methods
    private void UpdateVisuals()
    {
        // Update health bar
        if (m_HealthBarFill != null && m_MaxHp > 0)
        {
            float fillAmount = (float)m_CurrentHp / m_MaxHp;
            m_HealthBarFill.localScale = new Vector3(fillAmount, 1, 1);
        }
        
        // Update animator based on movement
        if (m_Animator != null && m_Interpolator != null)
        {
            var latest = m_Interpolator.GetLatestSnapshot();
            if (latest.HasValue)
            {
                // Check if moving
                float velocity = Vector3.Distance(transform.position, latest.Value.position);
                bool isMoving = velocity > 0.01f;
                
                // Set animator parameters if they exist
                m_Animator.SetBool("isMoving", isMoving);
                
                // Face direction based on movement
                if (isMoving && m_SpriteRenderer != null)
                {
                    Vector3 direction = latest.Value.position - transform.position;
                    if (Mathf.Abs(direction.x) > 0.1f)
                    {
                        m_SpriteRenderer.flipX = direction.x < 0;
                    }
                }
            }
        }
        
        // Handle death state
        if (IsDead)
        {
            // Could play death animation or hide player
            if (m_Animator != null)
            {
                m_Animator.SetBool("isDead", true);
            }
        }
    }

    private void UpdateCharacterAppearance()
    {
        // Load character-specific animator or sprite based on CharacterType
        // This would typically load from Resources based on m_CharacterType
        // For now, just log it
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Character type: {m_CharacterType}");
        }
    }
    #endregion
}

