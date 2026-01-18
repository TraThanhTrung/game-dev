namespace GameServer.Models.Dto;

public class MatchResultDto
{
    public Guid SessionId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int PlayerCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<EnemyTypeInfoDto> Enemies { get; set; } = new List<EnemyTypeInfoDto>();
    public List<PlayerInfoDto> Players { get; set; } = new List<PlayerInfoDto>();
}




