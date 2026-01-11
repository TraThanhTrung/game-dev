using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameServer.Models.Entities;

public class Checkpoint
{
    [Key]
    public int CheckpointId { get; set; }

    [Required]
    [MaxLength(100)]
    public string CheckpointName { get; set; } = string.Empty;

    public int? SectionId { get; set; } // Foreign Key to GameSection (nullable for backward compatibility)

    [ForeignKey("SectionId")]
    public GameSection? Section { get; set; } // Navigation property

    public float X { get; set; }

    public float Y { get; set; }

    [Required]
    [MaxLength(1000)]
    public string EnemyPool { get; set; } = "[]"; // JSON array: ["slime", "goblin", "slime"]

    public int MaxEnemies { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
