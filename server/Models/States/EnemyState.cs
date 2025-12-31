namespace GameServer.Models.States;

public enum EnemyStatus
{
    Idle,
    Chasing,
    Attacking,
    Knockback
}

public class EnemyState
{
    public Guid Id { get; set; }
    public string TypeId { get; set; } = "enemy";
    public float X { get; set; }
    public float Y { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public float DetectRange { get; set; }
    public float AttackRange { get; set; }
    public float Speed { get; set; }
    public int Damage { get; set; }
    public EnemyStatus Status { get; set; } = EnemyStatus.Idle;
    public float AttackCooldown { get; set; }
    public float AttackTimer { get; set; }
    public bool IsDead => Hp <= 0;
}

