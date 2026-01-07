using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GameServer.Pages;

/// <summary>
/// Root index page - shows Player Login if not authenticated, or redirects to Player Dashboard if authenticated.
/// </summary>
[AllowAnonymous]
public class IndexModel : PageModel
{
    #region Private Fields
    private readonly PlayerWebService _playerWebService;
    private readonly ILogger<IndexModel> _logger;
    #endregion

    #region Public Properties
    [BindProperty]
    public string? Username { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    public string? ErrorMessage { get; set; }
    public bool IsGmailLogin { get; set; }
    public string? GmailEmail { get; set; }
    #endregion

    #region Constructor
    public IndexModel(PlayerWebService playerWebService, ILogger<IndexModel> logger)
    {
        _playerWebService = playerWebService;
        _logger = logger;
    }
    #endregion

    #region Public Methods
    public IActionResult OnGet(string? gmail = null, string? email = null, string? error = null)
    {
        // Check if player is logged in
        var playerId = HttpContext.Session.GetString("PlayerId");
        var playerName = HttpContext.Session.GetString("PlayerName");

        if (!string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(playerName))
        {
            // Player is logged in, redirect to Player Dashboard
            return Redirect("/Player");
        }

        // Clear Gmail state if there's an error
        if (!string.IsNullOrEmpty(error))
        {
            HttpContext.Session.Remove("GmailEmail");
            HttpContext.Session.Remove("GoogleId");
            HttpContext.Session.Remove("GoogleName");
            ErrorMessage = "Gmail authentication failed. Please try again.";
            return Page();
        }

        // Only show Gmail login state if this is a direct callback from Google OAuth
        // Check if this is a Gmail login callback with query parameter
        if (gmail == "success" && !string.IsNullOrEmpty(email))
        {
            // Verify that session has matching Gmail info (prevent stale state)
            var sessionGmailEmail = HttpContext.Session.GetString("GmailEmail");
            var sessionGoogleId = HttpContext.Session.GetString("GoogleId");

            if (!string.IsNullOrEmpty(sessionGmailEmail) &&
                !string.IsNullOrEmpty(sessionGoogleId) &&
                sessionGmailEmail.Equals(email, StringComparison.OrdinalIgnoreCase))
            {
                IsGmailLogin = true;
                GmailEmail = email;
            }
            else
            {
                // Session doesn't match or is stale, clear it
                HttpContext.Session.Remove("GmailEmail");
                HttpContext.Session.Remove("GoogleId");
                HttpContext.Session.Remove("GoogleName");
            }
        }
        else
        {
            // Not a Gmail callback - clear any stale Gmail session data
            HttpContext.Session.Remove("GmailEmail");
            HttpContext.Session.Remove("GoogleId");
            HttpContext.Session.Remove("GoogleName");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? username, string? password, string? gmailUsername = null)
    {
        // Handle Gmail login flow - user enters username after Gmail auth
        var gmailEmail = HttpContext.Session.GetString("GmailEmail");
        var googleId = HttpContext.Session.GetString("GoogleId");

        // Check if this is a Gmail login flow
        if (!string.IsNullOrEmpty(gmailEmail) && !string.IsNullOrEmpty(googleId))
        {
            // Validate username for Gmail login
            if (string.IsNullOrWhiteSpace(gmailUsername))
            {
                ErrorMessage = "Username is required. Please enter your desired username.";
                IsGmailLogin = true;
                GmailEmail = gmailEmail;
                return Page();
            }

            // Trim and validate username
            gmailUsername = gmailUsername.Trim();
            if (gmailUsername.Length < 3)
            {
                ErrorMessage = "Username must be at least 3 characters long.";
                IsGmailLogin = true;
                GmailEmail = gmailEmail;
                return Page();
            }

            // Find or create player with Gmail
            var result = await _playerWebService.CreateOrLinkGoogleAccountAsync(
                googleId,  // Use real GoogleId instead of email
                gmailEmail,
                gmailUsername
            );

            if (result.Success)
            {
                // Set session and clear Gmail info
                HttpContext.Session.SetString("PlayerId", result.PlayerId.ToString());
                var player = await _playerWebService.GetPlayerProfileAsync(result.PlayerId);
                if (player != null)
                {
                    HttpContext.Session.SetString("PlayerName", player.Name);
                }
                HttpContext.Session.Remove("GmailEmail");
                HttpContext.Session.Remove("GoogleId");
                HttpContext.Session.Remove("GoogleName");

                _logger.LogInformation("Player logged in via Gmail: {Name} (Email: {Email}, ID: {Id})",
                    player?.Name ?? gmailUsername, gmailEmail, result.PlayerId);

                return Redirect("/Player");
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Failed to create account. Please try again.";
                IsGmailLogin = true;
                GmailEmail = gmailEmail;
                return Page();
            }
        }

        // Regular username/password login
        if (string.IsNullOrWhiteSpace(username))
        {
            ErrorMessage = "Username is required.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "Password is required.";
            Username = username;
            return Page();
        }

        // Find player by username
        var playerProfile = await _playerWebService.GetPlayerProfileByNameAsync(username.Trim());

        if (playerProfile == null)
        {
            ErrorMessage = "Invalid username or password.";
            Username = username;
            return Page();
        }

        // Verify password
        if (!_playerWebService.VerifyPassword(playerProfile, password))
        {
            ErrorMessage = "Invalid username or password.";
            Username = username;
            return Page();
        }

        // Set session variables
        HttpContext.Session.SetString("PlayerId", playerProfile.Id.ToString());
        HttpContext.Session.SetString("PlayerName", playerProfile.Name);

        _logger.LogInformation("Player logged in: {Name} (ID: {Id})", playerProfile.Name, playerProfile.Id);

        // Redirect to Player Dashboard
        return Redirect("/Player");
    }
    #endregion
}

