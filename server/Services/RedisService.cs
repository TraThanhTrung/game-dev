using StackExchange.Redis;
using System.Text.Json;

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
}

