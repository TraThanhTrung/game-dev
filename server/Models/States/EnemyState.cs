namespace GameServer.Models.States;

public enum EnemyStatus
{
    Idle,
    Chasing,
    Attacking,
    Knockback,
    Dead
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

    // Respawn
    public float SpawnX { get; set; }
    public float SpawnY { get; set; }
    public float RespawnTimer { get; set; }
    public float RespawnDelay { get; set; } = 5f;
    public float BaseRespawnDelay { get; set; } = 5f; // Base delay before SpawnRate adjustment

    // Rewards
    public int ExpReward { get; set; } = 25;
    public int GoldReward { get; set; } = 10;

    // Tracking fields (for respawn limitation + section progression)
    public int? CheckpointId { get; set; } // ID của checkpoint spawn enemy này
    public int? SectionId { get; set; } // ID của section chứa checkpoint
    public int EnemyLevel { get; set; } = 1; // Level của enemy (scaled from GameSection.EnemyLevel)
    
    // Boss identification
    public bool IsBoss { get; set; } = false;
}

