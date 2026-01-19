using GameServer.Models.Dto;
using GameServer.Models.States;
using Microsoft.Extensions.Logging;

namespace GameServer.Services;

/// <summary>
/// Service to manage temporary item buffs stored in Redis.
/// Item buffs are session-scoped and expire after duration.
/// </summary>
public class TemporaryItemService
{
    private readonly RedisService _redisService;
    private readonly ILogger<TemporaryItemService> _logger;
    private const int c_TtlHours = 4;

    public TemporaryItemService(RedisService redisService, ILogger<TemporaryItemService> logger)
    {
        _redisService = redisService;
        _logger = logger;
    }

    /// <summary>
    /// Get temporary item buffs from Redis.
    /// </summary>
    public async Task<TemporaryItemBuffs?> GetTemporaryItemBuffsAsync(string sessionId, Guid playerId)
    {
        return await _redisService.GetTemporaryItemBuffsAsync(sessionId, playerId);
    }

    /// <summary>
    /// Apply an item buff to player (stores in Redis with expiration).
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> ApplyItemBuffAsync(
        string sessionId, Guid playerId, string itemId, string itemName,
        int currentHealthBonus, int speedBonus, int damageBonus, float durationSeconds)
    {
        try
        {
            // Get or create temporary buffs
            var buffs = await _redisService.GetTemporaryItemBuffsAsync(sessionId, playerId);
            if (buffs == null)
            {
                buffs = new TemporaryItemBuffs
                {
                    PlayerId = playerId,
                    SessionId = sessionId,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow,
                    ActiveBuffs = new List<ItemBuff>()
                };
            }

            // Create new buff with expiration
            var newBuff = new ItemBuff
            {
                ItemId = itemId,
                ItemName = itemName,
                ExpiresAt = DateTime.UtcNow.AddSeconds(durationSeconds),
                CurrentHealthBonus = currentHealthBonus,
                SpeedBonus = speedBonus,
                DamageBonus = damageBonus
            };

            // Remove expired buffs
            buffs.ActiveBuffs.RemoveAll(b => b.ExpiresAt < DateTime.UtcNow);

            // Add new buff
            buffs.ActiveBuffs.Add(newBuff);
            buffs.LastUpdated = DateTime.UtcNow;

            // Save to Redis
            await _redisService.SetTemporaryItemBuffsAsync(sessionId, playerId, buffs);

            _logger.LogInformation("Applied item buff {ItemId} ({ItemName}) for player {PlayerId} in session {SessionId}, expires in {Duration}s",
                itemId, itemName, playerId, sessionId, durationSeconds);

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying item buff {ItemId} for player {PlayerId} in session {SessionId}",
                itemId, playerId, sessionId);
            return (false, "Failed to apply item buff");
        }
    }

    /// <summary>
    /// Remove expired buffs from Redis.
    /// Should be called periodically or when applying buffs.
    /// </summary>
    public async Task CleanupExpiredBuffsAsync(string sessionId, Guid playerId)
    {
        try
        {
            var buffs = await _redisService.GetTemporaryItemBuffsAsync(sessionId, playerId);
            if (buffs == null) return;

            int beforeCount = buffs.ActiveBuffs.Count;
            buffs.ActiveBuffs.RemoveAll(b => b.ExpiresAt < DateTime.UtcNow);

            if (buffs.ActiveBuffs.Count != beforeCount)
            {
                buffs.LastUpdated = DateTime.UtcNow;
                await _redisService.SetTemporaryItemBuffsAsync(sessionId, playerId, buffs);
                _logger.LogDebug("Cleaned up {Count} expired buffs for player {PlayerId} in session {SessionId}",
                    beforeCount - buffs.ActiveBuffs.Count, playerId, sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired buffs for player {PlayerId} in session {SessionId}",
                playerId, sessionId);
        }
    }

    /// <summary>
    /// Calculate total bonuses from all active (non-expired) item buffs.
    /// </summary>
    public ItemBonuses CalculateActiveBonuses(TemporaryItemBuffs? buffs)
    {
        if (buffs == null || buffs.ActiveBuffs.Count == 0)
        {
            return new ItemBonuses();
        }

        var now = DateTime.UtcNow;
        var bonuses = new ItemBonuses();

        foreach (var buff in buffs.ActiveBuffs)
        {
            // Only count non-expired buffs
            if (buff.ExpiresAt >= now)
            {
                bonuses.CurrentHealthBonus += buff.CurrentHealthBonus;
                bonuses.SpeedBonus += buff.SpeedBonus;
                bonuses.DamageBonus += buff.DamageBonus;
            }
        }

        return bonuses;
    }

    /// <summary>
    /// Apply temporary item bonuses to PlayerState.
    /// Only applies to current HP (temporary health boost).
    /// </summary>
    public void ApplyBonusesToPlayerState(PlayerState playerState, TemporaryItemBuffs? buffs)
    {
        if (buffs == null)
        {
            return; // No buffs
        }

        // Clean up expired buffs first
        buffs.ActiveBuffs.RemoveAll(b => b.ExpiresAt < DateTime.UtcNow);

        // Calculate active bonuses
        var bonuses = CalculateActiveBonuses(buffs);

        // Apply bonuses (only currentHealth for items, speed and damage are already in base stats)
        // Note: CurrentHealthBonus is added to current HP, not max HP
        playerState.Hp = Math.Min(playerState.Hp + bonuses.CurrentHealthBonus, playerState.MaxHp);
        
        // Speed and Damage are already applied via base stats in WorldService
        // We just track them here for UI display if needed
    }
}

/// <summary>
/// Total bonuses from all active item buffs.
/// </summary>
public class ItemBonuses
{
    public int CurrentHealthBonus { get; set; }
    public int SpeedBonus { get; set; }
    public int DamageBonus { get; set; }
}

