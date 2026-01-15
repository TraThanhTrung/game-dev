using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameServer.Models.Entities;

public class SessionPlayer
{
    [Key]
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public Guid PlayerId { get; set; }

    public DateTime JoinTime { get; set; } = DateTime.UtcNow;

    public DateTime? LeaveTime { get; set; }

    public int? PlayDuration { get; set; } // in seconds

    [ForeignKey(nameof(SessionId))]
    public GameSession Session { get; set; } = default!;
}

















