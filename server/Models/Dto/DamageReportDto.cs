namespace GameServer.Models.Dto;

public class DamageReportRequest
{
    public Guid PlayerId { get; set; }
    public int DamageAmount { get; set; }
    public string SessionId { get; set; } = "default";
    public string Token { get; set; } = string.Empty;
}

public class DamageReportResponse
{
    public bool Accepted { get; set; }
    public int CurrentHp { get; set; }
    public int MaxHp { get; set; }
}

