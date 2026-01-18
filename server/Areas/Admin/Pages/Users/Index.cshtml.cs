using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GameServer.Areas.Admin.Pages.Users;

[Authorize]
public class IndexModel : PageModel
{
    #region Private Fields
    private readonly AdminService _adminService;
    #endregion

    #region Public Properties
    public List<GameServer.Models.Entities.PlayerProfile> Users { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalUsers { get; set; }
    public string? SearchTerm { get; set; }
    #endregion

    #region Constructor
    public IndexModel(AdminService adminService)
    {
        _adminService = adminService;
    }
    #endregion

    #region Public Methods
    public async Task OnGetAsync(int page = 1, string? search = null)
    {
        CurrentPage = page;
        SearchTerm = search;
        const int pageSize = 20;

        var (users, total) = await _adminService.GetUsersAsync(page, pageSize, search);
        Users = users;
        TotalUsers = total;
        TotalPages = (int)Math.Ceiling(total / (double)pageSize);
    }
    #endregion
}


















