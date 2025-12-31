using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameServer.Models.Entities;

public class PlayerStats
{
    [Key]
    public Guid PlayerId { get; set; }

    public int Damage { get; set; }
    public float Range { get; set; }
    public float KnockbackForce { get; set; }
    public float Speed { get; set; }
    public int MaxHealth { get; set; }
    public int CurrentHealth { get; set; }

    [ForeignKey(nameof(PlayerId))]
    public PlayerProfile Player { get; set; } = default!;
}

