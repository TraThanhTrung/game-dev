namespace GameServer.Areas.Player.Models;

/// <summary>
/// View model for player stats.
/// </summary>
public class PlayerStatsViewModel
{
    public int Damage { get; set; }
    public float Range { get; set; }
    public float KnockbackForce { get; set; }
    public float Speed { get; set; }
    public int MaxHealth { get; set; }
    public int CurrentHealth { get; set; }
}

