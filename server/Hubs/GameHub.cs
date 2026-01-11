using Microsoft.AspNetCore.SignalR;
using GameServer.Services;
using GameServer.Models.Dto;
using System.Text.Json;

namespace GameServer.Hubs;

/// <summary>
/// SignalR Hub for real-time game state communication.
/// Handles player input and broadcasts game state updates at 20 Hz.
/// </summary>
public class GameHub : Hub
{
    #region Private Fields
    private readonly ILogger<GameHub> _logger;
    private readonly WorldService _worldService;
    private static readonly Dictionary<string, string> s_ConnectionToSession = new();
    private static readonly Dictionary<string, string> s_ConnectionToPlayer = new();
    private static readonly object s_Lock = new();
    #endregion

    #region Constructor
    public GameHub(ILogger<GameHub> logger, WorldService worldService)
    {
        _logger = logger;
        _worldService = worldService;
    }
    #endregion

    #region Connection Lifecycle
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("[GameHub] Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        
        lock (s_Lock)
        {
            if (s_ConnectionToSession.TryGetValue(connectionId, out var sessionId))
            {
                s_ConnectionToSession.Remove(connectionId);
                
                if (s_ConnectionToPlayer.TryGetValue(connectionId, out var playerId))
                {
                    s_ConnectionToPlayer.Remove(connectionId);
                    
                    // Notify other players in session
                    _ = Clients.Group(sessionId).SendAsync("OnPlayerLeft", new PlayerLeftEvent
                    {
                        PlayerId = playerId,
                        SessionId = sessionId
                    });
                    
                    _logger.LogInformation("[GameHub] Player {PlayerId} left session {SessionId}", playerId, sessionId);
                }
            }
        }
        
        if (exception != null)
        {
            _logger.LogWarning(exception, "[GameHub] Client disconnected with error: {ConnectionId}", connectionId);
        }
        else
        {
            _logger.LogInformation("[GameHub] Client disconnected: {ConnectionId}", connectionId);
        }
        
        await base.OnDisconnectedAsync(exception);
    }
    #endregion

    #region Client Methods
    /// <summary>
    /// Join a game session and subscribe to state updates.
    /// Must be called after connecting before receiving state updates.
    /// </summary>
    public async Task JoinSession(JoinSessionRequest request)
    {
        var connectionId = Context.ConnectionId;
        
        if (string.IsNullOrEmpty(request.SessionId) || string.IsNullOrEmpty(request.PlayerId))
        {
            await Clients.Caller.SendAsync("OnError", "Invalid session or player ID");
            return;
        }
        
        // Add to session group
        await Groups.AddToGroupAsync(connectionId, request.SessionId);
        
        lock (s_Lock)
        {
            s_ConnectionToSession[connectionId] = request.SessionId;
            s_ConnectionToPlayer[connectionId] = request.PlayerId;
        }
        
        // Notify other players
        await Clients.GroupExcept(request.SessionId, connectionId).SendAsync("OnPlayerJoined", new PlayerJoinedEvent
        {
            PlayerId = request.PlayerId,
            SessionId = request.SessionId,
            CharacterType = request.CharacterType ?? "lancer"
        });
        
        // Send initial state to the joining player
        var initialState = _worldService.GetSessionSnapshot(request.SessionId);
        if (initialState != null)
        {
            await Clients.Caller.SendAsync("ReceiveGameState", initialState);
        }
        
        _logger.LogInformation("[GameHub] Player {PlayerId} joined session {SessionId}", request.PlayerId, request.SessionId);
    }

    /// <summary>
    /// Leave the current session.
    /// </summary>
    public async Task LeaveSession()
    {
        var connectionId = Context.ConnectionId;
        
        lock (s_Lock)
        {
            if (s_ConnectionToSession.TryGetValue(connectionId, out var sessionId))
            {
                _ = Groups.RemoveFromGroupAsync(connectionId, sessionId);
                s_ConnectionToSession.Remove(connectionId);
                
                if (s_ConnectionToPlayer.TryGetValue(connectionId, out var playerId))
                {
                    s_ConnectionToPlayer.Remove(connectionId);
                    
                    // Notify other players
                    _ = Clients.Group(sessionId).SendAsync("OnPlayerLeft", new PlayerLeftEvent
                    {
                        PlayerId = playerId,
                        SessionId = sessionId
                    });
                }
            }
        }
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Send player input to the server (movement, actions).
    /// Input is processed by WorldService and included in next state broadcast.
    /// </summary>
    public async Task SendInput(InputPayload input)
    {
        if (string.IsNullOrEmpty(input.PlayerId))
        {
            return;
        }
        
        // Queue input for processing in next game tick
        _worldService.QueueInput(input);
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Request the current game state (for reconnection or initial sync).
    /// </summary>
    public async Task RequestState()
    {
        var connectionId = Context.ConnectionId;
        
        lock (s_Lock)
        {
            if (s_ConnectionToSession.TryGetValue(connectionId, out var sessionId))
            {
                var state = _worldService.GetSessionSnapshot(sessionId);
                if (state != null)
                {
                    _ = Clients.Caller.SendAsync("ReceiveGameState", state);
                }
            }
        }
        
        await Task.CompletedTask;
    }
    #endregion
}

#region SignalR DTOs
/// <summary>
/// Request to join a game session via SignalR.
/// </summary>
public class JoinSessionRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public string? CharacterType { get; set; }
    public string? Token { get; set; }
}

/// <summary>
/// Player input payload sent via SignalR.
/// </summary>
public class InputPayload
{
    public string PlayerId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public float MoveX { get; set; }
    public float MoveY { get; set; }
    public int Sequence { get; set; } // For client prediction reconciliation
    public bool Attack { get; set; }
    public bool Skill { get; set; }
    public float Timestamp { get; set; }
}

/// <summary>
/// Game state snapshot broadcast to all clients in session.
/// </summary>
public class GameStateSnapshot
{
    public int Sequence { get; set; } // Incremental sequence number
    public float ServerTime { get; set; } // Seconds since session start
    public int ConfirmedInputSequence { get; set; } // For client reconciliation
    
    public List<PlayerSnapshot> Players { get; set; } = new();
    public List<EnemySnapshot> Enemies { get; set; } = new();
    public List<ProjectileSnapshot> Projectiles { get; set; } = new();
}

/// <summary>
/// Player snapshot for state broadcast.
/// </summary>
public class PlayerSnapshot
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CharacterType { get; set; } = "lancer";
    public float X { get; set; }
    public float Y { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Level { get; set; }
    public string Status { get; set; } = "idle";
    public int LastConfirmedInputSequence { get; set; }
}

/// <summary>
/// Enemy snapshot for state broadcast (reuse existing if available).
/// </summary>
public class EnemySnapshot
{
    public string Id { get; set; } = string.Empty;
    public string TypeId { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public string Status { get; set; } = "idle";
}

/// <summary>
/// Projectile snapshot for state broadcast.
/// </summary>
public class ProjectileSnapshot
{
    public string Id { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
}

/// <summary>
/// Event when a player joins the session.
/// </summary>
public class PlayerJoinedEvent
{
    public string PlayerId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string CharacterType { get; set; } = "lancer";
}

/// <summary>
/// Event when a player leaves the session.
/// </summary>
public class PlayerLeftEvent
{
    public string PlayerId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
}

/// <summary>
/// Event when the game session ends.
/// </summary>
public class GameEndEvent
{
    public string SessionId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty; // "timeout", "all_players_dead", "completed"
}
#endregion

