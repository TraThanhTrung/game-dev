namespace GameServer.Models.Dto;

/// <summary>
/// Temporary item buffs stored in Redis for a session.
/// These buffs are temporary stat boosts from consumable items with duration.
/// </summary>
public class TemporaryItemBuffs
{
    public Guid PlayerId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }
    
    // List of active buffs with expiration times
    public List<ItemBuff> ActiveBuffs { get; set; } = new();
}

/// <summary>
/// Represents a single temporary item buff with expiration time.
/// </summary>
public class ItemBuff
{
    public string ItemId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    
    // Buff values
    public int CurrentHealthBonus { get; set; }
    public int SpeedBonus { get; set; }
    public int DamageBonus { get; set; }
}

