using GameServer.Models.Entities;
using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GameServer.Areas.Admin.Pages.Checkpoints;

[Authorize]
public class EditModel : PageModel
{
    #region Private Fields
    private readonly AdminService _adminService;
    #endregion

    #region Public Properties
    [BindProperty]
    public Checkpoint Checkpoint { get; set; } = new();
    public List<GameServer.Models.Entities.GameSection> GameSections { get; set; } = new();
    #endregion

    #region Constructor
    public EditModel(AdminService adminService)
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

        var checkpoint = await _adminService.GetCheckpointAsync(id.Value);
        if (checkpoint == null)
        {
            return NotFound();
        }

        Checkpoint = checkpoint;
        
        // Load active GameSections for dropdown
        GameSections = await _adminService.GetGameSectionsAsync();
        GameSections = GameSections.Where(s => s.IsActive).ToList();
        
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Reload GameSections for dropdown in case of validation errors
        GameSections = await _adminService.GetGameSectionsAsync();
        GameSections = GameSections.Where(s => s.IsActive).ToList();
        
        // Validate SectionId is provided
        if (!Checkpoint.SectionId.HasValue)
        {
            ModelState.AddModelError(nameof(Checkpoint.SectionId), "GameSection is required");
        }
        
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Validate enemy pool JSON format
        if (!string.IsNullOrWhiteSpace(Checkpoint.EnemyPool))
        {
            try
            {
                System.Text.Json.JsonSerializer.Deserialize<string[]>(Checkpoint.EnemyPool);
            }
            catch
            {
                ModelState.AddModelError(nameof(Checkpoint.EnemyPool), "EnemyPool must be a valid JSON array (e.g., [\"slime\", \"goblin\"])");
                return Page();
            }
        }
        else
        {
            Checkpoint.EnemyPool = "[]";
        }

        var updated = await _adminService.UpdateCheckpointAsync(Checkpoint.CheckpointId, Checkpoint);
        if (updated == null)
        {
            return NotFound();
        }

        // Redirect to filtered index if sectionId was provided
        if (Checkpoint.SectionId.HasValue)
        {
            return RedirectToPage("./Index", new { sectionId = Checkpoint.SectionId.Value });
        }
        
        return RedirectToPage("./Index");
    }
    #endregion
}
