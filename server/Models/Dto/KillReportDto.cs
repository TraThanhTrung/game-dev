namespace GameServer.Models.Dto;

public class KillReportRequest
{
    public Guid PlayerId { get; set; }
    public string EnemyTypeId { get; set; } = string.Empty;
    public string SessionId { get; set; } = "default";
    public string Token { get; set; } = string.Empty;
}

public class KillReportResponse
{
    public bool Granted { get; set; }
    public int Level { get; set; }
    public int Exp { get; set; }
    public int Gold { get; set; }
}

