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
    int Gold
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

