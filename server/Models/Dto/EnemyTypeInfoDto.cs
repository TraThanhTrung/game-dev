namespace GameServer.Models.Dto;

public class EnemyTypeInfoDto
{
    public string EnemyTypeId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string? CheckpointName { get; set; }
}

