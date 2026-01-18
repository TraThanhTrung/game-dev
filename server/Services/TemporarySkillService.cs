using GameServer.Models.Dto;
using GameServer.Models.Entities;
using GameServer.Models.States;
using Microsoft.Extensions.Logging;

namespace GameServer.Services;

/// <summary>
/// Service to manage temporary skill bonuses stored in Redis.
/// Skills are session-scoped and not persisted to database.
/// </summary>
public class TemporarySkillService
{
    private readonly RedisService _redisService;
    private readonly ILogger<TemporarySkillService> _logger;
    private const int c_TtlHours = 4;

    public TemporarySkillService(RedisService redisService, ILogger<TemporarySkillService> logger)
    {
        _redisService = redisService;
        _logger = logger;
    }

    /// <summary>
    /// Get temporary skill bonuses from Redis.
    /// </summary>
    public async Task<TemporarySkillBonuses?> GetTemporarySkillBonusesAsync(string sessionId, Guid playerId)
    {
        return await _redisService.GetTemporarySkillBonusesAsync(sessionId, playerId);
    }

    /// <summary>
    /// Upgrade a temporary skill for a player in a session.
    /// </summary>
    public async Task<(bool Success, int Level, string? ErrorMessage)> UpgradeTemporarySkillAsync(
        string sessionId, Guid playerId, string skillId)
    {
        try
        {
            // Get or create temporary bonuses
            var bonuses = await _redisService.GetTemporarySkillBonusesAsync(sessionId, playerId);
            if (bonuses == null)
            {
                bonuses = new TemporarySkillBonuses
                {
                    PlayerId = playerId,
                    SessionId = sessionId,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow,
                    SkillLevels = new Dictionary<string, int>()
                };
            }

            // Increment skill level
            if (!bonuses.SkillLevels.ContainsKey(skillId))
            {
                bonuses.SkillLevels[skillId] = 0;
            }
            bonuses.SkillLevels[skillId]++;

            // Recalculate bonuses from skill levels
            CalculateBonusesFromSkills(bonuses);

            // Save to Redis
            await _redisService.SetTemporarySkillBonusesAsync(sessionId, playerId, bonuses);

            int level = bonuses.SkillLevels[skillId];
            _logger.LogInformation("Upgraded temporary skill {SkillId} to level {Level} for player {PlayerId} in session {SessionId}",
                skillId, level, playerId, sessionId);

            return (true, level, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upgrading temporary skill {SkillId} for player {PlayerId} in session {SessionId}",
                skillId, playerId, sessionId);
            return (false, 0, "Failed to upgrade temporary skill");
        }
    }

    /// <summary>
    /// Calculate stat bonuses from skill levels.
    /// Uses the same logic as PlayerService.ApplySkillBonusToStats.
    /// </summary>
    private void CalculateBonusesFromSkills(TemporarySkillBonuses bonuses)
    {
        // Reset bonuses
        bonuses.SpeedBonus = 0;
        bonuses.DamageBonus = 0;
        bonuses.MaxHealthBonus = 0;
        bonuses.KnockbackForceBonus = 0f;
        bonuses.ExpBonusPercent = 0f;

        // Calculate bonuses from skill levels
        foreach (var (skillId, level) in bonuses.SkillLevels)
        {
            switch (skillId)
            {
                case "Max Health Boost ":
                    bonuses.MaxHealthBonus += level;
                    break;

                case "Speed Boost":
                    bonuses.SpeedBonus += level;
                    break;

                case "Damage Boost":
                    bonuses.DamageBonus += level;
                    break;

                case "Knockback Boost":
                    bonuses.KnockbackForceBonus += 0.5f * level;
                    break;

                case "Exp Bonus":
                    bonuses.ExpBonusPercent += 0.1f * level; // 10% per level
                    break;

                case "Respawn Count":
                    // NOTE: Not yet implemented
                    break;

                default:
                    _logger.LogWarning("Unknown skill type: {SkillId}", skillId);
                    break;
            }
        }
    }

    /// <summary>
    /// Apply temporary bonuses to base stats from database.
    /// Returns combined stats for PlayerState.
    /// </summary>
    public PlayerState ApplyBonusesToPlayerState(PlayerState baseState, TemporarySkillBonuses? bonuses)
    {
        if (bonuses == null)
        {
            return baseState; // No bonuses, return base stats
        }

        // Apply bonuses to base stats
        baseState.Speed += bonuses.SpeedBonus;
        baseState.Damage += bonuses.DamageBonus;
        baseState.MaxHp += bonuses.MaxHealthBonus;
        baseState.KnockbackForce += bonuses.KnockbackForceBonus;
        baseState.ExpBonusPercent += bonuses.ExpBonusPercent;
        // Update current HP to reflect new MaxHp if it increased
        baseState.Hp = Math.Min(baseState.Hp, baseState.MaxHp);

        return baseState;
    }

    /// <summary>
    /// Apply temporary bonuses to base PlayerStats from database.
    /// Used when creating PlayerState from database stats.
    /// </summary>
    public void ApplyBonusesToBaseStats(PlayerState playerState, PlayerStats baseStats, TemporarySkillBonuses? bonuses)
    {
        if (bonuses == null)
        {
            // No bonuses, use base stats directly
            playerState.Damage = baseStats.Damage;
            playerState.Speed = baseStats.Speed;
            playerState.MaxHp = baseStats.MaxHealth;
            playerState.KnockbackForce = baseStats.KnockbackForce;
            playerState.ExpBonusPercent = baseStats.ExpBonusPercent;
            return;
        }

        // Apply base stats + bonuses
        playerState.Damage = baseStats.Damage + bonuses.DamageBonus;
        playerState.Speed = baseStats.Speed + bonuses.SpeedBonus;
        playerState.MaxHp = baseStats.MaxHealth + bonuses.MaxHealthBonus;
        playerState.KnockbackForce = baseStats.KnockbackForce + bonuses.KnockbackForceBonus;
        playerState.ExpBonusPercent = baseStats.ExpBonusPercent + bonuses.ExpBonusPercent;
        // Update current HP to reflect new MaxHp if it increased
        playerState.Hp = Math.Min(playerState.Hp, playerState.MaxHp);
    }
}






