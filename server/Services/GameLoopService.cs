using Microsoft.Extensions.Hosting;

namespace GameServer.Services;

/// <summary>
/// Background service that runs the game loop at 20 Hz (50ms intervals).
/// Processes game ticks and updates game state.
/// </summary>
public class GameLoopService : BackgroundService
{
    #region Private Fields
    private readonly WorldService _world;
    private readonly ILogger<GameLoopService> _logger;
    private readonly TimeSpan _tickInterval = TimeSpan.FromMilliseconds(50); // 20 Hz
    #endregion

    #region Constructor
    public GameLoopService(
        WorldService world, 
        ILogger<GameLoopService> logger)
    {
        _world = world;
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
}
