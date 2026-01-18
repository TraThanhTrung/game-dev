using GameServer.Models.Dto;

namespace GameServer.Areas.Player.Models;

/// <summary>
/// View model for Results detail page.
/// </summary>
public class ResultsDetailViewModel
{
    public MatchResultDto? MatchResult { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
}




