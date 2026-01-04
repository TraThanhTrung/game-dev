using GameServer.Models.Dto;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly WorldService _world;
    private readonly PlayerService _playerService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(WorldService world, PlayerService playerService, ILogger<AuthController> logger)
    {
        _world = world;
        _playerService = playerService;
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
            return NotFound("Player not found");
        }

        return Ok(new PlayerProfileDto
        {
            PlayerId = player.Id,
            Name = player.Name,
            Level = player.Level,
            Exp = player.Exp,
            Gold = player.Gold,
            MaxHealth = player.Stats.MaxHealth,
            CurrentHealth = player.Stats.CurrentHealth,
            Damage = player.Stats.Damage,
            Speed = player.Stats.Speed
        });
    }
}

// DTO for profile response
public class PlayerProfileDto
{
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public int Exp { get; set; }
    public int Gold { get; set; }
    public int MaxHealth { get; set; }
    public int CurrentHealth { get; set; }
    public int Damage { get; set; }
    public float Speed { get; set; }
}
