namespace GameServer.Areas.Player.Models;

/// <summary>
/// View model for match history.
/// </summary>
public class MatchHistoryViewModel
{
    public Guid SessionId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int PlayerCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? PlayDuration { get; set; } // in seconds
    public DateTime? JoinTime { get; set; }
    public DateTime? LeaveTime { get; set; }
}

