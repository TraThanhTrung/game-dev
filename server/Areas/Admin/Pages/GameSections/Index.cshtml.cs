using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GameServer.Areas.Admin.Pages.GameSections;

[Authorize]
public class IndexModel : PageModel
{
    #region Private Fields
    private readonly AdminService _adminService;
    #endregion

    #region Public Properties
    public List<GameServer.Models.Entities.GameSection> GameSections { get; set; } = new();
    public Dictionary<int, int> CheckpointCounts { get; set; } = new();
    #endregion

    #region Constructor
    public IndexModel(AdminService adminService)
    {
        _adminService = adminService;
    }
    #endregion

    #region Public Methods
    public async Task OnGetAsync()
    {
        GameSections = await _adminService.GetGameSectionsAsync();
        
        // Load checkpoint counts for each section
        foreach (var section in GameSections)
        {
            var count = await _adminService.GetCheckpointCountForSectionAsync(section.SectionId);
            CheckpointCounts[section.SectionId] = count;
        }
    }
    #endregion
}








