using GameServer.Models.Dto;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers;

[ApiController]
[Route("sessions")]
public class SessionsController : ControllerBase
{
    private readonly WorldService _world;
    private readonly PlayerService _playerService;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(WorldService world, PlayerService playerService, ILogger<SessionsController> logger)
    {
        _world = world;
        _playerService = playerService;
        _logger = logger;
    }

    [HttpPost("join")]
    public IActionResult Join([FromBody] JoinSessionRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        HttpContext.Items["playerId"] = request.PlayerId.ToString();
        _world.JoinSession(request);
        return Ok(new JoinSessionResponse { SessionId = request.SessionId });
    }

    [HttpPost("input")]
    public IActionResult SendInput([FromBody] InputRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        HttpContext.Items["playerId"] = request.PlayerId.ToString();
        _world.EnqueueInput(request);
        return Ok(new { accepted = true });
    }

    [HttpGet("{sessionId}/state")]
    public ActionResult<StateResponse> GetState([FromRoute] string sessionId, [FromQuery] int? sinceVersion)
    {
        var state = _world.GetState(sessionId, sinceVersion);
        if (sinceVersion.HasValue && sinceVersion.Value >= state.Version && state.Players.Count == 0 && state.Enemies.Count == 0 && state.Projectiles.Count == 0)
        {
            return NoContent();
        }
        return Ok(state);
    }

    [HttpPost("{sessionId}/reset")]
    public IActionResult ResetSession([FromRoute] string sessionId)
    {
        _world.ResetSession(sessionId);
        return Ok(new { reset = true, sessionId });
    }

    /// <summary>
    /// Get session metadata for loading screen (players, room config).
    /// Called during loading screen to get session info before connecting SignalR.
    /// </summary>
    [HttpGet("{sessionId}/metadata")]
    public ActionResult<SessionMetadataResponse> GetSessionMetadata([FromRoute] string sessionId)
    {
        var roomInfo = _world.GetRoomInfo(sessionId);
        if (roomInfo == null)
        {
            return NotFound(new { message = "Session not found" });
        }

        var snapshot = _world.GetSessionSnapshot(sessionId);

        var response = new SessionMetadataResponse
        {
            SessionId = sessionId,
            PlayerCount = roomInfo.Value.playerCount,
            Version = roomInfo.Value.version,
            Players = snapshot?.Players.Select(p => new PlayerMetadata
            {
                Id = p.Id,
                Name = p.Name,
                CharacterType = p.CharacterType,
                Level = p.Level
            }).ToList() ?? new List<PlayerMetadata>()
        };

        return Ok(response);
    }

    /// <summary>
    /// Signal that client is ready to connect to SignalR.
    /// Called after loading screen completes resource validation.
    /// </summary>
    [HttpPost("{sessionId}/ready")]
    public IActionResult SignalReady([FromRoute] string sessionId, [FromBody] ReadyRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        HttpContext.Items["playerId"] = request.PlayerId.ToString();

        _logger.LogInformation("[Sessions] Player {PlayerId} ready for session {SessionId}",
            request.PlayerId.ToString()[..8], sessionId);

        return Ok(new { ready = true, sessionId });
    }

    /// <summary>
    /// Save player progress to database.
    /// </summary>
    [HttpPost("save")]
    public async Task<IActionResult> SaveProgress([FromBody] SaveProgressRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        HttpContext.Items["playerId"] = request.PlayerId.ToString();

        // Get current player state from WorldService
        var playerState = _world.GetPlayerState(request.PlayerId);
        if (playerState == null)
        {
            _logger.LogWarning("SaveProgress: Player not found in session: {Id}", request.PlayerId);
            return NotFound("Player not in session");
        }

        // Save to database
        await _playerService.SaveProgressAsync(
            request.PlayerId,
            playerState.Exp,
            playerState.Gold,
            playerState.Level,
            playerState.Hp
        );

        _logger.LogInformation("Saved progress for player {Id}: Level={Level}, Exp={Exp}, Gold={Gold}",
            request.PlayerId.ToString()[..8], playerState.Level, playerState.Exp, playerState.Gold);

        return Ok(new { saved = true });
    }

