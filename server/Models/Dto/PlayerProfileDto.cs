using System.Text.Json.Serialization;

namespace GameServer.Models.Dto;

public class PlayerProfileDto
{
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public int Exp { get; set; }
    public int Gold { get; set; }
    
    [JsonPropertyName("avatarPath")]
    public string? AvatarPath { get; set; }
}





