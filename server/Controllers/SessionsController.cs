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
}

