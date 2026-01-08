using GameServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Areas.Player.Pages;

/// <summary>
/// Change password page for players.
/// </summary>
public class ChangePasswordModel : BasePlayerPageModel
{
    #region Private Fields
    private readonly PlayerWebService _playerWebService;
    private readonly ILogger<ChangePasswordModel> _logger;
    #endregion

    #region Public Properties
    [BindProperty]
    public string? CurrentPassword { get; set; }

    [BindProperty]
    public string? NewPassword { get; set; }

    [BindProperty]
    public string? ConfirmPassword { get; set; }

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasPassword { get; set; }
    #endregion

    #region Constructor
    public ChangePasswordModel(PlayerWebService playerWebService, ILogger<ChangePasswordModel> logger)
    {
        _playerWebService = playerWebService;
        _logger = logger;
    }
    #endregion

    #region Public Methods
    public async Task<IActionResult> OnGetAsync()
    {
        if (!PlayerId.HasValue)
        {
            return RedirectToLogin();
        }

        var player = await _playerWebService.GetPlayerProfileAsync(PlayerId.Value);
        if (player == null)
        {
            return RedirectToLogin();
        }

        // Check if player has a password (not Gmail-only account)
        HasPassword = !string.IsNullOrEmpty(player.PasswordHash);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!PlayerId.HasValue)
        {
            return RedirectToLogin();
        }

        var player = await _playerWebService.GetPlayerProfileAsync(PlayerId.Value);
        if (player == null)
        {
            return RedirectToLogin();
        }

        HasPassword = !string.IsNullOrEmpty(player.PasswordHash);

        // Validate inputs
        if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            ErrorMessage = "Current password is required";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            ErrorMessage = "New password is required";
            return Page();
        }

        if (NewPassword.Length < 4)
        {
            ErrorMessage = "New password must be at least 4 characters long";
            return Page();
        }

        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "New password and confirm password do not match";
            return Page();
        }

        // Change password
        var result = await _playerWebService.ChangePasswordAsync(
            PlayerId.Value, 
            CurrentPassword, 
            NewPassword);

        if (result.Success)
        {
            SuccessMessage = "Password changed successfully!";
            CurrentPassword = null;
            NewPassword = null;
            ConfirmPassword = null;
        }
        else
        {
            ErrorMessage = result.ErrorMessage ?? "Failed to change password";
        }

        return Page();
    }
    #endregion
}



