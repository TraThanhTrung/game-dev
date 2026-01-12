using System.ComponentModel.DataAnnotations;

namespace GameServer.Models.Entities;

public class Enemy
{
    [Key]
    public int EnemyId { get; set; }

    [Required]
    [MaxLength(50)]
    public string TypeId { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int ExpReward { get; set; }

    public int GoldReward { get; set; }

    public int MaxHealth { get; set; }

    public int Damage { get; set; }

    public float Speed { get; set; }

    public float DetectRange { get; set; }

    public float AttackRange { get; set; }

    public float AttackCooldown { get; set; }

    public float WeaponRange { get; set; }

    public float KnockbackForce { get; set; }

    public float StunTime { get; set; }

    public float RespawnDelay { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}













