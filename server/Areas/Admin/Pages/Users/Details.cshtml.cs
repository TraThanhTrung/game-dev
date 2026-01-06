using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GameServer.Areas.Admin.Pages.Users;

[Authorize]
public class DetailsModel : PageModel
{
    #region Private Fields
    private readonly AdminService _adminService;
    #endregion

    #region Public Properties
    public GameServer.Models.Entities.PlayerProfile? PlayerProfile { get; set; }
    public TimeSpan PlayTime { get; set; }
    #endregion

    #region Constructor
    public DetailsModel(AdminService adminService)
    {
        _adminService = adminService;
    }
    #endregion

    #region Public Methods
    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        PlayerProfile = await _adminService.GetUserDetailsAsync(id);
        if (PlayerProfile == null)
        {
            return NotFound();
        }

        PlayTime = await _adminService.GetPlayerPlayTimeAsync(id);
        return Page();
    }
    #endregion
}

