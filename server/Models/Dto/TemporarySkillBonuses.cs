namespace GameServer.Models.Dto;

/// <summary>
/// Temporary skill bonuses stored in Redis for a session.
/// These bonuses are added to base stats from database.
/// </summary>
public class TemporarySkillBonuses
{
    public Guid PlayerId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }
    
    // Skill levels (for skill tree sync)
    public Dictionary<string, int> SkillLevels { get; set; } = new(); // SkillId -> Level
    
    // Bonuses calculated from skill levels
    public int SpeedBonus { get; set; }
    public int DamageBonus { get; set; }
    public int MaxHealthBonus { get; set; }
    public float KnockbackForceBonus { get; set; }
    public float ExpBonusPercent { get; set; }
}

