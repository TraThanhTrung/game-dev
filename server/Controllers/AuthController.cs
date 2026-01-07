using GameServer.Models.Dto;
using GameServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
        var redirectUrl = Url.Action("GoogleCallback", "Auth", null, Request.Scheme);
        var properties = new AuthenticationProperties 
        { 
            RedirectUri = redirectUrl 
        };
        return Challenge(properties, "Google");
    }

    /// <summary>
    /// Google OAuth callback - handles response from Google
    /// </summary>
    [HttpGet("google-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleCallback()
    {
        // Get Google user info from claims
        var result = await HttpContext.AuthenticateAsync("Google");
        if (!result.Succeeded)
        {
            _logger.LogWarning("Google authentication failed");
            return Redirect("/?error=google_auth_failed");
        }

        // Extract Google user information
        var googleId = result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = result.Principal.FindFirst(ClaimTypes.Email)?.Value;
        var name = result.Principal.FindFirst(ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(googleId) || string.IsNullOrEmpty(email))
        {
            _logger.LogWarning("Google authentication missing required claims");
            return Redirect("/?error=google_auth_incomplete");
        }

        // Check if user already exists
        var existingPlayer = await _playerWebService.GetPlayerProfileByGoogleIdAsync(googleId);
        
        if (existingPlayer != null)
        {
            // User exists, login directly
            HttpContext.Session.SetString("PlayerId", existingPlayer.Id.ToString());
            HttpContext.Session.SetString("PlayerName", existingPlayer.Name);
            
            // Sign out the external cookie
            await HttpContext.SignOutAsync("Google");
            
            _logger.LogInformation("Player logged in via Google: {Name} (Email: {Email}, ID: {Id})", 
                existingPlayer.Name, email, existingPlayer.Id);
            
            return Redirect("/Player");
        }

        // New user - store Google info in session and redirect to username setup
        HttpContext.Session.SetString("GmailEmail", email);
        HttpContext.Session.SetString("GoogleId", googleId);
        HttpContext.Session.SetString("GoogleName", name ?? email.Split('@')[0]);
        
        // Sign out the external cookie
        await HttpContext.SignOutAsync("Google");
        
        return Redirect($"/?gmail=success&email={Uri.EscapeDataString(email)}");
    }
}
