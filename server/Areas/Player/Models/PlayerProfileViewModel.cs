namespace GameServer.Areas.Player.Models;

/// <summary>
/// View model for player profile page.
/// </summary>
public class PlayerProfileViewModel
{
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool EmailVerified { get; set; }
    public bool HasGoogleAccount { get; set; }
    public string? AvatarPath { get; set; }
    public int Level { get; set; }
    public int Exp { get; set; }
    public int ExpToLevel { get; set; }
    public int Gold { get; set; }
    public DateTime CreatedAt { get; set; }
    public PlayerStatsViewModel Stats { get; set; } = new();
    public int InventoryItemCount { get; set; }
    public int SkillCount { get; set; }
}

