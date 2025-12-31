using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameServer.Models.Entities;

public class PlayerProfile
{
    [Key]
    public Guid Id { get; set; }

    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    public int Level { get; set; }
    public int Exp { get; set; }
    public int Gold { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PlayerStats Stats { get; set; } = default!;
    public ICollection<SkillUnlock> Skills { get; set; } = new List<SkillUnlock>();
    public ICollection<InventoryItem> Inventory { get; set; } = new List<InventoryItem>();
}

