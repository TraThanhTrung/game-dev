namespace GameServer.Models.Dto;

public class SkillUpgradeRequest
{
    public string PlayerId { get; set; } = string.Empty;
    public string SkillId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}

public class SkillUpgradeResponse
{
    public bool Success { get; set; }
    public string SkillId { get; set; } = string.Empty;
    public int Level { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class GetSkillsRequest
{
    public string PlayerId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}

public class GetSkillsResponse
{
    public List<SkillInfo> Skills { get; set; } = new();
}

public class SkillInfo
{
    public string SkillId { get; set; } = string.Empty;
    public int Level { get; set; }
}




