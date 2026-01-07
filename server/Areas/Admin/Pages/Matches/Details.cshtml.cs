using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GameServer.Areas.Admin.Pages.Matches;

[Authorize]
public class DetailsModel : PageModel
{
    #region Private Fields
    private readonly AdminService _adminService;
    #endregion

    #region Public Properties
    public GameServer.Models.Entities.GameSession? Match { get; set; }
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
        Match = await _adminService.GetMatchDetailsAsync(id);
        if (Match == null)
        {
            return NotFound();
        }

        return Page();
    }
    #endregion
}


