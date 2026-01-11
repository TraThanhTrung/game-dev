using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GameServer.Areas.Admin.Pages.Checkpoints;

[Authorize]
public class IndexModel : PageModel
{
    #region Private Fields
    private readonly AdminService _adminService;
    #endregion

    #region Public Properties
    public List<GameServer.Models.Entities.Checkpoint> Checkpoints { get; set; } = new();
    public List<GameServer.Models.Entities.GameSection> GameSections { get; set; } = new();
    public int? FilterSectionId { get; set; }
    #endregion

    #region Constructor
    public IndexModel(AdminService adminService)
    {
        _adminService = adminService;
    }
    #endregion

    #region Public Methods
    public async Task OnGetAsync(int? sectionId = null)
    {
        FilterSectionId = sectionId;
        
        // Load all GameSections for filter dropdown
        GameSections = await _adminService.GetGameSectionsAsync();
        
        // Load checkpoints - filter by sectionId if provided (using service method)
        Checkpoints = await _adminService.GetCheckpointsAsync(sectionId);
    }
    #endregion
}
