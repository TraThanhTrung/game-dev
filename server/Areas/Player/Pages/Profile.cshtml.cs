using GameServer.Areas.Player.Models;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace GameServer.Areas.Player.Pages;

/// <summary>
/// Profile page for players to view their profile information.
/// </summary>
public class ProfileModel : BasePlayerPageModel
{
    #region Private Fields
    private readonly PlayerWebService _playerWebService;
    private readonly ILogger<ProfileModel> _logger;
    #endregion

    #region Public Properties
    public PlayerProfileViewModel? Profile { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
    #endregion

    #region Constructor
    public ProfileModel(PlayerWebService playerWebService, ILogger<ProfileModel> logger)
    {
        _playerWebService = playerWebService;
        _logger = logger;
    }
    #endregion

    #region Public Methods
    public async Task<IActionResult> OnGetAsync(string? verified, string? error)
    {
        if (!PlayerId.HasValue)
        {
            return RedirectToLogin();
        }

        // Check for verification result
        if (verified == "success")
        {
            SuccessMessage = "Email verified successfully via Google OAuth!";
        }
        else if (error == "email_mismatch")
        {
            ErrorMessage = "Email verification failed. The Google account email does not match your registered email.";
        }

        // Load player profile
        var player = await _playerWebService.GetPlayerProfileAsync(PlayerId.Value);
        if (player == null)
        {
            return RedirectToLogin();
        }

        // Map to view model
        Profile = new PlayerProfileViewModel
        {
            PlayerId = player.Id,
            Name = player.Name,
            Email = player.Email,
            EmailVerified = player.EmailVerified,
            HasGoogleAccount = !string.IsNullOrEmpty(player.GoogleId),
            AvatarPath = player.AvatarPath,
            Level = player.Level,
            Exp = player.Exp,
            ExpToLevel = player.ExpToLevel,
            Gold = player.Gold,
            CreatedAt = player.CreatedAt,
            InventoryItemCount = player.Inventory?.Count ?? 0,
            SkillCount = player.Skills?.Count ?? 0,
            Stats = new PlayerStatsViewModel
            {
                Damage = player.Stats?.Damage ?? 0,
                Range = player.Stats?.Range ?? 0,
                KnockbackForce = player.Stats?.KnockbackForce ?? 0,
                Speed = player.Stats?.Speed ?? 0,
                MaxHealth = player.Stats?.MaxHealth ?? 0,
                CurrentHealth = player.Stats?.CurrentHealth ?? 0
            }
        };

        return Page();
    }

    public async Task<IActionResult> OnPostUpdateEmailAsync(string? newEmail)
    {
        if (!PlayerId.HasValue)
        {
            return RedirectToLogin();
        }

        if (string.IsNullOrWhiteSpace(newEmail))
        {
            ErrorMessage = "Email is required";
            await LoadProfileAsync();
            return Page();
        }

        var result = await _playerWebService.UpdateEmailAsync(PlayerId.Value, newEmail);

        if (result.Success)
        {
            SuccessMessage = "Email updated successfully. Please verify your new email.";
        }
        else
        {
            ErrorMessage = result.ErrorMessage ?? "Failed to update email";
        }

        await LoadProfileAsync();
        return Page();
    }

    // Removed OnPostVerifyEmailAsync - now using Google OAuth verification via /auth/verify-email

    public async Task<IActionResult> OnPostUploadAvatarAsync(IFormFile? avatarFile)
    {
        if (!PlayerId.HasValue)
        {
            return RedirectToLogin();
        }

        if (avatarFile == null || avatarFile.Length == 0)
        {
            ErrorMessage = "Please select an image file to upload.";
            await LoadProfileAsync();
            return Page();
        }

        // Validate file type
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var fileExtension = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(fileExtension))
        {
            ErrorMessage = "Invalid file type. Please upload an image file (JPG, PNG, GIF, or WEBP).";
            await LoadProfileAsync();
            return Page();
        }

        // Validate file size (max 5MB)
        if (avatarFile.Length > 5 * 1024 * 1024)
        {
            ErrorMessage = "File size is too large. Maximum size is 5MB.";
            await LoadProfileAsync();
            return Page();
        }

        try
        {
            // Create avatars directory if it doesn't exist
            var avatarsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "avatars");
            if (!Directory.Exists(avatarsDir))
            {
                Directory.CreateDirectory(avatarsDir);
            }

            // Generate unique filename
            var fileName = $"{PlayerId.Value}{fileExtension}";
            var filePath = Path.Combine(avatarsDir, fileName);

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await avatarFile.CopyToAsync(stream);
            }

            // Update database
            var avatarUrl = $"/avatars/{fileName}";
            var result = await _playerWebService.UpdateAvatarAsync(PlayerId.Value, avatarUrl);

            if (result.Success)
            {
                SuccessMessage = "Avatar updated successfully!";
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Failed to update avatar.";
                // Delete uploaded file if database update failed
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading avatar for player: {Id}", PlayerId.Value);
            ErrorMessage = "An error occurred while uploading the avatar. Please try again.";
        }

        await LoadProfileAsync();
        return Page();
    }

    #region Private Methods
    private async Task LoadProfileAsync()
    {
        if (!PlayerId.HasValue) return;

        var player = await _playerWebService.GetPlayerProfileAsync(PlayerId.Value);
        if (player == null) return;

        Profile = new PlayerProfileViewModel
        {
            PlayerId = player.Id,
            Name = player.Name,
            Email = player.Email,
            EmailVerified = player.EmailVerified,
            HasGoogleAccount = !string.IsNullOrEmpty(player.GoogleId),
            AvatarPath = player.AvatarPath,
            Level = player.Level,
            Exp = player.Exp,
            ExpToLevel = player.ExpToLevel,
            Gold = player.Gold,
            CreatedAt = player.CreatedAt,
            InventoryItemCount = player.Inventory?.Count ?? 0,
            SkillCount = player.Skills?.Count ?? 0,
            Stats = new PlayerStatsViewModel
            {
                Damage = player.Stats?.Damage ?? 0,
                Range = player.Stats?.Range ?? 0,
                KnockbackForce = player.Stats?.KnockbackForce ?? 0,
                Speed = player.Stats?.Speed ?? 0,
                MaxHealth = player.Stats?.MaxHealth ?? 0,
                CurrentHealth = player.Stats?.CurrentHealth ?? 0
            }
        };
    }
    #endregion
    #endregion
}


