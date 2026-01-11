using GameServer.Models.Entities;
using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GameServer.Areas.Admin.Pages.Checkpoints;

[Authorize]
public class CreateModel : PageModel
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
    public CreateModel(AdminService adminService)
    {
        _adminService = adminService;
    }
    #endregion

    #region Public Methods
    public async Task<IActionResult> OnGetAsync(int? sectionId = null)
    {
        // Load active GameSections for dropdown
        GameSections = await _adminService.GetGameSectionsAsync();
        GameSections = GameSections.Where(s => s.IsActive).ToList();
        
        // Pre-select section if provided (e.g., when coming from GameSection details page)
        if (sectionId.HasValue)
        {
            Checkpoint.SectionId = sectionId.Value;
        }
        
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

        await _adminService.CreateCheckpointAsync(Checkpoint);
        
        // Redirect to filtered index if sectionId was provided
        if (Checkpoint.SectionId.HasValue)
        {
            return RedirectToPage("./Index", new { sectionId = Checkpoint.SectionId.Value });
        }
        
        return RedirectToPage("./Index");
    }
    #endregion
}
