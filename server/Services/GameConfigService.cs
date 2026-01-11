using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GameServer.Services;

/// <summary>
/// Game configuration service.
/// Loads playerDefaults (for new player creation), expCurve, and polling settings from game-config.json.
/// After player is created, stats are loaded from database only (no fallback).
/// Enemy stats are loaded from database via EnemyConfigService.
/// </summary>
public class GameConfigService
{
    #region Private Fields
    private readonly ILogger<GameConfigService> _logger;
    private readonly GameConfig _config;
    #endregion

    #region Public Properties
    /// <summary>
    /// Player defaults from game-config.json. Used ONLY when creating new players.
    /// After creation, player data is loaded from database (no fallback to this).
    /// </summary>
    public PlayerDefaults PlayerDefaults => _config.PlayerDefaults ?? new PlayerDefaults();
    public ExpCurve ExpCurve => _config.ExpCurve ?? new ExpCurve();
    public PollingSettings Polling => _config.Polling ?? new PollingSettings();
    #endregion

    #region Constructor
    public GameConfigService(ILogger<GameConfigService> logger)
    {
        _logger = logger;
        var path = ResolveConfigPath();
        _config = LoadConfig(path);
    }
    #endregion

    #region Public Methods
    public int GetExpForNextLevel(int currentLevel)
    {
        var curve = ExpCurve;

        // Level 0 → 1: basePerLevel (no multiplier)
        if (currentLevel < 1)
        {
            return curve.BasePerLevel;
        }

        // Level 1+: basePerLevel * (1 + growthMultiplier * currentLevel)
        var levelCap = Math.Max(1, curve.LevelCap);
        var level = Math.Clamp(currentLevel, 1, levelCap);
        return (int)(curve.BasePerLevel * (1 + curve.GrowthMultiplier * level));
    }
    #endregion

    #region Private Methods
    private GameConfig LoadConfig(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                _logger.LogWarning("Game config not found at {Path}. Using defaults.", path);
                return new GameConfig();
            }

            var json = File.ReadAllText(path);
            var cfg = JsonSerializer.Deserialize<GameConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (cfg == null)
            {
                _logger.LogWarning("Game config at {Path} could not be parsed. Using defaults.", path);
                return new GameConfig();
            }

            _logger.LogInformation("Game config loaded from {Path}", path);
            return cfg;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load game config. Using defaults.");
            return new GameConfig();
        }
    }

    private string ResolveConfigPath()
    {
        var envPath = Environment.GetEnvironmentVariable("GAME_CONFIG_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            return Path.GetFullPath(envPath);
        }

        // Default: ../../shared/game-config.json relative to server bin directory
        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "shared", "game-config.json"));
        if (File.Exists(candidate))
        {
            return candidate;
        }

        // Fallback: ../shared/game-config.json
        candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "shared", "game-config.json"));
        return candidate;
    }
    #endregion
}

#region Config Models
/// <summary>
/// Game configuration data structure.
/// PlayerDefaults is used ONLY when creating new players.
/// After creation, player data is loaded from database (no fallback).
/// </summary>
public class GameConfig
{
    public PlayerDefaults? PlayerDefaults { get; set; } = new();
    public ExpCurve? ExpCurve { get; set; } = new();
    public PollingSettings? Polling { get; set; } = new();
}

/// <summary>
/// Default values for new players. Used ONLY at player creation time.
/// </summary>
public class PlayerDefaults
{
    public int Level { get; set; } = 0;
    public int Exp { get; set; } = 0;
    public int Gold { get; set; } = 0;
    public float SpawnX { get; set; } = -16f;
    public float SpawnY { get; set; } = 12f;
    public PlayerStatBlock Stats { get; set; } = new();
}

/// <summary>
/// Default stats for new players. Used ONLY at player creation time.
/// </summary>
public class PlayerStatBlock
{
    public int Damage { get; set; } = 1;
    public float WeaponRange { get; set; } = 0.5f;
    public float KnockbackForce { get; set; } = 5.5f;
    public float KnockbackTime { get; set; } = 0.2f;
    public float StunTime { get; set; } = 0.3f;
    public float Speed { get; set; } = 4f;
    public int MaxHealth { get; set; } = 25;
    public int CurrentHealth { get; set; } = 25;
    public float BonusDamagePercent { get; set; } = 0f;
    public float DamageReductionPercent { get; set; } = 0f;
}

public class ExpCurve
{
    public int BasePerLevel { get; set; } = 10;
    public float GrowthMultiplier { get; set; } = 0.2f;
    public int LevelCap { get; set; } = 100;
}

public class PollingSettings
{
    public float StateIntervalSeconds { get; set; } = 0.2f;
    public float LerpSpeed { get; set; } = 15f;
    public float PositionChangeThreshold { get; set; } = 0.01f;
}

/// <summary>
/// Enemy configuration data structure (loaded from database via EnemyConfigService).
/// This class is kept for type compatibility with EnemyConfigService.
/// </summary>
public class EnemyConfig
{
    public string TypeId { get; set; } = "enemy";
    public int ExpReward { get; set; } = 25;
    public int GoldReward { get; set; } = 10;
    public int MaxHealth { get; set; } = 30;
    public int Damage { get; set; } = 5;
    public float Speed { get; set; } = 2f;
    public float DetectRange { get; set; } = 6f;
    public float AttackRange { get; set; } = 1.2f;
    public float AttackCooldown { get; set; } = 2.0f;
    public float WeaponRange { get; set; } = 1.2f;
    public float KnockbackForce { get; set; } = 2.8f;
    public float StunTime { get; set; } = 0.28f;
    public float RespawnDelay { get; set; } = 5f;
}
#endregion
