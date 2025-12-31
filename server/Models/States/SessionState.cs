namespace GameServer.Models.States;

public class SessionState
{
    public string SessionId { get; set; } = "default";
    public int Version { get; set; }
    public Dictionary<Guid, PlayerState> Players { get; set; } = new();
    public Dictionary<Guid, EnemyState> Enemies { get; set; } = new();
    public Dictionary<Guid, ProjectileState> Projectiles { get; set; } = new();
}

