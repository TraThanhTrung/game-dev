namespace GameServer.Models.Dto;

public class PlayerInfoDto
{
    public Guid PlayerId { get; set; }
    public string? AvatarPath { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public int Gold { get; set; }
}

