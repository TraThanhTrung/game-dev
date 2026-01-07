using GameServer.Models.Dto;
using GameServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Linq;

namespace GameServer.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly WorldService _world;
    private readonly PlayerService _playerService;
    private readonly PlayerWebService _playerWebService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        WorldService world,
        PlayerService playerService,
        PlayerWebService playerWebService,
        ILogger<AuthController> logger)
    {
        _world = world;
        _playerService = playerService;
        _playerWebService = playerWebService;
        _logger = logger;
    }

    /// <summary>
    /// Register or login player by name.
    /// If player with name exists, returns existing PlayerId.
    /// If not, creates new player and returns new PlayerId.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerName))
        {
            return BadRequest("PlayerName is required");
        }

        // Find or create player in database
        var (player, isNew) = await _playerService.FindOrCreatePlayerAsync(request.PlayerName);

        // Register in WorldService (in-memory state)
        var result = _world.RegisterOrLoadPlayer(player, isNew);

        _logger.LogInformation("{Action} player: {Name} (ID: {Id})",
            isNew ? "Created" : "Loaded", player.Name, player.Id);

        return Ok(result);
    }

    /// <summary>
    /// Login player with username and password.
    /// Verifies password and returns player session if credentials are valid.
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<RegisterResponse>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerName) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("PlayerName and Password are required");
        }

        // Find player by name
        var player = await _playerWebService.GetPlayerProfileByNameAsync(request.PlayerName);
        if (player == null)
        {
            return Unauthorized("Invalid username or password");
        }

        // Verify password
        if (!_playerWebService.VerifyPassword(player, request.Password))
        {
            return Unauthorized("Invalid username or password");
        }

        // Register in WorldService (load existing player)
        var result = _world.RegisterOrLoadPlayer(player, isNew: false);

        _logger.LogInformation("Player logged in: {Name} (ID: {Id})", player.Name, player.Id);

        return Ok(result);
    }

    /// <summary>
    /// Get player profile data (for loading saved game).
    /// </summary>
    [HttpGet("profile/{playerId}")]
    public async Task<ActionResult<PlayerProfileDto>> GetProfile(Guid playerId)
    {
        var player = await _playerService.GetPlayerAsync(playerId);
        if (player == null)
        {
            return NotFound();
        }

        return Ok(new PlayerProfileDto
        {
            PlayerId = player.Id,
            Name = player.Name,
            Level = player.Level,
            Exp = player.Exp,
            Gold = player.Gold
        });
    }

    /// <summary>
    /// Google OAuth login - redirects to Google OAuth
    /// </summary>
    [HttpGet("google")]
    [AllowAnonymous]
    public IActionResult GoogleAuth()
    {
        // Clear any existing Gmail session data before starting new OAuth flow
        HttpContext.Session.Remove("GmailEmail");
        HttpContext.Session.Remove("GoogleId");
        HttpContext.Session.Remove("GoogleName");
        HttpContext.Session.Remove("VerifyEmailPlayerId");

        // Set ReturnUrl to our callback handler
        // After Google authentication, middleware will redirect here
        var returnUrl = Url.Action("GoogleCallback", "Auth", null, Request.Scheme, Request.Host.Value);
        var properties = new AuthenticationProperties
        {
            RedirectUri = returnUrl,
            AllowRefresh = true
        };
        return Challenge(properties, "Google");
    }

    /// <summary>
    /// Google OAuth email verification - redirects to Google OAuth for email verification
    /// </summary>
    [HttpGet("verify-email")]
    public IActionResult VerifyEmail()
    {
        var playerIdStr = HttpContext.Session.GetString("PlayerId");
        if (string.IsNullOrEmpty(playerIdStr) || !Guid.TryParse(playerIdStr, out var playerId))
        {
            return Redirect("/");
        }

        // Store player ID in session for verification callback
        HttpContext.Session.SetString("VerifyEmailPlayerId", playerId.ToString());

        // Set ReturnUrl to our callback handler
        var returnUrl = Url.Action("GoogleCallback", "Auth", null, Request.Scheme, Request.Host.Value);
        var properties = new AuthenticationProperties
        {
            RedirectUri = returnUrl,
            AllowRefresh = true
        };
        return Challenge(properties, "Google");
    }

    /// <summary>
    /// Google OAuth callback - handles response from Google
    /// This is called AFTER Google OAuth middleware processes the callback
    /// The middleware signs in to "External" scheme, so we authenticate with that scheme
    /// </summary>
    [HttpGet("google-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleCallback()
    {
        try
        {
            _logger.LogInformation("Google callback received");

            // Authenticate with External scheme (where Google middleware signed in)
            var result = await HttpContext.AuthenticateAsync("External");

            if (!result.Succeeded)
            {
                _logger.LogWarning("Google authentication failed - no valid authentication found. Succeeded: {Succeeded}, Failure: {Failure}",
                    result.Succeeded, result.Failure?.Message);
                await HttpContext.SignOutAsync("External");
                return Redirect("/?error=google_auth_failed");
            }

            // Extract Google user information from claims
            var principal = result.Principal;
            if (principal == null)
            {
                _logger.LogWarning("Google authentication principal is null");
                await HttpContext.SignOutAsync("External");
                return Redirect("/?error=google_auth_failed");
            }

            var googleId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst("sub")?.Value;
            var email = principal.FindFirst(ClaimTypes.Email)?.Value
                ?? principal.FindFirst("email")?.Value;
            var name = principal.FindFirst(ClaimTypes.Name)?.Value
                ?? principal.FindFirst("name")?.Value;

            _logger.LogInformation("Google user info extracted - GoogleId: {GoogleId}, Email: {Email}, Name: {Name}",
                googleId ?? "null", email ?? "null", name ?? "null");

            if (string.IsNullOrEmpty(googleId) || string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("Google authentication missing required claims. Available claims: {Claims}",
                    string.Join(", ", principal.Claims.Select(c => $"{c.Type}={c.Value}")));
                await HttpContext.SignOutAsync("External");
                return Redirect("/?error=google_auth_incomplete");
            }

            // Clean up external authentication cookie
            await HttpContext.SignOutAsync("External");

            // Check if user already exists by GoogleId
            var existingPlayer = await _playerWebService.GetPlayerProfileByGoogleIdAsync(googleId);

            if (existingPlayer != null)
            {
                // User exists with GoogleId, login directly
                // Set email verified since they authenticated via Google OAuth
                if (!existingPlayer.EmailVerified && !string.IsNullOrEmpty(existingPlayer.Email))
                {
                    existingPlayer.EmailVerified = true;
                    await _playerWebService.SaveChangesAsync();
                }

                HttpContext.Session.SetString("PlayerId", existingPlayer.Id.ToString());
                HttpContext.Session.SetString("PlayerName", existingPlayer.Name);

                _logger.LogInformation("Player logged in via Google: {Name} (Email: {Email}, ID: {Id})",
                    existingPlayer.Name, email, existingPlayer.Id);

                return Redirect("/Player");
            }

            // Check if email already exists (user registered with username/password)
            var existingEmailPlayer = await _playerWebService.GetPlayerProfileByEmailAsync(email);

            if (existingEmailPlayer != null)
            {
                // Email already exists - link GoogleId to existing account and login
                if (string.IsNullOrEmpty(existingEmailPlayer.GoogleId))
                {
                    // Link GoogleId to existing account
                    var linkResult = await _playerWebService.LinkGoogleIdToExistingAccountAsync(googleId, email);

                    if (!linkResult.Success)
                    {
                        _logger.LogWarning("Failed to link GoogleId to existing account: {Email}, Error: {Error}",
                            email, linkResult.ErrorMessage);
                        return Redirect("/?error=google_link_failed");
                    }

                    _logger.LogInformation("Linked Google account to existing email: {Email} (PlayerId: {Id})",
                        email, linkResult.PlayerId);
                }

                // Get updated player info
                var player = await _playerWebService.GetPlayerProfileAsync(existingEmailPlayer.Id);
                if (player == null)
                {
                    return Redirect("/?error=google_auth_failed");
                }

                // Set email verified since they authenticated via Google OAuth
                if (!player.EmailVerified && !string.IsNullOrEmpty(player.Email))
                {
                    player.EmailVerified = true;
                    await _playerWebService.SaveChangesAsync();
                }

                // Login with existing account
                HttpContext.Session.SetString("PlayerId", player.Id.ToString());
                HttpContext.Session.SetString("PlayerName", player.Name);

                _logger.LogInformation("Player logged in via Google (existing email): {Name} (Email: {Email}, ID: {Id})",
                    player.Name, email, player.Id);

                return Redirect("/Player");
            }

            // Check if this is an email verification flow (not login)
            var verifyEmailPlayerId = HttpContext.Session.GetString("VerifyEmailPlayerId");
            if (!string.IsNullOrEmpty(verifyEmailPlayerId) && Guid.TryParse(verifyEmailPlayerId, out var playerId))
            {
                // This is email verification flow
                var verifyPlayer = await _playerWebService.GetPlayerProfileAsync(playerId);
                if (verifyPlayer != null && verifyPlayer.Email?.ToLower() == email.ToLower())
                {
                    // Email matches - verify it
                    verifyPlayer.EmailVerified = true;
                    if (string.IsNullOrEmpty(verifyPlayer.GoogleId))
                    {
                        verifyPlayer.GoogleId = googleId; // Link Google account
                    }
                    await _playerWebService.SaveChangesAsync();

                    HttpContext.Session.Remove("VerifyEmailPlayerId");

                    _logger.LogInformation("Email verified via Google OAuth: {Email} (PlayerId: {Id})", email, playerId);
                    return Redirect("/Player/Profile?verified=success");
                }
                else
                {
                    // Email doesn't match
                    HttpContext.Session.Remove("VerifyEmailPlayerId");
                    return Redirect("/Player/Profile?error=email_mismatch");
                }
            }

            // New user - store Google info in session and redirect to username setup
            HttpContext.Session.SetString("GmailEmail", email);
            HttpContext.Session.SetString("GoogleId", googleId);
            HttpContext.Session.SetString("GoogleName", name ?? email.Split('@')[0]);

            _logger.LogInformation("New Google user authenticated: {Email} (GoogleId: {GoogleId})", email, googleId);

            return Redirect($"/?gmail=success&email={Uri.EscapeDataString(email)}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Google callback");
            await HttpContext.SignOutAsync("External");
            return Redirect("/?error=google_auth_failed");
        }
    }
}
