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
}
