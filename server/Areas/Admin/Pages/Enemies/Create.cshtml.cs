using GameServer.Models.Entities;
using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GameServer.Areas.Admin.Pages.Enemies;

[Authorize]
public class CreateModel : PageModel
{
    #region Private Fields
    private readonly AdminService _adminService;
    private readonly EnemyConfigService _enemyConfigService;
    #endregion

    #region Public Properties
    [BindProperty]
    public Enemy Enemy { get; set; } = new();
    #endregion

    #region Constructor
    public CreateModel(AdminService adminService, EnemyConfigService enemyConfigService)
    {
        _adminService = adminService;
        _enemyConfigService = enemyConfigService;
    }
    #endregion

    #region Public Methods
    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var created = await _adminService.CreateEnemyAsync(Enemy);

        // Invalidate cache
        await _enemyConfigService.InvalidateCacheAsync(Enemy.TypeId);

        return RedirectToPage("./Index");
    }
    #endregion
}













