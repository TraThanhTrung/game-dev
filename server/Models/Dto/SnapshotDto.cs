using GameServer.Models.States;

namespace GameServer.Models.Dto;

public record PlayerSnapshot(
    Guid Id, 
    string Name, 
    float X, 
    float Y, 
    int Hp, 
    int MaxHp, 
    int Sequence,
    int Level,
    int Exp,
    int ExpToLevel,
    int Gold,
    // Player stats (synced from database)
    int Damage,
    float Range,
    float Speed,
    float WeaponRange,
    float KnockbackForce,
    float KnockbackTime,
    float StunTime,
    float BonusDamagePercent,
    float DamageReductionPercent
);

public record EnemySnapshot(Guid Id, string TypeId, float X, float Y, int Hp, int MaxHp, string Status);

public record ProjectileSnapshot(Guid Id, Guid OwnerId, float X, float Y, float DirX, float DirY, float Radius);

public class StateResponse
{
    public string SessionId { get; set; } = "default";
    public int Version { get; set; }
    public List<PlayerSnapshot> Players { get; set; } = new();
    public List<EnemySnapshot> Enemies { get; set; } = new();
    public List<ProjectileSnapshot> Projectiles { get; set; } = new();
}

#region Session Metadata DTOs

/// <summary>
/// Response for session metadata (used during loading screen).
/// </summary>
public class SessionMetadataResponse
{
    public string SessionId { get; set; } = string.Empty;
    public int PlayerCount { get; set; }
    public int Version { get; set; }
    public List<PlayerMetadata> Players { get; set; } = new();
}

/// <summary>
/// Player metadata for session info.
/// </summary>
public class PlayerMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CharacterType { get; set; } = "lancer";
    public int Level { get; set; }
}

/// <summary>
/// Request to signal client is ready for SignalR connection.
/// </summary>
public class ReadyRequest
{
    public Guid PlayerId { get; set; }
    public string? CharacterType { get; set; }
}

#endregion

