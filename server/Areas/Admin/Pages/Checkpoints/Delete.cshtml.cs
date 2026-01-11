using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GameServer.Areas.Admin.Pages.Checkpoints;

[Authorize]
public class DeleteModel : PageModel
{
    #region Private Fields
    private readonly AdminService _adminService;
    #endregion

    #region Public Properties
    public GameServer.Models.Entities.Checkpoint? Checkpoint { get; set; }
    #endregion

    #region Constructor
    public DeleteModel(AdminService adminService)
    {
        _adminService = adminService;
    }
    #endregion

    #region Public Methods
    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        Checkpoint = await _adminService.GetCheckpointAsync(id.Value);
        if (Checkpoint == null)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var deleted = await _adminService.DeleteCheckpointAsync(id.Value);
        if (!deleted)
        {
            return NotFound();
        }

        return RedirectToPage("./Index");
    }
    #endregion
}
