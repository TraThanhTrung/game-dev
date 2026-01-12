using GameServer.Data;
using GameServer.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameServer.Services;

/// <summary>
/// Handles player persistence (database operations).
/// Player creation is handled via Admin Panel or PlayerWebService.
/// This service only finds and updates existing players.
/// </summary>
public class PlayerService
{
    private readonly GameDbContext _db;
    private readonly ILogger<PlayerService> _logger;
    private readonly GameConfigService _config;

    public PlayerService(GameDbContext db, ILogger<PlayerService> logger, GameConfigService config)
    {
        _db = db;
        _logger = logger;
        _config = config;
    }

    /// <summary>
    /// Find existing player by name. Does NOT create new player.
    /// Player must be created via Admin Panel or Register page (PlayerWebService).
    /// </summary>
    public async Task<PlayerProfile?> FindPlayerAsync(string playerName)
    {
        // Normalize name
        playerName = playerName.Trim();

        // Only find existing player, do NOT create
        var existing = await _db.PlayerProfiles
            .Include(p => p.Stats)
            .Include(p => p.Inventory)
            .FirstOrDefaultAsync(p => p.Name.ToLower() == playerName.ToLower());

        if (existing == null)
        {
            _logger.LogWarning("Player {Name} not found in database. Player must be created via Admin Panel or Register page.", playerName);
        }
        else
        {
            _logger.LogInformation("Found player: {Name} (ID: {Id})", existing.Name, existing.Id);
        }

        return existing;
    }

    /// <summary>
    /// Get player by ID with all related data.
    /// </summary>
    public async Task<PlayerProfile?> GetPlayerAsync(Guid playerId)
    {
        return await _db.PlayerProfiles
            .Include(p => p.Stats)
            .Include(p => p.Inventory)
            .Include(p => p.Skills)
            .FirstOrDefaultAsync(p => p.Id == playerId);
    }

    /// <summary>
    /// Save player progress (exp, gold, level, stats).
    /// </summary>
    public async Task SaveProgressAsync(Guid playerId, int exp, int gold, int level, int currentHealth)
    {
        var player = await _db.PlayerProfiles
            .Include(p => p.Stats)
            .FirstOrDefaultAsync(p => p.Id == playerId);

        if (player == null)
        {
            _logger.LogWarning("SaveProgress: Player not found: {Id}", playerId);
            return;
        }

        player.Exp = exp;
        player.Gold = gold;
        player.Level = level;
        player.ExpToLevel = _config.GetExpForNextLevel(level);
        player.Stats.CurrentHealth = currentHealth;

        await _db.SaveChangesAsync();
        _logger.LogDebug("Saved progress for {Name}: Exp={Exp}, Gold={Gold}, Level={Level}, ExpToLevel={ExpToLevel}", 
            player.Name, exp, gold, level, player.ExpToLevel);
    }

    /// <summary>
    /// Add item to player inventory.
    /// </summary>
    public async Task AddInventoryItemAsync(Guid playerId, string itemId, int quantity)
    {
        var existing = await _db.InventoryItems
            .FirstOrDefaultAsync(i => i.PlayerId == playerId && i.ItemId == itemId);

        if (existing != null)
        {
            existing.Quantity += quantity;
        }
        else
        {
            _db.InventoryItems.Add(new InventoryItem
            {
                PlayerId = playerId,
                ItemId = itemId,
                Quantity = quantity
            });
        }

        await _db.SaveChangesAsync();
        _logger.LogDebug("Added {Qty}x {Item} to player {Id}", quantity, itemId, playerId);
    }

    /// <summary>
    /// Remove item from player inventory.
    /// </summary>
    public async Task<bool> RemoveInventoryItemAsync(Guid playerId, string itemId, int quantity)
    {
        var existing = await _db.InventoryItems
            .FirstOrDefaultAsync(i => i.PlayerId == playerId && i.ItemId == itemId);

        if (existing == null || existing.Quantity < quantity)
        {
            return false;
        }

        existing.Quantity -= quantity;
        if (existing.Quantity <= 0)
        {
            _db.InventoryItems.Remove(existing);
        }

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Get player inventory.
    /// </summary>
    public async Task<List<InventoryItem>> GetInventoryAsync(Guid playerId)
    {
        return await _db.InventoryItems
            .Where(i => i.PlayerId == playerId)
            .ToListAsync();
    }

    /// <summary>
    /// Upgrade a skill for a player and apply bonuses to PlayerStats.
    /// DEPRECATED: Skills are now temporary and stored in Redis via TemporarySkillService.
    /// This method is kept for backward compatibility but should not be used.
    /// </summary>
    [Obsolete("Skills are now temporary and stored in Redis. Use TemporarySkillService.UpgradeTemporarySkillAsync() instead.")]
    public async Task<(bool Success, int Level, string? ErrorMessage)> UpgradeSkillAsync(Guid playerId, string skillId)
    {
        try
        {
            var player = await _db.PlayerProfiles
                .Include(p => p.Stats)
                .Include(p => p.Skills)
                .FirstOrDefaultAsync(p => p.Id == playerId);

            if (player == null)
            {
                return (false, 0, "Player not found");
            }

            if (player.Stats == null)
            {
                return (false, 0, "Player stats not found");
            }

            // Find or create skill unlock
            var skillUnlock = player.Skills.FirstOrDefault(s => s.SkillId == skillId);
            if (skillUnlock == null)
            {
                skillUnlock = new SkillUnlock
                {
                    PlayerId = playerId,
                    SkillId = skillId,
                    Level = 0
                };
                _db.SkillUnlocks.Add(skillUnlock);
                player.Skills.Add(skillUnlock);
            }

            // Increment skill level
            skillUnlock.Level++;

            // Apply skill bonus to PlayerStats
            ApplySkillBonusToStats(player.Stats, skillId, 1); // +1 level

            await _db.SaveChangesAsync();

            _logger.LogInformation("Upgraded skill {SkillId} to level {Level} for player {PlayerId}", 
                skillId, skillUnlock.Level, playerId);

            return (true, skillUnlock.Level, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upgrading skill {SkillId} for player {PlayerId}", skillId, playerId);
            return (false, 0, "Failed to upgrade skill");
        }
    }

    /// <summary>
    /// Get all skills for a player.
    /// </summary>
    public async Task<List<SkillUnlock>> GetPlayerSkillsAsync(Guid playerId)
    {
        return await _db.SkillUnlocks
            .Where(s => s.PlayerId == playerId)
            .ToListAsync();
    }

    /// <summary>
    /// Apply skill bonus to PlayerStats based on skill type and level increment.
    /// </summary>
    private void ApplySkillBonusToStats(PlayerStats stats, string skillId, int levelIncrement)
    {
        switch (skillId)
        {
            case "Max Health Boost ":
                stats.MaxHealth += levelIncrement;
                break;

            case "Speed Boost":
                stats.Speed += levelIncrement;
                break;

            case "Damage Boost":
                stats.Damage += levelIncrement;
                break;

            case "Knockback Boost":
                stats.KnockbackForce += 0.5f * levelIncrement;
                break;

            case "Exp Bonus":
                stats.ExpBonusPercent += 0.1f * levelIncrement; // 10% per level
                break;

            case "Respawn Count":
                // NOTE: Respawn count not yet implemented - would need to track in PlayerProfile or PlayerStats
                break;

            default:
                _logger.LogWarning("Unknown skill type: {SkillId}", skillId);
                break;
        }
    }
}
