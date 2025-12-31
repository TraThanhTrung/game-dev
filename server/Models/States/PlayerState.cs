namespace GameServer.Models.States;

public class PlayerState
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Damage { get; set; }
    public float Range { get; set; }
    public float Speed { get; set; }
    public int Sequence { get; set; }
    public bool IsDead => Hp <= 0;
}

