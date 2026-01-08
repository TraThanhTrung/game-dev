using GameServer.Areas.Player.Models;
using GameServer.Models.Entities;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Areas.Player.Pages;

/// <summary>
/// History page for players to view their match history.
/// </summary>
public class HistoryModel : BasePlayerPageModel
{
    #region Private Fields
    private readonly PlayerWebService _playerWebService;
    #endregion

    #region Public Properties
    public List<MatchHistoryViewModel> MatchHistory { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalMatches { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalMatches / PageSize);
    #endregion

    #region Constructor
    public HistoryModel(PlayerWebService playerWebService)
    {
        _playerWebService = playerWebService;
    }
    #endregion

    #region Public Methods
    public async Task<IActionResult> OnGetAsync(int page = 1)
    {
        if (!PlayerId.HasValue)
        {
            return RedirectToLogin();
        }

        CurrentPage = page;
        if (CurrentPage < 1) CurrentPage = 1;

        // Get total count
        TotalMatches = await _playerWebService.GetPlayerMatchCountAsync(PlayerId.Value);

        // Get player sessions with details
        var sessions = await _playerWebService.GetPlayerSessionsAsync(PlayerId.Value, CurrentPage, PageSize);

        // Map to view models
        MatchHistory = sessions.Select(sp => new MatchHistoryViewModel
        {
            SessionId = sp.SessionId,
            StartTime = sp.Session?.StartTime ?? sp.JoinTime,
            EndTime = sp.Session?.EndTime,
            PlayerCount = sp.Session?.PlayerCount ?? 0,
            Status = sp.Session?.Status ?? "Unknown",
            PlayDuration = sp.PlayDuration,
            JoinTime = sp.JoinTime,
            LeaveTime = sp.LeaveTime
        }).ToList();

        return Page();
    }
    #endregion
}




