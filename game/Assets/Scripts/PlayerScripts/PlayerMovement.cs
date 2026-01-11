using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles player movement input and physics.
/// Integrates with InputBlocker for loading screens and ClientPredictor for network prediction.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    #region Constants
    private const string c_LogPrefix = "[PlayerMovement]";
    #endregion

    #region Public Fields
    public int facingDirection = 1;
    public Rigidbody2D rb;
    public Animator anim;
    public bool isShooting;
    public Player_Combat player_Combat;
    #endregion

    #region Private Fields
    [Header("Network Settings")]
    [SerializeField] private bool m_EnableNetworking = true;
    [SerializeField] private bool m_EnableLogging = false;

    private bool isKnockedBack;
    private ClientPredictor m_Predictor;
    private int m_InputSequence = 0;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        // Get ClientPredictor component (only for local player)
        if (m_EnableNetworking && CompareTag("Player"))
        {
            m_Predictor = GetComponent<ClientPredictor>();
            if (m_Predictor == null)
            {
                m_Predictor = gameObject.AddComponent<ClientPredictor>();
            }
        }
    }

    private void Update()
    {
        // Check input blocker FIRST
        if (InputBlocker.Instance != null && InputBlocker.Instance.IsInputBlocked)
        {
            return;
        }

        if (player_Combat != null && player_Combat.enabled && Input.GetButtonDown("Slash"))
        {
            player_Combat.Attack();
        }
    }

    // FixedUpdate is called 50x second (matches server tick rate)
    private void FixedUpdate()
    {
        // Skip if dependencies not ready
        if (rb == null || StatsManager.Instance == null) return;

        // Check input blocker FIRST
        if (InputBlocker.Instance != null && InputBlocker.Instance.IsInputBlocked)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        if (isShooting == true)
        {
            rb.velocity = Vector2.zero;
        }
        else if (isKnockedBack == false)
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            // Handle flipping
            if (horizontal > 0 && transform.localScale.x < 0 ||
                horizontal < 0 && transform.localScale.x > 0)
            {
                Flip();
            }

            // Update animator
            if (anim != null)
            {
                anim.SetFloat("horizontal", Mathf.Abs(horizontal));
                anim.SetFloat("vertical", Mathf.Abs(vertical));
            }

            // Check if using network prediction
            bool useNetworkPrediction = m_EnableNetworking && 
                                        NetClient.Instance != null && 
                                        NetClient.Instance.IsSignalRConnected &&
                                        m_Predictor != null;
            
            if (useNetworkPrediction)
            {
                // Network mode: Let predictor handle movement
                SendNetworkInput(horizontal, vertical);
                m_Predictor.ApplyInput(horizontal, vertical, m_InputSequence);
            }
            else
            {
                // Offline/local mode: Apply velocity directly
                rb.velocity = new Vector2(horizontal, vertical) * StatsManager.Instance.speed;
            }
        }
    }
    #endregion

    #region Private Methods
    private void Flip()
    {
        facingDirection *= -1;
        transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);
    }

    private void SendNetworkInput(float horizontal, float vertical)
    {
        // Get next sequence number from predictor
        if (m_Predictor != null)
        {
            m_InputSequence = m_Predictor.GetNextSequence();
        }
        else
        {
            m_InputSequence++;
        }

        // Build input payload
        var input = new SignalRInputPayload
        {
            moveX = horizontal,
            moveY = vertical,
            sequence = m_InputSequence,
            attack = false,
            skill = false,
            timestamp = Time.time
        };

        // Send via NetClient
        NetClient.Instance.SendInputViaSignalR(input);

        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Sent input: seq={m_InputSequence}, move=({horizontal:F2}, {vertical:F2})");
        }
    }
    #endregion

    #region Public Methods
    public void Knockback(Transform enemy, float force, float stunTime)
    {
        isKnockedBack = true;
        Vector2 direction = (transform.position - enemy.position).normalized;
        rb.velocity = direction * force;
        StartCoroutine(KnockbackCounter(stunTime));
    }

    /// <summary>
    /// Reset prediction state (for respawn, teleport).
    /// </summary>
    public void ResetPrediction()
    {
        if (m_Predictor != null)
        {
            m_Predictor.Reset();
        }
        m_InputSequence = 0;
    }

    /// <summary>
    /// Force position (for server corrections).
    /// </summary>
    public void ForcePosition(Vector3 position)
    {
        transform.position = position;
        if (m_Predictor != null)
        {
            m_Predictor.ForcePosition(position);
        }
    }
    #endregion

    #region Coroutines
    private IEnumerator KnockbackCounter(float stunTime)
    {
        yield return new WaitForSeconds(stunTime);
        rb.velocity = Vector2.zero;
        isKnockedBack = false;
    }
    #endregion
}
