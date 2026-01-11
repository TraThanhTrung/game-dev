using GameServer.Models.Dto;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Controllers;

[ApiController]
[Route("rooms")]
public class RoomsController : ControllerBase
{
    private readonly WorldService _world;
    private readonly SessionTrackingService _tracking;
    private readonly PlayerService _playerService;
    private readonly ILogger<RoomsController> _logger;

    public RoomsController(
        WorldService world,
        SessionTrackingService tracking,
        PlayerService playerService,
        ILogger<RoomsController> logger)
    {
        _world = world;
        _tracking = tracking;
        _playerService = playerService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new room. Creates GameSession in DB and SessionState in memory.
    /// </summary>
    [HttpPost("create")]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request)
    {
        if (request == null)
            return BadRequest("Request body is required");

        // Get PlayerId (handles both Guid and string formats from Unity)
        var playerId = request.GetPlayerId();
        if (playerId == Guid.Empty)
        {
            return BadRequest("PlayerId required and must be a valid GUID");
        }

        // Validate player exists
        var player = await _playerService.GetPlayerAsync(playerId);
        if (player == null)
        {
            return Unauthorized("Invalid player");
        }

        HttpContext.Items["playerId"] = playerId.ToString();

        // 1. Create GameSession in DB (SessionTrackingService.StartSessionAsync)
        var gameSession = await _tracking.StartSessionAsync(playerCount: 1);

        // 2. Create SessionState in WorldService (in-memory)
        _world.CreateRoom(gameSession.SessionId);

        // 3. Track player join in DB
        await _tracking.TrackPlayerJoinAsync(gameSession.SessionId, playerId);

        // 4. Move player to the new room session
        var joinRequest = new JoinSessionRequest
        {
            PlayerId = playerId,
            PlayerName = player.Name,
            SessionId = gameSession.SessionId.ToString(),
            Token = request.Token
        };
        _world.JoinSession(joinRequest);

        _logger.LogInformation("Room created and joined: {RoomId} by player {PlayerId}",
            gameSession.SessionId, playerId);

        // 5. Return Room ID (GameSession.SessionId as GUID string)
        return Ok(new CreateRoomResponse
        {
            RoomId = gameSession.SessionId.ToString()
        });
    }

    /// <summary>
    /// Join a room with Room ID. Creates SessionPlayer in DB and joins SessionState in memory.
    /// </summary>
    [HttpPost("join")]
    public async Task<IActionResult> JoinRoom([FromBody] JoinRoomRequest request)
    {
        if (request == null)
            return BadRequest("Request body is required");

        // Get PlayerId (handles both Guid and string formats from Unity)
        var playerId = request.GetPlayerId();
        if (playerId == Guid.Empty)
        {
            return BadRequest("PlayerId required and must be a valid GUID");
        }

        if (string.IsNullOrWhiteSpace(request.RoomId))
            return BadRequest("RoomId required");

        // Validate player exists
        var player = await _playerService.GetPlayerAsync(playerId);
        if (player == null)
        {
            return Unauthorized("Invalid player");
        }

        // 1. Validate Room ID exists in DB (GameSessions table)
        if (!Guid.TryParse(request.RoomId, out var sessionId))
        {
            return BadRequest("Invalid Room ID format");
        }

        var gameSession = await _tracking.GetSessionAsync(sessionId);
        if (gameSession == null)
        {
            return NotFound("Room not found");
        }

        if (gameSession.Status != "Active")
        {
            return BadRequest("Room is not active");
        }

        HttpContext.Items["playerId"] = playerId.ToString();

        // 2. Create SessionPlayer in DB (SessionTrackingService.TrackPlayerJoinAsync)
        await _tracking.TrackPlayerJoinAsync(sessionId, playerId);

        // 3. Join SessionState in WorldService
        var joinRequest = new JoinSessionRequest
        {
            PlayerId = playerId,
            PlayerName = player.Name,
            SessionId = request.RoomId,
            Token = request.Token
        };

        _world.JoinSession(joinRequest);

        _logger.LogInformation("Player {PlayerId} joined room {RoomId}",
            playerId, request.RoomId);

        // 4. Return success
        return Ok(new JoinRoomResponse
        {
            Success = true,
            RoomId = request.RoomId
        });
    }

    /// <summary>
    /// Get room info (player count, status) from DB.
    /// </summary>
    [HttpGet("{roomId}")]
    public async Task<IActionResult> GetRoomInfo([FromRoute] string roomId)
    {
        if (!Guid.TryParse(roomId, out var sessionId))
        {
            return BadRequest("Invalid Room ID format");
        }

        var gameSession = await _tracking.GetSessionAsync(sessionId);
        if (gameSession == null)
        {
            return NotFound("Room not found");
        }

        return Ok(new
        {
            roomId = gameSession.SessionId.ToString(),
            playerCount = gameSession.PlayerCount,
            status = gameSession.Status,
            startTime = gameSession.StartTime
        });
    }
}

