using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GameServer.Areas.Admin.Pages;

public class LogoutModel : PageModel
{
    #region Private Fields
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly ILogger<LogoutModel> _logger;
    #endregion

    #region Constructor
    public LogoutModel(
        SignInManager<IdentityUser> signInManager,
        ILogger<LogoutModel> logger)
    {
        _signInManager = signInManager;
        _logger = logger;
    }
    #endregion

    #region Public Methods
    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            var username = User.Identity?.Name ?? "Unknown";

            // Sign out user - this deletes the authentication cookie
            await _signInManager.SignOutAsync();

            // Explicitly delete cookie to ensure it's removed
            var deleteCookieOptions = new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(-1),
                Path = "/",
                HttpOnly = true,
                Secure = false,
                SameSite = SameSiteMode.Lax
            };

            Response.Cookies.Append(".AspNetCore.Identity.Application", string.Empty, deleteCookieOptions);

            _logger.LogInformation("Admin user {Username} logged out successfully", username);

            // Redirect to login page
            return Redirect("/Admin/Login");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return Redirect("/Admin/Login");
        }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            var username = User.Identity?.Name ?? "Unknown";

            await _signInManager.SignOutAsync();

            // Delete cookie explicitly
            var deleteCookieOptions = new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(-1),
                Path = "/",
                HttpOnly = true,
                Secure = false,
                SameSite = SameSiteMode.Lax
            };

            Response.Cookies.Append(".AspNetCore.Identity.Application", string.Empty, deleteCookieOptions);

            _logger.LogInformation("Admin user {Username} logged out successfully (GET)", username);

            return Redirect("/Admin/Login");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout (GET)");
            return Redirect("/Admin/Login");
        }
    }
    #endregion
}

