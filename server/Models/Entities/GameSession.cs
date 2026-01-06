using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameServer.Models.Entities;

public class GameSession
{
    [Key]
    public Guid SessionId { get; set; }

    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    public DateTime? EndTime { get; set; }

    public int PlayerCount { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Active"; // Active, Completed, Abandoned

    public ICollection<SessionPlayer> Players { get; set; } = new List<SessionPlayer>();
}

