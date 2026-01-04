namespace GameServer.Models.Dto;

public class SaveProgressRequest
{
    public Guid PlayerId { get; set; }
    public string? Token { get; set; }
}

public class DisconnectRequest
{
    public Guid PlayerId { get; set; }
    public string? Token { get; set; }
}

