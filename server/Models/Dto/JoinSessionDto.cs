namespace GameServer.Models.Dto;

public class JoinSessionRequest
{
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string SessionId { get; set; } = "default";
    public string Token { get; set; } = string.Empty;
}

public class JoinSessionResponse
{
    public string SessionId { get; set; } = "default";
}

