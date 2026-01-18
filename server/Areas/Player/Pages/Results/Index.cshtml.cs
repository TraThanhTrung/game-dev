using GameServer.Areas.Player.Models;
using GameServer.Data;
using GameServer.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Areas.Player.Pages.Results;

/// <summary>
/// Results list page - shows all completed game sessions for the logged-in player.
/// </summary>
public class IndexModel : BasePlayerPageModel
{
    #region Private Fields
    private readonly GameDbContext _db;
    #endregion

    #region Public Properties
    public List<ResultsListViewModel> CompletedSessions { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalSessions { get; set; }
    public int TotalPages => TotalSessions > 0 ? (int)Math.Ceiling((double)TotalSessions / PageSize) : 0;
    #endregion

    #region Constructor
    public IndexModel(GameDbContext db)
    {
        _db = db;
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

        try
        {
            // Get total count of completed sessions for this player
            TotalSessions = await _db.GameSessions
                .Where(s => s.Status == "Completed" 
                    && s.EndTime != null 
                    && s.Players.Any(p => p.PlayerId == PlayerId.Value))
                .CountAsync();

            // Get completed sessions with pagination
            var skip = (CurrentPage - 1) * PageSize;
            var sessions = await _db.GameSessions
                .Where(s => s.Status == "Completed" 
                    && s.EndTime != null 
                    && s.Players.Any(p => p.PlayerId == PlayerId.Value))
                .OrderByDescending(s => s.EndTime)
                .Skip(skip)
                .Take(PageSize)
                .ToListAsync();

            // Map to ViewModel
            CompletedSessions = sessions.Select(s => new ResultsListViewModel
            {
                SessionId = s.SessionId,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                PlayerCount = s.PlayerCount,
                Status = s.Status
            }).ToList();
        }
        catch (Exception ex)
        {
            // Log error but don't crash - show empty list
            // In production, you might want to log this properly
        }

        return Page();
    }
    #endregion
}




