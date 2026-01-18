using GameServer.Models.Entities;
using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GameServer.Areas.Admin.Pages.GameSections;

[Authorize]
public class EditModel : PageModel
{
    #region Private Fields
    private readonly AdminService _adminService;
    #endregion

    #region Public Properties
    [BindProperty]
    public GameSection GameSection { get; set; } = new();
    #endregion

    #region Constructor
    public EditModel(AdminService adminService)
    {
        _adminService = adminService;
    }
    #endregion

    #region Public Methods
    public async Task<IActionResult> OnGetAsync(int id)
    {
        var section = await _adminService.GetGameSectionAsync(id);
        if (section == null)
        {
            return NotFound();
        }

        GameSection = section;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var updated = await _adminService.UpdateGameSectionAsync(GameSection.SectionId, GameSection);
        if (updated == null)
        {
            return NotFound();
        }

        return RedirectToPage("./Index");
    }
    #endregion
}


















