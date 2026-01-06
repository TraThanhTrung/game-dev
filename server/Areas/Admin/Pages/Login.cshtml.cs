using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace GameServer.Areas.Admin.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    #region Private Fields
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly ILogger<LoginModel> _logger;
    #endregion

    #region Public Properties
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }
    #endregion

    #region Constructor
    public LoginModel(
        SignInManager<IdentityUser> signInManager,
        ILogger<LoginModel> logger)
    {
        _signInManager = signInManager;
        _logger = logger;
    }
    #endregion

    #region Public Methods
    public void OnGet(string? returnUrl = null)
    {
        // If user is already authenticated, redirect to dashboard
        if (User.Identity?.IsAuthenticated == true)
        {
            Response.Redirect(Url.Content("~/Admin"));
            return;
        }

        // Prevent redirect loop - if returnUrl is Login page, use default
        if (!string.IsNullOrEmpty(returnUrl) && 
            (returnUrl.Contains("/Login", StringComparison.OrdinalIgnoreCase) || 
             returnUrl.Contains("%2FLogin", StringComparison.OrdinalIgnoreCase)))
        {
            ReturnUrl = Url.Content("~/Admin");
        }
        else
        {
            // Decode returnUrl to prevent double encoding
            ReturnUrl = string.IsNullOrEmpty(returnUrl) 
                ? Url.Content("~/Admin") 
                : Uri.UnescapeDataString(returnUrl);
        }
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        // Prevent redirect loop - if returnUrl is Login page, use default
        if (!string.IsNullOrEmpty(returnUrl) && returnUrl.Contains("/Login", StringComparison.OrdinalIgnoreCase))
        {
            ReturnUrl = Url.Content("~/Admin");
        }
        else
        {
            ReturnUrl = returnUrl ?? Url.Content("~/Admin");
        }

        if (ModelState.IsValid)
        {
            var result = await _signInManager.PasswordSignInAsync(
                Input.Username!,
                Input.Password!,
                Input.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("Admin user {Username} logged in", Input.Username);
                return LocalRedirect(ReturnUrl);
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("Admin user {Username} account locked out", Input.Username);
                ModelState.AddModelError(string.Empty, "Account locked out. Please try again later.");
                return Page();
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        }

        return Page();
    }
    #endregion

    #region Input Model
    public class InputModel
    {
        [Required]
        [Display(Name = "Username")]
        public string? Username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string? Password { get; set; }

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }
    #endregion
}

