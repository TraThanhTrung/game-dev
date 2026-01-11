namespace GameServer.Models.Dto;

/// <summary>
/// Request to report damage from player to enemy.
/// </summary>
public class EnemyDamageRequest
{
    public Guid PlayerId { get; set; }
    public Guid EnemyId { get; set; }
    public int DamageAmount { get; set; }
    public string SessionId { get; set; } = "default";
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// Response for enemy damage report.
/// </summary>
public class EnemyDamageResponse
{
    public bool Accepted { get; set; }
    public int CurrentHp { get; set; }
    public int MaxHp { get; set; }
    public bool IsDead { get; set; }
}

