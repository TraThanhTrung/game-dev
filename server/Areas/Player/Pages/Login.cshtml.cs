using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GameServer.Areas.Player.Pages;

/// <summary>
/// Login page for players - uses session-based authentication with PlayerName.
/// </summary>
[AllowAnonymous]
public class LoginModel : PageModel
{
    #region Private Fields
    private readonly PlayerWebService _playerWebService;
    private readonly ILogger<LoginModel> _logger;
    #endregion

    #region Public Properties
    [BindProperty]
    public string? PlayerName { get; set; }

    public string? ErrorMessage { get; set; }
    #endregion

    #region Constructor
    public LoginModel(PlayerWebService playerWebService, ILogger<LoginModel> logger)
    {
        _playerWebService = playerWebService;
        _logger = logger;
    }
    #endregion

    #region Public Methods
    public void OnGet(string? returnUrl = null)
    {
        // If already logged in, redirect to dashboard
        if (IsPlayerLoggedIn())
        {
            Response.Redirect(Url.Content("~/Player") ?? "/Player");
            return;
        }
    }

    public async Task<IActionResult> OnPostAsync(string? playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            ErrorMessage = "Player name is required.";
            PlayerName = playerName;
            return Page();
        }

        // Find player by name
        var player = await _playerWebService.GetPlayerProfileByNameAsync(playerName.Trim());

        if (player == null)
        {
            ErrorMessage = $"Player '{playerName}' not found. Please use the same name you use in the game.";
            PlayerName = playerName;
            return Page();
        }

        // Set session variables
        HttpContext.Session.SetString("PlayerId", player.Id.ToString());
        HttpContext.Session.SetString("PlayerName", player.Name);

        _logger.LogInformation("Player logged in: {Name} (ID: {Id})", player.Name, player.Id);

        // Redirect to dashboard
        return RedirectToPage("/Index");
    }

    private bool IsPlayerLoggedIn()
    {
        return !string.IsNullOrEmpty(HttpContext.Session.GetString("PlayerId"));
    }
    #endregion
}

