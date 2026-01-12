namespace GameServer.Models.States;

public enum SessionStatus
{
    InProgress,    // Đang chơi
    Completed,     // Hoàn thành tất cả sections
    Failed         // Tất cả players chết
}

public class SessionState
{
    public string SessionId { get; set; } = "default";
    public int Version { get; set; }
    public Dictionary<Guid, PlayerState> Players { get; set; } = new();
    public Dictionary<Guid, EnemyState> Enemies { get; set; } = new();
    public Dictionary<Guid, ProjectileState> Projectiles { get; set; } = new();

    // Section progression tracking
    public int? CurrentSectionId { get; set; }
    public DateTime? SectionStartTime { get; set; }
    public List<int> CompletedSections { get; set; } = new();

    // Session status
    public SessionStatus Status { get; set; } = SessionStatus.InProgress;

    // Boss tracking
    public Guid? CurrentBossId { get; set; }
    public bool IsBossAlive { get; set; } = false;

    #region Cached Section Data (populated on section init, used by ProcessEnemyRespawns)
    /// <summary>
    /// Cached section config to avoid Redis/DB queries every tick.
    /// Populated when section is initialized via InitializeRoomCheckpointsAsync().
    /// </summary>
    public CachedSectionConfig? CachedSection { get; set; }

    /// <summary>
    /// Cached checkpoint configs indexed by CheckpointId.
    /// Populated when section is initialized.
    /// </summary>
    public Dictionary<int, CachedCheckpointConfig> CachedCheckpoints { get; set; } = new();
    #endregion
}

#region Cached Config Classes
/// <summary>
/// In-memory cache of section config to avoid DB/Redis queries during game loop.
/// </summary>
public class CachedSectionConfig
{
    public int SectionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EnemyCount { get; set; }
    public int EnemyLevel { get; set; }
    public float SpawnRate { get; set; }
    public float Duration { get; set; }
}

/// <summary>
/// In-memory cache of checkpoint config for respawn limitation checks.
/// </summary>
public class CachedCheckpointConfig
{
    public int CheckpointId { get; set; }
    public int MaxEnemies { get; set; }
}
#endregion

