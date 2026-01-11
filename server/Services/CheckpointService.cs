using GameServer.Data;
using GameServer.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace GameServer.Services;

/// <summary>
/// Service for managing checkpoints and caching them in memory.
/// Uses IServiceProvider to create scopes for DbContext access (since this is a Singleton).
/// </summary>
public class CheckpointService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CheckpointService> _logger;
    private readonly ConcurrentDictionary<string, Checkpoint> _checkpointCache = new();

    public CheckpointService(IServiceProvider serviceProvider, ILogger<CheckpointService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Get all active checkpoints, cached in memory.
    /// Kept for backward compatibility - prefer section-based loading.
    /// </summary>
    public async Task<List<Checkpoint>> GetAllActiveCheckpointsAsync()
    {
        // Check cache first
        var cached = _checkpointCache.Values.Where(c => c.IsActive).ToList();
        if (cached.Any())
        {
            return cached;
        }

        // Load from database using a scope
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        
        var checkpoints = await db.Checkpoints
            .Include(c => c.Section)
            .Where(c => c.IsActive)
            .ToListAsync();

        // Update cache
        foreach (var checkpoint in checkpoints)
        {
            _checkpointCache.AddOrUpdate(
                checkpoint.CheckpointName,
                checkpoint,
                (key, oldValue) => checkpoint);
        }

        _logger.LogInformation("Loaded {Count} active checkpoints from database", checkpoints.Count);
        return checkpoints;
    }

    /// <summary>
    /// Get checkpoints for a specific GameSection.
    /// </summary>
    public async Task<List<Checkpoint>> GetCheckpointsBySectionAsync(int sectionId)
    {
        // Load from database using a scope (cache lookup by sectionId is complex, so always query)
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        
        var checkpoints = await db.Checkpoints
            .Include(c => c.Section)
            .Where(c => c.SectionId == sectionId && c.IsActive)
            .ToListAsync();

        // Update cache (for individual lookup later)
        foreach (var checkpoint in checkpoints)
        {
            _checkpointCache.AddOrUpdate(
                checkpoint.CheckpointName,
                checkpoint,
                (key, oldValue) => checkpoint);
        }

        _logger.LogInformation("Loaded {Count} active checkpoints for section {SectionId} from database", 
            checkpoints.Count, sectionId);
        return checkpoints;
    }

    /// <summary>
    /// Get checkpoints by GameSection name.
    /// </summary>
    public async Task<List<Checkpoint>> GetCheckpointsBySectionNameAsync(string sectionName)
    {
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            return new List<Checkpoint>();
        }

        // Load from database using a scope
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        
        var section = await db.GameSections
            .FirstOrDefaultAsync(s => s.Name == sectionName && s.IsActive);

        if (section == null)
        {
            _logger.LogWarning("GameSection '{SectionName}' not found or not active", sectionName);
            return new List<Checkpoint>();
        }

        return await GetCheckpointsBySectionAsync(section.SectionId);
    }

    /// <summary>
    /// Get checkpoint by name, cached in memory.
    /// </summary>
    public async Task<Checkpoint?> GetCheckpointAsync(string checkpointName)
    {
        // Check cache first
        if (_checkpointCache.TryGetValue(checkpointName, out var cached))
        {
            return cached;
        }

        // Load from database using a scope
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        
        var checkpoint = await db.Checkpoints
            .FirstOrDefaultAsync(c => c.CheckpointName == checkpointName);

        if (checkpoint != null)
        {
            _checkpointCache.AddOrUpdate(
                checkpointName,
                checkpoint,
                (key, oldValue) => checkpoint);
        }

        return checkpoint;
    }

    /// <summary>
    /// Invalidate checkpoint cache (call after updating checkpoints).
    /// </summary>
    public void InvalidateCache(string? checkpointName = null)
    {
        if (string.IsNullOrEmpty(checkpointName))
        {
            _checkpointCache.Clear();
            _logger.LogInformation("Cleared all checkpoint cache");
        }
        else
        {
            _checkpointCache.TryRemove(checkpointName, out _);
            _logger.LogInformation("Removed checkpoint {Name} from cache", checkpointName);
        }
    }

    /// <summary>
    /// Parse enemy pool JSON string to array of enemy type IDs.
    /// </summary>
    public static string[] ParseEnemyPool(string enemyPoolJson)
    {
        if (string.IsNullOrWhiteSpace(enemyPoolJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            var enemyTypes = JsonSerializer.Deserialize<string[]>(enemyPoolJson);
            return enemyTypes ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
