using Microsoft.Extensions.Hosting;

namespace GameServer.Services;

public class GameLoopService : BackgroundService
{
    private readonly WorldService _world;
    private readonly TimeSpan _tickInterval = TimeSpan.FromMilliseconds(50);

    public GameLoopService(WorldService world)
    {
        _world = world;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var started = DateTime.UtcNow;
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
        }
    }
}

