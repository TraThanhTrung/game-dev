using GameServer.Areas.Player.Models;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Areas.Player.Pages;

/// <summary>
/// Dashboard/Home page for players.
/// </summary>
public class IndexModel : BasePlayerPageModel
{
    #region Private Fields
    private readonly PlayerWebService _playerWebService;
    #endregion

    #region Public Properties
    public PlayerProfileViewModel? Profile { get; set; }
    public int TotalMatches { get; set; }
    public int TotalPlayTime { get; set; } // in seconds
    public int RecentMatchesCount { get; set; }
    #endregion

    #region Constructor
    public IndexModel(PlayerWebService playerWebService)
    {
        _playerWebService = playerWebService;
    }
    #endregion

    #region Public Methods
    public async Task<IActionResult> OnGetAsync()
    {
        if (!PlayerId.HasValue)
        {
            return RedirectToLogin();
        }

        // Load player profile
        var player = await _playerWebService.GetPlayerProfileAsync(PlayerId.Value);
        if (player == null)
        {
            return RedirectToLogin();
        }

        // Map to view model
        Profile = new PlayerProfileViewModel
        {
            PlayerId = player.Id,
            Name = player.Name,
            Level = player.Level,
            Exp = player.Exp,
            ExpToLevel = player.ExpToLevel,
            Gold = player.Gold,
            CreatedAt = player.CreatedAt,
            InventoryItemCount = player.Inventory?.Count ?? 0,
            SkillCount = player.Skills?.Count ?? 0,
            Stats = new PlayerStatsViewModel
            {
                Damage = player.Stats?.Damage ?? 0,
                Range = player.Stats?.Range ?? 0,
                KnockbackForce = player.Stats?.KnockbackForce ?? 0,
                Speed = player.Stats?.Speed ?? 0,
                MaxHealth = player.Stats?.MaxHealth ?? 0,
                CurrentHealth = player.Stats?.CurrentHealth ?? 0
            }
        };

        // Load statistics
        TotalMatches = await _playerWebService.GetPlayerMatchCountAsync(PlayerId.Value);
        TotalPlayTime = await _playerWebService.GetPlayerPlayTimeAsync(PlayerId.Value);
        
        var recentSessions = await _playerWebService.GetPlayerSessionsAsync(PlayerId.Value, page: 1, pageSize: 5);
        RecentMatchesCount = recentSessions.Count;

        return Page();
    }
    #endregion
}



