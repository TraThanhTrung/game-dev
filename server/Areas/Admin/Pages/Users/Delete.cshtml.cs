using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GameServer.Areas.Admin.Pages.Users;

[Authorize]
public class DeleteModel : PageModel
{
    #region Private Fields
    private readonly AdminService _adminService;
    #endregion

    #region Public Properties
    public GameServer.Models.Entities.PlayerProfile? PlayerProfile { get; set; }
    #endregion

    #region Constructor
    public DeleteModel(AdminService adminService)
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

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var player = await _adminService.GetUserDetailsAsync(id);
        if (player == null)
        {
            return NotFound();
        }

        var deleted = await _adminService.DeleteUserAsync(id);
        
        if (deleted)
        {
            TempData["SuccessMessage"] = $"User '{player.Name}' has been deleted successfully.";
        }
        else
        {
            TempData["ErrorMessage"] = "Failed to delete user.";
        }

        return RedirectToPage("./Index");
    }
    #endregion
}




