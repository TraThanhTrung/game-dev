using GameServer.Models.Dto;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers;

[ApiController]
[Route("sessions")]
public class SessionsController : ControllerBase
{
    private readonly WorldService _world;

    public SessionsController(WorldService world)
    {
        _world = world;
    }

    [HttpPost("join")]
    public IActionResult Join([FromBody] JoinSessionRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        _world.JoinSession(request);
        return Ok(new JoinSessionResponse { SessionId = request.SessionId });
    }

    [HttpPost("input")]
    public IActionResult SendInput([FromBody] InputRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

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
}

