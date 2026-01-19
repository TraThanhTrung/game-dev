using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GameServer.Areas.Admin.Pages.Enemies;

[Authorize]
public class DeleteModel : PageModel
{
    #region Private Fields
    private readonly AdminService _adminService;
    private readonly EnemyConfigService _enemyConfigService;
    #endregion

    #region Public Properties
    public GameServer.Models.Entities.Enemy? Enemy { get; set; }
    #endregion

    #region Constructor
    public DeleteModel(AdminService adminService, EnemyConfigService enemyConfigService)
    {
        _adminService = adminService;
        _enemyConfigService = enemyConfigService;
    }
    #endregion

    #region Public Methods
    public async Task<IActionResult> OnGetAsync(int id)
    {
        Enemy = await _adminService.GetEnemyAsync(id);
        if (Enemy == null)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var enemy = await _adminService.GetEnemyAsync(id);
        if (enemy == null)
        {
            return NotFound();
        }

        var typeId = enemy.TypeId;
        var deleted = await _adminService.DeleteEnemyAsync(id);
        
        if (deleted)
        {
            // Invalidate cache
            await _enemyConfigService.InvalidateCacheAsync(typeId);
        }

        return RedirectToPage("./Index");
    }
    #endregion
}



















