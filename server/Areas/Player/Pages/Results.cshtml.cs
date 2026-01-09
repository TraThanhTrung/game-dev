using GameServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Areas.Player.Pages;

/// <summary>
/// Game Results page for players to view their game results.
/// Note: This depends on GameResult entity from Plan 3, which may not be implemented yet.
/// </summary>
public class ResultsModel : BasePlayerPageModel
{
    #region Private Fields
    private readonly PlayerWebService _playerWebService;
    #endregion

    #region Public Properties
    public List<object> GameResults { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalResults { get; set; }
    public int TotalPages => TotalResults > 0 ? (int)Math.Ceiling((double)TotalResults / PageSize) : 0;
    #endregion

    #region Constructor
    public ResultsModel(PlayerWebService playerWebService)
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

        // Get game results (placeholder - depends on Plan 3 GameResult entity)
        var results = await _playerWebService.GetPlayerGameResultsAsync(PlayerId.Value, CurrentPage, PageSize);
        GameResults = results;
        TotalResults = results.Count;

        return Page();
    }
    #endregion
}





