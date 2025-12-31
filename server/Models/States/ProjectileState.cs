namespace GameServer.Models.States;

public class ProjectileState
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float DirX { get; set; }
    public float DirY { get; set; }
    public float Speed { get; set; }
    public int Damage { get; set; }
    public float Radius { get; set; } = 0.2f;
    public float TimeToLive { get; set; }
}

