using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GameServer.Areas.Admin.Pages.GameSections;

[Authorize]
public class DetailsModel : PageModel
{
    #region Private Fields
    private readonly AdminService _adminService;
    #endregion

    #region Public Properties
    public GameServer.Models.Entities.GameSection? GameSection { get; set; }
    public List<GameServer.Models.Entities.Checkpoint> Checkpoints { get; set; } = new();
    #endregion

    #region Constructor
    public DetailsModel(AdminService adminService)
    {
        _adminService = adminService;
    }
    #endregion

    #region Public Methods
    public async Task<IActionResult> OnGetAsync(int id)
    {
        GameSection = await _adminService.GetGameSectionAsync(id);
        if (GameSection == null)
        {
            return NotFound();
        }

        // Load checkpoints for this section
        Checkpoints = await _adminService.GetCheckpointsAsync(id);

        return Page();
    }
    #endregion
}








