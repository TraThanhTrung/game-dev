using System.ComponentModel.DataAnnotations;

namespace GameServer.Models.Entities;

public class GameSection
{
    [Key]
    public int SectionId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? EnemyTypeId { get; set; }

    public int EnemyCount { get; set; }

    public int EnemyLevel { get; set; } = 1;

    public float SpawnRate { get; set; } = 1.0f;

    public int? Duration { get; set; } // in seconds, null = unlimited

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}