    /// <summary>
    /// Player disconnect - save and remove from session.
    /// </summary>
    [HttpPost("disconnect")]
    public async Task<IActionResult> Disconnect([FromBody] DisconnectRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        HttpContext.Items["playerId"] = request.PlayerId.ToString();

        // Get current player state and save before removing
        var playerState = _world.GetPlayerState(request.PlayerId);
        if (playerState != null)
        {
            await _playerService.SaveProgressAsync(
                request.PlayerId,
                playerState.Exp,
                playerState.Gold,
                playerState.Level,
                playerState.Hp
            );

            _logger.LogInformation("Player disconnected and saved: {Id} Level={Level}",
                request.PlayerId.ToString()[..8], playerState.Level);
        }

        // Remove player from session (optional: implement in WorldService)
        // _world.RemovePlayer(request.PlayerId);

        return Ok(new { disconnected = true });
    }

    /// <summary>
    /// Report a kill so the server can grant rewards based on enemy type.
    /// </summary>
    [HttpPost("kill")]
    public IActionResult ReportKill([FromBody] KillReportRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        HttpContext.Items["playerId"] = request.PlayerId.ToString();

        var granted = _world.ReportKill(request.PlayerId, request.EnemyTypeId);
        if (!granted)
        {
            return NotFound(new { granted = false, message = "Kill not applied" });
        }

        var playerState = _world.GetPlayerState(request.PlayerId);
        if (playerState == null)
        {
            return NotFound(new { granted = false, message = "Player state missing" });
        }

        return Ok(new KillReportResponse
        {
            Granted = true,
            Level = playerState.Level,
            Exp = playerState.Exp,
            Gold = playerState.Gold
        });
    }

    /// <summary>
    /// Report damage taken from enemy so server can update HP authoritatively.
    /// </summary>
    [HttpPost("damage")]
    public IActionResult ReportDamage([FromBody] DamageReportRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        if (request.DamageAmount <= 0)
            return BadRequest("DamageAmount must be positive");

        HttpContext.Items["playerId"] = request.PlayerId.ToString();

        var result = _world.ApplyDamage(request.PlayerId, request.DamageAmount);
        if (result == null)
        {
            return NotFound(new { accepted = false, message = "Player not found" });
        }

        return Ok(new DamageReportResponse
        {
            Accepted = true,
            CurrentHp = result.Value.hp,
            MaxHp = result.Value.maxHp
        });
    }

    /// <summary>
    /// Report damage from player to enemy so server can update enemy HP authoritatively.
    /// </summary>
    [HttpPost("enemy-damage")]
    public IActionResult ReportEnemyDamage([FromBody] EnemyDamageRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        if (request.EnemyId == Guid.Empty)
            return BadRequest("EnemyId required");

        if (request.DamageAmount <= 0)
            return BadRequest("DamageAmount must be positive");

        HttpContext.Items["playerId"] = request.PlayerId.ToString();

        var result = _world.ApplyDamageToEnemy(request.PlayerId, request.EnemyId, request.DamageAmount);
        if (result == null)
        {
            return NotFound(new { accepted = false, message = "Enemy not found or player not in session" });
        }

        return Ok(new EnemyDamageResponse
        {
            Accepted = true,
            CurrentHp = result.Value.hp,
            MaxHp = result.Value.maxHp,
            IsDead = result.Value.hp <= 0
        });
    }

    /// <summary>
    /// Respawn player at spawn position with 50% health.
    /// </summary>
    [HttpPost("respawn")]
    public IActionResult Respawn([FromBody] RespawnRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        HttpContext.Items["playerId"] = request.PlayerId.ToString();

        var result = _world.RespawnPlayer(request.PlayerId);
        if (result == null)
        {
            return NotFound(new { accepted = false, message = "Player not found" });
        }

        return Ok(new RespawnResponse
        {
            Accepted = true,
            X = result.Value.x,
            Y = result.Value.y,
            CurrentHp = result.Value.hp,
            MaxHp = result.Value.maxHp
        });
    }
}

