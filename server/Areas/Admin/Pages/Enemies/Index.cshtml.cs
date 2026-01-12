using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GameServer.Areas.Admin.Pages.Enemies;

[Authorize]
public class IndexModel : PageModel
{
    #region Private Fields
    private readonly AdminService _adminService;
    #endregion

    #region Public Properties
    public List<GameServer.Models.Entities.Enemy> Enemies { get; set; } = new();
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
        Enemies = await _adminService.GetEnemiesAsync();
    }
    #endregion
}














