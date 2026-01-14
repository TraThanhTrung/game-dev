using System;
using UnityEngine;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    #region Events
    public static event Action OnPlayerDied;
    public static event Action OnPlayerRespawned;
    #endregion

    #region Private Fields
    [SerializeField] private TMP_Text m_HealthText;
    [SerializeField] private Animator m_HealthTextAnim;
    #endregion

    #region Public Properties
    // Legacy support for existing references
    public TMP_Text healthText { get => m_HealthText; set => m_HealthText = value; }
    public Animator healthTextAnim { get => m_HealthTextAnim; set => m_HealthTextAnim = value; }
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        UpdateHealthUI();
    }
    #endregion

    #region Public Methods
    public void ChangeHealth(int amount)
    {
        int finalAmount = amount;
        if (amount < 0)
        {
            int incomingDamage = Mathf.Abs(amount);
            int reducedDamage = StatsManager.Instance.CalculateDamageTaken(incomingDamage);
            finalAmount = -reducedDamage;

            // Report damage to server for authoritative HP management
            if (NetClient.Instance != null && NetClient.Instance.IsConnected)
            {
                // Store predicted HP for reconciliation
                int predictedHp = StatsManager.Instance.currentHealth + finalAmount;
                
                StartCoroutine(NetClient.Instance.ReportDamage(reducedDamage,
                    res =>
                    {
                        if (res != null && res.accepted)
                        {
                            // Reconcile with server HP if different (server is authoritative)
                            if (StatsManager.Instance.currentHealth != res.currentHp)
                            {
                                // Small difference: just update (network delay)
                                if (Mathf.Abs(StatsManager.Instance.currentHealth - res.currentHp) <= 5)
                                {
                                    StatsManager.Instance.currentHealth = res.currentHp;
                                    StatsManager.Instance.maxHealth = res.maxHp;
                                    UpdateHealthUI();
                                }
                                // Large difference: server corrected us (likely validation failed)
                                else
                                {
                                    StatsManager.Instance.currentHealth = res.currentHp;
                                    StatsManager.Instance.maxHealth = res.maxHp;
                                    UpdateHealthUI();
                                    Debug.LogWarning($"[PlayerHealth] HP reconciled: predicted={predictedHp}, server={res.currentHp}");
                                }
                            }
                        }
                    },
                    err => Debug.LogWarning($"Damage report failed: {err}")));
            }
        }

        StatsManager.Instance.currentHealth += finalAmount;

        // Clamp health
        if (StatsManager.Instance.currentHealth < 0)
            StatsManager.Instance.currentHealth = 0;
        if (StatsManager.Instance.currentHealth > StatsManager.Instance.maxHealth)
            StatsManager.Instance.currentHealth = StatsManager.Instance.maxHealth;

        if (m_HealthTextAnim != null)
            m_HealthTextAnim.Play("TextUpdate");

        UpdateHealthUI();

        if (StatsManager.Instance.currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Called to respawn the player with half health.
    /// </summary>
    public void Respawn()
    {
        // Respawn with 50% health instead of full health
        StatsManager.Instance.currentHealth = StatsManager.Instance.maxHealth / 2;
        gameObject.SetActive(true);
        UpdateHealthUI();
        OnPlayerRespawned?.Invoke();
    }
    #endregion

    #region Private Methods
    private void UpdateHealthUI()
    {
        string healthString = "HP: " + StatsManager.Instance.currentHealth + " / " + StatsManager.Instance.maxHealth;

        // Update local health text
        if (m_HealthText != null)
            m_HealthText.text = healthString;

        // Also update StatsManager's health text if it exists
        if (StatsManager.Instance != null && StatsManager.Instance.healthText != null)
            StatsManager.Instance.healthText.text = healthString;
    }

    private void Die()
    {
        OnPlayerDied?.Invoke();
        gameObject.SetActive(false);
    }
    #endregion
}
