using GameServer.Models.Dto;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly WorldService _world;

    public AuthController(WorldService world)
    {
        _world = world;
    }

    [HttpPost("register")]
    public ActionResult<RegisterResponse> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerName))
        {
            return BadRequest("PlayerName is required");
        }

        var result = _world.RegisterPlayer(request.PlayerName);
        return Ok(result);
    }
}

