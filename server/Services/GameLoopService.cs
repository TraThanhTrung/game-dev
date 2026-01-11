using GameServer.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

namespace GameServer.Services;

/// <summary>
/// Background service that runs the game loop at 20 Hz (50ms intervals).
/// Broadcasts game state to all connected clients via SignalR after each tick.
/// </summary>
public class GameLoopService : BackgroundService
{
    #region Private Fields
    private readonly WorldService _world;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly ILogger<GameLoopService> _logger;
    private readonly TimeSpan _tickInterval = TimeSpan.FromMilliseconds(50); // 20 Hz
    #endregion

    #region Constructor
    public GameLoopService(
        WorldService world, 
        IHubContext<GameHub> hubContext,
        ILogger<GameLoopService> logger)
    {
        _world = world;
        _hubContext = hubContext;
        _logger = logger;
    }
    #endregion

    #region BackgroundService Implementation
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[GameLoop] Starting game loop at 20 Hz (50ms intervals)");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            var started = DateTime.UtcNow;
            
            // Process game tick (all game logic)
            await _world.TickAsync(stoppingToken);
            
            // Broadcast state to all connected clients via SignalR
            await BroadcastStateAsync();
            
            var elapsed = DateTime.UtcNow - started;
            var delay = _tickInterval - elapsed;
            
            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
            else
            {
                // Log if tick took too long
                _logger.LogWarning("[GameLoop] Tick took {Elapsed}ms, exceeding target {Target}ms",
                    elapsed.TotalMilliseconds, _tickInterval.TotalMilliseconds);
            }
        }
        
        _logger.LogInformation("[GameLoop] Game loop stopped");
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Broadcast game state to all connected clients in each active session.
    /// </summary>
    private async Task BroadcastStateAsync()
    {
        var sessionIds = _world.GetActiveSessionIds();
        
        foreach (var sessionId in sessionIds)
        {
            var snapshot = _world.GetSessionSnapshot(sessionId);
            if (snapshot != null && (snapshot.Players.Count > 0 || snapshot.Enemies.Count > 0))
            {
                try
                {
                    await _hubContext.Clients.Group(sessionId)
                        .SendAsync("ReceiveGameState", snapshot);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[GameLoop] Failed to broadcast state to session {SessionId}", sessionId);
                }
            }
        }
    }
    #endregion
}
