using GameServer.Data;
using GameServer.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Services;

public class EnemyConfigService
{
    #region Private Fields
    private readonly GameDbContext _db;
    private readonly RedisService _redis;
    private readonly ILogger<EnemyConfigService> _logger;
    #endregion

    #region Constructor
    public EnemyConfigService(
        GameDbContext db,
        RedisService redis,
        ILogger<EnemyConfigService> logger)
    {
        _db = db;
        _redis = redis;
        _logger = logger;
    }
    #endregion

    #region Public Methods
    public async Task<GameServer.Services.EnemyConfig?> GetEnemyAsync(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId))
            return null;

        // Try Redis cache first
        var cached = await _redis.GetEnemyConfigAsync(typeId);
        if (cached != null)
            return cached;

        // Load from database
        var enemy = await _db.Enemies
            .FirstOrDefaultAsync(e => e.TypeId == typeId && e.IsActive);

        if (enemy == null)
            return null;

        var config = new GameServer.Services.EnemyConfig
        {
            TypeId = enemy.TypeId,
            ExpReward = enemy.ExpReward,
            GoldReward = enemy.GoldReward,
            MaxHealth = enemy.MaxHealth,
            Damage = enemy.Damage,
            Speed = enemy.Speed,
            DetectRange = enemy.DetectRange,
            AttackRange = enemy.AttackRange,
            AttackCooldown = enemy.AttackCooldown,
            WeaponRange = enemy.WeaponRange,
            KnockbackForce = enemy.KnockbackForce,
            StunTime = enemy.StunTime,
            RespawnDelay = enemy.RespawnDelay
        };

        // Cache in Redis
        await _redis.SetEnemyConfigAsync(typeId, config);

        return config;
    }

    public async Task<List<GameServer.Services.EnemyConfig>> GetAllEnemiesAsync()
    {
        var enemies = await _db.Enemies
            .Where(e => e.IsActive)
            .ToListAsync();

        var configs = new List<GameServer.Services.EnemyConfig>();
        foreach (var enemy in enemies)
        {
            var config = await GetEnemyAsync(enemy.TypeId);
            if (config != null)
                configs.Add(config);
        }

        return configs;
    }

    public async Task InvalidateCacheAsync(string? typeId = null)
    {
        if (string.IsNullOrWhiteSpace(typeId))
        {
            await _redis.InvalidateAllEnemyConfigsAsync();
        }
        else
        {
            await _redis.InvalidateEnemyConfigAsync(typeId);
        }
    }
    #endregion
}

