using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Cryptography;
using System.Text;

namespace GameServer.Pages;

/// <summary>
/// Register page for players.
/// </summary>
[AllowAnonymous]
public class RegisterModel : PageModel
{
    #region Private Fields
    private readonly PlayerWebService _playerWebService;
    private readonly ILogger<RegisterModel> _logger;
    #endregion

    #region Public Properties
    [BindProperty]
    public string? Username { get; set; }

    [BindProperty]
    public string? Email { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    [BindProperty]
    public string? ConfirmPassword { get; set; }

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    #endregion

    #region Constructor
    public RegisterModel(PlayerWebService playerWebService, ILogger<RegisterModel> logger)
    {
        _playerWebService = playerWebService;
        _logger = logger;
    }
    #endregion

    #region Public Methods
    public IActionResult OnGet()
    {
        // Check if already logged in
        var playerId = HttpContext.Session.GetString("PlayerId");
        if (!string.IsNullOrEmpty(playerId))
        {
            return Redirect("/Player");
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Validation
        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = "Username is required.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Email is required.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Password is required.";
            return Page();
        }

        if (Password != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            return Page();
        }

        if (Password.Length < 6)
        {
            ErrorMessage = "Password must be at least 6 characters.";
            return Page();
        }

        // Check if username already exists
        var existingPlayer = await _playerWebService.GetPlayerProfileByNameAsync(Username);
        if (existingPlayer != null)
        {
            ErrorMessage = "Username already exists. Please choose a different one.";
            return Page();
        }

        // Check if email already exists
        var existingEmail = await _playerWebService.GetPlayerProfileByEmailAsync(Email);
        if (existingEmail != null)
        {
            ErrorMessage = "Email already registered. Please use a different email or login.";
            return Page();
        }

        // Create new player
        var result = await _playerWebService.CreatePlayerAccountAsync(Username, Email, Password);
        if (result.Success)
        {
            SuccessMessage = "Account created successfully! You can now login.";
            _logger.LogInformation("New player registered: {Name} (Email: {Email})", Username, Email);
            
            // Auto login after registration
            HttpContext.Session.SetString("PlayerId", result.PlayerId.ToString());
            HttpContext.Session.SetString("PlayerName", Username);
            
            return Redirect("/Player");
        }
        else
        {
            ErrorMessage = result.ErrorMessage ?? "Failed to create account. Please try again.";
            return Page();
        }
    }
    #endregion
}

