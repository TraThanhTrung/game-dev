using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GameServer.Areas.Admin.Pages.Matches;

[Authorize]
public class IndexModel : PageModel
{
    #region Private Fields
    private readonly AdminService _adminService;
    #endregion

    #region Public Properties
    public List<GameServer.Models.Entities.GameSession> Matches { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalMatches { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    #endregion

    #region Constructor
    public IndexModel(AdminService adminService)
    {
        _adminService = adminService;
    }
    #endregion

    #region Public Methods
    public async Task OnGetAsync(int page = 1, DateTime? fromDate = null, DateTime? toDate = null)
    {
        CurrentPage = page;
        FromDate = fromDate;
        ToDate = toDate;
        const int pageSize = 20;

        var (matches, total) = await _adminService.GetMatchesAsync(page, pageSize, fromDate, toDate, null);
        Matches = matches;
        TotalMatches = total;
        TotalPages = (int)Math.Ceiling(total / (double)pageSize);
    }
    #endregion
}


















