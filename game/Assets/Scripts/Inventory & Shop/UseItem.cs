using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles applying item effects to player stats.
/// Supports both permanent effects (healing) and temporary buffs (stat boosts with duration).
/// Temporary effects are removed after duration expires, similar to skill boosts.
/// </summary>
public class UseItem : MonoBehaviour
{
    #region Private Fields
    private List<ActiveBuff> m_ActiveBuffs = new List<ActiveBuff>();
    #endregion

    #region Public Methods
    /// <summary>
    /// Apply all effects from an item to the player's stats.
    /// If item has duration > 0, effects are temporary and will be removed after duration.
    /// </summary>
    public void ApplyItemEffects(ItemSO itemSO)
    {
        // Apply health healing (permanent effect, no duration)
        if (itemSO.currentHealth > 0)
        {
            StatsManager.Instance.UpdateHealth(itemSO.currentHealth);
        }

        // Track buff IDs for this item usage
        List<int> buffIds = new List<int>();

        // Apply max health boost (can be temporary if duration > 0)
        if (itemSO.maxHealth > 0)
        {
            StatsManager.Instance.UpdateMaxHealth(itemSO.maxHealth);
            
            if (itemSO.duration > 0)
            {
                // Track for removal later
                int buffId = TrackBuff(itemSO, StatType.MaxHealth, itemSO.maxHealth);
                buffIds.Add(buffId);
            }
        }

        // Apply speed boost (temporary if duration > 0)
        if (itemSO.speed > 0)
        {
            StatsManager.Instance.UpdateSpeed(itemSO.speed);
            
            if (itemSO.duration > 0)
            {
                int buffId = TrackBuff(itemSO, StatType.Speed, itemSO.speed);
                buffIds.Add(buffId);
            }
        }

        // Apply damage boost (temporary if duration > 0)
        if (itemSO.damage > 0)
        {
            StatsManager.Instance.AddDamage(itemSO.damage);
            
            if (itemSO.duration > 0)
            {
                int buffId = TrackBuff(itemSO, StatType.Damage, itemSO.damage);
                buffIds.Add(buffId);
            }
        }

        // Start effect timer if item has duration (temporary buff)
        if (itemSO.duration > 0 && buffIds.Count > 0)
        {
            StartCoroutine(EffectTimer(itemSO, itemSO.duration, buffIds));
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Track an active buff for removal after duration expires.
    /// Returns a unique buff ID for tracking.
    /// </summary>
    private int TrackBuff(ItemSO itemSO, StatType statType, float value)
    {
        int buffId = GetUniqueBuffId();
        var buff = new ActiveBuff
        {
            buffId = buffId,
            itemSO = itemSO,
            statType = statType,
            value = value,
            startTime = Time.time
        };
        m_ActiveBuffs.Add(buff);
        return buffId;
    }

    /// <summary>
    /// Generate a unique ID for buff tracking.
    /// </summary>
    private int GetUniqueBuffId()
    {
        int maxId = 0;
        foreach (var buff in m_ActiveBuffs)
        {
            if (buff.buffId > maxId)
                maxId = buff.buffId;
        }
        return maxId + 1;
    }

    /// <summary>
    /// Remove temporary effects after duration expires.
    /// Works similarly to skill boosts - stats are reverted to original values.
    /// </summary>
    private IEnumerator EffectTimer(ItemSO itemSO, float duration, List<int> buffIds)
    {
        yield return new WaitForSeconds(duration);

        // Remove all buffs associated with this item usage
        foreach (int buffId in buffIds)
        {
            RemoveBuffById(buffId);
        }
    }

    /// <summary>
    /// Remove a specific buff by its unique ID.
    /// </summary>
    private void RemoveBuffById(int buffId)
    {
        ActiveBuff buffToRemove = null;
        foreach (var buff in m_ActiveBuffs)
        {
            if (buff.buffId == buffId)
            {
                buffToRemove = buff;
                break;
            }
        }

        if (buffToRemove != null)
        {
            RemoveBuff(buffToRemove);
            m_ActiveBuffs.Remove(buffToRemove);
        }
    }

    /// <summary>
    /// Remove a single buff effect from player stats.
    /// </summary>
    private void RemoveBuff(ActiveBuff buff)
    {
        switch (buff.statType)
        {
            case StatType.MaxHealth:
                StatsManager.Instance.UpdateMaxHealth(-(int)buff.value);
                break;

            case StatType.Speed:
                StatsManager.Instance.UpdateSpeed(-(int)buff.value);
                break;

            case StatType.Damage:
                StatsManager.Instance.AddDamage(-(int)buff.value);
                break;
        }
    }
    #endregion

    #region Nested Classes
    /// <summary>
    /// Tracks an active temporary buff from an item.
    /// </summary>
    private class ActiveBuff
    {
        public int buffId;
        public ItemSO itemSO;
        public StatType statType;
        public float value;
        public float startTime;
    }

    /// <summary>
    /// Types of stats that can be boosted by items.
    /// </summary>
    private enum StatType
    {
        MaxHealth,
        Speed,
        Damage
    }
    #endregion
}
