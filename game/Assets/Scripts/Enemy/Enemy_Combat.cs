using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_Combat : MonoBehaviour
{
    #region Private Fields
    [SerializeField] private Transform m_AttackPoint;
    [SerializeField] private LayerMask m_PlayerLayer;

    private Enemy_Health m_EnemyHealth;
    private int m_Damage = 1;
    private float m_WeaponRange = 1.2f;
    private float m_KnockbackForce = 5f;
    private float m_StunTime = 0.3f;
    #endregion

    #region Public Properties
    public Transform attackPoint => m_AttackPoint;
    public LayerMask playerLayer => m_PlayerLayer;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        m_EnemyHealth = GetComponent<Enemy_Health>();
        LoadFromConfig();
    }
    #endregion

    #region Private Methods
    private void LoadFromConfig()
    {
        // Load from EnemyConfigManager (database via API) instead of GameConfigLoader
        // Ensure EnemyConfigManager exists
        if (EnemyConfigManager.Instance == null)
        {
            var go = new GameObject("EnemyConfigManager");
            go.AddComponent<EnemyConfigManager>();
        }

        // Get typeId from Enemy_Health component
        string typeId = "slime"; // default
        if (m_EnemyHealth != null)
        {
            typeId = m_EnemyHealth.EnemyTypeId;
        }

        var enemyConfig = EnemyConfigManager.Instance?.GetEnemyConfig(typeId);
        if (enemyConfig != null)
        {
            m_Damage = enemyConfig.damage;
            m_WeaponRange = enemyConfig.weaponRange;
            m_KnockbackForce = enemyConfig.knockbackForce;
            m_StunTime = enemyConfig.stunTime;
            Debug.Log($"[Enemy_Combat] Loaded config from database for {typeId}: damage={m_Damage}, weaponRange={m_WeaponRange}");
        }
        else
        {
            Debug.LogWarning($"[Enemy_Combat] Config not found in database for typeId={typeId}. " +
                $"Make sure EnemyConfigManager has loaded configs from server. Using defaults.");
        }
    }
    #endregion




    #region Public Methods
    public void Attack()
    {
        if (m_AttackPoint == null)
        {
            Debug.LogWarning("[Enemy_Combat] AttackPoint not assigned!");
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(m_AttackPoint.position, m_WeaponRange, m_PlayerLayer);

        if (hits.Length > 0)
        {
            var playerGO = hits[0].gameObject;
            var playerTransform = playerGO.transform;
            
            // Client-side validation: Check actual distance to prevent damage from far away
            float actualDistance = Vector2.Distance(m_AttackPoint.position, playerTransform.position);
            if (actualDistance > m_WeaponRange)
            {
                // Player is outside weapon range, don't deal damage
                return;
            }

            // Apply damage (will be validated by server)
            hits[0].GetComponent<PlayerHealth>().ChangeHealth(-m_Damage);

            // Only apply knockback if player is still active (not dead)
            if (playerGO.activeInHierarchy)
            {
                hits[0].GetComponent<PlayerMovement>().Knockback(transform, m_KnockbackForce, m_StunTime);
            }
        }
    }
    #endregion
}
