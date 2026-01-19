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
    /// Temporary buffs are sent to server and stored in Redis (like skills).
    /// </summary>
    public void ApplyItemEffects(ItemSO itemSO)
    {
        // Apply local effects immediately (client-side prediction)
        // Track buff IDs for this item usage
        List<int> buffIds = new List<int>();

        // Apply current health boost (temporary if duration > 0)
        // Item chỉ nâng currentHealth tạm thời, KHÔNG nâng maxHealth
        if (itemSO.currentHealth > 0)
        {
            // Tăng currentHealth
            StatsManager.Instance.UpdateHealth(itemSO.currentHealth);

            // Nếu có duration, track để remove sau khi hết thời gian
            if (itemSO.duration > 0)
            {
                // Track để giảm máu khi hết buff
                int buffId = TrackBuff(itemSO, StatType.CurrentHealth, itemSO.currentHealth);
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

        // Nếu item có duration, gửi buff lên server để lưu vào Redis
        if (itemSO.duration > 0 && buffIds.Count > 0)
        {
            // Gửi buff lên server (lưu vào Redis)
            SendItemBuffToServer(itemSO, itemSO.duration);

            // Start effect timer để remove local buff sau khi hết thời gian
            // Server sẽ tự động remove buff từ Redis khi hết TTL
            StartCoroutine(EffectTimer(itemSO, itemSO.duration, buffIds));
        }
    }

    /// <summary>
    /// Send item buff to server to store in Redis (for multiplayer sync).
    /// </summary>
    private void SendItemBuffToServer(ItemSO itemSO, float duration)
    {
        // Chỉ gửi lên server nếu đang kết nối multiplayer
        if (NetClient.Instance == null || !NetClient.Instance.IsConnected)
        {
            Debug.LogWarning("[UseItem] Not connected to server, item buff only applied locally");
            return;
        }

        // Generate unique item ID from item name
        string itemId = itemSO.itemName.Replace(" ", "_").ToLower();

        // Gửi request lên server
        StartCoroutine(NetClient.Instance.UseItem(
            itemId, itemSO.itemName,
            itemSO.currentHealth, itemSO.speed, itemSO.damage, duration,
            res =>
            {
                if (res != null && res.success)
                {
                    Debug.Log($"[UseItem] Item buff sent to server: {itemSO.itemName} (duration: {duration}s)");
                }
            },
            err => Debug.LogWarning($"[UseItem] Failed to send item buff to server: {err}")
        ));
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
            case StatType.CurrentHealth:
                // Giảm currentHealth về mức ban đầu (trừ đi giá trị đã tăng)
                StatsManager.Instance.UpdateHealth(-(int)buff.value);
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
        CurrentHealth,
        Speed,
        Damage
    }
    #endregion
}
