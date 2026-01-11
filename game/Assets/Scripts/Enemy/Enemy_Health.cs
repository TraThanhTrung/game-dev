using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_Health : MonoBehaviour
{
    #region Private Fields
    [SerializeField] private string m_EnemyTypeId = "slime";
    #endregion

    #region Public Properties
    public string EnemyTypeId => m_EnemyTypeId;
    #endregion

    #region Public Methods
    /// <summary>
    /// Set enemy type ID dynamically (for server-spawned enemies).
    /// Reloads config from GameConfigLoader after setting typeId.
    /// Does NOT set currentHealth - caller should set it from snapshot.
    /// </summary>
    public void SetEnemyTypeId(string typeId)
    {
        if (string.IsNullOrEmpty(typeId) || typeId == m_EnemyTypeId)
            return;

        m_EnemyTypeId = typeId;
        
        // Reload config with new typeId
        LoadFromConfig();
        
        Debug.Log($"[Enemy_Health] Set typeId to {typeId}, loaded config: maxHealth={maxHealth}");
    }

    /// <summary>
    /// Trigger defeat event (called externally when enemy dies from server state).
    /// Used by EnemySpawner when enemy HP reaches 0 from server polling.
    /// </summary>
    public void TriggerDefeatEvent()
    {
        if (currentHealth <= 0 && !string.IsNullOrEmpty(m_EnemyTypeId))
        {
            Debug.Log($"[Enemy_Health] Triggering defeat event for {m_EnemyTypeId}");
            OnMonsterDefeated?.Invoke(m_EnemyTypeId);
        }
    }
    #endregion

    #region Events
    public delegate void MonsterDefeated(string enemyTypeId);
    public static event MonsterDefeated OnMonsterDefeated;
    #endregion

    #region Public Fields
    public int currentHealth;
    public int maxHealth;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        // Only load config if typeId hasn't been set dynamically (for pre-placed enemies)
        // Server-spawned enemies will have typeId set via SetEnemyTypeId, which already calls LoadFromConfig
        LoadFromConfig();
        
        // Only set health to max if not already set (server-spawned enemies have HP from snapshot)
        if (currentHealth <= 0 && maxHealth > 0)
        {
            currentHealth = maxHealth;
        }
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

        var enemyConfig = EnemyConfigManager.Instance?.GetEnemyConfig(m_EnemyTypeId);
        if (enemyConfig != null)
        {
            maxHealth = enemyConfig.maxHealth;
            Debug.Log($"[Enemy_Health] Loaded config from database for {m_EnemyTypeId}: maxHealth={maxHealth}");
        }
        else
        {
            Debug.LogWarning($"[Enemy_Health] Config not found in database for typeId={m_EnemyTypeId}. " +
                $"Make sure EnemyConfigManager has loaded configs from server. Using default maxHealth={maxHealth}");
        }
    }
    #endregion


    public void ChangeHealth(int amount)
    {
        currentHealth += amount;

        if(currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

        else if( currentHealth <= 0)
        {
            Debug.Log($"[Enemy_Health] Enemy defeated: typeId={m_EnemyTypeId}, invoking OnMonsterDefeated event");
            OnMonsterDefeated?.Invoke(m_EnemyTypeId);
            Debug.Log($"[Enemy_Health] OnMonsterDefeated event invoked for {m_EnemyTypeId}");
            Destroy(gameObject);
        }

    }
}
