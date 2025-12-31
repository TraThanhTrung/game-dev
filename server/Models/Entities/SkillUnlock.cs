using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameServer.Models.Entities;

public class SkillUnlock
{
    [Key]
    public int Id { get; set; }

    public Guid PlayerId { get; set; }

    [MaxLength(64)]
    public string SkillId { get; set; } = string.Empty;

    public int Level { get; set; }

    [ForeignKey(nameof(PlayerId))]
    public PlayerProfile Player { get; set; } = default!;
}

