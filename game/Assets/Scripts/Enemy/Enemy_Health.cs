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
        LoadFromConfig();
        currentHealth = maxHealth;
    }
    #endregion

    #region Private Methods
    private void LoadFromConfig()
    {
        // Ensure GameConfigLoader exists
        GameConfigLoader.EnsureInstance();

        if (GameConfigLoader.Instance == null)
        {
            Debug.LogWarning($"[Enemy_Health] GameConfigLoader not found. Using default maxHealth={maxHealth}");
            return;
        }

        var enemyConfig = GameConfigLoader.Instance.GetEnemyConfig(m_EnemyTypeId);
        if (enemyConfig != null)
        {
            maxHealth = enemyConfig.maxHealth;
            Debug.Log($"[Enemy_Health] Loaded config for {m_EnemyTypeId}: maxHealth={maxHealth}");
        }
        else
        {
            Debug.LogWarning($"[Enemy_Health] Config not found for typeId={m_EnemyTypeId}. Using default maxHealth={maxHealth}");
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
