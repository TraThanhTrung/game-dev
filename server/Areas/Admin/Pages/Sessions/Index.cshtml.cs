using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GameServer.Areas.Admin.Pages.Sessions;

[Authorize]
public class IndexModel : PageModel
{
    #region Private Fields
    private readonly AdminService _adminService;
    #endregion

    #region Public Properties
    public List<GameServer.Models.Entities.GameSession> ActiveSessions { get; set; } = new();
    #endregion

    #region Constructor
    public IndexModel(AdminService adminService)
    {
        _adminService = adminService;
    }
    #endregion

    #region Public Methods
    public async Task OnGetAsync()
    {
        ActiveSessions = await _adminService.GetActiveSessionsAsync();
    }
    #endregion
}










