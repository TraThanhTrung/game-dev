using GameServer.Areas.Player.Models;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Areas.Player.Pages;

/// <summary>
/// Stats page for players to view detailed statistics.
/// </summary>
public class StatsModel : BasePlayerPageModel
{
    #region Private Fields
    private readonly PlayerWebService _playerWebService;
    #endregion

    #region Public Properties
    public PlayerStatsViewModel? Stats { get; set; }
    public PlayerProfileViewModel? Profile { get; set; }
    #endregion

    #region Constructor
    public StatsModel(PlayerWebService playerWebService)
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

        // Map profile
        Profile = new PlayerProfileViewModel
        {
            PlayerId = player.Id,
            Name = player.Name,
            Level = player.Level,
            Exp = player.Exp,
            ExpToLevel = player.ExpToLevel,
            Gold = player.Gold,
            CreatedAt = player.CreatedAt
        };

        // Map stats
        if (player.Stats != null)
        {
            Stats = new PlayerStatsViewModel
            {
                Damage = player.Stats.Damage,
                Range = player.Stats.Range,
                KnockbackForce = player.Stats.KnockbackForce,
                Speed = player.Stats.Speed,
                MaxHealth = player.Stats.MaxHealth,
                CurrentHealth = player.Stats.CurrentHealth
            };
        }
        else
        {
            Stats = new PlayerStatsViewModel();
        }

        return Page();
    }
    #endregion
}

