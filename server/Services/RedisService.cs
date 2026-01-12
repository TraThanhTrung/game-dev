using StackExchange.Redis;
using System.Text.Json;
using GameServer.Models.Dto;

namespace GameServer.Services;

public class RedisService
{
    #region Private Fields
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisService> _logger;
    #endregion

    #region Constructor
    public RedisService(IConnectionMultiplexer redis, ILogger<RedisService> logger)
    {
        _redis = redis;
        _logger = logger;
    }
    #endregion

    #region Public Methods - Basic Cache
    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);

            if (!value.HasValue)
                return default;

            return JsonSerializer.Deserialize<T>(value.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting key {Key} from Redis", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var db = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(value);
            await db.StringSetAsync(key, json, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting key {Key} in Redis", key);
        }
    }

    public async Task DeleteAsync(string key)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting key {Key} from Redis", key);
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            var db = _redis.GetDatabase();
            return await db.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking key {Key} in Redis", key);
            return false;
        }
    }
    #endregion

    #region Public Methods - Enemy Config Cache
    public async Task<GameServer.Services.EnemyConfig?> GetEnemyConfigAsync(string typeId)
    {
        var key = $"enemy:config:{typeId}";
        return await GetAsync<GameServer.Services.EnemyConfig>(key);
    }

    public async Task SetEnemyConfigAsync(string typeId, GameServer.Services.EnemyConfig config)
    {
        var key = $"enemy:config:{typeId}";
        await SetAsync(key, config, TimeSpan.FromHours(24));
    }

    public async Task InvalidateEnemyConfigAsync(string typeId)
    {
        var key = $"enemy:config:{typeId}";
        await DeleteAsync(key);
    }

    public async Task InvalidateAllEnemyConfigsAsync()
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: "enemy:config:*");
            var db = _redis.GetDatabase();

            foreach (var key in keys)
            {
                await db.KeyDeleteAsync(key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating all enemy configs from Redis");
        }
    }
    #endregion

    #region Public Methods - Session State Cache
    /// <summary>
    /// Cache session metadata (short TTL, for REST API queries)
    /// </summary>
    public async Task<SessionMetadata?> GetSessionMetadataAsync(string sessionId)
    {
        var key = $"session:metadata:{sessionId}";
        return await GetAsync<SessionMetadata>(key);
    }

    public async Task SetSessionMetadataAsync(string sessionId, SessionMetadata metadata)
    {
        var key = $"session:metadata:{sessionId}";
        await SetAsync(key, metadata, TimeSpan.FromSeconds(5)); // Short TTL
    }

    public async Task InvalidateSessionMetadataAsync(string sessionId)
    {
        var key = $"session:metadata:{sessionId}";
        await DeleteAsync(key);
    }
    #endregion

    #region Public Methods - Player Profile Cache
    /// <summary>
    /// Cache player profile (medium TTL, for profile lookups)
    /// </summary>
    public async Task<PlayerProfileCache?> GetPlayerProfileAsync(Guid playerId)
    {
        var key = $"player:profile:{playerId}";
        return await GetAsync<PlayerProfileCache>(key);
    }

    public async Task SetPlayerProfileAsync(Guid playerId, PlayerProfileCache profile)
    {
        var key = $"player:profile:{playerId}";
        await SetAsync(key, profile, TimeSpan.FromMinutes(5)); // 5 min TTL
    }

    public async Task InvalidatePlayerProfileAsync(Guid playerId)
    {
        var key = $"player:profile:{playerId}";
        await DeleteAsync(key);
    }
    #endregion

    #region Public Methods - Game Section Cache
    /// <summary>
    /// Cache game section config (long TTL, static data)
    /// </summary>
    public async Task<GameSectionCache?> GetGameSectionAsync(int sectionId)
    {
        var key = $"game:section:{sectionId}";
        return await GetAsync<GameSectionCache>(key);
    }

    public async Task SetGameSectionAsync(int sectionId, GameSectionCache section)
    {
        var key = $"game:section:{sectionId}";
        await SetAsync(key, section, TimeSpan.FromHours(24)); // 24 hour TTL
    }

    public async Task InvalidateGameSectionAsync(int sectionId)
    {
        var key = $"game:section:{sectionId}";
        await DeleteAsync(key);
    }
    #endregion

    #region Public Methods - Checkpoint Cache
    /// <summary>
    /// Cache checkpoints by section (long TTL, static data)
    /// </summary>
    public async Task<List<CheckpointCache>?> GetCheckpointsBySectionAsync(int sectionId)
    {
        var key = $"game:checkpoints:section:{sectionId}";
        return await GetAsync<List<CheckpointCache>>(key);
    }

    public async Task SetCheckpointsBySectionAsync(int sectionId, List<CheckpointCache> checkpoints)
    {
        var key = $"game:checkpoints:section:{sectionId}";
        await SetAsync(key, checkpoints, TimeSpan.FromHours(24)); // 24 hour TTL
    }

    public async Task InvalidateCheckpointsBySectionAsync(int sectionId)
    {
        var key = $"game:checkpoints:section:{sectionId}";
        await DeleteAsync(key);
    }

    /// <summary>
    /// Invalidate all checkpoint caches (when checkpoint is updated/deleted)
    /// </summary>
    public async Task InvalidateAllCheckpointsAsync()
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: "game:checkpoints:*");
            var db = _redis.GetDatabase();

            foreach (var key in keys)
            {
                await db.KeyDeleteAsync(key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating all checkpoint caches from Redis");
        }
    }
    #endregion

    #region Public Methods - Temporary Skill Bonuses
    /// <summary>
    /// Get temporary skill bonuses from Redis for a player in a session.
    /// </summary>
    public async Task<TemporarySkillBonuses?> GetTemporarySkillBonusesAsync(string sessionId, Guid playerId)
    {
        var key = $"session:skills:{sessionId}:{playerId}";
        return await GetAsync<TemporarySkillBonuses>(key);
    }

    /// <summary>
    /// Set temporary skill bonuses in Redis with TTL (4 hours).
    /// </summary>
    public async Task SetTemporarySkillBonusesAsync(string sessionId, Guid playerId, TemporarySkillBonuses bonuses)
    {
        var key = $"session:skills:{sessionId}:{playerId}";
        bonuses.LastUpdated = DateTime.UtcNow;
        await SetAsync(key, bonuses, TimeSpan.FromHours(4)); // 4 hour TTL
    }

    /// <summary>
    /// Delete temporary skill bonuses from Redis (optional, when session ends).
    /// </summary>
    public async Task DeleteTemporarySkillBonusesAsync(string sessionId, Guid playerId)
    {
        var key = $"session:skills:{sessionId}:{playerId}";
        await DeleteAsync(key);
    }
    #endregion
}

#region Cache DTOs
/// <summary>
/// Session metadata for caching (lightweight version for REST queries)
/// </summary>
public class SessionMetadata
{
    public string SessionId { get; set; } = string.Empty;
    public int PlayerCount { get; set; }
    public List<PlayerInfo> Players { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public int Version { get; set; }
}

public class PlayerInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CharacterType { get; set; } = "lancer";
}

/// <summary>
/// Player profile for caching
/// </summary>
public class PlayerProfileCache
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public int Gold { get; set; }
    public string? AvatarPath { get; set; }
}

/// <summary>
/// Game section for caching
/// </summary>
public class GameSectionCache
{
    public int SectionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EnemyCount { get; set; }
    public int EnemyLevel { get; set; }
    public float SpawnRate { get; set; }
    public int? Duration { get; set; }
}

/// <summary>
/// Checkpoint for caching
/// </summary>
public class CheckpointCache
{
    public int CheckpointId { get; set; }
    public string CheckpointName { get; set; } = string.Empty;
    public int? SectionId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public string EnemyPool { get; set; } = "[]";
    public int MaxEnemies { get; set; }
    public bool IsActive { get; set; }
}
#endregion

