using System.Text.Json;
using GameServer.Data;
using GameServer.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameServer.Scripts;

public static class MigrateEnemiesFromConfig
{
    public static async Task RunAsync(GameDbContext db, ILogger logger, string configPath)
    {
        logger.LogInformation("Starting enemy migration from config.json");

        if (!File.Exists(configPath))
        {
            logger.LogWarning("Config file not found at {Path}", configPath);
            return;
        }

        var json = await File.ReadAllTextAsync(configPath);
        var config = JsonSerializer.Deserialize<GameConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (config?.EnemyStats == null || config.EnemyStats.Count == 0)
        {
            logger.LogWarning("No enemies found in config file");
            return;
        }

        int imported = 0;
        int skipped = 0;

        foreach (var enemyConfig in config.EnemyStats)
        {
            // Check if enemy already exists
            var exists = await db.Enemies
                .AnyAsync(e => e.TypeId == enemyConfig.TypeId);

            if (exists)
            {
                logger.LogInformation("Enemy {TypeId} already exists, skipping", enemyConfig.TypeId);
                skipped++;
                continue;
            }

            var enemy = new Enemy
            {
                TypeId = enemyConfig.TypeId,
                Name = enemyConfig.TypeId, // Use TypeId as name if not provided
                ExpReward = enemyConfig.ExpReward,
                GoldReward = enemyConfig.GoldReward,
                MaxHealth = enemyConfig.MaxHealth,
                Damage = enemyConfig.Damage,
                Speed = enemyConfig.Speed,
                DetectRange = enemyConfig.DetectRange,
                AttackRange = enemyConfig.AttackRange,
                AttackCooldown = enemyConfig.AttackCooldown,
                WeaponRange = enemyConfig.WeaponRange,
                KnockbackForce = enemyConfig.KnockbackForce,
                StunTime = enemyConfig.StunTime,
                RespawnDelay = enemyConfig.RespawnDelay,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            db.Enemies.Add(enemy);
            imported++;
            logger.LogInformation("Imported enemy: {TypeId}", enemyConfig.TypeId);
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Migration completed: {Imported} imported, {Skipped} skipped", imported, skipped);
    }
}

#region Config Models (duplicated from GameConfigService for script use)
public class GameConfig
{
    public List<EnemyConfig>? EnemyStats { get; set; }
}

public class EnemyConfig
{
    public string TypeId { get; set; } = "enemy";
    public int ExpReward { get; set; }
    public int GoldReward { get; set; }
    public int MaxHealth { get; set; }
    public int Damage { get; set; }
    public float Speed { get; set; }
    public float DetectRange { get; set; }
    public float AttackRange { get; set; }
    public float AttackCooldown { get; set; }
    public float WeaponRange { get; set; }
    public float KnockbackForce { get; set; }
    public float StunTime { get; set; }
    public float RespawnDelay { get; set; }
}
#endregion



