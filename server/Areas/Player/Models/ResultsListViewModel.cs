namespace GameServer.Areas.Player.Models;

/// <summary>
/// View model for Results list page.
/// </summary>
public class ResultsListViewModel
{
    public Guid SessionId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int PlayerCount { get; set; }
    public string Status { get; set; } = string.Empty;
}




