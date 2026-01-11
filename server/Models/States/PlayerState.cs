namespace GameServer.Models.States;

public class PlayerState
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Sequence { get; set; }
    public bool IsDead => Hp <= 0;

    // Progression
    public int Level { get; set; } = 1;
    public int Exp { get; set; }
    public int ExpToLevel { get; set; }
    public int Gold { get; set; }

    // Combat stats (synced from database PlayerStats)
    public int Damage { get; set; }
    public float Range { get; set; }
    public float Speed { get; set; }
    public float WeaponRange { get; set; }
    public float KnockbackForce { get; set; }
    public float KnockbackTime { get; set; }
    public float StunTime { get; set; }
    public float BonusDamagePercent { get; set; }
    public float DamageReductionPercent { get; set; }
}

