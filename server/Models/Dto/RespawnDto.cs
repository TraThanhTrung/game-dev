namespace GameServer.Models.Dto;

public class RespawnRequest
{
    public Guid PlayerId { get; set; }
    public string SessionId { get; set; } = "default";
    public string Token { get; set; } = string.Empty;
}

public class RespawnResponse
{
    public bool Accepted { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public int CurrentHp { get; set; }
    public int MaxHp { get; set; }
}

